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

using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Aircraft commits to a descending one-way attack instead of making conventional attack runs.")]
	public class AttackDiveInfo : AttackAircraftInfo
	{
		[Desc("Horizontal distance from the target at which the terminal dive may begin.")]
		public readonly WDist DiveRange = WDist.FromCells(6);

		[Desc("Desired altitude above terrain at the impact point.")]
		public readonly WDist ImpactAltitude = new(96);

		[Desc("Maximum heading error allowed before committing to the terminal dive.")]
		public readonly WAngle CommitFacingTolerance = new(64);

		[GrantedConditionReference]
		[Desc("Condition granted from dive commitment until impact or disposal.")]
		public readonly string DivingCondition = null;

		public override object Create(ActorInitializer init) { return new AttackDive(init.Self, this); }
	}

	public class AttackDive : AttackAircraft
	{
		public new readonly AttackDiveInfo Info;

		public AttackDive(Actor self, AttackDiveInfo info)
			: base(self, info)
		{
			Info = info;
		}

		public override Activity GetAttackActivity(
			Actor self, AttackSource source, in Target newTarget, bool allowMove, bool forceAttack, Color? targetLineColor = null)
		{
			return new DiveAttack(self, this, newTarget, forceAttack, targetLineColor);
		}
	}
}
