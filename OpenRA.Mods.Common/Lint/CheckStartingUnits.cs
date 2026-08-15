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
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Lint
{
	sealed class CheckStartingUnits : ILintRulesPass
	{
		void ILintRulesPass.Run(Action<string> emitError, Action<string> emitWarning, ModData modData, Ruleset rules)
		{
			var world = rules.Actors[SystemActors.World];
			var startingUnits = world.TraitInfos<StartingUnitsInfo>().ToArray();
			if (startingUnits.Length == 0 || !world.HasTraitInfo<SpawnStartingUnitsInfo>())
				return;

			var classes = startingUnits.Select(s => s.Class).Distinct();
			var factions = world.TraitInfos<FactionInfo>()
				.Where(f => f.Selectable && f.RandomFactionMembers.Count == 0);

			foreach (var faction in factions)
				foreach (var startingUnitsClass in classes)
					if (!startingUnits.Any(s => s.Class == startingUnitsClass && s.Factions.Contains(faction.InternalName)))
						emitError($"No starting units defined for selectable faction `{faction.InternalName}` " +
							$"with class `{startingUnitsClass}`.");
		}
	}
}
