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
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Traits;
using RLProto = OpenRA.Mods.Common.RL;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Serializes the current game state visible to a player into a protobuf GameObservation.
	/// </summary>
	public sealed class ObservationSerializer
	{
		const int SpatialChannelCount = 9;

		readonly World world;
		readonly Player player;
		readonly string episodeId;

		// Cached trait references for spatial map (resolved lazily)
		IResourceLayer resourceLayer;
		BuildingInfluence buildingInfluence;
		Locomotor locomotor;
		bool traitsCached;

		public ObservationSerializer(World world, Player player, string episodeId)
		{
			this.world = world;
			this.player = player;
			this.episodeId = episodeId;
		}

		void EnsureTraitsCached()
		{
			if (traitsCached)
				return;

			traitsCached = true;
			resourceLayer = world.WorldActor.TraitOrDefault<IResourceLayer>();
			buildingInfluence = world.WorldActor.TraitOrDefault<BuildingInfluence>();

			// Use first available locomotor for passability checks
			locomotor = world.WorldActor.TraitsImplementing<Locomotor>().FirstOrDefault();
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
			SerializeRememberedEnemyBuildings(obs);
			SerializeProduction(obs);
			SerializeMissionContext(obs);
			SerializeSpatialMap(obs);

			return obs;
		}

		void SerializeMissionContext(RLProto.GameObservation obs)
		{
			var missionData = world.WorldActor.Info.TraitInfoOrDefault<MissionDataInfo>();
			obs.MissionMode = missionData != null;
			if (missionData != null && !string.IsNullOrEmpty(missionData.Briefing))
				obs.MissionBriefing = FluentProvider.TryGetMessage(missionData.Briefing, out var briefing)
					? briefing
					: missionData.Briefing;

			var missionObjectives = player.PlayerActor.TraitOrDefault<MissionObjectives>();
			if (missionObjectives == null)
				return;

			for (var id = 0; id < missionObjectives.Objectives.Count; id++)
			{
				var objective = missionObjectives.Objectives[id];
				obs.Objectives.Add(new RLProto.RlObjective
				{
					Id = id,
					Description = objective.Description ?? "",
					Type = objective.Type ?? "",
					Required = objective.Required,
					State = objective.State.ToString().ToLowerInvariant(),
				});
			}
		}

		void SerializeRememberedEnemyBuildings(RLProto.GameObservation obs)
		{
			var frozenLayer = player.FrozenActorLayer;
			if (frozenLayer == null)
				return;

			foreach (var frozen in frozenLayer.FrozenActorsInRegion(world.Map.AllCells))
			{
				if (frozen.Owner == player || frozen.Owner.NonCombatant ||
					!frozen.Info.HasTraitInfo<BuildingInfo>())
					continue;

				var cell = world.Map.CellContaining(frozen.CenterPosition);
				var healthInfo = frozen.Info.TraitInfoOrDefault<HealthInfo>();
				var hpPercent = healthInfo != null && healthInfo.HP > 0
					? (float)frozen.HP / healthInfo.HP
					: 1f;

				obs.RememberedEnemyBuildings.Add(new RLProto.RlBuildingInfo
				{
					ActorId = frozen.ID,
					Type = frozen.Info.Name,
					PosX = frozen.CenterPosition.X,
					PosY = frozen.CenterPosition.Y,
					CellX = cell.X,
					CellY = cell.Y,
					HpPercent = hpPercent,
					Owner = frozen.Owner.InternalName,
				});
			}
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
				military.KillsCost = stats.KillsCost;
				military.DeathsCost = stats.DeathsCost;
				military.AssetsValue = stats.AssetsValue;
				military.Experience = stats.Experience;
				military.OrderCount = stats.OrderCount;
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
					var bldg = SerializeBuilding(actor, hpPercent, player.InternalName);
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
			var enemyCount = 0;
			var buildingCount = 0;
			foreach (var actor in world.Actors)
			{
				if (actor.Owner == player || actor.Owner.NonCombatant
					|| actor.IsDead || !actor.IsInWorld)
					continue;

				if (actor == world.WorldActor)
					continue;

				enemyCount++;
				try
				{
					var isBuilding = actor.Info.HasTraitInfo<BuildingInfo>();
					if (isBuilding) buildingCount++;
					// Check if visible through shroud/fog
					if (player.Shroud.IsVisible(actor.CenterPosition))
					{
						var health = actor.TraitOrDefault<Health>();
						var hpPercent = health != null ? (float)health.HP / health.MaxHP : 1f;

						if (actor.Info.HasTraitInfo<BuildingInfo>())
						{
							var bldg = SerializeBuilding(actor, hpPercent, actor.Owner.InternalName);
							obs.VisibleEnemyBuildings.Add(bldg);
						}
						else
						{
							var unit = SerializeUnit(actor, hpPercent, actor.Owner.InternalName);
							obs.VisibleEnemies.Add(unit);
						}
					}
				}
				catch (NullReferenceException)
				{
					// Actor may be in a transitional state — skip it
				}
			}
			if (obs.Tick % 500 == 0 || obs.Tick < 10)
				Log.Write("rl-bridge", $"Tick {obs.Tick}: {enemyCount} enemy actors, {buildingCount} buildings, {obs.VisibleEnemyBuildings.Count} visible bldgs");
		}

		RLProto.RlBuildingInfo SerializeBuilding(Actor actor, float hpPercent, string owner)
		{
			var bldg = new RLProto.RlBuildingInfo
			{
				ActorId = actor.ActorID,
				Type = actor.Info.Name,
				PosX = actor.CenterPosition.X,
				PosY = actor.CenterPosition.Y,
				HpPercent = hpPercent,
				Owner = owner,
			};

			// Cell position
			var bldgCell = world.Map.CellContaining(actor.CenterPosition);
			bldg.CellX = bldgCell.X;
			bldg.CellY = bldgCell.Y;

			// Power
			var powerTrait = actor.TraitOrDefault<Power>();
			bldg.IsPowered = powerTrait == null || !powerTrait.IsTraitDisabled;
			if (powerTrait != null)
				bldg.PowerAmount = powerTrait.GetEnabledPower();

			// Repair status
			var repair = actor.TraitOrDefault<RepairableBuilding>();
			bldg.IsRepairing = repair != null && repair.RepairActive;

			// Sell value
			var valued = actor.Info.TraitInfoOrDefault<ValuedInfo>();
			var sellable = actor.Info.TraitInfoOrDefault<SellableInfo>();
			if (valued != null && sellable != null)
				bldg.SellValue = valued.Cost * sellable.RefundPercent / 100;

			// Rally point
			var rally = actor.TraitOrDefault<RallyPoint>();
			if (rally != null && rally.Path.Count > 0)
			{
				bldg.RallyX = rally.Path[0].X;
				bldg.RallyY = rally.Path[0].Y;
			}
			else
			{
				bldg.RallyX = -1;
				bldg.RallyY = -1;
			}

			// Production status and capabilities
			var queues = actor.TraitsImplementing<ProductionQueue>();
			foreach (var queue in queues)
			{
				var current = queue.CurrentItem();
				if (current != null && !bldg.IsProducing)
				{
					bldg.IsProducing = true;
					bldg.ProducingItem = current.Item;
					bldg.ProductionProgress = current.TotalTime > 0
						? 1f - (float)current.RemainingTime / current.TotalTime
						: 0f;
				}

				foreach (var buildable in queue.BuildableItems())
					bldg.CanProduce.Add(buildable.Name);
			}

			return bldg;
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
				PassengerCount = -1,
			};

			// Cell position
			var cell = world.Map.CellContaining(actor.CenterPosition);
			unit.CellX = cell.X;
			unit.CellY = cell.Y;

			// Current activity
			var activity = actor.CurrentActivity;
			if (activity != null)
				unit.CurrentActivity = activity.GetType().Name;

			// Attack capability and range
			unit.CanAttack = actor.Info.HasTraitInfo<AttackBaseInfo>();
			var attack = actor.TraitOrDefault<AttackBase>();
			if (attack != null)
				unit.AttackRange = attack.GetMaximumRange().Length;

			// Ammo
			var ammoPool = actor.TraitOrDefault<AmmoPool>();
			if (ammoPool != null)
				unit.Ammo = ammoPool.CurrentAmmoCount;

			// Facing
			var facing = actor.TraitOrDefault<IFacing>();
			if (facing != null)
				unit.Facing = facing.Facing.Angle;

			// Veterancy
			var exp = actor.TraitOrDefault<GainsExperience>();
			if (exp != null)
				unit.ExperienceLevel = exp.Level;

			// Stance
			var autoTarget = actor.TraitOrDefault<AutoTarget>();
			if (autoTarget != null)
				unit.Stance = (int)autoTarget.Stance;

			// Speed (base speed from trait info)
			var mobileInfo = actor.Info.TraitInfoOrDefault<MobileInfo>();
			if (mobileInfo != null)
				unit.Speed = mobileInfo.Speed;

			// Cargo
			var cargo = actor.TraitOrDefault<Cargo>();
			if (cargo != null)
				unit.PassengerCount = cargo.PassengerCount;

			// Building flag (for distinguishing in mixed lists)
			unit.IsBuilding = actor.Info.HasTraitInfo<BuildingInfo>();

			unit.IsDisguised = actor.EffectiveOwner?.Disguised == true;
			unit.DisguiseOwner = unit.IsDisguised ? actor.EffectiveOwner.Owner?.InternalName ?? "" : "";
			unit.DetectsDisguise = actor.Info.HasTraitInfo<IgnoresDisguiseInfo>();

			if (actor.Owner == player)
				SerializeSpecialOrders(actor, unit);

			return unit;
		}

		void SerializeSpecialOrders(Actor actor, RLProto.RlUnitInfo unit)
		{
			var targeters = actor.TraitsImplementing<IIssueOrder>()
				.SelectMany(issuer => issuer.Orders)
				.Where(targeter => targeter.OrderID is "Disguise" or "Infiltrate" or "C4")
				.ToArray();
			unit.CanDisguise = targeters.Any(targeter => targeter.OrderID == "Disguise");
			unit.CanInfiltrate = targeters.Any(targeter => targeter.OrderID == "Infiltrate");
			unit.CanDemolish = targeters.Any(targeter => targeter.OrderID == "C4");
			if (targeters.Length == 0)
				return;

			foreach (var target in world.Actors)
			{
				if (target == actor || target.IsDead || !target.IsInWorld || target == world.WorldActor)
					continue;

				WPos centerPosition;
				try
				{
					centerPosition = target.CenterPosition;
				}
				catch (NullReferenceException)
				{
					continue;
				}

				if (target.Owner != player && !player.Shroud.IsVisible(centerPosition))
					continue;

				foreach (var targeter in targeters)
				{
					var modifiers = TargetModifiers.None;
					var cursor = "";
					if (!targeter.CanTarget(actor, Target.FromActor(target), ref modifiers, ref cursor))
						continue;

					switch (targeter.OrderID)
					{
						case "Disguise":
							unit.ValidDisguiseTargets.Add(target.ActorID);
							break;
						case "Infiltrate":
							unit.ValidInfiltrationTargets.Add(target.ActorID);
							break;
						case "C4":
							unit.ValidDemolitionTargets.Add(target.ActorID);
							break;
					}
				}
			}
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
			var bounds = world.Map.Bounds;
			return new RLProto.RlMapInfo
			{
				Width = world.Map.MapSize.Width,
				Height = world.Map.MapSize.Height,
				MapName = world.Map.Title ?? "",
				BoundsX = bounds.X,
				BoundsY = bounds.Y,
				BoundsWidth = bounds.Width,
				BoundsHeight = bounds.Height,
			};
		}

		void SerializeSpatialMap(RLProto.GameObservation obs)
		{
			EnsureTraitsCached();

			var map = world.Map;
			var width = map.MapSize.Width;
			var height = map.MapSize.Height;
			var shroud = player.Shroud;

			// Allocate spatial tensor: H × W × channels, row-major channels-last
			var data = new float[height * width * SpatialChannelCount];

			// Pre-compute actor cell positions for unit/building density layers
			var ownBuildingCells = new bool[height * width];
			var ownUnitDensity = new int[height * width];
			var enemyBuildingCells = new bool[height * width];
			var enemyUnitDensity = new int[height * width];
			var exploredCells = 0;
			var totalCells = 0;

			foreach (var actor in world.Actors)
			{
				if (actor.IsDead || !actor.IsInWorld || actor == world.WorldActor)
					continue;

				if (actor.Owner.NonCombatant)
					continue;

				CPos actorCell;
				try
				{
					actorCell = map.CellContaining(actor.CenterPosition);
				}
				catch (NullReferenceException)
				{
					continue;
				}

				if (actorCell.X < 0 || actorCell.X >= width || actorCell.Y < 0 || actorCell.Y >= height)
					continue;

				var idx = actorCell.Y * width + actorCell.X;

				if (actor.Owner == player)
				{
					if (actor.Info.HasTraitInfo<BuildingInfo>())
						ownBuildingCells[idx] = true;
					else if (actor != player.PlayerActor)
						ownUnitDensity[idx]++;
				}
				else if (shroud.IsVisible(actor.CenterPosition))
				{
					if (actor.Info.HasTraitInfo<BuildingInfo>())
						enemyBuildingCells[idx] = true;
					else
						enemyUnitDensity[idx]++;
				}
			}

			// Fill per-cell data
			foreach (var cell in map.AllCells)
			{
				var x = cell.X;
				var y = cell.Y;
				if (x < 0 || x >= width || y < 0 || y >= height)
					continue;

				var baseIdx = (y * width + x) * SpatialChannelCount;
				var cellIdx = y * width + x;
				totalCells++;
				var explored = shroud.IsExplored(cell);

				// Ch 0: Terrain type index
				data[baseIdx + 0] = map.GetTerrainIndex(cell);

				// Ch 1: Height
				data[baseIdx + 1] = map.Height[cell];

				// Ch 2: Resource density
				if (resourceLayer != null && explored)
				{
					var resource = resourceLayer.GetResource(cell);
					data[baseIdx + 2] = resource.Density;
				}

				// Ch 3: Passability (1=passable, 0=impassable)
				if (locomotor != null)
				{
					var cost = locomotor.MovementCostForCell(cell);
					data[baseIdx + 3] = cost >= PathGraph.MovementCostForUnreachableCell ? 0f : 1f;
				}

				// Ch 4: Fog of war (0=hidden, 0.5=explored, 1=visible)
				if (explored)
					exploredCells++;

				if (shroud.IsVisible(cell))
					data[baseIdx + 4] = 1f;
				else if (explored)
					data[baseIdx + 4] = 0.5f;

				// Ch 5-8: Actor density (pre-computed above)
				data[baseIdx + 5] = ownBuildingCells[cellIdx] ? 1f : 0f;
				data[baseIdx + 6] = ownUnitDensity[cellIdx];
				data[baseIdx + 7] = enemyBuildingCells[cellIdx] ? 1f : 0f;
				data[baseIdx + 8] = enemyUnitDensity[cellIdx];
			}

			// Convert float[] to byte[] for protobuf
			var bytes = new byte[data.Length * sizeof(float)];
			Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);

			obs.SpatialMap = Google.Protobuf.ByteString.CopyFrom(bytes);
			obs.SpatialChannels = SpatialChannelCount;
			obs.ExploredPercent = totalCells > 0 ? (float)exploredCells / totalCells * 100f : 0f;
		}
	}
}
