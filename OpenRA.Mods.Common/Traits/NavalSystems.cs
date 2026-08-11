#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Adds acceleration and turn-speed loss to naval Mobile actors.")]
	public class NavalHandlingInfo : ConditionalTraitInfo, Requires<IMoveInfo>
	{
		[Desc("Speed percentage when beginning to move.")]
		public readonly int InitialSpeed = 35;

		[Desc("Percentage points gained per tick while moving.")]
		public readonly int Acceleration = 3;

		[Desc("Percentage points lost per tick while stopping.")]
		public readonly int Deceleration = 7;

		[Desc("Maximum speed percentage.")]
		public readonly int MaximumSpeed = 100;

		[Desc("Additional multiplier while the hull is turning.")]
		public readonly int TurningModifier = 72;

		public override object Create(ActorInitializer init) { return new NavalHandling(this); }
	}

	public class NavalHandling : ConditionalTrait<NavalHandlingInfo>, ITick, INotifyMoving, ISpeedModifier, ISync
	{
		[VerifySync]
		int speed;

		[VerifySync]
		bool moving;

		[VerifySync]
		bool turning;

		public NavalHandling(NavalHandlingInfo info)
			: base(info)
		{
			speed = info.InitialSpeed;
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled)
				return;

			if (moving)
				speed = (speed + Info.Acceleration).Clamp(Info.InitialSpeed, Info.MaximumSpeed);
			else
				speed = (speed - Info.Deceleration).Clamp(Info.InitialSpeed, Info.MaximumSpeed);
		}

		void INotifyMoving.MovementTypeChanged(Actor self, MovementType type)
		{
			moving = type.HasFlag(MovementType.Horizontal);
			turning = type.HasFlag(MovementType.Turn);
		}

		int ISpeedModifier.GetSpeedModifier()
		{
			if (IsTraitDisabled)
				return 100;

			return turning ? speed * Info.TurningModifier / 100 : speed;
		}
	}

	[Desc("Spawns authored directional wake sprites behind a moving naval actor.")]
	public class WithNavalWakeInfo : ConditionalTraitInfo, Requires<IMoveInfo>, Requires<IFacingInfo>
	{
		public readonly string Image = "naval_wake";
		public readonly string Sequence = "wake";
		public readonly string Palette = "effect";
		public readonly int Interval = 5;
		public readonly WDist RearOffset = new(640);

		public override object Create(ActorInitializer init) { return new WithNavalWake(init.Self, this); }
	}

	public class WithNavalWake : ConditionalTrait<WithNavalWakeInfo>, ITick, INotifyMoving, ISync
	{
		readonly IFacing facing;

		[VerifySync]
		bool moving;

		[VerifySync]
		int tick;

		public WithNavalWake(Actor self, WithNavalWakeInfo info)
			: base(info)
		{
			facing = self.Trait<IFacing>();
		}

		void INotifyMoving.MovementTypeChanged(Actor self, MovementType type)
		{
			moving = type.HasFlag(MovementType.Horizontal);
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled || !moving || ++tick < Info.Interval)
				return;

			tick = 0;
			var offset = new WVec(0, Info.RearOffset.Length, 0).Rotate(WRot.FromYaw(facing.Facing));
			var position = self.CenterPosition + offset;
			var wakeFacing = facing.Facing;
			self.World.AddFrameEndTask(w => w.Add(new SpriteEffect(position, wakeFacing, w, Info.Image, Info.Sequence, Info.Palette)));
		}
	}

	[Desc("Uses normal fog visibility while passive, but reveals an emitting actor in explored enemy fog.")]
	public class NavalRadarVisibilityInfo : ConditionalTraitInfo, IDefaultVisibilityInfo
	{
		[Desc("Relationships that always see the actor.")]
		public readonly PlayerRelationship AlwaysVisibleRelationships = PlayerRelationship.Ally;

		public override object Create(ActorInitializer init) { return new NavalRadarVisibility(this); }
	}

	public class NavalRadarVisibility : ConditionalTrait<NavalRadarVisibilityInfo>, IDefaultVisibility
	{
		public NavalRadarVisibility(NavalRadarVisibilityInfo info)
			: base(info) { }

		bool IDefaultVisibility.IsVisible(Actor self, Player viewer)
		{
			if (viewer == null)
				return true;

			var relationship = self.Owner.RelationshipWith(viewer);
			if (Info.AlwaysVisibleRelationships.HasRelationship(relationship))
				return true;

			if (!viewer.Shroud.IsExplored(self.CenterPosition))
				return false;

			return !IsTraitDisabled || viewer.Shroud.IsVisible(self.CenterPosition);
		}
	}

	[Desc("Marks a mobile actor as a naval repair and rearm host.")]
	public class NavalResupplyHostInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new NavalResupplyHost(); }
	}

	public class NavalResupplyHost { }

	[Desc("Allows a naval actor to repair and rearm alongside a mobile naval resupply host.")]
	public class NavalResupplyableInfo : TraitInfo, Requires<IHealthInfo>, Requires<IMoveInfo>
	{
		public readonly WDist CloseEnough = WDist.FromCells(2);
		public readonly string EnterCursor = "enter";
		public readonly string EnterBlockedCursor = "enter-blocked";

		public override object Create(ActorInitializer init) { return new NavalResupplyable(init.Self, this); }
	}

	public class NavalResupplyable : IIssueOrder, IResolveOrder
	{
		readonly NavalResupplyableInfo info;
		readonly Actor self;
		readonly IHealth health;
		Rearmable rearmable;

		public NavalResupplyable(Actor self, NavalResupplyableInfo info)
		{
			this.info = info;
			this.self = self;
			health = self.Trait<IHealth>();
		}

		IEnumerable<IOrderTargeter> IIssueOrder.Orders
		{
			get
			{
				yield return new EnterAlliedActorTargeter<NavalResupplyHostInfo>("NavalResupply", 6,
					info.EnterCursor, info.EnterBlockedCursor, (target, modifiers) => true, target => NeedsResupply());
			}
		}

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			return order.OrderID == "NavalResupply" ? new Order(order.OrderID, self, target, queued) : null;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != "NavalResupply" || order.Target.Type != TargetType.Actor || !NeedsResupply())
				return;

			var target = order.Target.Actor;
			if (!self.Owner.IsAlliedWith(target.Owner) || !target.Info.HasTraitInfo<NavalResupplyHostInfo>())
				return;

			self.QueueActivity(order.Queued, new Resupply(self, target, info.CloseEnough));
			self.ShowTargetLines();
		}

		bool NeedsResupply()
		{
			rearmable ??= self.TraitOrDefault<Rearmable>();
			return health.DamageState > DamageState.Undamaged ||
				(rearmable != null && rearmable.RearmableAmmoPools.Any(pool => !pool.HasFullAmmo));
		}
	}

	[Desc("Marks survivors or wreckage that can be recovered by support vessels.")]
	public class NavalRecoverableInfo : TraitInfo
	{
		public readonly int Value = 100;
		public override object Create(ActorInitializer init) { return new NavalRecoverable(this); }
	}

	public class NavalRecoverable
	{
		public readonly NavalRecoverableInfo Info;
		public NavalRecoverable(NavalRecoverableInfo info) { Info = info; }
	}

	[Desc("Orders a vessel to rescue survivors and salvage recoverable wreckage.")]
	public class NavalRecoveryInfo : TraitInfo, Requires<IMoveInfo>
	{
		public readonly BitSet<TargetableType> TargetTypes = new("NavalSurvivor", "NavalSalvage");
		public readonly WDist CloseEnough = new(768);
		public readonly string Cursor = "enter";
		public readonly string RecoverySound = null;

		public override object Create(ActorInitializer init) { return new NavalRecovery(init.Self, this); }
	}

	public class NavalRecovery : IIssueOrder, IResolveOrder
	{
		readonly NavalRecoveryInfo info;
		readonly IMove move;

		public NavalRecovery(Actor self, NavalRecoveryInfo info)
		{
			this.info = info;
			move = self.Trait<IMove>();
		}

		IEnumerable<IOrderTargeter> IIssueOrder.Orders
		{
			get { yield return new TargetTypeOrderTargeter(info.TargetTypes, "NavalRecover", 7, info.Cursor, false, true); }
		}

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			return order.OrderID == "NavalRecover" ? new Order(order.OrderID, self, target, queued) : null;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != "NavalRecover" || order.Target.Type != TargetType.Actor)
				return;

			var target = order.Target.Actor;
			if (!target.Info.HasTraitInfo<NavalRecoverableInfo>())
				return;

			self.QueueActivity(order.Queued, move.MoveWithinRange(Target.FromActor(target), info.CloseEnough, targetLineColor: Color.LimeGreen));
			self.QueueActivity(new RecoverNavalActor(target, info.RecoverySound));
			self.ShowTargetLines();
		}
	}

	[Desc("Orders a support vessel to tow actors with the configured target types.")]
	public class NavalTowInfo : TraitInfo, Requires<IMoveInfo>, Requires<IFacingInfo>
	{
		public readonly BitSet<TargetableType> TargetTypes = new("Towable");
		public readonly WDist CloseEnough = new(896);
		public readonly WDist TowOffset = new(1100);
		public readonly string Cursor = "enter";
		public readonly string AttachSound = null;

		public override object Create(ActorInitializer init) { return new NavalTow(init.Self, this); }
	}

	public class NavalTow : IIssueOrder, IResolveOrder, ITick
	{
		readonly NavalTowInfo info;
		readonly IMove move;
		readonly IFacing facing;
		Actor towed;

		public NavalTow(Actor self, NavalTowInfo info)
		{
			this.info = info;
			move = self.Trait<IMove>();
			facing = self.Trait<IFacing>();
		}

		IEnumerable<IOrderTargeter> IIssueOrder.Orders
		{
			get { yield return new TargetTypeOrderTargeter(info.TargetTypes, "NavalTow", 6, info.Cursor, false, true); }
		}

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			return order.OrderID == "NavalTow" ? new Order(order.OrderID, self, target, queued) : null;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != "NavalTow" || order.Target.Type != TargetType.Actor)
				return;

			var target = order.Target.Actor;
			if (!info.TargetTypes.Overlaps(target.GetEnabledTargetTypes()) || target.TraitOrDefault<IPositionable>() == null)
				return;

			self.QueueActivity(order.Queued, move.MoveWithinRange(Target.FromActor(target), info.CloseEnough, targetLineColor: Color.Cyan));
			self.QueueActivity(new BeginNavalTow(this, target));
			self.ShowTargetLines();
		}

		public void Attach(Actor self, Actor target)
		{
			towed = target;
			towed.CancelActivity();
			Game.Sound.Play(SoundType.World, info.AttachSound, self.CenterPosition);
		}

		void ITick.Tick(Actor self)
		{
			if (towed == null || towed.IsDead || !towed.IsInWorld || self.IsDead)
			{
				towed = null;
				return;
			}

			var positionable = towed.TraitOrDefault<IPositionable>();
			if (positionable == null)
			{
				towed = null;
				return;
			}

			var offset = new WVec(0, info.TowOffset.Length, 0).Rotate(WRot.FromYaw(facing.Facing));
			positionable.SetCenterPosition(towed, self.CenterPosition + offset);
		}
	}

	[TraitLocation(SystemActors.Player)]
	[Desc("Coordinates naval radar, mobile resupply, and support-ship escorts for bots.")]
	public class NavalLogisticsBotModuleInfo : ConditionalTraitInfo
	{
		public readonly int Interval = 75;
		public readonly int RearmThreshold = 35;
		public readonly WDist ThreatScanRange = WDist.FromCells(18);
		public readonly FrozenSet<string> DefensiveShipTypes = FrozenSet<string>.Empty;
		public readonly FrozenSet<string> SupportShipTypes = FrozenSet<string>.Empty;
		public readonly FrozenSet<string> EscortShipTypes = FrozenSet<string>.Empty;
		public readonly FrozenSet<string> RadarShipTypes = FrozenSet<string>.Empty;
		public readonly FrozenSet<string> EnemyNavalTypes = FrozenSet<string>.Empty;

		public override object Create(ActorInitializer init) { return new NavalLogisticsBotModule(init.Self, this); }
	}

	public class NavalLogisticsBotModule : ConditionalTrait<NavalLogisticsBotModuleInfo>, IBotTick, ISync
	{
		readonly World world;
		readonly Player player;

		[VerifySync]
		int tick;

		public NavalLogisticsBotModule(Actor self, NavalLogisticsBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (IsTraitDisabled || --tick > 0)
				return;

			tick = Info.Interval;
			var own = world.Actors.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead).ToArray();
			var supports = own.Where(a => Info.SupportShipTypes.Contains(a.Info.Name)).ToArray();

			foreach (var ship in own.Where(a => Info.DefensiveShipTypes.Contains(a.Info.Name) && a.IsIdle))
			{
				var pool = ship.TraitsImplementing<AmmoPool>().FirstOrDefault();
				if (pool == null || pool.Info.Ammo == 0 || pool.CurrentAmmoCount * 100 / pool.Info.Ammo > Info.RearmThreshold)
					continue;

				var support = supports.OrderBy(a => (a.CenterPosition - ship.CenterPosition).HorizontalLengthSquared).FirstOrDefault();
				if (support != null)
					bot.QueueOrder(new Order("NavalResupply", ship, Target.FromActor(support), false));
			}

			foreach (var support in supports)
			{
				var escort = own.Where(a => Info.EscortShipTypes.Contains(a.Info.Name) && a.IsIdle)
					.OrderBy(a => (a.CenterPosition - support.CenterPosition).HorizontalLengthSquared).FirstOrDefault();
				if (escort != null)
					bot.QueueOrder(new Order("Guard", escort, Target.FromActor(support), false));
			}

			var enemies = world.Actors.Where(a => a.Owner.RelationshipWith(player) == PlayerRelationship.Enemy &&
				a.IsInWorld && !a.IsDead && Info.EnemyNavalTypes.Contains(a.Info.Name)).ToArray();
			foreach (var radarShip in own.Where(a => Info.RadarShipTypes.Contains(a.Info.Name)))
			{
				var deploy = radarShip.TraitOrDefault<GrantConditionOnDeploy>();
				if (deploy == null || deploy.DeployState != DeployState.Undeployed)
					continue;

				if (enemies.Any(enemy => (enemy.CenterPosition - radarShip.CenterPosition).HorizontalLengthSquared <= Info.ThreatScanRange.LengthSquared))
					bot.QueueOrder(new Order("GrantConditionOnDeploy", radarShip, false));
			}
		}
	}
}
