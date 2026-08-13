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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace OpenRA.Mods.Common.Experience
{
	public sealed class ExperienceParameterChange
	{
		public readonly ExperienceComponent Component;
		public readonly ExperienceParameter Parameter;
		public readonly string PreviousValue;
		public readonly string Value;

		public ExperienceParameterChange(ExperienceComponent component, ExperienceParameter parameter,
			string previousValue, string value)
		{
			Component = component;
			Parameter = parameter;
			PreviousValue = previousValue;
			Value = value;
		}
	}

	public sealed class ExperienceReviewModel
	{
		public readonly IReadOnlyDictionary<string, ExperienceComponent> ComponentsById;
		public readonly ImmutableArray<ExperienceComponent> EnabledComponents;
		public readonly ImmutableArray<ExperienceComponent> DisabledComponents;
		public readonly ImmutableArray<ExperienceComponent> NewlyEnabledComponents;
		public readonly ImmutableArray<ExperienceComponent> NewlyDisabledComponents;
		public readonly ImmutableArray<ExperienceParameterChange> ParameterChanges;
		public readonly IReadOnlyDictionary<string, ImmutableArray<ExperienceComponent>> RequiredBy;
		public readonly IReadOnlyDictionary<string, ImmutableArray<ExperienceComponent>> ConflictedBy;
		public readonly IReadOnlyDictionary<string, string> ParameterValues;
		public readonly string PreviousPresentation;
		public readonly string Presentation;

		public int RegisteredCount => EnabledComponents.Length + DisabledComponents.Length;
		public int ChangeCount => NewlyEnabledComponents.Length + NewlyDisabledComponents.Length +
			ParameterChanges.Length + (PresentationChanged ? 1 : 0);
		public bool PresentationChanged => PreviousPresentation != Presentation;

		public ExperienceReviewModel(ExperienceCatalog catalog,
			IEnumerable<string> previousComponents, IEnumerable<string> components,
			IReadOnlyDictionary<string, string> previousParameterValues,
			IReadOnlyDictionary<string, string> parameterValues,
			string previousPresentation, string presentation)
			: this(catalog.Components, previousComponents, components, previousParameterValues,
				parameterValues, previousPresentation, presentation)
		{
		}

		public ExperienceReviewModel(IReadOnlyDictionary<string, ExperienceComponent> componentsById,
			IEnumerable<string> previousComponents, IEnumerable<string> components,
			IReadOnlyDictionary<string, string> previousParameterValues,
			IReadOnlyDictionary<string, string> parameterValues,
			string previousPresentation, string presentation)
		{
			ComponentsById = componentsById;
			var previous = previousComponents.Where(componentsById.ContainsKey).ToHashSet();
			var selected = components.Where(componentsById.ContainsKey).ToHashSet();
			var ordered = componentsById.Values.OrderBy(c => c.Category).ThenBy(c => c.Title).ToArray();

			EnabledComponents = ordered.Where(c => selected.Contains(c.Id)).ToImmutableArray();
			DisabledComponents = ordered.Where(c => !selected.Contains(c.Id)).ToImmutableArray();
			NewlyEnabledComponents = ordered.Where(c => selected.Contains(c.Id) && !previous.Contains(c.Id)).ToImmutableArray();
			NewlyDisabledComponents = ordered.Where(c => previous.Contains(c.Id) && !selected.Contains(c.Id)).ToImmutableArray();
			RequiredBy = ordered.ToDictionary(component => component.Id, component => ordered
				.Where(candidate => selected.Contains(candidate.Id) && candidate.Dependencies.Contains(component.Id))
				.ToImmutableArray());
			ConflictedBy = ordered.ToDictionary(component => component.Id, component => NewlyEnabledComponents
				.Where(candidate => candidate.Conflicts.Contains(component.Id)).ToImmutableArray());

			ParameterChanges = ordered.SelectMany(component => component.Parameters.Values
				.OrderBy(p => p.Group).ThenBy(p => p.Title)
				.Select(parameter =>
				{
					var key = ExperienceCatalog.ParameterKey(component.Id, parameter.Id);
					var before = previousParameterValues.TryGetValue(key, out var oldValue) ? oldValue : parameter.Default;
					var after = parameterValues.TryGetValue(key, out var newValue) ? newValue : parameter.Default;
					return before == after ? null : new ExperienceParameterChange(component, parameter, before, after);
				})
				.Where(change => change != null)).ToImmutableArray();
			ParameterValues = parameterValues.ToImmutableDictionary();

			PreviousPresentation = previousPresentation;
			Presentation = presentation;
		}
	}
}
