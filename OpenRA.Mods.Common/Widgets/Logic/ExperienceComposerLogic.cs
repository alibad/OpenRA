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
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenRA.Mods.Common.Experience;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class ExperienceComposerLogic : ChromeLogic
	{
		[FluentReference]
		const string CopyPackFolder = "button-experience-copy-pack-folder";

		[FluentReference]
		const string PackFolderCopied = "button-experience-pack-folder-copied";

		[FluentReference]
		const string CreatePackTitle = "dialog-experience-create-pack-title";

		[FluentReference]
		const string CreatePackPrompt = "dialog-experience-create-pack-prompt";

		[FluentReference]
		const string DuplicatePackTitle = "dialog-experience-duplicate-pack-title";

		[FluentReference]
		const string DuplicatePackPrompt = "dialog-experience-duplicate-pack-prompt";

		[FluentReference]
		const string RenamePackTitle = "dialog-experience-rename-pack-title";

		[FluentReference]
		const string RenamePackPrompt = "dialog-experience-rename-pack-prompt";

		[FluentReference]
		const string DeletePackTitle = "dialog-experience-delete-pack-title";

		[FluentReference("pack")]
		const string DeletePackPrompt = "dialog-experience-delete-pack-prompt";

		[FluentReference]
		const string DeletePackAccept = "dialog-experience-delete-pack-accept";

		[FluentReference]
		const string RemoveReplacementTitle = "dialog-experience-remove-replacement-title";

		[FluentReference("asset")]
		const string RemoveReplacementPrompt = "dialog-experience-remove-replacement-prompt";

		[FluentReference]
		const string RemoveReplacementAccept = "dialog-experience-remove-replacement-accept";

		[FluentReference]
		const string RemoveModuleTitle = "dialog-experience-remove-module-title";

		[FluentReference("module")]
		const string RemoveModulePrompt = "dialog-experience-remove-module-prompt";

		[FluentReference]
		const string RemoveModuleAccept = "dialog-experience-remove-module-accept";

		readonly Action onExit;
		readonly ModData modData;
		readonly ExperienceCatalog catalog;
		readonly ExperienceSettings settings;
		readonly ScrollPanelWidget componentPanel;
		readonly CheckboxWidget componentTemplate;
		readonly ScrollPanelWidget parameterPanel;
		readonly ScrollItemWidget parameterTemplate;
		readonly ScrollPanelWidget replacementPanel;
		readonly ScrollItemWidget replacementTemplate;
		readonly DropDownButtonWidget profileDropdown;
		readonly DropDownButtonWidget presentationDropdown;
		readonly LabelWidget profileDescription;
		readonly LabelWidget presentationDescription;
		readonly LabelWidget componentSummary;
		readonly TextFieldWidget componentSearch;
		readonly CheckboxWidget enabledComponentsOnly;
		readonly LabelWidget componentDetailTitle;
		readonly LabelWidget componentDetailDescription;
		readonly LabelWidget componentDetailImpact;
		readonly LabelWidget componentDetailScope;
		readonly LabelWidget componentDetailSource;
		readonly FactionPackPreviewWidget factionPreview;
		readonly LabelWidget factionPreviewEmpty;
		readonly LabelWidget componentKind;
		readonly LabelWidget gameplayFingerprint;
		readonly LabelWidget presentationFingerprint;
		readonly LabelWidget packFolder;
		readonly LabelWidget parameterEmpty;
		readonly LabelWidget replacementDetail;
		readonly LabelWidget packStatus;
		readonly ButtonWidget applyButton;

		IReadOnlyDictionary<string, PresentationPackDefinition> presentationPacks;
		ImmutableArray<string> workingComponents;
		Dictionary<string, string> workingParameterValues;
		string workingProfileId;
		string workingPresentationPackId;
		string selectedComponentId;
		string selectedReplacement;
		bool workingCustomComponents;
		bool packFolderCopied;
		bool showEnabledComponentsOnly;
		string aiStatus = "AI Assistant status: local service unavailable; inspect Settings > AI before enabling it.";

		[ObjectCreator.UseCtor]
		public ExperienceComposerLogic(Widget widget, ModData modData, Action onExit)
		{
			this.onExit = onExit;
			this.modData = modData;
			catalog = modData.GetOrNull<ExperienceCatalog>() ??
				throw new InvalidOperationException("The Experience Composer requires an ExperienceCatalog in mod.yaml.");
			settings = modData.GetSettings<ExperienceSettings>();

			workingProfileId = catalog.Profiles.ContainsKey(settings.Profile) ? settings.Profile : catalog.DefaultProfileId;
			workingCustomComponents = settings.UseCustomComponents;
			workingComponents = catalog.ActiveComponentIds;
			workingParameterValues = catalog.ParseParameterSettings(settings.ParameterValues)
				.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
			selectedComponentId = workingComponents.FirstOrDefault() ?? catalog.Components.Keys.Order().FirstOrDefault();
			presentationPacks = PresentationPackRegistry.Discover(catalog.Mod);
			workingPresentationPackId = presentationPacks.ContainsKey(settings.PresentationPack) ? settings.PresentationPack : "default";

			profileDropdown = widget.Get<DropDownButtonWidget>("PROFILE_DROPDOWN");
			profileDropdown.OnClick = ShowProfileDropdown;
			profileDropdown.GetText = ProfileTitle;
			widget.Get<ButtonWidget>("IMPORT_COMPONENT_BUTTON").OnClick = ImportCapabilityPack;
			widget.Get<ButtonWidget>("COPY_COMPONENT_FOLDER_BUTTON").OnClick = () =>
				Game.SetClipboardText(CapabilityPackRegistry.PackDirectory(catalog.Mod));

			presentationDropdown = widget.Get<DropDownButtonWidget>("PRESENTATION_DROPDOWN");
			presentationDropdown.OnClick = ShowPresentationDropdown;
			presentationDropdown.GetText = () => presentationPacks[workingPresentationPackId].Title;

			profileDescription = widget.Get<LabelWidget>("PROFILE_DESCRIPTION");
			presentationDescription = widget.Get<LabelWidget>("PRESENTATION_DESCRIPTION");
			componentSummary = widget.Get<LabelWidget>("COMPONENT_SUMMARY");
			componentSearch = widget.Get<TextFieldWidget>("COMPONENT_SEARCH");
			componentSearch.OnTextEdited = PopulateComponents;
			componentSearch.OnEscKey = _ =>
			{
				if (string.IsNullOrEmpty(componentSearch.Text))
					componentSearch.YieldKeyboardFocus();
				else
				{
					componentSearch.Text = "";
					PopulateComponents();
				}

				return true;
			};
			enabledComponentsOnly = widget.Get<CheckboxWidget>("ENABLED_COMPONENTS_ONLY");
			enabledComponentsOnly.IsChecked = () => showEnabledComponentsOnly;
			enabledComponentsOnly.OnClick = () =>
			{
				showEnabledComponentsOnly ^= true;
				PopulateComponents();
			};
			componentDetailTitle = widget.Get<LabelWidget>("COMPONENT_DETAIL_TITLE");
			componentDetailDescription = widget.Get<LabelWidget>("COMPONENT_DETAIL_DESCRIPTION");
			componentDetailImpact = widget.Get<LabelWidget>("COMPONENT_DETAIL_IMPACT");
			componentDetailScope = widget.Get<LabelWidget>("COMPONENT_DETAIL_SCOPE");
			componentDetailSource = widget.Get<LabelWidget>("COMPONENT_DETAIL_SOURCE");
			factionPreview = widget.Get<FactionPackPreviewWidget>("FACTION_PREVIEW");
			factionPreviewEmpty = widget.Get<LabelWidget>("FACTION_PREVIEW_EMPTY");
			componentKind = widget.Get<LabelWidget>("COMPONENT_KIND");
			gameplayFingerprint = widget.Get<LabelWidget>("GAMEPLAY_FINGERPRINT");
			presentationFingerprint = widget.Get<LabelWidget>("PRESENTATION_FINGERPRINT");
			packFolder = widget.Get<LabelWidget>("PACK_FOLDER");
			parameterEmpty = widget.Get<LabelWidget>("PARAMETER_EMPTY");
			replacementDetail = widget.Get<LabelWidget>("REPLACEMENT_DETAIL");
			packStatus = widget.Get<LabelWidget>("PACK_STATUS");

			componentPanel = widget.Get<ScrollPanelWidget>("COMPONENTS");
			componentTemplate = componentPanel.Get<CheckboxWidget>("COMPONENT_TEMPLATE");
			componentPanel.RemoveChild(componentTemplate);

			parameterPanel = widget.Get<ScrollPanelWidget>("PARAMETERS");
			parameterTemplate = parameterPanel.Get<ScrollItemWidget>("PARAMETER_TEMPLATE");
			parameterPanel.RemoveChild(parameterTemplate);

			replacementPanel = widget.Get<ScrollPanelWidget>("REPLACEMENTS");
			replacementTemplate = replacementPanel.Get<ScrollItemWidget>("REPLACEMENT_TEMPLATE");
			replacementPanel.RemoveChild(replacementTemplate);

			widget.Get<ButtonWidget>("REFRESH_PACKS_BUTTON").OnClick = () =>
			{
				ReloadPacks(workingPresentationPackId);
				SetPackStatus("Presentation packs refreshed.");
			};

			widget.Get<ButtonWidget>("BROWSE_ASSETS_BUTTON").OnClick = () => OpenAssetLibrary(selectedReplacement);

			var copyPackFolderButton = widget.Get<ButtonWidget>("COPY_PACK_FOLDER_BUTTON");
			copyPackFolderButton.GetText = () => FluentProvider.GetMessage(packFolderCopied ? PackFolderCopied : CopyPackFolder);
			copyPackFolderButton.OnClick = () =>
			{
				var pack = presentationPacks[workingPresentationPackId];
				Game.SetClipboardText(pack.RootPath ?? PresentationPackRegistry.PackDirectory(catalog.Mod));
				packFolderCopied = true;
				Game.RunAfterDelay(1500, () => packFolderCopied = false);
			};

			widget.Get<ButtonWidget>("CREATE_PACK_BUTTON").OnClick = CreatePack;
			var duplicatePackButton = widget.Get<ButtonWidget>("DUPLICATE_PACK_BUTTON");
			duplicatePackButton.IsDisabled = () => workingPresentationPackId == PresentationPackDefinition.Default.Id;
			duplicatePackButton.OnClick = DuplicatePack;
			var renamePackButton = widget.Get<ButtonWidget>("RENAME_PACK_BUTTON");
			renamePackButton.IsDisabled = () => workingPresentationPackId == PresentationPackDefinition.Default.Id;
			renamePackButton.OnClick = RenamePack;
			var deletePackButton = widget.Get<ButtonWidget>("DELETE_PACK_BUTTON");
			deletePackButton.IsDisabled = () => workingPresentationPackId == PresentationPackDefinition.Default.Id;
			deletePackButton.OnClick = DeletePack;

			var compareReplacementButton = widget.Get<ButtonWidget>("COMPARE_REPLACEMENT_BUTTON");
			compareReplacementButton.IsDisabled = () => selectedReplacement == null;
			compareReplacementButton.OnClick = () => OpenAssetLibrary(selectedReplacement);

			var removeReplacementButton = widget.Get<ButtonWidget>("REMOVE_REPLACEMENT_BUTTON");
			removeReplacementButton.IsDisabled = () => selectedReplacement == null ||
				workingPresentationPackId == PresentationPackDefinition.Default.Id;
			removeReplacementButton.OnClick = RemoveReplacement;

			var resetParametersButton = widget.Get<ButtonWidget>("RESET_PARAMETERS_BUTTON");
			resetParametersButton.IsDisabled = () => selectedComponentId == null ||
				catalog.Components[selectedComponentId].Parameters.Count == 0;
			resetParametersButton.OnClick = ResetSelectedParameters;

			var removeComponentPackButton = widget.Get<ButtonWidget>("REMOVE_COMPONENT_PACK_BUTTON");
			removeComponentPackButton.IsVisible = () => selectedComponentId != null &&
				catalog.Components.TryGetValue(selectedComponentId, out var selected) && selected.IsExternal;
			removeComponentPackButton.OnClick = RemoveSelectedCapabilityPack;

			widget.Get<ButtonWidget>("RESET_BUTTON").OnClick = () =>
			{
				workingProfileId = catalog.DefaultProfileId;
				workingCustomComponents = false;
				workingComponents = catalog.ComponentsForProfile(workingProfileId);
				workingParameterValues = catalog.DefaultParameterValues()
					.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
				workingPresentationPackId = "default";
				PopulateComponents();
				PopulateReplacements();
				RefreshSummary();
			};

			var disableAllButton = widget.Get<ButtonWidget>("DISABLE_ALL_BUTTON");
			disableAllButton.IsDisabled = () => workingComponents.Length == 0 &&
				workingPresentationPackId == PresentationPackDefinition.Default.Id;
			disableAllButton.OnClick = () =>
			{
				workingProfileId = catalog.DefaultProfileId;
				workingComponents = [];
				workingCustomComponents = catalog.ComponentsForProfile(workingProfileId).Length != 0;
				workingParameterValues = catalog.DefaultParameterValues()
					.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
				workingPresentationPackId = PresentationPackDefinition.Default.Id;
				selectedReplacement = null;
				PopulateComponents();
				PopulateReplacements();
				RefreshSummary();
			};

			applyButton = widget.Get<ButtonWidget>("APPLY_BUTTON");
			applyButton.IsDisabled = () => !HasChanges();
			applyButton.OnClick = ReviewChanges;

			widget.Get<ButtonWidget>("CANCEL_BUTTON").OnClick = Close;

			PopulateComponents();
			PopulateReplacements();
			RefreshSummary();
			_ = LoadAIStatusAsync();

			// Opt-in deterministic states for local visual regression capture.
			var captureFactionPack = Environment.GetEnvironmentVariable("OPENRA_AI_CAPTURE_FACTION_PACK");
			if (Environment.GetEnvironmentVariable("OPENRA_AI_CAPTURE_CAPABILITY_BROWSER") == "1")
				Game.RunAfterDelay(750, ImportCapabilityPack);
			else if (!string.IsNullOrWhiteSpace(captureFactionPack) &&
				catalog.Components.TryGetValue(captureFactionPack, out var capturedFaction) && capturedFaction.Faction != null)
				Game.RunAfterDelay(750, () =>
				{
					selectedComponentId = capturedFaction.Id;
					PopulateComponents();
					Game.RunAfterDelay(750, Game.TakeScreenshot);
				});
			else if (Environment.GetEnvironmentVariable("OPENRA_AI_CAPTURE_EXPERIENCE_REVIEW") == "1")
				Game.RunAfterDelay(750, () =>
				{
					workingComponents = catalog.ToggleComponent(workingComponents, "advanced-projectile-library", true);
					workingCustomComponents = true;
					ReviewChanges();
					Game.RunAfterDelay(750, Game.TakeScreenshot);
				});
		}

		void ShowProfileDropdown()
		{
			ScrollItemWidget SetupItem(ExperienceProfile profile, ScrollItemWidget template)
			{
				var item = ScrollItemWidget.Setup(template,
					() => !workingCustomComponents && workingProfileId == profile.Id,
					() =>
					{
						workingProfileId = profile.Id;
						workingCustomComponents = false;
						workingComponents = catalog.ComponentsForProfile(profile.Id);
						workingParameterValues = catalog.DefaultParameterValues()
							.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
						PopulateComponents();
						RefreshSummary();
					});

				item.Get<LabelWidget>("LABEL").GetText = () => profile.Title;
				return item;
			}

			profileDropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 440,
				catalog.Profiles.Values.OrderBy(p => p.Title), SetupItem);
		}

		void ShowPresentationDropdown()
		{
			ScrollItemWidget SetupItem(PresentationPackDefinition pack, ScrollItemWidget template)
			{
				var item = ScrollItemWidget.Setup(template,
					() => workingPresentationPackId == pack.Id,
					() =>
					{
						workingPresentationPackId = pack.Id;
						selectedReplacement = pack.Replaces.FirstOrDefault();
						PopulateReplacements();
						RefreshSummary();
					});

				item.Get<LabelWidget>("LABEL").GetText = () => pack.Title;
				return item;
			}

			presentationDropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 440,
				presentationPacks.Values.OrderBy(p => p.Title), SetupItem);
		}

		void PopulateComponents()
		{
			componentPanel.RemoveChildren();
			var matchingComponents = catalog.Components.Values
				.Where(component => !component.Hidden && ComponentMatchesFilter(component))
				.OrderBy(c => c.Category)
				.ThenBy(c => c.Title)
				.ToArray();
			foreach (var component in matchingComponents)
			{
				var captured = component;
				var checkbox = componentTemplate.Clone();
				checkbox.Id = "COMPONENT_" + captured.Id;
				checkbox.IsVisible = () => true;
				checkbox.GetText = () => $"{captured.Category} - {captured.Title}";
				checkbox.IsChecked = () => workingComponents.Contains(captured.Id);
				checkbox.IsHighlighted = () => selectedComponentId == captured.Id;
				checkbox.OnMouseDown = _ =>
				{
					selectedComponentId = captured.Id;
					RefreshComponentDetail();
					PopulateParameters();
				};
				checkbox.OnClick = () =>
				{
					workingComponents = catalog.ToggleComponent(
						workingComponents, captured.Id, !workingComponents.Contains(captured.Id));
					workingCustomComponents = true;
					PopulateComponents();
					RefreshSummary();
				};

				checkbox.GetTooltipText = () => captured.Title;
				checkbox.GetTooltipDesc = () =>
					$"{captured.Description}\n\nDoes: {captured.Effects}\nTradeoff: {captured.Tradeoffs}\n" +
					$"Where: {captured.Scope}\n\nSource: {captured.Source}\nLicense: {captured.License}";
				componentPanel.AddChild(checkbox);
			}

			componentPanel.Layout.AdjustChildren();
			componentPanel.ScrollToTop();
			RefreshComponentSummary(matchingComponents.Length);
			RefreshComponentDetail();
			PopulateParameters();
		}

		bool ComponentMatchesFilter(ExperienceComponent component)
		{
			if (component.Hidden)
				return false;

			if (showEnabledComponentsOnly && !workingComponents.Contains(component.Id))
				return false;

			var filter = componentSearch.Text.Trim();
			return filter.Length == 0 || new[]
			{
				component.Id, component.Title, component.Category, component.Description, component.Source
			}.Any(value => value.Contains(filter, StringComparison.OrdinalIgnoreCase));
		}

		void RefreshComponentSummary(int? visibleCount = null)
		{
			var loadout = workingCustomComponents ? "Custom loadout" : "Preset loadout";
			var selectableComponents = catalog.Components.Values.Where(component => !component.Hidden).ToArray();
			var enabled = workingComponents.Count(id => catalog.Components.TryGetValue(id, out var component) && !component.Hidden);
			var visible = visibleCount ?? selectableComponents.Count(ComponentMatchesFilter);
			var filtered = componentSearch.Text.Trim().Length > 0 || showEnabledComponentsOnly;
			componentSummary.GetText = () => filtered ?
				$"{loadout} - {enabled} enabled - {visible} matching" :
				$"{loadout} - {enabled} of {selectableComponents.Length} enabled";
		}

		void PopulateParameters()
		{
			parameterPanel.RemoveChildren();
			if (selectedComponentId == null || !catalog.Components.TryGetValue(selectedComponentId, out var component))
			{
				parameterEmpty.IsVisible = () => true;
				return;
			}

			parameterEmpty.IsVisible = () => component.Parameters.Count == 0;
			foreach (var parameter in component.Parameters.Values.OrderBy(p => p.Group).ThenBy(p => p.Title))
			{
				var captured = parameter;
				var key = ExperienceCatalog.ParameterKey(component.Id, captured.Id);
				var row = parameterTemplate.Clone();
				row.Id = "PARAMETER_" + captured.Id;
				row.IsVisible = () => true;

				row.Get<LabelWidget>("TITLE").GetText = () => $"{captured.Group} - {captured.Title}";
				var description = row.Get<LabelWithTooltipWidget>("DESCRIPTION");
				WidgetUtils.TruncateLabelToTooltip(description, captured.Description);
				description.GetTooltipText = () => captured.Description;

				var slider = row.Get<SliderWidget>("INTEGER");
				slider.MinimumValue = captured.Minimum;
				slider.MaximumValue = captured.Maximum;
				slider.Ticks = (captured.Maximum - captured.Minimum) / captured.Step + 1;
				slider.GetValue = () => int.Parse(workingParameterValues[key], CultureInfo.InvariantCulture);
				slider.OnChange += value =>
				{
					workingParameterValues[key] = captured.Normalize(((int)Math.Round(value)).ToStringInvariant());
					workingCustomComponents = true;
					RefreshSummary();
				};
				slider.IsVisible = () => captured.Type == ExperienceParameterType.Integer;
				slider.IsDisabled = () => !workingComponents.Contains(component.Id);

				var valueLabel = row.Get<LabelWidget>("VALUE");
				valueLabel.IsVisible = () => captured.Type == ExperienceParameterType.Integer;
				valueLabel.GetText = () => string.IsNullOrEmpty(captured.Unit) ? workingParameterValues[key] :
					$"{workingParameterValues[key]} {captured.Unit}";

				var toggle = row.Get<CheckboxWidget>("BOOLEAN");
				toggle.IsVisible = () => captured.Type == ExperienceParameterType.Boolean;
				toggle.IsDisabled = () => !workingComponents.Contains(component.Id);
				toggle.IsChecked = () => bool.Parse(workingParameterValues[key]);
				toggle.GetText = () => bool.Parse(workingParameterValues[key]) ? "Enabled" : "Disabled";
				toggle.OnClick = () =>
				{
					workingParameterValues[key] = captured.Normalize(
						(!bool.Parse(workingParameterValues[key])).ToString());
					workingCustomComponents = true;
					RefreshSummary();
				};

				var choice = row.Get<DropDownButtonWidget>("CHOICE");
				choice.IsVisible = () => captured.Type == ExperienceParameterType.Choice;
				choice.IsDisabled = () => !workingComponents.Contains(component.Id);
				choice.GetText = () => workingParameterValues[key];
				choice.OnMouseDown = _ => ShowParameterChoice(choice, captured, key);

				parameterPanel.AddChild(row);
			}

			parameterPanel.Layout.AdjustChildren();
			parameterPanel.ScrollToTop();
		}

		void ShowParameterChoice(DropDownButtonWidget dropdown, ExperienceParameter parameter, string key)
		{
			ScrollItemWidget SetupItem(string option, ScrollItemWidget template)
			{
				var item = ScrollItemWidget.Setup(template,
					() => workingParameterValues[key] == option,
					() =>
					{
						workingParameterValues[key] = option;
						workingCustomComponents = true;
						RefreshSummary();
					});
				item.Get<LabelWidget>("LABEL").GetText = () => option;
				return item;
			}

			dropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", dropdown.Bounds.Width,
				parameter.Options, SetupItem);
		}

		void ResetSelectedParameters()
		{
			if (selectedComponentId == null || !catalog.Components.TryGetValue(selectedComponentId, out var component))
				return;

			foreach (var parameter in component.Parameters.Values)
				workingParameterValues[ExperienceCatalog.ParameterKey(component.Id, parameter.Id)] = parameter.Default;

			workingCustomComponents = true;
			PopulateParameters();
			RefreshSummary();
		}

		void RefreshComponentDetail()
		{
			if (selectedComponentId == null || !catalog.Components.TryGetValue(selectedComponentId, out var component))
			{
				factionPreview.Clear();
				factionPreview.IsVisible = () => false;
				factionPreviewEmpty.IsVisible = () => true;
				factionPreviewEmpty.GetText = () => "SELECT A PACK";
				componentKind.GetText = () => "";
				componentDetailTitle.GetText = () => "Select a gameplay module";
				componentDetailDescription.GetText = () => "Choose a module above to inspect its behavior and provenance.";
				componentDetailImpact.GetText = () => "";
				componentDetailScope.GetText = () => "";
				componentDetailSource.GetText = () => "";
				return;
			}

			var isFaction = component.Faction != null;
			var previewLoaded = isFaction && factionPreview.Update(modData.DefaultFileSystem, component.Faction.Preview);
			factionPreview.IsVisible = () => previewLoaded;
			factionPreviewEmpty.IsVisible = () => !previewLoaded;
			factionPreviewEmpty.GetText = () => isFaction ? "PREVIEW UNAVAILABLE" : "GAMEPLAY MODULE";
			componentKind.GetText = () => isFaction ?
				$"{component.Faction.Side.ToUpperInvariant()} FACTION PACK" : "REUSABLE CAPABILITY";

			var enabled = workingComponents.Contains(component.Id) ? "Enabled" : "Disabled";
			var dependencies = component.Dependencies.Length == 0 ? "No dependencies" :
				"Requires " + component.Dependencies.Select(id => catalog.Components[id].Title).JoinWith(", ");
			var conflicts = component.Conflicts.Length == 0 ? "No conflicts" :
				"Conflicts with " + component.Conflicts.Select(id => catalog.Components[id].Title).JoinWith(", ");
			var fileCounts = new (string Type, int Count)[]
			{
				("rule", component.Rules.Length), ("weapon", component.Weapons.Length),
				("sequence", component.Sequences.Length), ("cursor", component.Cursors.Length),
				("interface", component.Chrome.Length), ("voice", component.Voices.Length),
				("notification", component.Notifications.Length), ("music", component.Music.Length)
			}.Where(v => v.Count > 0).Select(v => $"{v.Count} {v.Type}{(v.Count == 1 ? "" : "s")}").ToArray();
			var changes = fileCounts.Length == 0 ? "Native runtime behavior" : "Loads " + fileCounts.JoinWith(", ");

			componentDetailTitle.GetText = () => component.Title;
			componentDetailDescription.GetText = () => WidgetUtils.WrapText(component.Description,
				componentDetailDescription.Bounds.Width, Game.Renderer.Fonts[componentDetailDescription.Font]);
			var factionStatus = isFaction ?
				$"{component.Faction.InternalName} | {component.Faction.Doctrine} | {component.Faction.ActorCount} roster actors\n" : "";
			componentDetailImpact.GetText = () => WidgetUtils.WrapText(
				$"{enabled} | {dependencies} | {conflicts}\n{factionStatus}DOES: {component.Effects}",
				componentDetailImpact.Bounds.Width, Game.Renderer.Fonts[componentDetailImpact.Font]);
			componentDetailScope.GetText = () => WidgetUtils.WrapText($"WHERE: {component.Scope}",
				componentDetailScope.Bounds.Width, Game.Renderer.Fonts[componentDetailScope.Font]);
			componentDetailSource.GetText = () => WidgetUtils.WrapText(
				$"Version {component.Version} | {changes} | Source: {component.Source} | License: {component.License}",
				componentDetailSource.Bounds.Width, Game.Renderer.Fonts[componentDetailSource.Font]);
		}

		void PopulateReplacements()
		{
			replacementPanel.RemoveChildren();
			var pack = presentationPacks[workingPresentationPackId];
			if (selectedReplacement != null && !pack.Replaces.Contains(selectedReplacement))
				selectedReplacement = null;

			selectedReplacement ??= pack.Replaces.FirstOrDefault();
			foreach (var replacement in pack.Replaces.Order())
			{
				var captured = replacement;
				var item = ScrollItemWidget.Setup(replacementTemplate,
					() => selectedReplacement == captured,
					() =>
					{
						selectedReplacement = captured;
						RefreshReplacementDetail();
					});
				item.Id = "REPLACEMENT_" + captured.Replace('/', '_');
				var label = item.Get<LabelWithTooltipWidget>("TITLE");
				WidgetUtils.TruncateLabelToTooltip(label, captured);
				label.GetTooltipText = () => captured;
				replacementPanel.AddChild(item);
			}

			replacementPanel.Layout.AdjustChildren();
			RefreshReplacementDetail();
		}

		void RefreshReplacementDetail()
		{
			var pack = presentationPacks[workingPresentationPackId];
			if (selectedReplacement == null)
			{
				replacementDetail.GetText = () => pack.Id == PresentationPackDefinition.Default.Id ?
					"Create a pack to start replacing images, audio, video, palettes, and cursors." :
					"No replacements yet. Open the Asset Library and choose an original target.";
				return;
			}

			var original = modData.DefaultFileSystem.Exists(selectedReplacement) ? "Original target found" : "Original target missing";
			var replacementPath = Path.Combine(pack.AssetsPath,
				selectedReplacement.Replace('/', Path.DirectorySeparatorChar));
			var size = File.Exists(replacementPath) ? new FileInfo(replacementPath).Length : 0;
			replacementDetail.GetText = () => WidgetUtils.WrapText(
				$"{selectedReplacement}\n{original} | Replacement {FormatBytes(size)}",
				replacementDetail.Bounds.Width, Game.Renderer.Fonts[replacementDetail.Font]);
		}

		void ReloadPacks(string preferredId)
		{
			presentationPacks = PresentationPackRegistry.Discover(catalog.Mod);
			workingPresentationPackId = presentationPacks.ContainsKey(preferredId) ? preferredId : PresentationPackDefinition.Default.Id;
			selectedReplacement = presentationPacks[workingPresentationPackId].Replaces.FirstOrDefault();
			PopulateReplacements();
			RefreshSummary();
		}

		void CreatePack()
		{
			ConfirmationDialogs.TextInputPrompt(modData, CreatePackTitle, CreatePackPrompt, "My presentation pack",
				onAccept: title => RunPackOperation(() => PresentationPackRegistry.Create(catalog.Mod, title), "Pack created."),
				inputValidator: title => !string.IsNullOrWhiteSpace(title));
		}

		void ImportCapabilityPack()
		{
			var capture = Environment.GetEnvironmentVariable("OPENRA_AI_CAPTURE_CAPABILITY_BROWSER") == "1";
			var initialDirectory = capture ? CapabilityPackRegistry.PackDirectory(catalog.Mod) : null;
			Directory.CreateDirectory(CapabilityPackRegistry.PackDirectory(catalog.Mod));
			Action<string> install = sourcePath =>
			{
				try
				{
					CapabilityPackRegistry.Import(catalog.Mod, sourcePath);
					SaveAndRestart();
				}
				catch (Exception e) { SetPackStatus(e.Message); }
			};
			Action cancel = () => { };
			Ui.OpenWindow("CAPABILITY_PACK_BROWSER", new WidgetArgs
			{
				{ "initialDirectory", initialDirectory },
				{ "onSelected", install },
				{ "onCancel", cancel }
			});
		}

		void RemoveSelectedCapabilityPack()
		{
			if (selectedComponentId == null || !catalog.Components.TryGetValue(selectedComponentId, out var component) ||
				!component.IsExternal)
				return;

			ConfirmationDialogs.ButtonPrompt(modData, RemoveModuleTitle, RemoveModulePrompt,
				textArguments: ["module", component.Title],
				onConfirm: () =>
				{
					try
					{
						workingComponents = workingComponents.Where(id => id != component.Id).ToImmutableArray();
						workingCustomComponents = true;
						CapabilityPackRegistry.Delete(catalog.Mod, component.PackageId);
						SaveAndRestart();
					}
					catch (Exception e) { SetPackStatus(e.Message); }
				},
				confirmText: RemoveModuleAccept,
				onCancel: () => { });
		}

		void DuplicatePack()
		{
			var pack = presentationPacks[workingPresentationPackId];
			ConfirmationDialogs.TextInputPrompt(modData, DuplicatePackTitle, DuplicatePackPrompt, pack.Title + " Copy",
				onAccept: title => RunPackOperation(
					() => PresentationPackRegistry.Duplicate(catalog.Mod, pack.Id, title), "Pack duplicated."),
				inputValidator: title => !string.IsNullOrWhiteSpace(title));
		}

		void RenamePack()
		{
			var pack = presentationPacks[workingPresentationPackId];
			ConfirmationDialogs.TextInputPrompt(modData, RenamePackTitle, RenamePackPrompt, pack.Title,
				onAccept: title => RunPackOperation(
					() => PresentationPackRegistry.Rename(catalog.Mod, pack.Id, title), "Pack renamed."),
				inputValidator: title => !string.IsNullOrWhiteSpace(title) && title.Trim() != pack.Title);
		}

		void DeletePack()
		{
			var pack = presentationPacks[workingPresentationPackId];
			ConfirmationDialogs.ButtonPrompt(modData, DeletePackTitle, DeletePackPrompt,
				textArguments: ["pack", pack.Title],
				onConfirm: () =>
				{
					try
					{
						PresentationPackRegistry.Delete(catalog.Mod, pack.Id);
						ReloadPacks(PresentationPackDefinition.Default.Id);
						SetPackStatus("Pack deleted.");
					}
					catch (Exception e) { SetPackStatus(e.Message); }
				},
				confirmText: DeletePackAccept,
				onCancel: () => { });
		}

		void RemoveReplacement()
		{
			var pack = presentationPacks[workingPresentationPackId];
			var target = selectedReplacement;
			ConfirmationDialogs.ButtonPrompt(modData, RemoveReplacementTitle, RemoveReplacementPrompt,
				textArguments: ["asset", target],
				onConfirm: () =>
				{
					try
					{
						PresentationPackRegistry.RemoveReplacement(catalog.Mod, pack.Id, target);
						ReloadPacks(pack.Id);
						SetPackStatus("Replacement removed.");
					}
					catch (Exception e) { SetPackStatus(e.Message); }
				},
				confirmText: RemoveReplacementAccept,
				onCancel: () => { });
		}

		void RunPackOperation(Func<PresentationPackDefinition> operation, string success)
		{
			try
			{
				var pack = operation();
				ReloadPacks(pack.Id);
				SetPackStatus(success);
			}
			catch (Exception e) { SetPackStatus(e.Message); }
		}

		void OpenAssetLibrary(string target)
		{
			Game.OpenWindow("ASSETBROWSER_PANEL", new WidgetArgs
			{
				{ "onExit", () => { } },
				{ "presentationPackId", workingPresentationPackId },
				{ "comparisonTarget", target },
				{ "initialAsset", target },
				{ "onPackChanged", (Action)(() => ReloadPacks(workingPresentationPackId)) },
			});
		}

		void SetPackStatus(string status)
		{
			packStatus.GetText = () => status;
		}

		void RefreshSummary()
		{
			var profile = catalog.Profiles[workingProfileId];
			profileDescription.GetText = () => WidgetUtils.WrapText(profile.Description,
				profileDescription.Bounds.Width, Game.Renderer.Fonts[profileDescription.Font]);
			RefreshComponentSummary();

			var pack = presentationPacks[workingPresentationPackId];
			presentationDescription.GetText = () => WidgetUtils.WrapText(
				$"{pack.Description}\n{pack.Author} | {pack.License}",
				presentationDescription.Bounds.Width, Game.Renderer.Fonts[presentationDescription.Font]);
			gameplayFingerprint.GetText = () =>
				$"Gameplay: {Short(catalog.ComputeGameplayFingerprint(workingComponents, workingParameterValues))}";
			presentationFingerprint.GetText = () => $"Presentation: {Short(pack.Fingerprint)}";
			packFolder.GetText = () => WidgetUtils.WrapText(
				$"Pack folder: {pack.RootPath ?? PresentationPackRegistry.PackDirectory(catalog.Mod)}",
				packFolder.Bounds.Width, Game.Renderer.Fonts[packFolder.Font]);
		}

		string ProfileTitle()
		{
			var title = catalog.Profiles[workingProfileId].Title;
			return workingCustomComponents ? title + " (custom)" : title;
		}

		bool HasChanges()
		{
			var savedProfile = catalog.Profiles.ContainsKey(settings.Profile) ? settings.Profile : catalog.DefaultProfileId;
			var savedPack = presentationPacks.ContainsKey(settings.PresentationPack) ? settings.PresentationPack : "default";
			var savedComponents = settings.UseCustomComponents ?
				ParseComponentSettings(settings.EnabledComponents) : catalog.ComponentsForProfile(savedProfile);
			var parametersChanged = catalog.SerializeParameterSettings(workingParameterValues) !=
				catalog.SerializeParameterSettings(catalog.ParseParameterSettings(settings.ParameterValues));

			return savedProfile != workingProfileId || settings.UseCustomComponents != workingCustomComponents ||
				savedPack != workingPresentationPackId || parametersChanged ||
				!savedComponents.SequenceEqual(workingComponents.Order());
		}

		void ReviewChanges()
		{
			var savedProfile = catalog.Profiles.ContainsKey(settings.Profile) ? settings.Profile : catalog.DefaultProfileId;
			var savedComponents = settings.UseCustomComponents ?
				ParseComponentSettings(settings.EnabledComponents) : catalog.ComponentsForProfile(savedProfile);
			var savedParameters = catalog.ParseParameterSettings(settings.ParameterValues);
			var savedPresentation = presentationPacks.ContainsKey(settings.PresentationPack) ?
				settings.PresentationPack : PresentationPackDefinition.Default.Id;
			var review = new ExperienceReviewModel(catalog, savedComponents, workingComponents,
				savedParameters, workingParameterValues, savedPresentation, workingPresentationPackId);
			var gameplayFingerprint = catalog.ComputeGameplayFingerprint(workingComponents, workingParameterValues);

			Game.OpenWindow("EXPERIENCE_REVIEW_PANEL", new WidgetArgs
			{
				{ "review", review },
				{ "profileTitle", ProfileTitle() },
				{ "presentationTitle", presentationPacks[workingPresentationPackId].Title },
				{ "gameplayFingerprint", gameplayFingerprint },
				{ "aiStatus", aiStatus },
				{ "onConfirm", (Action)ApplyAndRestart },
				{ "onExit", (Action)(() => { }) }
			});
		}

		async System.Threading.Tasks.Task LoadAIStatusAsync()
		{
			try
			{
				var baseUri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_CONSOLE_URL", "http://127.0.0.1:8787/");
				using var document = await OpenRAAILocalClient.GetAsync(baseUri, "v1/state");
				var config = document.RootElement.GetProperty("config");
				var enabled = config.GetProperty("companion_enabled").GetBoolean() ? "ON" : "OFF";
				var provider = config.TryGetProperty("model_provider", out var providerValue) ?
					providerValue.GetString() ?? "unknown" : "unknown";
				var vision = config.TryGetProperty("vision_model", out var visionValue) ?
					visionValue.GetString() ?? "not configured" : "not configured";
				Game.RunAfterTick(() => aiStatus = $"AI Assistant: {enabled} | Provider: {provider} | Vision: {vision}");
			}
			catch { }
		}

		void ApplyAndRestart()
		{
			SaveAndRestart();
		}

		void SaveAndRestart()
		{
			settings.Profile = workingProfileId;
			settings.UseCustomComponents = workingCustomComponents;
			settings.EnabledComponents = workingCustomComponents ? workingComponents.Order().JoinWith(",") : "";
			settings.ParameterValues = catalog.SerializeParameterSettings(workingParameterValues);
			settings.PresentationPack = workingPresentationPackId;
			settings.Save();

			if (Game.ExternalMods.TryGetValue(ExternalMod.MakeKey(modData.Manifest), out var external))
				Game.SwitchToExternalMod(external, null, Close);
			else
				Close();
		}

		ImmutableArray<string> ParseComponentSettings(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return [];

			return value.Split(',').Select(v => v.Trim()).Where(catalog.Components.ContainsKey).Order().ToImmutableArray();
		}

		void Close()
		{
			Ui.CloseWindow();
			onExit();
		}

		static string Short(string fingerprint)
		{
			return string.IsNullOrEmpty(fingerprint) ? "default" : fingerprint[..Math.Min(12, fingerprint.Length)];
		}

		static string FormatBytes(long bytes)
		{
			if (bytes >= 1024 * 1024)
				return $"{bytes / (1024d * 1024d):0.0} MB";
			if (bytes >= 1024)
				return $"{bytes / 1024d:0.0} KB";

			return $"{bytes} bytes";
		}
	}
}
