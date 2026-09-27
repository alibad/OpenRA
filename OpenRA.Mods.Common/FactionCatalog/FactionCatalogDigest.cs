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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenRA.Mods.Common.Experience;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Mods.Common.FactionCatalog
{
	/// <summary>
	/// Rules-derived catalog facts for the loaded game mode, shaped for the AI companion. It contains
	/// static unit knowledge only (rules and editorial text), never match state, so it cannot reveal
	/// anything hidden by fog of war.
	/// </summary>
	public static class FactionCatalogDigest
	{
		public const string Schema = "openra-ai.faction-catalog.live/1";

		static readonly JsonSerializerOptions Compact = new();
		static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

		public static Dictionary<string, object> Create(FactionCatalogView view, string engineVersion, ExperienceCatalog experience,
			int timestep, string source)
		{
			return new Dictionary<string, object>
			{
				["schema"] = Schema,
				["source"] = source,
				["mode"] = view.Mode,
				["modeName"] = view.ModeInfo?.Name ?? view.Mode,
				["engineVersion"] = engineVersion,
				["catalogRevision"] = view.Catalog?.Revision,
				["catalogAvailable"] = view.Catalog != null,
				["experience"] = experience == null ? null : new Dictionary<string, object>
				{
					["title"] = experience.ActiveTitle,
					["fingerprint"] = experience.GameplayFingerprint,
				},
				["otherModeFactions"] = view.OtherModeFactions.Select(f => new Dictionary<string, object>
				{
					["id"] = f.Id,
					["name"] = f.Name,
					["availableIn"] = f.Variants.Where(v => v.Value.IsImplemented).Select(v => v.Key).ToArray(),
				}).ToArray(),
				["factions"] = view.Factions.Select(f => Faction(f, timestep)).ToArray(),
			};
		}

		static Dictionary<string, object> Faction(CatalogFactionEntry faction, int timestep)
		{
			return new Dictionary<string, object>
			{
				["key"] = faction.Key,
				["catalogId"] = faction.Catalog?.Id,
				["internalName"] = faction.InternalName,
				["name"] = faction.Name,
				["title"] = faction.Title,
				["side"] = faction.Side,
				["kind"] = faction.Kind == CatalogFactionKind.Modern ? "modern" : "original",
				["availability"] = faction.Availability switch
				{
					CatalogAvailability.Active => "active",
					CatalogAvailability.Disabled => "disabled",
					_ => "not-installed"
				},
				["doctrine"] = faction.Doctrine,
				["description"] = faction.RulesDescription,
				["url"] = faction.WebUrl,
				["signature"] = faction.Signature?.Actor,
				["roster"] = faction.Roster.Select(r => Unit(r, timestep)).ToArray(),
			};
		}

		static Dictionary<string, object> Unit(CatalogRosterEntry entry, int timestep)
		{
			var unit = new Dictionary<string, object>
			{
				["actor"] = entry.Actor,
				["catalogUnitId"] = entry.CatalogUnit?.Id,
				["name"] = entry.DisplayName,
				["domain"] = entry.Domain,
				["role"] = entry.Role,
				["exclusive"] = entry.IsExclusive,
				["signature"] = entry.IsSignature,
				["url"] = entry.WebUrl,
				["rulesLoaded"] = entry.Stats != null,
			};

			var stats = entry.Stats;
			if (stats == null)
				return unit;

			unit["cost"] = stats.Cost;
			unit["buildTimeSeconds"] = Math.Round(stats.BuildTimeTicks * Math.Max(1, timestep) / 1000.0, 1);
			unit["hitPoints"] = stats.HitPoints;
			unit["armor"] = stats.Armor;
			unit["speed"] = stats.Speed;
			unit["sightCells"] = Math.Round(stats.Sight.Length / 1024.0, 2);
			unit["passengers"] = stats.Passengers;
			unit["power"] = stats.Power;
			unit["roles"] = stats.Roles.ToArray();
			unit["counters"] = stats.Counters.ToArray();
			unit["targets"] = stats.TargetSummary();
			unit["antiAir"] = stats.TargetsAir;
			unit["antiGround"] = stats.TargetsGround;
			unit["prerequisites"] = stats.Prerequisites.ToArray();
			unit["description"] = stats.Description;
			unit["weapons"] = stats.Weapons.Select(w => new Dictionary<string, object>
			{
				["name"] = w.Name,
				["rangeCells"] = Math.Round(w.Range.Length / 1024.0, 2),
				["minRangeCells"] = Math.Round(w.MinRange.Length / 1024.0, 2),
				["damage"] = w.Damage,
				["burst"] = w.Burst,
				["reloadTicks"] = w.ReloadDelay,
				["targets"] = w.Targets.ToArray(),
				["hits"] = CatalogUnitStats.ReadableTargets(w.Targets).ToArray(),
				["antiAir"] = w.TargetsAir,
				["antiGround"] = w.TargetsGround,
			}).ToArray();
			return unit;
		}

		public static string Serialize(Dictionary<string, object> digest, bool indented = false)
		{
			return JsonSerializer.Serialize(digest, indented ? Indented : Compact);
		}
	}

	/// <summary>Sends the live digest to the local companion so it can answer catalog questions for this mode.</summary>
	public static class FactionCatalogPublisher
	{
		static readonly Lock PublishLock = new();
		static string lastPublished;
		static long lastPublishedAt;

		public static bool CompanionRequested => Environment.GetEnvironmentVariable("OPENRA_AI_COMPANION") == "1";

		/// <summary>Builds the digest off the UI thread; rules and Fluent lookups are immutable or locked.</summary>
		public static void PublishInBackground(ModData modData, Ruleset rules, int timestep, string source)
		{
			if (CompanionRequested)
				Task.Run(() => Publish(modData, rules, timestep, source));
		}

		public static void Publish(ModData modData, Ruleset rules, int timestep, string source)
		{
			if (!CompanionRequested || modData == null || rules == null)
				return;

			string json;
			try
			{
				var catalog = SharedFactionCatalog.LoadInstalled(out var error);
				var experience = modData.GetOrNull<ExperienceCatalog>();
				var view = FactionCatalogView.Build(modData.Manifest.Id, rules, experience, catalog, key => FluentProvider.GetMessage(key), error);
				var digest = FactionCatalogDigest.Create(view, modData.Manifest.Metadata.Version, experience, timestep, source);
				json = FactionCatalogDigest.Serialize(digest);
			}
			catch (Exception e)
			{
				Log.Write("debug", "Faction catalog digest failed: " + e.Message);
				return;
			}

			// The main menu and the catalog screen can publish the same facts moments apart. Resend later
			// visits so a restarted companion is refreshed.
			lock (PublishLock)
			{
				if (json == lastPublished && Environment.TickCount64 - lastPublishedAt < 30000)
					return;

				lastPublished = json;
				lastPublishedAt = Environment.TickCount64;
			}

			_ = SendAsync(json);
		}

		static async Task SendAsync(string json)
		{
			Uri uri;
			try
			{
				uri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_CONSOLE_URL", "http://127.0.0.1:8787/");
			}
			catch (InvalidOperationException)
			{
				return;
			}

			using var payload = JsonDocument.Parse(json);

			// The companion may still be starting; a short bounded retry keeps menus responsive.
			foreach (var delay in new[] { 0, 2000, 5000, 10000 })
			{
				if (delay > 0)
					await Task.Delay(delay);

				try
				{
					using var result = await OpenRAAILocalClient.PostAsync(uri, "v1/factions/live", payload.RootElement, 8);
					return;
				}
				catch (Exception e)
				{
					Log.Write("debug", "Faction catalog digest not delivered yet: " + e.Message);
				}
			}

			lock (PublishLock)
				if (lastPublished == json)
					lastPublished = null;
		}
	}
}
