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
	public enum ExperienceParameterType { Boolean, Integer, Choice }
	public enum ExperienceComponentKind { Module, Faction, Internal }

	public sealed class FactionPackMetadata
	{
		static readonly string[] RequiredRosterCategories =
		[
			"Infantry", "Vehicles", "Aircraft", "Navy", "Buildings", "Defenses"
		];

		public readonly string InternalName;
		public readonly string Side;
		public readonly string RandomPool;
		public readonly string Doctrine;
		public readonly string Preview;
		public readonly IReadOnlyDictionary<string, ImmutableArray<string>> Roster;

		public FactionPackMetadata(string componentId, MiniYaml yaml, string filePrefix)
		{
			InternalName = Required(yaml, "InternalName", componentId);
			Side = Required(yaml, "Side", componentId);
			RandomPool = Required(yaml, "RandomPool", componentId);
			Doctrine = Required(yaml, "Doctrine", componentId);
			Preview = ExperienceComponent.ParseFile(yaml, "Preview", filePrefix, required: true);

			if (!InternalName.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
				throw new InvalidDataException($"Faction pack `{componentId}` has invalid InternalName `{InternalName}`.");

			if (!Path.GetExtension(Preview).Equals(".png", StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException($"Faction pack `{componentId}` preview must be a PNG image.");

			var rosterNode = yaml.NodeWithKeyOrDefault("Roster")?.Value ??
				throw new InvalidDataException($"Faction pack `{componentId}` requires a Roster node.");
			var roster = new Dictionary<string, ImmutableArray<string>>(StringComparer.OrdinalIgnoreCase);
			foreach (var category in RequiredRosterCategories)
			{
				var actors = ExperienceComponent.ParseList(rosterNode, category);
				if (actors.Length == 0)
					throw new InvalidDataException($"Faction pack `{componentId}` roster requires at least one {category} actor.");

				roster.Add(category, actors);
			}

			Roster = roster;
		}

		public int ActorCount => Roster.Values.SelectMany(actors => actors).Distinct(StringComparer.OrdinalIgnoreCase).Count();

		static string Required(MiniYaml yaml, string key, string componentId)
		{
			var value = ExperienceComponent.Value(yaml, key, null);
			if (string.IsNullOrWhiteSpace(value))
				throw new InvalidDataException($"Faction pack `{componentId}` requires `{key}`.");

			return value.Trim();
		}
	}

	public sealed class ExperienceParameter
	{
		public readonly string Id;
		public readonly string Title;
		public readonly string Description;
		public readonly string Group;
		public readonly ExperienceParameterType Type;
		public readonly string Default;
		public readonly int Minimum;
		public readonly int Maximum;
		public readonly int Step;
		public readonly string Unit;
		public readonly ImmutableArray<string> Options;

		public ExperienceParameter(string id, MiniYaml yaml)
		{
			Id = id;
			Title = Required(yaml, "Title", id);
			Description = Required(yaml, "Description", id);
			Group = ExperienceComponent.Value(yaml, "Group", "Balance").Trim();
			Type = FieldLoader.GetValue<ExperienceParameterType>("Type", Required(yaml, "Type", id));
			Unit = ExperienceComponent.Value(yaml, "Unit", "").Trim();
			Options = ExperienceComponent.ParseList(yaml, "Options");
			Minimum = ParseInteger(yaml, "Minimum", 0, id);
			Maximum = ParseInteger(yaml, "Maximum", 100, id);
			Step = Math.Max(1, ParseInteger(yaml, "Step", 1, id));

			if (Type == ExperienceParameterType.Integer && Maximum < Minimum)
				throw new InvalidDataException($"Experience parameter `{id}` Maximum must be greater than or equal to Minimum.");

			if (Type == ExperienceParameterType.Choice && Options.Length < 2)
				throw new InvalidDataException($"Experience choice parameter `{id}` requires at least two Options.");

			Default = Normalize(Required(yaml, "Default", id));
		}

		public string Normalize(string value)
		{
			value = value?.Trim() ?? "";
			switch (Type)
			{
				case ExperienceParameterType.Boolean:
					if (bool.TryParse(value, out var boolean))
						return boolean ? "true" : "false";

					break;
				case ExperienceParameterType.Integer:
					if (int.TryParse(value, out var integer))
					{
						integer = Math.Clamp(integer, Minimum, Maximum);
						integer = Minimum + (int)Math.Round((integer - Minimum) / (double)Step) * Step;
						return Math.Clamp(integer, Minimum, Maximum).ToStringInvariant();
					}

					break;
				case ExperienceParameterType.Choice:
					var option = Options.FirstOrDefault(o => o.Equals(value, StringComparison.OrdinalIgnoreCase));
					if (option != null)
						return option;

					break;
			}

			throw new InvalidDataException($"Invalid value `{value}` for experience parameter `{Id}`.");
		}

		static string Required(MiniYaml yaml, string key, string id)
		{
			var value = ExperienceComponent.Value(yaml, key, null);
			if (string.IsNullOrWhiteSpace(value))
				throw new InvalidDataException($"Experience parameter `{id}` requires `{key}`.");

			return value.Trim();
		}

		static int ParseInteger(MiniYaml yaml, string key, int fallback, string id)
		{
			var value = ExperienceComponent.Value(yaml, key, null);
			if (value == null)
				return fallback;

			if (!int.TryParse(value, out var result))
				throw new InvalidDataException($"Experience parameter `{id}` field `{key}` must be an integer.");

			return result;
		}
	}

	public sealed class ExperienceComponent
	{
		public readonly string Id;
		public readonly string Title;
		public readonly string Description;
		public readonly string Effects;
		public readonly string Tradeoffs;
		public readonly string Scope;
		public readonly string Category;
		public readonly string Version;
		public readonly string Source;
		public readonly string License;
		public readonly ExperienceComponentKind Kind;
		public readonly bool Hidden;
		public readonly FactionPackMetadata Faction;
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
		public readonly IReadOnlyDictionary<string, ExperienceParameter> Parameters;
		public readonly bool IsExternal;
		public readonly string PackageId;
		public readonly string PackagePath;
		public readonly string ContentFingerprint;

		public ExperienceComponent(string id, MiniYaml yaml, string filePrefix = null,
			string packageId = null, string packagePath = null, string contentFingerprint = null)
		{
			Id = id;
			Title = Required(yaml, "Title", id);
			Description = Required(yaml, "Description", id);
			Effects = Required(yaml, "Effects", id);
			Tradeoffs = Required(yaml, "Tradeoffs", id);
			Scope = Required(yaml, "Scope", id);
			Category = Required(yaml, "Category", id);
			Version = Required(yaml, "Version", id);
			Source = Value(yaml, "Source", "OpenRA AI");
			License = Value(yaml, "License", "GPL-3.0-or-later code; project asset policy");
			Kind = FieldLoader.GetValue<ExperienceComponentKind>("Kind", Value(yaml, "Kind", "Module"));
			Hidden = FieldLoader.GetValue<bool>("Hidden", Value(yaml, "Hidden", "false"));
			Dependencies = ParseList(yaml, "Dependencies");
			Conflicts = ParseList(yaml, "Conflicts");
			Rules = ParseFiles(yaml, "Rules", filePrefix);
			Weapons = ParseFiles(yaml, "Weapons", filePrefix);
			Sequences = ParseFiles(yaml, "Sequences", filePrefix);
			Cursors = ParseFiles(yaml, "Cursors", filePrefix);
			Chrome = ParseFiles(yaml, "Chrome", filePrefix);
			Voices = ParseFiles(yaml, "Voices", filePrefix);
			Notifications = ParseFiles(yaml, "Notifications", filePrefix);
			Music = ParseFiles(yaml, "Music", filePrefix);
			Parameters = ParseParameters(yaml.NodeWithKeyOrDefault("Parameters")?.Value);
			var factionNode = yaml.NodeWithKeyOrDefault("Faction")?.Value;
			Faction = Kind == ExperienceComponentKind.Faction ?
				new FactionPackMetadata(id, factionNode ??
					throw new InvalidDataException($"Faction component `{id}` requires a Faction node."), filePrefix) : null;
			if (Kind != ExperienceComponentKind.Faction && factionNode != null)
				throw new InvalidDataException($"Experience component `{id}` declares Faction metadata but Kind is `{Kind}`.");

			IsExternal = packageId != null;
			PackageId = packageId;
			PackagePath = packagePath;
			ContentFingerprint = contentFingerprint;
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

		internal static string ParseFile(MiniYaml yaml, string key, string prefix, bool required)
		{
			var files = ParseFiles(yaml, key, prefix);
			if (files.Length > 1)
				throw new InvalidDataException($"Experience component field `{key}` accepts only one file.");

			if (required && files.Length == 0)
				throw new InvalidDataException($"Experience component requires `{key}`.");

			return files.FirstOrDefault();
		}

		static ImmutableArray<string> ParseFiles(MiniYaml yaml, string key, string prefix)
		{
			var files = ParseList(yaml, key);
			if (!string.IsNullOrEmpty(prefix))
				foreach (var file in files)
					if (file.Contains("..", StringComparison.Ordinal) || file.StartsWith('^') ||
						file.Contains('|') || Path.IsPathRooted(file))
						throw new InvalidDataException($"Experience component file `{file}` must be inside its capability pack.");

			return string.IsNullOrEmpty(prefix) ? files : files.Select(file => $"{prefix}/{file.Replace('\\', '/')}")
				.ToImmutableArray();
		}

		static IReadOnlyDictionary<string, ExperienceParameter> ParseParameters(MiniYaml yaml)
		{
			return yaml == null ? new Dictionary<string, ExperienceParameter>() : yaml.Nodes
				.Select(n => new ExperienceParameter(n.Key, n.Value))
				.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
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
		public readonly IReadOnlyDictionary<string, ExperienceComponent> FactionPacks;
		public readonly IReadOnlyDictionary<string, ExperienceProfile> Profiles;
		public readonly ExperienceProfile ActiveProfile;
		public readonly ImmutableArray<string> ActiveComponentIds;
		public readonly PresentationPackDefinition ActivePresentationPack;
		public readonly IReadOnlyDictionary<string, string> ActiveParameterValues;

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
			var builtInComponents = ParseComponents(RequiredNode(yaml, "Components"));
			var components = new Dictionary<string, ExperienceComponent>(builtInComponents, StringComparer.OrdinalIgnoreCase);
			foreach (var pack in CapabilityPackRegistry.Discover(Mod).Values)
			{
				if (!components.TryAdd(pack.Component.Id, pack.Component))
					throw new InvalidDataException($"Capability pack `{pack.Id}` duplicates experience component `{pack.Component.Id}`.");
			}

			Components = components;
			FactionPacks = components.Values.Where(component => component.Kind == ExperienceComponentKind.Faction)
				.ToDictionary(component => component.Faction.InternalName, StringComparer.OrdinalIgnoreCase);
			Profiles = ParseProfiles(RequiredNode(yaml, "Profiles"));

			if (!Profiles.TryGetValue(DefaultProfileId, out var defaultProfile))
				throw new InvalidDataException($"Experience default profile `{DefaultProfileId}` does not exist.");

			ValidateGraph();
			foreach (var profile in Profiles.Values)
				Resolve(profile.Components, strictConflicts: true);

			var settings = Game.Settings.GetOrCreate<ExperienceSettings>(null, Mod);
			var utilityProfile = Environment.GetEnvironmentVariable("OPENRA_UTILITY_EXPERIENCE_PROFILE");
			var selectedProfileId = string.IsNullOrWhiteSpace(utilityProfile) ? settings.Profile : utilityProfile;
			ActiveProfile = Profiles.TryGetValue(selectedProfileId, out var selectedProfile) ? selectedProfile : defaultProfile;
			var requested = settings.UseCustomComponents ? ParseSettingsList(settings.EnabledComponents) : ActiveProfile.Components;
			ActiveComponentIds = Resolve(requested, strictConflicts: false).Order().ToImmutableArray();
			ActiveParameterValues = ParseParameterSettings(settings.ParameterValues);
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

			GameplayFingerprint = ComputeGameplayFingerprint(ActiveComponentIds, ActiveParameterValues);
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

		public IReadOnlyDictionary<string, string> DefaultParameterValues()
		{
			return Components.Values.SelectMany(component => component.Parameters.Values.Select(parameter =>
				KeyValuePair.Create(ParameterKey(component.Id, parameter.Id), parameter.Default)))
				.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
		}

		public IReadOnlyDictionary<string, string> ParseParameterSettings(string value)
		{
			var result = DefaultParameterValues().ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrWhiteSpace(value))
				return result;

			foreach (var entry in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				var separator = entry.IndexOf('=');
				if (separator <= 0)
					continue;

				var key = entry[..separator].Trim();
				var rawValue = entry[(separator + 1)..].Trim();
				if (TryGetParameter(key, out var parameter))
				{
					try { result[key] = parameter.Normalize(rawValue); }
					catch (InvalidDataException) { }
				}
			}

			return result;
		}

		public string SerializeParameterSettings(IReadOnlyDictionary<string, string> values)
		{
			var defaults = DefaultParameterValues();
			return values.Where(kv => defaults.TryGetValue(kv.Key, out var defaultValue) && kv.Value != defaultValue)
				.OrderBy(kv => kv.Key)
				.Select(kv => $"{kv.Key}={kv.Value}")
				.JoinWith(";");
		}

		public int GetIntegerParameter(string componentId, string parameterId, int fallback)
		{
			return ActiveParameterValues.TryGetValue(ParameterKey(componentId, parameterId), out var value) &&
				int.TryParse(value, out var integer) ? integer : fallback;
		}

		public bool GetBooleanParameter(string componentId, string parameterId, bool fallback)
		{
			return ActiveParameterValues.TryGetValue(ParameterKey(componentId, parameterId), out var value) &&
				bool.TryParse(value, out var boolean) ? boolean : fallback;
		}

		public string GetChoiceParameter(string componentId, string parameterId, string fallback)
		{
			return ActiveParameterValues.TryGetValue(ParameterKey(componentId, parameterId), out var value) ? value : fallback;
		}

		public bool IsComponentActive(string componentId)
		{
			return ActiveComponentIds.Contains(componentId, StringComparer.OrdinalIgnoreCase);
		}

		public string ComputeGameplayFingerprint(IEnumerable<string> componentIds,
			IReadOnlyDictionary<string, string> parameterValues = null)
		{
			parameterValues ??= DefaultParameterValues();
			var builder = new StringBuilder("experience-v2\n");
			foreach (var id in componentIds.Distinct().Order())
			{
				var component = Components[id];
				builder.Append(component.Id).Append(':').Append(component.Version).Append('\n');
				if (!string.IsNullOrEmpty(component.ContentFingerprint))
					builder.Append("content:").Append(component.ContentFingerprint).Append('\n');
				foreach (var parameter in component.Parameters.Values.OrderBy(p => p.Id))
				{
					var key = ParameterKey(component.Id, parameter.Id);
					var value = parameterValues.TryGetValue(key, out var configured) ? parameter.Normalize(configured) : parameter.Default;
					builder.Append("parameter:").Append(key).Append('=').Append(value).Append('\n');
				}

				AppendFiles(builder, "rules", component.Rules);
				AppendFiles(builder, "weapons", component.Weapons);
				AppendFiles(builder, "sequences", component.Sequences);
				AppendFiles(builder, "cursors", component.Cursors);
				AppendFiles(builder, "chrome", component.Chrome);
				AppendFiles(builder, "voices", component.Voices);
				AppendFiles(builder, "notifications", component.Notifications);
				AppendFiles(builder, "music", component.Music);
			}

			return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
		}

		public static string ParameterKey(string componentId, string parameterId) => $"{componentId}.{parameterId}";

		bool TryGetParameter(string key, out ExperienceParameter parameter)
		{
			var separator = key.IndexOf('.');
			if (separator > 0 && Components.TryGetValue(key[..separator], out var component) &&
				component.Parameters.TryGetValue(key[(separator + 1)..], out parameter))
				return true;

			parameter = null;
			return false;
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
			var duplicateFaction = Components.Values.Where(component => component.Faction != null)
				.GroupBy(component => component.Faction.InternalName, StringComparer.OrdinalIgnoreCase)
				.FirstOrDefault(group => group.Count() > 1);
			if (duplicateFaction != null)
				throw new InvalidDataException($"Multiple faction packs register `{duplicateFaction.Key}`.");

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
