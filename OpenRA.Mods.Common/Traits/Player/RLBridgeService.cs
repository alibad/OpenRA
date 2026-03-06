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

using System;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Grpc.Core;
using RLProto = OpenRA.Mods.Common.RL;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// gRPC service implementation for the RL Bridge.
	/// Bridges between the gRPC streaming protocol and the game thread
	/// via System.Threading.Channels on ExternalBotBridge.
	///
	/// Observation sending and action receiving run as independent async loops
	/// so the game thread is never blocked waiting for agent responses.
	/// </summary>
	public sealed class RLBridgeService : RLProto.RLBridge.RLBridgeBase
	{
		/// <summary>
		/// Bidirectional streaming: game sends observations, agent sends actions.
		/// The two directions are fully decoupled — observations flow continuously
		/// and actions are processed whenever the agent sends them.
		/// </summary>
		public override async Task GameSession(
			IAsyncStreamReader<RLProto.AgentAction> requestStream,
			IServerStreamWriter<RLProto.GameObservation> responseStream,
			ServerCallContext context)
		{
			var bridge = ExternalBotBridge.ActiveBridge;

			// Wait up to 60s for bridge to activate (custom maps take longer to load)
			for (var i = 0; i < 600 && (bridge == null || !bridge.IsEnabled); i++)
			{
				await Task.Delay(100, context.CancellationToken);
				bridge = ExternalBotBridge.ActiveBridge;
			}

			if (bridge == null || !bridge.IsEnabled)
			{
				Log.Write("rl-bridge", "GameSession rejected: bridge not activated within 60s timeout");
				return;
			}

			bridge.OnAgentConnected();
			Log.Write("rl-bridge", "GameSession started");

			var ct = context.CancellationToken;

			try
			{
				// Observation sender: game → agent (runs independently)
				var obsTask = Task.Run(async () =>
				{
					try
					{
						while (!ct.IsCancellationRequested)
						{
							var obs = await bridge.ObservationReader.ReadAsync(ct);
							await responseStream.WriteAsync(obs, ct);
							if (obs.Done)
							{
								Log.Write("rl-bridge", $"Game over: {obs.Result}");
								break;
							}
						}
					}
					catch (OperationCanceledException) { }
					catch (ChannelClosedException) { }
				}, ct);

				// Action receiver: agent → game (runs independently)
				var actionTask = Task.Run(async () =>
				{
					try
					{
						while (await requestStream.MoveNext(ct))
							await bridge.ActionWriter.WriteAsync(requestStream.Current, ct);
					}
					catch (OperationCanceledException) { }
					catch (ChannelClosedException) { }
				}, ct);

				// Exit when either loop ends (game over, disconnect, or cancel)
				await Task.WhenAny(obsTask, actionTask);
			}
			finally
			{
				bridge.OnAgentDisconnected();
				Log.Write("rl-bridge", "GameSession ended");
			}
		}

		/// <summary>
		/// Unary RPC: advance N ticks with optional commands, return observation.
		/// Bypasses streaming — works reliably on all platforms including aarch64.
		/// </summary>
		public override async Task<RLProto.GameObservation> FastAdvance(
			RLProto.FastAdvanceRequest request,
			ServerCallContext context)
		{
			var bridge = ExternalBotBridge.ActiveBridge;

			// Wait up to 60s for bridge to activate (custom maps take longer to load)
			for (var i = 0; i < 600 && (bridge == null || !bridge.IsEnabled); i++)
			{
				await Task.Delay(100, context.CancellationToken);
				bridge = ExternalBotBridge.ActiveBridge;
			}

			if (bridge == null || !bridge.IsEnabled)
				throw new RpcException(new Status(StatusCode.Unavailable, "Bridge not activated within 60s"));

			return await bridge.RequestFastAdvance(
				request.Ticks, request.Commands, context.CancellationToken);
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
