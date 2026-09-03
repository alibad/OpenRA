using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class RA2IntegrationTest
	{
		[TestCase(MapGridType.Rectangular)]
		[TestCase(MapGridType.RectangularIsometric)]
		public void BridgeMapCoordinatesRoundTrip(MapGridType grid)
		{
			for (var y = 0; y < 80; y++)
				for (var x = 0; x < 80; x++)
				{
					var mapCell = new MPos(x, y);
					Assert.That(mapCell.ToCPos(grid).ToMPos(grid), Is.EqualTo(mapCell));
				}
		}

		[Test]
		public void IsometricOrdersAndObservationsUseMapCoordinates()
		{
			var root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, ".."));
			var action = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.Common/Traits/Player/ActionHandler.cs"));
			Assert.That(action, Does.Not.Contain("new CPos(cmd.TargetX, cmd.TargetY)"));
			Assert.That(action, Does.Contain("new MPos(cmd.TargetX, cmd.TargetY).ToCPos(world.Map)"));
			var observation = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.Common/Traits/Player/ObservationSerializer.cs"));
			Assert.That(observation, Does.Contain("unit.CellX = cell.U"));
			Assert.That(observation, Does.Contain("bldg.CellY = bldgCell.V"));
			Assert.That(observation, Does.Contain("obs.ActorNames[type]"));
		}

		[Test]
		public void GameSelectionIsAboveTheInputBlockingMenuBackground()
		{
			var root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, ".."));
			var menu = File.ReadAllText(Path.Combine(root, "mods/common/chrome/mainmenu.yaml"));
			Assert.That(menu.IndexOf("Container@GAME_SELECTION", System.StringComparison.Ordinal),
				Is.GreaterThan(menu.IndexOf("Background@BORDER", System.StringComparison.Ordinal)));
		}

		[Test]
		public void CompanionShortcutAndVoiceLabelsAreShared()
		{
			var root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, ".."));
			var logic = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.Common/Widgets/Logic/Ingame/CompanionStatusLogic.cs"));
			Assert.That(logic, Does.Contain("Hold {Binding(\"AIAsk\")} to ask AI"));
			var common = File.ReadAllText(Path.Combine(root, "mods/common/fluent/chrome.ftl"));
			Assert.That(common, Does.Contain("button-ai-companion-voice-on = VOICE: ON"));
			var ra = File.ReadAllText(Path.Combine(root, "mods/ra/fluent/chrome.ftl"));
			Assert.That(ra, Does.Not.Contain("button-ai-companion-voice-on ="));
		}
	}
}
