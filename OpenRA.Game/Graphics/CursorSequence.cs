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

using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public enum CursorEffectStyle
	{
		Cross,
		Sweep,
		Chevrons,
		Orbit,
		Crescent,
		Pulse,
		Sparks,
		Streaks,
		Sunburst
	}

	public sealed class CursorEffect
	{
		public readonly Color PrimaryColor = Color.White;
		public readonly Color SecondaryColor = Color.White;
		public readonly CursorEffectStyle Style = CursorEffectStyle.Cross;
		public readonly int FrameCount = 8;
		public readonly int Size = 40;

		public CursorEffect(MiniYaml info, CursorEffect defaults = null)
		{
			if (defaults != null)
			{
				PrimaryColor = defaults.PrimaryColor;
				SecondaryColor = defaults.SecondaryColor;
				Style = defaults.Style;
				FrameCount = defaults.FrameCount;
				Size = defaults.Size;
			}

			FieldLoader.Load(this, info);

			if (FrameCount < 1)
				throw new YamlException($"Cursor effect {nameof(FrameCount)} must be at least 1.");

			if (Size < 16 || Size > 128 || Size % 2 != 0)
				throw new YamlException($"Cursor effect {nameof(Size)} must be an even number between 16 and 128.");
		}
	}

	public class CursorSequence
	{
		public readonly string Name;
		public readonly string Src;
		public readonly int Start;
		public readonly int? Length;
		public readonly string Palette;
		public readonly int2 Hotspot;

		public CursorSequence(string name, string cursorSrc, string palette, MiniYaml info)
		{
			var d = info.ToDictionary();

			Palette = palette;
			Name = name;
			Src = cursorSrc;

			if (d.TryGetValue("X", out var yaml))
			{
				Exts.TryParseInt32Invariant(yaml.Value, out var x);
				Hotspot = Hotspot.WithX(x);
			}

			if (d.TryGetValue("Y", out yaml))
			{
				Exts.TryParseInt32Invariant(yaml.Value, out var y);
				Hotspot = Hotspot.WithY(y);
			}

			Start = Exts.ParseInt32Invariant(d["Start"].Value);
			if (d.TryGetValue("Length", out yaml))
				Length = yaml.Value != "*" ? Exts.ParseInt32Invariant(yaml.Value) : null;
			else if (d.TryGetValue("End", out yaml) && yaml.Value == "*")
				Length = yaml.Value != "*" ? Exts.ParseInt32Invariant(yaml.Value) - Start : null;
			else
				Length = 1;
		}
	}
}
