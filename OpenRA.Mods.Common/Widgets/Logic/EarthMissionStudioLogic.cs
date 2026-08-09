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
using System.Text.Json;
using OpenRA.FileFormats;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class EarthMissionStudioLogic : ChromeLogic
	{
		static readonly Dictionary<int, string> MapSizeLabels = new()
		{
			{ 64, "64 x 64  -  Fast" },
			{ 96, "96 x 96  -  Standard" },
			{ 128, "128 x 128  -  Large" }
		};

		static readonly Dictionary<int, string> RadiusLabels = new()
		{
			{ 500, "1 km wide  -  Tactical" },
			{ 750, "1.5 km wide  -  Neighborhood" },
			{ 1000, "2 km wide  -  Local" },
			{ 2000, "4 km wide  -  District (compressed)" }
		};

		static readonly int[] EarthViewRadii = [500, 750, 1000, 2000, 4000, 8000];

		static readonly Dictionary<string, string> ArchetypeLabels = new()
		{
			{ "Balanced Skirmish", "Balanced Skirmish" },
			{ "River Crossing", "River Crossing" },
			{ "Urban Siege", "Urban Siege" },
			{ "Supply Raid", "Supply Raid" }
		};

		static readonly Dictionary<string, string> ArchetypeDirections = new()
		{
			{ "Balanced Skirmish", "Control the center, protect both supply lines, and keep the flanks tactically distinct." },
			{ "River Crossing", "Secure the crossings while limited routes create ambushes, bridge pressure, and contested high ground." },
			{ "Urban Siege", "Fight through dense approaches toward a fortified center with alternate routes for armor and infantry." },
			{ "Supply Raid", "Disrupt the enemy economy while defending exposed resource fields and a vulnerable reinforcement route." }
		};

		static readonly Dictionary<string, string> ImageryStyleLabels = new()
		{
			{ "auto", "Auto detail" },
			{ "hybrid", "Hybrid detail" },
			{ "satellite", "Satellite (regional)" },
			{ "terrain", "Map + buildings" }
		};

		static readonly string[] PipelineLabels =
		[
			"1  Earth geometry",
			"2  Earth imagery",
			"3  AI terrain vision",
			"4  Gameplay translation",
			"5  Map validation",
			"6  Ready to play"
		];

		static readonly string[] WorkflowLabels =
		[
			"1  PIN EARTH",
			"2  SHAPE MISSION",
			"3  BUILD + VALIDATE",
			"4  PLAY OR EDIT"
		];

		readonly ModData modData;
		readonly Action onExit;
		readonly TextFieldWidget location;
		readonly TextFieldWidget latitude;
		readonly TextFieldWidget longitude;
		readonly TextFieldWidget title;
		readonly TextFieldWidget seed;
		readonly TextFieldWidget story;
		readonly LabelWidget statusLabel;
		readonly LabelWidget coordinateStatusLabel;
		readonly LabelWidget previewBadgeLabel;
		readonly LabelWidget earthAttributionLabel;
		readonly LabelWidget visionStatusLabel;
		readonly EarthMapPreviewWidget earthPreview;
		readonly GeneratedMapPreviewWidget generatedPreview;
		readonly ProgressBarWidget generationBar;
		readonly LabelWidget[] pipelineLabels;
		readonly LabelWidget[] workflowLabels;
		readonly Widget advancedOptions;

		bool busy;
		bool advancedVisible;
		bool earthPreviewLoaded;
		bool generationFailed;
		int mapSize = 96;
		int radiusMeters = 500;
		int earthViewRadiusMeters = 500;
		int earthPreviewRequestId;
		int generationStage;
		string archetype = "Balanced Skirmish";
		string generationMode = "playability-first";
		string imageryStyle = "auto";
		string generatedImageryStyle = "terrain";
		string status = "Select a location, inspect its terrain, then create the battlefield.";
		string previewBadge = "PLAYABLE PREVIEW  |  WAITING";
		string generatedMapUid;
		string visionStatus = "AUTO DETAIL + MAP DATA | PREPARING SOURCE";
		string reliefIntel = "Ready";
		string waterIntel = "Ready";
		string urbanIntel = "Ready";
		string vegetationIntel = "Ready";
		string landmarkIntel = "Ready";
		string reliefDetail = "Shapes high ground";
		string waterDetail = "Routes crossings";
		string urbanDetail = "Defines choke points";
		string vegetationDetail = "Controls sightlines";
		string landmarkDetail = "Seeds objectives";
		int reliefPercent;
		int waterPercent;
		int urbanPercent;
		int vegetationPercent;
		int landmarkPercent;

		[ObjectCreator.UseCtor]
		public EarthMissionStudioLogic(Widget widget, ModData modData, Action onExit, Action<string> onPlay, Action<string> onEdit)
		{
			// WindowScale defines the usable design canvas, but widget positions still
			// use the complete renderer coordinate space. The previous implementation
			// centered against the smaller design canvas, which pinned the correctly
			// scaled panel to the upper-left on high-DPI displays.
			var renderWidth = Game.Renderer.Resolution.Width;
			var renderHeight = Game.Renderer.Resolution.Height;
			var targetWidth = (int)(renderWidth / Game.Renderer.WindowScale * 0.94f);
			var targetHeight = (int)(renderHeight / Game.Renderer.WindowScale * 0.92f);
			ScaleLayout(widget, targetWidth / (float)widget.Bounds.Width, targetHeight / (float)widget.Bounds.Height);
			widget.Bounds.Width = targetWidth;
			widget.Bounds.Height = targetHeight;
			widget.Bounds.X = (renderWidth - targetWidth) / 2;
			widget.Bounds.Y = (renderHeight - targetHeight) / 2;

			this.modData = modData;
			this.onExit = onExit;
			location = widget.Get<TextFieldWidget>("LOCATION");
			latitude = widget.Get<TextFieldWidget>("LATITUDE");
			longitude = widget.Get<TextFieldWidget>("LONGITUDE");
			title = widget.Get<TextFieldWidget>("TITLE");
			seed = widget.Get<TextFieldWidget>("SEED");
			story = widget.Get<TextFieldWidget>("STORY");

			location.Text = "Riyadh, Saudi Arabia";
			latitude.Text = "24.638916";
			longitude.Text = "46.71601";
			title.Text = "Riyadh Crossing";
			seed.Text = "1";

			statusLabel = widget.Get<LabelWidget>("STATUS");
			statusLabel.GetText = () => status;
			coordinateStatusLabel = widget.Get<LabelWidget>("COORDINATE_STATUS");
			coordinateStatusLabel.GetText = CoordinateStatus;
			previewBadgeLabel = widget.Get<LabelWidget>("PREVIEW_BADGE");
			previewBadgeLabel.GetText = () => previewBadge;
			earthAttributionLabel = widget.Get<LabelWidget>("EARTH_ATTRIBUTION");
			earthAttributionLabel.GetText = EarthAttribution;
			visionStatusLabel = widget.Get<LabelWidget>("VISION_STATUS");
			visionStatusLabel.GetText = () => visionStatus;

			earthPreview = widget.Get<EarthMapPreviewWidget>("EARTH_PREVIEW");
			earthPreview.OnMapClick = MoveEarthPin;
			earthPreview.OnZoom = ZoomEarthView;
			var earthViewScale = widget.Get<LabelWidget>("EARTH_VIEW_SCALE");
			earthViewScale.GetText = EarthViewScaleText;
			var radiusLabel = widget.Get<LabelWidget>("RADIUS_LABEL");
			radiusLabel.GetText = () => $"Battlefield footprint  |  {FormatDistance(radiusMeters * 2)} wide  |  {MetersPerCell():0.#} m/cell";
			generatedPreview = widget.Get<GeneratedMapPreviewWidget>("GENERATED_PREVIEW");
			widget.Get("EARTH_EMPTY").IsVisible = () => !earthPreviewLoaded;
			var gameEmpty = widget.Get<LabelWidget>("GAME_EMPTY");
			gameEmpty.GetText = () => "Step 3 builds the real battlefield here.\nOpenRA owns legal terrain, routes, resources, and spawn points.";
			gameEmpty.IsVisible = () => generatedMapUid == null;

			var blueprintHeadline = widget.Get<LabelWidget>("BLUEPRINT_HEADLINE");
			blueprintHeadline.GetText = () => $"{archetype.ToUpperInvariant()}  |  {mapSize} x {mapSize}";
			var blueprintEvidence = widget.Get<LabelWidget>("BLUEPRINT_EVIDENCE_VALUE");
			blueprintEvidence.GetText = () => earthPreviewLoaded ? "SOURCE LOCKED" : "AWAITING EARTH";
			blueprintEvidence.GetColor = () => earthPreviewLoaded ? Color.FromArgb(112, 221, 126) : Color.FromArgb(150, 150, 150);
			var blueprintSafety = widget.Get<LabelWidget>("BLUEPRINT_SAFETY_VALUE");
			blueprintSafety.GetText = () => generatedMapUid != null ? "VALIDATED" : busy ? "VALIDATING" : "OPENRA GUARDED";
			blueprintSafety.GetColor = () => generatedMapUid != null ? Color.FromArgb(112, 221, 126) :
				busy ? Color.FromArgb(244, 205, 67) : Color.White;
			var blueprintStyle = widget.Get<LabelWidget>("BLUEPRINT_STYLE_VALUE");
			blueprintStyle.GetText = () => generationMode == "creative-remix" ? "CREATIVE REMIX" : "EARTH + BALANCE";
			blueprintStyle.GetColor = () => Color.FromArgb(112, 221, 126);

			BindIntelCard(widget.Get("RELIEF_CARD"), () => reliefIntel, () => reliefDetail, () => reliefPercent);
			BindIntelCard(widget.Get("WATER_CARD"), () => waterIntel, () => waterDetail, () => waterPercent);
			BindIntelCard(widget.Get("URBAN_CARD"), () => urbanIntel, () => urbanDetail, () => urbanPercent);
			BindIntelCard(widget.Get("VEGETATION_CARD"), () => vegetationIntel, () => vegetationDetail, () => vegetationPercent);
			BindIntelCard(widget.Get("LANDMARKS_CARD"), () => landmarkIntel, () => landmarkDetail, () => landmarkPercent);

			pipelineLabels = new LabelWidget[PipelineLabels.Length];
			for (var index = 0; index < PipelineLabels.Length; index++)
			{
				var stage = index + 1;
				var label = widget.Get<LabelWidget>($"STAGE_{stage}");
				label.GetText = () => PipelineLabels[stage - 1];
				label.GetColor = () => PipelineColor(stage);
				pipelineLabels[index] = label;
			}

			workflowLabels = new LabelWidget[WorkflowLabels.Length];
			for (var index = 0; index < WorkflowLabels.Length; index++)
			{
				var step = index + 1;
				var label = widget.Get<LabelWidget>($"WORKFLOW_{step}");
				label.GetText = () => WorkflowLabels[step - 1];
				label.GetColor = () => WorkflowColor(step);
				workflowLabels[index] = label;
			}

			generationBar = widget.Get<ProgressBarWidget>("GENERATION_BAR");
			generationBar.GetPercentage = () => generationStage * 100 / PipelineLabels.Length;
			generationBar.IsIndeterminate = () => busy && generationStage == 0;
			generationBar.GetBarColor = GenerationBarColor;

			BindDropdown(widget.Get<DropDownButtonWidget>("MAP_SIZE"), MapSizeLabels, () => mapSize, value => mapSize = value, 260);
			BindDropdown(widget.Get<DropDownButtonWidget>("RADIUS"), RadiusLabels, () => radiusMeters, SelectRadius, 220);
			BindDropdown(widget.Get<DropDownButtonWidget>("MISSION_ARCHETYPE"), ArchetypeLabels, () => archetype, SelectArchetype, 260);
			BindDropdown(widget.Get<DropDownButtonWidget>("EARTH_LAYER"), ImageryStyleLabels,
				() => imageryStyle, SelectImageryStyle, 190);

			var zoomCloser = widget.Get<ButtonWidget>("EARTH_ZOOM_CLOSER");
			zoomCloser.IsDisabled = () => earthViewRadiusMeters <= EarthViewRadii[0] || busy;
			zoomCloser.OnClick = () => ZoomEarthView(-1);
			var zoomWider = widget.Get<ButtonWidget>("EARTH_ZOOM_WIDER");
			zoomWider.IsDisabled = () => earthViewRadiusMeters >= EarthViewRadii[^1] || busy;
			zoomWider.OnClick = () => ZoomEarthView(1);
			var zoomFit = widget.Get<ButtonWidget>("EARTH_ZOOM_FIT");
			zoomFit.IsDisabled = () => earthViewRadiusMeters == radiusMeters || busy;
			zoomFit.OnClick = () =>
			{
				earthViewRadiusMeters = radiusMeters;
				_ = RefreshEarthPreviewAsync(false);
			};

			var balancedMode = widget.Get<ButtonWidget>("MODE_BALANCED");
			balancedMode.IsHighlighted = () => generationMode == "playability-first";
			balancedMode.OnClick = () => generationMode = "playability-first";
			var creativeMode = widget.Get<ButtonWidget>("MODE_CREATIVE");
			creativeMode.IsHighlighted = () => generationMode == "creative-remix";
			creativeMode.OnClick = () => generationMode = "creative-remix";

			var search = widget.Get<ButtonWidget>("SEARCH");
			search.IsDisabled = () => busy || string.IsNullOrWhiteSpace(location.Text);
			search.OnClick = () => _ = SearchAsync();

			var generate = widget.Get<ButtonWidget>("GENERATE");
			generate.IsDisabled = () => busy || !HasValidCoordinates();
			generate.OnClick = () => _ = GenerateAsync();

			var play = widget.Get<ButtonWidget>("PLAY");
			play.IsDisabled = () => busy || generatedMapUid == null;
			play.OnClick = () => Complete(onPlay);
			var edit = widget.Get<ButtonWidget>("EDIT");
			edit.IsDisabled = () => busy || generatedMapUid == null;
			edit.OnClick = () => Complete(onEdit);

			advancedOptions = widget.Get("ADVANCED_OPTIONS");
			advancedOptions.IsVisible = () => advancedVisible;
			widget.Get<ButtonWidget>("ADVANCED").OnClick = () => advancedVisible = !advancedVisible;
			widget.Get<ButtonWidget>("ADVANCED_CLOSE").OnClick = () => advancedVisible = false;
			widget.Get<ButtonWidget>("BACK").OnClick = Close;

			_ = RefreshEarthPreviewAsync(true);
		}

		static void BindIntelCard(Widget card, Func<string> value, Func<string> detail, Func<int> percentage)
		{
			card.Get<LabelWidget>("VALUE").GetText = value;
			card.Get<LabelWidget>("DETAIL").GetText = detail;
			card.Get<ProgressBarWidget>("BAR").GetPercentage = percentage;
		}

		void BindDropdown<T>(DropDownButtonWidget dropdown, Dictionary<T, string> options,
			Func<T> getValue, Action<T> setValue, int width)
		{
			dropdown.GetText = () => options[getValue()];
			dropdown.OnMouseDown = _ =>
			{
				ScrollItemWidget SetupItem(T value, ScrollItemWidget template)
				{
					var item = ScrollItemWidget.Setup(template, () => EqualityComparer<T>.Default.Equals(getValue(), value), () => setValue(value));
					item.Get<LabelWidget>("LABEL").GetText = () => options[value];
					return item;
				}

				dropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", width, options.Keys, SetupItem);
			};
		}

		void SelectRadius(int value)
		{
			radiusMeters = value;
			earthViewRadiusMeters = value;
			_ = RefreshEarthPreviewAsync(false);
		}

		void ZoomEarthView(int direction)
		{
			var index = Array.IndexOf(EarthViewRadii, earthViewRadiusMeters);
			if (index < 0)
				index = 0;
			var next = (index + direction).Clamp(0, EarthViewRadii.Length - 1);
			if (next == index)
				return;

			earthViewRadiusMeters = EarthViewRadii[next];
			_ = RefreshEarthPreviewAsync(false);
		}

		double MetersPerCell()
		{
			return radiusMeters * 2.0 / mapSize;
		}

		string EarthViewScaleText()
		{
			var sourceDetail = EffectiveImageryStyle(earthViewRadiusMeters) switch
			{
				"hybrid" => "satellite + buildings",
				"satellite" => "~10 m regional source",
				_ => "street + building detail"
			};
			return $"VIEW {FormatDistance(earthViewRadiusMeters * 2).ToUpperInvariant()}  |  {sourceDetail.ToUpperInvariant()}";
		}

		string EffectiveImageryStyle(int viewRadius)
		{
			if (imageryStyle != "auto")
				return imageryStyle;

			return viewRadius <= 1000 ? "terrain" : "satellite";
		}

		static string FormatDistance(int meters)
		{
			return meters < 1000 ? $"{meters} m" : $"{meters / 1000.0:0.#} km";
		}

		void SelectArchetype(string value)
		{
			archetype = value;
			if (string.IsNullOrWhiteSpace(story.Text))
				story.Text = ArchetypeDirections[value];
		}

		void SelectImageryStyle(string value)
		{
			imageryStyle = value;
			visionStatus = $"{ImageryStyleLabels[EffectiveImageryStyle(earthViewRadiusMeters)].ToUpperInvariant()} + MAP DATA | AWAITING SCAN";
			earthPreviewLoaded = false;
			SetStatus($"Switching Earth reconnaissance to {ImageryStyleLabels[value].ToLowerInvariant()}...");
			_ = RefreshEarthPreviewAsync(false);
		}

		string EarthAttribution()
		{
			return EffectiveImageryStyle(earthViewRadiusMeters) switch
			{
				"hybrid" => "Sentinel-2 + OpenTopoMap | EOX | OpenStreetMap contributors",
				"satellite" => "Sentinel-2 Cloudless 2025 | EOX | modified Copernicus data",
				_ => "OpenTopoMap | OpenStreetMap contributors | SRTM"
			};
		}

		string CoordinateStatus()
		{
			if (!double.TryParse(latitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
				!double.TryParse(longitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
				return "SELECT A POINT ON EARTH";

			return $"{Math.Abs(lat):0.000000}{(lat >= 0 ? "N" : "S")}  |  {Math.Abs(lon):0.000000}{(lon >= 0 ? "E" : "W")}";
		}

		Color PipelineColor(int stage)
		{
			if (generationFailed && stage == Math.Max(1, generationStage))
				return Color.FromArgb(255, 112, 80);
			if (generationStage == 0)
				return stage == 1 ? Color.White : Color.FromArgb(150, 150, 150);
			if (generationStage >= stage)
				return generationStage == stage && busy ? Color.FromArgb(244, 205, 67) : Color.FromArgb(112, 221, 126);
			return Color.FromArgb(150, 150, 150);
		}

		Color GenerationBarColor()
		{
			if (generationFailed)
				return Color.FromArgb(255, 112, 80);
			if (generatedMapUid != null || generationStage >= PipelineLabels.Length)
				return Color.FromArgb(112, 221, 126);
			if (busy)
				return Color.FromArgb(244, 205, 67);
			return Color.FromArgb(105, 105, 105);
		}

		Color WorkflowColor(int step)
		{
			var current = generatedMapUid != null ? 4 : busy ? 3 : earthPreviewLoaded ? 2 : 1;
			if (step < current)
				return Color.FromArgb(112, 221, 126);
			if (step == current)
				return busy ? Color.FromArgb(244, 205, 67) : Color.White;
			return Color.FromArgb(150, 150, 150);
		}

		async System.Threading.Tasks.Task SearchAsync()
		{
			var query = location.Text.Trim();
			SetBusy("Finding that place and preparing its terrain view...");
			try
			{
				var baseUri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_WORLD_STUDIO_URL", "http://127.0.0.1:8788/");
				using var document = await OpenRAAILocalClient.GetAsync(baseUri, "v1/geocode?query=" + Uri.EscapeDataString(query), 20);
				var result = document.RootElement.Clone();
				Game.RunAfterTick(() =>
				{
					var resultName = result.GetProperty("name").GetString() ?? query;
					var name = ReadableLocationName(resultName, query);
					latitude.Text = result.GetProperty("latitude").GetDouble().ToString("0.######", CultureInfo.InvariantCulture);
					longitude.Text = result.GetProperty("longitude").GetDouble().ToString("0.######", CultureInfo.InvariantCulture);
					location.Text = name;
					title.Text = name.Split(',')[0].Trim() + " Crossing";
					SetIdle("Location selected. Click the Earth view to refine the exact battlefield center.");
					_ = RefreshEarthPreviewAsync(false);
				});
			}
			catch (Exception e)
			{
				Game.RunAfterTick(() => SetIdle($"Location search failed: {e.Message}"));
			}
		}

		static string ReadableLocationName(string resultName, string query)
		{
			if (UsesLatinScript(resultName))
				return resultName;
			if (UsesLatinScript(query))
				return query;
			return "Selected Earth location";
		}

		static bool UsesLatinScript(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return false;
			foreach (var character in value)
			{
				if (!char.IsLetter(character))
					continue;
				var codepoint = (int)character;
				if (codepoint < 0x0041 || codepoint > 0x024F)
					return false;
			}

			return true;
		}

		async System.Threading.Tasks.Task RefreshEarthPreviewAsync(bool initial)
		{
			if (!TryReadCoordinates(out var lat, out var lon))
				return;

			if (!busy && !initial)
				SetStatus("Refreshing the exact terrain image used by generation and AI vision...");
			var requestId = ++earthPreviewRequestId;
			try
			{
				var baseUri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_WORLD_STUDIO_URL", "http://127.0.0.1:8788/");
				var resolvedStyle = EffectiveImageryStyle(earthViewRadiusMeters);
				var path = "v1/terrain-view?latitude=" + lat.ToString(CultureInfo.InvariantCulture) +
					"&longitude=" + lon.ToString(CultureInfo.InvariantCulture) + "&radius_m=" + earthViewRadiusMeters +
					"&style=" + Uri.EscapeDataString(resolvedStyle);
				var bytes = await OpenRAAILocalClient.GetBytesAsync(baseUri, path, 45);
				await using var stream = new MemoryStream(bytes);
				var preview = new Png(stream);
				Game.RunAfterTick(() =>
				{
					if (requestId != earthPreviewRequestId)
						return;
					earthPreview.Update(preview, new float2(0.5f, 0.5f), radiusMeters / (float)earthViewRadiusMeters);
					earthPreviewLoaded = true;
					visionStatus = $"{ImageryStyleLabels[resolvedStyle].ToUpperInvariant()} + MAP DATA | READY TO ANALYZE";
					if (!busy)
						SetStatus($"{ImageryStyleLabels[resolvedStyle]} ready. Generation will fit this source to the battlefield footprint.");
				});
			}
			catch (Exception e)
			{
				Game.RunAfterTick(() => SetStatus($"Earth view unavailable; generation can still use map geometry. {e.Message}"));
			}
		}

		void MoveEarthPin(float2 point)
		{
			if (!TryReadCoordinates(out var lat, out var lon))
				return;
			var northMeters = (0.5 - point.Y) * earthViewRadiusMeters * 2;
			var eastMeters = (point.X - 0.5) * earthViewRadiusMeters * 2;
			var selectedLatitude = lat + northMeters / 111320.0;
			var selectedLongitude = lon + eastMeters / (111320.0 * Math.Max(0.15, Math.Cos(lat * Math.PI / 180.0)));
			latitude.Text = selectedLatitude.ToString("0.######", CultureInfo.InvariantCulture);
			longitude.Text = selectedLongitude.ToString("0.######", CultureInfo.InvariantCulture);
			SetStatus("Battlefield center moved. Refreshing Earth reconnaissance...");
			_ = RefreshEarthPreviewAsync(false);
		}

		async System.Threading.Tasks.Task GenerateAsync()
		{
			YieldTextFocus();
			if (!TryReadGenerationValues(out var lat, out var lon, out var seedValue))
			{
				SetIdle("Check coordinates and terrain seed before creating the battlefield.");
				return;
			}

			// Generation always analyzes the battlefield footprint, not a wider
			// reconnaissance zoom. Fit the visible source first so the screen and
			// multimodal input describe the same physical area.
			if (earthViewRadiusMeters != radiusMeters)
			{
				earthViewRadiusMeters = radiusMeters;
				SetStatus("Fitting Earth detail to the battlefield footprint...");
				await RefreshEarthPreviewAsync(false);
			}
			generatedImageryStyle = EffectiveImageryStyle(radiusMeters);

			generationFailed = false;
			generationStage = 0;
			generatedMapUid = null;
			generatedPreview.Clear();
			previewBadge = "GENERATING  |  LIVE PIPELINE";
			ResetIntel();
			SetBusy("Starting the Earth-to-battlefield pipeline...");
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
					story_seed = string.IsNullOrWhiteSpace(story.Text) ? ArchetypeDirections[archetype] : story.Text.Trim(),
					generation_mode = generationMode,
					imagery_style = generatedImageryStyle,
					source = "openstreetmap"
				};

				using var accepted = await OpenRAAILocalClient.PostAsync(baseUri, "v1/missions/generate-async", payload, 20);
				var jobId = accepted.RootElement.GetProperty("job_id").GetString();
				if (string.IsNullOrWhiteSpace(jobId))
					throw new InvalidOperationException("World generation did not return a job identifier.");

				JsonElement result = default;
				while (true)
				{
					await System.Threading.Tasks.Task.Delay(350);
					using var jobDocument = await OpenRAAILocalClient.GetAsync(baseUri, "v1/jobs/" + jobId, 20);
					var job = jobDocument.RootElement.Clone();
					var state = job.GetProperty("state").GetString() ?? "running";
					var stage = job.GetProperty("stage").GetInt32();
					var message = job.GetProperty("message").GetString() ?? "Generating battlefield...";
					Game.RunAfterTick(() => ApplyJobProgress(stage, message, state));
					if (state == "succeeded")
					{
						result = job.GetProperty("result").Clone();
						break;
					}

					if (state == "failed")
						throw new InvalidOperationException(message);
				}

				Game.RunAfterTick(() => ApplyGeneratedResult(result));
			}
			catch (Exception e)
			{
				Game.RunAfterTick(() =>
				{
					generationFailed = true;
					previewBadge = "GENERATION FAILED  |  GAME UNAFFECTED";
					SetIdle($"Battlefield generation failed: {e.Message}");
				});
			}
		}

		void ApplyJobProgress(int stage, string message, string state)
		{
			generationStage = Math.Max(generationStage, stage);
			previewBadge = state == "succeeded" ? "PLAYABLE PREVIEW  |  READY" : $"GENERATING  |  STAGE {Math.Max(1, stage)} OF 6";
			SetStatus(message);
		}

		void ApplyGeneratedResult(JsonElement result, int indexAttempt = 0)
		{
			var filename = result.GetProperty("filename").GetString() ?? "generated Earth map";
			if (indexAttempt == 0)
				ApplySynthesis(result);

			modData.MapCache.UpdateMaps();
			generatedMapUid = FindGeneratedMap(filename);
			if (generatedMapUid == null)
			{
				if (indexAttempt < 20)
				{
					previewBadge = "PLAYABLE PREVIEW  |  INDEXING";
					SetStatus("Battlefield validated. Indexing it in OpenRA for the playable preview...");
					_ = RetryApplyGeneratedResult(result, indexAttempt + 1);
					return;
				}

				generationFailed = true;
				SetIdle("The battlefield was generated, but OpenRA could not index its installed package.");
				return;
			}

			generatedPreview.Update(modData.MapCache[generatedMapUid]);
			generationStage = 6;
			previewBadge = "PLAYABLE PREVIEW  |  VALIDATED";
			SetIdle($"Ready: {filename}. Play now or continue into the native map editor.");
		}

		async System.Threading.Tasks.Task RetryApplyGeneratedResult(JsonElement result, int indexAttempt)
		{
			await System.Threading.Tasks.Task.Delay(250);
			Game.RunAfterTick(() => ApplyGeneratedResult(result, indexAttempt));
		}

		string FindGeneratedMap(string filename)
		{
			foreach (var map in modData.MapCache)
				if (map.Status == MapStatus.Available && string.Equals(Path.GetFileName(map.Path), filename, StringComparison.OrdinalIgnoreCase))
					return map.Uid;

			return null;
		}

		void ApplySynthesis(JsonElement result)
		{
			if (!result.TryGetProperty("synthesis", out var synthesis))
				return;
			var analysis = synthesis.GetProperty("analysis");
			var biome = ReadString(analysis, "biome", "mapped");
			var relief = ReadString(analysis, "relief", "Mapped");
			var water = ReadRatio(analysis, "water_confidence");
			var urban = ReadRatio(analysis, "urban_density");
			var vegetation = ReadRatio(analysis, "vegetation_density");
			var confidence = ReadRatio(analysis, "confidence");
			var landmarks = 0;
			if (synthesis.TryGetProperty("feature_counts", out var counts))
				landmarks = ReadCount(counts, "building") + ReadCount(counts, "landmark") + ReadCount(counts, "rail");

			var visionUsed = analysis.TryGetProperty("vision_used", out var visionValue) && visionValue.GetBoolean();
			reliefIntel = TitleCase(relief);
			waterIntel = water.ToString("P0", CultureInfo.InvariantCulture);
			urbanIntel = urban.ToString("P0", CultureInfo.InvariantCulture);
			vegetationIntel = vegetation.ToString("P0", CultureInfo.InvariantCulture);
			landmarkIntel = landmarks.ToString(CultureInfo.InvariantCulture);
			reliefPercent = (int)Math.Round(confidence * 100);
			waterPercent = (int)Math.Round(water * 100);
			urbanPercent = (int)Math.Round(urban * 100);
			vegetationPercent = (int)Math.Round(vegetation * 100);
			landmarkPercent = Math.Min(100, landmarks * 8);
			reliefDetail = visionUsed ? $"Vision {confidence:P0}" : $"Map confidence {confidence:P0}";
			waterDetail = DensityLabel(water);
			urbanDetail = DensityLabel(urban);
			vegetationDetail = DensityLabel(vegetation);
			landmarkDetail = landmarks == 1 ? "Mapped feature" : "Mapped features";
			var evidence = visionUsed ? "AI VISION" : "MAP FALLBACK";
			visionStatus = $"{TitleCase(biome).ToUpperInvariant()} | {evidence} | " +
				ImageryStyleLabels[generatedImageryStyle].ToUpperInvariant();
			var tileset = synthesis.TryGetProperty("tileset", out var value) ? value.GetString() ?? "OPENRA" : "OPENRA";
			previewBadge = $"{tileset.ToUpperInvariant()}  |  EARTH MATCH {confidence:P0}";
		}

		static void ScaleLayout(Widget parent, float horizontalScale, float verticalScale)
		{
			foreach (var child in parent.Children)
			{
				child.Bounds.X = (int)Math.Round(child.Bounds.X * horizontalScale);
				child.Bounds.Y = (int)Math.Round(child.Bounds.Y * verticalScale);
				child.Bounds.Width = (int)Math.Round(child.Bounds.Width * horizontalScale);
				child.Bounds.Height = (int)Math.Round(child.Bounds.Height * verticalScale);
				ScaleLayout(child, horizontalScale, verticalScale);
			}
		}

		static string DensityLabel(double value)
		{
			if (value >= 0.66)
				return "High signal";
			if (value >= 0.33)
				return "Medium signal";
			return "Low signal";
		}

		static string ReadString(JsonElement element, string name, string fallback)
		{
			return element.TryGetProperty(name, out var value) ? value.GetString() ?? fallback : fallback;
		}

		static double ReadRatio(JsonElement element, string name)
		{
			return element.TryGetProperty(name, out var value) && value.TryGetDouble(out var number) ? number.Clamp(0, 1) : 0;
		}

		static int ReadCount(JsonElement element, string name)
		{
			return element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;
		}

		static string TitleCase(string value)
		{
			return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Replace('-', ' '));
		}

		void ResetIntel()
		{
			reliefIntel = "Scanning";
			waterIntel = "Scanning";
			urbanIntel = "Scanning";
			vegetationIntel = "Scanning";
			landmarkIntel = "Scanning";
			reliefDetail = waterDetail = urbanDetail = vegetationDetail = landmarkDetail = "Reading Earth";
			reliefPercent = waterPercent = urbanPercent = vegetationPercent = landmarkPercent = 0;
			generatedImageryStyle = EffectiveImageryStyle(radiusMeters);
			visionStatus = $"{ImageryStyleLabels[generatedImageryStyle].ToUpperInvariant()} + MAP DATA | SCANNING";
		}

		bool HasValidCoordinates()
		{
			return TryReadCoordinates(out _, out _);
		}

		bool TryReadCoordinates(out double lat, out double lon)
		{
			lat = 0;
			lon = 0;
			return double.TryParse(latitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out lat) && lat >= -90 && lat <= 90 &&
				double.TryParse(longitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out lon) && lon >= -180 && lon <= 180;
		}

		bool TryReadGenerationValues(out double lat, out double lon, out int seedValue)
		{
			seedValue = 0;
			return TryReadCoordinates(out lat, out lon) &&
				int.TryParse(seed.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out seedValue) && seedValue >= 0;
		}

		void Complete(Action<string> action)
		{
			if (generatedMapUid == null)
				return;
			var uid = generatedMapUid;
			Ui.CloseWindow();
			action(uid);
		}

		void Close()
		{
			Ui.CloseWindow();
			onExit();
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
			status = statusLabel == null ? message : WidgetUtils.TruncateText(message, statusLabel.Bounds.Width,
				Game.Renderer.Fonts[statusLabel.Font]);
		}

		void YieldTextFocus()
		{
			location.YieldKeyboardFocus();
			latitude.YieldKeyboardFocus();
			longitude.YieldKeyboardFocus();
			title.YieldKeyboardFocus();
			seed.YieldKeyboardFocus();
			story.YieldKeyboardFocus();
		}
	}
}
