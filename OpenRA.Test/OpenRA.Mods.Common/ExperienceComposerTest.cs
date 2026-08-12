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

		static ExperienceParameter Parameter(string yaml)
		{
			var indented = yaml.Split('\n', StringSplitOptions.RemoveEmptyEntries)
				.Select(line => "\t" + line.Trim()).JoinWith("\n");
			var node = MiniYaml.FromString("Parameter:\n" + indented, "experience-parameter-test").Single();
			return new ExperienceParameter("test", node.Value);
		}
	}
}
