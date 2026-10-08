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
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenRA.Mods.Common.Experience;
using OpenRA.Mods.Common.FactionCatalog;

namespace OpenRA.Mods.Common.UtilityCommands
{
	sealed class CheckFactionCatalogCommand : IUtilityCommand
	{
		static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

		string IUtilityCommand.Name => "--check-faction-catalog";

		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return args.Length >= 1 && args.Length <= 6;
		}

		[Desc("[PATH/TO/factions.json] [--digest OUTPUT.json] [--report OUTPUT.json]",
			"Validate the shared faction catalog against this mod's loaded rules and Experience faction packs. " +
			"Set OPENRA_UTILITY_EXPERIENCE_PROFILE to the catalog profile for the mode.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.ModData is set.
			var modData = Game.ModData = utility.ModData;
			string catalogPath = null, digestPath = null, reportPath = null;
			for (var i = 1; i < args.Length; i++)
			{
				if (args[i] == "--digest" && i + 1 < args.Length)
					digestPath = args[++i];
				else if (args[i] == "--report" && i + 1 < args.Length)
					reportPath = args[++i];
				else
					catalogPath = args[i];
			}

			SharedFactionCatalog catalog;
			string error = null;
			if (catalogPath != null)
			{
				catalogPath = Path.GetFullPath(catalogPath);
				catalog = SharedFactionCatalog.Parse(File.ReadAllText(catalogPath), catalogPath);
			}
			else
				catalog = SharedFactionCatalog.LoadInstalled(out error);

			if (catalog == null)
			{
				Console.WriteLine($"Error: {error}");
				Environment.Exit(1);
			}

			var mode = modData.Manifest.Id;
			var experience = modData.GetOrNull<ExperienceCatalog>();
			var profileId = catalog.Profiles.FirstOrDefault(p => p.Mode == mode)?.Id;
			if (experience != null && profileId != null && experience.ActiveProfile.Id != profileId)
				Console.WriteLine($"Warning: validating experience `{experience.ActiveProfile.Id}`; set OPENRA_UTILITY_EXPERIENCE_PROFILE={profileId}.");

			var rules = modData.DefaultRules;
			var issues = FactionCatalogValidator.Validate(mode, rules, experience, catalog);
			static string Translate(string key) => FluentProvider.TryGetMessage(key, out var message) ? message : key;
			var view = FactionCatalogView.Build(mode, rules, experience, catalog, Translate);

			foreach (var faction in view.Factions)
			{
				var exclusive = faction.Roster.Count(r => r.IsExclusive);
				Console.WriteLine($"{mode}: {faction.Kind.ToString().ToLowerInvariant()} {faction.Key} ({faction.Name}) " +
					$"{faction.Availability.ToString().ToLowerInvariant()}, {faction.Roster.Count()} roster entries, {exclusive} exclusive" +
					(faction.WebUrl != null ? $", {faction.WebUrl}" : ""));
			}

			foreach (var issue in issues)
				Console.WriteLine($"Error: {issue}");

			var report = new Dictionary<string, object>
			{
				["mode"] = mode,
				["catalog"] = catalog.SourcePath,
				["catalogRevision"] = catalog.Revision,
				["experienceProfile"] = experience?.ActiveProfile.Id,
				["valid"] = issues.Count == 0,
				["factions"] = view.Factions.Select(f => new Dictionary<string, object>
				{
					["key"] = f.Key,
					["kind"] = f.Kind.ToString().ToLowerInvariant(),
					["availability"] = f.Availability.ToString().ToLowerInvariant(),
					["roster"] = f.Roster.Count(),
					["exclusive"] = f.Roster.Where(r => r.IsExclusive).Select(r => r.Actor).ToArray(),
					["url"] = f.WebUrl,
				}).ToArray(),
				["issues"] = issues.Select(i => new Dictionary<string, string>
				{
					["code"] = i.Code,
					["subject"] = i.Subject,
					["message"] = i.Message,
				}).ToArray(),
			};

			if (reportPath != null)
				File.WriteAllText(reportPath, JsonSerializer.Serialize(report, IndentedJson));

			if (digestPath != null)
			{
				var digest = FactionCatalogDigest.Create(view, modData.Manifest.Metadata.Version, experience, 40, "utility");
				File.WriteAllText(digestPath, FactionCatalogDigest.Serialize(digest, true));
			}

			Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object>
			{
				["mode"] = mode,
				["valid"] = issues.Count == 0,
				["issues"] = issues.Count,
				["factions"] = view.Factions.Length,
			}));

			if (issues.Count > 0)
				Environment.Exit(1);
		}
	}
}
