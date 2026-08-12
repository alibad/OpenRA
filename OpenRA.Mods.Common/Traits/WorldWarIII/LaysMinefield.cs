#region Copyright & License Information
/*
 * Adapted for OpenRA AI from OpenRA Combined Arms LaysMinefield.cs.
 * Copyright (c) The OpenRA Combined Arms Developers (see upstream CREDITS).
 * Adaptation copyright (c) OpenRA AI contributors.
 *
 * This file is free software: you can redistribute it and/or modify it under
 * the terms of the GNU General Public License as published by the Free
 * Software Foundation, either version 3 of the License, or (at your option)
 * any later version. For more information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Experience;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public enum MineSelectionMode { Random, Ordered, Shuffled }

	[Desc("Periodically fills configured cells around this actor with mines.",
		"Destroyed mines are replenished without stacking duplicates on occupied slots.")]
	public class LaysMinefieldInfo : PausableConditionalTraitInfo
	{
		[FieldLoader.Require]
		[ActorReference]
		[Desc("Mine actor types. SelectionMode controls how they are assigned to slots.")]
		public readonly List<string> Mines = [];

		[Desc("Random selects independently per slot; Ordered cycles through Mines; Shuffled cycles through a deterministic shuffle.")]
		public readonly MineSelectionMode SelectionMode = MineSelectionMode.Random;

		[FieldLoader.Require]
		[Desc("Mine locations relative to the provider actor cell.")]
		public readonly CVec[] Locations = [];

		[Desc("Ticks before the first fill attempt.")]
		public readonly int InitialDelay = 1;

		[Desc("Ticks between replenishment checks.")]
		public readonly int RecreationInterval = 2500;

		[Desc("Remove live mines when the trait becomes disabled.")]
		public readonly bool RemoveOnDisable = true;

		[Desc("Kill mines when removed. If false, dispose them without a death event.")]
		public readonly bool KillOnRemove = true;

		[Desc("Damage types used when KillOnRemove is enabled.")]
		public readonly BitSet<DamageType> DamageTypes = default;

		[Desc("Spawn even when the mine's positionable trait reports that the cell is blocked.")]
		public readonly bool IgnorePlacementRules = false;

		public override object Create(ActorInitializer init) { return new LaysMinefield(this); }
	}

	public class LaysMinefield : PausableConditionalTrait<LaysMinefieldInfo>,
		INotifyKilled, INotifyOwnerChanged, INotifyActorDisposing, ITick, ISync
	{
		readonly Dictionary<CVec, Actor> mines = [];
		readonly int recreationInterval;
		readonly int slotCount;

		[VerifySync]
		int ticks;

		public LaysMinefield(LaysMinefieldInfo info)
			: base(info)
		{
			var experience = Game.ModData.GetOrNull<ExperienceCatalog>();
			recreationInterval = experience?.GetIntegerParameter(
				"minefield-generator", "recreation-interval", Info.RecreationInterval) ?? Info.RecreationInterval;
			slotCount = experience?.GetIntegerParameter(
				"minefield-generator", "slot-count", Info.Locations.Length) ?? Info.Locations.Length;
			ticks = Info.InitialDelay;
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitPaused || IsTraitDisabled)
				return;

			if (--ticks >= 0)
				return;

			ticks = recreationInterval;
			FillEmptySlots(self);
		}

		void FillEmptySlots(Actor self)
		{
			var mineTypes = Info.Mines;
			if (Info.SelectionMode == MineSelectionMode.Shuffled)
				mineTypes = Info.Mines.Shuffle(self.World.SharedRandom).ToList();

			for (var index = 0; index < Math.Min(slotCount, Info.Locations.Length); index++)
			{
				var offset = Info.Locations[index];
				if (mines.TryGetValue(offset, out var existing) && !existing.IsDead && existing.IsInWorld)
					continue;

				mines.Remove(offset);
				var mineType = SelectMineType(self, mineTypes, index);
				var cell = self.Location + offset;
				var actorInfo = self.World.Map.Rules.Actors[mineType];
				var positionable = actorInfo.TraitInfo<IPositionableInfo>();

				if (!Info.IgnorePlacementRules && !positionable.CanEnterCell(self.World, null, cell))
					continue;

				var mine = self.World.CreateActor(mineType.ToLowerInvariant(),
					[new OwnerInit(self.Owner), new LocationInit(cell)]);
				mines.Add(offset, mine);
			}
		}

		string SelectMineType(Actor self, List<string> mineTypes, int index)
		{
			if (Info.SelectionMode == MineSelectionMode.Random)
				return mineTypes[self.World.SharedRandom.Next(mineTypes.Count)];

			return mineTypes[index % mineTypes.Count];
		}

		void RemoveMines(Actor self)
		{
			foreach (var mine in mines.Values)
			{
				if (mine.IsDead || !mine.IsInWorld)
					continue;

				if (Info.KillOnRemove)
					mine.Kill(self, Info.DamageTypes);
				else
					mine.Dispose();
			}

			mines.Clear();
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			foreach (var mine in mines.Values)
				if (!mine.IsDead && mine.IsInWorld)
					mine.ChangeOwnerSync(newOwner);
		}

		protected override void TraitDisabled(Actor self)
		{
			ticks = Info.InitialDelay;
			if (Info.RemoveOnDisable)
				RemoveMines(self);
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e) { RemoveMines(self); }

		void INotifyActorDisposing.Disposing(Actor self) { RemoveMines(self); }
	}
}
