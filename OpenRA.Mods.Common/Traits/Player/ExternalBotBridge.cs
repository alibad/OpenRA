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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenRA.Traits;
using RLProto = OpenRA.Mods.Common.RL;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("External bot bridge for reinforcement learning agents. " +
		"Hosts a gRPC server that streams observations and receives actions.")]
	[TraitLocation(SystemActors.Player)]
	public sealed class ExternalBotBridgeInfo : TraitInfo, IBotInfo
	{
		[FieldLoader.Require]
		[Desc("Internal id for this bot.")]
		public readonly string Type = null;

		[FluentReference]
		[Desc("Human-readable name this bot uses.")]
		public readonly string Name = null;

		[Desc("gRPC port for the RL agent to connect to.")]
		public readonly int Port = 9999;

		[Desc("How many game ticks between observations sent to the agent. " +
			"1 = every tick, 10 = every 10th tick.")]
		public readonly int ObservationInterval = 1;

		string IBotInfo.Type => Type;
		string IBotInfo.Name => Name;

		public override object Create(ActorInitializer init) { return new ExternalBotBridge(this, init); }
	}

	public sealed class ExternalBotBridge : ITick, IBot, INotifyCreated
	{
		/// <summary>
		/// Static reference holder so the gRPC service always reaches the active bridge.
		/// When mod reloads create new instances, Activate() updates this to the new one.
		/// </summary>
		internal static ExternalBotBridge ActiveBridge;

		static WebApplication grpcApp;
		static bool grpcServerStarted;
		static readonly object GrpcLock = new();

		public bool IsEnabled;

		readonly ExternalBotBridgeInfo info;
		readonly World world;
		readonly Queue<Order> orders = new();
		readonly string episodeId;

		Player player;
		ObservationSerializer observationSerializer;
		ActionHandler actionHandler;

		// Channels for async communication between game thread and gRPC stream.
		// DropOldest ensures the game thread never blocks:
		//   - Observations: capacity=1, latest overwrites stale (agent always gets freshest state)
		//   - Actions: capacity=16, game drains all pending each tick
		readonly Channel<RLProto.GameObservation> observationChannel =
			Channel.CreateBounded<RLProto.GameObservation>(
				new BoundedChannelOptions(1)
				{
					FullMode = BoundedChannelFullMode.DropOldest,
					SingleWriter = true,
					SingleReader = true,
				});

		readonly Channel<RLProto.AgentAction> actionChannel =
			Channel.CreateBounded<RLProto.AgentAction>(
				new BoundedChannelOptions(16)
				{
					FullMode = BoundedChannelFullMode.DropOldest,
					SingleWriter = true,
					SingleReader = true,
				});

		bool agentConnected;

		IBotInfo IBot.Info => info;
		Player IBot.Player => player;

		public ExternalBotBridge(ExternalBotBridgeInfo info, ActorInitializer init)
		{
			this.info = info;
			world = init.World;
			episodeId = Guid.NewGuid().ToString("N")[..12];
		}

		void INotifyCreated.Created(Actor self)
		{
			// gRPC server is started in Activate(), not here
		}

		void StartGrpcServer()
		{
			lock (GrpcLock)
			{
				if (grpcServerStarted)
					return;

				grpcServerStarted = true;
			}

			try
			{
				var builder = WebApplication.CreateBuilder(new WebApplicationOptions
				{
					Args = []
				});

				builder.WebHost.ConfigureKestrel(options =>
				{
					options.ListenAnyIP(info.Port, listenOptions =>
						listenOptions.Protocols = HttpProtocols.Http2);
				});

				builder.Services.AddGrpc();

				// Suppress all ASP.NET Core console logging
				builder.Logging.ClearProviders();

				grpcApp = builder.Build();
				grpcApp.MapGrpcService<RLBridgeService>();

				Log.Write("rl-bridge", $"gRPC server starting on port {info.Port}, episode {episodeId}");
				grpcApp.Run();
			}
			catch (Exception e)
			{
				Log.Write("rl-bridge", $"gRPC server failed: {e}");
				lock (GrpcLock)
				{
					grpcServerStarted = false;
				}
			}
		}

		public void Activate(Player p)
		{
			if (p.World.IsReplay)
				return;

			IsEnabled = true;
			player = p;
			observationSerializer = new ObservationSerializer(world, player, episodeId);
			actionHandler = new ActionHandler(world, player);

			// Register as the active bridge (so gRPC service can reach us)
			ActiveBridge = this;

			Log.Write("rl-bridge", $"ExternalBotBridge activated for player {p.InternalName}, episode {episodeId}");

			// Start the gRPC server (only once across all instances)
			var thread = new Thread(StartGrpcServer)
			{
				IsBackground = true,
				Name = "RL-Bridge-gRPC"
			};
			thread.Start();
		}

		void IBot.QueueOrder(Order order)
		{
			orders.Enqueue(order);
		}

		void ITick.Tick(Actor self)
		{
			if (!IsEnabled || self.World.IsLoadingGameSave)
				return;

			try
			{
				// Process all pending actions from agent (non-blocking drain)
				while (actionChannel.Reader.TryRead(out var agentAction))
					actionHandler.ProcessAction(agentAction, this);
			}
			catch (ChannelClosedException)
			{
				Log.Write("rl-bridge", "Agent disconnected (action channel closed)");
				agentConnected = false;
			}

			// Issue any queued orders
			while (orders.Count > 0)
				world.IssueOrder(orders.Dequeue());

			// Only send observations at the configured interval
			if (world.WorldTick % info.ObservationInterval != 0)
				return;

			if (!agentConnected)
				return;

			try
			{
				// Serialize and push observation (DropOldest — never blocks)
				var observation = observationSerializer.Serialize(world.WorldTick);
				observationChannel.Writer.TryWrite(observation);
			}
			catch (ChannelClosedException)
			{
				Log.Write("rl-bridge", "Agent disconnected (observation channel closed)");
				agentConnected = false;
			}
			catch (Exception e)
			{
				Log.Write("rl-bridge", $"Error serializing observation: {e}");
			}
		}

		/// <summary>
		/// Called by RLBridgeService to signal that an agent has connected.
		/// </summary>
		internal void OnAgentConnected()
		{
			agentConnected = true;
			Log.Write("rl-bridge", "RL agent connected via gRPC");
		}

		/// <summary>
		/// Called by RLBridgeService to signal that the agent has disconnected.
		/// </summary>
		internal void OnAgentDisconnected()
		{
			agentConnected = false;
			Log.Write("rl-bridge", "RL agent disconnected");
		}

		/// <summary>
		/// Channel for the game thread to push observations to the gRPC stream.
		/// </summary>
		internal ChannelReader<RLProto.GameObservation> ObservationReader => observationChannel.Reader;

		/// <summary>
		/// Channel for the gRPC stream to push actions to the game thread.
		/// </summary>
		internal ChannelWriter<RLProto.AgentAction> ActionWriter => actionChannel.Writer;

		/// <summary>
		/// Get a snapshot of the current game state for unary GetState RPC.
		/// </summary>
		internal RLProto.GameState GetCurrentState()
		{
			var phase = !IsEnabled ? "waiting"
				: world.IsGameOver ? "game_over"
				: "playing";

			var winner = "";
			if (world.IsGameOver)
			{
				foreach (var p in world.Players)
				{
					if (p.WinState == WinState.Won)
					{
						winner = p.InternalName;
						break;
					}
				}
			}

			// Player and enemy faction
			var playerFaction = player?.Faction?.InternalName ?? "";
			var enemyFaction = "";
			foreach (var p in world.Players)
			{
				if (p != player && !p.NonCombatant && p.Playable)
				{
					enemyFaction = p.Faction?.InternalName ?? "";
					break;
				}
			}

			return new RLProto.GameState
			{
				EpisodeId = episodeId,
				Tick = world.WorldTick,
				Phase = phase,
				Winner = winner,
				PlayerCount = world.Players.Length,
				PlayerFaction = playerFaction,
				EnemyFaction = enemyFaction,
			};
		}
	}
}
