#region Copyright & License Information
/*
 * Adapted from the OpenRA Combined Arms captured-faction pattern.
 * Copyright (c) The OpenRA Combined Arms Developers (see upstream CREDITS).
 * Adaptation copyright (c) OpenRA AI contributors.
 * Licensed under the GNU General Public License, version 3 or later.
 */
#endregion

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Tracks captured production technology and exposes the original faction production prerequisites.")]
	public sealed class CapturedTechnologyManagerInfo : TraitInfo, ITechTreePrerequisiteInfo
	{
		[Desc("Faction identifiers that may be captured.")]
		public readonly ImmutableArray<string> Factions = [];

		[Desc("Production prerequisite prefixes exposed for a captured faction.")]
		public readonly ImmutableArray<string> ProductionDomains =
			["infantry", "vehicles", "aircraft", "ships", "structures"];

		IEnumerable<string> ITechTreePrerequisiteInfo.Prerequisites(ActorInfo info)
		{
			foreach (var faction in Factions)
			{
				yield return $"captured.{faction}";
				foreach (var domain in ProductionDomains)
					yield return $"{domain}.{faction}";
			}
		}

		public override object Create(ActorInitializer init) { return new CapturedTechnologyManager(init.Self, this); }
	}

	public sealed class CapturedTechnologyManager : ITechTreePrerequisite, IGameSaveTraitData
	{
		readonly Actor self;
		readonly CapturedTechnologyManagerInfo info;
		readonly SortedSet<string> captured = [];
		readonly TechTree techTree;

		public CapturedTechnologyManager(Actor self, CapturedTechnologyManagerInfo info)
		{
			this.self = self;
			this.info = info;
			techTree = self.Trait<TechTree>();
		}

		public IEnumerable<string> ProvidesPrerequisites
		{
			get
			{
				foreach (var faction in captured)
				{
					yield return $"captured.{faction}";
					foreach (var domain in info.ProductionDomains)
						yield return $"{domain}.{faction}";
				}
			}
		}

		public bool AddFaction(string faction)
		{
			if (!info.Factions.Contains(faction) || !captured.Add(faction))
				return false;

			techTree.ActorChanged(self);
			return true;
		}

		public bool HasFaction(string faction) { return captured.Contains(faction); }

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor actor)
		{
			return [new("CapturedFactions", FieldSaver.FormatValue(captured.ToArray()))];
		}

		void IGameSaveTraitData.ResolveTraitData(Actor actor, MiniYaml data)
		{
			if (actor.World.IsReplay)
				return;

			var node = data.NodeWithKeyOrDefault("CapturedFactions");
			if (node == null)
				return;

			captured.Clear();
			foreach (var faction in FieldLoader.GetValue<ImmutableArray<string>>("CapturedFactions", node.Value.Value))
				if (info.Factions.Contains(faction))
					captured.Add(faction);

			techTree.ActorChanged(self);
		}
	}

	[Desc("Registers the previous owner's faction when this production structure is captured.")]
	public sealed class TracksCapturedTechnologyInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new TracksCapturedTechnology(); }
	}

	public sealed class TracksCapturedTechnology : INotifyOwnerChanged
	{
		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			if (oldOwner == null || newOwner == null || oldOwner == newOwner || oldOwner.NonCombatant)
				return;

			foreach (var manager in newOwner.PlayerActor.TraitsImplementing<CapturedTechnologyManager>())
				manager.AddFaction(oldOwner.Faction.InternalName);
		}
	}
}
