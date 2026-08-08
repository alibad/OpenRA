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
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class CompanionStatusLogic : ChromeLogic
	{
		const int SidebarWidth = 250;
		const int EdgeMargin = 16;
		const int HorizontalPadding = 12;
		const int VerticalPadding = 8;
		const int VoiceButtonWidth = 94;
		const int VoiceButtonMargin = 4;
		const int VoiceButtonGap = 8;
		const int MinWidth = 320;
		const int MaxWidth = 860;

		[FluentReference]
		const string VoiceOn = "button-ai-companion-voice-on";

		[FluentReference]
		const string VoiceOff = "button-ai-companion-voice-off";

		[FluentReference]
		const string VoicePending = "button-ai-companion-voice-pending";

		[ObjectCreator.UseCtor]
		public CompanionStatusLogic(Widget widget)
		{
			var label = widget.Get<LabelWidget>("STATUS");
			var voiceButton = widget.Get<ButtonWidget>("VOICE_TOGGLE");
			var font = Game.Renderer.Fonts[label.Font];
			var displayMessage = "";
			var requestPending = false;

			voiceButton.GetText = () => requestPending
				? FluentProvider.GetMessage(VoicePending)
				: CompanionBridge.TryGetStatus(out _, out _, out var enabled, out var muted) && enabled && !muted
					? FluentProvider.GetMessage(VoiceOn)
					: FluentProvider.GetMessage(VoiceOff);
			voiceButton.IsHighlighted = () =>
				CompanionBridge.TryGetStatus(out _, out _, out var enabled, out var muted) && enabled && !muted;
			voiceButton.IsDisabled = () => requestPending ||
				!CompanionBridge.TryGetStatus(out _, out _, out var enabled, out _) || !enabled;
			voiceButton.OnClick = () =>
			{
				if (!CompanionBridge.TryGetStatus(out _, out _, out var enabled, out var muted) || !enabled)
					return;

				_ = SetVoiceMutedAsync(!muted);
			};

			async Task SetVoiceMutedAsync(bool muted)
			{
				requestPending = true;
				try
				{
					var configuredUrl = Environment.GetEnvironmentVariable("OPENRA_AI_CONSOLE_URL");
					var baseUrl = string.IsNullOrWhiteSpace(configuredUrl) ? "http://127.0.0.1:8787/" : configuredUrl;
					if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || !uri.IsLoopback)
						throw new InvalidOperationException("The companion control URL must be local.");

					using var client = HttpClientFactory.Create();
					client.Timeout = TimeSpan.FromSeconds(4);
					using var content = new StringContent(JsonSerializer.Serialize(new { muted }), Encoding.UTF8, "application/json");
					using var response = await client.PostAsync(new Uri(uri, "v1/control"), content);
					response.EnsureSuccessStatusCode();
					using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
					var appliedMuted = result.RootElement.GetProperty("muted").GetBoolean();
					Game.RunAfterTick(() => CompanionBridge.UpdateLocalVoiceState(appliedMuted));
				}
				catch (Exception e)
				{
					Log.Write("debug", $"Failed to update OpenRA AI voice state: {e}");
					Game.RunAfterTick(() => CompanionBridge.UpdateLocalControlError(
						"AI VOICE CONTROL UNAVAILABLE  •  GAMEPLAY UNAFFECTED"));
				}
				finally
				{
					Game.RunAfterTick(() => requestPending = false);
				}
			}

			widget.IsVisible = () =>
			{
				if (!CompanionBridge.TryGetStatus(out _, out var message))
					return false;

				var playableWidth = Math.Max(MinWidth, Game.Renderer.Resolution.Width - SidebarWidth);
				var availableWidth = Math.Max(2 * HorizontalPadding + 1, playableWidth - 2 * EdgeMargin);
				var maximumWidth = Math.Min(MaxWidth, availableWidth);
				var minimumWidth = Math.Min(MinWidth, maximumWidth);
				var desiredWidth = font.Measure(message).X + 2 * HorizontalPadding + VoiceButtonWidth + VoiceButtonGap;
				var width = Math.Clamp(desiredWidth, minimumWidth, maximumWidth);

				widget.Bounds.X = Math.Max(EdgeMargin, (playableWidth - width) / 2);
				widget.Bounds.Width = width;
				label.Bounds.X = HorizontalPadding;
				label.Bounds.Width = width - HorizontalPadding - VoiceButtonWidth - VoiceButtonMargin - VoiceButtonGap;
				voiceButton.Bounds.X = width - VoiceButtonWidth - VoiceButtonMargin;

				displayMessage = FitToTwoLines(message, label.Bounds.Width, font);
				var height = Math.Max(30, font.Measure(displayMessage).Y + VerticalPadding);
				widget.Bounds.Height = height;
				label.Bounds.Height = height;
				voiceButton.Bounds.Y = (height - voiceButton.Bounds.Height) / 2;
				return true;
			};

			label.GetText = () => displayMessage;
			label.GetColor = () =>
			{
				if (!CompanionBridge.TryGetStatus(out var state, out _))
					return Color.White;

				return state switch
				{
					"listening" or "transcript" => Color.Cyan,
					"transcribing" or "thinking" => Color.Gold,
					"speaking" or "insight" or "routine" => Color.LightGreen,
					"important" or "speaking-important" => Color.Gold,
					"critical" or "speaking-critical" or "error" => Color.OrangeRed,
					"muted" or "disabled" => Color.Gray,
					_ => Color.White
				};
			};
		}

		static string FitToTwoLines(string message, int width, SpriteFont font)
		{
			var wrapped = WidgetUtils.WrapText(message, width, font);
			var firstBreak = wrapped.IndexOf('\n');
			if (firstBreak < 0)
				return WidgetUtils.TruncateText(wrapped, width, font);

			var firstLine = WidgetUtils.TruncateText(wrapped[..firstBreak], width, font);
			var remaining = wrapped[(firstBreak + 1)..];
			var secondBreak = remaining.IndexOf('\n');
			if (secondBreak < 0)
				return $"{firstLine}\n{WidgetUtils.TruncateText(remaining, width, font)}";

			remaining = remaining.Replace('\n', ' ');
			return $"{firstLine}\n{WidgetUtils.TruncateText(remaining, width, font)}";
		}
	}
}
