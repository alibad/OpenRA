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

using System.Collections.Generic;
using System.Collections.Immutable;
using OpenRA.Mods.Common.Experience;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.FileSystem
{
	[RequireExplicitImplementation]
	public interface IFileSystemExternalContent
	{
		bool InstallContentIfRequired(ModData modData);
		void ManageContent(ModData modData);
	}

	public class DefaultFileSystemLoader : IFileSystemLoader
	{
		[FieldLoader.LoadUsing(nameof(LoadPackages))]
		public readonly ImmutableArray<KeyValuePair<string, string>> Packages = default;

		static object LoadPackages(MiniYaml yaml)
		{
			var packageNode = yaml.NodeWithKeyOrDefault(nameof(Packages));
			if (packageNode == null)
				return default(ImmutableArray<KeyValuePair<string, string>>);

			var packages = new List<KeyValuePair<string, string>>(packageNode.Value.Nodes.Length);
			foreach (var node in packageNode.Value.Nodes)
				packages.Add(KeyValuePair.Create(node.Key, node.Value.Value));

			return packages.ToImmutableArray();
		}

		public void Mount(Manifest manifest, OpenRA.FileSystem.FileSystem fileSystem, ObjectCreator objectCreator)
		{
			if (Packages != null)
				foreach (var kv in Packages)
					fileSystem.Mount(kv.Key, kv.Value);

			MountExperiencePacks(manifest, fileSystem, objectCreator);
		}

		internal static void MountExperiencePacks(Manifest manifest, OpenRA.FileSystem.FileSystem fileSystem, ObjectCreator objectCreator)
		{
			if (!manifest.GlobalModData.ContainsKey("ExperienceCatalog"))
				return;
			var directory = CapabilityPackRegistry.PackDirectory(manifest.Id);
			if (CapabilityPackRegistry.Discover(manifest.Id).Count > 0)
				fileSystem.Mount(directory);
			var settings = Game.Settings.GetOrCreate<ExperienceSettings>(objectCreator, manifest.Id);
			var presentation = PresentationPackRegistry.Find(manifest.Id, settings.PresentationPack);
			if (presentation.Id != PresentationPackDefinition.Default.Id)
			{
				presentation.ValidateReplacementTargets(fileSystem.Exists);
				fileSystem.Mount(presentation.AssetsPath);
			}

		}
	}
}
