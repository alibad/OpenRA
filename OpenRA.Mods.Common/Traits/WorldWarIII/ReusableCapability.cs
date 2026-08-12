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
using System.Collections.Immutable;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Queryable metadata for reusable gameplay contracts.  This deliberately carries no
	/// simulation behavior: concrete actors compose the native traits that implement the
	/// contract, while tools, missions, and AI can discover the stable capability IDs.
	/// </summary>
	public interface IReusableCapability
	{
		string Id { get; }
		string Domain { get; }
		FrozenSet<string> Provides { get; }
		FrozenSet<string> Requires { get; }
		ImmutableArray<string> Strategies { get; }
	}

	[Desc("Declares a stable, reusable capability contract for actors, players, missions, and tooling.")]
	public sealed class ReusableCapabilityInfo : TraitInfo
	{
		[FieldLoader.Require]
		[Desc("Stable kebab-case capability identifier used by the Experience Composer and mission tooling.")]
		public readonly string Id = null;

		[Desc("Capability domain, such as economy, air, naval, mission, or effects.")]
		public readonly string Domain = "general";

		[Desc("Stable services exposed by this actor contract.")]
		public readonly FrozenSet<string> Provides = FrozenSet<string>.Empty;

		[Desc("Stable services that a concrete implementation must compose.")]
		public readonly FrozenSet<string> Requires = FrozenSet<string>.Empty;

		[Desc("AI and mission strategies that know how to consume the contract.")]
		public readonly ImmutableArray<string> Strategies = [];

		[Desc("True when enabling a concrete implementation changes synchronized gameplay.")]
		public readonly bool SimulationAffecting = true;

		public override object Create(ActorInitializer init) { return new ReusableCapability(this); }
	}

	public sealed class ReusableCapability : IReusableCapability
	{
		public ReusableCapability(ReusableCapabilityInfo info)
		{
			Id = info.Id;
			Domain = info.Domain;
			Provides = info.Provides;
			Requires = info.Requires;
			Strategies = info.Strategies;
			SimulationAffecting = info.SimulationAffecting;
		}

		public string Id { get; }
		public string Domain { get; }
		public FrozenSet<string> Provides { get; }
		public FrozenSet<string> Requires { get; }
		public ImmutableArray<string> Strategies { get; }
		public bool SimulationAffecting { get; }
	}
}
