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
using System.IO;
using System.Linq;

namespace OpenRA.Mods.Common.Experience
{
	/// <summary>Resolves complete, compatible module sets before their files are loaded.</summary>
	public sealed class ExperienceSelection
	{
		readonly IReadOnlyDictionary<string, ExperienceComponent> components;

		public ExperienceSelection(IReadOnlyDictionary<string, ExperienceComponent> components)
		{
			this.components = components.Values.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
			foreach (var component in components.Values)
			{
				foreach (var dependency in component.Dependencies)
					if (!this.components.ContainsKey(dependency))
						throw new InvalidDataException($"Experience component `{component.Id}` depends on unknown `{dependency}`.");

				foreach (var conflict in component.Conflicts)
					if (!this.components.ContainsKey(conflict))
						throw new InvalidDataException($"Experience component `{component.Id}` conflicts with unknown `{conflict}`.");
			}

			foreach (var component in components.Values)
			{
				var closure = DependencyOrder([component.Id]);
				foreach (var id in closure)
					if (closure.Any(other => Conflicts(id, other)))
						throw new InvalidDataException($"Experience component `{component.Id}` requires mutually conflicting modules.");
			}
		}

		public bool Conflicts(string first, string second)
		{
			return components[first].Conflicts.Contains(second, StringComparer.OrdinalIgnoreCase) ||
				components[second].Conflicts.Contains(first, StringComparer.OrdinalIgnoreCase);
		}

		public ImmutableArray<string> Resolve(IEnumerable<string> requested, bool strictConflicts)
		{
			var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var id in requested)
			{
				if (!components.ContainsKey(id))
				{
					if (strictConflicts)
						throw new InvalidDataException($"Experience profile references unknown component `{id}`.");

					continue;
				}

				Enable(id, selected, strictConflicts);
			}

			RemoveOrphanedInternals(selected);
			return selected.Order(StringComparer.Ordinal).ToImmutableArray();
		}

		public ImmutableArray<string> Toggle(IEnumerable<string> current, string componentId, bool enabled)
		{
			if (!components.ContainsKey(componentId))
				throw new InvalidDataException($"Unknown experience component `{componentId}`.");

			var selected = Resolve(current, false).ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (enabled)
				Enable(componentId, selected, false);
			else
			{
				selected.Remove(componentId);
				RemoveBrokenDependents(selected);
			}

			RemoveOrphanedInternals(selected);
			return selected.Order(StringComparer.Ordinal).ToImmutableArray();
		}

		public ImmutableArray<string> DependencyOrder(IEnumerable<string> ids)
		{
			var ordered = new List<string>();
			var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			void Visit(string id)
			{
				if (visited.Contains(id))
					return;
				if (!stack.Add(id))
					throw new InvalidDataException($"Experience component dependency cycle includes `{id}`.");

				var component = components[id];
				foreach (var dependency in component.Dependencies.Order(StringComparer.Ordinal))
					Visit(dependency);

				stack.Remove(id);
				visited.Add(id);
				ordered.Add(component.Id);
			}

			foreach (var id in ids.Order(StringComparer.Ordinal))
				Visit(id);
			return ordered.ToImmutableArray();
		}

		void Enable(string id, HashSet<string> selected, bool strictConflicts)
		{
			var incoming = DependencyOrder([id]);
			var conflicts = selected.Where(existing => incoming.Any(added => Conflicts(existing, added))).ToArray();
			if (strictConflicts && conflicts.Length > 0)
				throw new InvalidDataException($"Experience component `{id}` conflicts with {conflicts.JoinWith(", ")}.");

			selected.ExceptWith(conflicts);
			RemoveBrokenDependents(selected);
			selected.UnionWith(incoming);
		}

		void RemoveBrokenDependents(HashSet<string> selected)
		{
			while (selected.RemoveWhere(id => components[id].Dependencies.Any(dependency => !selected.Contains(dependency))) > 0) { }
		}

		void RemoveOrphanedInternals(HashSet<string> selected)
		{
			var required = DependencyOrder(selected.Where(id => components[id].Kind != ExperienceComponentKind.Internal))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			selected.RemoveWhere(id => components[id].Kind == ExperienceComponentKind.Internal && !required.Contains(id));
		}
	}
}
