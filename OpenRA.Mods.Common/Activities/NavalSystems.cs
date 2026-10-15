#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public class RecoverNavalActor : Activity
	{
		readonly Actor target;
		readonly string sound;

		public RecoverNavalActor(Actor target, string sound)
		{
			this.target = target;
			this.sound = sound;
		}

		public override bool Tick(Actor self)
		{
			if (target == null || target.IsDead || !target.IsInWorld)
				return true;

			var recoverable = target.TraitOrDefault<NavalRecoverable>();
			if (recoverable == null)
				return true;

			self.Owner.PlayerActor.Trait<PlayerResources>().GiveCash(recoverable.Info.Value);
			Game.Sound.Play(SoundType.World, sound, self.CenterPosition);
			target.World.AddFrameEndTask(w => target.Dispose());
			return true;
		}
	}

	public class BeginNavalTow : Activity
	{
		readonly NavalTow tow;
		readonly Actor target;

		public BeginNavalTow(NavalTow tow, Actor target)
		{
			this.tow = tow;
			this.target = target;
		}

		public override bool Tick(Actor self)
		{
			if (target != null && !target.IsDead && target.IsInWorld)
				tow.Attach(self, target);
			return true;
		}
	}
}
