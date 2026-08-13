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
using System.Collections.Immutable;
using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Experience;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Converts player combat experience into synchronized commander promotion prerequisites.")]
	public sealed class CommanderPromotionManagerInfo : TraitInfo, ITechTreePrerequisiteInfo
	{
		public readonly ImmutableArray<int> Thresholds = [250, 750, 1500];

		IEnumerable<string> ITechTreePrerequisiteInfo.Prerequisites(ActorInfo info)
		{
			for (var i = 1; i <= Thresholds.Length; i++)
				yield return $"promotion.level.{i}";

			foreach (var doctrine in new[] { "balanced", "firepower", "mobility", "fortification" })
				yield return $"promotion.doctrine.{doctrine}";
		}

		public override object Create(ActorInitializer init) { return new CommanderPromotionManager(init.Self, this); }
	}

	public sealed class CommanderPromotionManager : ITechTreePrerequisite, ITick
	{
		readonly Actor self;
		readonly CommanderPromotionManagerInfo info;
		readonly PlayerExperience experience;
		readonly TechTree techTree;
		readonly int pointRate;
		readonly string doctrine;
		int level;
		int checkInterval;

		public CommanderPromotionManager(Actor self, CommanderPromotionManagerInfo info)
		{
			this.self = self;
			this.info = info;
			experience = self.Trait<PlayerExperience>();
			techTree = self.Trait<TechTree>();
			var catalog = Game.ModData.GetOrNull<ExperienceCatalog>();
			pointRate = Math.Max(1, catalog?.GetIntegerParameter("commander-promotions", "point-rate", 100) ?? 100);
			doctrine = (catalog?.GetChoiceParameter("commander-promotions", "doctrine", "Balanced") ?? "Balanced")
				.ToLowerInvariant();
		}

		public IEnumerable<string> ProvidesPrerequisites
		{
			get
			{
				yield return $"promotion.doctrine.{doctrine}";
				for (var i = 1; i <= level; i++)
					yield return $"promotion.level.{i}";
			}
		}

		void ITick.Tick(Actor actor)
		{
			if (--checkInterval > 0)
				return;

			checkInterval = 25;
			var points = (long)experience.Experience * pointRate / 100;
			var newLevel = info.Thresholds.Count(threshold => points >= threshold);
			if (newLevel == level)
				return;

			level = newLevel;
			techTree.ActorChanged(self);
		}
	}

	[Desc("Maintains a replenishing group of real aircraft around a carrier or drone mothership.")]
	public sealed class CarrierWingSpawnerInfo : ConditionalTraitInfo, IRulesetLoaded
	{
		[ActorReference]
		[FieldLoader.Require]
		public readonly string Actor = null;

		public readonly int ReplenishInterval = 750;

		public override object Create(ActorInitializer init) { return new CarrierWingSpawner(this); }

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (!rules.Actors.ContainsKey(Actor.ToLowerInvariant()))
				throw new YamlException($"Carrier wing actor `{Actor}` does not exist.");
		}
	}

	public sealed class CarrierWingSpawner : ConditionalTrait<CarrierWingSpawnerInfo>, ITick, INotifyOwnerChanged
	{
		readonly List<Actor> children = [];
		readonly int wingSize;
		int replenish;

		public CarrierWingSpawner(CarrierWingSpawnerInfo info)
			: base(info)
		{
			var catalog = Game.ModData.GetOrNull<ExperienceCatalog>();
			wingSize = Math.Max(1, catalog?.GetIntegerParameter("carrier-and-drone-wing", "wing-size", 4) ?? 4);
		}

		void ITick.Tick(Actor self)
		{
			children.RemoveAll(child => child.Disposed || child.IsDead || !child.IsInWorld);
			if (IsTraitDisabled || children.Count >= wingSize || --replenish > 0)
				return;

			replenish = Math.Max(1, Info.ReplenishInterval);
			self.World.AddFrameEndTask(world =>
			{
				if (self.IsDead || !self.IsInWorld)
					return;

				var actorInfo = world.Map.Rules.Actors[Info.Actor.ToLowerInvariant()];
				var altitude = actorInfo.TraitInfoOrDefault<AircraftInfo>()?.CruiseAltitude.Length ?? 0;
				var child = world.CreateActor(Info.Actor,
				[
					new OwnerInit(self.Owner),
					new ParentActorInit(self),
					new CenterPositionInit(self.CenterPosition + new WVec(0, 0, altitude)),
					new FacingInit(self.Orientation.Yaw)
				]);
				children.Add(child);
			});
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			foreach (var child in children.Where(child => !child.Disposed && child.IsInWorld))
				child.ChangeOwner(newOwner);
		}
	}

	[Desc("Adds a validated, synchronized targeted transit jump to a mobile actor.")]
	public sealed class TargetedTransitAbilityInfo : PausableConditionalTraitInfo
	{
		public readonly string OrderName = "TargetedTransit";
		public readonly WDist Range = WDist.FromCells(12);
		public readonly int Cooldown = 900;
		public readonly bool RequiresForceFire = true;
		[CursorReference]
		public readonly string Cursor = "ability";

		[CursorReference]
		public readonly string BlockedCursor = "ability-blocked";

		[VoiceReference]
		public readonly string Voice = "Action";

		public override object Create(ActorInitializer init) { return new TargetedTransitAbility(this); }
	}

	public sealed class TargetedTransitAbility : PausableConditionalTrait<TargetedTransitAbilityInfo>,
		IIssueOrder, IResolveOrder, IOrderVoice, ITick, ISelectionBar, ISync
	{
		[VerifySync]
		int cooldown;
		readonly int cooldownDuration;

		public TargetedTransitAbility(TargetedTransitAbilityInfo info)
			: base(info)
		{
			var catalog = Game.ModData.GetOrNull<ExperienceCatalog>();
			var rate = catalog?.GetIntegerParameter("targeted-unit-abilities", "cooldown-rate", 100) ?? 100;
			cooldownDuration = Math.Max(1, info.Cooldown * rate / 100);
		}

		public IEnumerable<IOrderTargeter> Orders
		{
			get { if (!IsTraitDisabled) yield return new TransitOrderTargeter(this); }
		}

		public Order IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			return order.OrderID == Info.OrderName && CanTarget(self, target) ?
				new Order(order.OrderID, self, target, queued) : null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != Info.OrderName || !CanTarget(self, order.Target))
				return;

			var cell = self.World.Map.CellContaining(order.Target.CenterPosition);
			self.CancelActivity();
			self.QueueActivity(new SimpleTeleport(cell));
			cooldown = cooldownDuration;
		}

		bool CanTarget(Actor self, in Target target)
		{
			if (IsTraitDisabled || IsTraitPaused || cooldown > 0 || target.Type != TargetType.Terrain ||
				!target.IsInRange(self.CenterPosition, Info.Range))
				return false;

			var cell = self.World.Map.CellContaining(target.CenterPosition);
			return self.World.Map.Contains(cell) && self.Trait<IPositionable>().CanEnterCell(cell, self);
		}

		void ITick.Tick(Actor self) { if (cooldown > 0 && !IsTraitPaused) cooldown--; }
		public string VoicePhraseForOrder(Actor self, Order order) => order.OrderString == Info.OrderName ? Info.Voice : null;
		float ISelectionBar.GetValue() => (float)cooldown / cooldownDuration;
		Color ISelectionBar.GetColor() => Color.Magenta;
		bool ISelectionBar.DisplayWhenEmpty => false;

		sealed class TransitOrderTargeter : IOrderTargeter
		{
			readonly TargetedTransitAbility ability;
			public TransitOrderTargeter(TargetedTransitAbility ability) { this.ability = ability; }
			public string OrderID => ability.Info.OrderName;
			public int OrderPriority => 7;
			public bool IsQueued { get; private set; }
			public bool TargetOverridesSelection(Actor self, in Target target, List<Actor> actorsAt,
				CPos xy, TargetModifiers modifiers) => true;

			public bool CanTarget(Actor self, in Target target, ref TargetModifiers modifiers, ref string cursor)
			{
				IsQueued = modifiers.HasModifier(TargetModifiers.ForceQueue);
				if (ability.Info.RequiresForceFire && !modifiers.HasModifier(TargetModifiers.ForceAttack))
					return false;

				var valid = ability.CanTarget(self, target);
				cursor = valid ? ability.Info.Cursor : ability.Info.BlockedCursor;
				return valid;
			}
		}
	}

	[Desc("Limits the number of temporarily controlled actors owned by this controller.")]
	public sealed class MindControlCapacityInfo : TraitInfo
	{
		public readonly int Capacity = 2;
		public override object Create(ActorInitializer init) { return new MindControlCapacity(this); }
	}

	public sealed class MindControlCapacity
	{
		readonly int capacity;
		readonly List<Actor> controlled = [];

		public MindControlCapacity(MindControlCapacityInfo info)
		{
			var catalog = Game.ModData.GetOrNull<ExperienceCatalog>();
			capacity = Math.Max(1, catalog?.GetIntegerParameter("mind-control-and-disguise", "capacity", info.Capacity) ?? info.Capacity);
		}

		public bool TryReserve(Actor controller, Actor target)
		{
			controlled.RemoveAll(actor => actor.Disposed || actor.IsDead || actor.Owner != controller.Owner);
			if (controlled.Contains(target))
				return true;

			if (controlled.Count >= capacity)
				return false;

			controlled.Add(target);
			return true;
		}
	}
}
