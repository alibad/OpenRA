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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OpenRA.Mods.Common.Experience
{
	public sealed class PresentationPackDefinition
	{
		static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".aud", ".bmp", ".dds", ".flac", ".jpeg", ".jpg", ".mp3", ".ogg",
			".pal", ".pcx", ".png", ".shp", ".tga", ".vqa", ".wav"
		};

		public readonly string Id;
		public readonly string Title;
		public readonly string Version;
		public readonly string Author;
		public readonly string License;
		public readonly string Description;
		public readonly string RootPath;
		public readonly string AssetsPath;
		public readonly ImmutableArray<string> Replaces;
		public readonly string Fingerprint;

		PresentationPackDefinition()
		{
			Id = "default";
			Title = "Original presentation";
			Version = "1";
			Author = "OpenRA AI";
			License = "Project defaults";
			Description = "Uses the standard images, sounds, interface, and mouse cursors.";
			Replaces = [];
			Fingerprint = HashText("presentation-pack:default:1");
		}

		PresentationPackDefinition(string rootPath, MiniYaml yaml)
		{
			RootPath = Path.GetFullPath(rootPath);
			Id = Required(yaml, "Id");
			Title = Required(yaml, "Title");
			Version = Required(yaml, "Version");
			Author = Required(yaml, "Author");
			License = Required(yaml, "License");
			Description = Value(yaml, "Description", "Custom images, sounds, and cursors.");
			var assetsFolder = Value(yaml, "Assets", "assets");

			if (!IsSafeId(Id))
				throw new InvalidDataException($"Presentation pack id `{Id}` may contain only letters, digits, '-' and '_'.");

			if (Path.IsPathRooted(assetsFolder) || assetsFolder.Split('/', '\\').Any(p => p == ".."))
				throw new InvalidDataException("Presentation pack Assets must be a relative path inside the pack.");

			AssetsPath = Path.GetFullPath(Path.Combine(RootPath, assetsFolder));
			if (!AssetsPath.StartsWith(RootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException("Presentation pack Assets resolves outside the pack.");

			if (!Directory.Exists(AssetsPath))
				throw new InvalidDataException($"Presentation pack assets folder `{assetsFolder}` does not exist.");

			RejectReparsePoints(RootPath, AssetsPath);

			var replaces = ParseList(yaml, "Replaces").Select(NormalizeVirtualPath).ToImmutableArray();
			if (replaces.Length == 0)
				throw new InvalidDataException("Presentation pack must declare every overridden file in Replaces.");

			if (replaces.Distinct(StringComparer.OrdinalIgnoreCase).Count() != replaces.Length)
				throw new InvalidDataException("Presentation pack Replaces contains duplicate paths.");

			var files = Directory.GetFiles(AssetsPath, "*", SearchOption.AllDirectories)
				.Select(f => NormalizeVirtualPath(Path.GetRelativePath(AssetsPath, f)))
				.Order(StringComparer.Ordinal)
				.ToArray();

			foreach (var file in files)
				if (!AllowedExtensions.Contains(Path.GetExtension(file)))
					throw new InvalidDataException($"Presentation pack file `{file}` has a prohibited or unsupported extension.");

			var undeclared = files.Except(replaces, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
			if (undeclared != null)
				throw new InvalidDataException($"Presentation pack file `{undeclared}` is not declared in Replaces.");

			var missing = replaces.Except(files, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
			if (missing != null)
				throw new InvalidDataException($"Presentation pack replacement `{missing}` is missing from the assets folder.");

			Replaces = replaces;
			Fingerprint = HashPack(files);
		}

		public static PresentationPackDefinition Default { get; } = new();

		public static PresentationPackDefinition Load(string rootPath)
		{
			var manifestPath = Path.Combine(rootPath, "pack.yaml");
			if (!File.Exists(manifestPath))
				throw new InvalidDataException("Presentation pack does not contain pack.yaml.");

			var root = MiniYaml.FromFile(manifestPath, false).SingleOrDefault(n => n.Key == "PresentationPack");
			if (root == null)
				throw new InvalidDataException("pack.yaml must contain one PresentationPack node.");

			return new PresentationPackDefinition(rootPath, root.Value);
		}

		public void ValidateReplacementTargets(Func<string, bool> exists)
		{
			var missingTarget = Replaces.FirstOrDefault(r => !exists(r));
			if (missingTarget != null)
				throw new InvalidDataException($"Presentation pack targets unknown asset `{missingTarget}`.");
		}

		string HashPack(IEnumerable<string> files)
		{
			using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			Append(hash, $"{Id}\n{Version}\n{Author}\n{License}\n");
			foreach (var relativePath in files)
			{
				Append(hash, relativePath + "\n");
				using var stream = File.OpenRead(Path.Combine(AssetsPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
				var buffer = new byte[81920];
				int read;
				while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
					hash.AppendData(buffer, 0, read);
			}

			return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
		}

		static void Append(IncrementalHash hash, string value)
		{
			hash.AppendData(Encoding.UTF8.GetBytes(value));
		}

		static string HashText(string value)
		{
			return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
		}

		static string Required(MiniYaml yaml, string key)
		{
			var value = Value(yaml, key, null);
			if (string.IsNullOrWhiteSpace(value))
				throw new InvalidDataException($"Presentation pack field `{key}` is required.");

			return value.Trim();
		}

		static string Value(MiniYaml yaml, string key, string fallback)
		{
			return yaml.NodeWithKeyOrDefault(key)?.Value.Value ?? fallback;
		}

		static IEnumerable<string> ParseList(MiniYaml yaml, string key)
		{
			var node = yaml.NodeWithKeyOrDefault(key);
			if (node == null || string.IsNullOrWhiteSpace(node.Value.Value))
				return [];

			return node.Value.Value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0);
		}

		static string NormalizeVirtualPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
				throw new InvalidDataException("Presentation pack replacement paths must be relative.");

			var normalized = path.Replace('\\', '/').TrimStart('/');
			if (normalized.Split('/').Any(p => p is "" or "." or ".."))
				throw new InvalidDataException($"Presentation pack path `{path}` is invalid.");

			return normalized;
		}

		static bool IsSafeId(string id)
		{
			return !string.IsNullOrEmpty(id) && id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
		}

		static void RejectReparsePoints(string root, string assets)
		{
			var paths = new[] { root, assets, Path.Combine(root, "pack.yaml") }
				.Concat(Directory.GetDirectories(assets, "*", SearchOption.AllDirectories))
				.Concat(Directory.GetFiles(assets, "*", SearchOption.AllDirectories));
			foreach (var path in paths)
				if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
					throw new InvalidDataException("Presentation packs may not contain symbolic links or filesystem junctions.");
		}
	}

	public static class PresentationPackRegistry
	{
		public const string MountName = "experience-presentation";

		public static string PackDirectory(string modId)
		{
			return Path.Combine(Platform.SupportDir, "ExperiencePacks", modId);
		}

		public static IReadOnlyDictionary<string, PresentationPackDefinition> Discover(string modId)
		{
			var result = new Dictionary<string, PresentationPackDefinition>(StringComparer.OrdinalIgnoreCase)
			{
				{ PresentationPackDefinition.Default.Id, PresentationPackDefinition.Default }
			};

			var packDirectory = PackDirectory(modId);
			Directory.CreateDirectory(packDirectory);
			foreach (var directory in Directory.GetDirectories(packDirectory).Order(StringComparer.Ordinal))
			{
				try
				{
					var pack = PresentationPackDefinition.Load(directory);
					if (!Path.GetFileName(directory).Equals(pack.Id, StringComparison.OrdinalIgnoreCase))
						throw new InvalidDataException($"Folder name must match pack id `{pack.Id}`.");

					if (!result.TryAdd(pack.Id, pack))
						throw new InvalidDataException($"Duplicate presentation pack id `{pack.Id}`.");
				}
				catch (Exception e)
				{
					Log.Write("debug", $"Ignoring presentation pack `{directory}`: {e.Message}");
				}
			}

			return result;
		}

		public static PresentationPackDefinition Find(string modId, string id)
		{
			var packs = Discover(modId);
			return id != null && packs.TryGetValue(id, out var pack) ? pack : PresentationPackDefinition.Default;
		}
	}
}
