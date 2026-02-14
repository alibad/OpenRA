#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Threading.Tasks;
using Grpc.Core;
using RLProto = OpenRA.Mods.Common.RL;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// gRPC service implementation for the RL Bridge.
	/// Bridges between the gRPC streaming protocol and the game thread
	/// via System.Threading.Channels on ExternalBotBridge.
	/// Uses ExternalBotBridge.ActiveBridge to always reach the current active instance.
	/// </summary>
	public sealed class RLBridgeService : RLProto.RLBridge.RLBridgeBase
	{
		/// <summary>
		/// Bidirectional streaming: game sends observations, agent sends actions.
		/// Lock-step protocol: for each observation written, we wait for one action.
		/// </summary>
		public override async Task GameSession(
			IAsyncStreamReader<RLProto.AgentAction> requestStream,
			IServerStreamWriter<RLProto.GameObservation> responseStream,
			ServerCallContext context)
		{
			var bridge = ExternalBotBridge.ActiveBridge;
			if (bridge == null || !bridge.IsEnabled)
			{
				Log.Write("rl-bridge", "GameSession rejected: no active bridge");
				return;
			}

			bridge.OnAgentConnected();
			Log.Write("rl-bridge", "GameSession started");

			try
			{
				while (!context.CancellationToken.IsCancellationRequested)
				{
					// Wait for the game thread to produce an observation
					var observation = await bridge.ObservationReader.ReadAsync(context.CancellationToken);

					// Send observation to the agent
					await responseStream.WriteAsync(observation, context.CancellationToken);

					// If the game is over, send the final observation and stop
					if (observation.Done)
					{
						Log.Write("rl-bridge", $"Game over: {observation.Result}");
						break;
					}

					// Wait for the agent to send an action
					if (!await requestStream.MoveNext(context.CancellationToken))
					{
						Log.Write("rl-bridge", "Agent stream ended");
						break;
					}

					var action = requestStream.Current;

					// Push the action to the game thread
					await bridge.ActionWriter.WriteAsync(action, context.CancellationToken);
				}
			}
			finally
			{
				bridge.OnAgentDisconnected();
				Log.Write("rl-bridge", "GameSession ended");
			}
		}

		/// <summary>
		/// Unary RPC: query current game state on demand.
		/// </summary>
		public override Task<RLProto.GameState> GetState(
			RLProto.StateRequest request,
			ServerCallContext context)
		{
			var bridge = ExternalBotBridge.ActiveBridge;
			if (bridge == null)
			{
				return Task.FromResult(new RLProto.GameState
				{
					Phase = "no_bridge"
				});
			}

			return Task.FromResult(bridge.GetCurrentState());
		}
	}
}
