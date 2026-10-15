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
using System.Linq;
using System.Text;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.FactionCatalog
{
	public sealed class CatalogWeaponStats
	{
		public readonly string Name;
		public readonly WDist Range;
		public readonly WDist MinRange;
		public readonly int Damage;
		public readonly int Burst;
		public readonly int ReloadDelay;
		public readonly ImmutableArray<string> Targets;
		public readonly bool TargetsAir;
		public readonly bool TargetsGround;

		public CatalogWeaponStats(string name, WDist range, WDist minRange, int damage, int burst, int reloadDelay,
			ImmutableArray<string> targets, bool targetsAir, bool targetsGround)
		{
			Name = name;
			Range = range;
			MinRange = minRange;
			Damage = damage;
			Burst = burst;
			ReloadDelay = reloadDelay;
			Targets = targets;
			TargetsAir = targetsAir;
			TargetsGround = targetsGround;
		}
	}

	/// <summary>Player-facing numbers read directly from one actor's loaded rules. Nothing here is duplicated data.</summary>
	public sealed class CatalogUnitStats
	{
		public readonly string Actor;
		public readonly string Name;
		public readonly string Description;
		public readonly int Cost;
		public readonly int BuildTimeTicks;
		public readonly int Power;
		public readonly int HitPoints;
		public readonly string Armor;
		public readonly int Speed;
		public readonly WDist Sight;
		public readonly int Passengers;
		public readonly ImmutableArray<CatalogWeaponStats> Weapons;
		public readonly ImmutableArray<string> Prerequisites;
		public readonly ImmutableArray<string> Roles;
		public readonly ImmutableArray<string> Counters;
		public readonly string StrategicDomain;
		public readonly ImmutableArray<string> Queues;
		public readonly bool IsAircraft;
		public readonly bool IsBuilding;
		public bool TargetsAir => Weapons.Any(w => w.TargetsAir);
		public bool TargetsGround => Weapons.Any(w => w.TargetsGround);

		CatalogUnitStats(string actor, string name, string description, int cost, int buildTimeTicks, int power, int hitPoints,
			string armor, int speed, WDist sight, int passengers, ImmutableArray<CatalogWeaponStats> weapons,
			ImmutableArray<string> prerequisites, ImmutableArray<string> roles, ImmutableArray<string> counters,
			string strategicDomain, ImmutableArray<string> queues, bool isAircraft, bool isBuilding)
		{
			Actor = actor;
			Name = name;
			Description = description;
			Cost = cost;
			BuildTimeTicks = buildTimeTicks;
			Power = power;
			HitPoints = hitPoints;
			Armor = armor;
			Speed = speed;
			Sight = sight;
			Passengers = passengers;
			Weapons = weapons;
			Prerequisites = prerequisites;
			Roles = roles;
			Counters = counters;
			StrategicDomain = strategicDomain;
			Queues = queues;
			IsAircraft = isAircraft;
			IsBuilding = isBuilding;
		}

		public static CatalogTargetDomains TargetDomains(Ruleset rules)
		{
			return new CatalogTargetDomains(rules.Actors.Values
				.Where(info => !info.Name.StartsWith('^'))
				.SelectMany(info => info.TraitInfos<ITargetableInfo>()).Select(info => info.GetTargetTypes()));
		}

		/// <summary>Reads one actor's statistics. <paramref name="translate"/> resolves the rules' Fluent keys.</summary>
		public static CatalogUnitStats Read(Ruleset rules, ActorInfo actor, Func<string, string> translate,
			CatalogTargetDomains targetDomains = null)
		{
			targetDomains ??= TargetDomains(rules);
			var buildable = actor.TraitInfoOrDefault<BuildableInfo>();
			var weapons = new List<CatalogWeaponStats>();
			foreach (var armament in actor.TraitInfos<ArmamentInfo>())
			{
				if (armament.Weapon == null || !rules.Weapons.TryGetValue(armament.Weapon.ToLowerInvariant(), out var weapon))
					continue;

				if (weapons.Any(w => string.Equals(w.Name, armament.Weapon, StringComparison.OrdinalIgnoreCase)))
					continue;

				var (air, ground) = targetDomains.Classify(weapon);
				var damage = weapon.Warheads.OfType<DamageWarhead>().Sum(w => w.Damage);
				var targets = weapon.ValidTargets.Where(t => !weapon.InvalidTargets.Contains(t))
					.Order(StringComparer.Ordinal).ToImmutableArray();
				weapons.Add(new CatalogWeaponStats(armament.Weapon, weapon.Range, weapon.MinRange, damage, weapon.Burst,
					weapon.ReloadDelay, targets, air, ground));
			}

			var role = actor.TraitInfoOrDefault<StrategicRoleInfo>();
			var sight = actor.TraitInfos<RevealsShroudInfo>().Select(r => r.Range).DefaultIfEmpty(WDist.Zero).Max();
			var speed = actor.TraitInfoOrDefault<MobileInfo>()?.Speed ?? actor.TraitInfoOrDefault<AircraftInfo>()?.Speed ?? 0;
			var prerequisites = buildable == null ? [] : buildable.Prerequisites
				.Where(p => !p.StartsWith('~') && !p.StartsWith('!'))
				.Select(p => ActorDisplayName(rules, p, translate))
				.ToImmutableArray();

			return new CatalogUnitStats(
				actor.Name,
				ActorDisplayName(rules, actor.Name, translate),
				string.IsNullOrEmpty(buildable?.Description) ? null : translate(buildable.Description),
				actor.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0,
				BuildTime(rules, actor),
				actor.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(i => i.Amount),
				actor.TraitInfoOrDefault<HealthInfo>()?.HP ?? 0,
				actor.TraitInfos<ArmorInfo>().Select(a => a.Type).FirstOrDefault(t => !string.IsNullOrEmpty(t)),
				speed,
				sight,
				actor.TraitInfoOrDefault<CargoInfo>()?.MaxWeight ?? 0,
				weapons.ToImmutableArray(),
				prerequisites,
				role?.Roles.Order(StringComparer.Ordinal).ToImmutableArray() ?? [],
				role?.Counters.Order(StringComparer.Ordinal).ToImmutableArray() ?? [],
				role?.Domain,
				buildable?.Queue.Order(StringComparer.Ordinal).ToImmutableArray() ?? [],
				actor.HasTraitInfo<AircraftInfo>(),
				actor.HasTraitInfo<BuildingInfo>());
		}

		public static string ActorDisplayName(Ruleset rules, string actor, Func<string, string> translate)
		{
			if (rules.Actors.TryGetValue(actor.ToLowerInvariant(), out var info))
			{
				var tooltip = info.TraitInfos<TooltipInfo>().FirstOrDefault(t => t.EnabledByDefault) ?? info.TraitInfos<TooltipInfo>().FirstOrDefault();
				if (tooltip != null && !string.IsNullOrEmpty(tooltip.Name))
					return translate(tooltip.Name);
			}

			return actor;
		}

		/// <summary>Same formula as the production palette tooltip, before power penalties.</summary>
		public static int BuildTime(Ruleset rules, ActorInfo actor)
		{
			var bi = actor.TraitInfoOrDefault<BuildableInfo>();
			if (bi == null)
				return 0;

			var time = bi.BuildDuration;
			if (time == -1)
				time = actor.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;

			var queueModifier = rules.Actors.Values.SelectMany(a => a.TraitInfos<ProductionQueueInfo>())
				.FirstOrDefault(q => bi.Queue.Contains(q.Type))?.BuildDurationModifier ?? 100;
			return time * bi.BuildDurationModifier * queueModifier / 10000;
		}

		public static string FormatCells(WDist distance)
		{
			var cells = distance.Length / 1024.0;
			return cells.ToString(cells % 1 == 0 ? "0" : "0.#", CultureInfo.InvariantCulture);
		}

		public static string FormatSeconds(int ticks, int timestep)
		{
			var seconds = ticks * Math.Max(1, timestep) / 1000.0;
			return seconds.ToString(seconds < 10 ? "0.#" : "0", CultureInfo.InvariantCulture) + "s";
		}

		/// <summary>Summaries used by the native screen and the companion digest.</summary>
		public string TargetSummary()
		{
			if (Weapons.Length == 0)
				return Passengers > 0 && IsBuilding ? "Garrison fire only" : "Unarmed";

			if (TargetsAir && TargetsGround)
				return "Air and ground";

			return TargetsAir ? "Air only" : "Ground only";
		}

		static readonly HashSet<string> IncidentalTargets = new(StringComparer.OrdinalIgnoreCase)
		{
			"Barrel", "Trees", "Tree", "Bridge", "Husk", "Repair", "NoAutoTarget", "C4", "DetonateAttack",
			"SpyInfiltrate", "Disguise", "MindControl", "TeslaBoost"
		};

		/// <summary>Player-facing target classes: `GroundActor` reads as Ground, `AirborneActor` as Air; incidental tags are dropped.</summary>
		public static IEnumerable<string> ReadableTargets(IEnumerable<string> targets)
		{
			return targets
				.Where(t => !IncidentalTargets.Contains(t))
				.Select(t => t.StartsWith("Airborne", StringComparison.Ordinal) ? "Air" : t.EndsWith("Actor", StringComparison.Ordinal) && t.Length > 5 ? t[..^5] : t)
				.Distinct(StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>Rules descriptions are wrapped for narrow tooltips; rejoin broken sentences into paragraphs.</summary>
		public static string Reflow(string text)
		{
			if (string.IsNullOrEmpty(text))
				return text;

			var lines = text.Replace("\r", "").Split('\n').Select(l => l.Trim()).ToArray();
			var result = new StringBuilder();
			for (var i = 0; i < lines.Length; i++)
			{
				result.Append(lines[i]);
				if (i == lines.Length - 1)
					break;

				// Tooltip wrapping splits sentences before a lowercase word; new facts start with a capital.
				var continues = lines[i].Length > 0 && lines[i + 1].Length > 0 && !".:!?".Contains(lines[i][^1]) &&
					(char.IsLower(lines[i + 1][0]) || char.IsDigit(lines[i + 1][0]));
				result.Append(continues ? ' ' : '\n');
			}

			return result.ToString();
		}

		public static string Humanize(string tag)
		{
			if (string.IsNullOrEmpty(tag))
				return tag;

			var words = tag.Replace('_', ' ').Replace('-', ' ');
			return char.ToUpperInvariant(words[0]) + words[1..];
		}
	}

	/// <summary>
	/// Air versus surface reach of a weapon, judged against every targetable profile in the rules.
	/// Classic marks flying actors `AirborneActor`, RA2 marks them `Air`; both count as air.
	/// </summary>
	public sealed class CatalogTargetDomains
	{
		readonly BitSet<TargetableType>[] profiles;

		public CatalogTargetDomains(IEnumerable<BitSet<TargetableType>> profiles)
		{
			this.profiles = profiles.Where(p => !p.IsEmpty).Distinct().ToArray();
		}

		public static bool IsAirProfile(BitSet<TargetableType> profile)
		{
			return profile.Any(t => t == "Air" || t.StartsWith("Airborne", StringComparison.Ordinal));
		}

		public (bool Air, bool Ground) Classify(WeaponInfo weapon)
		{
			var air = false;
			var ground = false;
			foreach (var profile in profiles)
			{
				if (!weapon.ValidTargets.Overlaps(profile) || weapon.InvalidTargets.Overlaps(profile))
					continue;

				if (IsAirProfile(profile))
					air = true;
				else
					ground = true;
			}

			return (air, ground);
		}
	}
}
