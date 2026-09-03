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
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Experience;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class ExperienceComposerTest
	{
		[Test]
		public void ShippedDefaultIncludesModernFactionsAndEnhancedMechanics()
		{
			var path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
				"..", "mods", "ra", "experiences.yaml"));
			var catalog = MiniYaml.FromFile(path, false).Single().Value;
			Assert.That(catalog.Nodes.Single(n => n.Key == "DefaultProfile").Value.Value, Is.EqualTo("world-war-iii"));
			var profiles = catalog.Nodes.Single(n => n.Key == "Profiles").Value;
			var components = profiles.Nodes.Single(n => n.Key == "world-war-iii").Value.Nodes
				.Single(n => n.Key == "Components").Value.Value.Split(',').Select(id => id.Trim()).ToArray();
			foreach (var id in new[] { "saudi-arabia-faction", "turkey-faction", "china-faction", "yemen-faction", "iran-faction",
				"building-garrisons", "teleport-network", "commander-promotions", "point-defense-interception" })
				Assert.That(components, Does.Contain(id));
		}

		[TestCase(TestName = "The default experience enables the complete World War III portfolio")]
		public void DefaultExperienceIsWorldWarThree()
		{
			var settings = new ExperienceSettings();
			Assert.That(settings.Profile, Is.EqualTo("world-war-iii"));
			Assert.That(settings.UseCustomComponents, Is.False);
			Assert.That(settings.EnabledComponents, Is.Empty);
			Assert.That(settings.PresentationPack, Is.EqualTo(PresentationPackDefinition.Default.Id));
		}

		[TestCase(TestName = "Experience parameters validate and normalize typed values")]
		public void NormalizeExperienceParameters()
		{
			var integer = Parameter("""
				Title: Range
				Description: Test range
				Type: Integer
				Default: 50
				Minimum: 25
				Maximum: 100
				Step: 5
				""");
			Assert.That(integer.Normalize("83"), Is.EqualTo("85"));
			Assert.That(integer.Normalize("500"), Is.EqualTo("100"));

			var boolean = Parameter("""
				Title: Effects
				Description: Test effects
				Type: Boolean
				Default: true
				""");
			Assert.That(boolean.Normalize("FALSE"), Is.EqualTo("false"));

			var choice = Parameter("""
				Title: Faction
				Description: Test faction
				Type: Choice
				Default: All
				Options: All, turkey, iran
				""");
			Assert.That(choice.Normalize("TURKEY"), Is.EqualTo("turkey"));
			Assert.Throws<InvalidDataException>(() => choice.Normalize("unknown"));
		}

		[TestCase(TestName = "Presentation packs may be created empty and remain data-only")]
		public void LoadEmptyPresentationPack()
		{
			var root = Path.Combine(Path.GetTempPath(), "openra-experience-test-" + Guid.NewGuid().ToString("N"));
			try
			{
				Directory.CreateDirectory(Path.Combine(root, "assets"));
				File.WriteAllText(Path.Combine(root, "pack.yaml"), """
					PresentationPack:
						Id: empty-pack
						Title: Empty Pack
						Version: 1
						Author: Test
						License: Test-only
						Assets: assets
						Replaces:
					""");

				var pack = PresentationPackDefinition.Load(root);
				Assert.That(pack.Id, Is.EqualTo("empty-pack"));
				Assert.That(pack.Replaces, Is.Empty);
			}
			finally
			{
				if (Directory.Exists(root))
					Directory.Delete(root, true);
			}
		}

		[TestCase(TestName = "Capability packs load as namespaced data-only experience modules")]
		public void LoadCapabilityPack()
		{
			var root = CreateCapabilityPack("true");
			try
			{
				var pack = CapabilityPackDefinition.Load(root, "ra");
				Assert.That(pack.Id, Is.EqualTo("test-capability"));
				Assert.That(pack.Component.IsExternal, Is.True);
				Assert.That(pack.Component.Rules.Single(),
					Is.EqualTo("experience-packs/test-capability/rules.yaml"));
				Assert.That(pack.Component.Preview,
					Is.EqualTo("experience-packs/test-capability/preview.png"));
				Assert.That(pack.Fingerprint, Has.Length.EqualTo(64));
			}
			finally
			{
				if (Directory.Exists(root))
					Directory.Delete(root, true);
			}
		}

		[TestCase(TestName = "Capability packs require an explicit redistribution-rights acknowledgement")]
		public void RejectCapabilityPackWithoutRights()
		{
			var root = CreateCapabilityPack("false");
			try
			{
				Assert.Throws<InvalidDataException>(() => CapabilityPackDefinition.Load(root, "ra"));
			}
			finally
			{
				if (Directory.Exists(root))
					Directory.Delete(root, true);
			}
		}

		[TestCase(TestName = "Capability packs reject executable or compiled code")]
		public void RejectExecutableCapabilityPack()
		{
			var root = CreateCapabilityPack("true");
			try
			{
				File.WriteAllText(Path.Combine(root, "plugin.dll"), "not executable, but still prohibited");
				Assert.Throws<InvalidDataException>(() => CapabilityPackDefinition.Load(root, "ra"));
			}
			finally
			{
				if (Directory.Exists(root))
					Directory.Delete(root, true);
			}
		}

		[TestCase(TestName = "Faction packs require preview, roster, and registration metadata")]
		public void LoadFactionCapabilityPack()
		{
			var root = CreateFactionCapabilityPack();
			try
			{
				var pack = CapabilityPackDefinition.Load(root, "ra");
				Assert.That(pack.Component.Kind, Is.EqualTo(ExperienceComponentKind.Faction));
				Assert.That(pack.Component.Faction.InternalName, Is.EqualTo("test-faction"));
				Assert.That(pack.Component.Faction.RandomPool, Is.EqualTo("RandomAllies"));
				Assert.That(pack.Component.Faction.ActorCount, Is.EqualTo(6));
				Assert.That(pack.Component.Faction.Preview,
					Is.EqualTo("experience-packs/test-faction/preview.png"));
				Assert.That(pack.Component.Preview, Is.EqualTo(pack.Component.Faction.Preview));
			}
			finally
			{
				if (Directory.Exists(root))
					Directory.Delete(root, true);
			}
		}

		[TestCase(TestName = "Faction packs reject incomplete roster metadata")]
		public void RejectIncompleteFactionRoster()
		{
			Assert.Throws<InvalidDataException>(() => Component("""
				Title: Incomplete faction
				Description: Missing a navy roster.
				Effects: Adds a test faction.
				Tradeoffs: Test only.
				Scope: Test only.
				Category: Faction packs
				Version: 1
				Kind: Faction
				Faction:
					InternalName: incomplete
					Side: Allies
					RandomPool: RandomAllies
					Doctrine: test
					Preview: preview.png
					Roster:
						Infantry: TESTINF
						Vehicles: TESTVEH
						Aircraft: TESTAIR
						Buildings: TESTBUILDING
						Defenses: TESTDEFENSE
				"""));
		}

		[TestCase(TestName = "Data-only factions extend random faction pools without replacing them")]
		public void ExtendRandomFactionPool()
		{
			var randomAllies = Faction("RandomAllies", "england, france");
			var turkey = Faction("turkey", randomFactionMemberOf: "RandomAllies");
			var iran = Faction("iran", randomFactionMemberOf: "RandomSoviet");

			Assert.That(Player.RandomFactionMembers(randomAllies, [randomAllies, turkey, iran]),
				Is.EquivalentTo(new[] { "england", "france", "turkey" }));
		}

		[TestCase(TestName = "Built-in faction packs compose complete starting-unit definitions")]
		public void BuiltInFactionPacksComposeStartingUnits()
		{
			var rulesDirectory = Path.GetFullPath(Path.Combine(
				TestContext.CurrentContext.TestDirectory, "..", "mods", "ra", "rules"));
			var files = new[] { "world.yaml", "china.yaml", "iran.yaml", "red-sea.yaml", "turkey.yaml" };
			var definitions = files.SelectMany(file =>
			{
				var path = Path.Combine(rulesDirectory, file);
				return MiniYaml.FromFile(path, false)
					.Where(node => node.Key == "World")
					.SelectMany(node => node.Value.Nodes)
					.Where(node => node.Key.StartsWith("StartingUnits@", StringComparison.Ordinal))
					.Select(node => (node.Key, Info: FieldLoader.Load<StartingUnitsInfo>(node.Value), File: file));
			}).ToArray();

			var duplicateKeys = definitions.GroupBy(definition => definition.Key)
				.Where(group => group.Count() > 1)
				.Select(group => $"{group.Key} ({group.Select(definition => definition.File).JoinWith(", ")})")
				.ToArray();
			Assert.That(duplicateKeys, Is.Empty,
				"Faction packs must not override starting-unit traits from another pack.");

			foreach (var faction in new[] { "china", "iran", "saudi", "turkey", "yemen" })
				foreach (var startingUnitsClass in new[] { "none", "light", "heavy" })
					Assert.That(definitions.Count(definition => definition.Info.Class == startingUnitsClass &&
						definition.Info.Factions.Contains(faction)), Is.EqualTo(1),
						$"Faction `{faction}` must define exactly one `{startingUnitsClass}` starting-unit group.");
		}

		[TestCase(TestName = "Built-in faction packs compose AI type extensions without replacing each other")]
		public void BuiltInFactionPacksComposeBotTypes()
		{
			var rulesDirectory = Path.GetFullPath(Path.Combine(
				TestContext.CurrentContext.TestDirectory, "..", "mods", "ra", "rules"));
			var files = new[] { "ai.yaml", "china.yaml", "iran.yaml", "red-sea.yaml", "turkey.yaml" };
			var player = MiniYaml.Merge(files.Select(file =>
				MiniYaml.FromFile(Path.Combine(rulesDirectory, file), false).Where(node => node.Key == "Player")))
				.Single(node => node.Key == "Player");

			var baseBuilder = FieldLoader.Load<BaseBuilderBotModuleInfo>(player.Value.Nodes
				.Single(node => node.Key == "BaseBuilderBotModule@normal").Value);
			Assert.That(baseBuilder.AdditionalProductionTypes.Keys,
				Is.EquivalentTo(new[] { "china", "iran", "red-sea", "turkey" }));
			Assert.That(baseBuilder.AdditionalProductionTypes.Values.SelectMany(types => types),
				Does.Contain("irhpad").And.Contain("safld"));
			Assert.That(baseBuilder.AdditionalDefenseTypes["china"],
				Does.Contain("cnbastion").And.Contain("cnskyshield").And.Contain("cnspectrum"));

			var squadManager = FieldLoader.Load<SquadManagerBotModuleInfo>(player.Value.Nodes
				.Single(node => node.Key == "SquadManagerBotModule@normal").Value);
			Assert.That(squadManager.AdditionalAirUnitsTypes.Keys,
				Is.EquivalentTo(new[] { "china", "iran", "red-sea", "turkey" }));
			Assert.That(squadManager.AdditionalNavalUnitsTypes.Keys,
				Is.EquivalentTo(new[] { "china", "iran", "turkey" }));
		}

		static string CreateCapabilityPack(string rightsAcknowledged)
		{
			var root = Path.Combine(Path.GetTempPath(), "openra-capability-test-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(root);
			File.WriteAllText(Path.Combine(root, "rules.yaml"), "World:\n");
			File.WriteAllBytes(Path.Combine(root, "preview.png"), [137, 80, 78, 71]);
			File.WriteAllText(Path.Combine(root, "pack.yaml"), $$"""
				CapabilityPack:
					Id: test-capability
					Title: Test Capability
					Version: 1
					Author: OpenRA Test
					License: GPL-3.0-or-later
					Source: Automated test fixture
					TargetMod: ra
					EngineApi: experience-v2
					RightsAcknowledged: {{rightsAcknowledged}}
					Component:
						Title: Test Capability
						Description: Test-only reusable module.
						Effects: Adds a test-only reusable capability.
						Tradeoffs: Exists only for automated validation.
						Scope: Test fixtures only.
						Category: Tests
						Preview: preview.png
						Version: 1
						Source: Automated test fixture
						License: GPL-3.0-or-later
						Rules: rules.yaml
				""");
			return root;
		}

		static string CreateFactionCapabilityPack()
		{
			var root = Path.Combine(Path.GetTempPath(), "openra-faction-pack-test-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(root);
			File.WriteAllBytes(Path.Combine(root, "preview.png"), [137, 80, 78, 71]);
			File.WriteAllText(Path.Combine(root, "rules.yaml"), """
				World:
					Faction@test-faction:
						Name: Test Faction
						InternalName: test-faction
						Side: Allies
						RandomFactionMemberOf: RandomAllies
				""");
			File.WriteAllText(Path.Combine(root, "pack.yaml"), """
				CapabilityPack:
					Id: test-faction
					Title: Test Faction
					Version: 1
					Author: OpenRA Test
					License: GPL-3.0-or-later
					Source: Automated test fixture
					TargetMod: ra
					EngineApi: experience-v2
					RightsAcknowledged: true
					Component:
						Title: Test Faction
						Description: Test-only data faction.
						Effects: Adds a complete test faction contract.
						Tradeoffs: Exists only for automated validation.
						Scope: Test fixtures only.
						Category: Faction packs
						Version: 1
						Source: Automated test fixture
						License: GPL-3.0-or-later
						Kind: Faction
						Rules: rules.yaml
						Faction:
							InternalName: test-faction
							Side: Allies
							RandomPool: RandomAllies
							Doctrine: test-doctrine
							Preview: preview.png
							Roster:
								Infantry: TESTINF
								Vehicles: TESTVEH
								Aircraft: TESTAIR
								Navy: TESTSHIP
								Buildings: TESTBUILDING
								Defenses: TESTDEFENSE
				""");
			return root;
		}

		static FactionInfo Faction(string internalName, string randomFactionMembers = null,
			string randomFactionMemberOf = null)
		{
			var nodes = new List<MiniYamlNode> { new("InternalName", internalName) };
			if (randomFactionMembers != null)
				nodes.Add(new("RandomFactionMembers", randomFactionMembers));
			if (randomFactionMemberOf != null)
				nodes.Add(new("RandomFactionMemberOf", randomFactionMemberOf));

			return FieldLoader.Load<FactionInfo>(new MiniYaml("", nodes));
		}

		[TestCase(TestName = "Experience components require complete player-facing disclosure metadata")]
		public void ExperienceComponentDisclosureIsRequired()
		{
			var component = Component("""
				Title: Layered defense
				Description: Adds a defensive system.
				Effects: Intercepts compatible projectiles.
				Tradeoffs: Saturation attacks can overwhelm it.
				Scope: Only affects actors that inherit the contract.
				Category: Defenses
				Preview: preview.png
				Version: 1
				""");
			Assert.That(component.Effects, Does.Contain("Intercepts"));
			Assert.That(component.Tradeoffs, Does.Contain("Saturation"));
			Assert.That(component.Scope, Does.Contain("Only affects"));

			Assert.Throws<InvalidDataException>(() => Component("""
				Title: Incomplete
				Description: Missing the required disclosure fields.
				Category: Test
				Version: 1
				"""));
		}

		[TestCase(TestName = "Every selectable Experience component requires a PNG preview")]
		public void SelectableExperienceComponentsRequirePngPreviews()
		{
			Assert.Throws<InvalidDataException>(() => Component("""
				Title: No preview
				Description: A selectable component without artwork.
				Effects: Adds a test effect.
				Tradeoffs: Test only.
				Scope: Test fixtures only.
				Category: Test
				Version: 1
				"""));

			Assert.Throws<InvalidDataException>(() => Component("""
				Title: Wrong preview type
				Description: A selectable component with invalid artwork.
				Effects: Adds a test effect.
				Tradeoffs: Test only.
				Scope: Test fixtures only.
				Category: Test
				Preview: preview.jpg
				Version: 1
				"""));

			var internalComponent = Component("""
				Title: Internal dependency
				Description: Hidden implementation data.
				Effects: Supports selectable components.
				Tradeoffs: Not directly selectable.
				Scope: Test fixtures only.
				Category: Test
				Kind: Internal
				Hidden: true
				Version: 1
				""");
			Assert.That(internalComponent.Preview, Is.Null);
		}

		[TestCase(TestName = "Experience review reports effective module, parameter, and presentation changes")]
		public void ExperienceReviewReportsEffectiveChanges()
		{
			var components = Components();
			var before = components.Values.SelectMany(component => component.Parameters.Values.Select(parameter =>
				KeyValuePair.Create(ExperienceCatalog.ParameterKey(component.Id, parameter.Id), parameter.Default)))
				.ToDictionary(kv => kv.Key, kv => kv.Value);
			var after = before.ToDictionary(kv => kv.Key, kv => kv.Value);
			after[ExperienceCatalog.ParameterKey("dependent", "amount")] = "75";
			var review = new ExperienceReviewModel(components, ["standalone"], ["framework", "dependent"],
				before, after, "default", "alternate");

			Assert.That(review.RegisteredCount, Is.EqualTo(3));
			Assert.That(review.NewlyEnabledComponents.Select(c => c.Id), Is.EquivalentTo(new[] { "framework", "dependent" }));
			Assert.That(review.NewlyDisabledComponents.Single().Id, Is.EqualTo("standalone"));
			Assert.That(review.RequiredBy["framework"].Single().Id, Is.EqualTo("dependent"));
			Assert.That(review.ParameterChanges.Single().Value, Is.EqualTo("75"));
			Assert.That(review.PresentationChanged, Is.True);
			Assert.That(review.ChangeCount, Is.EqualTo(5));
		}

		static ExperienceParameter Parameter(string yaml)
		{
			var indented = yaml.Split('\n', StringSplitOptions.RemoveEmptyEntries)
				.Select(line => "\t" + line.Trim()).JoinWith("\n");
			var node = MiniYaml.FromString("Parameter:\n" + indented, "experience-parameter-test").Single();
			return new ExperienceParameter("test", node.Value);
		}

		static ExperienceComponent Component(string yaml, string id = "test")
		{
			var indented = yaml.Split('\n', StringSplitOptions.RemoveEmptyEntries)
				.Select(line => "\t" + line.Trim()).JoinWith("\n");
			var node = MiniYaml.FromString("Component:\n" + indented, "experience-component-test").Single();
			return new ExperienceComponent(id, node.Value);
		}

		static IReadOnlyDictionary<string, ExperienceComponent> Components()
		{
			var yaml = MiniYaml.FromString("""
				Components:
					framework:
						Title: Framework
						Description: Shared framework.
						Effects: Publishes shared metadata.
						Tradeoffs: No direct mechanic.
						Scope: Compatible actors only.
						Category: Test
						Preview: preview.png
						Version: 1
					dependent:
						Title: Dependent
						Description: Dependent module.
						Effects: Adds a dependent effect.
						Tradeoffs: Has a counter.
						Scope: Authored maps only.
						Category: Test
						Preview: preview.png
						Version: 1
						Dependencies: framework
						Parameters:
							amount:
								Title: Amount
								Description: Effect amount.
								Type: Integer
								Default: 50
								Minimum: 0
								Maximum: 100
					standalone:
						Title: Standalone
						Description: Standalone module.
						Effects: Adds a standalone effect.
						Tradeoffs: Has another counter.
						Scope: Compatible actors only.
						Category: Test
						Preview: preview.png
						Version: 1
				""", "experience-catalog-test").Single();
			return yaml.Value.Nodes.Select(node => new ExperienceComponent(node.Key, node.Value))
				.ToDictionary(component => component.Id);
		}
	}
}
