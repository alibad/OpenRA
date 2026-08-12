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
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	static class CursorEffectRenderer
	{
		static readonly int2[] OrbitPoints =
		[
			new(0, -1), new(1, -1), new(1, 0), new(1, 1),
			new(0, 1), new(-1, 1), new(-1, 0), new(-1, -1)
		];

		public static byte[] Render(CursorEffect effect, int frame)
		{
			var size = effect.Size;
			var data = new byte[4 * size * size];
			var center = size / 2;
			var phase = frame * 8 / effect.FrameCount;
			var pulse = phase <= 4 ? phase : 8 - phase;
			var radius = size / 4 + pulse / 2;

			// A restrained common halo keeps every effect legible over both terrain and chrome.
			for (var i = 0; i < OrbitPoints.Length; i++)
			{
				var p = OrbitPoints[i];
				SetPixel(data, size, center + p.X * radius, center + p.Y * radius,
					effect.PrimaryColor, 26 + 5 * pulse);
			}

			switch (effect.Style)
			{
				case CursorEffectStyle.Cross:
					DrawCross(data, size, center, radius, effect, phase);
					break;

				case CursorEffectStyle.Sweep:
					DrawSweep(data, size, center, radius, effect, phase);
					break;

				case CursorEffectStyle.Chevrons:
					DrawChevrons(data, size, center, radius, effect, phase);
					break;

				case CursorEffectStyle.Orbit:
					DrawOrbit(data, size, center, radius, effect, phase);
					break;

				case CursorEffectStyle.Crescent:
					DrawCrescent(data, size, center, radius, effect, phase);
					break;

				case CursorEffectStyle.Pulse:
					DrawPulse(data, size, center, radius, effect, phase);
					break;

				case CursorEffectStyle.Sparks:
					DrawSparks(data, size, center, radius, effect, phase);
					break;

				case CursorEffectStyle.Streaks:
					DrawStreaks(data, size, center, radius, effect, phase);
					break;

				case CursorEffectStyle.Sunburst:
					DrawSunburst(data, size, center, radius, effect, phase);
					break;
			}

			return data;
		}

		static void DrawCross(byte[] data, int size, int c, int radius, CursorEffect effect, int phase)
		{
			var alpha = 150 + 12 * (phase % 4);
			DrawSpark(data, size, c, c - radius, effect.SecondaryColor, alpha);
			DrawSpark(data, size, c + radius, c, effect.PrimaryColor, alpha);
			DrawSpark(data, size, c, c + radius, effect.SecondaryColor, alpha);
			DrawSpark(data, size, c - radius, c, effect.PrimaryColor, alpha);
		}

		static void DrawSweep(byte[] data, int size, int c, int radius, CursorEffect effect, int phase)
		{
			for (var i = 0; i < 4; i++)
			{
				var index = (phase + i) % OrbitPoints.Length;
				var p = OrbitPoints[index];
				var color = i < 2 ? effect.PrimaryColor : effect.SecondaryColor;
				DrawPoint(data, size, c + p.X * radius, c + p.Y * radius, color, 190 - i * 38);
			}
		}

		static void DrawChevrons(byte[] data, int size, int c, int radius, CursorEffect effect, int phase)
		{
			var inset = phase % 4 < 2 ? 0 : 1;
			var r = radius - inset;
			DrawChevron(data, size, c - r, c, 1, effect.PrimaryColor);
			DrawChevron(data, size, c + r, c, -1, effect.PrimaryColor);
			DrawPoint(data, size, c, c - r, effect.SecondaryColor, 180);
			DrawPoint(data, size, c, c + r, effect.SecondaryColor, 180);
		}

		static void DrawOrbit(byte[] data, int size, int c, int radius, CursorEffect effect, int phase)
		{
			var first = OrbitPoints[phase % OrbitPoints.Length];
			var second = OrbitPoints[(phase + 4) % OrbitPoints.Length];
			DrawSpark(data, size, c + first.X * radius, c + first.Y * radius, effect.PrimaryColor, 220);
			DrawSpark(data, size, c + second.X * radius, c + second.Y * radius, effect.SecondaryColor, 210);
			var trail = OrbitPoints[(phase + 7) % OrbitPoints.Length];
			DrawPoint(data, size, c + trail.X * radius, c + trail.Y * radius, effect.SecondaryColor, 105);
		}

		static void DrawCrescent(byte[] data, int size, int c, int radius, CursorEffect effect, int phase)
		{
			var shimmer = phase % 3;
			for (var y = -radius + 2; y <= radius - 2; y += 3)
			{
				var normalized = (float)y / radius;
				var x = (int)Math.Round(-Math.Sqrt(Math.Max(0, 1 - normalized * normalized)) * radius);
				DrawPoint(data, size, c + x + shimmer, c + y, effect.PrimaryColor, 150 + shimmer * 25);
			}

			DrawSpark(data, size, c + radius - 2, c - radius / 2, effect.SecondaryColor, 220);
		}

		static void DrawPulse(byte[] data, int size, int c, int radius, CursorEffect effect, int phase)
		{
			var alpha = 230 - 14 * phase;
			for (var i = 0; i < 4; i++)
			{
				var p = OrbitPoints[i * 2 + 1];
				DrawPoint(data, size, c + p.X * radius, c + p.Y * radius, effect.PrimaryColor, alpha);
			}

			DrawPoint(data, size, c, c - radius, effect.SecondaryColor, alpha);
			DrawPoint(data, size, c, c + radius, effect.SecondaryColor, alpha);
		}

		static void DrawSparks(byte[] data, int size, int c, int radius, CursorEffect effect, int phase)
		{
			var offset = phase % 4 - 1;
			DrawSpark(data, size, c - radius + offset, c - radius + offset, effect.PrimaryColor, 220);
			DrawSpark(data, size, c + radius - offset, c + radius - offset, effect.SecondaryColor, 220);
			DrawPoint(data, size, c + radius / 2, c - radius, effect.SecondaryColor, 160);
			DrawPoint(data, size, c - radius / 2, c + radius, effect.PrimaryColor, 160);
		}

		static void DrawStreaks(byte[] data, int size, int c, int radius, CursorEffect effect, int phase)
		{
			var drift = phase % 4 - 2;
			for (var i = -2; i <= 2; i++)
			{
				SetPixel(data, size, c - radius + i + drift, c + radius - i, effect.PrimaryColor, 185 - 18 * Math.Abs(i));
				SetPixel(data, size, c + radius + i + drift, c - radius - i, effect.SecondaryColor, 185 - 18 * Math.Abs(i));
			}
		}

		static void DrawSunburst(byte[] data, int size, int c, int radius, CursorEffect effect, int phase)
		{
			for (var i = 0; i < OrbitPoints.Length; i++)
			{
				var p = OrbitPoints[i];
				var distance = i == phase ? radius + 1 : radius - 1;
				var color = i % 2 == 0 ? effect.PrimaryColor : effect.SecondaryColor;
				var alpha = i == phase ? 235 : 105;
				if (i == phase)
					DrawSpark(data, size, c + p.X * distance, c + p.Y * distance, color, alpha);
				else
					DrawPoint(data, size, c + p.X * distance, c + p.Y * distance, color, alpha);
			}
		}

		static void DrawChevron(byte[] data, int size, int x, int y, int direction, Color color)
		{
			SetPixel(data, size, x, y, color, 230);
			SetPixel(data, size, x + direction, y - 1, color, 190);
			SetPixel(data, size, x + direction, y + 1, color, 190);
			SetPixel(data, size, x + 2 * direction, y - 2, color, 130);
			SetPixel(data, size, x + 2 * direction, y + 2, color, 130);
		}

		static void DrawSpark(byte[] data, int size, int x, int y, Color color, int alpha)
		{
			SetPixel(data, size, x, y, color, alpha);
			SetPixel(data, size, x - 1, y, color, alpha / 2);
			SetPixel(data, size, x + 1, y, color, alpha / 2);
			SetPixel(data, size, x, y - 1, color, alpha / 2);
			SetPixel(data, size, x, y + 1, color, alpha / 2);
		}

		static void DrawPoint(byte[] data, int size, int x, int y, Color color, int alpha)
		{
			SetPixel(data, size, x, y, color, alpha);
			SetPixel(data, size, x + 1, y, color, alpha / 3);
			SetPixel(data, size, x, y + 1, color, alpha / 3);
		}

		static void SetPixel(byte[] data, int size, int x, int y, Color color, int alpha)
		{
			if (x < 0 || y < 0 || x >= size || y >= size)
				return;

			alpha = (alpha * color.A / 255).Clamp(0, 255);
			var offset = 4 * (y * size + x);
			if (data[offset + 3] >= alpha)
				return;

			data[offset] = color.B;
			data[offset + 1] = color.G;
			data[offset + 2] = color.R;
			data[offset + 3] = (byte)alpha;
		}
	}
}
