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

using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class CompanionStatusLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public CompanionStatusLogic(Widget widget)
		{
			var label = widget.Get<LabelWidget>("STATUS");
			widget.IsVisible = () => CompanionBridge.TryGetStatus(out _, out _);
			label.GetText = () => CompanionBridge.TryGetStatus(out _, out var message) ? message : "";
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
	}
}
