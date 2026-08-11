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
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	/// <summary>
	/// Approaches at cruise altitude, aligns with the target, then irreversibly
	/// descends to weapon range. This deliberately contains no resupply path.
	/// </summary>
	public sealed class DiveAttack : Activity
	{
		readonly Aircraft aircraft;
		readonly AttackDive attackDive;
		readonly bool forceAttack;
		readonly Color? targetLineColor;

		Target target;
		Target lastVisibleTarget;
		bool committed;
		int divingToken = Actor.InvalidConditionToken;
		int desiredAltitude;

		public DiveAttack(Actor self, AttackDive attackDive, in Target target, bool forceAttack, Color? targetLineColor)
		{
			aircraft = self.Trait<Aircraft>();
			this.attackDive = attackDive;
			this.target = target;
			this.forceAttack = forceAttack;
			this.targetLineColor = targetLineColor;

			if ((target.Type == TargetType.Actor && target.Actor.CanBeViewedByPlayer(self.Owner))
				|| target.Type == TargetType.FrozenActor || target.Type == TargetType.Terrain)
				lastVisibleTarget = Target.FromPos(target.CenterPosition);
		}

		protected override void OnFirstRun(Actor self)
		{
			attackDive.SetRequestedTarget(target, forceAttack);
			desiredAltitude = aircraft.Info.CruiseAltitude.Length;
		}

		public override bool Tick(Actor self)
		{
			if (attackDive.IsTraitPaused)
				return false;

			target = target.Recalculate(self.Owner, out var targetIsHiddenActor);
			if (!targetIsHiddenActor && target.Type == TargetType.Actor)
				lastVisibleTarget = Target.FromTargetPositions(target);

			var useLastVisibleTarget = targetIsHiddenActor || !target.IsValidFor(self);
			if (useLastVisibleTarget && !lastVisibleTarget.IsValidFor(self))
				return !committed;

			var checkTarget = useLastVisibleTarget ? lastVisibleTarget : target;
			var delta = checkTarget.CenterPosition - self.CenterPosition;
			var desiredFacing = delta.HorizontalLengthSquared != 0 ? delta.Yaw : aircraft.Facing;
			var horizontalDistance = delta.HorizontalLength;

			if (!committed && horizontalDistance <= attackDive.Info.DiveRange.Length
				&& Util.FacingWithinTolerance(aircraft.Facing, desiredFacing, attackDive.Info.CommitFacingTolerance))
			{
				committed = true;
				IsInterruptible = false;
				if (!string.IsNullOrEmpty(attackDive.Info.DivingCondition))
					divingToken = self.GrantCondition(attackDive.Info.DivingCondition);
			}

			if (committed)
			{
				// Once committed, a hidden or destroyed actor becomes a terrain
				// impact point. The munition must complete its strike, not abort.
				attackDive.SetRequestedTarget(checkTarget, true);
				var range = Math.Max(1, attackDive.Info.DiveRange.Length);
				var remaining = Math.Min(horizontalDistance, range);
				var altitudeSpan = aircraft.Info.CruiseAltitude.Length - attackDive.Info.ImpactAltitude.Length;
				var profileAltitude = attackDive.Info.ImpactAltitude.Length + altitudeSpan * remaining / range;
				desiredAltitude = Math.Min(desiredAltitude, profileAltitude);
			}
			else
				attackDive.SetRequestedTarget(target, forceAttack);

			Fly.FlyTick(self, aircraft, desiredFacing, new WDist(desiredAltitude));
			return false;
		}

		protected override void OnLastRun(Actor self)
		{
			attackDive.ClearRequestedTarget();
			RevokeDiveCondition(self);
		}

		protected override void OnActorDispose(Actor self)
		{
			RevokeDiveCondition(self);
		}

		void RevokeDiveCondition(Actor self)
		{
			if (divingToken != Actor.InvalidConditionToken)
			{
				self.RevokeCondition(divingToken);
				divingToken = Actor.InvalidConditionToken;
			}
		}

		public override IEnumerable<Target> GetTargets(Actor self)
		{
			yield return target;
		}

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			if (targetLineColor.HasValue)
				yield return new TargetLineNode(target, targetLineColor.Value);
		}
	}
}
