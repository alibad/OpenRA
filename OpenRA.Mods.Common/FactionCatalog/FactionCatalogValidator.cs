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

namespace OpenRA.Mods.Common.FactionCatalog
{
	public sealed class FactionCatalogIssue
	{
		public readonly string Code;
		public readonly string Subject;
		public readonly string Message;

		public FactionCatalogIssue(string code, string subject, string message)
		{
			Code = code;
			Subject = subject;
			Message = message;
		}

		public override string ToString() => $"{Code} [{Subject}]: {Message}";
	}

	/// <summary>
	/// Consistency between the shared catalog, the Experience faction packs and the loaded rules for one game mode.
	/// Every issue is an error: the in-game catalog would otherwise show a unit that cannot exist, hide a
	/// buildable faction unit, or link to a malformed web page.
	/// </summary>
	public static class FactionCatalogValidator
	{
		public static IReadOnlyList<FactionCatalogIssue> Validate(string mode, Ruleset rules, ExperienceCatalog experience,
			SharedFactionCatalog catalog, string profileId = null)
		{
			var issues = new List<FactionCatalogIssue>();
			void Fail(string code, string subject, string message) => issues.Add(new FactionCatalogIssue(code, subject, message));

			if (catalog.Mode(mode) == null)
			{
				Fail("unknown-mode", mode, $"the shared catalog does not describe game mode `{mode}`");
				return issues;
			}

			var linkProblem = catalog.Links.Problem();
			if (linkProblem != null)
				Fail("malformed-link", "links", linkProblem);

			var view = FactionCatalogView.Build(mode, rules, experience, catalog, null);
			var implemented = catalog.Factions.Where(f => f.IsImplementedIn(mode)).ToArray();

			// Profile membership: the catalog profile for this mode must enable exactly its implemented factions.
			var profile = profileId == null ? catalog.Profiles.FirstOrDefault(p => p.Mode == mode) :
				catalog.Profiles.FirstOrDefault(p => p.Id == profileId);
			if (profile == null)
				Fail("missing-profile", mode, "no shared-catalog profile describes this mode");
			else
			{
				if (profile.Mode != mode)
					Fail("profile-mode", profile.Id, $"profile belongs to `{profile.Mode}`, not `{mode}`");

				foreach (var faction in implemented.Where(f => !profile.FactionIds.Contains(f.Id)))
					Fail("profile-membership", faction.Id, $"implemented in `{mode}` but missing from profile `{profile.Id}`");

				foreach (var id in profile.FactionIds.Where(id => !implemented.Any(f => f.Id == id)))
					Fail("profile-membership", id, $"listed by profile `{profile.Id}` but not implemented in `{mode}`");

				if (experience != null && !experience.Profiles.ContainsKey(profile.Id))
					Fail("missing-experience-profile", profile.Id, $"the `{mode}` Experience catalog has no profile `{profile.Id}`");
			}

			foreach (var entry in view.Factions.Where(e => e.Kind == CatalogFactionKind.Modern))
			{
				var subject = entry.Catalog?.Id ?? entry.InternalName;
				if (entry.Catalog == null)
				{
					Fail("faction-missing-from-catalog", subject,
						$"faction pack `{entry.Pack.Id}` provides a buildable `{mode}` faction that the shared catalog does not list as implemented");
					continue;
				}

				if (entry.Availability == CatalogAvailability.NotInstalled)
				{
					Fail("faction-without-rules", subject, $"implemented in `{mode}` but no faction pack provides it");
					continue;
				}

				if (entry.Availability == CatalogAvailability.Disabled)
				{
					Fail("faction-not-loaded", subject,
						$"faction pack `{entry.Pack.Id}` is not active in the validated `{mode}` experience; validate with its catalog profile");
					continue;
				}

				var linkIssue = SharedCatalogLinks.LinkProblem(entry.WebUrl, catalog.Links.Origin);
				if (linkIssue != null)
					Fail("malformed-link", subject, linkIssue);

				var buildable = view.Buildable.TryGetValue(entry.InternalName, out var actors) ? actors.ToHashSet() : [];
				foreach (var unit in catalog.UnitsOf(entry.Catalog))
				{
					var actor = unit.ActorIn(mode);
					if (actor == null)
					{
						Fail("unit-missing-mode", unit.Id, $"faction is implemented in `{mode}` but the unit has no `{mode}` actor");
						continue;
					}

					if (!rules.Actors.TryGetValue(actor.ToLowerInvariant(), out var info))
					{
						Fail("unit-missing-actor", unit.Id, $"`{mode}` actor `{actor}` does not exist in the loaded rules");
						continue;
					}

					if (!buildable.Contains(info))
						Fail("unit-not-buildable", unit.Id, $"`{actor}` is not buildable by `{entry.InternalName}` in the loaded rules");

					if (!entry.Pack.Faction.Roster.Values.Any(r => r.Contains(actor, StringComparer.OrdinalIgnoreCase)))
						Fail("unit-not-in-roster", unit.Id, $"`{actor}` is missing from faction pack `{entry.Pack.Id}` roster");

					linkIssue = SharedCatalogLinks.LinkProblem(catalog.Links.UnitUrl(unit.Id, mode), catalog.Links.Origin);
					if (linkIssue != null)
						Fail("malformed-link", unit.Id, linkIssue);
				}

				foreach (var (category, actorsInCategory) in entry.Pack.Faction.Roster)
					foreach (var actor in actorsInCategory.Where(a => !rules.Actors.ContainsKey(a.ToLowerInvariant())))
						Fail("roster-missing-actor", subject, $"{category} roster actor `{actor}` does not exist in the loaded rules");

				// Exclusive = buildable by this faction and by no other playable faction.
				var listed = entry.Pack.Faction.Roster.Values.SelectMany(r => r)
					.Concat(catalog.UnitsOf(entry.Catalog).Select(u => u.ActorIn(mode)).Where(a => a != null))
					.ToHashSet(StringComparer.OrdinalIgnoreCase);
				foreach (var rosterEntry in entry.Roster.Where(r => r.IsExclusive && !listed.Contains(r.Actor)))
					Fail("buildable-missing-from-catalog", subject,
						$"`{rosterEntry.Actor}` ({rosterEntry.DisplayName}) is buildable only by `{entry.InternalName}` but is missing from the catalog roster");
			}

			// A unit variant for a mode where its faction is unavailable would leak into the wrong game.
			foreach (var faction in catalog.Factions)
				foreach (var unit in catalog.UnitsOf(faction))
					foreach (var variantMode in unit.ActorIds.Keys.Where(m => !faction.IsImplementedIn(m)).Order())
						Fail("unit-in-unavailable-mode", unit.Id, $"has a `{variantMode}` actor but faction `{faction.Id}` is unavailable in `{variantMode}`");

			return issues;
		}
	}
}
