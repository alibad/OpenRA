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
using NUnit.Framework;
using OpenRA.Mods.Cnc.Traits;
using CncUtil = OpenRA.Mods.Cnc.Util;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class ModelRendererTest
	{
		// RA2/TS isometric grid: 60x30 (RA2) tiles at TileScale 1448
		const float TileWidth = 60;
		const float TileHeight = 30;
		const float TileScale = 1448;

		// Mirrors WorldRenderer.ScreenVectorComponents
		static float3 ScreenVector(int x, int y, int z)
		{
			return new float3(TileWidth * x / TileScale, TileHeight * (y - z) / TileScale, TileHeight * z / TileScale);
		}

		// RenderVoxels' camera for a given BodyOrientation.CameraPitch
		static float[] Camera(int cameraPitch, int yaw = 256)
		{
			return CncUtil.MakeFloatMatrix(new WRot(WAngle.Zero, new WAngle(cameraPitch) - new WAngle(256), new WAngle(yaw)).AsMatrix());
		}

		static float[] ToModelSpace(float[] camera, float3 screen, float depth)
		{
			return CncUtil.MatrixVectorMultiply(CncUtil.MatrixInverse(camera), [screen.X, screen.Y, depth, 0]);
		}

		[TestCase(85, 256, TestName = "RA2/TS voxel camera")]
		[TestCase(171, 256, TestName = "BodyOrientation default camera (40 degrees)")]
		[TestCase(85, 356, TestName = "Yawed camera")]
		public void GroundOffsetStaysInGroundPlane(int cameraPitch, int yaw)
		{
			var camera = Camera(cameraPitch, yaw);
			foreach (var (x, y) in new[] { (0, 529), (0, -217), (443, 0), (-300, 250) })
			{
				var ground = ScreenVector(x, y, 0);
				var depth = ModelRenderer.OffsetDepth(ground, ScreenVector(0, 0, 0), camera);
				var model = ToModelSpace(camera, ground, depth);
				Assert.That(model[2], Is.EqualTo(0).Within(1e-3), $"offset {x},{y},0 leaves the ground plane");
			}
		}

		[TestCase(85, TestName = "RA2/TS voxel camera (height)")]
		[TestCase(171, TestName = "BodyOrientation default camera (height)")]
		public void HeightOffsetStaysOnUpAxis(int cameraPitch)
		{
			var camera = Camera(cameraPitch);
			var height = ScreenVector(0, 0, 400);
			var depth = ModelRenderer.OffsetDepth(ScreenVector(0, 0, 0), height, camera);
			var model = ToModelSpace(camera, height, depth);
			Assert.That(model[0], Is.EqualTo(0).Within(1e-3));
			Assert.That(model[1], Is.EqualTo(0).Within(1e-3));
			Assert.That(model[2], Is.GreaterThan(0));
		}

		[TestCase(TestName = "Yawed camera: height offset is as close to the up axis as its screen position allows")]
		public void HeightOffsetClosestToUpAxis()
		{
			// With a camera yaw other than 256 the up axis is not vertical on screen, so ScreenVector's
			// (vertical) screen position cannot be reached exactly: the depth must minimise the horizontal error.
			var camera = Camera(85, 356);
			var height = ScreenVector(0, 0, 400);
			var depth = ModelRenderer.OffsetDepth(ScreenVector(0, 0, 0), height, camera);
			float Horizontal(float d)
			{
				var m = ToModelSpace(camera, height, d);
				return m[0] * m[0] + m[1] * m[1];
			}

			Assert.That(Horizontal(depth), Is.LessThan(Horizontal(depth - 0.1f)));
			Assert.That(Horizontal(depth), Is.LessThan(Horizontal(depth + 0.1f)));
		}

		[TestCase(TestName = "RA2 depth per 1448 WDist: 52.9 forward, 17.0 up")]
		public void RA2Depth()
		{
			// CameraPitch 85: a forward (+Y, towards the viewer) offset comes closer to the camera,
			// where ScreenVector's depth only used Z (30 px per 1448 WDist up, 0 forward).
			var camera = Camera(85);
			var forward = ModelRenderer.OffsetDepth(ScreenVector(0, 1448, 0), ScreenVector(0, 0, 0), camera);
			var up = ModelRenderer.OffsetDepth(ScreenVector(0, 0, 0), ScreenVector(0, 0, 1448), camera);
			var sideways = ModelRenderer.OffsetDepth(ScreenVector(1448, 0, 0), ScreenVector(0, 0, 0), camera);
			Assert.That(forward, Is.EqualTo(52.88).Within(0.05));
			Assert.That(up, Is.EqualTo(17.02).Within(0.05));
			Assert.That(sideways, Is.EqualTo(0).Within(1e-3));
		}

		[TestCase(TestName = "Top-down camera keeps ScreenVector's height depth")]
		public void TopDownCamera()
		{
			var camera = Camera(256);
			var height = ScreenVector(0, 0, 724);
			Assert.That(ModelRenderer.OffsetDepth(ScreenVector(300, 200, 0), height, camera), Is.EqualTo(height.Z).Within(1e-4));
		}
	}
}
