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

namespace OpenRA.Test
{
	[TestFixture]
	sealed class ExperienceComposerTest
	{
		[TestCase(TestName = "The default experience keeps optional modules disabled")]
		public void DefaultExperienceIsAssistantOnly()
		{
			var settings = new ExperienceSettings();
			Assert.That(settings.Profile, Is.EqualTo("ai-assistant-only"));
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

		static string CreateCapabilityPack(string rightsAcknowledged)
		{
			var root = Path.Combine(Path.GetTempPath(), "openra-capability-test-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(root);
			File.WriteAllText(Path.Combine(root, "rules.yaml"), "World:\n");
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
						Version: 1
						Source: Automated test fixture
						License: GPL-3.0-or-later
						Rules: rules.yaml
				""");
			return root;
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
						Version: 1
					dependent:
						Title: Dependent
						Description: Dependent module.
						Effects: Adds a dependent effect.
						Tradeoffs: Has a counter.
						Scope: Authored maps only.
						Category: Test
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
						Version: 1
				""", "experience-catalog-test").Single();
			return yaml.Value.Nodes.Select(node => new ExperienceComponent(node.Key, node.Value))
				.ToDictionary(component => component.Id);
		}
	}
}
