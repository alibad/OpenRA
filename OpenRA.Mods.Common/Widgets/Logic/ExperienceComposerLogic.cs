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
using System.Linq;
using OpenRA.Mods.Common.Experience;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class ExperienceComposerLogic : ChromeLogic
	{
		readonly Action onExit;
		readonly ModData modData;
		readonly ExperienceCatalog catalog;
		readonly ExperienceSettings settings;
		readonly ScrollPanelWidget componentPanel;
		readonly CheckboxWidget componentTemplate;
		readonly DropDownButtonWidget profileDropdown;
		readonly DropDownButtonWidget presentationDropdown;
		readonly LabelWidget profileDescription;
		readonly LabelWidget presentationDescription;
		readonly LabelWidget componentSummary;
		readonly LabelWidget gameplayFingerprint;
		readonly LabelWidget presentationFingerprint;
		readonly LabelWidget packFolder;
		readonly ButtonWidget applyButton;

		IReadOnlyDictionary<string, PresentationPackDefinition> presentationPacks;
		ImmutableArray<string> workingComponents;
		string workingProfileId;
		string workingPresentationPackId;
		bool workingCustomComponents;

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
			presentationPacks = PresentationPackRegistry.Discover(catalog.Mod);
			workingPresentationPackId = presentationPacks.ContainsKey(settings.PresentationPack) ? settings.PresentationPack : "default";

			profileDropdown = widget.Get<DropDownButtonWidget>("PROFILE_DROPDOWN");
			profileDropdown.OnClick = ShowProfileDropdown;
			profileDropdown.GetText = ProfileTitle;

			presentationDropdown = widget.Get<DropDownButtonWidget>("PRESENTATION_DROPDOWN");
			presentationDropdown.OnClick = ShowPresentationDropdown;
			presentationDropdown.GetText = () => presentationPacks[workingPresentationPackId].Title;

			profileDescription = widget.Get<LabelWidget>("PROFILE_DESCRIPTION");
			presentationDescription = widget.Get<LabelWidget>("PRESENTATION_DESCRIPTION");
			componentSummary = widget.Get<LabelWidget>("COMPONENT_SUMMARY");
			gameplayFingerprint = widget.Get<LabelWidget>("GAMEPLAY_FINGERPRINT");
			presentationFingerprint = widget.Get<LabelWidget>("PRESENTATION_FINGERPRINT");
			packFolder = widget.Get<LabelWidget>("PACK_FOLDER");

			componentPanel = widget.Get<ScrollPanelWidget>("COMPONENTS");
			componentTemplate = componentPanel.Get<CheckboxWidget>("COMPONENT_TEMPLATE");
			componentPanel.RemoveChild(componentTemplate);

			widget.Get<ButtonWidget>("REFRESH_PACKS_BUTTON").OnClick = () =>
			{
				presentationPacks = PresentationPackRegistry.Discover(catalog.Mod);
				if (!presentationPacks.ContainsKey(workingPresentationPackId))
					workingPresentationPackId = "default";

				RefreshSummary();
			};

			widget.Get<ButtonWidget>("RESET_BUTTON").OnClick = () =>
			{
				workingProfileId = catalog.DefaultProfileId;
				workingCustomComponents = false;
				workingComponents = catalog.ComponentsForProfile(workingProfileId);
				workingPresentationPackId = "default";
				PopulateComponents();
				RefreshSummary();
			};

			applyButton = widget.Get<ButtonWidget>("APPLY_BUTTON");
			applyButton.IsDisabled = () => !HasChanges();
			applyButton.OnClick = ApplyAndRestart;

			widget.Get<ButtonWidget>("CANCEL_BUTTON").OnClick = Close;

			PopulateComponents();
			RefreshSummary();
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
			foreach (var component in catalog.Components.Values.OrderBy(c => c.Category).ThenBy(c => c.Title))
			{
				var captured = component;
				var checkbox = componentTemplate.Clone();
				checkbox.Id = "COMPONENT_" + captured.Id;
				checkbox.IsVisible = () => true;
				checkbox.GetText = () => $"{captured.Category} — {captured.Title}";
				checkbox.IsChecked = () => workingComponents.Contains(captured.Id);
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
					$"{captured.Description}\n\nSource: {captured.Source}\nLicense: {captured.License}";
				componentPanel.AddChild(checkbox);
			}

			componentPanel.Layout.AdjustChildren();
		}

		void RefreshSummary()
		{
			var profile = catalog.Profiles[workingProfileId];
			profileDescription.GetText = () => WidgetUtils.WrapText(profile.Description,
				profileDescription.Bounds.Width, Game.Renderer.Fonts[profileDescription.Font]);
			componentSummary.GetText = () => workingCustomComponents ?
				$"Custom selection · {workingComponents.Length} components" :
				$"Preset selection · {workingComponents.Length} components";

			var pack = presentationPacks[workingPresentationPackId];
			presentationDescription.GetText = () => WidgetUtils.WrapText(
				$"{pack.Description}\n{pack.Author} · {pack.License}",
				presentationDescription.Bounds.Width, Game.Renderer.Fonts[presentationDescription.Font]);
			gameplayFingerprint.GetText = () => $"Gameplay: {Short(catalog.ComputeGameplayFingerprint(workingComponents))}";
			presentationFingerprint.GetText = () => $"Presentation: {Short(pack.Fingerprint)}";
			packFolder.GetText = () => WidgetUtils.WrapText(
				$"Pack folder: {PresentationPackRegistry.PackDirectory(catalog.Mod)}",
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

			return savedProfile != workingProfileId || settings.UseCustomComponents != workingCustomComponents ||
				savedPack != workingPresentationPackId || !savedComponents.SequenceEqual(workingComponents.Order());
		}

		void ApplyAndRestart()
		{
			settings.Profile = workingProfileId;
			settings.UseCustomComponents = workingCustomComponents;
			settings.EnabledComponents = workingCustomComponents ? workingComponents.Order().JoinWith(",") : "";
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
	}
}
