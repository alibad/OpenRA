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

using NUnit.Framework;
using OpenRA.Graphics;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class UnicodeTextTest
	{
		[Test]
		public void ArabicIsContextuallyShapedAndOrderedRightToLeft()
		{
			Assert.That(UnicodeText.PrepareForDisplay("Saudi / \u0633\u0644\u0627\u0645"),
				Is.EqualTo("Saudi / \uFEE1\uFE8E\uFEE0\uFEB3"));
		}

		[Test]
		public void ArabicPreparationIsIdempotentAndPreservesNumberOrder()
		{
			var prepared = UnicodeText.PrepareForDisplay("\u0639\u0627\u0645 2026");
			Assert.That(prepared, Does.Contain("2026"));
			Assert.That(UnicodeText.PrepareForDisplay(prepared), Is.EqualTo(prepared));
		}
	}
}
