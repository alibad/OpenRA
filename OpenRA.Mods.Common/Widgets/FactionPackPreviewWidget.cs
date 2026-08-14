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
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	/// <summary>Displays a scaled PNG identity card for a data-driven faction pack.</summary>
	public sealed class FactionPackPreviewWidget : Widget
	{
		Sheet sheet;
		Sprite sprite;

		public FactionPackPreviewWidget() { }

		FactionPackPreviewWidget(FactionPackPreviewWidget other)
			: base(other) { }

		public override FactionPackPreviewWidget Clone() { return new FactionPackPreviewWidget(this); }

		public bool Update(IReadOnlyFileSystem fileSystem, string path)
		{
			Clear();
			if (string.IsNullOrWhiteSpace(path) || !fileSystem.Exists(path))
				return false;

			try
			{
				var preview = new Png(fileSystem.Open(path));
				var spriteBounds = new Rectangle(0, 0, preview.Width, preview.Height);
				sheet = new Sheet(SheetType.BGRA, spriteBounds.Size.NextPowerOf2());
				sprite = new Sprite(sheet, spriteBounds, TextureChannel.RGBA);
				OpenRA.Graphics.Util.FastCopyIntoSprite(sprite, preview);
				sheet.CommitBufferedData();
				sheet.GetTexture().ScaleFilter = TextureScaleFilter.Linear;
				return true;
			}
			catch
			{
				Clear();
				return false;
			}
		}

		public void Clear()
		{
			sprite = null;
			sheet?.Dispose();
			sheet = null;
		}

		public override void Draw()
		{
			if (sprite == null)
				return;

			var scale = Math.Min(RenderBounds.Width / (float)sprite.Size.X, RenderBounds.Height / (float)sprite.Size.Y);
			var size = (scale * sprite.Size.XY).ToInt2();
			var location = new int2(RenderBounds.X, RenderBounds.Y) +
				(new int2(RenderBounds.Width, RenderBounds.Height) - size) / 2;
			WidgetUtils.DrawSprite(sprite, location, size);
		}

		public override void Removed()
		{
			base.Removed();
			Clear();
		}
	}
}
