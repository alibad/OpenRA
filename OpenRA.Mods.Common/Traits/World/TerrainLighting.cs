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
using System.Numerics;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	[Desc("Add to the world actor to apply a global lighting tint and allow actors using the TerrainLightSource to add localised lighting.")]
	public class TerrainLightingInfo : TraitInfo, ILobbyCustomRulesIgnore
	{
		public readonly float Intensity = 1;
		public readonly float HeightStep = 0;
		public readonly float RedTint = 1;
		public readonly float GreenTint = 1;
		public readonly float BlueTint = 1;

		[Desc("Size of light source partition bins (cells)")]
		public readonly int BinSize = 10;

		[Desc("Shade slopes by how they face the light: 0 is off, 1 shades them again as much as the terrain art does.")]
		public readonly float SlopeShading = 0f; // Opt in per mode; preserve existing terrain lighting by default.

		[Desc("Direction towards the light in cell axes (x, y, up), the way the terrain art is lit.")]
		public readonly Vector3 SlopeLight = new(-0.55f, 0.15f, 0.82f);

		[Desc("Rise of one height level over one cell edge, the way the terrain art is drawn.")]
		public readonly float SlopeRise = 0.408f;

		[Desc("Darkest slope shade.")]
		public readonly float SlopeMinimum = 0.55f;

		[Desc("Brightest slope shade.")]
		public readonly float SlopeMaximum = 1.35f;

		public override object Create(ActorInitializer init) { return new TerrainLighting(init.World, this); }
	}

	public sealed class TerrainLighting : ITerrainLighting
	{
		sealed class LightSource(WPos pos, CPos cell, WDist range, float intensity, in Vector3 tint)
		{
			public readonly WPos Pos = pos;
			public readonly CPos Cell = cell;
			public readonly WDist Range = range;
			public readonly float Intensity = intensity;
			public readonly Vector3 Tint = tint;
		}

		readonly TerrainLightingInfo info;
		readonly Map map;
		readonly Dictionary<int, LightSource> lightSources = [];
		readonly SpatiallyPartitioned<LightSource> partitionedLightSources;
		readonly Vector3 globalTint;
		int nextLightSourceToken = 1;

		// Slope shading: the ground's surface height per cell, smoothed over its neighbours (not across cliffs), and a
		// shade per cell from the smoothed surface's slope, read between cell centres so it runs smoothly across tiles.
		const int SlopeSmoothing = 2;
		readonly CellLayer<float> surface;
		readonly CellLayer<float> smoothSurface;
		readonly CellLayer<float> slopeShade;
		readonly Vector3 slopeLight;

		public event Action<MPos> CellChanged = null;

		public TerrainLighting(World world, TerrainLightingInfo info)
		{
			this.info = info;
			map = world.Map;
			globalTint = new Vector3(info.RedTint, info.GreenTint, info.BlueTint);

			var tileScale = map.Grid.TileScale;
			partitionedLightSources = new SpatiallyPartitioned<LightSource>(
				(map.MapSize.Width + 1) * tileScale,
				(map.MapSize.Height + 1) * tileScale,
				info.BinSize * tileScale);

			if (info.SlopeShading > 0 && map.Grid.Type == MapGridType.RectangularIsometric)
			{
				slopeLight = Vector3.Normalize(info.SlopeLight);
				surface = new CellLayer<float>(map);
				smoothSurface = new CellLayer<float>(map);
				slopeShade = new CellLayer<float>(map);
				foreach (var uv in map.AllCells.MapCoords)
					surface[uv] = SurfaceOf(uv);
				foreach (var uv in map.AllCells.MapCoords)
					smoothSurface[uv] = SmoothSurfaceOf(uv.ToCPos(map));
				foreach (var uv in map.AllCells.MapCoords)
					slopeShade[uv] = SlopeShadeOf(uv.ToCPos(map));

				// The map editor changes heights and tiles (ramps): reshade around the cell, and retint there.
				map.Height.CellEntryChanged += SlopeChanged;
				map.Ramp.CellEntryChanged += SlopeChanged;
			}
		}

		/// <summary>The ground's height at the cell centre, in height levels.</summary>
		float SurfaceOf(MPos uv)
		{
			var ramp = map.Ramp.Contains(uv) ? map.Ramp[uv] : (byte)0;
			return map.Height[uv] + map.Grid.Ramps[ramp].CenterHeightOffset / (map.Grid.TileScale / 2f);
		}

		bool TrySurface(CPos cell, out float height)
		{
			var uv = cell.ToMPos(map);
			if (!surface.Contains(uv))
			{
				height = 0;
				return false;
			}

			height = surface[uv];
			return true;
		}

		// Two cells this far apart on one slope differ by at most this much; more is a cliff between them.
		static bool Joined(float a, float b, int distance) => Math.Abs(a - b) <= distance + 0.6f;

		float SmoothSurfaceOf(CPos cell)
		{
			if (!TrySurface(cell, out var own))
				return 0;

			float sum = 0, weights = 0;
			for (var dy = -SlopeSmoothing; dy <= SlopeSmoothing; dy++)
			{
				for (var dx = -SlopeSmoothing; dx <= SlopeSmoothing; dx++)
				{
					if (!TrySurface(cell + new CVec(dx, dy), out var h) || !Joined(own, h, Math.Max(Math.Abs(dx), Math.Abs(dy))))
						continue;

					// Binomial weights (1 4 6 4 1).
					var w = Binomial(dx) * Binomial(dy);
					sum += w * h;
					weights += w;
				}
			}

			return sum / weights;
		}

		static float Binomial(int d) => d == 0 ? 6 : Math.Abs(d) == 1 ? 4 : 1;

		float SlopeShadeOf(CPos cell)
		{
			var uv = cell.ToMPos(map);
			if (!smoothSurface.Contains(uv))
				return 1;

			// The smoothed surface's gradient along the cell axes (one-sided where a neighbour is past a cliff or the edge).
			var own = smoothSurface[uv];
			var gx = Gradient(cell, new CVec(1, 0), own);
			var gy = Gradient(cell, new CVec(0, 1), own);
			if (gx == 0 && gy == 0)
				return 1;

			var normal = Vector3.Normalize(new Vector3(-gx * info.SlopeRise, -gy * info.SlopeRise, 1));
			var facing = Vector3.Dot(normal, slopeLight) / slopeLight.Z;
			return Math.Clamp(1 + info.SlopeShading * (facing - 1), info.SlopeMinimum, info.SlopeMaximum);
		}

		float Gradient(CPos cell, CVec axis, float own)
		{
			var hasNext = TrySmooth(cell + axis, own, out var next);
			var hasPrevious = TrySmooth(cell - axis, own, out var previous);
			if (hasNext && hasPrevious)
				return (next - previous) / 2;

			if (hasNext)
				return next - own;

			if (hasPrevious)
				return own - previous;

			return 0;
		}

		bool TrySmooth(CPos cell, float own, out float height)
		{
			var uv = cell.ToMPos(map);
			if (!smoothSurface.Contains(uv) || !Joined(own, smoothSurface[uv], 1))
			{
				height = 0;
				return false;
			}

			height = smoothSurface[uv];
			return true;
		}

		void SlopeChanged(CPos cell)
		{
			var uv = cell.ToMPos(map);
			if (!surface.Contains(uv))
				return;

			var height = SurfaceOf(uv);
			if (height == surface[uv])
				return;

			surface[uv] = height;
			var reach = SlopeSmoothing + 1;
			for (var dy = -SlopeSmoothing; dy <= SlopeSmoothing; dy++)
			{
				for (var dx = -SlopeSmoothing; dx <= SlopeSmoothing; dx++)
				{
					var c = cell + new CVec(dx, dy);
					var cuv = c.ToMPos(map);
					if (smoothSurface.Contains(cuv))
						smoothSurface[cuv] = SmoothSurfaceOf(c);
				}
			}

			for (var dy = -reach; dy <= reach; dy++)
			{
				for (var dx = -reach; dx <= reach; dx++)
				{
					var c = cell + new CVec(dx, dy);
					var cuv = c.ToMPos(map);
					if (!slopeShade.Contains(cuv))
						continue;

					var shade = SlopeShadeOf(c);
					if (shade == slopeShade[cuv])
						continue;

					slopeShade[cuv] = shade;

					// Corners read the shades of the cells around them (TintAt): the neighbours' tints change too.
					if (CellChanged != null)
						for (var ny = -1; ny <= 1; ny++)
							for (var nx = -1; nx <= 1; nx++)
								if (slopeShade.Contains((c + new CVec(nx, ny)).ToMPos(map)))
									CellChanged((c + new CVec(nx, ny)).ToMPos(map));
				}
			}
		}

		float SlopeShadeAt(WPos pos)
		{
			// World to fractional cell coordinates (Map.CenterOfCell inverted), then bilinear between cell centres.
			var u = pos.X / 724f - 1;
			var v = pos.Y / 724f - 1;
			var cx = (u + v) / 2;
			var cy = (v - u) / 2;
			var x0 = (int)MathF.Floor(cx);
			var y0 = (int)MathF.Floor(cy);
			var fx = cx - x0;
			var fy = cy - y0;
			var own = ShadeOrDefault(new CPos(x0, y0), 1);
			var s00 = own;
			var s10 = ShadeOrDefault(new CPos(x0 + 1, y0), own);
			var s01 = ShadeOrDefault(new CPos(x0, y0 + 1), own);
			var s11 = ShadeOrDefault(new CPos(x0 + 1, y0 + 1), own);
			return (s00 * (1 - fx) + s10 * fx) * (1 - fy) + (s01 * (1 - fx) + s11 * fx) * fy;
		}

		float ShadeOrDefault(CPos cell, float fallback)
		{
			var uv = cell.ToMPos(map);
			return slopeShade.Contains(uv) ? slopeShade[uv] : fallback;
		}

		static Rectangle Bounds(LightSource source)
		{
			var c = source.Pos;
			var r = source.Range.Length;
			return new Rectangle(c.X - r, c.Y - r, 2 * r, 2 * r);
		}

		public int AddLightSource(WPos pos, WDist range, float intensity, in Vector3 tint)
		{
			var token = nextLightSourceToken++;
			var source = new LightSource(pos, map.CellContaining(pos), range, intensity, tint);
			var bounds = Bounds(source);
			lightSources.Add(token, source);
			partitionedLightSources.Add(source, bounds);

			if (CellChanged != null)
				foreach (var c in map.FindTilesInCircle(source.Cell, (source.Range.Length + 1023) / 1024))
					CellChanged(c.ToMPos(map));

			return token;
		}

		public void RemoveLightSource(int token)
		{
			if (!lightSources.TryGetValue(token, out var source))
				return;

			lightSources.Remove(token);
			partitionedLightSources.Remove(source);
			if (CellChanged != null)
				foreach (var c in map.FindTilesInCircle(source.Cell, (source.Range.Length + 1023) / 1024))
					CellChanged(c.ToMPos(map));
		}

		Vector3 ITerrainLighting.TintAt(WPos pos)
		{
			using (new PerfSample("terrain_lighting"))
			{
				var uv = map.CellContaining(pos).ToMPos(map);
				var tint = globalTint;
				if (!map.Height.Contains(uv))
					return tint;

				var intensity = info.Intensity + info.HeightStep * map.Height[uv];
				if (slopeShade != null)
					intensity *= SlopeShadeAt(pos);

				if (lightSources.Count > 0)
				{
					foreach (var source in partitionedLightSources.At(new int2(pos.X, pos.Y)))
					{
						var range = source.Range.Length;
						var distance = (source.Pos - pos).Length;
						if (distance > range)
							continue;

						var falloff = (range - distance) * 1f / range;
						intensity += falloff * source.Intensity;
						tint += falloff * source.Tint;
					}
				}

				return intensity * tint;
			}
		}
	}
}
