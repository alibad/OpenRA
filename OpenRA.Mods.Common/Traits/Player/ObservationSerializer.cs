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
using RLProto = OpenRA.Mods.Common.RL;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Serializes the current game state visible to a player into a protobuf GameObservation.
	/// </summary>
	public sealed class ObservationSerializer
	{
		readonly World world;
		readonly Player player;
		readonly string episodeId;

		public ObservationSerializer(World world, Player player, string episodeId)
		{
			this.world = world;
			this.player = player;
			this.episodeId = episodeId;
		}

		public RLProto.GameObservation Serialize(int tick)
		{
			var obs = new RLProto.GameObservation
			{
				Tick = tick,
				EpisodeId = episodeId,
				Economy = SerializeEconomy(),
				Military = SerializeMilitary(),
				MapInfo = SerializeMapInfo(),
				Done = world.IsGameOver,
			};

			if (world.IsGameOver)
			{
				obs.Result = player.WinState == WinState.Won ? "win"
					: player.WinState == WinState.Lost ? "lose"
					: "draw";
			}

			SerializeOwnedActors(obs);
			SerializeVisibleEnemies(obs);
			SerializeProduction(obs);

			return obs;
		}

		RLProto.RlEconomy SerializeEconomy()
		{
			var economy = new RLProto.RlEconomy();

			var resources = player.PlayerActor.TraitOrDefault<PlayerResources>();
			if (resources != null)
			{
				economy.Cash = resources.Cash;
				economy.Ore = resources.Resources;
				economy.ResourceCapacity = resources.ResourceCapacity;
			}

			var power = player.PlayerActor.TraitOrDefault<PowerManager>();
			if (power != null)
			{
				economy.PowerProvided = power.PowerProvided;
				economy.PowerDrained = power.PowerDrained;
			}

			// Count harvesters
			economy.HarvesterCount = world.ActorsHavingTrait<Harvester>()
				.Count(a => a.Owner == player && !a.IsDead && a.IsInWorld);

			return economy;
		}

		RLProto.RlMilitary SerializeMilitary()
		{
			var military = new RLProto.RlMilitary();

			var stats = player.PlayerActor.TraitOrDefault<PlayerStatistics>();
			if (stats != null)
			{
				military.UnitsKilled = stats.UnitsKilled;
				military.UnitsLost = stats.UnitsDead;
				military.BuildingsKilled = stats.BuildingsKilled;
				military.BuildingsLost = stats.BuildingsDead;
			}

			// Calculate army value and active unit count
			var ownedActors = world.ActorsHavingTrait<Health>()
				.Where(a => a.Owner == player && !a.IsDead && a.IsInWorld);

			var armyValue = 0;
			var unitCount = 0;
			foreach (var actor in ownedActors)
			{
				if (actor.Info.HasTraitInfo<BuildingInfo>())
					continue;

				unitCount++;

				var valued = actor.Info.TraitInfoOrDefault<ValuedInfo>();
				if (valued != null)
					armyValue += valued.Cost;
			}

			military.ArmyValue = armyValue;
			military.ActiveUnitCount = unitCount;

			return military;
		}

		void SerializeOwnedActors(RLProto.GameObservation obs)
		{
			foreach (var actor in world.Actors)
			{
				if (actor.Owner != player || actor.IsDead || !actor.IsInWorld)
					continue;

				// Skip world/player actors
				if (actor == world.WorldActor || actor == player.PlayerActor)
					continue;

				Health health;
				float hpPercent;
				try
				{
					health = actor.TraitOrDefault<Health>();
					hpPercent = health != null ? (float)health.HP / health.MaxHP : 1f;

					// Test position access early to catch transitional actors
					_ = actor.CenterPosition;
				}
				catch (NullReferenceException)
				{
					continue;
				}

				if (actor.Info.HasTraitInfo<BuildingInfo>())
				{
					var bldg = new RLProto.RlBuildingInfo
					{
						ActorId = actor.ActorID,
						Type = actor.Info.Name,
						PosX = actor.CenterPosition.X,
						PosY = actor.CenterPosition.Y,
						HpPercent = hpPercent,
						Owner = player.InternalName,
					};

					var powerTrait = actor.TraitOrDefault<Power>();
					bldg.IsPowered = powerTrait == null || !powerTrait.IsTraitDisabled;

					// Check if this building is producing
					var queues = actor.TraitsImplementing<ProductionQueue>();
					foreach (var queue in queues)
					{
						var current = queue.CurrentItem();
						if (current != null)
						{
							bldg.IsProducing = true;
							bldg.ProducingItem = current.Item;
							bldg.ProductionProgress = current.TotalTime > 0
								? 1f - (float)current.RemainingTime / current.TotalTime
								: 0f;
							break;
						}
					}

					obs.Buildings.Add(bldg);
				}
				else
				{
					var unit = SerializeUnit(actor, hpPercent, player.InternalName);
					obs.Units.Add(unit);
				}
			}
		}

		void SerializeVisibleEnemies(RLProto.GameObservation obs)
		{
			foreach (var actor in world.Actors)
			{
				if (actor.Owner == player || actor.Owner.NonCombatant
					|| actor.IsDead || !actor.IsInWorld)
					continue;

				if (actor == world.WorldActor)
					continue;

				try
				{
					// Check if visible through shroud/fog
					if (player.Shroud.IsVisible(actor.CenterPosition))
					{
						var health = actor.TraitOrDefault<Health>();
						var hpPercent = health != null ? (float)health.HP / health.MaxHP : 1f;
						var unit = SerializeUnit(actor, hpPercent, actor.Owner.InternalName);
						obs.VisibleEnemies.Add(unit);
					}
				}
				catch (NullReferenceException)
				{
					// Actor may be in a transitional state — skip it
				}
			}
		}

		RLProto.RlUnitInfo SerializeUnit(Actor actor, float hpPercent, string owner)
		{
			var unit = new RLProto.RlUnitInfo
			{
				ActorId = actor.ActorID,
				Type = actor.Info.Name,
				PosX = actor.CenterPosition.X,
				PosY = actor.CenterPosition.Y,
				HpPercent = hpPercent,
				IsIdle = actor.IsIdle,
				Owner = owner,
				Ammo = -1,
			};

			// Cell position
			var cell = world.Map.CellContaining(actor.CenterPosition);
			unit.CellX = cell.X;
			unit.CellY = cell.Y;

			// Current activity
			var activity = actor.CurrentActivity;
			if (activity != null)
				unit.CurrentActivity = activity.GetType().Name;

			// Attack capability
			unit.CanAttack = actor.Info.HasTraitInfo<AttackBaseInfo>();

			// Ammo
			var ammoPool = actor.TraitOrDefault<AmmoPool>();
			if (ammoPool != null)
				unit.Ammo = ammoPool.CurrentAmmoCount;

			return unit;
		}

		void SerializeProduction(RLProto.GameObservation obs)
		{
			// Collect production from all owned production structures
			foreach (var actor in world.ActorsHavingTrait<ProductionQueue>())
			{
				if (actor.Owner != player || actor.IsDead || !actor.IsInWorld)
					continue;

				foreach (var queue in actor.TraitsImplementing<ProductionQueue>())
				{
					foreach (var item in queue.AllQueued())
					{
						obs.Production.Add(new RLProto.RlProductionInfo
						{
							QueueType = queue.Info.Type,
							Item = item.Item,
							Progress = item.TotalTime > 0
								? 1f - (float)item.RemainingTime / item.TotalTime
								: 0f,
							RemainingTicks = item.RemainingTime,
							RemainingCost = item.RemainingCost,
							Paused = item.Paused,
						});
					}

					// Available production items
					foreach (var buildable in queue.BuildableItems())
						obs.AvailableProduction.Add(buildable.Name);
				}
			}
		}

		RLProto.RlMapInfo SerializeMapInfo()
		{
			return new RLProto.RlMapInfo
			{
				Width = world.Map.MapSize.Width,
				Height = world.Map.MapSize.Height,
				MapName = world.Map.Title ?? "",
			};
		}
	}
}
