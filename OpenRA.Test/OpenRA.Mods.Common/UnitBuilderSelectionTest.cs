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
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class UnitBuilderSelectionTest
	{
		[Test]
		public void RoleOnlyUnitsAreTrackedAndReplaceMissingCombatRoles()
		{
			var info = Info("""
				RoleShares:
					armor: 50
					anti-air: 50
				""");
			var tank = Unit("tank", "armor");
			var importedAA = Unit("imported-aa", "anti-air");
			Assert.That(info.UnitsToBuild, Is.Empty);
			Assert.That(info.GetUnitTypesToTrack([tank, importedAA, Unit("unrelated", "scout")]),
				Is.EquivalentTo(new[] { "tank", "imported-aa" }));
			Assert.That(UnitBuilderBotModule.ChooseUnitToBuild(info, [importedAA, tank], [importedAA], 100, _ => true),
				Is.SameAs(tank));
			Assert.That(UnitBuilderBotModule.ChooseUnitToBuild(info, [tank, importedAA], [tank], 100, _ => true),
				Is.SameAs(importedAA));
		}

		[Test]
		public void RoleTrackingExcludesDefenseQueuesAndKeepsExplicitTransformedActors()
		{
			var info = Info("""
				UnitsToBuild:
					deployed-tank: 20
				RoleShares:
					anti-air: 50
				""");
			var actors = new[]
			{
				Unit("mobile-aa", "anti-air"),
				Unit("aa-defense", "anti-air", "Defense"),
				Unit("script-only-aa", "anti-air", null),
				Unit("deployed-tank", "anti-air", null)
			};
			Assert.That(info.GetUnitTypesToTrack(actors), Is.EquivalentTo(new[] { "mobile-aa", "deployed-tank" }));
		}

		[Test]
		public void OtherProductionQueuesCannotStarveVehicleBuiltAircraft()
		{
			var info = Info("""
				RoleShares:
					main-battle-tank: 26
					anti-air: 12
					artillery: 12
					strike-aircraft: 6
					line-infantry: 24
					naval-screen: 8
				""");
			var tank = Unit("tank", "main-battle-tank");
			var antiAir = Unit("anti-air", "anti-air");
			var artillery = Unit("artillery", "artillery");
			var drone = Unit("drone", "strike-aircraft");
			ActorInfo[] factoryArmy = [.. Enumerable.Repeat(tank, 13), .. Enumerable.Repeat(antiAir, 3),
				.. Enumerable.Repeat(artillery, 4)];
			ActorInfo[] combinedArmy = [.. factoryArmy,
				.. Enumerable.Repeat(Unit("rifle", "line-infantry", "Infantry"), 25),
				.. Enumerable.Repeat(Unit("ship", "naval-screen", "Ship"), 25)];
			Assert.That(UnitBuilderBotModule.ChooseUnitToBuild(info, [tank, antiAir, artillery, drone], factoryArmy,
				100, _ => true, "Vehicle"), Is.SameAs(drone));
			Assert.That(UnitBuilderBotModule.ChooseUnitToBuild(info, [tank, antiAir, artillery, drone], combinedArmy,
				100, _ => true, "Vehicle"), Is.SameAs(drone));
		}

		[Test]
		public void RoleWeightsAreRelativeAndExcludeUnavailableRoles()
		{
			var smallWeights = Info("""
				RoleShares:
					armor: 2
					strike-aircraft: 1
					artillery: 100
				""");
			var largeWeights = Info("""
				RoleShares:
					armor: 200
					strike-aircraft: 100
					artillery: 10000
				""");
			var tank = Unit("tank", "armor");
			var drone = Unit("drone", "strike-aircraft");
			var artillery = Unit("artillery", "artillery");
			ActorInfo[] owned = [tank, .. Enumerable.Repeat(artillery, 100)];
			foreach (var info in new[] { smallWeights, largeWeights })
				Assert.That(UnitBuilderBotModule.ChooseUnitToBuild(info, [tank, drone, artillery], owned,
					100, unit => unit != artillery, "Vehicle"), Is.SameAs(drone));
		}

		[Test]
		public void MultiQueueActorsCountOnceAndCappedAlternativeStillCountsForItsRole()
		{
			var info = Info("""
				RoleShares:
					armor: 1
					strike-aircraft: 1
				UnitLimits:
					limited-drone: 1
				""");
			var tank = Unit("tank", "armor");
			var limited = Unit("limited-drone", "strike-aircraft", "Vehicle, Aircraft");
			var alternate = Unit("alternate-drone", "strike-aircraft");
			Assert.That(UnitBuilderBotModule.ChooseUnitToBuild(info, [alternate, limited, tank], [limited],
				100, _ => true, "Vehicle"), Is.SameAs(tank));
		}

		[Test]
		public void RoleOnlyUnitLimitsAndTechDelaysAreRespected()
		{
			var info = Info("""
				RoleShares:
					armor: 30
					support: 70
				UnitLimits:
					imported-support: 1
				UnitDelays:
					tank: 200
				""");
			var tank = Unit("tank", "armor");
			var support = Unit("imported-support", "support");
			Assert.That(UnitBuilderBotModule.ChooseUnitToBuild(info, [support, tank], [support], 100, _ => true), Is.Null);
			Assert.That(UnitBuilderBotModule.ChooseUnitToBuild(info, [support, tank], [support], 200, _ => true), Is.SameAs(tank));
		}

		[Test]
		public void AnUnavailablePreferredAircraftDoesNotBlockAnotherRole()
		{
			var info = Info("""
				RoleShares:
					interceptor: 80
					strike-aircraft: 20
				""");
			var fighter = Unit("fighter", "interceptor");
			var helicopter = Unit("helicopter", "strike-aircraft");
			Assert.That(UnitBuilderBotModule.ChooseUnitToBuild(info, [fighter, helicopter], [], 100,
				unit => unit != fighter), Is.SameAs(helicopter));
			Assert.That(UnitBuilderBotModule.ChooseUnitToBuild(info, [fighter, helicopter], [], 100,
				_ => false), Is.Null);
		}

		[Test]
		public void ActorSpecificProductionAlsoSkipsUnavailableAircraft()
		{
			var info = Info("""
				UnitsToBuild:
					fighter: 80
					helicopter: 20
				""");
			var fighter = Unit("fighter", "interceptor");
			var helicopter = Unit("helicopter", "strike-aircraft");
			Assert.That(UnitBuilderBotModule.ChooseUnitToBuild(info, [fighter, helicopter], [], 100,
				unit => unit != fighter), Is.SameAs(helicopter));
		}

		[Test]
		public void ZeroSharesDoNotRecruitUnwantedUnits()
		{
			var info = Info("""
				UnitsToBuild:
					unwanted: 0
				RoleShares:
					unused: 0
				""");
			Assert.That(UnitBuilderBotModule.ChooseUnitToBuild(info, [Unit("unwanted", "unused")], [], 100,
				_ => true), Is.Null);
		}

		static UnitBuilderBotModuleInfo Info(string yaml)
		{
			return FieldLoader.Load<UnitBuilderBotModuleInfo>(new MiniYaml(null, MiniYaml.FromString(yaml, "unit-builder-test")));
		}

		static ActorInfo Unit(string name, string role, string queue = "Vehicle")
		{
			var info = FieldLoader.Load<StrategicRoleInfo>(new MiniYaml(null,
				MiniYaml.FromString($"Roles: {role}", "unit-builder-test")));
			if (queue == null)
				return new ActorInfo(name, info);

			var buildable = FieldLoader.Load<BuildableInfo>(new MiniYaml(null,
				MiniYaml.FromString($"Queue: {queue}", "unit-builder-test")));
			return new ActorInfo(name, info, buildable);
		}
	}
}
