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
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class EarthMapPreviewWidget : Widget
	{
		readonly Sprite pinSprite;
		Sheet mapSheet;
		Sprite mapSprite;
		float2 pin = new(0.5f, 0.5f);

		public Action<float2> OnMapClick = _ => { };

		public EarthMapPreviewWidget()
		{
			pinSprite = ChromeProvider.GetImage("lobby-bits", "spawn-unclaimed");
		}

		protected EarthMapPreviewWidget(EarthMapPreviewWidget other)
			: base(other)
		{
			pinSprite = ChromeProvider.GetImage("lobby-bits", "spawn-unclaimed");
			OnMapClick = other.OnMapClick;
		}

		public override EarthMapPreviewWidget Clone() { return new EarthMapPreviewWidget(this); }

		public void Update(Png preview, float2 pinPosition)
		{
			if (mapSheet == null || mapSheet.Size.Width < preview.Width || mapSheet.Size.Height < preview.Height)
			{
				mapSheet?.Dispose();
				mapSheet = new Sheet(SheetType.BGRA, new Size(preview.Width, preview.Height).NextPowerOf2());
			}

			var spriteRect = new Rectangle(0, 0, preview.Width, preview.Height);
			mapSprite = new Sprite(mapSheet, spriteRect, TextureChannel.RGBA);
			OpenRA.Graphics.Util.FastCopyIntoSprite(mapSprite, preview);
			mapSheet.CommitBufferedData();
			pin = new float2(pinPosition.X.Clamp(0f, 1f), pinPosition.Y.Clamp(0f, 1f));
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event != MouseInputEvent.Down || mi.Button != MouseButton.Left || mapSprite == null)
				return false;

			var point = mi.Location - RenderBounds.Location;
			OnMapClick(new float2(
				(point.X * 1f / RenderBounds.Width).Clamp(0f, 1f),
				(point.Y * 1f / RenderBounds.Height).Clamp(0f, 1f)));
			return true;
		}

		public override void Draw()
		{
			if (mapSprite == null)
				return;

			WidgetUtils.DrawSprite(mapSprite, RenderBounds.Location, RenderBounds.Size);
			var pinPosition = RenderBounds.Location + new int2(
				(int)(pin.X * RenderBounds.Width),
				(int)(pin.Y * RenderBounds.Height));
			WidgetUtils.DrawSprite(pinSprite, pinPosition - pinSprite.Size.XY.ToInt2() / 2);
		}

		public override void Removed()
		{
			base.Removed();
			mapSheet?.Dispose();
			mapSheet = null;
		}
	}
}
