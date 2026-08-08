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

		readonly ModData modData;
		readonly Action onExit;
		readonly TextFieldWidget location;
		readonly TextFieldWidget latitude;
		readonly TextFieldWidget longitude;
		readonly TextFieldWidget title;
		readonly TextFieldWidget radius;
		readonly TextFieldWidget seed;
		readonly TextFieldWidget story;
		readonly LabelWidget selectionLabel;
		readonly LabelWidget statusLabel;
		bool busy;
		int mapSize = 96;
		string status = "Search for a place to begin.";
		string selection = "No Earth location selected.";
		string generatedMapUid;

		[ObjectCreator.UseCtor]
		public EarthMissionStudioLogic(Widget widget, ModData modData, Action onExit, Action<string> onPlay, Action<string> onEdit)
		{
			this.modData = modData;
			this.onExit = onExit;
			location = widget.Get<TextFieldWidget>("LOCATION");
			latitude = widget.Get<TextFieldWidget>("LATITUDE");
			longitude = widget.Get<TextFieldWidget>("LONGITUDE");
			title = widget.Get<TextFieldWidget>("TITLE");
			radius = widget.Get<TextFieldWidget>("RADIUS");
			seed = widget.Get<TextFieldWidget>("SEED");
			story = widget.Get<TextFieldWidget>("STORY");

			location.Text = "Riyadh, Saudi Arabia";
			title.Text = "Riyadh Crossing";
			radius.Text = "3500";
			seed.Text = "1";

			selectionLabel = widget.Get<LabelWidget>("SELECTION");
			selectionLabel.GetText = () => selection;
			statusLabel = widget.Get<LabelWidget>("STATUS");
			statusLabel.GetText = () => status;

			var size = widget.Get<DropDownButtonWidget>("MAP_SIZE");
			size.GetText = () => MapSizeLabels[mapSize];
			size.OnMouseDown = _ => ShowMapSizeDropdown(size);

			var search = widget.Get<ButtonWidget>("SEARCH");
			search.IsDisabled = () => busy || string.IsNullOrWhiteSpace(location.Text);
			search.OnClick = () => _ = SearchAsync();
			var generate = widget.Get<ButtonWidget>("GENERATE");
			generate.IsDisabled = () => busy || string.IsNullOrWhiteSpace(latitude.Text) || string.IsNullOrWhiteSpace(longitude.Text);
			generate.OnClick = () => _ = GenerateAsync();

			var play = widget.Get<ButtonWidget>("PLAY");
			play.IsDisabled = () => busy || generatedMapUid == null;
			play.OnClick = () => Complete(onPlay);
			var edit = widget.Get<ButtonWidget>("EDIT");
			edit.IsDisabled = () => busy || generatedMapUid == null;
			edit.OnClick = () => Complete(onEdit);

			widget.Get<ButtonWidget>("BACK").OnClick = Close;
		}

		void ShowMapSizeDropdown(DropDownButtonWidget dropdown)
		{
			ScrollItemWidget SetupItem(int value, ScrollItemWidget template)
			{
				var item = ScrollItemWidget.Setup(template, () => mapSize == value, () => mapSize = value);
				item.Get<LabelWidget>("LABEL").GetText = () => MapSizeLabels[value];
				return item;
			}

			dropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 260, MapSizeLabels.Keys, SetupItem);
		}

		async System.Threading.Tasks.Task SearchAsync()
		{
			var query = location.Text.Trim();
			SetBusy("Finding that place on Earth...");
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
					selection = WidgetUtils.TruncateText($"Selected: {name}", selectionLabel.Bounds.Width,
						Game.Renderer.Fonts[selectionLabel.Font]);
					SetIdle("Location ready. Add a story seed or generate the battlefield now.");
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

		async System.Threading.Tasks.Task GenerateAsync()
		{
			YieldTextFocus();
			if (!double.TryParse(latitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) || lat < -90 || lat > 90 ||
				!double.TryParse(longitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) || lon < -180 || lon > 180 ||
				!int.TryParse(radius.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var radiusMeters) || radiusMeters < 500 || radiusMeters > 20000 ||
				!int.TryParse(seed.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seedValue) || seedValue < 0)
			{
				SetIdle("Check coordinates, radius (500-20000 m), and seed before generating.");
				return;
			}

			SetBusy("Reading terrain and building the OpenRA map...");
			generatedMapUid = null;
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
					story_seed = story.Text.Trim(),
					source = "openstreetmap"
				};

				using var document = await OpenRAAILocalClient.PostAsync(baseUri, "v1/missions/generate", payload, 120);
				var result = document.RootElement.Clone();
				Game.RunAfterTick(() =>
				{
					modData.MapCache.UpdateMaps();
					generatedMapUid = modData.MapCache.PickLastModifiedMap(MapVisibility.Lobby);
					var filename = result.GetProperty("filename").GetString() ?? "generated map";
					var source = result.GetProperty("source_status").GetString() ?? "terrain source complete";
					SetIdle(generatedMapUid == null
						? $"Generated {filename}, but OpenRA has not indexed it yet. Try generating again."
						: $"Ready: {filename}  |  {source}. Choose Play or Edit.");
				});
			}
			catch (Exception e)
			{
				Game.RunAfterTick(() => SetIdle($"Mission generation failed: {e.Message}"));
			}
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
			status = WidgetUtils.WrapText(message, statusLabel.Bounds.Width, Game.Renderer.Fonts[statusLabel.Font]);
		}

		void YieldTextFocus()
		{
			location.YieldKeyboardFocus();
			latitude.YieldKeyboardFocus();
			longitude.YieldKeyboardFocus();
			title.YieldKeyboardFocus();
			radius.YieldKeyboardFocus();
			seed.YieldKeyboardFocus();
			story.YieldKeyboardFocus();
		}
	}
}
