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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class AIControlTest
	{
		[TestCase(TestName = "AUTO reports startup until the companion acknowledges its state")]
		public void AutoButtonShowsStartupState()
		{
			Assert.That(AIControlDisplay.AutoButtonText(false, false, false), Is.EqualTo("AUTO: STARTING…"));
			Assert.That(AIControlDisplay.AutoButtonText(false, true, false), Is.EqualTo("AUTO: OFF"));
			Assert.That(AIControlDisplay.AutoButtonText(false, true, true), Is.EqualTo("AUTO: ON"));
		}

		[TestCase(TestName = "AI shortcuts keep their platform defaults and remain data-defined")]
		public void AIHotkeysUsePlatformDefaults()
		{
			var hotkeysPath = Path.GetFullPath(Path.Combine(
				TestContext.CurrentContext.TestDirectory, "..", "mods", "common", "hotkeys", "game.yaml"));
			var definitions = MiniYaml.FromFile(hotkeysPath, false)
				.Where(node => node.Key?.StartsWith("AI") == true)
				.ToDictionary(node => node.Key, node => new HotkeyDefinition(node.Key, node.Value));

			var ask = definitions["AIAsk"].Default;
			Assert.That(ask.Key, Is.EqualTo(Keycode.SPACE));
			Assert.That(ask.Modifiers, Is.EqualTo(
				Platform.CurrentPlatform == PlatformType.OSX ? Modifiers.Alt : Modifiers.Ctrl));
			Assert.That(ModifiersExts.FluentKey(Modifiers.Alt, PlatformType.OSX),
				Is.EqualTo("keycode-modifier.option"));
			AssertHotkey(definitions["AIAccept"].Default, Keycode.RETURN, Modifiers.Ctrl);
			AssertHotkey(definitions["AIReject"].Default, Keycode.BACKSPACE, Modifiers.Ctrl);
			AssertHotkey(definitions["AIToggleAuto"].Default, Keycode.A, Modifiers.Ctrl | Modifiers.Shift);
			AssertHotkey(definitions["AIToggleVoice"].Default, Keycode.M, Modifiers.Ctrl | Modifiers.Shift);
		}

		[TestCase(TestName = "The Ask hotkey checks setup before sending the voice press and release lifecycle")]
		public void AskHotkeyTargetsCompanionVoiceEndpoints()
		{
			var logicPath = Path.GetFullPath(Path.Combine(
				TestContext.CurrentContext.TestDirectory, "..", "OpenRA.Mods.Common", "Widgets", "Logic", "Ingame", "AIHotkeyLogic.cs"));
			var logic = File.ReadAllText(logicPath);

			Assert.That(logic, Does.Contain("v1/voice/readiness"));
			Assert.That(logic, Does.Contain("v1/voice/start"));
			Assert.That(logic, Does.Contain("v1/voice/stop"));
			Assert.That(logic, Does.Contain("v1/local-ai/{operation}"));
			Assert.That(logic, Does.Contain("ConfirmationDialogs.ButtonPrompt"));
			Assert.That(logic, Does.Contain("askKey.IsActivatedBy(e)"));

			var fluentPath = Path.GetFullPath(Path.Combine(
				TestContext.CurrentContext.TestDirectory, "..", "mods", "common", "fluent", "chrome.ftl"));
			var fluent = File.ReadAllText(fluentPath);
			Assert.That(fluent, Does.Contain("dialog-ai-voice-setup-install ="));
			Assert.That(fluent, Does.Contain("Install Local AI Pack"));
			Assert.That(fluent, Does.Contain("Settings > AI > Models"));
		}

		[TestCase(TestName = "AI settings show the configured Ask binding and a setup action")]
		public void AISettingsExposeVoiceShortcutAndLocalSetup()
		{
			Assert.That(AISettingsDisplay.AskShortcut("Option + Space"), Is.EqualTo("HOLD OPTION + SPACE TO TALK"));
			Assert.That(AISettingsDisplay.DiagnosticFailure(true, false),
				Is.EqualTo("Local AI is not installed yet. Open Models and select Install Local AI."));

			var chromePath = Path.GetFullPath(Path.Combine(
				TestContext.CurrentContext.TestDirectory, "..", "mods", "common", "chrome", "settings-ai.yaml"));
			var chrome = File.ReadAllText(chromePath);
			Assert.That(chrome, Does.Contain("Container@VOICE_SHORTCUT_ROW"));
			Assert.That(chrome, Does.Contain("Button@INSTALL_LOCAL_AI"));
		}

		[TestCase(TestName = "Verified launcher state removes the companion startup gap")]
		public void CompanionUsesVerifiedLauncherStateImmediately()
		{
			var environment = new Dictionary<string, string>
			{
				{ "OPENRA_AI_COMPANION_READY", "1" },
				{ "OPENRA_AI_STARTUP_ENABLED", "true" },
				{ "OPENRA_AI_STARTUP_MUTED", "0" },
				{ "OPENRA_AI_STARTUP_AUTO_ACT", "yes" },
				{ "OPENRA_AI_STARTUP_STRATEGY", "adaptive" }
			};
			var startup = CompanionBridge.StartupState(name => environment.GetValueOrDefault(name));

			Assert.That(startup.Ready, Is.True);
			Assert.That(startup.Enabled, Is.True);
			Assert.That(startup.Muted, Is.False);
			Assert.That(startup.AutoAct, Is.True);
			Assert.That(startup.Strategy, Is.EqualTo("adaptive"));
		}

		static void AssertHotkey(Hotkey actual, Keycode key, Modifiers modifiers)
		{
			Assert.That(actual.Key, Is.EqualTo(key));
			Assert.That(actual.Modifiers, Is.EqualTo(modifiers));
		}
	}
}
