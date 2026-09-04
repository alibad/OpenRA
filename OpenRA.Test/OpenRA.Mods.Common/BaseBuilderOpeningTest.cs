using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class BaseBuilderOpeningTest
	{
		[Test]
		public void EmptyOpeningPreservesExistingBehavior()
		{
			Assert.That(BaseBuilderBotModule.NextInitialBuilding([], ["gapowr"], []), Is.Null);
		}

		[Test]
		public void OpeningUsesOrderAndSkipsOtherFactionsAndUnavailablePrerequisites()
		{
			string[] order = ["gapowr", "napowr", "garefn", "narefn", "gapile", "nahand", "gaweap", "naweap", "gaairc", "naradr"];
			Assert.That(BaseBuilderBotModule.NextInitialBuilding(order, ["gapowr", "gapile", "garefn"], ["gapowr"]), Is.EqualTo("garefn"));
			Assert.That(BaseBuilderBotModule.NextInitialBuilding(order, ["napowr", "naweap", "naradr"], ["napowr", "narefn", "nahand"]), Is.EqualTo("naweap"));
		}

		[Test]
		public void CompletedOpeningDoesNotBuildDuplicates()
		{
			Assert.That(BaseBuilderBotModule.NextInitialBuilding(["garefn", "gaweap"], ["garefn", "gaweap"], ["garefn", "gaweap"]), Is.Null);
		}
	}
}
