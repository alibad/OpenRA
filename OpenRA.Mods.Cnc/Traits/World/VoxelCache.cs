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
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Cnc.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	[Desc("Loads voxel models.")]
	public sealed class VoxelCacheInfo : TraitInfo, IModelCacheInfo
	{
		public readonly int SheetSize = 2048;
		public override object Create(ActorInitializer init) { return new VoxelCache(this, init.Self); }
	}

	public sealed class VoxelCache : IModelCache, INotifyActorDisposing, IDisposable
	{
		/// <summary>A loader and the models loaded into it, with the number of worlds using them.</summary>
		sealed class LoadedModels
		{
			public (object ModelSequences, int SheetSize)? Key;
			public VoxelLoader Loader;
			public readonly Dictionary<string, Dictionary<string, IModel>> Models = [];
			public int Users;
		}

		/// <summary>
		/// Loading every model (decoded into sheets and vertex buffers: tens of MB of allocations and much of the time a
		/// world takes to create) is shared by successive worlds with the same model rules, unless a map ships model
		/// files of its own. The last set loaded is kept for the next world; a set no world uses any more is freed once
		/// another one replaces it.
		/// </summary>
		static LoadedModels shared;

		readonly LoadedModels loaded;
		readonly VoxelLoader loader;
		readonly Dictionary<string, Dictionary<string, IModel>> models;
		bool disposed;

		public VoxelCache(VoxelCacheInfo info, Actor self)
		{
			var map = self.World.Map;

			// A map's own model files would make its models differ from another map's under the same rules.
			var ownModels = map.Package.Contents.Any(f =>
				f.EndsWith(".vxl", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".hva", StringComparison.OrdinalIgnoreCase));
			(object ModelSequences, int SheetSize)? key = ownModels ? null : (map.Rules.ModelSequences, info.SheetSize);
			if (key != null && shared != null && shared.Key.Equals(key))
			{
				loaded = shared;
				loaded.Users++;
				loader = loaded.Loader;
				models = loaded.Models;
				return;
			}

			loaded = new LoadedModels { Key = key, Loader = new VoxelLoader(ownModels ? map : Game.ModData.DefaultFileSystem, info.SheetSize), Users = 1 };
			loader = loaded.Loader;
			models = loaded.Models;
			foreach (var kv in map.Rules.ModelSequences)
			{
				Game.ModData.LoadScreen.Display();
				try
				{
					CacheModel(kv.Key, kv.Value.Value);
				}
				catch (FileNotFoundException ex)
				{
					// Eat the FileNotFound exceptions from missing sprites.
					Console.WriteLine(ex);
					Log.Write("debug", ex.Message);
				}
			}

			loader.RefreshBuffer();
			loader.Finish();

			if (key != null)
			{
				if (shared != null && shared.Users == 0)
					shared.Loader.Dispose();

				shared = loaded;
			}
		}

		public void CacheModel(string model, MiniYaml definition)
		{
			models.Add(model, definition.ToDictionary(my => LoadVoxel(model, my)));
		}

		IModel LoadVoxel(string unit, MiniYaml info)
		{
			var vxl = unit;
			var hva = unit;
			if (info.Value != null)
			{
				var fields = info.Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
				if (fields.Length >= 1)
					vxl = hva = fields[0].Trim();

				if (fields.Length >= 2)
					hva = fields[1].Trim();
			}

			return loader.Load(vxl, hva);
		}

		public IModel GetModel(string model)
		{
			return loader.Load(model, model);
		}

		public IModel GetModelSequence(string model, string sequence)
		{
			try { return models[model][sequence]; }
			catch (KeyNotFoundException)
			{
				if (models.ContainsKey(model))
					throw new InvalidOperationException(
						$"Model `{model}` does not have a sequence `{sequence}`");
				else
					throw new InvalidOperationException(
						$"Model `{model}` does not have any sequences defined.");
			}
		}

		public bool HasModelSequence(string model, string sequence)
		{
			if (!models.TryGetValue(model, out var sequences))
				throw new InvalidOperationException(
					$"Model `{model}` does not have any sequences defined.");

			return sequences.ContainsKey(sequence);
		}

		public IVertexBuffer<ModelVertex> VertexBuffer => loader.VertexBuffer;

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;
			loaded.Users--;

			// The shared set stays loaded for the next world; any other set goes with the last world using it.
			if (loaded != shared && loaded.Users == 0)
				loader.Dispose();
		}

		void INotifyActorDisposing.Disposing(Actor a)
		{
			Dispose();
		}
	}
}
