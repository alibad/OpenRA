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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Manages bot support power handling.")]
	public class SupportPowerBotModuleInfo : ConditionalTraitInfo, Requires<SupportPowerManagerInfo>
	{
		[Desc("Tells the AI how to use its support powers.")]
		[FieldLoader.LoadUsing(nameof(LoadDecisions))]
		public readonly ImmutableArray<SupportPowerDecision> Decisions = [];

		static object LoadDecisions(MiniYaml yaml)
		{
			var ret = new List<SupportPowerDecision>();
			var decisions = yaml.NodeWithKeyOrDefault("Decisions");
			if (decisions != null)
				foreach (var d in decisions.Value.Nodes)
					ret.Add(new SupportPowerDecision(d.Value));

			return ret.ToImmutableArray();
		}

		public override object Create(ActorInitializer init) { return new SupportPowerBotModule(init.Self, this); }
	}

	public class SupportPowerBotModule : ConditionalTrait<SupportPowerBotModuleInfo>, IBotTick, IBotOrderFilter, IGameSaveTraitData
	{
		sealed class ReservedBlastZone
		{
			public readonly CPos Center;
			public readonly int Radius;
			public readonly int EvacuationPadding;
			public readonly int ExpiresAt;

			public ReservedBlastZone(CPos center, int radius, int evacuationPadding, int expiresAt)
			{
				Center = center;
				Radius = radius;
				EvacuationPadding = evacuationPadding;
				ExpiresAt = expiresAt;
			}
		}

		readonly World world;
		readonly Player player;
		readonly Dictionary<SupportPowerInstance, int> waitingPowers = [];
		readonly Dictionary<string, SupportPowerDecision> powerDecisions = [];
		readonly List<SupportPowerInstance> stalePowers = [];
		readonly List<ReservedBlastZone> reservedBlastZones = [];
		readonly Dictionary<Actor, int> evacuationCooldowns = [];
		SupportPowerManager supportPowerManager;

		public SupportPowerBotModule(Actor self, SupportPowerBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			supportPowerManager = self.Owner.PlayerActor.Trait<SupportPowerManager>();
		}

		protected override void TraitEnabled(Actor self)
		{
			// Conditional bot profiles can be switched while a match is running.
			// Rebuild the lookup so re-enabling this shared module does not retain
			// the decisions registered by the previous profile activation.
			powerDecisions.Clear();
			foreach (var decision in Info.Decisions)
				powerDecisions.Add(decision.OrderName, decision);
		}

		void IBotTick.BotTick(IBot bot)
		{
			PruneExpiredBlastZones();
			EvacuateReservedBlastZones(bot);

			foreach (var sp in supportPowerManager.Powers.Values)
			{
				if (sp.Disabled)
					continue;

				// Add power to dictionary if not in delay dictionary yet
				waitingPowers.TryAdd(sp, 0);

				if (waitingPowers[sp] > 0)
					waitingPowers[sp]--;

				// If we have recently tried and failed to find a use location for a power, then do not try again until later
				var isDelayed = waitingPowers[sp] > 0;
				if (sp.Ready && !isDelayed && powerDecisions.TryGetValue(sp.Info.OrderName, out var powerDecision))
				{
					if (powerDecision == null)
					{
						AIUtils.BotDebug($"{player.ResolvedPlayerName} couldn't find powerDecision for {sp.Info.OrderName}");
						continue;
					}

					var attackLocation = FindCoarseAttackLocationToSupportPower(sp);
					if (attackLocation == null)
					{
						AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable coarse attack location for support power {sp.Info.OrderName}. Delaying rescan.");
						waitingPowers[sp] += powerDecision.GetNextScanTime(world);

						continue;
					}

					// Found a target location, check for precise target
					var protectedPositions = GetProtectedFriendlyPositions();
					attackLocation = FindFineAttackLocationToSupportPower(sp, (CPos)attackLocation, protectedPositions);
					if (attackLocation == null)
					{
						AIUtils.BotDebug($"{player.ResolvedPlayerName} can't find suitable final attack location for support power {sp.Info.OrderName}. Delaying rescan.");
						waitingPowers[sp] += powerDecision.GetNextScanTime(world);

						continue;
					}

					// Valid target found, delay by a few ticks to avoid rescanning before power fires via order
					AIUtils.BotDebug($"{player.ResolvedPlayerName} found new target location {attackLocation} for support power {sp.Info.OrderName}.");
					waitingPowers[sp] += 10;

					// Note: SelectDirectionalTarget uses uint.MaxValue in ExtraData to indicate that the player did not pick a direction.
					bot.QueueOrder(
						new Order(sp.Key, supportPowerManager.Self, Target.FromCell(world, attackLocation.Value), false)
						{ SuppressVisualFeedback = true, ExtraData = uint.MaxValue });

					ReserveBlastZone(attackLocation.Value, powerDecision);
				}
			}

			// Remove stale powers
			stalePowers.AddRange(waitingPowers.Keys.Where(wp => !supportPowerManager.Powers.ContainsKey(wp.Key)));
			foreach (var p in stalePowers)
				waitingPowers.Remove(p);

			stalePowers.Clear();
		}

		/// <summary>Scans the map in chunks, evaluating all actors in each.</summary>
		CPos? FindCoarseAttackLocationToSupportPower(SupportPowerInstance readyPower)
		{
			var powerDecision = powerDecisions[readyPower.Info.OrderName];
			if (powerDecision == null)
			{
				AIUtils.BotDebug($"{player.ResolvedPlayerName} couldn't find powerDecision for {readyPower.Info.OrderName}");
				return null;
			}

			var map = world.Map;
			var checkRadius = powerDecision.CoarseScanRadius;
			var suitableLocations = new List<(MPos UV, int Attractiveness)>();

			for (var i = 0; i < map.MapSize.Width; i += checkRadius)
			{
				for (var j = 0; j < map.MapSize.Height; j += checkRadius)
				{
					var tl = new MPos(i, j);
					var br = new MPos(i + checkRadius, j + checkRadius);
					var region = new CellRegion(map.Grid.Type, tl, br);

					// HACK: The AI code should not be messing with raw coordinate transformations
					var wtl = world.Map.CenterOfCell(tl.ToCPos(map));
					var wbr = world.Map.CenterOfCell(br.ToCPos(map));
					var targets = world.ActorMap.ActorsInBox(wtl, wbr);

					var frozenTargets = player.FrozenActorLayer != null ? player.FrozenActorLayer.FrozenActorsInRegion(region) : [];
					var consideredAttractiveness = powerDecision.GetAttractiveness(targets, player) + powerDecision.GetAttractiveness(frozenTargets, player);
					if (consideredAttractiveness < powerDecision.MinimumAttractiveness)
						continue;

					suitableLocations.Add((tl, consideredAttractiveness));
				}
			}

			if (suitableLocations.Count == 0)
				return null;

			// Prefer the strongest coarse target instead of choosing randomly from all above-average regions.
			var maximumAttractiveness = suitableLocations.Max(x => x.Attractiveness);
			return suitableLocations
				.Where(x => x.Attractiveness == maximumAttractiveness)
				.Random(world.LocalRandom)
				.UV.ToCPos(map);
		}

		/// <summary>Detail scans an area, evaluating positions.</summary>
		CPos? FindFineAttackLocationToSupportPower(
			SupportPowerInstance readyPower, CPos checkPos, IReadOnlyCollection<WPos> protectedPositions, int extendedRange = 1)
		{
			CPos? bestLocation = null;
			var bestAttractiveness = 0;
			var powerDecision = powerDecisions[readyPower.Info.OrderName];
			if (powerDecision == null)
			{
				AIUtils.BotDebug($"{player.ResolvedPlayerName} couldn't find powerDecision for {readyPower.Info.OrderName}");
				return null;
			}

			var checkRadius = powerDecision.CoarseScanRadius;
			var fineCheck = powerDecision.FineScanRadius;
			for (var i = 0 - extendedRange; i <= checkRadius + extendedRange; i += fineCheck)
			{
				var x = checkPos.X + i;

				for (var j = 0 - extendedRange; j <= checkRadius + extendedRange; j += fineCheck)
				{
					var y = checkPos.Y + j;
					var candidate = new CPos(x, y);
					if (!world.Map.Contains(candidate) || !IsFriendlyFireSafe(candidate, powerDecision, protectedPositions))
						continue;

					var pos = world.Map.CenterOfCell(candidate);
					var consideredAttractiveness = 0;
					consideredAttractiveness += powerDecision.GetAttractiveness(pos, player);

					if (consideredAttractiveness <= bestAttractiveness || consideredAttractiveness < powerDecision.MinimumAttractiveness)
						continue;

					bestAttractiveness = consideredAttractiveness;
					bestLocation = candidate;
				}
			}

			return bestLocation;
		}

		WPos[] GetProtectedFriendlyPositions()
		{
			var positions = new HashSet<WPos>();
			foreach (var actor in world.Actors)
			{
				if (!IsProtectedFriendly(actor))
					continue;

				positions.Add(actor.CenterPosition);
				if (actor.CurrentActivity == null)
					continue;

				foreach (var target in actor.CurrentActivity.GetTargets(actor))
					if (target.Type != TargetType.Invalid)
						positions.Add(target.CenterPosition);
			}

			return positions.ToArray();
		}

		bool IsProtectedFriendly(Actor actor)
		{
			return actor != null && actor.IsInWorld && !actor.IsDead && actor.Owner != null &&
				player.RelationshipWith(actor.Owner) == PlayerRelationship.Ally &&
				actor.Info.TraitInfoOrDefault<ValuedInfo>() != null;
		}

		bool IsFriendlyFireSafe(CPos target, SupportPowerDecision decision, IEnumerable<WPos> protectedPositions)
		{
			var radius = decision.FriendlyFireSafetyRadius.Length;
			if (radius <= 0)
				return true;

			var targetPosition = world.Map.CenterOfCell(target);
			var radiusSquared = (long)radius * radius;
			return protectedPositions.All(p => (p - targetPosition).HorizontalLengthSquared > radiusSquared);
		}

		void ReserveBlastZone(CPos center, SupportPowerDecision decision)
		{
			if (decision.FriendlyFireSafetyRadius.Length <= 0 || decision.BlastZoneReservationTicks <= 0)
				return;

			var radius = (decision.FriendlyFireSafetyRadius.Length + 1023) / 1024;
			reservedBlastZones.Add(new ReservedBlastZone(
				center, radius, Math.Max(1, decision.EvacuationPadding), world.WorldTick + decision.BlastZoneReservationTicks));
		}

		void PruneExpiredBlastZones()
		{
			reservedBlastZones.RemoveAll(z => z.ExpiresAt <= world.WorldTick);
			if (reservedBlastZones.Count == 0)
				evacuationCooldowns.Clear();
		}

		void EvacuateReservedBlastZones(IBot bot)
		{
			if (reservedBlastZones.Count == 0)
				return;

			foreach (var actor in world.Actors)
			{
				if (actor.Owner != player || !IsProtectedFriendly(actor))
					continue;

				var zone = reservedBlastZones.FirstOrDefault(z => IsInsideBlastZone(actor.Location, z));
				if (zone == null || evacuationCooldowns.TryGetValue(actor, out var cooldown) && cooldown > world.WorldTick)
					continue;

				var positionable = actor.TraitOrDefault<IPositionable>();
				if (positionable == null)
					continue;

				var mobile = actor.TraitOrDefault<Mobile>();
				var minRange = zone.Radius + zone.EvacuationPadding;
				var escape = world.Map.FindTilesInAnnulus(zone.Center, minRange, minRange + 6)
					.Where(c => !IsInsideReservedBlastZone(c) &&
						positionable.CanEnterCell(c, actor, BlockedByActor.Immovable) &&
						(mobile == null || mobile.CanStayInCell(c)))
					.OrderBy(c => (c - actor.Location).LengthSquared)
					.Select(c => (CPos?)c)
					.FirstOrDefault();

				if (escape == null)
					continue;

				bot.QueueOrder(new Order("Move", actor, Target.FromCell(world, escape.Value), false));
				evacuationCooldowns[actor] = world.WorldTick + 50;
			}
		}

		bool IsInsideReservedBlastZone(CPos cell)
		{
			return reservedBlastZones.Any(z => z.ExpiresAt > world.WorldTick && IsInsideBlastZone(cell, z));
		}

		static bool IsInsideBlastZone(CPos cell, ReservedBlastZone zone)
		{
			return (cell - zone.Center).LengthSquared <= zone.Radius * zone.Radius;
		}

		bool IBotOrderFilter.AllowOrder(Order order)
		{
			if (reservedBlastZones.Count == 0 || order.Target.Type == TargetType.Invalid)
				return true;

			// Support-power orders must be able to create or overlap a reserved zone.
			if (supportPowerManager.Powers.Values.Any(p => p.Key == order.OrderString))
				return true;

			return !IsInsideReservedBlastZone(world.Map.CellContaining(order.Target.CenterPosition));
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			var waitingPowersNodes = waitingPowers
				.Select(kv => new MiniYamlNode(kv.Key.Key, FieldSaver.FormatValue(kv.Value)))
				.ToList();

			var reservedBlastZoneNodes = reservedBlastZones
				.Where(z => z.ExpiresAt > world.WorldTick)
				.Select(z => new MiniYamlNode("BlastZone", "",
				[
					new("Center", FieldSaver.FormatValue(z.Center)),
					new("Radius", FieldSaver.FormatValue(z.Radius)),
					new("EvacuationPadding", FieldSaver.FormatValue(z.EvacuationPadding)),
					new("RemainingTicks", FieldSaver.FormatValue(z.ExpiresAt - world.WorldTick))
				])).ToList();

			return
			[
				new("WaitingPowers", "", waitingPowersNodes),
				new("ReservedBlastZones", "", reservedBlastZoneNodes)
			];
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, MiniYaml data)
		{
			if (self.World.IsReplay)
				return;

			var waitingPowersNode = data.NodeWithKeyOrDefault("WaitingPowers");
			if (waitingPowersNode != null)
			{
				foreach (var n in waitingPowersNode.Value.Nodes)
				{
					if (supportPowerManager.Powers.TryGetValue(n.Key, out var instance))
						waitingPowers[instance] = FieldLoader.GetValue<int>("WaitingPowers", n.Value.Value);
				}
			}

			reservedBlastZones.Clear();
			var reservedBlastZonesNode = data.NodeWithKeyOrDefault("ReservedBlastZones");
			if (reservedBlastZonesNode != null)
			{
				foreach (var n in reservedBlastZonesNode.Value.Nodes)
				{
					var fields = n.Value.ToDictionary();
					if (!fields.TryGetValue("Center", out var centerNode) ||
						!fields.TryGetValue("Radius", out var radiusNode) ||
						!fields.TryGetValue("EvacuationPadding", out var paddingNode) ||
						!fields.TryGetValue("RemainingTicks", out var remainingNode))
						continue;

					var remaining = FieldLoader.GetValue<int>("RemainingTicks", remainingNode.Value);
					if (remaining <= 0)
						continue;

					reservedBlastZones.Add(new ReservedBlastZone(
						FieldLoader.GetValue<CPos>("Center", centerNode.Value),
						FieldLoader.GetValue<int>("Radius", radiusNode.Value),
						FieldLoader.GetValue<int>("EvacuationPadding", paddingNode.Value),
						world.WorldTick + remaining));
				}
			}
		}
	}
}
