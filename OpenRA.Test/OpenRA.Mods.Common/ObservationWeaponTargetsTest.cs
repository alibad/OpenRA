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
using NUnit.Framework;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class ObservationWeaponTargetsTest
	{
		static ObservationWeaponTargets Targets() => new([
			new BitSet<TargetableType>("Ground", "Vehicle"),
			new BitSet<TargetableType>("Ground", "Structure"),
			new BitSet<TargetableType>("Water", "Ship"),
			new BitSet<TargetableType>("Underwater", "Submarine"),
			new BitSet<TargetableType>("Air", "Aircraft"),
			new BitSet<TargetableType>("Air", "CustomDrone")
		]);

		[Test]
		public void SecondaryAntiAirWeaponContributesToCombinedCapabilities()
		{
			var targets = Targets();
			var cannon = Weapon("Ground, Water");
			var missile = Weapon("Air");
			Assert.That(targets.Summarize([cannon, missile]), Is.EqualTo((true, true)));
			Assert.That(targets.Summarize([missile, cannon]), Is.EqualTo((true, true)));
		}

		[TestCase("Water, Structure")]
		[TestCase("Structure")]
		[TestCase("Underwater")]
		public void SurfaceAndSubmarineWeaponsDoNotRequireLiteralGroundTarget(string targetTypes)
		{
			Assert.That(Targets().Summarize([Weapon(targetTypes)]), Is.EqualTo((false, true)));
		}

		[Test]
		public void InvalidTargetTypesOverrideMatchingValidTypes()
		{
			var targets = Targets();
			Assert.That(targets.Summarize([Weapon("Ground, Air", "Air")]), Is.EqualTo((false, true)));
			Assert.That(targets.Summarize([Weapon("Structure", "Ground")]), Is.EqualTo((false, false)));
		}

		[Test]
		public void ModSpecificTargetProfilesUseTheirActualDomain()
		{
			var targets = Targets();
			Assert.That(targets.Summarize([Weapon("CustomDrone")]), Is.EqualTo((true, false)));
			Assert.That(targets.Summarize([Weapon("MissingTargetType")]), Is.EqualTo((false, false)));
		}

		[Test]
		public void SwitchingActiveWeaponsDoesNotKeepPreviousModeCapabilities()
		{
			var targets = Targets();
			var antiTank = Weapon("Ground");
			var antiAir = Weapon("Air");
			Assert.That(targets.Summarize([antiTank]), Is.EqualTo((false, true)));
			Assert.That(targets.Summarize([antiAir]), Is.EqualTo((true, false)));
			Assert.That(targets.Summarize([]), Is.EqualTo((false, false)));
			Assert.That(targets.Summarize([antiTank]), Is.EqualTo((false, true)));
		}

		static WeaponInfo Weapon(string validTargets, string invalidTargets = "")
		{
			return new WeaponInfo(MiniYaml.FromString($"""
				Weapon:
					ValidTargets: {validTargets}
					InvalidTargets: {invalidTargets}
				""", "observation-weapon-test").Single().Value);
		}
	}
}
