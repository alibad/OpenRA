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

using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("This actor deflects missiles.")]
	public class JamsMissilesInfo : ConditionalTraitInfo
	{
		[Desc("Range of the deflection.")]
		public readonly WDist Range = WDist.Zero;

		[Desc("What player relationships are affected.")]
		public readonly PlayerRelationship DeflectionRelationships = PlayerRelationship.Ally | PlayerRelationship.Neutral | PlayerRelationship.Enemy;

		[Desc("Chance of deflecting missiles.")]
		public readonly int Chance = 100;

		[Desc("Optional AmmoPool name consumed by successful defensive engagements. Leave empty for unlimited jamming.")]
		public readonly string AmmoPool = null;

		[Desc("Ammo consumed by one successful defensive engagement.")]
		public readonly int AmmoUsage = 1;

		[Desc("Ticks before another missile can consume ammo and be deflected.")]
		public readonly int InterceptCooldown = 0;

		[Desc("Sound played when a missile is intercepted.")]
		public readonly string InterceptSound = null;

		public override object Create(ActorInitializer init) { return new JamsMissiles(init.Self, this); }
	}

	public class JamsMissiles : ConditionalTrait<JamsMissilesInfo>, ITick, ISync
	{
		readonly Actor self;
		AmmoPool ammoPool;

		[VerifySync]
		int cooldown;

		public WDist Range => IsTraitDisabled ? WDist.Zero : Info.Range;
		public PlayerRelationship DeflectionStances => Info.DeflectionRelationships;
		public int Chance => Info.Chance;

		public JamsMissiles(Actor self, JamsMissilesInfo info)
			: base(info)
		{
			this.self = self;
		}

		protected override void Created(Actor self)
		{
			base.Created(self);

			if (!string.IsNullOrEmpty(Info.AmmoPool))
				ammoPool = self.TraitsImplementing<AmmoPool>().FirstOrDefault(pool => pool.Info.Name == Info.AmmoPool);
		}

		void ITick.Tick(Actor self)
		{
			if (cooldown > 0)
				cooldown--;
		}

		public bool TryJam()
		{
			if (IsTraitDisabled || cooldown > 0)
				return false;

			if (ammoPool != null && (!ammoPool.HasAmmo || !ammoPool.TakeAmmo(self, Info.AmmoUsage)))
				return false;

			cooldown = Info.InterceptCooldown;
			if (!string.IsNullOrEmpty(Info.InterceptSound))
				Game.Sound.Play(SoundType.World, Info.InterceptSound, self.CenterPosition);

			return true;
		}
	}
}
