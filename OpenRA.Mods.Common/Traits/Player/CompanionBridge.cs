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
using System.Threading;
using OpenRA.Traits;
using RLProto = OpenRA.Mods.Common.RL;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Publishes read-only, fog-respecting local-player observations for OpenRA AI.")]
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	public sealed class CompanionBridgeInfo : TraitInfo
	{
		[Desc("Number of game ticks between snapshots.")]
		public readonly int ObservationInterval = 10;

		public override object Create(ActorInitializer init) { return new CompanionBridge(this, init); }
	}

	/// <summary>
	/// Captures observations on the game thread, then serves immutable clones
	/// through the existing gRPC host. It never queues an order or pauses play.
	/// </summary>
	public sealed class CompanionBridge : ITick, INotifyCreated, INotifyActorDisposing
	{
		static readonly object CurrentLock = new();
		const long SpokenStatusTimeoutMilliseconds = 12000;
		const long ErrorStatusTimeoutMilliseconds = 4000;
		static CompanionBridge current;
		static RLProto.CompanionStatus companionStatus = ReadyStatus();
		static long companionStatusUpdatedAt = Environment.TickCount64;

		readonly CompanionBridgeInfo info;
		readonly World world;
		readonly string episodeId = Guid.NewGuid().ToString("N")[..12];
		readonly object observationLock = new();

		ObservationSerializer serializer;
		RLProto.GameObservation latestObservation;
		bool enabled;

		public CompanionBridge(CompanionBridgeInfo info, ActorInitializer init)
		{
			this.info = info;
			world = init.World;
		}

		void INotifyCreated.Created(Actor self)
		{
			enabled = world.Type == WorldType.Regular
				&& Environment.GetEnvironmentVariable("OPENRA_AI_COMPANION") == "1"
				&& !world.IsReplay;
			if (!enabled)
				return;

			lock (CurrentLock)
			{
				current = this;
				companionStatus = ReadyStatus();
				companionStatusUpdatedAt = Environment.TickCount64;
			}

			var port = 9998;
			var envPort = Environment.GetEnvironmentVariable("OPENRA_AI_GRPC_PORT");
			if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out var configuredPort))
				port = configuredPort;

			var thread = new Thread(() => ExternalBotBridge.StartGrpcServer(port))
			{
				IsBackground = true,
				Name = "OpenRA-AI-Companion-gRPC"
			};
			thread.Start();
			Log.Write("rl-bridge", $"OpenRA AI companion enabled on port {port}");
		}

		void ITick.Tick(Actor self)
		{
			if (!enabled || world.LocalPlayer == null || world.LocalPlayer.Spectating
				|| world.WorldTick % Math.Max(1, info.ObservationInterval) != 0)
				return;

			serializer ??= new ObservationSerializer(world, world.LocalPlayer, episodeId);
			var observation = serializer.Serialize(world.WorldTick);
			lock (observationLock)
				latestObservation = observation;
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			lock (CurrentLock)
			{
				if (current == this)
					current = null;
			}
		}

		internal static bool TryGetObservation(out RLProto.GameObservation observation)
		{
			CompanionBridge bridge;
			lock (CurrentLock)
				bridge = current;

			if (bridge == null)
			{
				observation = null;
				return false;
			}

			lock (bridge.observationLock)
			{
				if (bridge.latestObservation == null)
				{
					observation = null;
					return false;
				}

				observation = bridge.latestObservation.Clone();
				return true;
			}
		}

		static RLProto.CompanionStatus ReadyStatus()
		{
			return IdleStatus(true, false);
		}

		static RLProto.CompanionStatus IdleStatus(bool enabled, bool muted)
		{
			return new RLProto.CompanionStatus
			{
				State = !enabled ? "disabled" : muted ? "muted" : "ready",
				Message = !enabled ? "AI OFF  •  CTRL+SHIFT+A TO ENABLE" : muted
					? "AI VOICE OFF  •  TEXT INSIGHTS STAY ON"
					: "AI READY  •  HOLD CTRL+SPACE TO ASK",
				Enabled = enabled,
				Muted = muted
			};
		}

		internal static bool UpdateStatus(RLProto.CompanionStatus status)
		{
			lock (CurrentLock)
			{
				if (current == null || !current.enabled)
					return false;

				companionStatus = status.Clone();
				companionStatusUpdatedAt = Environment.TickCount64;
				return true;
			}
		}

		internal static bool UpdateLocalVoiceState(bool muted)
		{
			lock (CurrentLock)
			{
				if (current == null || !current.enabled)
					return false;

				companionStatus = IdleStatus(companionStatus.Enabled, muted);
				companionStatusUpdatedAt = Environment.TickCount64;
				return true;
			}
		}

		internal static bool UpdateLocalControlError(string message)
		{
			lock (CurrentLock)
			{
				if (current == null || !current.enabled)
					return false;

				companionStatus.State = "error";
				companionStatus.Message = message;
				companionStatusUpdatedAt = Environment.TickCount64;
				return true;
			}
		}

		internal static bool TryGetStatus(out string state, out string message)
		{
			return TryGetStatus(out state, out message, out _, out _);
		}

		internal static bool TryGetStatus(out string state, out string message, out bool statusEnabled, out bool muted)
		{
			lock (CurrentLock)
			{
				if (current == null || !current.enabled)
				{
					state = null;
					message = null;
					statusEnabled = false;
					muted = false;
					return false;
				}

				// Insights describe a moment in time. Never leave one pinned
				// after the companion or audio process has moved on.
				if ((companionStatus.State == "speaking" || companionStatus.State == "insight" ||
					companionStatus.State == "routine" || companionStatus.State == "important" ||
					companionStatus.State == "critical" || companionStatus.State == "speaking-important" ||
					companionStatus.State == "speaking-critical")
					&& Environment.TickCount64 - companionStatusUpdatedAt >= SpokenStatusTimeoutMilliseconds)
				{
					companionStatus = IdleStatus(companionStatus.Enabled, companionStatus.Muted);
					companionStatusUpdatedAt = Environment.TickCount64;
				}
				else if (companionStatus.State == "error"
					&& Environment.TickCount64 - companionStatusUpdatedAt >= ErrorStatusTimeoutMilliseconds)
				{
					companionStatus = IdleStatus(companionStatus.Enabled, companionStatus.Muted);
					companionStatusUpdatedAt = Environment.TickCount64;
				}

				state = companionStatus.State;
				message = companionStatus.Message;
				statusEnabled = companionStatus.Enabled;
				muted = companionStatus.Muted;
				return true;
			}
		}
	}
}
