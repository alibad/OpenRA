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
using OpenRA.GameRules;
using OpenRA.Mods.Common.Experience;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Fires a dedicated weapon at an explicitly targeted actor or cell, with a synchronized cooldown.")]
	public sealed class TargetedWeaponAbilityInfo : PausableConditionalTraitInfo, IRulesetLoaded
	{
		[WeaponReference]
		[FieldLoader.Require]
		public readonly string Weapon = null;

		[Desc("Stable order identifier. Multiple abilities on one actor must use different identifiers.")]
		public readonly string OrderName = "TargetedWeaponAbility";

		[Desc("Base cooldown in ticks.")]
		public readonly int Cooldown = 500;

		[Desc("Target relationships accepted by actor targets.")]
		public readonly PlayerRelationship ValidRelationships = PlayerRelationship.Enemy | PlayerRelationship.Neutral;

		[Desc("Allow targeting terrain cells in addition to actors.")]
		public readonly bool TargetTerrain = true;

		[Desc("Require force-fire while hovering a target, preventing the ability from replacing normal attack orders.")]
		public readonly bool RequiresForceFire = true;

		[CursorReference]
		public readonly string Cursor = "ability";

		[CursorReference]
		public readonly string BlockedCursor = "ability-blocked";

		[VoiceReference]
		public readonly string Voice = "Action";

		public readonly Color CooldownColor = Color.Cyan;

		public WeaponInfo WeaponInfo { get; private set; }

		public override object Create(ActorInitializer init) { return new TargetedWeaponAbility(this); }

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);
			if (!rules.Weapons.TryGetValue(Weapon.ToLowerInvariant(), out var weapon))
				throw new YamlException($"Weapons Ruleset does not contain an entry '{Weapon}'.");

			WeaponInfo = weapon;
			if (Cooldown < 1)
				throw new YamlException("TargetedWeaponAbility Cooldown must be greater than zero.");
		}
	}

	public sealed class TargetedWeaponAbility : PausableConditionalTrait<TargetedWeaponAbilityInfo>,
		IIssueOrder, IResolveOrder, IOrderVoice, ITick, ISelectionBar, ISync
	{
		readonly int cooldownDuration;

		[VerifySync]
		int cooldown;

		public TargetedWeaponAbility(TargetedWeaponAbilityInfo info)
			: base(info)
		{
			var experience = Game.ModData.GetOrNull<ExperienceCatalog>();
			var rate = experience?.IsComponentActive("targeted-unit-abilities") == true ?
				experience.GetIntegerParameter("targeted-unit-abilities", "cooldown-rate", 100) : 100;
			cooldownDuration = Math.Max(1, info.Cooldown * rate / 100);
		}

		public IEnumerable<IOrderTargeter> Orders
		{
			get
			{
				if (!IsTraitDisabled)
					yield return new AbilityOrderTargeter(this);
			}
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

			Fire(self, order.Target);
			cooldown = cooldownDuration;
		}

		void Fire(Actor self, in Target target)
		{
			var weapon = Info.WeaponInfo;
			if (weapon.Projectile == null)
				weapon.Impact(target, self);
			else
			{
				var source = self.CenterPosition;
				var args = new ProjectileArgs
				{
					Weapon = weapon,
					DamageModifiers = [],
					InaccuracyModifiers = [],
					RangeModifiers = [],
					Facing = WAngle.Zero,
					CurrentMuzzleFacing = () => WAngle.Zero,
					Source = source,
					CurrentSource = () => self.IsDead ? source : self.CenterPosition,
					SourceActor = self,
					PassiveTarget = target.CenterPosition,
					GuidedTarget = target
				};
				self.World.AddFrameEndTask(w => w.Add(weapon.Projectile.Create(args)));
			}

			if (weapon.Report.Length > 0)
				Game.Sound.Play(SoundType.World, weapon.Report, self.World, self.CenterPosition);
		}

		bool CanTarget(Actor self, in Target target)
		{
			if (IsTraitDisabled || IsTraitPaused || cooldown > 0 || !target.IsValidFor(self) ||
				!target.IsInRange(self.CenterPosition, Info.WeaponInfo.Range))
				return false;

			if (target.Type == TargetType.Terrain && !Info.TargetTerrain)
				return false;

			if (target.Type == TargetType.Actor &&
				!Info.ValidRelationships.HasRelationship(self.Owner.RelationshipWith(target.Actor.Owner)))
				return false;

			if (target.Type == TargetType.FrozenActor &&
				!Info.ValidRelationships.HasRelationship(self.Owner.RelationshipWith(target.FrozenActor.Owner)))
				return false;

			return Info.WeaponInfo.IsValidAgainst(target, self.World, self);
		}

		void ITick.Tick(Actor self)
		{
			if (cooldown > 0 && !IsTraitPaused)
				cooldown--;
		}

		public string VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == Info.OrderName ? Info.Voice : null;
		}

		float ISelectionBar.GetValue() { return cooldownDuration == 0 ? 0 : (float)cooldown / cooldownDuration; }
		Color ISelectionBar.GetColor() { return Info.CooldownColor; }
		bool ISelectionBar.DisplayWhenEmpty => false;

		sealed class AbilityOrderTargeter : IOrderTargeter
		{
			readonly TargetedWeaponAbility ability;

			public AbilityOrderTargeter(TargetedWeaponAbility ability) { this.ability = ability; }
			public string OrderID => ability.Info.OrderName;
			public int OrderPriority => 7;
			public bool IsQueued { get; private set; }
			public bool TargetOverridesSelection(Actor self, in Target target, List<Actor> actorsAt,
				CPos xy, TargetModifiers modifiers) => true;

			public bool CanTarget(Actor self, in Target target, ref TargetModifiers modifiers,
				ref string cursor)
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
}
