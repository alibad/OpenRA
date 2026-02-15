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
using OpenRA.Traits;
using RLProto = OpenRA.Mods.Common.RL;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// Converts protobuf AgentAction commands into OpenRA Orders and queues them via IBot.
	/// </summary>
	public sealed class ActionHandler
	{
		readonly World world;
		readonly Player player;

		public ActionHandler(World world, Player player)
		{
			this.world = world;
			this.player = player;
		}

		public void ProcessAction(RLProto.AgentAction agentAction, IBot bot)
		{
			foreach (var command in agentAction.Commands)
			{
				var order = ConvertCommand(command);
				if (order != null)
					bot.QueueOrder(order);
			}
		}

		Order ConvertCommand(RLProto.Command cmd)
		{
			switch (cmd.Action)
			{
				case RLProto.ActionType.NoOp:
					return null;

				case RLProto.ActionType.Move:
					return CreateMoveOrder(cmd);

				case RLProto.ActionType.AttackMove:
					return CreateAttackMoveOrder(cmd);

				case RLProto.ActionType.Attack:
					return CreateAttackOrder(cmd);

				case RLProto.ActionType.Stop:
					return CreateStopOrder(cmd);

				case RLProto.ActionType.Harvest:
					return CreateHarvestOrder(cmd);

				case RLProto.ActionType.Build:
				case RLProto.ActionType.Train:
					return CreateProductionOrder(cmd);

				case RLProto.ActionType.Deploy:
					return CreateDeployOrder(cmd);

				case RLProto.ActionType.Sell:
					return CreateSellOrder(cmd);

				case RLProto.ActionType.Repair:
					return CreateRepairOrder(cmd);

				case RLProto.ActionType.PlaceBuilding:
					return CreatePlaceBuildingOrder(cmd);

				case RLProto.ActionType.CancelProduction:
					return CreateCancelProductionOrder(cmd);

				case RLProto.ActionType.SetRallyPoint:
					return CreateSetRallyPointOrder(cmd);

				case RLProto.ActionType.Guard:
					return CreateGuardOrder(cmd);

				case RLProto.ActionType.SetStance:
					return CreateSetStanceOrder(cmd);

				case RLProto.ActionType.EnterTransport:
					return CreateEnterTransportOrder(cmd);

				case RLProto.ActionType.Unload:
					return CreateUnloadOrder(cmd);

				default:
					Log.Write("rl-bridge", $"Unknown action type: {cmd.Action}");
					return null;
			}
		}

		Order CreateMoveOrder(RLProto.Command cmd)
		{
			var subject = world.GetActorById(cmd.ActorId);
			if (subject == null || subject.IsDead || !subject.IsInWorld)
				return null;

			var cell = new CPos(cmd.TargetX, cmd.TargetY);
			var target = Target.FromCell(world, cell);

			return new Order("Move", subject, target, cmd.Queued);
		}

		Order CreateAttackMoveOrder(RLProto.Command cmd)
		{
			var subject = world.GetActorById(cmd.ActorId);
			if (subject == null || subject.IsDead || !subject.IsInWorld)
				return null;

			var cell = new CPos(cmd.TargetX, cmd.TargetY);
			var target = Target.FromCell(world, cell);

			return new Order("AttackMove", subject, target, cmd.Queued);
		}

		Order CreateAttackOrder(RLProto.Command cmd)
		{
			var subject = world.GetActorById(cmd.ActorId);
			if (subject == null || subject.IsDead || !subject.IsInWorld)
				return null;

			if (cmd.TargetActorId != 0)
			{
				var targetActor = world.GetActorById(cmd.TargetActorId);
				if (targetActor == null || targetActor.IsDead || !targetActor.IsInWorld)
					return null;

				return new Order("Attack", subject, Target.FromActor(targetActor), cmd.Queued);
			}

			// Attack-ground at position
			var cell = new CPos(cmd.TargetX, cmd.TargetY);
			return new Order("ForceAttack", subject, Target.FromCell(world, cell), cmd.Queued);
		}

		Order CreateStopOrder(RLProto.Command cmd)
		{
			var subject = world.GetActorById(cmd.ActorId);
			if (subject == null || subject.IsDead || !subject.IsInWorld)
				return null;

			return new Order("Stop", subject, false);
		}

		Order CreateHarvestOrder(RLProto.Command cmd)
		{
			var subject = world.GetActorById(cmd.ActorId);
			if (subject == null || subject.IsDead || !subject.IsInWorld)
				return null;

			if (cmd.TargetX != 0 || cmd.TargetY != 0)
			{
				var cell = new CPos(cmd.TargetX, cmd.TargetY);
				return new Order("Harvest", subject, Target.FromCell(world, cell), cmd.Queued);
			}

			return new Order("Harvest", subject, cmd.Queued);
		}

		Order CreateProductionOrder(RLProto.Command cmd)
		{
			if (string.IsNullOrEmpty(cmd.ItemType))
			{
				Log.Write("rl-bridge", "Production command missing item_type");
				return null;
			}

			// Find a production structure that can build this item
			foreach (var actor in world.ActorsHavingTrait<ProductionQueue>())
			{
				if (actor.Owner != player || actor.IsDead || !actor.IsInWorld)
					continue;

				foreach (var queue in actor.TraitsImplementing<ProductionQueue>())
				{
					foreach (var buildable in queue.BuildableItems())
					{
						if (string.Equals(buildable.Name, cmd.ItemType, StringComparison.OrdinalIgnoreCase))
							return Order.StartProduction(actor, cmd.ItemType, 1, cmd.Queued);
					}
				}
			}

			Log.Write("rl-bridge", $"Cannot produce '{cmd.ItemType}': no capable production structure found");
			return null;
		}

		Order CreateDeployOrder(RLProto.Command cmd)
		{
			var subject = world.GetActorById(cmd.ActorId);
			if (subject == null || subject.IsDead || !subject.IsInWorld)
				return null;

			return new Order("DeployTransform", subject, false);
		}

		Order CreateSellOrder(RLProto.Command cmd)
		{
			var subject = world.GetActorById(cmd.ActorId);
			if (subject == null || subject.IsDead || !subject.IsInWorld)
				return null;

			return new Order("Sell", subject, false) { IsImmediate = true };
		}

		Order CreateRepairOrder(RLProto.Command cmd)
		{
			var subject = world.GetActorById(cmd.ActorId);
			if (subject == null || subject.IsDead || !subject.IsInWorld)
				return null;

			return new Order("RepairBuilding", subject, false) { IsImmediate = true };
		}

		Order CreatePlaceBuildingOrder(RLProto.Command cmd)
		{
			if (string.IsNullOrEmpty(cmd.ItemType))
			{
				Log.Write("rl-bridge", "PlaceBuilding command missing item_type");
				return null;
			}

			var cell = new CPos(cmd.TargetX, cmd.TargetY);

			// Find the production queue that has this building ready
			foreach (var actor in world.ActorsHavingTrait<ProductionQueue>())
			{
				if (actor.Owner != player || actor.IsDead || !actor.IsInWorld)
					continue;

				foreach (var queue in actor.TraitsImplementing<ProductionQueue>())
				{
					foreach (var item in queue.AllQueued())
					{
						if (item.Done && string.Equals(item.Item, cmd.ItemType, StringComparison.OrdinalIgnoreCase))
						{
							return new Order("PlaceBuilding", player.PlayerActor,
								Target.FromCell(world, cell), false)
							{
								TargetString = cmd.ItemType,
								ExtraData = actor.ActorID,
							};
						}
					}
				}
			}

			Log.Write("rl-bridge", $"Cannot place '{cmd.ItemType}': no completed building in any production queue");
			return null;
		}

		Order CreateCancelProductionOrder(RLProto.Command cmd)
		{
			if (string.IsNullOrEmpty(cmd.ItemType))
			{
				Log.Write("rl-bridge", "CancelProduction command missing item_type");
				return null;
			}

			// Find the production queue producing this item
			foreach (var actor in world.ActorsHavingTrait<ProductionQueue>())
			{
				if (actor.Owner != player || actor.IsDead || !actor.IsInWorld)
					continue;

				foreach (var queue in actor.TraitsImplementing<ProductionQueue>())
				{
					foreach (var item in queue.AllQueued())
					{
						if (string.Equals(item.Item, cmd.ItemType, StringComparison.OrdinalIgnoreCase))
							return Order.CancelProduction(actor, cmd.ItemType, 1);
					}
				}
			}

			Log.Write("rl-bridge", $"Cannot cancel '{cmd.ItemType}': not in any production queue");
			return null;
		}

		Order CreateSetRallyPointOrder(RLProto.Command cmd)
		{
			var subject = world.GetActorById(cmd.ActorId);
			if (subject == null || subject.IsDead || !subject.IsInWorld)
				return null;

			var cell = new CPos(cmd.TargetX, cmd.TargetY);
			return new Order("SetRallyPoint", subject, Target.FromCell(world, cell), false);
		}

		Order CreateGuardOrder(RLProto.Command cmd)
		{
			var subject = world.GetActorById(cmd.ActorId);
			if (subject == null || subject.IsDead || !subject.IsInWorld)
				return null;

			var target = world.GetActorById(cmd.TargetActorId);
			if (target == null || target.IsDead || !target.IsInWorld)
				return null;

			return new Order("Guard", subject, Target.FromActor(target), cmd.Queued);
		}

		Order CreateSetStanceOrder(RLProto.Command cmd)
		{
			var subject = world.GetActorById(cmd.ActorId);
			if (subject == null || subject.IsDead || !subject.IsInWorld)
				return null;

			// Stance encoded in target_x: 0=HoldFire, 1=ReturnFire, 2=Defend, 3=AttackAnything
			return new Order("SetUnitStance", subject, false)
			{
				ExtraData = (uint)Math.Clamp(cmd.TargetX, 0, 3)
			};
		}

		Order CreateEnterTransportOrder(RLProto.Command cmd)
		{
			var subject = world.GetActorById(cmd.ActorId);
			if (subject == null || subject.IsDead || !subject.IsInWorld)
				return null;

			var target = world.GetActorById(cmd.TargetActorId);
			if (target == null || target.IsDead || !target.IsInWorld)
				return null;

			return new Order("EnterTransport", subject, Target.FromActor(target), cmd.Queued);
		}

		Order CreateUnloadOrder(RLProto.Command cmd)
		{
			var subject = world.GetActorById(cmd.ActorId);
			if (subject == null || subject.IsDead || !subject.IsInWorld)
				return null;

			return new Order("Unload", subject, false);
		}
	}
}
