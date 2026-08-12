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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class EarthAIEditorLogic : ChromeLogic
	{
		static readonly Dictionary<int, string> MapSizeLabels = new()
		{
			{ 64, "64 x 64  -  Fast" },
			{ 96, "96 x 96  -  Standard" },
			{ 128, "128 x 128  -  Large" }
		};

		static readonly Dictionary<string, string> ArchetypeDirections = new()
		{
			{ "Balanced Skirmish", "Control the center, protect both supply lines, and keep the flanks tactically distinct." },
			{ "River Crossing", "Secure the crossings while limited routes create ambushes, bridge pressure, and contested high ground." },
			{ "Urban Siege", "Fight through dense approaches toward a fortified center with alternate routes for armor and infantry." },
			{ "Supply Raid", "Disrupt the enemy economy while defending exposed resource fields and a vulnerable reinforcement route." }
		};

		static readonly Dictionary<string, string> ArchetypeIds = new()
		{
			{ "Balanced Skirmish", "balanced-skirmish" },
			{ "River Crossing", "river-crossing" },
			{ "Urban Siege", "urban-siege" },
			{ "Supply Raid", "supply-raid" }
		};

		static readonly Dictionary<string, string> GenerationModeLabels = new()
		{
			{ "reality-first", "Reality First  -  Closest Match" },
			{ "playability-first", "Balanced  -  Earth + Playability" },
			{ "creative-remix", "Creative Remix  -  Earth Inspired" }
		};

		public static bool OpenOnNextEditor { get; set; }

		public static bool ConsumeOpenRequest()
		{
			var open = OpenOnNextEditor;
			OpenOnNextEditor = false;
			return open;
		}

		readonly ModData modData;
		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly TextFieldWidget location;
		readonly TextFieldWidget latitude;
		readonly TextFieldWidget longitude;
		readonly TextFieldWidget title;
		readonly TextFieldWidget direction;
		readonly TextFieldWidget radius;
		readonly TextFieldWidget seed;
		readonly LabelWidget sourceLabel;
		readonly LabelWidget mapHealthLabel;
		readonly LabelWidget draftLabel;
		readonly LabelWidget statusLabel;
		readonly EarthMapPreviewWidget earthPreview;
		readonly EditorActionManager actionManager;

		bool busy;
		bool earthLinked;
		int mapSize;
		string archetype = "Balanced Skirmish";
		string generationMode = "reality-first";
		string realitySummary = "";
		string source = "STANDARD MAP  •  ADD AN EARTH SOURCE";
		string mapHealth = "Analyzing map...";
		string draft = "Ask AI for a playable objective and tactical twist.";
		string status = "Ready to shape this battlefield.";

		[ObjectCreator.UseCtor]
		public EarthAIEditorLogic(Widget widget, World world, WorldRenderer worldRenderer, ModData modData)
		{
			this.modData = modData;
			this.world = world;
			this.worldRenderer = worldRenderer;
			actionManager = world.WorldActor.Trait<EditorActionManager>();

			location = widget.Get<TextFieldWidget>("EARTH_LOCATION");
			latitude = widget.Get<TextFieldWidget>("EARTH_LATITUDE");
			longitude = widget.Get<TextFieldWidget>("EARTH_LONGITUDE");
			title = widget.Get<TextFieldWidget>("MISSION_TITLE");
			direction = widget.Get<TextFieldWidget>("MISSION_DIRECTION");
			radius = widget.Get<TextFieldWidget>("EARTH_RADIUS");
			seed = widget.Get<TextFieldWidget>("TERRAIN_SEED");
			sourceLabel = widget.Get<LabelWidget>("EARTH_SOURCE");
			mapHealthLabel = widget.Get<LabelWidget>("MAP_HEALTH");
			draftLabel = widget.Get<LabelWidget>("AI_DRAFT");
			statusLabel = widget.Get<LabelWidget>("WORKBENCH_STATUS");
			earthPreview = widget.Get<EarthMapPreviewWidget>("EARTH_PREVIEW");
			earthPreview.OnMapClick = MoveEarthPin;
			var currentMapLabel = widget.Get<LabelWidget>("CURRENT_MAP");
			currentMapLabel.GetText = () => WidgetUtils.TruncateText($"CURRENT MAP  •  {world.Map.Title}",
				currentMapLabel.Bounds.Width, Game.Renderer.Fonts[currentMapLabel.Font]);

			title.Text = world.Map.Title;
			radius.Text = "3500";
			seed.Text = "1";
			mapSize = MapSizeLabels.Keys.OrderBy(value => Math.Abs(value - world.Map.MapSize.Width)).First();
			LoadEarthMetadata();
			AnalyzeMap();

			sourceLabel.GetText = () => source;
			mapHealthLabel.GetText = () => mapHealth;
			draftLabel.GetText = () => draft;
			statusLabel.GetText = () => status;

			var archetypeDropdown = widget.Get<DropDownButtonWidget>("MISSION_ARCHETYPE");
			archetypeDropdown.GetText = () => archetype;
			archetypeDropdown.OnMouseDown = _ => ShowArchetypeDropdown(archetypeDropdown);

			var sizeDropdown = widget.Get<DropDownButtonWidget>("EARTH_MAP_SIZE");
			sizeDropdown.GetText = () => MapSizeLabels[mapSize];
			sizeDropdown.OnMouseDown = _ => ShowMapSizeDropdown(sizeDropdown);

			var modeDropdown = widget.Get<DropDownButtonWidget>("GENERATION_MODE");
			modeDropdown.GetText = () => GenerationModeLabels[generationMode];
			modeDropdown.OnMouseDown = _ => ShowGenerationModeDropdown(modeDropdown);

			var find = widget.Get<ButtonWidget>("FIND_EARTH_LOCATION");
			find.IsDisabled = () => busy || string.IsNullOrWhiteSpace(location.Text);
			find.OnClick = () => _ = SearchAsync();

			var aiDraft = widget.Get<ButtonWidget>("DRAFT_MISSION");
			aiDraft.IsDisabled = () => busy;
			aiDraft.OnClick = () => _ = DraftMissionAsync();

			var build = widget.Get<ButtonWidget>("BUILD_EARTH_MAP");
			build.IsDisabled = () => busy || actionManager.HasUnsavedItems() || !HasValidCoordinates();
			build.OnClick = () => _ = GenerateAsync(false);

			var remix = widget.Get<ButtonWidget>("REMIX_EARTH_MAP");
			remix.IsDisabled = () => busy || actionManager.HasUnsavedItems() || !HasValidCoordinates();
			remix.OnClick = () => _ = GenerateAsync(true);

			widget.Get<ButtonWidget>("REFRESH_MAP_HEALTH").OnClick = AnalyzeMap;
			widget.Get<ButtonWidget>("AI_SETTINGS").OnClick = OpenAISettings;
			widget.Get<ButtonWidget>("PLAYTEST_MAP").OnClick = () =>
				widget.Parent.GetOrNull<MenuButtonWidget>("OPTIONS_BUTTON")?.OnClick();

			if (HasValidCoordinates())
				_ = RefreshEarthPreviewAsync();
		}

		void LoadEarthMetadata()
		{
			if (world.Map.Package == null || !world.Map.Package.Contains("openra-ai-manifest.json"))
			{
				location.Text = world.Map.Title;
				return;
			}

			try
			{
				using var stream = world.Map.Package.GetStream("openra-ai-manifest.json");
				using var document = JsonDocument.Parse(stream);
				var selection = document.RootElement.GetProperty("selection");
				location.Text = selection.TryGetProperty("location_name", out var locationName)
					? locationName.GetString() ?? world.Map.Title
					: world.Map.Title;
				latitude.Text = selection.GetProperty("latitude").GetDouble().ToString("0.######", CultureInfo.InvariantCulture);
				longitude.Text = selection.GetProperty("longitude").GetDouble().ToString("0.######", CultureInfo.InvariantCulture);
				radius.Text = selection.GetProperty("radius_m").GetInt32().ToString(CultureInfo.InvariantCulture);
				seed.Text = selection.GetProperty("seed").GetInt32().ToString(CultureInfo.InvariantCulture);
				mapSize = selection.GetProperty("map_size").GetInt32();
				if (selection.TryGetProperty("story_seed", out var storySeed))
					direction.Text = storySeed.GetString() ?? "";
				if (selection.TryGetProperty("generation_mode", out var mode) &&
					GenerationModeLabels.ContainsKey(mode.GetString() ?? ""))
					generationMode = mode.GetString() ?? "reality-first";

				if (document.RootElement.TryGetProperty("analysis", out var analysis))
				{
					var confidence = analysis.TryGetProperty("confidence", out var value) ? value.GetDouble() : 0;
					var visionUsed = analysis.TryGetProperty("vision_used", out var used) && used.GetBoolean();
					var biome = analysis.TryGetProperty("biome", out var biomeValue) ? biomeValue.GetString() : "terrain";
					var summary = analysis.TryGetProperty("summary", out var summaryValue) ? summaryValue.GetString() : "Earth terrain translated.";
					realitySummary = $"EARTH MATCH {confidence:P0}  •  {biome?.ToUpperInvariant()}  •  {(visionUsed ? "TERRAIN VISION + OSM" : "OSM FALLBACK")}\n{summary}";
				}

				earthLinked = true;
				source = "EARTH LINKED  •  TERRAIN VIEW + OSM  •  READY";
			}
			catch (Exception e)
			{
				source = "EARTH METADATA NEEDS REFRESH";
				SetStatus($"Could not read Earth metadata: {e.Message}");
			}
		}

		void AnalyzeMap()
		{
			var actorLayer = world.WorldActor.Trait<EditorActorLayer>();
			var actors = actorLayer.Save();
			var spawns = actors.Count(actor => string.Equals(actor.Value.Value, "mpspawn", StringComparison.OrdinalIgnoreCase));
			var mines = actors.Count(actor => string.Equals(actor.Value.Value, "mine", StringComparison.OrdinalIgnoreCase));
			var players = actorLayer.Players.Players.Values.Count(player => player.Playable);
			var resourceCells = world.Map.Resources.Count(resource => resource.Type != 0);
			var healthy = spawns >= 2 && players >= 2 && (mines > 0 || resourceCells > 0);

			var health = $"{(healthy ? "READY" : "NEEDS ATTENTION")}  •  {spawns} spawns  •  {players} players  •  {mines} mines  •  {resourceCells} resource cells";
			if (!string.IsNullOrWhiteSpace(realitySummary))
				health += "\n" + realitySummary;
			mapHealth = WidgetUtils.WrapText(
				health,
				mapHealthLabel.Bounds.Width,
				Game.Renderer.Fonts[mapHealthLabel.Font]);
			SetStatus(actionManager.HasUnsavedItems()
				? "Map analyzed. Save current edits before rebuilding the Earth terrain."
				: "Map analyzed. AI can now draft against the current battlefield.");
		}

		async System.Threading.Tasks.Task SearchAsync()
		{
			var query = location.Text.Trim();
			SetBusy("Finding that place and linking it to this map...");
			try
			{
				var baseUri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_WORLD_STUDIO_URL", "http://127.0.0.1:8788/");
				using var document = await OpenRAAILocalClient.GetAsync(baseUri, "v1/geocode?query=" + Uri.EscapeDataString(query), 20);
				var result = document.RootElement.Clone();
				Game.RunAfterTick(() =>
				{
					location.Text = result.GetProperty("name").GetString() ?? query;
					latitude.Text = result.GetProperty("latitude").GetDouble().ToString("0.######", CultureInfo.InvariantCulture);
					longitude.Text = result.GetProperty("longitude").GetDouble().ToString("0.######", CultureInfo.InvariantCulture);
					if (string.IsNullOrWhiteSpace(title.Text) || title.Text == world.Map.Title)
						title.Text = location.Text.Split(',')[0].Trim() + " Crossing";
					earthLinked = true;
					source = "EARTH LINKED  |  SATELLITE VIEW WILL BE SENT TO AI";
					SetIdle("Location linked. Draft a mission or build the battlefield now.");
					_ = RefreshEarthPreviewAsync();
				});
			}
			catch (Exception e)
			{
				Game.RunAfterTick(() => SetIdle($"Earth search failed: {e.Message}"));
			}
		}

		async System.Threading.Tasks.Task RefreshEarthPreviewAsync()
		{
			if (!double.TryParse(latitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
				!double.TryParse(longitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
				!int.TryParse(radius.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var radiusMeters))
				return;

			try
			{
				var baseUri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_WORLD_STUDIO_URL", "http://127.0.0.1:8788/");
				var path = "v1/terrain-view?latitude=" + lat.ToString(CultureInfo.InvariantCulture) +
					"&longitude=" + lon.ToString(CultureInfo.InvariantCulture) + "&radius_m=" + radiusMeters +
					"&style=satellite";
				var bytes = await OpenRAAILocalClient.GetBytesAsync(baseUri, path, 45);
				await using var stream = new MemoryStream(bytes);
				var preview = new Png(stream);
				Game.RunAfterTick(() =>
				{
					earthPreview.Update(preview, new float2(0.5f, 0.5f));
					source = "SATELLITE VIEW READY  |  THIS EXACT IMAGE GOES TO AI";
					if (!busy)
						SetStatus("Satellite image captured. Pick a fidelity mode, then generate the OpenRA translation.");
				});
			}
			catch (Exception e)
			{
				Game.RunAfterTick(() => SetStatus($"Earth map preview unavailable; generation still works. {e.Message}"));
			}
		}

		void MoveEarthPin(float2 point)
		{
			if (!double.TryParse(latitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
				!double.TryParse(longitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
				!int.TryParse(radius.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var radiusMeters))
				return;

			var northMeters = (0.5 - point.Y) * radiusMeters * 2;
			var eastMeters = (point.X - 0.5) * radiusMeters * 2;
			var selectedLatitude = lat + northMeters / 111320.0;
			var selectedLongitude = lon + eastMeters / (111320.0 * Math.Max(0.15, Math.Cos(lat * Math.PI / 180.0)));
			latitude.Text = selectedLatitude.ToString("0.######", CultureInfo.InvariantCulture);
			longitude.Text = selectedLongitude.ToString("0.######", CultureInfo.InvariantCulture);
			earthLinked = true;
			source = "EARTH PIN MOVED  •  CLICK AGAIN TO REFINE";
			SetStatus("Earth pin updated. Build, remix, or ask AI to adapt the mission.");
			_ = RefreshEarthPreviewAsync();
		}

		async System.Threading.Tasks.Task DraftMissionAsync()
		{
			YieldTextFocus();
			SetBusy("AI is reading the map and drafting a playable mission...");
			try
			{
				var actorLayer = world.WorldActor.Trait<EditorActorLayer>();
				var actors = actorLayer.Save();
				var payload = new
				{
					location = string.IsNullOrWhiteSpace(location.Text) ? "Selected Earth location" : location.Text.Trim(),
					archetype,
					player_direction = direction.Text.Trim(),
					map = new
					{
						title = world.Map.Title,
						size = $"{world.Map.MapSize.Width}x{world.Map.MapSize.Height}",
						spawns = actors.Count(actor => string.Equals(actor.Value.Value, "mpspawn", StringComparison.OrdinalIgnoreCase)),
						mines = actors.Count(actor => string.Equals(actor.Value.Value, "mine", StringComparison.OrdinalIgnoreCase)),
						resource_cells = world.Map.Resources.Count(resource => resource.Type != 0)
					}
				};

				var baseUri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_CONSOLE_URL", "http://127.0.0.1:8787/");
				using var document = await OpenRAAILocalClient.PostAsync(baseUri, "v1/design/mission", payload, 45);
				var result = document.RootElement.Clone();
				Game.RunAfterTick(() =>
				{
					var text = result.GetProperty("text").GetString()?.Trim();
					if (string.IsNullOrWhiteSpace(text))
						text = ArchetypeDirections[archetype];
					direction.Text = text[..Math.Min(240, text.Length)];
					draft = WidgetUtils.WrapText(text, draftLabel.Bounds.Width, Game.Renderer.Fonts[draftLabel.Font]);
					SetIdle("AI draft applied to the mission direction. Refine it or build the map.");
				});
			}
			catch (Exception e)
			{
				Game.RunAfterTick(() =>
				{
					var fallback = ArchetypeDirections[archetype];
					direction.Text = fallback;
					draft = WidgetUtils.WrapText(fallback, draftLabel.Bounds.Width, Game.Renderer.Fonts[draftLabel.Font]);
					SetIdle($"AI layer unavailable; a local {archetype} draft is ready. {e.Message}");
				});
			}
		}

		async System.Threading.Tasks.Task GenerateAsync(bool remix)
		{
			YieldTextFocus();
			if (actionManager.HasUnsavedItems())
			{
				SetStatus("Save current edits before rebuilding; generation opens a new editor map.");
				return;
			}

			if (!TryReadGenerationValues(out var lat, out var lon, out var radiusMeters, out var seedValue))
			{
				SetStatus("Check coordinates, radius (500-20000 m), and terrain seed.");
				return;
			}

			if (remix)
			{
				seedValue++;
				seed.Text = seedValue.ToString(CultureInfo.InvariantCulture);
			}

			if (string.IsNullOrWhiteSpace(direction.Text))
				direction.Text = ArchetypeDirections[archetype];

			SetBusy(remix ? "Step 1/4  Capturing Earth terrain for a creative remix..." : "Step 1/4  Capturing the real terrain view and map geometry...");
			try
			{
				var baseUri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_WORLD_STUDIO_URL", "http://127.0.0.1:8788/");
				var payload = new
				{
					latitude = lat,
					longitude = lon,
					title = string.IsNullOrWhiteSpace(title.Text) ? "Earth Skirmish" : title.Text.Trim(),
					location_name = string.IsNullOrWhiteSpace(location.Text) ? "Selected Earth location" : location.Text.Trim(),
					radius_m = radiusMeters,
					map_size = mapSize,
					seed = seedValue,
					story_seed = direction.Text.Trim(),
					generation_mode = remix ? "creative-remix" : generationMode,
					mission_archetype = ArchetypeIds[archetype],
					imagery_style = "satellite",
					source = "openstreetmap"
				};

				using var document = await OpenRAAILocalClient.PostAsync(baseUri, "v1/missions/generate", payload, 180);
				var result = document.RootElement.Clone();
				Game.RunAfterTick(() =>
				{
					modData.MapCache.UpdateMaps();
					var uid = modData.MapCache.PickLastModifiedMap(MapVisibility.Lobby);
					if (uid == null)
					{
						SetIdle("The map was generated, but OpenRA has not indexed it yet. Try Build again.");
						return;
					}

					var filename = result.GetProperty("filename").GetString() ?? "generated Earth map";
					SetStatus($"Step 4/4  Opening validated map {filename}...");
					DiscordService.UpdateStatus(DiscordState.InMapEditor);
					OpenOnNextEditor = true;
					Game.LoadEditor(uid);
				});
			}
			catch (Exception e)
			{
				Game.RunAfterTick(() => SetIdle($"Earth build failed: {e.Message}"));
			}
		}

		bool HasValidCoordinates()
		{
			return earthLinked &&
				double.TryParse(latitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) && lat >= -90 && lat <= 90 &&
				double.TryParse(longitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) && lon >= -180 && lon <= 180;
		}

		bool TryReadGenerationValues(out double lat, out double lon, out int radiusMeters, out int seedValue)
		{
			lat = 0;
			lon = 0;
			radiusMeters = 0;
			seedValue = 0;
			return double.TryParse(latitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out lat) && lat >= -90 && lat <= 90 &&
				double.TryParse(longitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out lon) && lon >= -180 && lon <= 180 &&
				int.TryParse(radius.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out radiusMeters) && radiusMeters >= 500 && radiusMeters <= 20000 &&
				int.TryParse(seed.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out seedValue) && seedValue >= 0;
		}

		void ShowArchetypeDropdown(DropDownButtonWidget dropdown)
		{
			ScrollItemWidget SetupItem(string value, ScrollItemWidget template)
			{
				var item = ScrollItemWidget.Setup(template, () => archetype == value, () => archetype = value);
				item.Get<LabelWidget>("LABEL").GetText = () => value;
				return item;
			}

			dropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 255, ArchetypeDirections.Keys, SetupItem);
		}

		void ShowMapSizeDropdown(DropDownButtonWidget dropdown)
		{
			ScrollItemWidget SetupItem(int value, ScrollItemWidget template)
			{
				var item = ScrollItemWidget.Setup(template, () => mapSize == value, () => mapSize = value);
				item.Get<LabelWidget>("LABEL").GetText = () => MapSizeLabels[value];
				return item;
			}

			dropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 255, MapSizeLabels.Keys, SetupItem);
		}

		void ShowGenerationModeDropdown(DropDownButtonWidget dropdown)
		{
			ScrollItemWidget SetupItem(string value, ScrollItemWidget template)
			{
				var item = ScrollItemWidget.Setup(template, () => generationMode == value, () => generationMode = value);
				item.Get<LabelWidget>("LABEL").GetText = () => GenerationModeLabels[value];
				return item;
			}

			dropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 300, GenerationModeLabels.Keys, SetupItem);
		}

		void OpenAISettings()
		{
			Ui.OpenWindow("SETTINGS_PANEL", new WidgetArgs
			{
				{ "world", world },
				{ "worldRenderer", worldRenderer },
				{ "initialPanel", "AI_PANEL" },
				{ "onExit", () => { } }
			});
		}

		void SetBusy(string message)
		{
			busy = true;
			SetStatus(message);
		}

		void SetIdle(string message)
		{
			busy = false;
			SetStatus(message);
		}

		void SetStatus(string message)
		{
			status = WidgetUtils.WrapText(message, statusLabel.Bounds.Width, Game.Renderer.Fonts[statusLabel.Font]);
		}

		void YieldTextFocus()
		{
			location.YieldKeyboardFocus();
			latitude.YieldKeyboardFocus();
			longitude.YieldKeyboardFocus();
			title.YieldKeyboardFocus();
			direction.YieldKeyboardFocus();
			radius.YieldKeyboardFocus();
			seed.YieldKeyboardFocus();
		}
	}
}
