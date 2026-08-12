#region Copyright & License Information
/*
 * Adapted for OpenRA AI from OpenHV's scrap traits and ValueInit.
 * Copyright 2019-2025 The OpenHV Developers (see upstream CREDITS).
 * Adaptation copyright (c) OpenRA AI contributors.
 *
 * This file is free software: you can redistribute it and/or modify it under
 * the terms of the GNU General Public License as published by the Free
 * Software Foundation, either version 3 of the License, or (at your option)
 * any later version. For more information, see COPYING.
 */
#endregion

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Experience;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public sealed class SalvageValueInit : ValueActorInit<int>
	{
		public SalvageValueInit(int value)
			: base(value) { }
	}

	[Desc("Defines the cash value of a collectible salvage actor.")]
	public class SalvageValueInfo : TraitInfo
	{
		[Desc("Percentage of the destroyed actor's sell value awarded to the collector.")]
		public readonly int Percentage = 10;

		[Desc("Fallback range for editor-placed salvage without a SalvageValueInit.")]
		public readonly int MinimumFallbackAmount = 40;

		public readonly int MaximumFallbackAmount = 200;

		public override object Create(ActorInitializer init) { return new SalvageValue(init, this); }
	}

	public class SalvageValue
	{
		public readonly int Bounty;

		public SalvageValue(ActorInitializer init, SalvageValueInfo info)
		{
			var value = init.GetOrDefault<SalvageValueInit>(info);
			var percentage = Game.ModData.GetOrNull<ExperienceCatalog>()?.GetIntegerParameter(
				"battlefield-salvage", "value-percentage", info.Percentage) ?? info.Percentage;
			Bounty = value != null ? value.Value * percentage / 100 :
				init.Self.World.SharedRandom.Next(info.MinimumFallbackAmount, info.MaximumFallbackAmount);
		}
	}

	[Desc("Awards the collectible salvage value as cash.")]
	public class CollectSalvageCrateActionInfo : CrateActionInfo, Requires<SalvageValueInfo>
	{
		[Desc("Display the collected amount as floating cash text.")]
		public readonly bool UseCashTick = true;

		public override object Create(ActorInitializer init) { return new CollectSalvageCrateAction(init.Self, this); }
	}

	public class CollectSalvageCrateAction : CrateAction
	{
		readonly CollectSalvageCrateActionInfo info;
		readonly int bounty;

		public CollectSalvageCrateAction(Actor self, CollectSalvageCrateActionInfo info)
			: base(self, info)
		{
			this.info = info;
			bounty = self.Trait<SalvageValue>().Bounty;
		}

		public override void Activate(Actor collector)
		{
			collector.World.AddFrameEndTask(w =>
			{
				var amount = collector.Owner.PlayerActor.Trait<PlayerResources>().ChangeCash(bounty);
				if (info.UseCashTick)
					w.Add(new FloatingText(collector.CenterPosition, collector.Owner.Color, FloatingText.FormatCashTick(amount), 30));
			});

			base.Activate(collector);
		}

		public override int GetSelectionShares(Actor collector)
		{
			var resources = collector.Owner.PlayerActor.Trait<PlayerResources>();
			return bounty < 0 && resources.Cash + resources.Resources == 0 ? 0 : base.GetSelectionShares(collector);
		}
	}

	[Desc("Spawns collectible salvage when this actor is killed.")]
	public class SpawnSalvageOnDeathInfo : ConditionalTraitInfo
	{
		[ActorReference]
		[FieldLoader.Require]
		[Desc("Salvage actor types. One is selected deterministically when spawning.")]
		public readonly ImmutableArray<string> Actors = [];

		[Desc("Spawn probability from 0 through 100 percent.")]
		public readonly int Probability = 100;

		[Desc("Allowed terrain types. Empty allows every terrain type.")]
		public readonly FrozenSet<string> TerrainTypes = FrozenSet<string>.Empty;

		[Desc("Map player that owns spawned salvage.")]
		public readonly string InternalOwner = "Neutral";

		[Desc("Damage types that may create salvage. Empty accepts every damage type.")]
		public readonly BitSet<DamageType> DeathTypes = default;

		[Desc("Spawn offset relative to the destroyed actor's cell.")]
		public readonly CVec Offset = CVec.Zero;

		public override object Create(ActorInitializer init) { return new SpawnSalvageOnDeath(init, this); }
	}

	public class SpawnSalvageOnDeath : ConditionalTrait<SpawnSalvageOnDeathInfo>, INotifyKilled, INotifyRemovedFromWorld
	{
		readonly string faction;
		bool shouldSpawn;

		public SpawnSalvageOnDeath(ActorInitializer init, SpawnSalvageOnDeathInfo info)
			: base(info)
		{
			faction = init.GetValue<FactionInit, string>(info, init.Self.Owner.Faction.InternalName);
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			var experience = Game.ModData.GetOrNull<ExperienceCatalog>();
			var probability = experience?.GetIntegerParameter(
				"battlefield-salvage", "spawn-probability", Info.Probability) ?? Info.Probability;
			var eligibleOwners = experience?.GetChoiceParameter(
				"battlefield-salvage", "eligible-owners", "All") ?? "All";
			var ownerEligible = eligibleOwners.Equals("All", System.StringComparison.OrdinalIgnoreCase) ||
				(eligibleOwners.Equals("AI", System.StringComparison.OrdinalIgnoreCase) && self.Owner.IsBot) ||
				(eligibleOwners.Equals("Human", System.StringComparison.OrdinalIgnoreCase) && !self.Owner.IsBot);

			if (IsTraitDisabled || !self.IsInWorld || !ownerEligible || probability <= 0 ||
				self.World.SharedRandom.Next(100) >= probability ||
				(!Info.DeathTypes.IsEmpty && !e.Damage.DamageTypes.Overlaps(Info.DeathTypes)))
				return;

			var terrain = self.World.Map.GetTerrainInfo(self.Location).Type;
			shouldSpawn = Info.TerrainTypes.Count == 0 || Info.TerrainTypes.Contains(terrain);
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			if (!shouldSpawn)
				return;

			var initializers = new TypeDictionary
			{
				new ParentActorInit(self),
				new LocationInit(self.Location + Info.Offset),
				new CenterPositionInit(self.CenterPosition),
				new FactionInit(faction),
				new SalvageValueInit(self.GetSellValue()),
				new OwnerInit(self.World.Players.First(p => p.InternalName == Info.InternalOwner)),
				new SkipMakeAnimsInit()
			};

			var salvageType = Info.Actors.Random(self.World.SharedRandom);
			self.World.AddFrameEndTask(w => w.CreateActor(salvageType, initializers));
		}
	}
}
