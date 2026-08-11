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

using OpenRA.Scripting;

namespace OpenRA.Mods.Common.Scripting
{
	[ScriptGlobal("Camera")]
	public class CameraGlobal : ScriptGlobal
	{
		public CameraGlobal(ScriptContext context)
			: base(context) { }

		[Desc("The center of the visible viewport.")]
		public WPos Position
		{
			get => Game.IsHeadless ? WPos.Zero : Context.WorldRenderer.Viewport.CenterPosition;
			set
			{
				// Camera instructions are presentation-only. Headless mission
				// evaluation has no viewport, but visible games must never silently
				// ignore a recenter request and open outside the playable map.
				if (!Game.IsHeadless)
					Context.WorldRenderer.Viewport.Center(value);
			}
		}
	}
}
