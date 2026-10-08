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
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.FactionCatalog
{
	public enum CatalogFactionKind { Modern, Original }

	public enum CatalogAvailability
	{
		/// <summary>Faction rules are loaded by the active experience.</summary>
		Active,

		/// <summary>The faction pack exists for this game but the active experience does not load it.</summary>
		Disabled,

		/// <summary>The shared catalog lists the faction for this game but no faction pack provides it.</summary>
		NotInstalled
	}

	public sealed class CatalogRosterEntry
	{
		public readonly string Actor;
		public readonly string Domain;
		public readonly ActorInfo Info;
		public readonly CatalogUnitStats Stats;
		public readonly SharedCatalogUnit CatalogUnit;
		public readonly bool IsExclusive;
		public readonly bool IsSignature;
		public readonly string WebUrl;

		public CatalogRosterEntry(string actor, string domain, ActorInfo info, CatalogUnitStats stats, SharedCatalogUnit catalogUnit,
			bool isExclusive, bool isSignature, string webUrl)
		{
			Actor = actor;
			Domain = domain;
			Info = info;
			Stats = stats;
			CatalogUnit = catalogUnit;
			IsExclusive = isExclusive;
			IsSignature = isSignature;
			WebUrl = webUrl;
		}

		public string DisplayName => Stats?.Name ?? CatalogUnit?.Name ?? Actor;

		/// <summary>Editorial role when curated, otherwise the rules' strategic role tags.</summary>
		public string Role
		{
			get
			{
				if (!string.IsNullOrEmpty(CatalogUnit?.Role))
					return CatalogUnit.Role;

				if (Stats != null && Stats.Roles.Length > 0)
					return string.Join(", ", Stats.Roles.Take(2).Select(CatalogUnitStats.Humanize));

				return FactionCatalogView.SingularDomain(Domain);
			}
		}
	}

	public sealed class CatalogRosterGroup
	{
		public readonly string Domain;
		public readonly ImmutableArray<CatalogRosterEntry> Entries;

		public CatalogRosterGroup(string domain, ImmutableArray<CatalogRosterEntry> entries)
		{
			Domain = domain;
			Entries = entries;
		}
	}

	public sealed class CatalogFactionEntry
	{
		public readonly string Key;
		public readonly string InternalName;
		public readonly string Name;
		public readonly string Title;
		public readonly string Tagline;
		public readonly string Side;
		public readonly string Doctrine;
		public readonly string RulesDescription;
		public readonly CatalogFactionKind Kind;
		public readonly CatalogAvailability Availability;
		public readonly ExperienceComponent Pack;
		public readonly SharedCatalogFaction Catalog;
		public readonly string WebUrl;
		public readonly Color? Accent;
		public readonly ImmutableArray<CatalogRosterGroup> Groups;

		public CatalogFactionEntry(string key, string internalName, string name, string title, string tagline, string side,
			string doctrine, string rulesDescription, CatalogFactionKind kind, CatalogAvailability availability, ExperienceComponent pack,
			SharedCatalogFaction catalog, string webUrl, Color? accent, ImmutableArray<CatalogRosterGroup> groups)
		{
			Key = key;
			InternalName = internalName;
			Name = name;
			Title = title;
			Tagline = tagline;
			Side = side;
			Doctrine = doctrine;
			RulesDescription = rulesDescription;
			Kind = kind;
			Availability = availability;
			Pack = pack;
			Catalog = catalog;
			WebUrl = webUrl;
			Accent = accent;
			Groups = groups;
		}

		public IEnumerable<CatalogRosterEntry> Roster => Groups.SelectMany(g => g.Entries);

		/// <summary>Curated signature units for this mode, in shared-catalog order (the web's "Signature units").</summary>
		public IEnumerable<CatalogRosterEntry> SignatureUnits => Catalog == null ? [] : Catalog.UnitIds
			.Select(id => Roster.FirstOrDefault(e => e.CatalogUnit?.Id == id))
			.Where(e => e != null);

		public CatalogRosterEntry Signature => SignatureUnits.FirstOrDefault();
	}

	/// <summary>
	/// Mode-aware catalog for the currently loaded game: editorial text from the shared catalog joined to
	/// live rules. Factions that the shared catalog marks unavailable for this mode are not listed.
	/// </summary>
	public sealed class FactionCatalogView
	{
		public static readonly ImmutableArray<string> DomainOrder = ["Infantry", "Vehicles", "Aircraft", "Navy", "Buildings", "Defenses", "Other"];

		public readonly string Mode;
		public readonly SharedCatalogMode ModeInfo;
		public readonly SharedFactionCatalog Catalog;
		public readonly string CatalogError;
		public readonly ImmutableArray<CatalogFactionEntry> Factions;

		/// <summary>Shared-catalog factions that exist only in another game mode.</summary>
		public readonly ImmutableArray<SharedCatalogFaction> OtherModeFactions;

		/// <summary>Actors buildable by each selectable rules faction (internal name), from <see cref="FactionTechTree"/>.</summary>
		public readonly IReadOnlyDictionary<string, IReadOnlyCollection<ActorInfo>> Buildable;

		FactionCatalogView(string mode, SharedCatalogMode modeInfo, SharedFactionCatalog catalog, string catalogError,
			ImmutableArray<CatalogFactionEntry> factions, ImmutableArray<SharedCatalogFaction> otherModeFactions,
			IReadOnlyDictionary<string, IReadOnlyCollection<ActorInfo>> buildable)
		{
			Mode = mode;
			ModeInfo = modeInfo;
			Catalog = catalog;
			CatalogError = catalogError;
			Factions = factions;
			OtherModeFactions = otherModeFactions;
			Buildable = buildable;
		}

		public static IReadOnlyList<FactionInfo> PlayableFactions(Ruleset rules)
		{
			return rules.Actors[SystemActors.World].TraitInfos<FactionInfo>()
				.Where(f => f.Selectable && f.RandomFactionMembers.Count == 0 && !string.IsNullOrEmpty(f.InternalName))
				.ToArray();
		}

		/// <summary>Joins a shared-catalog faction to the faction pack that implements it in this mode.</summary>
		public static ExperienceComponent FindPack(SharedFactionCatalog catalog, SharedCatalogFaction faction, string mode,
			IEnumerable<ExperienceComponent> packs)
		{
			var candidates = packs.ToArray();
			var actors = catalog.UnitsOf(faction).Select(u => u.ActorIn(mode)).Where(a => a != null)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			return candidates.FirstOrDefault(p => actors.Count > 0 && p.Faction.Roster.Values.Any(r => r.Any(actors.Contains)))
				?? candidates.FirstOrDefault(p => string.Equals(p.Faction.InternalName, faction.Id, StringComparison.OrdinalIgnoreCase))
				?? candidates.FirstOrDefault(p => faction.Id.StartsWith(p.Faction.InternalName + "-", StringComparison.OrdinalIgnoreCase))
				?? candidates.FirstOrDefault(p => string.Equals(p.Title, faction.Name, StringComparison.OrdinalIgnoreCase));
		}

		public static FactionCatalogView Build(string mode, Ruleset rules, ExperienceCatalog experience, SharedFactionCatalog catalog,
			Func<string, string> translate, string catalogError = null)
		{
			// RA2's rules messages spell line breaks as a literal "\n" for its own tooltips.
			var lookup = translate ?? (key => key);
			translate = key => lookup(key)?.Replace("\\n", "\n");
			var targetDomains = CatalogUnitStats.TargetDomains(rules);
			var playable = PlayableFactions(rules);
			var playableByName = playable.GroupBy(f => f.InternalName, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
			var buildable = playable.Select(f => f.InternalName).Distinct(StringComparer.OrdinalIgnoreCase)
				.ToDictionary(f => f, f => FactionTechTree.BuildableActors(rules, f), StringComparer.OrdinalIgnoreCase);

			HashSet<ActorInfo> ExclusiveTo(string faction)
			{
				if (faction == null || !buildable.TryGetValue(faction, out var own))
					return [];

				var exclusive = own.ToHashSet();
				foreach (var (other, actors) in buildable)
					if (!string.Equals(other, faction, StringComparison.OrdinalIgnoreCase))
						exclusive.ExceptWith(actors);

				return exclusive;
			}

			var statsCache = new Dictionary<string, CatalogUnitStats>(StringComparer.OrdinalIgnoreCase);
			CatalogUnitStats StatsFor(ActorInfo info)
			{
				if (info == null)
					return null;

				if (!statsCache.TryGetValue(info.Name, out var stats))
					statsCache[info.Name] = stats = CatalogUnitStats.Read(rules, info, translate, targetDomains);

				return stats;
			}

			var packs = experience?.FactionPacks.Values.ToArray() ?? [];
			var joinedPacks = new HashSet<ExperienceComponent>();
			var entries = new List<CatalogFactionEntry>();
			var links = catalog?.Links ?? SharedCatalogLinks.Default;

			CatalogFactionEntry Modern(SharedCatalogFaction shared, ExperienceComponent pack)
			{
				var internalName = pack?.Faction.InternalName;
				playableByName.TryGetValue(internalName ?? "", out var factionInfo);
				var availability = pack == null ? CatalogAvailability.NotInstalled :
					factionInfo != null && (experience?.IsComponentActive(pack.Id) ?? true) ? CatalogAvailability.Active : CatalogAvailability.Disabled;

				var catalogUnits = shared == null ? [] : catalog.UnitsOf(shared).Where(u => u.ActorIn(mode) != null).ToArray();
				var exclusive = availability == CatalogAvailability.Active ? ExclusiveTo(internalName) : [];
				var ordered = new List<(string Actor, string Domain)>();
				if (pack != null)
					foreach (var category in DomainOrder)
						if (pack.Faction.Roster.TryGetValue(category, out var actors))
							foreach (var actor in actors)
								if (!ordered.Any(o => string.Equals(o.Actor, actor, StringComparison.OrdinalIgnoreCase)))
									ordered.Add((actor, category));

				foreach (var info in exclusive.OrderBy(a => a.TraitInfo<BuildableInfo>().BuildPaletteOrder).ThenBy(a => a.Name, StringComparer.Ordinal))
					if (!ordered.Any(o => string.Equals(o.Actor, info.Name, StringComparison.OrdinalIgnoreCase)))
						ordered.Add((info.Name, Classify(info, null)));

				foreach (var unit in catalogUnits)
				{
					var actor = unit.ActorIn(mode);
					if (!ordered.Any(o => string.Equals(o.Actor, actor, StringComparison.OrdinalIgnoreCase)))
					{
						rules.Actors.TryGetValue(actor.ToLowerInvariant(), out var unitInfo);
						ordered.Add((actor, Classify(unitInfo, null)));
					}
				}

				var roster = ordered.Select(o =>
				{
					var loaded = availability == CatalogAvailability.Active && rules.Actors.TryGetValue(o.Actor.ToLowerInvariant(), out var info) ? info : null;
					var unit = catalogUnits.FirstOrDefault(u => string.Equals(u.ActorIn(mode), o.Actor, StringComparison.OrdinalIgnoreCase));
					return new CatalogRosterEntry(loaded?.Name ?? o.Actor, o.Domain, loaded, StatsFor(loaded), unit,
						loaded != null && exclusive.Contains(loaded), unit != null, unit == null ? null : links.UnitUrl(unit.Id, mode));
				}).ToArray();

				var name = shared?.Name ?? (factionInfo?.Name != null ? translate(factionInfo.Name) : pack?.Title);
				var description = factionInfo?.Description != null ? translate(factionInfo.Description) : pack?.Description;
				Color? accent = shared?.Accent != null && Color.TryParse(shared.Accent.TrimStart('#'), out var parsed) ? parsed : null;
				return new CatalogFactionEntry(shared?.Id ?? "pack:" + internalName, internalName, name, shared?.Title, shared?.Tagline,
					factionInfo?.Side ?? pack?.Faction.Side, pack == null ? null : CatalogUnitStats.Humanize(pack.Faction.Doctrine), description,
					CatalogFactionKind.Modern, availability, pack, shared, shared == null ? null : links.FactionUrl(shared.Id, mode), accent,
					Group(roster));
			}

			var otherMode = ImmutableArray<SharedCatalogFaction>.Empty;
			if (catalog != null)
			{
				foreach (var shared in catalog.Factions.Where(f => f.IsImplementedIn(mode)))
				{
					var pack = FindPack(catalog, shared, mode, packs.Where(p => !joinedPacks.Contains(p)));
					if (pack != null)
						joinedPacks.Add(pack);

					entries.Add(Modern(shared, pack));
				}

				otherMode = catalog.Factions.Where(f => !f.IsImplementedIn(mode)).ToImmutableArray();
			}

			// Faction packs without a shared-catalog entry still appear, from rules only.
			foreach (var pack in packs.Where(p => !joinedPacks.Contains(p)).OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase))
				entries.Add(Modern(null, pack));

			var packNames = packs.Select(p => p.Faction.InternalName).ToHashSet(StringComparer.OrdinalIgnoreCase);
			foreach (var faction in playable.Where(f => !packNames.Contains(f.InternalName)))
			{
				var exclusive = ExclusiveTo(faction.InternalName);
				var roster = buildable[faction.InternalName]
					.Select(info => new CatalogRosterEntry(info.Name, Classify(info, null), info, StatsFor(info), null,
						exclusive.Contains(info), false, null))
					.ToArray();

				entries.Add(new CatalogFactionEntry("rules:" + faction.InternalName, faction.InternalName, translate(faction.Name), null, null,
					faction.Side, null, faction.Description != null ? translate(faction.Description) : null, CatalogFactionKind.Original,
					CatalogAvailability.Active, null, null, null, null, Group(roster)));
			}

			IReadOnlyDictionary<string, IReadOnlyCollection<ActorInfo>> buildableView = buildable
				.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
			return new FactionCatalogView(mode, catalog?.Mode(mode), catalog, catalogError, entries.ToImmutableArray(), otherMode, buildableView);
		}

		static ImmutableArray<CatalogRosterGroup> Group(IEnumerable<CatalogRosterEntry> roster)
		{
			var entries = roster.ToArray();
			return DomainOrder.Select(domain => new CatalogRosterGroup(domain, entries.Where(e => e.Domain == domain).ToImmutableArray()))
				.Where(g => g.Entries.Length > 0)
				.ToImmutableArray();
		}

		public static string Classify(ActorInfo info, string rosterCategory)
		{
			if (rosterCategory != null && DomainOrder.Contains(rosterCategory))
				return rosterCategory;

			if (info == null)
				return "Other";

			if (info.HasTraitInfo<AircraftInfo>())
				return "Aircraft";

			foreach (var queue in info.TraitInfoOrDefault<BuildableInfo>()?.Queue ?? [])
			{
				var q = queue.ToLowerInvariant();
				if (q.Contains("infantry"))
					return "Infantry";

				if (q.Contains("ship") || q.Contains("naval") || q.Contains("boat"))
					return "Navy";

				if (q.Contains("aircraft") || q.Contains("plane") || q.Contains("helicopter"))
					return "Aircraft";

				if (q.Contains("defen") || q.Contains("support"))
					return "Defenses";

				if (q.Contains("building") || q.Contains("structure"))
					return "Buildings";

				if (q.Contains("vehicle") || q.Contains("armor") || q.Contains("tank"))
					return "Vehicles";
			}

			switch (info.TraitInfoOrDefault<StrategicRoleInfo>()?.Domain)
			{
				case "infantry": return "Infantry";
				case "ground": return "Vehicles";
				case "air": return "Aircraft";
				case "naval": return "Navy";
				case "defense": return "Defenses";
				case "building": return "Buildings";
			}

			if (info.HasTraitInfo<BuildingInfo>())
				return info.HasTraitInfo<ArmamentInfo>() ? "Defenses" : "Buildings";

			return "Other";
		}

		public static string SingularDomain(string domain)
		{
			return domain switch
			{
				"Infantry" => "Infantry",
				"Vehicles" => "Vehicle",
				"Aircraft" => "Aircraft",
				"Navy" => "Naval unit",
				"Buildings" => "Structure",
				"Defenses" => "Defense",
				_ => "Unit"
			};
		}
	}
}
