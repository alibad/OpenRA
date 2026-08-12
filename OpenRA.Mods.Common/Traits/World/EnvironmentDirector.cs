#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License,
 * version 3 or later. For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public enum EnvironmentEventType
	{
		None,
		ShamalFront,
		OilFireSmoke,
		CoastalSquall,
		HeatMirage,
		NightBlackout
	}

	public enum EnvironmentDomain
	{
		Infantry,
		Ground,
		Air,
		Naval,
		Building
	}

	enum EnvironmentPhase
	{
		Cooldown,
		Warning,
		Active
	}

	readonly struct EnvironmentModifiers
	{
		public readonly int Speed;
		public readonly int Range;
		public readonly int Reload;
		public readonly int Inaccuracy;
		public readonly int Vision;
		public readonly int Detection;

		public EnvironmentModifiers(int speed, int range, int reload, int inaccuracy, int vision, int detection)
		{
			Speed = speed;
			Range = range;
			Reload = reload;
			Inaccuracy = inaccuracy;
			Vision = vision;
			Detection = detection;
		}

		public EnvironmentModifiers Adapt(int strength)
		{
			return new EnvironmentModifiers(
				AdaptValue(Speed, strength),
				AdaptValue(Range, strength),
				AdaptValue(Reload, strength),
				AdaptValue(Inaccuracy, strength),
				AdaptValue(Vision, strength),
				AdaptValue(Detection, strength));
		}

		static int AdaptValue(int value, int strength)
		{
			return value + (100 - value) * strength / 100;
		}
	}

	[Desc("Applies the current global environment event penalties to this actor.")]
	public class EnvironmentResponseInfo : TraitInfo
	{
		[Desc("Controls which environment penalties are applied to the actor.")]
		public readonly EnvironmentDomain Domain = EnvironmentDomain.Ground;

		public override object Create(ActorInitializer init) { return new EnvironmentResponse(init.Self, this); }
	}

	public class EnvironmentResponse : INotifyCreated, ISpeedModifier, IRangeModifier, IReloadModifier,
		IInaccuracyModifier, IRevealsShroudModifier, IDetectCloakedModifier
	{
		readonly Actor self;
		readonly EnvironmentResponseInfo info;
		EnvironmentDirector director;

		public EnvironmentResponse(Actor self, EnvironmentResponseInfo info)
		{
			this.self = self;
			this.info = info;
		}

		void INotifyCreated.Created(Actor actor)
		{
			director = actor.World.WorldActor.TraitOrDefault<EnvironmentDirector>();
		}

		EnvironmentModifiers CurrentModifiers()
		{
			if (director == null || director.CurrentEvent == EnvironmentEventType.None)
				return new EnvironmentModifiers(100, 100, 100, 100, 100, 100);

			var modifiers = BaseModifiers(director.CurrentEvent, info.Domain);
			return modifiers.Adapt(AdaptationStrength(director.CurrentEvent, self.Owner.Faction.InternalName));
		}

		static EnvironmentModifiers BaseModifiers(EnvironmentEventType type, EnvironmentDomain domain)
		{
			return type switch
			{
				EnvironmentEventType.ShamalFront => domain switch
				{
					EnvironmentDomain.Air => new(82, 80, 114, 150, 66, 70),
					EnvironmentDomain.Naval => new(92, 88, 108, 130, 76, 78),
					EnvironmentDomain.Building => new(100, 84, 112, 138, 68, 72),
					_ => new(88, 84, 112, 138, 68, 72)
				},
				EnvironmentEventType.OilFireSmoke => domain switch
				{
					EnvironmentDomain.Air => new(96, 88, 108, 128, 72, 74),
					EnvironmentDomain.Naval => new(94, 86, 110, 132, 68, 70),
					EnvironmentDomain.Building => new(100, 78, 115, 145, 58, 62),
					EnvironmentDomain.Infantry => new(92, 82, 112, 142, 60, 64),
					_ => new(88, 80, 114, 145, 58, 62)
				},
				EnvironmentEventType.CoastalSquall => domain switch
				{
					EnvironmentDomain.Air => new(82, 80, 118, 152, 66, 68),
					EnvironmentDomain.Naval => new(86, 84, 114, 140, 72, 74),
					EnvironmentDomain.Building => new(100, 90, 108, 120, 80, 82),
					_ => new(95, 90, 108, 122, 80, 82)
				},
				EnvironmentEventType.HeatMirage => domain switch
				{
					EnvironmentDomain.Air => new(96, 88, 108, 138, 78, 80),
					EnvironmentDomain.Building => new(100, 82, 110, 152, 72, 74),
					_ => new(96, 82, 110, 155, 72, 74)
				},
				EnvironmentEventType.NightBlackout => domain switch
				{
					EnvironmentDomain.Air => new(94, 80, 112, 140, 54, 56),
					EnvironmentDomain.Naval => new(92, 80, 112, 138, 56, 58),
					EnvironmentDomain.Building => new(100, 82, 110, 135, 58, 60),
					_ => new(96, 80, 112, 140, 54, 56)
				},
				_ => new(100, 100, 100, 100, 100, 100)
			};
		}

		public static int AdaptationStrength(EnvironmentEventType type, string faction)
		{
			return type switch
			{
				EnvironmentEventType.ShamalFront when faction == "saudi" => 65,
				EnvironmentEventType.ShamalFront when faction == "russia" => 35,
				EnvironmentEventType.OilFireSmoke when faction == "yemen" => 60,
				EnvironmentEventType.OilFireSmoke when faction == "iran" => 45,
				EnvironmentEventType.OilFireSmoke when faction == "france" => 35,
				EnvironmentEventType.CoastalSquall when faction == "turkey" => 60,
				EnvironmentEventType.CoastalSquall when faction == "england" => 45,
				EnvironmentEventType.HeatMirage when faction == "yemen" => 60,
				EnvironmentEventType.HeatMirage when faction == "saudi" => 45,
				EnvironmentEventType.HeatMirage when faction == "ukraine" => 35,
				EnvironmentEventType.NightBlackout when faction == "turkey" => 60,
				EnvironmentEventType.NightBlackout when faction == "iran" => 65,
				EnvironmentEventType.NightBlackout when faction == "germany" => 45,
				_ => 0
			};
		}

		int ISpeedModifier.GetSpeedModifier() { return CurrentModifiers().Speed; }
		int IRangeModifier.GetRangeModifier() { return CurrentModifiers().Range; }
		int IReloadModifier.GetReloadModifier() { return CurrentModifiers().Reload; }
		int IInaccuracyModifier.GetInaccuracyModifier() { return CurrentModifiers().Inaccuracy; }
		int IRevealsShroudModifier.GetRevealsShroudModifier() { return CurrentModifiers().Vision; }
		int IDetectCloakedModifier.GetDetectCloakedModifier() { return CurrentModifiers().Detection; }
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Schedules synchronized environment events for the modern-war factions. Attach this to the World actor.")]
	public class EnvironmentDirectorInfo : TraitInfo, ILobbyCustomRulesIgnore
	{
		[Desc("The event system is enabled when at least one combatant uses one of these factions.")]
		public readonly FrozenSet<string> ActiveFactions = new HashSet<string> { "saudi", "yemen", "turkey", "iran" }.ToFrozenSet();

		[Desc("Ticks before the first event warning.")]
		public readonly int InitialDelay = 750;

		[Desc("Ticks between the event warning and activation.")]
		public readonly int WarningDuration = 250;

		[Desc("Random cooldown range between events, in ticks.")]
		public readonly int MinimumCooldown = 1000;
		public readonly int MaximumCooldown = 1500;

		public readonly int ShamalDuration = 1500;
		public readonly int OilFireDuration = 1250;
		public readonly int SquallDuration = 1500;
		public readonly int MirageDuration = 1125;
		public readonly int BlackoutDuration = 1250;

		[Desc("Actor spawned in harmless clusters to visualize oil-fire smoke columns.")]
		public readonly string OilFireActor = "env.oilfire";

		[Desc("Radar-only contact actor spawned during a heat mirage.")]
		public readonly string MirageContactActor = "env.mirage-contact";

		[GrantedConditionReference]
		public readonly string ShamalCondition = "environment-shamal";

		[GrantedConditionReference]
		public readonly string OilFireCondition = "environment-oilfire";

		[GrantedConditionReference]
		public readonly string SquallCondition = "environment-squall";

		[GrantedConditionReference]
		public readonly string MirageCondition = "environment-mirage";

		[GrantedConditionReference]
		public readonly string BlackoutCondition = "environment-blackout";

		public override object Create(ActorInitializer init) { return new EnvironmentDirector(init.Self, this); }
	}

	public class EnvironmentDirector : ITick, ISync, IRenderAboveWorld
	{
		static readonly EnvironmentEventType[] AllEvents =
		[
			EnvironmentEventType.ShamalFront,
			EnvironmentEventType.OilFireSmoke,
			EnvironmentEventType.CoastalSquall,
			EnvironmentEventType.HeatMirage,
			EnvironmentEventType.NightBlackout
		];

		readonly Actor self;
		readonly EnvironmentDirectorInfo info;
		readonly List<EnvironmentEventType> eventBag = [];
		readonly List<Actor> spawnedActors = [];

		[VerifySync]
		int currentEvent;

		[VerifySync]
		int currentPhase;

		[VerifySync]
		int ticksRemaining;

		[VerifySync]
		int lightningCountdown;

		bool initialized;
		bool enabled;
		int conditionToken = Actor.InvalidConditionToken;

		public EnvironmentEventType CurrentEvent => (EnvironmentEventType)currentEvent;

		public EnvironmentDirector(Actor self, EnvironmentDirectorInfo info)
		{
			this.self = self;
			this.info = info;
			currentPhase = (int)EnvironmentPhase.Cooldown;
			ticksRemaining = info.InitialDelay;
		}

		void ITick.Tick(Actor actor)
		{
			if (!initialized)
			{
				initialized = true;
				enabled = actor.World.Players.Any(p => !p.NonCombatant && info.ActiveFactions.Contains(p.Faction.InternalName));
			}

			if (!enabled || actor.World.IsLoadingGameSave)
				return;

			if ((EnvironmentPhase)currentPhase == EnvironmentPhase.Active && CurrentEvent == EnvironmentEventType.CoastalSquall)
				TickLightning(actor.World);

			if (--ticksRemaining > 0)
				return;

			switch ((EnvironmentPhase)currentPhase)
			{
				case EnvironmentPhase.Cooldown:
					currentEvent = (int)NextEvent(actor.World);
					currentPhase = (int)EnvironmentPhase.Warning;
					ticksRemaining = info.WarningDuration;
					TextNotificationsManager.AddSystemLine($"ENVIRONMENT WARNING: {EventName(CurrentEvent)} in {SecondsRemaining(actor.World)} seconds.");
					break;

				case EnvironmentPhase.Warning:
					StartEvent(actor.World);
					break;

				case EnvironmentPhase.Active:
					EndEvent(actor.World);
					break;
			}
		}

		EnvironmentEventType NextEvent(World world)
		{
			if (eventBag.Count == 0)
			{
				eventBag.AddRange(AllEvents);
				for (var i = eventBag.Count - 1; i > 0; i--)
				{
					var j = world.SharedRandom.Next(i + 1);
					(eventBag[i], eventBag[j]) = (eventBag[j], eventBag[i]);
				}
			}

			var next = eventBag[^1];
			eventBag.RemoveAt(eventBag.Count - 1);
			return next;
		}

		void StartEvent(World world)
		{
			currentPhase = (int)EnvironmentPhase.Active;
			ticksRemaining = EventDuration(CurrentEvent);
			conditionToken = self.GrantCondition(EventCondition(CurrentEvent));
			lightningCountdown = world.SharedRandom.Next(80, 170);

			if (CurrentEvent == EnvironmentEventType.OilFireSmoke)
				SpawnDecorations(world, info.OilFireActor, 4, false);
			else if (CurrentEvent == EnvironmentEventType.HeatMirage)
				SpawnDecorations(world, info.MirageContactActor, 9, true);

			TextNotificationsManager.AddSystemLine($"{EventName(CurrentEvent)} ACTIVE: {EventEffect(CurrentEvent)}");
			Log.Write("environment", $"Started {CurrentEvent} for {ticksRemaining} ticks.");
		}

		void EndEvent(World world)
		{
			if (conditionToken != Actor.InvalidConditionToken && self.TokenValid(conditionToken))
				conditionToken = self.RevokeCondition(conditionToken);

			CleanupDecorations(world);
			currentEvent = (int)EnvironmentEventType.None;
			currentPhase = (int)EnvironmentPhase.Cooldown;
			ticksRemaining = world.SharedRandom.Next(info.MinimumCooldown, info.MaximumCooldown + 1);
		}

		void TickLightning(World world)
		{
			if (--lightningCountdown > 0)
				return;

			lightningCountdown = world.SharedRandom.Next(110, 240);
			foreach (var flash in self.TraitsImplementing<FlashPostProcessEffect>())
				if (flash.Info.Type == "EnvironmentLightning")
					flash.Enable(world.SharedRandom.Next(8, 16));

			Game.Sound.Play(SoundType.UI, "environment/env-thunder.wav");
		}

		void SpawnDecorations(World world, string actorName, int count, bool allowWater)
		{
			var neutral = world.Players.FirstOrDefault(p => p.NonCombatant);
			if (neutral == null || !world.Map.Rules.Actors.ContainsKey(actorName))
				return;

			var candidates = world.Map.AllCells
				.Where(c => world.Map.Contains(c)
					&& (allowWater || world.Map.GetTerrainInfo(c).Type != "Water")
					&& !world.ActorMap.GetActorsAt(c).Any())
				.ToList();

			for (var i = 0; i < count && candidates.Count > 0; i++)
			{
				var index = world.SharedRandom.Next(candidates.Count);
				var cell = candidates[index];
				candidates.RemoveAt(index);
				world.AddFrameEndTask(w =>
				{
					var spawned = w.CreateActor(actorName, new TypeDictionary
					{
						new OwnerInit(neutral),
						new LocationInit(cell)
					});
					spawnedActors.Add(spawned);
				});
			}
		}

		void CleanupDecorations(World world)
		{
			var decorations = spawnedActors.ToArray();
			spawnedActors.Clear();
			world.AddFrameEndTask(_ =>
			{
				foreach (var actor in decorations)
					if (actor.IsInWorld)
						actor.Dispose();
			});
		}

		int EventDuration(EnvironmentEventType type)
		{
			return type switch
			{
				EnvironmentEventType.ShamalFront => info.ShamalDuration,
				EnvironmentEventType.OilFireSmoke => info.OilFireDuration,
				EnvironmentEventType.CoastalSquall => info.SquallDuration,
				EnvironmentEventType.HeatMirage => info.MirageDuration,
				EnvironmentEventType.NightBlackout => info.BlackoutDuration,
				_ => 1
			};
		}

		string EventCondition(EnvironmentEventType type)
		{
			return type switch
			{
				EnvironmentEventType.ShamalFront => info.ShamalCondition,
				EnvironmentEventType.OilFireSmoke => info.OilFireCondition,
				EnvironmentEventType.CoastalSquall => info.SquallCondition,
				EnvironmentEventType.HeatMirage => info.MirageCondition,
				EnvironmentEventType.NightBlackout => info.BlackoutCondition,
				_ => null
			};
		}

		static string EventName(EnvironmentEventType type)
		{
			return type switch
			{
				EnvironmentEventType.ShamalFront => "SHAMAL FRONT",
				EnvironmentEventType.OilFireSmoke => "OIL-FIRE SMOKE",
				EnvironmentEventType.CoastalSquall => "COASTAL SQUALL",
				EnvironmentEventType.HeatMirage => "HEAT MIRAGE",
				EnvironmentEventType.NightBlackout => "NIGHT BLACKOUT",
				_ => "CLEAR CONDITIONS"
			};
		}

		static string EventEffect(EnvironmentEventType type)
		{
			return type switch
			{
				EnvironmentEventType.ShamalFront => "low visibility; ground and air handling degraded",
				EnvironmentEventType.OilFireSmoke => "dense smoke masks targets and slows vehicles",
				EnvironmentEventType.CoastalSquall => "aircraft and ships lose speed and accuracy",
				EnvironmentEventType.HeatMirage => "optical ranging is distorted; false radar contacts active",
				EnvironmentEventType.NightBlackout => "visual range and target detection severely reduced",
				_ => ""
			};
		}

		int SecondsRemaining(World world)
		{
			return Math.Max(0, (ticksRemaining * world.Timestep + 999) / 1000);
		}

		void IRenderAboveWorld.RenderAboveWorld(Actor actor, WorldRenderer wr)
		{
			if (!enabled || (EnvironmentPhase)currentPhase == EnvironmentPhase.Cooldown)
				return;

			var warning = (EnvironmentPhase)currentPhase == EnvironmentPhase.Warning;
			var accent = warning ? Color.FromArgb(255, 235, 154, 44) : EventColor(CurrentEvent);
			var x = 12f;
			var y = Math.Max(78, Game.Renderer.NativeResolution.Height / 2 - 62);
			var width = 304f;
			var renderer = Game.Renderer.RgbaColorRenderer;
			renderer.FillRect(new float3(x, y, 0), new float3(x + width, y + 62, 0), Color.FromArgb(220, 9, 12, 17));
			renderer.FillRect(new float3(x, y, 0), new float3(x + 5, y + 62, 0), accent);
			renderer.DrawRect(new float3(x, y, 0), new float3(x + width, y + 62, 0), 1, Color.FromArgb(210, accent.R, accent.G, accent.B));

			var faction = wr.World.LocalPlayer?.Faction.InternalName;
			var adaptation = faction == null ? 0 : EnvironmentResponse.AdaptationStrength(CurrentEvent, faction);
			var status = adaptation > 0 ? $"ADAPTED {adaptation}%" : "EXPOSED";
			var title = warning ? $"INCOMING: {EventName(CurrentEvent)}" : EventName(CurrentEvent);
			var timer = $"{SecondsRemaining(wr.World):00}s";
			var font = Game.Renderer.Fonts["TinyBold"];
			font.DrawTextWithContrast(title, new float2(x + 14, y + 9), Color.White, Color.Black, 1);
			font.DrawTextWithContrast(timer, new float2(x + width - 42, y + 9), accent, Color.Black, 1);
			font.DrawTextWithContrast(status, new float2(x + 14, y + 31), adaptation > 0 ? Color.FromArgb(255, 117, 224, 151) : Color.FromArgb(255, 255, 128, 112), Color.Black, 1);
			font.DrawTextWithContrast(EventEffect(CurrentEvent), new float2(x + 14, y + 46), Color.FromArgb(255, 195, 204, 214), Color.Black, 1);

			if (CurrentEvent == EnvironmentEventType.NightBlackout && !warning)
				RenderEmergencyLights(wr);
		}

		static void RenderEmergencyLights(WorldRenderer wr)
		{
			var localPlayer = wr.World.LocalPlayer;
			if (localPlayer == null)
				return;

			var renderer = Game.Renderer.WorldRgbaColorRenderer;
			foreach (var building in wr.World.Actors.Where(a => a.IsInWorld && a.Owner.IsAlliedWith(localPlayer) && a.Info.HasTraitInfo<BuildingInfo>()))
			{
				var pos = wr.Viewport.WorldToViewPx(wr.ScreenPosition(building.CenterPosition)).ToFloat2();
				var pulse = 2 + (building.ActorID + (uint)(wr.World.WorldTick / 12)) % 3;
				var color = Color.FromArgb(190, 255, 194, 64);
				renderer.FillRect(new float3(pos.X - pulse, pos.Y - 12, pos.Y), new float3(pos.X + pulse, pos.Y - 8, pos.Y), color);
			}
		}

		static Color EventColor(EnvironmentEventType type)
		{
			return type switch
			{
				EnvironmentEventType.ShamalFront => Color.FromArgb(255, 214, 159, 83),
				EnvironmentEventType.OilFireSmoke => Color.FromArgb(255, 220, 93, 45),
				EnvironmentEventType.CoastalSquall => Color.FromArgb(255, 81, 160, 220),
				EnvironmentEventType.HeatMirage => Color.FromArgb(255, 245, 184, 76),
				EnvironmentEventType.NightBlackout => Color.FromArgb(255, 97, 118, 178),
				_ => Color.White
			};
		}
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Smoothly tints the battlefield to match the current EnvironmentDirector event.")]
	public class EnvironmentTintPostProcessEffectInfo : TraitInfo, ILobbyCustomRulesIgnore
	{
		[Desc("Ticks used to transition between clear and event lighting.")]
		public readonly int FadeTicks = 75;

		public override object Create(ActorInitializer init) { return new EnvironmentTintPostProcessEffect(init.Self, this); }
	}

	public class EnvironmentTintPostProcessEffect : RenderPostProcessPassBase, ITick
	{
		readonly EnvironmentTintPostProcessEffectInfo info;
		EnvironmentDirector director;
		float red = 1;
		float green = 1;
		float blue = 1;
		float ambient = 1;

		public EnvironmentTintPostProcessEffect(Actor self, EnvironmentTintPostProcessEffectInfo info)
			: base("tint", PostProcessPassType.AfterActors)
		{
			this.info = info;
			director = self.TraitOrDefault<EnvironmentDirector>();
		}

		void ITick.Tick(Actor self)
		{
			var target = TargetTint(director?.CurrentEvent ?? EnvironmentEventType.None, self.World.WorldTick);
			var step = 1f / Math.Max(1, info.FadeTicks);
			red += (target.Red - red) * step;
			green += (target.Green - green) * step;
			blue += (target.Blue - blue) * step;
			ambient += (target.Ambient - ambient) * step;
		}

		protected override bool Enabled => Math.Abs(red - 1) > 0.002f || Math.Abs(green - 1) > 0.002f || Math.Abs(blue - 1) > 0.002f || Math.Abs(ambient - 1) > 0.002f;

		protected override void PrepareRender(WorldRenderer wr, IShader shader)
		{
			shader.SetVec("Tint", ambient * red, ambient * green, ambient * blue);
		}

		static (float Red, float Green, float Blue, float Ambient) TargetTint(EnvironmentEventType type, int worldTick)
		{
			return type switch
			{
				EnvironmentEventType.ShamalFront => (1.08f, 0.91f, 0.70f, 0.88f),
				EnvironmentEventType.OilFireSmoke => (0.94f, 0.78f, 0.66f, 0.70f),
				EnvironmentEventType.CoastalSquall => (0.76f, 0.88f, 1.12f, 0.76f),
				EnvironmentEventType.HeatMirage => (1.12f, 0.92f, 0.70f, 0.92f + 0.025f * (float)Math.Sin(worldTick / 12f)),
				EnvironmentEventType.NightBlackout => (0.58f, 0.72f, 1.00f, 0.52f),
				_ => (1, 1, 1, 1)
			};
		}
	}
}
