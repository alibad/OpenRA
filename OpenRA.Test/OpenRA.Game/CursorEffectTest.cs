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
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Graphics;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class CursorEffectTest
	{
		static readonly MethodInfo RenderMethod = typeof(CursorEffect).Assembly
			.GetType("OpenRA.Graphics.CursorEffectRenderer", true)
			.GetMethod("Render", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

		[TestCaseSource(nameof(EffectStyles))]
		public void EffectStyleProducesVisibleAnimation(CursorEffectStyle style)
		{
			var effect = CreateEffect(style);
			var first = Render(effect, 0);
			var second = Render(effect, 1);

			Assert.Multiple(() =>
			{
				Assert.That(first, Has.Length.EqualTo(4 * effect.Size * effect.Size));
				Assert.That(AlphaPixels(first), Is.GreaterThan(0));
				Assert.That(second, Is.Not.EqualTo(first));
			});
		}

		[Test]
		public void EffectStylesHaveDistinctSignatures()
		{
			var signatures = EffectStyles.Select(style => Convert.ToBase64String(Render(CreateEffect(style), 2))).ToArray();
			Assert.That(signatures.Distinct().Count(), Is.EqualTo(signatures.Length));
		}

		static IEnumerable<CursorEffectStyle> EffectStyles => Enum.GetValues<CursorEffectStyle>();

		static CursorEffect CreateEffect(CursorEffectStyle style)
		{
			var yaml = MiniYaml.FromString($"Effect:\n\tStyle: {style}\n\tPrimaryColor: 2675FF\n\tSecondaryColor: FFC928",
				"cursor-effect-test").Single();
			return new CursorEffect(yaml.Value);
		}

		static byte[] Render(CursorEffect effect, int frame)
		{
			return (byte[])RenderMethod.Invoke(null, [effect, frame]);
		}

		static int AlphaPixels(byte[] data)
		{
			var count = 0;
			for (var i = 3; i < data.Length; i += 4)
				if (data[i] != 0)
					count++;

			return count;
		}
	}
}
