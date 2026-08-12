#region Copyright & License Information
/*
 * Adapted for OpenRA AI from OpenRA Combined Arms PointDefense.cs.
 * Copyright (c) The OpenRA Combined Arms Developers (see upstream CREDITS).
 * Adaptation copyright (c) OpenRA AI contributors.
 *
 * This file is free software: you can redistribute it and/or modify it under
 * the terms of the GNU General Public License as published by the Free
 * Software Foundation, either version 3 of the License, or (at your option)
 * any later version. For more information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Experience;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Intercepts opt-in projectile classes using a named armament.")]
	public class PointDefenseInfo : ConditionalTraitInfo, Requires<ArmamentInfo>
	{
		[FieldLoader.Require]
		[Desc("Name of the Armament trait used to fire the interception effect.")]
		public readonly string Armament = null;

		[FieldLoader.Require]
		[Desc("Projectile PointDefenseType values this actor can intercept.")]
		public readonly BitSet<string> PointDefenseTypes = default;

		[Desc("Projectile-owner relationships that may be intercepted.")]
		public readonly PlayerRelationship ValidRelationships = PlayerRelationship.Neutral | PlayerRelationship.Enemy;

		public override object Create(ActorInitializer init) { return new PointDefense(init.Self, this); }
	}

	public class PointDefense : ConditionalTrait<PointDefenseInfo>, IPointDefense, ITick
	{
		readonly Actor self;
		readonly Armament armament;
		readonly int rangePercent;
		readonly bool interceptEffects;
		INotifyPointDefenseHit[] notifyHit;
		bool hasFiredThisTick;

		public PointDefense(Actor self, PointDefenseInfo info)
			: base(info)
		{
			this.self = self;
			armament = self.TraitsImplementing<Armament>().First(a => a.Info.Name == info.Armament);
			var experience = Game.ModData.GetOrNull<ExperienceCatalog>();
			rangePercent = experience?.GetIntegerParameter(
				"point-defense-interception", "range-percent", 100) ?? 100;
			interceptEffects = experience?.GetBooleanParameter(
				"point-defense-interception", "intercept-effects", true) ?? true;
		}

		protected override void Created(Actor self)
		{
			notifyHit = self.TraitsImplementing<INotifyPointDefenseHit>().ToArray();
			base.Created(self);
		}

		void ITick.Tick(Actor self) { hasFiredThisTick = false; }

		bool IPointDefense.TryIntercept(WPos position, Player projectileOwner, string type, ProjectileArgs args)
		{
			if (IsTraitDisabled || armament.IsTraitDisabled || armament.IsTraitPaused || hasFiredThisTick || armament.IsReloading)
				return false;

			if (!Info.ValidRelationships.HasRelationship(self.Owner.RelationshipWith(projectileOwner)) ||
				!Info.PointDefenseTypes.Contains(type) ||
				(self.CenterPosition - position).HorizontalLengthSquared >
					armament.MaxRange().LengthSquared * rangePercent * rangePercent / 10000)
				return false;

			if (interceptEffects && !armament.CheckFire(self, null, Target.FromPos(position)))
				return false;

			hasFiredThisTick = true;
			var damagePrevented = args.Weapon.Warheads.OfType<DamageWarhead>()
				.Sum(w => Util.ApplyPercentageModifiers(w.Damage, args.DamageModifiers));
			foreach (var notify in notifyHit)
				notify.Hit(damagePrevented);

			return true;
		}
	}
}
