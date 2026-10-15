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

		public static bool IsSupportedAsset(string filename) => AllowedExtensions.Contains(Path.GetExtension(filename));

		public static string NormalizeReplacementPath(string path) => NormalizeVirtualPath(path);

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
		const long MaximumReplacementBytes = 256 * 1024 * 1024;

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

		public static PresentationPackDefinition Create(string modId, string title)
		{
			if (string.IsNullOrWhiteSpace(title))
				throw new InvalidDataException("Presentation pack title is required.");

			var id = UniqueId(modId, Slug(title));
			var root = SafePackPath(modId, id);
			Directory.CreateDirectory(Path.Combine(root, "assets"));
			WriteManifest(root, id, title.Trim(), "1", "Local creator",
				"Private local use; verify redistribution rights before sharing.",
				"A locally composed presentation pack.", []);
			return PresentationPackDefinition.Load(root);
		}

		public static PresentationPackDefinition Duplicate(string modId, string sourceId, string title)
		{
			var source = FindEditable(modId, sourceId);
			var id = UniqueId(modId, Slug(title));
			var destination = SafePackPath(modId, id);
			CopyDirectory(source.RootPath, destination);
			WriteManifest(destination, id, title.Trim(), source.Version, source.Author, source.License,
				source.Description, source.Replaces);
			return PresentationPackDefinition.Load(destination);
		}

		public static PresentationPackDefinition Rename(string modId, string sourceId, string title)
		{
			var source = FindEditable(modId, sourceId);
			var id = Slug(title);
			if (!id.Equals(source.Id, StringComparison.OrdinalIgnoreCase))
				id = UniqueId(modId, id);

			var destination = SafePackPath(modId, id);
			if (!source.RootPath.Equals(destination, StringComparison.OrdinalIgnoreCase))
				Directory.Move(source.RootPath, destination);

			WriteManifest(destination, id, title.Trim(), source.Version, source.Author, source.License,
				source.Description, source.Replaces);
			return PresentationPackDefinition.Load(destination);
		}

		public static void Delete(string modId, string id)
		{
			var pack = FindEditable(modId, id);
			var expected = SafePackPath(modId, pack.Id);
			if (!pack.RootPath.Equals(expected, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException("Presentation pack path is outside the managed pack directory.");

			Directory.Delete(pack.RootPath, true);
		}

		public static PresentationPackDefinition AddOrUpdateReplacement(
			string modId, string id, string target, string sourceName, Stream source)
		{
			var pack = FindEditable(modId, id);
			target = PresentationPackDefinition.NormalizeReplacementPath(target);
			if (!PresentationPackDefinition.IsSupportedAsset(target))
				throw new InvalidDataException($"Replacement target `{target}` uses an unsupported format.");

			if (!Path.GetExtension(sourceName).Equals(Path.GetExtension(target), StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException("Replacement source and target must use the same file format.");

			using var buffer = new MemoryStream();
			source.CopyTo(buffer);
			if (buffer.Length > MaximumReplacementBytes)
				throw new InvalidDataException("Replacement files may not exceed 256 MB.");

			var destination = Path.GetFullPath(Path.Combine(pack.AssetsPath,
				target.Replace('/', Path.DirectorySeparatorChar)));
			if (!destination.StartsWith(pack.AssetsPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException("Replacement target resolves outside the pack assets folder.");

			Directory.CreateDirectory(Path.GetDirectoryName(destination));
			File.WriteAllBytes(destination, buffer.ToArray());
			var replacements = pack.Replaces.Append(target).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
			WriteManifest(pack.RootPath, pack.Id, pack.Title, pack.Version, pack.Author, pack.License,
				pack.Description, replacements);
			return PresentationPackDefinition.Load(pack.RootPath);
		}

		public static PresentationPackDefinition AddOrUpdateReplacementFromFile(
			string modId, string id, string target, string sourcePath)
		{
			if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
				throw new FileNotFoundException("Replacement source file was not found.", sourcePath);

			using var stream = new MemoryStream(File.ReadAllBytes(sourcePath));
			return AddOrUpdateReplacement(modId, id, target, sourcePath, stream);
		}

		public static PresentationPackDefinition RemoveReplacement(string modId, string id, string target)
		{
			var pack = FindEditable(modId, id);
			target = PresentationPackDefinition.NormalizeReplacementPath(target);
			var destination = Path.GetFullPath(Path.Combine(pack.AssetsPath,
				target.Replace('/', Path.DirectorySeparatorChar)));
			if (!destination.StartsWith(pack.AssetsPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException("Replacement target resolves outside the pack assets folder.");

			if (File.Exists(destination))
				File.Delete(destination);

			var replacements = pack.Replaces.Where(r => !r.Equals(target, StringComparison.OrdinalIgnoreCase)).ToArray();
			WriteManifest(pack.RootPath, pack.Id, pack.Title, pack.Version, pack.Author, pack.License,
				pack.Description, replacements);
			return PresentationPackDefinition.Load(pack.RootPath);
		}

		static PresentationPackDefinition FindEditable(string modId, string id)
		{
			var pack = Find(modId, id);
			if (pack.Id == PresentationPackDefinition.Default.Id || string.IsNullOrEmpty(pack.RootPath))
				throw new InvalidOperationException("The original presentation is read-only. Create or duplicate a pack first.");

			return pack;
		}

		static string SafePackPath(string modId, string id)
		{
			var root = Path.GetFullPath(PackDirectory(modId));
			var path = Path.GetFullPath(Path.Combine(root, id));
			if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException("Presentation pack path resolves outside the managed pack directory.");

			return path;
		}

		static string Slug(string title)
		{
			if (string.IsNullOrWhiteSpace(title))
				throw new InvalidDataException("Presentation pack title is required.");

			var builder = new StringBuilder();
			var pendingSeparator = false;
			foreach (var c in title.Trim().ToLowerInvariant())
			{
				if (char.IsLetterOrDigit(c) || c == '_')
				{
					if (pendingSeparator && builder.Length > 0)
						builder.Append('-');

					builder.Append(c);
					pendingSeparator = false;
				}
				else
					pendingSeparator = true;
			}

			return builder.Length > 0 ? builder.ToString() : "custom-pack";
		}

		static string UniqueId(string modId, string requested)
		{
			var existing = Discover(modId);
			if (!existing.ContainsKey(requested) && !Directory.Exists(SafePackPath(modId, requested)))
				return requested;

			var suffix = 2;
			while (existing.ContainsKey($"{requested}-{suffix}") || Directory.Exists(SafePackPath(modId, $"{requested}-{suffix}")))
				suffix++;

			return $"{requested}-{suffix}";
		}

		static void CopyDirectory(string source, string destination)
		{
			Directory.CreateDirectory(destination);
			foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
				Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));

			foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
				File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
		}

		static void WriteManifest(string root, string id, string title, string version, string author,
			string license, string description, IEnumerable<string> replacements)
		{
			var fields = new[]
			{
				new MiniYamlNode("Id", id),
				new MiniYamlNode("Title", title),
				new MiniYamlNode("Version", version),
				new MiniYamlNode("Author", author),
				new MiniYamlNode("License", license),
				new MiniYamlNode("Description", description),
				new MiniYamlNode("Assets", "assets"),
				new MiniYamlNode("Replaces", replacements.Order().JoinWith(", "))
			};
			new[] { new MiniYamlNode("PresentationPack", "", fields) }
				.WriteToFile(Path.Combine(root, "pack.yaml"));
		}
	}
}
