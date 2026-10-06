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

using System.Collections.Generic;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class WRotTest
	{
		// The slope orientations of MapGrid.Ramps: rotations about these axes by these angles.
		static readonly WVec[] RampAxes = [new(724, 724, 0), new(-724, 724, 0), new(0, 1024, 0), new(1024, 0, 0)];
		static readonly int[] RampAngles = [64, -64, 48, -48];

		static IEnumerable<WRot> RampOrientations()
		{
			yield return WRot.None;
			foreach (var axis in RampAxes)
				foreach (var angle in RampAngles)
					yield return new WRot(axis, new WAngle(angle));
		}

		[Test]
		public void SLerpBetweenNearlyEqualRampOrientations()
		{
			// Mobile's tilt between a full-tile slope (cell ramp 16) and the half-cell ramp tilted about the
			// same axis (cell ramp 12): the boundary orientation is their midpoint, a single angle step from
			// the slope, and the actor interpolates from there back to the slope over the adjustment margin.
			var east = new WVec(1024, 0, 0);
			var slope = new WRot(east, new WAngle(-64));
			var ramp = new WRot(east, new WAngle(-48));
			var boundary = WRot.SLerp(slope, ramp, 1, 2);

			Assert.That(boundary.Roll.Angle, Is.EqualTo(968));
			Assert.That(slope.Roll.Angle, Is.EqualTo(960));

			const int Margin = 256;
			for (var mul = 0; mul <= Margin; mul++)
			{
				var r = WRot.SLerp(boundary, slope, mul, Margin);
				Assert.That(r.Roll.Angle, Is.InRange(960, 968), $"SLerp(boundary, slope, {mul}, {Margin})");
				Assert.That(r.Pitch.Angle, Is.Zero);
				Assert.That(r.Yaw.Angle, Is.Zero);

				r = WRot.SLerp(slope, boundary, mul, Margin);
				Assert.That(r.Roll.Angle, Is.InRange(960, 968), $"SLerp(slope, boundary, {mul}, {Margin})");
			}

			Assert.That(WRot.SLerp(boundary, slope, 0, Margin).Roll.Angle, Is.EqualTo(968));
			Assert.That(WRot.SLerp(boundary, slope, Margin, Margin).Roll.Angle, Is.EqualTo(960));
		}

		[Test]
		public void SLerpNeverThrowsBetweenRampOrientations()
		{
			// Every orientation Mobile can interpolate between: the ramps and the midpoints of ramp pairs.
			var orientations = new List<WRot>(RampOrientations());
			var ramps = orientations.ToArray();
			foreach (var a in ramps)
				foreach (var b in ramps)
					orientations.Add(WRot.SLerp(a, b, 1, 2));

			foreach (var a in orientations)
				foreach (var b in ramps)
					foreach (var div in new[] { 2, 7, 256 })
						for (var mul = 0; mul <= div; mul++)
							Assert.DoesNotThrow(() => WRot.SLerp(a, b, mul, div), $"SLerp({a}, {b}, {mul}, {div})");
		}
	}
}
