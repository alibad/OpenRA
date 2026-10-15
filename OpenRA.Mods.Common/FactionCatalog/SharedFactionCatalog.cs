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
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace OpenRA.Mods.Common.FactionCatalog
{
	public sealed class SharedCatalogMode
	{
		public readonly string Id;
		public readonly string Name;
		public readonly string Subtitle;
		public readonly string Description;

		public SharedCatalogMode(string id, string name, string subtitle, string description)
		{
			Id = id;
			Name = name;
			Subtitle = subtitle;
			Description = description;
		}
	}

	public sealed class SharedCatalogProfile
	{
		public readonly string Id;
		public readonly string Mode;
		public readonly ImmutableArray<string> FactionIds;

		public SharedCatalogProfile(string id, string mode, ImmutableArray<string> factionIds)
		{
			Id = id;
			Mode = mode;
			FactionIds = factionIds;
		}
	}

	public sealed class SharedCatalogFactionVariant
	{
		public readonly string Status;
		public readonly string ProfileId;
		public readonly string Description;
		public bool IsImplemented => Status == "implemented";

		public SharedCatalogFactionVariant(string status, string profileId, string description)
		{
			Status = status;
			ProfileId = profileId;
			Description = description;
		}
	}

	public sealed class SharedCatalogFaction
	{
		public readonly string Id;
		public readonly string Name;
		public readonly string Title;
		public readonly string Tagline;
		public readonly string Story;
		public readonly string Playstyle;
		public readonly string Strengths;
		public readonly string Counterplay;
		public readonly string Accent;
		public readonly ImmutableArray<string> UnitIds;
		public readonly IReadOnlyDictionary<string, SharedCatalogFactionVariant> Variants;

		public SharedCatalogFaction(string id, string name, string title, string tagline, string story, string playstyle,
			string strengths, string counterplay, string accent, ImmutableArray<string> unitIds,
			IReadOnlyDictionary<string, SharedCatalogFactionVariant> variants)
		{
			Id = id;
			Name = name;
			Title = title;
			Tagline = tagline;
			Story = story;
			Playstyle = playstyle;
			Strengths = strengths;
			Counterplay = counterplay;
			Accent = accent;
			UnitIds = unitIds;
			Variants = variants;
		}

		public bool IsImplementedIn(string mode)
		{
			return mode != null && Variants.TryGetValue(mode, out var variant) && variant.IsImplemented;
		}
	}

	public sealed class SharedCatalogUnit
	{
		public readonly string Id;
		public readonly string FactionId;
		public readonly string Name;
		public readonly string Role;
		public readonly string Story;
		public readonly string Strengths;
		public readonly string Counterplay;

		/// <summary>Mode id to the engine actor that implements this unit in that mode.</summary>
		public readonly IReadOnlyDictionary<string, string> ActorIds;

		public SharedCatalogUnit(string id, string factionId, string name, string role, string story, string strengths,
			string counterplay, IReadOnlyDictionary<string, string> actorIds)
		{
			Id = id;
			FactionId = factionId;
			Name = name;
			Role = role;
			Story = story;
			Strengths = strengths;
			Counterplay = counterplay;
			ActorIds = actorIds;
		}

		public string ActorIn(string mode)
		{
			return mode != null && ActorIds.TryGetValue(mode, out var actor) ? actor : null;
		}
	}

	/// <summary>Public web pages that mirror the shared catalog. Paths contain {factionId}/{unitId} and {mode} placeholders.</summary>
	public sealed class SharedCatalogLinks
	{
		public const string DefaultOrigin = "https://rtsai.net";
		public const string DefaultFactionPath = "/factions/{factionId}?mode={mode}";
		public const string DefaultUnitPath = "/units/{unitId}?mode={mode}";
		public static readonly SharedCatalogLinks Default = new(DefaultOrigin, DefaultFactionPath, DefaultUnitPath);

		static readonly Regex IdPattern = new("^[a-z0-9-]+$", RegexOptions.CultureInvariant);

		public readonly string Origin;
		public readonly string FactionPath;
		public readonly string UnitPath;

		public SharedCatalogLinks(string origin, string factionPath, string unitPath)
		{
			Origin = origin;
			FactionPath = factionPath;
			UnitPath = unitPath;
		}

		public string FactionUrl(string factionId, string mode)
		{
			return Build(FactionPath, "{factionId}", factionId, mode);
		}

		public string UnitUrl(string unitId, string mode)
		{
			return Build(UnitPath, "{unitId}", unitId, mode);
		}

		string Build(string template, string placeholder, string id, string mode)
		{
			if (string.IsNullOrEmpty(template) || !IsId(id) || !IsId(mode))
				return null;

			return Origin.TrimEnd('/') + template.Replace(placeholder, id, StringComparison.Ordinal)
				.Replace("{mode}", mode, StringComparison.Ordinal);
		}

		public static bool IsId(string value) => value != null && IdPattern.IsMatch(value);

		/// <summary>Returns null for a well-formed public link, otherwise a short explanation.</summary>
		public static string LinkProblem(string url, string expectedOrigin)
		{
			if (string.IsNullOrWhiteSpace(url))
				return "link could not be built from the catalog id and mode";

			if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
				return $"`{url}` is not an absolute URL";

			if (uri.Scheme != Uri.UriSchemeHttps)
				return $"`{url}` must use https";

			if (uri.IsLoopback || string.IsNullOrEmpty(uri.Host) || uri.Host.Contains('{') || url.Contains('{') || url.Contains('}'))
				return $"`{url}` has an unresolved placeholder or no public host";

			if (!string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo))
				return $"`{url}` must not contain credentials or a fragment";

			if (expectedOrigin != null && Uri.TryCreate(expectedOrigin, UriKind.Absolute, out var origin) &&
				!string.Equals(origin.Host, uri.Host, StringComparison.OrdinalIgnoreCase))
				return $"`{url}` does not point at {expectedOrigin}";

			if (url.Any(char.IsWhiteSpace))
				return $"`{url}` contains whitespace";

			return null;
		}

		public string Problem()
		{
			if (!Uri.TryCreate(Origin, UriKind.Absolute, out var origin) || origin.Scheme != Uri.UriSchemeHttps ||
				origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
				return $"links.origin `{Origin}` must be an https origin without a path";

			if (FactionPath == null || !FactionPath.StartsWith('/') || !FactionPath.Contains("{factionId}") || !FactionPath.Contains("{mode}"))
				return $"links.faction `{FactionPath}` must start with / and contain {{factionId}} and {{mode}}";

			if (UnitPath == null || !UnitPath.StartsWith('/') || !UnitPath.Contains("{unitId}") || !UnitPath.Contains("{mode}"))
				return $"links.unit `{UnitPath}` must start with / and contain {{unitId}} and {{mode}}";

			return null;
		}
	}

	/// <summary>
	/// The product's shared faction/unit catalog (catalog/factions.json). It is editorial data only:
	/// numbers shown in game always come from the loaded rules, never from this file.
	/// </summary>
	public sealed class SharedFactionCatalog
	{
		public const string EnvironmentOverride = "OPENRA_AI_CATALOG";
		public const string RelativePath = "catalog/factions.json";

		public readonly int SchemaVersion;
		public readonly string Revision;
		public readonly string SourcePath;
		public readonly ImmutableArray<SharedCatalogMode> Modes;
		public readonly ImmutableArray<SharedCatalogProfile> Profiles;
		public readonly ImmutableArray<SharedCatalogFaction> Factions;
		public readonly ImmutableArray<SharedCatalogUnit> Units;
		public readonly SharedCatalogLinks Links;
		public readonly bool DeclaresLinks;

		readonly Dictionary<string, SharedCatalogFaction> factionsById;
		readonly Dictionary<string, SharedCatalogUnit> unitsById;

		SharedFactionCatalog(int schemaVersion, string revision, string sourcePath, ImmutableArray<SharedCatalogMode> modes,
			ImmutableArray<SharedCatalogProfile> profiles, ImmutableArray<SharedCatalogFaction> factions,
			ImmutableArray<SharedCatalogUnit> units, SharedCatalogLinks links, bool declaresLinks)
		{
			SchemaVersion = schemaVersion;
			Revision = revision;
			SourcePath = sourcePath;
			Modes = modes;
			Profiles = profiles;
			Factions = factions;
			Units = units;
			Links = links;
			DeclaresLinks = declaresLinks;
			factionsById = factions.ToDictionary(f => f.Id, StringComparer.Ordinal);
			unitsById = units.ToDictionary(u => u.Id, StringComparer.Ordinal);
		}

		public SharedCatalogFaction Faction(string id) => id != null && factionsById.TryGetValue(id, out var faction) ? faction : null;
		public SharedCatalogUnit Unit(string id) => id != null && unitsById.TryGetValue(id, out var unit) ? unit : null;
		public SharedCatalogMode Mode(string id) => Modes.FirstOrDefault(m => m.Id == id);

		public IEnumerable<SharedCatalogUnit> UnitsOf(SharedCatalogFaction faction)
		{
			return faction.UnitIds.Select(Unit).Where(u => u != null);
		}

		/// <summary>Parses the catalog. Throws <see cref="InvalidDataException"/> when the structure is unusable.</summary>
		public static SharedFactionCatalog Parse(string json, string sourcePath = null)
		{
			JsonDocument document;
			try
			{
				document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = false });
			}
			catch (JsonException e)
			{
				throw new InvalidDataException($"Faction catalog is not valid JSON: {e.Message}", e);
			}

			using (document)
			{
				var root = document.RootElement;
				if (root.ValueKind != JsonValueKind.Object)
					throw new InvalidDataException("Faction catalog root must be an object.");

				var schemaVersion = root.TryGetProperty("schemaVersion", out var schema) && schema.TryGetInt32(out var parsedSchema) ? parsedSchema : 0;
				if (schemaVersion != 1)
					throw new InvalidDataException($"Unsupported faction catalog schema `{schemaVersion}`.");

				var modes = Array(root, "modes").Select(m => new SharedCatalogMode(Id(m), Text(m, "name"), Text(m, "subtitle"), Text(m, "description")))
					.ToImmutableArray();
				var profiles = Array(root, "profiles").Select(p => new SharedCatalogProfile(Id(p), Text(p, "mode"), Strings(p, "factionIds")))
					.ToImmutableArray();
				var factions = Array(root, "factions").Select(ParseFaction).ToImmutableArray();
				var units = Array(root, "units").Select(ParseUnit).ToImmutableArray();

				foreach (var (name, ids) in new[]
				{
					("mode", modes.Select(m => m.Id)), ("profile", profiles.Select(p => p.Id)),
					("faction", factions.Select(f => f.Id)), ("unit", units.Select(u => u.Id))
				})
				{
					var duplicate = ids.GroupBy(id => id).FirstOrDefault(g => g.Count() > 1);
					if (duplicate != null)
						throw new InvalidDataException($"Faction catalog has duplicate {name} id `{duplicate.Key}`.");
				}

				var declaresLinks = root.TryGetProperty("links", out var linksElement) && linksElement.ValueKind == JsonValueKind.Object;
				var links = declaresLinks ? new SharedCatalogLinks(
					Text(linksElement, "origin") ?? SharedCatalogLinks.DefaultOrigin,
					Text(linksElement, "faction") ?? SharedCatalogLinks.DefaultFactionPath,
					Text(linksElement, "unit") ?? SharedCatalogLinks.DefaultUnitPath) : SharedCatalogLinks.Default;

				return new SharedFactionCatalog(schemaVersion, Text(root, "revision"), sourcePath, modes, profiles, factions, units,
					links, declaresLinks);
			}
		}

		static SharedCatalogFaction ParseFaction(JsonElement element)
		{
			var variants = new Dictionary<string, SharedCatalogFactionVariant>(StringComparer.Ordinal);
			if (element.TryGetProperty("variants", out var variantsElement) && variantsElement.ValueKind == JsonValueKind.Object)
				foreach (var property in variantsElement.EnumerateObject())
					variants[property.Name] = new SharedCatalogFactionVariant(Text(property.Value, "status"),
						Text(property.Value, "profileId"), Text(property.Value, "description"));

			return new SharedCatalogFaction(Id(element), Text(element, "name"), Text(element, "title"), Text(element, "tagline"),
				Text(element, "story"), Text(element, "playstyle"), Text(element, "strengths"), Text(element, "counterplay"),
				Text(element, "accent"), Strings(element, "unitIds"), variants);
		}

		static SharedCatalogUnit ParseUnit(JsonElement element)
		{
			var actors = new Dictionary<string, string>(StringComparer.Ordinal);
			if (element.TryGetProperty("variants", out var variantsElement) && variantsElement.ValueKind == JsonValueKind.Object)
				foreach (var property in variantsElement.EnumerateObject())
					actors[property.Name] = Text(property.Value, "actorId");

			return new SharedCatalogUnit(Id(element), Text(element, "factionId"), Text(element, "name"), Text(element, "role"),
				Text(element, "story"), Text(element, "strengths"), Text(element, "counterplay"), actors);
		}

		static IEnumerable<JsonElement> Array(JsonElement root, string name)
		{
			if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
				throw new InvalidDataException($"Faction catalog requires a `{name}` array.");

			return array.EnumerateArray().ToArray();
		}

		static string Id(JsonElement element)
		{
			var id = Text(element, "id");
			if (!SharedCatalogLinks.IsId(id))
				throw new InvalidDataException($"Faction catalog id `{id}` must use lowercase letters, digits and hyphens.");

			return id;
		}

		static string Text(JsonElement element, string name)
		{
			return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
				value.ValueKind == JsonValueKind.String ? value.GetString() : null;
		}

		static ImmutableArray<string> Strings(JsonElement element, string name)
		{
			if (!element.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
				return [];

			return array.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()).ToImmutableArray();
		}

		/// <summary>
		/// Where an installed or development build keeps the product catalog, in priority order:
		/// explicit override, product root, macOS app resources (engine dir), Windows package/product
		/// submodule (engine/openra), and a canonical engine checkout beside the product checkout.
		/// </summary>
		public static IEnumerable<string> CandidatePaths(string engineDir, Func<string, string> getEnvironmentVariable)
		{
			var explicitPath = getEnvironmentVariable(EnvironmentOverride);
			if (!string.IsNullOrWhiteSpace(explicitPath))
				yield return Path.GetFullPath(explicitPath);

			var productRoot = getEnvironmentVariable("OPENRA_AI_ROOT");
			if (!string.IsNullOrWhiteSpace(productRoot))
				yield return Path.GetFullPath(Path.Combine(productRoot, RelativePath));

			if (string.IsNullOrWhiteSpace(engineDir))
				yield break;

			yield return Path.GetFullPath(Path.Combine(engineDir, RelativePath));
			yield return Path.GetFullPath(Path.Combine(engineDir, "..", "..", RelativePath));
			yield return Path.GetFullPath(Path.Combine(engineDir, "..", "OpenRA-AI", RelativePath));
		}

		public static string Locate(string engineDir, Func<string, string> getEnvironmentVariable, Func<string, bool> fileExists)
		{
			return CandidatePaths(engineDir, getEnvironmentVariable).FirstOrDefault(fileExists);
		}

		static readonly Lock CacheLock = new();
		static (string Path, DateTime Modified, SharedFactionCatalog Catalog, string Error) cache;

		/// <summary>Loads the installed catalog, or returns null with a reason when it is missing or invalid.</summary>
		public static SharedFactionCatalog LoadInstalled(out string error)
		{
			var path = Locate(Platform.EngineDir, Environment.GetEnvironmentVariable, File.Exists);
			if (path == null)
			{
				error = "The shared faction catalog is not installed with this build.";
				return null;
			}

			lock (CacheLock)
			{
				var modified = File.GetLastWriteTimeUtc(path);
				if (cache.Path != path || cache.Modified != modified)
				{
					try
					{
						cache = (path, modified, Parse(File.ReadAllText(path), path), null);
					}
					catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
					{
						Log.Write("debug", $"Failed to load faction catalog {path}: {e.Message}");
						cache = (path, modified, null, $"The shared faction catalog at {path} could not be read: {e.Message}");
					}
				}

				error = cache.Error;
				return cache.Catalog;
			}
		}
	}
}
