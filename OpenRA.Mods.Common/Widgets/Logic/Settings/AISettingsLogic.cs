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
using System.Linq;
using System.Text.Json;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class AISettingsLogic : ChromeLogic
	{
		static readonly Dictionary<string, string> PaceLabels = new()
		{
			{ "calm", "Calm" },
			{ "balanced", "Balanced" },
			{ "frequent", "Frequent" }
		};

		static readonly Dictionary<string, string> VoicePriorityLabels = new()
		{
			{ "critical", "Critical only" },
			{ "important", "Important + critical" },
			{ "off", "Never for updates" }
		};

		bool companionEnabled = true;
		bool voiceEnabled = true;
		bool busy;
		string pace = "calm";
		string voicePriority = "critical";
		string status = "Connecting to the local AI layer...";
		string costTotal = "Session estimate: waiting for usage";
		string costBreakdown = "Text --  |  Speech --  |  Transcription --";
		string costAssumptions = "Estimates appear after the local companion reports its active routes.";

		TextFieldWidget routerUrl;
		TextFieldWidget textModel;
		TextFieldWidget visionModel;
		TextFieldWidget transcribeModel;
		TextFieldWidget speechModel;
		TextFieldWidget speechVoice;
		LabelWidget statusLabel;
		LabelWidget costAssumptionsLabel;

		[ObjectCreator.UseCtor]
		public AISettingsLogic(SettingsLogic settingsLogic, string panelID, string label)
		{
			settingsLogic.RegisterSettingsPanel(panelID, label, InitPanel, ResetPanel);
		}

		Func<bool> InitPanel(Widget panel)
		{
			var enabled = panel.Get<CheckboxWidget>("AI_ENABLED");
			enabled.IsChecked = () => companionEnabled;
			enabled.OnClick = () => companionEnabled = !companionEnabled;
			var voice = panel.Get<CheckboxWidget>("VOICE_ENABLED");
			voice.IsChecked = () => voiceEnabled;
			voice.OnClick = () => voiceEnabled = !voiceEnabled;

			BindDropdown(panel.Get<DropDownButtonWidget>("PACE"), PaceLabels, () => pace, value => pace = value);
			BindDropdown(panel.Get<DropDownButtonWidget>("VOICE_PRIORITY"), VoicePriorityLabels, () => voicePriority, value => voicePriority = value);

			routerUrl = panel.Get<TextFieldWidget>("ROUTER_URL");
			textModel = panel.Get<TextFieldWidget>("TEXT_MODEL");
			visionModel = panel.Get<TextFieldWidget>("VISION_MODEL");
			transcribeModel = panel.Get<TextFieldWidget>("TRANSCRIBE_MODEL");
			speechModel = panel.Get<TextFieldWidget>("SPEECH_MODEL");
			speechVoice = panel.Get<TextFieldWidget>("SPEECH_VOICE");

			statusLabel = panel.Get<LabelWidget>("STATUS");
			statusLabel.GetText = () => status;
			panel.Get<LabelWidget>("COST_TOTAL").GetText = () => costTotal;
			panel.Get<LabelWidget>("COST_BREAKDOWN").GetText = () => costBreakdown;
			costAssumptionsLabel = panel.Get<LabelWidget>("COST_ASSUMPTIONS");
			costAssumptionsLabel.GetText = () => costAssumptions;

			var apply = panel.Get<ButtonWidget>("APPLY");
			apply.IsDisabled = () => busy;
			apply.OnClick = () => _ = ApplyAsync();
			var test = panel.Get<ButtonWidget>("TEST");
			test.IsDisabled = () => busy;
			test.OnClick = () => _ = TestAsync();
			var refresh = panel.Get<ButtonWidget>("REFRESH_USAGE");
			refresh.IsDisabled = () => busy;
			refresh.OnClick = () => _ = LoadAsync();

			SettingsUtils.AdjustSettingsScrollPanelLayout(panel.Get<ScrollPanelWidget>("SETTINGS_SCROLLPANEL"));
			_ = LoadAsync();
			return () =>
			{
				YieldTextFocus();
				return false;
			};
		}

		Action ResetPanel(Widget panel)
		{
			return () =>
			{
				companionEnabled = true;
				voiceEnabled = true;
				pace = "calm";
				voicePriority = "critical";
				routerUrl.Text = "http://127.0.0.1:4000";
				textModel.Text = "gpt-5.5";
				visionModel.Text = "gpt-5.5";
				transcribeModel.Text = "openai-transcribe";
				speechModel.Text = "openai-tts";
				speechVoice.Text = "alloy";
				_ = ApplyAsync();
			};
		}

		static void BindDropdown(DropDownButtonWidget dropdown, Dictionary<string, string> options,
			Func<string> getValue, Action<string> setValue)
		{
			dropdown.GetText = () => options.TryGetValue(getValue(), out var value) ? value : getValue();
			dropdown.OnMouseDown = _ =>
			{
				ScrollItemWidget SetupItem(string key, ScrollItemWidget template)
				{
					var item = ScrollItemWidget.Setup(template, () => getValue() == key, () => setValue(key));
					item.Get<LabelWidget>("LABEL").GetText = () => options[key];
					return item;
				}

				dropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 300, options.Keys, SetupItem);
			};
		}

		async System.Threading.Tasks.Task LoadAsync()
		{
			SetBusy("Reading local AI settings...");
			try
			{
				var baseUri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_CONSOLE_URL", "http://127.0.0.1:8787/");
				using var document = await OpenRAAILocalClient.GetAsync(baseUri, "v1/state");
				var snapshot = document.RootElement.Clone();
				Game.RunAfterTick(() => ApplyState(snapshot, "AI layer connected. Settings and usage are current."));
			}
			catch (Exception e)
			{
				Game.RunAfterTick(() => SetIdle($"AI layer unavailable: {e.Message}"));
			}
		}

		async System.Threading.Tasks.Task ApplyAsync()
		{
			YieldTextFocus();
			SetBusy("Saving AI settings...");
			try
			{
				var baseUri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_CONSOLE_URL", "http://127.0.0.1:8787/");
				using var document = await OpenRAAILocalClient.PostAsync(baseUri, "v1/state", BuildPayload());
				var snapshot = document.RootElement.Clone();
				Game.RunAfterTick(() => ApplyState(snapshot, "AI settings saved. Changes apply immediately."));
			}
			catch (Exception e)
			{
				Game.RunAfterTick(() => SetIdle($"Could not save AI settings: {e.Message}"));
			}
		}

		async System.Threading.Tasks.Task TestAsync()
		{
			SetBusy("Testing text and voice routes...");
			try
			{
				var baseUri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_CONSOLE_URL", "http://127.0.0.1:8787/");
				using var savedState = await OpenRAAILocalClient.PostAsync(baseUri, "v1/state", BuildPayload());
				using var document = await OpenRAAILocalClient.PostAsync(baseUri, "v1/test/full", new { }, 45);
				var root = document.RootElement;
				var latency = root.GetProperty("text").GetProperty("latency_ms").GetInt32();
				var speechPlayed = root.GetProperty("ok").GetBoolean();
				Game.RunAfterTick(() =>
				{
					SetIdle(speechPlayed
						? $"Diagnostic passed. Text replied in {latency} ms; voice played locally."
						: $"Text passed in {latency} ms. Enable AI voice to include speech in this test.");
					_ = RefreshUsageAsync();
				});
			}
			catch (Exception e)
			{
				Game.RunAfterTick(() => SetIdle($"Diagnostic failed: {e.Message}"));
			}
		}

		Dictionary<string, object> BuildPayload()
		{
			return new Dictionary<string, object>
			{
				{ "companion_enabled", companionEnabled },
				{ "voice_enabled", voiceEnabled },
				{ "notification_pace", pace },
				{ "voice_priority", voicePriority },
				{ "router_url", routerUrl.Text.Trim() },
				{ "text_model", textModel.Text.Trim() },
				{ "vision_model", visionModel.Text.Trim() },
				{ "transcribe_model", transcribeModel.Text.Trim() },
				{ "speech_model", speechModel.Text.Trim() },
				{ "speech_voice", speechVoice.Text.Trim() }
			};
		}

		async System.Threading.Tasks.Task RefreshUsageAsync()
		{
			try
			{
				var baseUri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_CONSOLE_URL", "http://127.0.0.1:8787/");
				using var document = await OpenRAAILocalClient.GetAsync(baseUri, "v1/usage");
				var usage = document.RootElement.Clone();
				Game.RunAfterTick(() => ApplyUsage(usage));
			}
			catch { }
		}

		void ApplyState(JsonElement root, string message)
		{
			var config = root.GetProperty("config");
			companionEnabled = config.GetProperty("companion_enabled").GetBoolean();
			voiceEnabled = config.GetProperty("voice_enabled").GetBoolean();
			pace = config.GetProperty("notification_pace").GetString() ?? "calm";
			voicePriority = config.GetProperty("voice_priority").GetString() ?? "critical";
			routerUrl.Text = config.GetProperty("router_url").GetString() ?? "";
			textModel.Text = config.GetProperty("text_model").GetString() ?? "";
			visionModel.Text = config.GetProperty("vision_model").GetString() ?? "";
			transcribeModel.Text = config.GetProperty("transcribe_model").GetString() ?? "";
			speechModel.Text = config.GetProperty("speech_model").GetString() ?? "";
			speechVoice.Text = config.GetProperty("speech_voice").GetString() ?? "";
			ApplyUsage(root.GetProperty("usage"));
			SetIdle(message);
		}

		void ApplyUsage(JsonElement usage)
		{
			var total = usage.GetProperty("session_cost_usd").GetDouble();
			var hourly = usage.GetProperty("hourly_cost_usd").GetDouble();
			costTotal = $"Session estimate: ${total:0.000000}  |  Current pace: ${hourly:0.0000}/hour";
			costBreakdown = $"Text ${usage.GetProperty("text_cost_usd").GetDouble():0.000000}  |  " +
				$"Speech ${usage.GetProperty("speech_cost_usd").GetDouble():0.000000}  |  " +
				$"Transcription ${usage.GetProperty("transcription_cost_usd").GetDouble():0.000000}";
			var assumptions = string.Join("  |  ", usage.GetProperty("assumptions").EnumerateArray().Select(value => value.GetString()));
			costAssumptions = WidgetUtils.WrapText(assumptions, costAssumptionsLabel.Bounds.Width,
				Game.Renderer.Fonts[costAssumptionsLabel.Font]);
		}

		void SetBusy(string message)
		{
			busy = true;
			SetStatus(message);
		}

		void SetIdle(string message)
		{
			busy = false;
			SetStatus(message);
		}

		void SetStatus(string message)
		{
			status = statusLabel == null ? message : WidgetUtils.TruncateText(message, statusLabel.Bounds.Width,
				Game.Renderer.Fonts[statusLabel.Font]);
		}

		void YieldTextFocus()
		{
			routerUrl?.YieldKeyboardFocus();
			textModel?.YieldKeyboardFocus();
			visionModel?.YieldKeyboardFocus();
			transcribeModel?.YieldKeyboardFocus();
			speechModel?.YieldKeyboardFocus();
			speechVoice?.YieldKeyboardFocus();
		}
	}
}
