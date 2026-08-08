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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class CompanionStatusLogic : ChromeLogic
	{
		const int SidebarWidth = 250;
		const int EdgeMargin = 16;
		const int HorizontalPadding = 12;
		const int VerticalPadding = 8;
		const int MinWidth = 320;
		const int MaxWidth = 860;

		[ObjectCreator.UseCtor]
		public CompanionStatusLogic(Widget widget)
		{
			var label = widget.Get<LabelWidget>("STATUS");
			var font = Game.Renderer.Fonts[label.Font];
			var displayMessage = "";

			widget.IsVisible = () =>
			{
				if (!CompanionBridge.TryGetStatus(out _, out var message))
					return false;

				var playableWidth = Math.Max(MinWidth, Game.Renderer.Resolution.Width - SidebarWidth);
				var availableWidth = Math.Max(2 * HorizontalPadding + 1, playableWidth - 2 * EdgeMargin);
				var maximumWidth = Math.Min(MaxWidth, availableWidth);
				var minimumWidth = Math.Min(MinWidth, maximumWidth);
				var desiredWidth = font.Measure(message).X + 2 * HorizontalPadding;
				var width = Math.Clamp(desiredWidth, minimumWidth, maximumWidth);

				widget.Bounds.X = Math.Max(EdgeMargin, (playableWidth - width) / 2);
				widget.Bounds.Width = width;
				label.Bounds.X = HorizontalPadding;
				label.Bounds.Width = width - 2 * HorizontalPadding;

				displayMessage = FitToTwoLines(message, label.Bounds.Width, font);
				var height = Math.Max(30, font.Measure(displayMessage).Y + VerticalPadding);
				widget.Bounds.Height = height;
				label.Bounds.Height = height;
				return true;
			};

			label.GetText = () => displayMessage;
			label.GetColor = () =>
			{
				if (!CompanionBridge.TryGetStatus(out var state, out _))
					return Color.White;

				return state switch
				{
					"listening" => Color.Cyan,
					"thinking" => Color.Gold,
					"speaking" or "insight" => Color.LightGreen,
					"error" => Color.OrangeRed,
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
