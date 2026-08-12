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
using System.Security.Cryptography;
using System.Text;

namespace OpenRA.Mods.Common.Experience
{
	public sealed class ExperienceComponent
	{
		public readonly string Id;
		public readonly string Title;
		public readonly string Description;
		public readonly string Category;
		public readonly string Version;
		public readonly string Source;
		public readonly string License;
		public readonly ImmutableArray<string> Dependencies;
		public readonly ImmutableArray<string> Conflicts;
		public readonly ImmutableArray<string> Rules;
		public readonly ImmutableArray<string> Weapons;
		public readonly ImmutableArray<string> Sequences;
		public readonly ImmutableArray<string> Cursors;
		public readonly ImmutableArray<string> Chrome;
		public readonly ImmutableArray<string> Voices;
		public readonly ImmutableArray<string> Notifications;
		public readonly ImmutableArray<string> Music;

		public ExperienceComponent(string id, MiniYaml yaml)
		{
			Id = id;
			Title = Required(yaml, "Title", id);
			Description = Required(yaml, "Description", id);
			Category = Required(yaml, "Category", id);
			Version = Required(yaml, "Version", id);
			Source = Value(yaml, "Source", "OpenRA AI");
			License = Value(yaml, "License", "GPL-3.0-or-later code; project asset policy");
			Dependencies = ParseList(yaml, "Dependencies");
			Conflicts = ParseList(yaml, "Conflicts");
			Rules = ParseFiles(yaml, "Rules");
			Weapons = ParseFiles(yaml, "Weapons");
			Sequences = ParseFiles(yaml, "Sequences");
			Cursors = ParseFiles(yaml, "Cursors");
			Chrome = ParseFiles(yaml, "Chrome");
			Voices = ParseFiles(yaml, "Voices");
			Notifications = ParseFiles(yaml, "Notifications");
			Music = ParseFiles(yaml, "Music");
		}

		static string Required(MiniYaml yaml, string key, string id)
		{
			var value = Value(yaml, key, null);
			if (string.IsNullOrWhiteSpace(value))
				throw new InvalidDataException($"Experience component `{id}` requires `{key}`.");

			return value.Trim();
		}

		internal static string Value(MiniYaml yaml, string key, string fallback)
		{
			return yaml.NodeWithKeyOrDefault(key)?.Value.Value ?? fallback;
		}

		internal static ImmutableArray<string> ParseList(MiniYaml yaml, string key)
		{
			var value = Value(yaml, key, null);
			return string.IsNullOrWhiteSpace(value) ? [] : value.Split(',')
				.Select(v => v.Trim())
				.Where(v => v.Length > 0)
				.Distinct()
				.ToImmutableArray();
		}

		static ImmutableArray<string> ParseFiles(MiniYaml yaml, string key)
		{
			var files = ParseList(yaml, key);
			foreach (var file in files)
				if (file.Contains("..", StringComparison.Ordinal) || file.StartsWith('^'))
					throw new InvalidDataException($"Experience component file `{file}` must be inside a mounted mod package.");

			return files;
		}
	}

	public sealed class ExperienceProfile
	{
		public readonly string Id;
		public readonly string Title;
		public readonly string Description;
		public readonly ImmutableArray<string> Components;

		public ExperienceProfile(string id, MiniYaml yaml)
		{
			Id = id;
			Title = ExperienceComponent.Value(yaml, "Title", id);
			Description = ExperienceComponent.Value(yaml, "Description", "Customizable OpenRA experience.");
			Components = ExperienceComponent.ParseList(yaml, "Components");
		}
	}

	public sealed class ExperienceCatalog : IModFileConfiguration
	{
		static readonly IReadOnlyCollection<string> EmptyFiles = [];

		public readonly string Mod;
		public readonly string DefaultProfileId;
		public readonly IReadOnlyDictionary<string, ExperienceComponent> Components;
		public readonly IReadOnlyDictionary<string, ExperienceProfile> Profiles;
		public readonly ExperienceProfile ActiveProfile;
		public readonly ImmutableArray<string> ActiveComponentIds;
		public readonly PresentationPackDefinition ActivePresentationPack;

		readonly ImmutableArray<string> managedRules;
		readonly ImmutableArray<string> activeRules;
		readonly ImmutableArray<string> managedWeapons;
		readonly ImmutableArray<string> activeWeapons;
		readonly ImmutableArray<string> managedSequences;
		readonly ImmutableArray<string> activeSequences;
		readonly ImmutableArray<string> managedCursors;
		readonly ImmutableArray<string> activeCursors;
		readonly ImmutableArray<string> managedChrome;
		readonly ImmutableArray<string> activeChrome;
		readonly ImmutableArray<string> managedVoices;
		readonly ImmutableArray<string> activeVoices;
		readonly ImmutableArray<string> managedNotifications;
		readonly ImmutableArray<string> activeNotifications;
		readonly ImmutableArray<string> managedMusic;
		readonly ImmutableArray<string> activeMusic;

		public ExperienceCatalog(MiniYaml yaml)
		{
			Mod = RequiredValue(yaml, "Mod");
			DefaultProfileId = RequiredValue(yaml, "DefaultProfile");
			Components = ParseComponents(RequiredNode(yaml, "Components"));
			Profiles = ParseProfiles(RequiredNode(yaml, "Profiles"));

			if (!Profiles.TryGetValue(DefaultProfileId, out var defaultProfile))
				throw new InvalidDataException($"Experience default profile `{DefaultProfileId}` does not exist.");

			ValidateGraph();
			foreach (var profile in Profiles.Values)
				Resolve(profile.Components, strictConflicts: true);

			var settings = Game.Settings.GetOrCreate<ExperienceSettings>(null, Mod);
			ActiveProfile = Profiles.TryGetValue(settings.Profile, out var selectedProfile) ? selectedProfile : defaultProfile;
			var requested = settings.UseCustomComponents ? ParseSettingsList(settings.EnabledComponents) : ActiveProfile.Components;
			ActiveComponentIds = Resolve(requested, strictConflicts: false).Order().ToImmutableArray();
			ActivePresentationPack = PresentationPackRegistry.Find(Mod, settings.PresentationPack);

			var activeComponents = ActiveComponentIds.Select(id => Components[id]).ToArray();
			managedRules = AllFiles(c => c.Rules);
			activeRules = ActiveFiles(activeComponents, c => c.Rules);
			managedWeapons = AllFiles(c => c.Weapons);
			activeWeapons = ActiveFiles(activeComponents, c => c.Weapons);
			managedSequences = AllFiles(c => c.Sequences);
			activeSequences = ActiveFiles(activeComponents, c => c.Sequences);
			managedCursors = AllFiles(c => c.Cursors);
			activeCursors = ActiveFiles(activeComponents, c => c.Cursors);
			managedChrome = AllFiles(c => c.Chrome);
			activeChrome = ActiveFiles(activeComponents, c => c.Chrome);
			managedVoices = AllFiles(c => c.Voices);
			activeVoices = ActiveFiles(activeComponents, c => c.Voices);
			managedNotifications = AllFiles(c => c.Notifications);
			activeNotifications = ActiveFiles(activeComponents, c => c.Notifications);
			managedMusic = AllFiles(c => c.Music);
			activeMusic = ActiveFiles(activeComponents, c => c.Music);

			GameplayFingerprint = ComputeGameplayFingerprint(ActiveComponentIds);
			PresentationFingerprint = ActivePresentationPack.Fingerprint;
		}

		public IReadOnlyCollection<string> ManagedRules => managedRules;
		public IReadOnlyCollection<string> ActiveRules => activeRules;
		public IReadOnlyCollection<string> ManagedWeapons => managedWeapons;
		public IReadOnlyCollection<string> ActiveWeapons => activeWeapons;
		public IReadOnlyCollection<string> ManagedSequences => managedSequences;
		public IReadOnlyCollection<string> ActiveSequences => activeSequences;
		public IReadOnlyCollection<string> ManagedCursors => managedCursors;
		public IReadOnlyCollection<string> ActiveCursors => activeCursors;
		public IReadOnlyCollection<string> ManagedChrome => managedChrome;
		public IReadOnlyCollection<string> ActiveChrome => activeChrome;
		public IReadOnlyCollection<string> ManagedVoices => managedVoices;
		public IReadOnlyCollection<string> ActiveVoices => activeVoices;
		public IReadOnlyCollection<string> ManagedNotifications => managedNotifications;
		public IReadOnlyCollection<string> ActiveNotifications => activeNotifications;
		public IReadOnlyCollection<string> ManagedMusic => managedMusic;
		public IReadOnlyCollection<string> ActiveMusic => activeMusic;
		public string GameplayFingerprint { get; }
		public string PresentationFingerprint { get; }

		public ImmutableArray<string> ComponentsForProfile(string profileId)
		{
			var profile = Profiles.TryGetValue(profileId, out var value) ? value : Profiles[DefaultProfileId];
			return Resolve(profile.Components, strictConflicts: true).Order().ToImmutableArray();
		}

		public ImmutableArray<string> ToggleComponent(IEnumerable<string> current, string componentId, bool enabled)
		{
			if (!Components.ContainsKey(componentId))
				throw new InvalidDataException($"Unknown experience component `{componentId}`.");

			var selected = current.Where(Components.ContainsKey).ToHashSet();
			if (enabled)
			{
				foreach (var conflict in Components[componentId].Conflicts)
					selected.Remove(conflict);

				EnableWithDependencies(componentId, selected, []);
			}
			else
			{
				selected.Remove(componentId);
				var changed = true;
				while (changed)
				{
					changed = false;
					foreach (var selectedId in selected.ToArray())
						if (Components[selectedId].Dependencies.Any(d => !selected.Contains(d)))
							changed |= selected.Remove(selectedId);
				}
			}

			return selected.Order().ToImmutableArray();
		}

		public string ComputeGameplayFingerprint(IEnumerable<string> componentIds)
		{
			var builder = new StringBuilder("experience-v1\n");
			foreach (var id in componentIds.Distinct().Order())
			{
				var component = Components[id];
				builder.Append(component.Id).Append(':').Append(component.Version).Append('\n');
				AppendFiles(builder, "rules", component.Rules);
				AppendFiles(builder, "weapons", component.Weapons);
				AppendFiles(builder, "sequences", component.Sequences);
				AppendFiles(builder, "voices", component.Voices);
				AppendFiles(builder, "notifications", component.Notifications);
				AppendFiles(builder, "music", component.Music);
			}

			return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
		}

		ImmutableArray<string> Resolve(IEnumerable<string> requested, bool strictConflicts)
		{
			var selected = new HashSet<string>();
			foreach (var id in requested)
			{
				if (!Components.ContainsKey(id))
				{
					if (strictConflicts)
						throw new InvalidDataException($"Experience profile references unknown component `{id}`.");

					continue;
				}

				var conflicts = Components[id].Conflicts.Where(selected.Contains).ToArray();
				if (strictConflicts && conflicts.Length > 0)
					throw new InvalidDataException($"Experience component `{id}` conflicts with {conflicts.JoinWith(", ")}.");

				foreach (var conflict in conflicts)
					selected.Remove(conflict);

				EnableWithDependencies(id, selected, []);
			}

			return selected.Order().ToImmutableArray();
		}

		void EnableWithDependencies(string id, HashSet<string> selected, HashSet<string> stack)
		{
			if (!stack.Add(id))
				throw new InvalidDataException($"Experience component dependency cycle includes `{id}`.");

			foreach (var dependency in Components[id].Dependencies)
				EnableWithDependencies(dependency, selected, stack);

			stack.Remove(id);
			selected.Add(id);
		}

		void ValidateGraph()
		{
			foreach (var component in Components.Values)
			{
				foreach (var dependency in component.Dependencies)
					if (!Components.ContainsKey(dependency))
						throw new InvalidDataException($"Experience component `{component.Id}` depends on unknown `{dependency}`.");

				foreach (var conflict in component.Conflicts)
					if (!Components.ContainsKey(conflict))
						throw new InvalidDataException($"Experience component `{component.Id}` conflicts with unknown `{conflict}`.");

				EnableWithDependencies(component.Id, [], []);
			}
		}

		ImmutableArray<string> AllFiles(Func<ExperienceComponent, ImmutableArray<string>> selector)
		{
			return Components.Values.SelectMany(c => selector(c).AsEnumerable()).Distinct().ToImmutableArray();
		}

		static ImmutableArray<string> ActiveFiles(
			IEnumerable<ExperienceComponent> components,
			Func<ExperienceComponent, ImmutableArray<string>> selector)
		{
			return components.SelectMany(c => selector(c).AsEnumerable()).Distinct().ToImmutableArray();
		}

		static IReadOnlyDictionary<string, ExperienceComponent> ParseComponents(MiniYaml yaml)
		{
			return yaml.Nodes.Select(n => new ExperienceComponent(n.Key, n.Value))
				.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
		}

		static IReadOnlyDictionary<string, ExperienceProfile> ParseProfiles(MiniYaml yaml)
		{
			return yaml.Nodes.Select(n => new ExperienceProfile(n.Key, n.Value))
				.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
		}

		static MiniYaml RequiredNode(MiniYaml yaml, string key)
		{
			return yaml.NodeWithKeyOrDefault(key)?.Value ??
				throw new InvalidDataException($"Experience catalog requires `{key}`.");
		}

		static string RequiredValue(MiniYaml yaml, string key)
		{
			var value = ExperienceComponent.Value(yaml, key, null);
			return !string.IsNullOrWhiteSpace(value) ? value.Trim() :
				throw new InvalidDataException($"Experience catalog requires `{key}`.");
		}

		static IEnumerable<string> ParseSettingsList(string value)
		{
			return string.IsNullOrWhiteSpace(value) ? EmptyFiles : value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0);
		}

		static void AppendFiles(StringBuilder builder, string type, IEnumerable<string> files)
		{
			foreach (var file in files.Order())
				builder.Append(type).Append(':').Append(file).Append('\n');
		}
	}
}
