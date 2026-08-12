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

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Provides stable country, side, and doctrine prerequisites for a configured set of factions.")]
	public class ProvidesFactionDoctrineInfo : ConditionalTraitInfo, ITechTreePrerequisiteInfo
	{
		[Desc("Factions that receive the configured prerequisite contract.")]
		public readonly FrozenSet<string> Factions = FrozenSet<string>.Empty;

		[Desc("Prerequisites granted while the owner belongs to one of Factions.")]
		public readonly ImmutableArray<string> Prerequisites = [];

		[Desc("Re-evaluate the initial faction when the actor changes owner.")]
		public readonly bool ResetOnOwnerChange = true;

		IEnumerable<string> ITechTreePrerequisiteInfo.Prerequisites(ActorInfo info) => Prerequisites;

		public override object Create(ActorInitializer init) { return new ProvidesFactionDoctrine(init, this); }
	}

	public class ProvidesFactionDoctrine : ConditionalTrait<ProvidesFactionDoctrineInfo>,
		ITechTreePrerequisite, INotifyOwnerChanged, INotifyCreated
	{
		readonly string[] prerequisites;
		string faction;
		TechTree techTree;
		bool enabled;

		public ProvidesFactionDoctrine(ActorInitializer init, ProvidesFactionDoctrineInfo info)
			: base(info)
		{
			prerequisites = info.Prerequisites.Distinct().ToArray();
			faction = init.GetValue<FactionInit, string>(init.Self.Owner.Faction.InternalName);
		}

		public IEnumerable<string> ProvidesPrerequisites => enabled ? prerequisites : [];

		protected override void Created(Actor self)
		{
			// Player.PlayerActor is assigned after the Player actor's Created notifications.
			var playerActor = self.Info.Name == "player" ? self : self.Owner.PlayerActor;
			techTree = playerActor.Trait<TechTree>();
			Update();
			base.Created(self);
		}

		public void OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			techTree = newOwner.PlayerActor.Trait<TechTree>();
			if (Info.ResetOnOwnerChange)
				faction = newOwner.Faction.InternalName;

			Update();
			techTree.ActorChanged(self);
		}

		void Update()
		{
			enabled = !IsTraitDisabled && Info.Factions.Contains(faction);
		}

		protected override void TraitEnabled(Actor self)
		{
			Update();
			techTree?.ActorChanged(self);
		}

		protected override void TraitDisabled(Actor self)
		{
			Update();
			techTree?.ActorChanged(self);
		}
	}

	public interface IStrategicRole
	{
		IReadOnlyCollection<string> Roles { get; }
		IReadOnlyCollection<string> Counters { get; }
		string Domain { get; }
		int AIWeight { get; }
		int TransportWeight { get; }
		string VeterancyCurve { get; }
	}

	[Desc("Declares reusable strategic roles and balance metadata without changing actor simulation behavior.")]
	public class StrategicRoleInfo : TraitInfo
	{
		[Desc("Stable role tags used by doctrine AI, mission tooling, and composition templates.")]
		public readonly FrozenSet<string> Roles = FrozenSet<string>.Empty;

		[Desc("Role tags that this actor is designed to counter.")]
		public readonly FrozenSet<string> Counters = FrozenSet<string>.Empty;

		[Desc("Strategic domain: infantry, ground, air, naval, building, defense, logistics, or support.")]
		public readonly string Domain = "support";

		[Desc("Relative desirability used by composition-aware AI.")]
		public readonly int AIWeight = 100;

		[Desc("Cargo footprint used by transport and evacuation planning.")]
		public readonly int TransportWeight = 1;

		[Desc("Stable identifier for the intended veterancy progression.")]
		public readonly string VeterancyCurve = "standard";

		public override object Create(ActorInitializer init) { return new StrategicRole(this); }
	}

	public sealed class StrategicRole : IStrategicRole
	{
		readonly StrategicRoleInfo info;

		public StrategicRole(StrategicRoleInfo info) { this.info = info; }

		public IReadOnlyCollection<string> Roles => info.Roles;
		public IReadOnlyCollection<string> Counters => info.Counters;
		public string Domain => info.Domain;
		public int AIWeight => info.AIWeight;
		public int TransportWeight => info.TransportWeight;
		public string VeterancyCurve => info.VeterancyCurve;
	}
}
