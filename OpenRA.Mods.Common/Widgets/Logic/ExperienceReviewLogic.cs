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
using OpenRA.Mods.Common.Experience;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class ExperienceReviewLogic : ChromeLogic
	{
		readonly ScrollPanelWidget changePanel;
		readonly ScrollItemWidget changeTemplate;
		readonly ScrollPanelWidget modulePanel;
		readonly ScrollItemWidget moduleTemplate;

		[ObjectCreator.UseCtor]
		public ExperienceReviewLogic(Widget widget, ExperienceReviewModel review,
			string profileTitle, string presentationTitle, string gameplayFingerprint,
			string aiStatus, Action onConfirm, Action onExit)
		{
			widget.Get<LabelWidget>("SUMMARY").GetText = () =>
				$"{review.ChangeCount} pending change{(review.ChangeCount == 1 ? "" : "s")} | " +
				$"{review.EnabledComponents.Length} of {review.RegisteredCount} modules enabled";
			widget.Get<LabelWidget>("PROFILE").GetText = () => $"Loadout: {profileTitle}";
			widget.Get<LabelWidget>("PRESENTATION").GetText = () => $"Presentation: {presentationTitle}";
			widget.Get<LabelWidget>("FINGERPRINT").GetText = () => $"Gameplay fingerprint: {Short(gameplayFingerprint)}";
			widget.Get<LabelWidget>("EXTERNAL_STATUS").GetText = () => aiStatus;

			changePanel = widget.Get<ScrollPanelWidget>("CHANGES");
			changeTemplate = changePanel.Get<ScrollItemWidget>("CHANGE_TEMPLATE");
			changePanel.RemoveChild(changeTemplate);
			PopulateChanges(review);

			modulePanel = widget.Get<ScrollPanelWidget>("MODULES");
			moduleTemplate = modulePanel.Get<ScrollItemWidget>("MODULE_TEMPLATE");
			modulePanel.RemoveChild(moduleTemplate);
			PopulateModules(review);

			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				Ui.CloseWindow();
				onExit();
			};
			widget.Get<ButtonWidget>("CONFIRM_BUTTON").OnClick = () =>
			{
				Ui.CloseWindow();
				onConfirm();
			};
		}

		void PopulateChanges(ExperienceReviewModel review)
		{
			var changes = new List<(string State, string Title, string Description)>();
			changes.AddRange(review.NewlyEnabledComponents.Select(component =>
			{
				var requiredBy = review.RequiredBy[component.Id];
				var reason = requiredBy.Length == 0 ? "" :
					$" Automatically required by {requiredBy.Select(c => c.Title).JoinWith(", ")}.";
				return (requiredBy.Length == 0 ? "ENABLE" : "ENABLE*", component.Title, component.Effects + reason);
			}));
			changes.AddRange(review.NewlyDisabledComponents.Select(component =>
			{
				var conflictedBy = review.ConflictedBy[component.Id];
				var reason = conflictedBy.Length == 0 ? "" :
					$" Automatically removed because it conflicts with {conflictedBy.Select(c => c.Title).JoinWith(", ")}.";
				return (conflictedBy.Length == 0 ? "DISABLE" : "DISABLE*", component.Title,
					$"Stops loading this module. Previous scope: {component.Scope}.{reason}");
			}));
			changes.AddRange(review.ParameterChanges.Select(change =>
				("PARAMETER", $"{change.Component.Title} / {change.Parameter.Title}",
					$"{DisplayValue(change.Parameter, change.PreviousValue)} -> {DisplayValue(change.Parameter, change.Value)}. " +
					change.Parameter.Description)));
			if (review.PresentationChanged)
				changes.Add(("PRESENTATION", "Presentation pack",
					$"Changes from {review.PreviousPresentation} to {review.Presentation}. This affects presentation assets, not simulation rules."));
			if (changes.Count == 0)
				changes.Add(("NO CHANGE", "Nothing will be changed", "Return to the manager to choose a different loadout."));

			foreach (var change in changes)
			{
				var row = changeTemplate.Clone();
				row.IsVisible = () => true;
				row.Get<LabelWidget>("STATE").GetText = () => change.State;
				row.Get<LabelWidget>("TITLE").GetText = () => change.Title;
				var description = row.Get<LabelWidget>("DESCRIPTION");
				description.GetText = () => WidgetUtils.WrapText(change.Description,
					description.Bounds.Width, Game.Renderer.Fonts[description.Font]);
				changePanel.AddChild(row);
			}

			changePanel.Layout.AdjustChildren();
		}

		void PopulateModules(ExperienceReviewModel review)
		{
			var enabled = review.EnabledComponents.Select(c => c.Id).ToHashSet();
			var enabledChanges = review.NewlyEnabledComponents.Select(c => c.Id).ToHashSet();
			var disabledChanges = review.NewlyDisabledComponents.Select(c => c.Id).ToHashSet();
			foreach (var component in review.EnabledComponents.Concat(review.DisabledComponents)
				.OrderBy(c => c.Category).ThenBy(c => c.Title))
			{
				var state = enabledChanges.Contains(component.Id) ? "+ ON" :
					disabledChanges.Contains(component.Id) ? "- OFF" :
					enabled.Contains(component.Id) ? "ON" : "OFF";
				var row = moduleTemplate.Clone();
				row.IsVisible = () => true;
				row.Get<LabelWidget>("STATE").GetText = () => state;
				row.Get<LabelWidget>("TITLE").GetText = () => $"{component.Category} / {component.Title}";
				var description = row.Get<LabelWidget>("DESCRIPTION");
				var dependencies = component.Dependencies.Length == 0 ? "none" : component.Dependencies
					.Select(id => review.ComponentsById.TryGetValue(id, out var dependency) ? dependency.Title : id)
					.JoinWith(", ");
				var conflicts = component.Conflicts.Length == 0 ? "none" : component.Conflicts
					.Select(id => review.ComponentsById.TryGetValue(id, out var conflict) ? conflict.Title : id)
					.JoinWith(", ");
				var parameters = component.Parameters.Values.OrderBy(p => p.Group).ThenBy(p => p.Title)
					.Select(parameter =>
					{
						var key = ExperienceCatalog.ParameterKey(component.Id, parameter.Id);
						var value = review.ParameterValues.TryGetValue(key, out var configured) ? configured : parameter.Default;
						return $"{parameter.Title}={DisplayValue(parameter, value)}";
					}).ToArray();
				var settings = parameters.Length == 0 ? "" : $"  SETTINGS: {parameters.JoinWith(", ")}.";
				description.GetText = () => WidgetUtils.WrapText(
					$"DOES: {component.Effects}  WHERE: {component.Scope}  TRADEOFF: {component.Tradeoffs}" +
					$"  REQUIRES: {dependencies}. CONFLICTS: {conflicts}.{settings}",
					description.Bounds.Width, Game.Renderer.Fonts[description.Font]);
				modulePanel.AddChild(row);
			}

			modulePanel.Layout.AdjustChildren();
		}

		static string DisplayValue(ExperienceParameter parameter, string value)
		{
			return string.IsNullOrEmpty(parameter.Unit) ? value : $"{value} {parameter.Unit}";
		}

		static string Short(string fingerprint)
		{
			return string.IsNullOrEmpty(fingerprint) || fingerprint.Length <= 12 ? fingerprint : fingerprint[..12];
		}
	}
}
