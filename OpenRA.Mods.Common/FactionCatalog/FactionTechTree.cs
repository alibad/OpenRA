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
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common.FactionCatalog
{
	/// <summary>
	/// Static reading of the loaded rules: which actors a player of one faction can produce with
	/// default lobby options and every tech level. Uses the same prerequisite grammar as <see cref="TechTree"/>
	/// ('~' hides, '!' inverts) and the same faction filters as <see cref="ProvidesPrerequisite"/>.
	/// Prerequisites that are only earned during a match (captures, promotions, support powers) are not assumed.
	/// </summary>
	public static class FactionTechTree
	{
		public static IReadOnlyCollection<ActorInfo> BuildableActors(Ruleset rules, string faction)
		{
			var world = rules.Actors[SystemActors.World];
			var player = rules.Actors[SystemActors.Player];
			var candidates = rules.Actors.Values
				.Where(a => !a.Name.StartsWith('^') && a.TraitInfoOrDefault<BuildableInfo>() is { Queue.Count: > 0 })
				.OrderBy(a => a.Name, StringComparer.Ordinal)
				.ToArray();

			var owned = new HashSet<ActorInfo>();
			foreach (var start in StartingActors(rules, world, faction))
				AddWithTransforms(rules, owned, start);

			// Grow the tree one step at a time. The provided set only grows, so an actor gated by
			// '!X' counts as buildable when it was available before X; faction-level exclusions such as
			// '~!faction.china' are granted by the player actor up front and therefore always apply.
			var buildable = new HashSet<ActorInfo>();
			var changed = true;
			while (changed)
			{
				changed = false;
				var provided = Provided(player, world, owned.Concat(buildable), faction);
				var queues = ProducibleQueues(player, owned.Concat(buildable), faction);
				foreach (var actor in candidates)
				{
					if (buildable.Contains(actor))
						continue;

					var bi = actor.TraitInfo<BuildableInfo>();
					if (bi.Queue.Any(queues.Contains) && Satisfied(bi.Prerequisites, provided))
					{
						buildable.Add(actor);
						changed = true;
					}
				}
			}

			return buildable.OrderBy(a => a.TraitInfo<BuildableInfo>().BuildPaletteOrder).ThenBy(a => a.Name, StringComparer.Ordinal).ToArray();
		}

		/// <summary>Buildable for <paramref name="faction"/> but not for any other listed faction.</summary>
		public static IReadOnlyCollection<ActorInfo> ExclusiveActors(Ruleset rules, string faction, IEnumerable<string> otherFactions)
		{
			var others = new HashSet<ActorInfo>();
			foreach (var other in otherFactions.Where(f => !string.Equals(f, faction, StringComparison.OrdinalIgnoreCase)))
				others.UnionWith(BuildableActors(rules, other));

			return BuildableActors(rules, faction).Where(a => !others.Contains(a)).ToArray();
		}

		public static bool Satisfied(IEnumerable<string> prerequisites, IReadOnlySet<string> provided)
		{
			foreach (var prerequisite in prerequisites)
			{
				var token = prerequisite.Replace("~", "");
				var inverted = token.StartsWith('!');
				token = token.Replace("!", "").ToLowerInvariant();
				if (inverted == provided.Contains(token))
					return false;
			}

			return true;
		}

		static IEnumerable<ActorInfo> StartingActors(Ruleset rules, ActorInfo world, string faction)
		{
			var entries = world.TraitInfos<StartingUnitsInfo>().ToArray();
			var matching = entries.Where(s => s.Factions.Contains(faction)).ToArray();
			if (matching.Length == 0)
				matching = entries.Where(s => s.Factions.Count == 0).ToArray();

			foreach (var entry in matching)
				foreach (var name in new[] { entry.BaseActor }.Concat(entry.SupportActors))
					if (name != null && rules.Actors.TryGetValue(name.ToLowerInvariant(), out var actor))
						yield return actor;
		}

		static void AddWithTransforms(Ruleset rules, HashSet<ActorInfo> owned, ActorInfo actor)
		{
			if (!owned.Add(actor))
				return;

			foreach (var transform in actor.TraitInfos<TransformsInfo>())
				if (transform.IntoActor != null && rules.Actors.TryGetValue(transform.IntoActor.ToLowerInvariant(), out var target))
					AddWithTransforms(rules, owned, target);
		}

		static HashSet<string> ProducibleQueues(ActorInfo player, IEnumerable<ActorInfo> owned, string faction)
		{
			var actors = owned.Prepend(player).ToArray();
			var queues = actors.SelectMany(a => a.TraitInfos<ProductionQueueInfo>())
				.Where(q => q.Factions.Count == 0 || q.Factions.Contains(faction))
				.Select(q => q.Type)
				.ToHashSet(StringComparer.Ordinal);

			// A queue also needs something that can deliver its products.
			var produced = actors.SelectMany(a => a.TraitInfos<ProductionInfo>()).SelectMany(p => p.Produces).ToHashSet(StringComparer.Ordinal);
			queues.IntersectWith(produced);
			return queues;
		}

		static HashSet<string> Provided(ActorInfo player, ActorInfo world, IEnumerable<ActorInfo> owned, string faction)
		{
			var provided = new HashSet<string>(StringComparer.Ordinal);
			var sources = owned.Prepend(world).Prepend(player).Distinct().ToArray();

			// Conditional grants (RequiresPrerequisites) may depend on other grants, so iterate to a fixed point.
			var changed = true;
			while (changed)
			{
				changed = false;
				foreach (var actor in sources)
				{
					foreach (var info in actor.TraitInfos<ITechTreePrerequisiteInfo>())
					{
						foreach (var prerequisite in Grants(actor, info, faction, provided))
							changed |= provided.Add(prerequisite.ToLowerInvariant());
					}
				}
			}

			return provided;
		}

		static IEnumerable<string> Grants(ActorInfo actor, ITechTreePrerequisiteInfo info, string faction, HashSet<string> provided)
		{
			switch (info)
			{
				case ProvidesPrerequisiteInfo p:
					if (p.Factions.Count > 0 && !p.Factions.Contains(faction))
						return [];

					if (p.RequiresPrerequisites.Length > 0 && !Satisfied(p.RequiresPrerequisites, provided))
						return [];

					return info.Prerequisites(actor);

				case ProvidesFactionDoctrineInfo d:
					return d.Factions.Contains(faction) ? info.Prerequisites(actor) : [];

				case ProvidesTechPrerequisiteInfo:
					// Every tech level is offered by the lobby; the catalog describes the complete tree.
					return info.Prerequisites(actor);

				case LobbyPrerequisiteCheckboxInfo checkbox:
					return checkbox.Enabled ? info.Prerequisites(actor) : [];

				default:
					// Captured technology, promotions and support-power grants are earned in a match.
					return [];
			}
		}
	}
}
