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
		float cropLeft;
		float cropTop;
		float cropWidth = 1f;
		float cropHeight = 1f;

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

			var fullRect = new Rectangle(0, 0, preview.Width, preview.Height);
			var fullSprite = new Sprite(mapSheet, fullRect, TextureChannel.RGBA);
			OpenRA.Graphics.Util.FastCopyIntoSprite(fullSprite, preview);
			mapSheet.CommitBufferedData();

			var targetAspect = RenderBounds.Width * 1f / Math.Max(1, RenderBounds.Height);
			var sourceAspect = preview.Width * 1f / preview.Height;
			var spriteRect = fullRect;
			if (targetAspect > sourceAspect)
			{
				var height = Math.Max(1, (int)(preview.Width / targetAspect));
				spriteRect = new Rectangle(0, (preview.Height - height) / 2, preview.Width, height);
			}
			else if (targetAspect < sourceAspect)
			{
				var width = Math.Max(1, (int)(preview.Height * targetAspect));
				spriteRect = new Rectangle((preview.Width - width) / 2, 0, width, preview.Height);
			}

			mapSprite = new Sprite(mapSheet, spriteRect, TextureChannel.RGBA);
			cropLeft = spriteRect.X * 1f / preview.Width;
			cropTop = spriteRect.Y * 1f / preview.Height;
			cropWidth = spriteRect.Width * 1f / preview.Width;
			cropHeight = spriteRect.Height * 1f / preview.Height;
			pin = new float2(pinPosition.X.Clamp(0f, 1f), pinPosition.Y.Clamp(0f, 1f));
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event != MouseInputEvent.Down || mi.Button != MouseButton.Left || mapSprite == null)
				return false;

			var point = mi.Location - RenderBounds.Location;
			OnMapClick(new float2(
				(cropLeft + point.X * 1f / RenderBounds.Width * cropWidth).Clamp(0f, 1f),
				(cropTop + point.Y * 1f / RenderBounds.Height * cropHeight).Clamp(0f, 1f)));
			return true;
		}

		public override void Draw()
		{
			if (mapSprite == null)
				return;

			WidgetUtils.DrawSprite(mapSprite, RenderBounds.Location, RenderBounds.Size);
			var pinPosition = RenderBounds.Location + new int2(
				(int)((pin.X - cropLeft) / cropWidth * RenderBounds.Width),
				(int)((pin.Y - cropTop) / cropHeight * RenderBounds.Height));

			var radius = Math.Min(RenderBounds.Width, RenderBounds.Height) * 0.42f;
			var topLeft = new float3(pinPosition.X - radius, pinPosition.Y - radius, 0);
			var bottomRight = new float3(pinPosition.X + radius, pinPosition.Y + radius, 0);
			Game.Renderer.RgbaColorRenderer.FillEllipse(topLeft, bottomRight, Color.FromArgb(42, 244, 205, 67));
			var circle = new float3[48];
			for (var i = 0; i < circle.Length; i++)
			{
				var angle = i * Math.PI * 2 / circle.Length;
				circle[i] = new float3(
					pinPosition.X + (float)Math.Cos(angle) * radius,
					pinPosition.Y + (float)Math.Sin(angle) * radius,
					0);
			}

			Game.Renderer.RgbaColorRenderer.DrawPolygon(circle, 2, Color.FromArgb(220, 244, 205, 67));
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
