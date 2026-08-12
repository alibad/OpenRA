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

using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common.Projectiles
{
	static class ProjectileInterception
	{
		public static bool TryIntercept(World world, WPos position, Player projectileOwner, string type, ProjectileArgs args)
		{
			if (string.IsNullOrEmpty(type))
				return false;

			foreach (var defense in world.ActorsWithTrait<IPointDefense>())
				if (defense.Trait.TryIntercept(position, projectileOwner, type, args))
					return true;

			return false;
		}
	}
}
