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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Experience;
using OpenRA.Mods.Common.FactionCatalog;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	/// <summary>
	/// Native Faction Catalog: the shared editorial catalog for the loaded game mode, with every number
	/// read from the loaded rules and deep links to the public faction/unit pages.
	/// </summary>
	public sealed class FactionCatalogLogic : ChromeLogic
	{
		// Referenced here, not by the shared main menu chrome, so mods without the catalog need no message.
		[FluentReference]
		const string MainMenuButton = "button-main-menu-factions";

		public const string PanelId = "FACTION_CATALOG_PANEL";

		public static string MainMenuButtonText() => FluentProvider.GetMessage(MainMenuButton);

		/// <summary>The panel ships in common chrome but only mods that declare it (Classic and RA2) offer it.</summary>
		public static bool IsAvailable(ModData modData)
		{
			return modData.Manifest.ChromeLayout.Any(file => file.EndsWith("faction-catalog.yaml", StringComparison.OrdinalIgnoreCase));
		}

		static readonly Color NeutralAccent = Color.FromArgb(150, 150, 150);
		static readonly Color AlliesAccent = Color.FromArgb(96, 140, 206);
		static readonly Color SovietAccent = Color.FromArgb(196, 72, 60);

		// Some skins draw selected and plain list rows alike; the selected name is always gold.
		static readonly Color SelectedText = Color.Gold;

		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly FactionCatalogView view;
		readonly ScrollPanelWidget factionList;
		readonly ScrollItemWidget factionHeaderTemplate;
		readonly ScrollItemWidget factionTemplate;
		readonly ScrollPanelWidget roster;
		readonly ScrollItemWidget groupTemplate;
		readonly ScrollItemWidget unitTemplate;
		readonly ScrollPanelWidget detail;
		readonly Widget detailTitleTemplate;
		readonly Widget detailSectionTemplate;
		readonly Widget detailRowTemplate;
		readonly LabelWidget detailTextTemplate;
		readonly Dictionary<string, bool> paletteExists = [];
		readonly List<CatalogFactionEntry> orderedFactions = [];
		readonly int timestep;

		readonly Dictionary<string, Action> captureCommands = [];

		CatalogFactionEntry selectedFaction;
		CatalogRosterEntry selectedUnit;
		string webStatusText = "";

		[ObjectCreator.UseCtor]
		public FactionCatalogLogic(Widget widget, ModData modData, World world, WorldRenderer worldRenderer, Action onExit,
			string initialFaction, Action onOpenExperience)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			timestep = world?.Timestep > 0 ? world.Timestep : 40;

			var catalog = SharedFactionCatalog.LoadInstalled(out var catalogError);
			var experience = modData.GetOrNull<ExperienceCatalog>();
			view = FactionCatalogView.Build(modData.Manifest.Id, modData.DefaultRules, experience, catalog,
				key => FluentProvider.GetMessage(key), catalogError);
			FactionCatalogPublisher.Publish(modData, modData.DefaultRules, timestep, "faction-catalog");

			var subtitle = widget.Get<LabelWidget>("SUBTITLE");
			var modeText = view.ModeInfo != null ? $"{view.ModeInfo.Name} · {view.ModeInfo.Subtitle}" : modData.Manifest.Metadata.TitleTranslated;
			if (experience != null)
				modeText += $" · Experience: {experience.ActiveTitle}";

			modeText += " · Numbers are read from the loaded rules";
			subtitle.GetText = () => modeText;

			factionList = widget.Get<ScrollPanelWidget>("FACTION_LIST");
			factionHeaderTemplate = factionList.Get<ScrollItemWidget>("HEADER_TEMPLATE");
			factionTemplate = factionList.Get<ScrollItemWidget>("FACTION_TEMPLATE");
			factionList.RemoveChildren();

			roster = widget.Get<ScrollPanelWidget>("ROSTER");
			groupTemplate = roster.Get<ScrollItemWidget>("GROUP_TEMPLATE");
			unitTemplate = roster.Get<ScrollItemWidget>("UNIT_TEMPLATE");
			roster.RemoveChildren();

			detail = widget.Get<ScrollPanelWidget>("DETAIL");
			detailTitleTemplate = detail.Get("DETAIL_TITLE_TEMPLATE");
			detailSectionTemplate = detail.Get("DETAIL_SECTION_TEMPLATE");
			detailRowTemplate = detail.Get("DETAIL_ROW_TEMPLATE");
			detailTextTemplate = detail.Get<LabelWidget>("DETAIL_TEXT_TEMPLATE");
			detail.RemoveChildren();

			var keyHint = widget.Get<LabelWidget>("KEY_HINT");
			var defaultHint = keyHint.GetText();
			keyHint.GetText = () => string.IsNullOrEmpty(webStatusText) ? defaultHint : webStatusText;
			keyHint.GetColor = () => string.IsNullOrEmpty(webStatusText) ? keyHint.TextColor : Color.Gold;

			SetupHeader(widget);
			PopulateFactions();
			SetupOtherModeNote(widget.Get<LabelWithTooltipWidget>("OTHER_MODE_NOTE"));

			var factionWeb = widget.Get<ButtonWidget>("FACTION_WEB_BUTTON");
			factionWeb.IsDisabled = () => selectedFaction?.WebUrl == null;
			factionWeb.GetTooltipText = () => selectedFaction?.WebUrl ?? "No public page for this faction yet";
			factionWeb.OnClick = () => OpenWeb(selectedFaction?.WebUrl);

			var unitWeb = widget.Get<ButtonWidget>("UNIT_WEB_BUTTON");
			unitWeb.IsDisabled = () => selectedUnit?.WebUrl == null;
			unitWeb.GetTooltipText = () => selectedUnit?.WebUrl ?? "Only catalog signature units have public pages";
			unitWeb.OnClick = () => OpenWeb(selectedUnit?.WebUrl);

			var experienceButton = widget.Get<ButtonWidget>("EXPERIENCE_BUTTON");
			experienceButton.IsVisible = () => onOpenExperience != null && experience != null;
			experienceButton.OnClick = () =>
			{
				Ui.CloseWindow();
				onOpenExperience?.Invoke();
			};

			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				Ui.CloseWindow();
				onExit();
			};

			widget.Get<LogicKeyListenerWidget>("CATALOG_KEYS").AddHandler(HandleKey);
			captureCommands["back"] = widget.Get<ButtonWidget>("BACK_BUTTON").OnClick;
			captureCommands["experience"] = experienceButton.OnClick;

			var initial = orderedFactions.FirstOrDefault(f => Matches(f, initialFaction)) ??
				orderedFactions.FirstOrDefault(f => f.Kind == CatalogFactionKind.Modern && f.Availability == CatalogAvailability.Active) ??
				orderedFactions.FirstOrDefault();
			if (initial != null)
				SelectFaction(initial, null);

			ScheduleCapture();
		}

		static bool Matches(CatalogFactionEntry faction, string key)
		{
			return !string.IsNullOrEmpty(key) && (string.Equals(faction.Key, key, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(faction.InternalName, key, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(faction.Catalog?.Id, key, StringComparison.OrdinalIgnoreCase));
		}

		void SetupHeader(Widget widget)
		{
			var header = widget.Get("FACTION_HEADER");
			header.Get<ColorBlockWidget>("ACCENT_BAR").GetColor = () => AccentOf(selectedFaction);
			var name = header.Get<LabelWithTooltipWidget>("FACTION_NAME");
			var tagline = header.Get<LabelWithTooltipWidget>("FACTION_TAGLINE");
			var facts = header.Get<LabelWithTooltipWidget>("FACTION_FACTS");
			var summary = header.Get<LabelWithTooltipWidget>("FACTION_SUMMARY");
			var summaryFont = Game.Renderer.Fonts[summary.Font];

			var signature = header.Get("SIGNATURE_FRAME");
			var signatureLabel = signature.Get<LabelWidget>("SIGNATURE_LABEL");
			var defaultSignatureLabel = signatureLabel.GetText();
			var signatureIcon = signature.Get<SpriteWidget>("SIGNATURE_ICON");
			var signatureName = signature.Get<LabelWidget>("SIGNATURE_NAME");
			var signatureRole = signature.Get<LabelWidget>("SIGNATURE_ROLE");
			var signatureButton = signature.Get<ButtonWidget>("SIGNATURE_BUTTON");
			signatureIcon.GetSprite = () => null;
			signatureIcon.GetPalette = () => null;

			CatalogRosterEntry Featured() => selectedFaction?.Signature ??
				selectedFaction?.Roster.FirstOrDefault(r => r.IsExclusive && r.Domain != "Buildings") ??
				selectedFaction?.Roster.FirstOrDefault(r => r.IsExclusive);

			CatalogFactionEntry cachedFor = null;
			string summaryText = "", summaryTooltip = null;
			void Refresh()
			{
				if (cachedFor == selectedFaction)
					return;

				cachedFor = selectedFaction;
				var f = selectedFaction;
				if (f == null)
					return;

				var title = f.Title != null ? $"{f.Name} — {f.Title}" : f.Name;
				WidgetUtils.TruncateLabelToTooltip(name, title);

				var descriptionLines = (f.RulesDescription ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				WidgetUtils.TruncateLabelToTooltip(tagline, f.Tagline ?? descriptionLines.FirstOrDefault() ?? "");

				var parts = new List<string>();
				if (!string.IsNullOrEmpty(f.Side))
					parts.Add(f.Side);

				if (!string.IsNullOrEmpty(f.Doctrine))
					parts.Add("Doctrine: " + f.Doctrine);

				parts.Add(f.Kind == CatalogFactionKind.Modern ? "Modern faction" : "Original country");
				var units = f.Roster.Count();
				var unique = f.Roster.Count(r => r.IsExclusive);
				parts.Add(f.Availability switch
				{
					CatalogAvailability.Active => $"{units} roster entries, {unique} unique",
					CatalogAvailability.Disabled => "Not loaded by the current experience",
					_ => "Faction pack not installed"
				});
				WidgetUtils.TruncateLabelToTooltip(facts, string.Join(" · ", parts));

				var body = new List<string>();
				if (f.Catalog != null)
				{
					body.Add(f.Catalog.Story);
					body.Add(f.Catalog.Playstyle);
				}
				else if (descriptionLines.Length > 1)
					body.Add(string.Join(" · ", descriptionLines.Skip(1)));
				else if (f.Pack != null)
					body.Add(f.Pack.Description);

				if (f.Availability == CatalogAvailability.Disabled)
					body.Insert(0, $"{f.Name} is installed but the active experience does not load it. " +
						"Enable it in Factions & Capabilities to play it and see live statistics.");
				else if (f.Availability == CatalogAvailability.NotInstalled)
					body.Insert(0, $"{f.Name} is listed for this game but its faction pack is missing from this build.");

				var full = string.Join("\n", body.Where(b => !string.IsNullOrWhiteSpace(b)));
				var wrapped = WidgetUtils.WrapText(full.Replace("\n", " "), summary.Bounds.Width, summaryFont);
				var lines = wrapped.Split('\n');
				var maxLines = Math.Max(1, summary.Bounds.Height / Math.Max(1, summaryFont.Measure("Ag").Y + 1));
				if (lines.Length > maxLines)
				{
					lines = lines.Take(maxLines).ToArray();
					lines[^1] = WidgetUtils.TruncateText(lines[^1] + " …", summary.Bounds.Width, summaryFont);
				}

				summaryText = string.Join("\n", lines);
				var tooltip = new List<string>();
				if (f.Catalog != null)
				{
					tooltip.Add(f.Catalog.Story);
					tooltip.Add("Playstyle: " + f.Catalog.Playstyle);
					tooltip.Add("Strengths: " + f.Catalog.Strengths);
					tooltip.Add("Counterplay: " + f.Catalog.Counterplay);
				}
				else
					tooltip.Add(full);

				summaryTooltip = WidgetUtils.WrapText(string.Join("\n", tooltip.Where(t => !string.IsNullOrWhiteSpace(t))), 520, summaryFont);
				summary.GetTooltipText = lines.Length < wrapped.Split('\n').Length || f.Catalog != null ? () => summaryTooltip : null;

				var featured = Featured();
				var signatureCount = f.SignatureUnits.Count();
				var signatureText = signatureCount > 1 ? $"{defaultSignatureLabel} (1 of {signatureCount})" :
					f.Signature != null ? defaultSignatureLabel : featured == null ? "No unique units" :
					featured.Domain is "Buildings" or "Defenses" ? "Unique structure" : "Unique unit";
				signatureLabel.GetText = () => signatureText;
				signatureIcon.GetSprite = () => featured == null ? null : IconFor(featured, f, out _);
				signatureIcon.GetPalette = () => featured == null ? null : IconPalette(featured);
				var featuredName = featured == null ? "" :
					WidgetUtils.TruncateText(featured.DisplayName, signatureName.Bounds.Width, Game.Renderer.Fonts[signatureName.Font]);
				var featuredRole = featured == null ? "" :
					WidgetUtils.TruncateText(featured.Role, signatureRole.Bounds.Width, Game.Renderer.Fonts[signatureRole.Font]);
				signatureName.GetText = () => featuredName;
				signatureRole.GetText = () => featuredRole;
				signatureButton.IsDisabled = () => featured == null;
				signatureButton.OnClick = () => SelectUnit(featured, true);
			}

			summary.GetText = () =>
			{
				Refresh();
				return summaryText;
			};

			name.IsVisible = tagline.IsVisible = () =>
			{
				Refresh();
				return selectedFaction != null;
			};
		}

		void SetupOtherModeNote(LabelWithTooltipWidget note)
		{
			string text;
			if (view.Catalog == null)
				text = "Shared catalog unavailable; showing loaded rules only.";
			else if (view.OtherModeFactions.Length == 0)
				text = "";
			else
			{
				var groups = view.OtherModeFactions.GroupBy(f => string.Join(" and ", f.Variants.Where(v => v.Value.IsImplemented)
					.Select(v => view.Catalog.Mode(v.Key)?.Name ?? v.Key)));
				text = string.Join("; ", groups.Select(g => $"{(string.IsNullOrEmpty(g.Key) ? "Planned" : g.Key + " only")}: {string.Join(", ", g.Select(f => f.Name))}"));
			}

			var font = Game.Renderer.Fonts[note.Font];
			var wrapped = WidgetUtils.WrapText(text, note.Bounds.Width, font);
			var lines = wrapped.Split('\n');
			var shown = lines.Length > 2 ? string.Join("\n", lines.Take(2)) + "…" : wrapped;
			note.GetText = () => shown;
			note.GetTooltipText = lines.Length > 2 || view.CatalogError != null ? () => view.CatalogError ?? text : null;
		}

		void PopulateFactions()
		{
			foreach (var (kind, title) in new[]
			{
				(CatalogFactionKind.Modern, "MODERN FACTIONS"), (CatalogFactionKind.Original, "ORIGINAL COUNTRIES")
			})
			{
				var entries = view.Factions.Where(f => f.Kind == kind).ToArray();
				if (entries.Length == 0)
					continue;

				var header = ScrollItemWidget.Setup(factionHeaderTemplate, () => false, () => { });
				header.Get<LabelWidget>("LABEL").GetText = () => title;
				factionList.AddChild(header);

				foreach (var entry in entries)
				{
					var faction = entry;
					orderedFactions.Add(faction);
					var item = ScrollItemWidget.Setup(faction.Key, factionTemplate, () => selectedFaction == faction,
						() => SelectFaction(faction, null), null);
					item.Get<ColorBlockWidget>("ACCENT").GetColor = () => AccentOf(faction);

					var name = item.Get<LabelWithTooltipWidget>("NAME");
					WidgetUtils.TruncateLabelToTooltip(name, faction.Title != null ? $"{faction.Name} — {faction.Title}" : faction.Name);
					var factionTextColor = name.TextColor;
					name.GetColor = () => selectedFaction == faction ? SelectedText : factionTextColor;

					var detailLabel = item.Get<LabelWidget>("DETAIL");
					var detailText = faction.Availability switch
					{
						CatalogAvailability.Disabled => "Disabled in this experience",
						CatalogAvailability.NotInstalled => "Faction pack missing",
						_ => string.Join(" · ", new[]
						{
							faction.Side,
							faction.Doctrine ?? $"{faction.Roster.Count(r => r.IsExclusive)} unique units"
						}.Where(s => !string.IsNullOrEmpty(s)))
					};
					detailText = WidgetUtils.TruncateText(detailText, detailLabel.Bounds.Width, Game.Renderer.Fonts[detailLabel.Font]);
					detailLabel.GetText = () => detailText;
					if (faction.Availability != CatalogAvailability.Active)
						detailLabel.GetColor = () => Color.Orange;

					factionList.AddChild(item);
				}
			}
		}

		void SelectFaction(CatalogFactionEntry faction, string actor)
		{
			selectedFaction = faction;
			webStatusText = "";
			factionList.ScrollToItem(faction.Key, true);
			PopulateRoster();

			var target = faction.Roster.FirstOrDefault(r => string.Equals(r.Actor, actor, StringComparison.OrdinalIgnoreCase)) ??
				faction.Signature ?? faction.Roster.FirstOrDefault(r => r.IsExclusive) ?? faction.Roster.FirstOrDefault();
			SelectUnit(target, true);
		}

		void PopulateRoster()
		{
			roster.RemoveChildren();
			foreach (var group in selectedFaction.Groups)
			{
				var header = ScrollItemWidget.Setup(groupTemplate, () => false, () => { });
				var label = $"{group.Domain.ToUpperInvariant()} ({group.Entries.Length})";
				header.Get<LabelWidget>("LABEL").GetText = () => label;
				roster.AddChild(header);

				foreach (var entry in group.Entries)
				{
					var unit = entry;
					var faction = selectedFaction;
					var item = ScrollItemWidget.Setup(UnitKey(unit), unitTemplate, () => selectedUnit == unit, () => SelectUnit(unit, false), null);

					var icon = item.Get<SpriteWidget>("ICON");
					var sprite = IconFor(unit, faction, out var palette);
					SetIcon(icon, sprite, palette);

					var name = item.Get<LabelWithTooltipWidget>("NAME");
					WidgetUtils.TruncateLabelToTooltip(name, unit.DisplayName);
					var unitTextColor = name.TextColor;
					name.GetColor = () => selectedUnit == unit ? SelectedText : unitTextColor;

					var tag = unit.IsSignature ? "SIGNATURE" : unit.IsExclusive ? "UNIQUE" : "";
					item.Get<LabelWidget>("TAG").GetText = () => tag;
					item.Get<LabelWidget>("TAG").GetColor = () => unit.IsSignature ? Color.Gold : Color.LightGray;

					var role = item.Get<LabelWidget>("ROLE");
					var roleText = WidgetUtils.TruncateText(unit.Role ?? "", role.Bounds.Width, Game.Renderer.Fonts[role.Font]);
					role.GetText = () => roleText;

					var facts = item.Get<LabelWidget>("FACTS");
					var factsText = WidgetUtils.TruncateText(ShortFacts(unit), facts.Bounds.Width, Game.Renderer.Fonts[facts.Font]);
					facts.GetText = () => factsText;
					if (unit.Stats == null)
						facts.GetColor = () => Color.Orange;

					roster.AddChild(item);
				}
			}

			roster.ScrollToTop();
		}

		static string UnitKey(CatalogRosterEntry unit) => unit.Domain + ":" + unit.Actor;

		static void SetIcon(SpriteWidget icon, Sprite sprite, string palette)
		{
			// Cloned sprite widgets do not inherit their scale getter.
			icon.GetScale = () => 1f;
			icon.GetSprite = () => sprite;
			icon.GetPalette = () => palette;
		}

		static string ShortFacts(CatalogRosterEntry unit)
		{
			var s = unit.Stats;
			if (s == null)
				return "Rules not loaded in this experience";

			var parts = new List<string>();
			if (s.Cost > 0)
				parts.Add("$" + s.Cost.ToString("N0", CultureInfo.InvariantCulture));

			if (s.HitPoints > 0)
				parts.Add(s.HitPoints.ToString("N0", CultureInfo.InvariantCulture) + " HP");

			if (!string.IsNullOrEmpty(s.Armor))
				parts.Add(CatalogUnitStats.Humanize(s.Armor));

			if (s.Weapons.Length > 0)
				parts.Add(s.TargetSummary());

			return string.Join(" · ", parts);
		}

		void SelectUnit(CatalogRosterEntry unit, bool scroll)
		{
			selectedUnit = unit;
			webStatusText = "";
			if (unit != null && scroll)
				roster.ScrollToItem(UnitKey(unit), true);

			PopulateDetail();
		}

		void PopulateDetail()
		{
			detail.RemoveChildren();
			var unit = selectedUnit;
			if (unit == null)
			{
				AddParagraph("This faction has no roster entries in the loaded rules.");
				return;
			}

			var title = detailTitleTemplate.Clone();
			title.IsVisible = () => true;
			var sprite = IconFor(unit, selectedFaction, out var palette);
			SetIcon(title.Get<SpriteWidget>("ICON"), sprite, palette);
			var nameLabel = title.Get<LabelWidget>("NAME");
			var name = WidgetUtils.TruncateText(unit.DisplayName, nameLabel.Bounds.Width, Game.Renderer.Fonts[nameLabel.Font]);
			nameLabel.GetText = () => name;
			var roleLabel = title.Get<LabelWidget>("ROLE");
			var roleText = unit.Role + (unit.IsSignature ? " · Signature unit" : unit.IsExclusive ? $" · Unique to {selectedFaction.Name}" : "");
			roleText = WidgetUtils.TruncateText(roleText, roleLabel.Bounds.Width, Game.Renderer.Fonts[roleLabel.Font]);
			roleLabel.GetText = () => roleText;
			detail.AddChild(title);

			var s = unit.Stats;
			if (s == null)
			{
				AddSection("Live rules");
				AddParagraph(selectedFaction.Availability == CatalogAvailability.Disabled
					? $"{selectedFaction.Name} is not loaded by the current experience, so no statistics are shown. Enable it in Factions & Capabilities."
					: $"`{unit.Actor}` is not present in the loaded rules.");
			}
			else
			{
				AddSection("Live rules");
				AddRow("Cost", s.Cost > 0 ? "$" + s.Cost.ToString("N0", CultureInfo.InvariantCulture) : "—");
				if (s.BuildTimeTicks > 0)
					AddRow("Build time", WidgetUtils.FormatTime(s.BuildTimeTicks, timestep) + " at normal speed");

				AddRow("Hit points", s.HitPoints > 0 ? s.HitPoints.ToString("N0", CultureInfo.InvariantCulture) : "—");
				AddRow("Armor", string.IsNullOrEmpty(s.Armor) ? "—" : CatalogUnitStats.Humanize(s.Armor));
				if (s.Speed > 0)
					AddRow("Speed", s.Speed.ToString(CultureInfo.InvariantCulture));

				if (s.Sight.Length > 0)
					AddRow("Sight", CatalogUnitStats.FormatCells(s.Sight) + " cells");

				if (s.Power != 0)
					AddRow("Power", s.Power.ToString(CultureInfo.InvariantCulture));

				if (s.Passengers > 0)
					AddRow("Transport", $"{s.Passengers} passenger slots");

				AddRow("Attacks", s.TargetSummary());
				if (s.Roles.Length > 0)
					AddRow("Roles", string.Join(", ", s.Roles.Select(CatalogUnitStats.Humanize)));

				if (s.Counters.Length > 0)
					AddRow("Strong against", string.Join(", ", s.Counters.Select(CatalogUnitStats.Humanize)));

				AddRow("Requires", s.Prerequisites.Length > 0 ? string.Join(", ", s.Prerequisites) : "No visible prerequisite");

				if (s.Weapons.Length > 0)
				{
					AddSection("Weapons");
					foreach (var weapon in s.Weapons)
					{
						var range = weapon.MinRange.Length > 0 ?
							$"{CatalogUnitStats.FormatCells(weapon.MinRange)}–{CatalogUnitStats.FormatCells(weapon.Range)}" :
							CatalogUnitStats.FormatCells(weapon.Range);
						var damage = weapon.Burst > 1 ? $"{weapon.Damage}×{weapon.Burst}" : weapon.Damage.ToString(CultureInfo.InvariantCulture);
						var targets = string.Join(", ", CatalogUnitStats.ReadableTargets(weapon.Targets));
						AddRow(weapon.Name, $"Range {range} · Damage {damage} · Reload {weapon.ReloadDelay} · Hits {targets}");
					}
				}

				if (!string.IsNullOrWhiteSpace(s.Description))
				{
					AddSection("Description");
					AddParagraph(CatalogUnitStats.Reflow(s.Description));
				}
			}

			if (unit.CatalogUnit != null)
			{
				AddSection("Field notes");
				AddParagraph(unit.CatalogUnit.Story);
				AddRow("Use it to", unit.CatalogUnit.Strengths);
				AddRow("Watch out", unit.CatalogUnit.Counterplay);
			}

			AddRow("Rules actor", unit.Actor);
			detail.ScrollToTop();
		}

		void AddSection(string text)
		{
			var section = detailSectionTemplate.Clone();
			section.IsVisible = () => true;
			var upper = text.ToUpperInvariant();
			section.Get<LabelWidget>("LABEL").GetText = () => upper;
			detail.AddChild(section);
		}

		void AddRow(string key, string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return;

			var row = detailRowTemplate.Clone();
			row.IsVisible = () => true;
			var keyLabel = row.Get<LabelWidget>("KEY");
			var valueLabel = row.Get<LabelWidget>("VALUE");
			var keyText = WidgetUtils.TruncateText(key, keyLabel.Bounds.Width, Game.Renderer.Fonts[keyLabel.Font]);
			keyLabel.GetText = () => keyText;

			var font = Game.Renderer.Fonts[valueLabel.Font];
			var wrapped = WidgetUtils.WrapText(value, valueLabel.Bounds.Width, font);
			var lineCount = wrapped.Split('\n').Length;
			valueLabel.GetText = () => wrapped;
			valueLabel.VAlign = TextVAlign.Top;
			valueLabel.Bounds.Height = Math.Max(row.Bounds.Height, font.Measure(wrapped).Y + 2);
			row.Bounds.Height = valueLabel.Bounds.Height + (lineCount > 1 ? 2 : 0);
			keyLabel.VAlign = TextVAlign.Top;
			detail.AddChild(row);
		}

		void AddParagraph(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return;

			var label = detailTextTemplate.Clone();
			label.IsVisible = () => true;
			var font = Game.Renderer.Fonts[label.Font];
			var wrapped = WidgetUtils.WrapText(text.Trim(), label.Bounds.Width, font);
			label.GetText = () => wrapped;
			label.Bounds.Height = font.Measure(wrapped).Y + 4;
			detail.AddChild(label);
		}

		void OpenWeb(string url)
		{
			if (url == null)
				return;

			webStatusText = Game.Renderer.TryOpenUrl(url) ? $"Opened {url} in your browser." :
				$"Could not open a browser. Visit {url}";
		}

		bool HandleKey(KeyInput e)
		{
			if (e.Event != KeyInputEvent.Down)
				return false;

			switch (e.Key)
			{
				case Keycode.LEFT:
				case Keycode.RIGHT:
				{
					if (orderedFactions.Count == 0)
						return true;

					var index = orderedFactions.IndexOf(selectedFaction);
					index = (index + (e.Key == Keycode.LEFT ? -1 : 1) + orderedFactions.Count) % orderedFactions.Count;
					SelectFaction(orderedFactions[index], null);
					return true;
				}

				case Keycode.UP:
				case Keycode.DOWN:
				{
					var units = selectedFaction?.Roster.ToList();
					if (units == null || units.Count == 0)
						return true;

					var index = units.IndexOf(selectedUnit);
					index = Math.Clamp(index + (e.Key == Keycode.UP ? -1 : 1), 0, units.Count - 1);
					SelectUnit(units[index], true);
					return true;
				}

				case Keycode.PAGEUP:
					detail.ScrollToTop(true);
					return true;

				case Keycode.PAGEDOWN:
					detail.ScrollToBottom(true);
					return true;

				case Keycode.RETURN:
				case Keycode.KP_ENTER:
					OpenWeb(selectedUnit?.WebUrl ?? selectedFaction?.WebUrl);
					return true;
			}

			return false;
		}

		static Color AccentOf(CatalogFactionEntry faction)
		{
			if (faction == null)
				return NeutralAccent;

			if (faction.Accent.HasValue)
				return faction.Accent.Value;

			return faction.Side?.ToLowerInvariant() switch
			{
				"allies" => AlliesAccent,
				"soviet" or "soviets" => SovietAccent,
				_ => NeutralAccent
			};
		}

		Sprite IconFor(CatalogRosterEntry unit, CatalogFactionEntry faction, out string palette)
		{
			palette = null;
			var info = unit?.Info;
			var rsi = info?.TraitInfoOrDefault<RenderSpritesInfo>();
			var bi = info?.TraitInfoOrDefault<BuildableInfo>();
			if (rsi == null || bi == null || world == null)
				return null;

			try
			{
				var image = rsi.GetImage(info, faction?.InternalName);
				if (!world.Map.Sequences.HasSequence(image, bi.Icon))
					return null;

				var animation = new Animation(world, image);
				animation.Play(bi.Icon);
				palette = IconPalette(unit);
				return palette == null ? null : animation.Image;
			}
			catch (Exception e)
			{
				Log.Write("debug", $"Faction catalog icon for {info.Name} failed: {e.Message}");
				return null;
			}
		}

		string IconPalette(CatalogRosterEntry unit)
		{
			var bi = unit?.Info?.TraitInfoOrDefault<BuildableInfo>();
			if (bi == null)
				return null;

			if (bi.IconPaletteIsPlayerPalette)
			{
				var owner = world.LocalPlayer ?? world.Players.FirstOrDefault(p => p.Playable) ?? world.WorldActor.Owner;
				var playerPalette = bi.IconPalette + owner.InternalName;
				if (HasPalette(playerPalette))
					return playerPalette;
			}

			return HasPalette(bi.IconPalette) ? bi.IconPalette : HasPalette("chrome") ? "chrome" : null;
		}

		bool HasPalette(string name)
		{
			if (string.IsNullOrEmpty(name) || worldRenderer == null)
				return false;

			if (!paletteExists.TryGetValue(name, out var exists))
			{
				try
				{
					exists = worldRenderer.Palette(name) != null;
				}
				catch (InvalidOperationException)
				{
					exists = false;
				}

				paletteExists[name] = exists;
			}

			return exists;
		}

		/// <summary>Opt-in deterministic states for local visual regression capture.</summary>
		void ScheduleCapture()
		{
			var request = Environment.GetEnvironmentVariable("OPENRA_AI_CAPTURE_FACTION_CATALOG");
			if (string.IsNullOrWhiteSpace(request))
				return;

			var steps = request.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			var exit = Environment.GetEnvironmentVariable("OPENRA_AI_CAPTURE_FACTION_CATALOG_EXIT") == "1";
			void Step(int index)
			{
				if (index >= steps.Length)
				{
					if (exit)
						Game.RunAfterDelay(1500, Game.Exit);

					return;
				}

				// "@key:RIGHT" drives keyboard navigation, "@back"/"@experience" press buttons,
				// anything else is "<faction>[:<actor>]".
				var step = steps[index];
				string result;
				if (step.StartsWith("@key:", StringComparison.Ordinal) && Enum.TryParse<Keycode>(step[5..], true, out var key))
					result = HandleKey(new KeyInput { Key = key, Event = KeyInputEvent.Down }) ? $"{selectedFaction?.Key}/{selectedUnit?.Actor}" : "ignored";
				else if (step.StartsWith('@') && captureCommands.TryGetValue(step[1..], out var command))
				{
					command();
					result = "pressed";
				}
				else
				{
					var parts = step.Split(':', 2);
					var faction = orderedFactions.FirstOrDefault(f => Matches(f, parts[0]));
					if (faction != null)
						SelectFaction(faction, parts.Length > 1 ? parts[1] : null);

					result = faction == null ? "missing" : $"{faction.Key}/{selectedUnit?.Actor}";
				}

				Log.Write("debug", $"Faction catalog capture {index}: {step} -> {result}");
				Game.RunAfterDelay(900, () =>
				{
					Game.TakeScreenshot();
					Game.RunAfterDelay(900, () => Step(index + 1));
				});
			}

			Game.RunAfterDelay(1200, () => Step(0));
		}
	}
}
