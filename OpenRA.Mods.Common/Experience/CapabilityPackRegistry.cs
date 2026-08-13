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
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OpenRA.Mods.Common.Experience
{
	/// <summary>
	/// A data-only, locally installed Experience component. Capability packs may
	/// contain YAML and presentation data, but never compiled code. Every file is
	/// mounted below a stable package namespace to prevent accidental collisions.
	/// </summary>
	public sealed class CapabilityPackDefinition
	{
		static readonly HashSet<string> ProhibitedExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".bat", ".cmd", ".com", ".cs", ".dll", ".exe", ".js", ".msi", ".ps1", ".sh", ".vbs"
		};

		public const string EngineApi = "experience-v2";
		public readonly string Id;
		public readonly string Title;
		public readonly string Version;
		public readonly string Author;
		public readonly string License;
		public readonly string Source;
		public readonly string SourceUrl;
		public readonly string TargetMod;
		public readonly string RootPath;
		public readonly string Fingerprint;
		public readonly ExperienceComponent Component;
		public readonly ImmutableArray<string> Files;

		CapabilityPackDefinition(string rootPath, MiniYaml yaml, string expectedMod)
		{
			RootPath = Path.GetFullPath(rootPath);
			Id = Required(yaml, "Id");
			Title = Required(yaml, "Title");
			Version = Required(yaml, "Version");
			Author = Required(yaml, "Author");
			License = Required(yaml, "License");
			Source = Required(yaml, "Source");
			SourceUrl = Value(yaml, "SourceUrl", "").Trim();
			TargetMod = Required(yaml, "TargetMod");
			var engineApi = Required(yaml, "EngineApi");
			var rightsAcknowledged = FieldLoader.GetValue<bool>("RightsAcknowledged", Required(yaml, "RightsAcknowledged"));

			if (!IsSafeId(Id))
				throw new InvalidDataException($"Capability pack id `{Id}` may contain only letters, digits, '-' and '_'.");

			if (!TargetMod.Equals(expectedMod, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException($"Capability pack `{Id}` targets `{TargetMod}`, not `{expectedMod}`.");

			if (!engineApi.Equals(EngineApi, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException($"Capability pack `{Id}` requires unsupported engine API `{engineApi}`.");

			if (!rightsAcknowledged)
				throw new InvalidDataException($"Capability pack `{Id}` must acknowledge redistribution rights before it can be installed.");

			var expectedRoot = Path.Combine(CapabilityPackRegistry.PackDirectory(expectedMod), "experience-packs", Id);
			if (!RootPath.Equals(Path.GetFullPath(expectedRoot), StringComparison.OrdinalIgnoreCase) &&
				RootPath.StartsWith(Path.GetFullPath(CapabilityPackRegistry.PackDirectory(expectedMod)) + Path.DirectorySeparatorChar,
					StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException($"Capability pack folder must be named `{Id}`.");

			RejectReparsePoints(RootPath);
			Files = Directory.GetFiles(RootPath, "*", SearchOption.AllDirectories)
				.Select(file => NormalizeRelativePath(Path.GetRelativePath(RootPath, file)))
				.Order(StringComparer.Ordinal)
				.ToImmutableArray();

			if (Files.Length > 4096)
				throw new InvalidDataException($"Capability pack `{Id}` contains more than 4096 files.");

			long totalBytes = 0;
			foreach (var file in Files)
			{
				if (ProhibitedExtensions.Contains(Path.GetExtension(file)))
					throw new InvalidDataException($"Capability pack file `{file}` is executable or compiled code and cannot be loaded.");

				totalBytes += new FileInfo(Path.Combine(RootPath, file.Replace('/', Path.DirectorySeparatorChar))).Length;
				if (totalBytes > CapabilityPackRegistry.MaximumPackBytes)
					throw new InvalidDataException("Capability packs may not exceed 512 MB.");
			}

			Fingerprint = HashFiles(Files);
			var componentNode = yaml.NodeWithKeyOrDefault("Component")?.Value ??
				throw new InvalidDataException("Capability pack requires a Component node.");
			var prefix = $"experience-packs/{Id}";
			Component = new ExperienceComponent(Id, componentNode, prefix, Id, RootPath, Fingerprint);
			ValidateDeclaredFiles(Component, Files);
		}

		public static CapabilityPackDefinition Load(string rootPath, string expectedMod)
		{
			var manifestPath = Path.Combine(rootPath, "pack.yaml");
			if (!File.Exists(manifestPath))
				throw new InvalidDataException("Capability pack does not contain pack.yaml.");

			var root = MiniYaml.FromFile(manifestPath, false).SingleOrDefault(n => n.Key == "CapabilityPack") ??
				throw new InvalidDataException("pack.yaml must contain one CapabilityPack node.");
			return new CapabilityPackDefinition(rootPath, root.Value, expectedMod);
		}

		static void ValidateDeclaredFiles(ExperienceComponent component, ImmutableArray<string> files)
		{
			var prefix = $"experience-packs/{component.Id}/";
			var declared = new[]
			{
				component.Rules, component.Weapons, component.Sequences, component.Cursors,
				component.Chrome, component.Voices, component.Notifications, component.Music
			}.SelectMany(x => x).Select(file => file.StartsWith(prefix, StringComparison.Ordinal) ? file[prefix.Length..] : file);
			var missing = declared.FirstOrDefault(file => !files.Contains(file, StringComparer.OrdinalIgnoreCase));
			if (missing != null)
				throw new InvalidDataException($"Capability pack declares missing file `{missing}`.");
		}

		string HashFiles(IEnumerable<string> files)
		{
			using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			foreach (var file in files)
			{
				hash.AppendData(Encoding.UTF8.GetBytes(file + "\n"));
				using var stream = File.OpenRead(Path.Combine(RootPath, file.Replace('/', Path.DirectorySeparatorChar)));
				var buffer = new byte[81920];
				int read;
				while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
					hash.AppendData(buffer, 0, read);
			}

			return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
		}

		static string Required(MiniYaml yaml, string key)
		{
			var value = Value(yaml, key, null);
			if (string.IsNullOrWhiteSpace(value))
				throw new InvalidDataException($"Capability pack field `{key}` is required.");

			return value.Trim();
		}

		static string Value(MiniYaml yaml, string key, string fallback)
		{
			return yaml.NodeWithKeyOrDefault(key)?.Value.Value ?? fallback;
		}

		static bool IsSafeId(string id)
		{
			return !string.IsNullOrWhiteSpace(id) && id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
		}

		static string NormalizeRelativePath(string path)
		{
			if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
				throw new InvalidDataException("Capability pack paths must be relative.");

			var normalized = path.Replace('\\', '/');
			if (normalized.Split('/').Any(p => p is "" or "." or ".."))
				throw new InvalidDataException($"Capability pack path `{path}` is invalid.");

			return normalized;
		}

		static void RejectReparsePoints(string root)
		{
			foreach (var path in new[] { root }.Concat(Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
				.Concat(Directory.GetFiles(root, "*", SearchOption.AllDirectories)))
				if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
					throw new InvalidDataException("Capability packs may not contain symbolic links or filesystem junctions.");
		}
	}

	public static class CapabilityPackRegistry
	{
		public const long MaximumPackBytes = 512L * 1024 * 1024;

		public static string PackDirectory(string modId)
		{
			return Path.Combine(Platform.SupportDir, "CapabilityPacks", modId);
		}

		public static IReadOnlyDictionary<string, CapabilityPackDefinition> Discover(string modId)
		{
			var result = new Dictionary<string, CapabilityPackDefinition>(StringComparer.OrdinalIgnoreCase);
			var root = Path.Combine(PackDirectory(modId), "experience-packs");
			Directory.CreateDirectory(root);
			foreach (var directory in Directory.GetDirectories(root).Order(StringComparer.Ordinal))
			{
				try
				{
					var pack = CapabilityPackDefinition.Load(directory, modId);
					if (!Path.GetFileName(directory).Equals(pack.Id, StringComparison.OrdinalIgnoreCase))
						throw new InvalidDataException($"Folder name must match pack id `{pack.Id}`.");

					if (!result.TryAdd(pack.Id, pack))
						throw new InvalidDataException($"Duplicate capability pack id `{pack.Id}`.");
				}
				catch (Exception e)
				{
					Log.Write("debug", $"Ignoring capability pack `{directory}`: {e.Message}");
				}
			}

			return result;
		}

		public static CapabilityPackDefinition Import(string modId, string sourcePath)
		{
			if (string.IsNullOrWhiteSpace(sourcePath))
				throw new InvalidDataException("Choose a capability pack folder or .zip archive.");

			sourcePath = Path.GetFullPath(sourcePath.Trim().Trim('"'));
			var staging = Path.Combine(Path.GetTempPath(), "openra-capability-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(staging);
			try
			{
				if (Directory.Exists(sourcePath))
					CopyDirectory(sourcePath, staging);
				else if (File.Exists(sourcePath) && Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
					ExtractArchive(sourcePath, staging);
				else
					throw new FileNotFoundException("Capability pack folder or .zip archive was not found.", sourcePath);

				var manifestRoot = FindManifestRoot(staging);
				var manifest = MiniYaml.FromFile(Path.Combine(manifestRoot, "pack.yaml"), false)
					.SingleOrDefault(n => n.Key == "CapabilityPack") ??
					throw new InvalidDataException("pack.yaml must contain one CapabilityPack node.");
				var id = manifest.Value.NodeWithKeyOrDefault("Id")?.Value.Value?.Trim();
				if (string.IsNullOrWhiteSpace(id) || !id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
					throw new InvalidDataException("Capability pack has an invalid Id.");

				var destination = Path.Combine(PackDirectory(modId), "experience-packs", id);
				if (Directory.Exists(destination))
					throw new InvalidDataException($"Capability pack `{id}` is already installed.");

				Directory.CreateDirectory(Path.GetDirectoryName(destination));
				CopyDirectory(manifestRoot, destination);
				try
				{
					return CapabilityPackDefinition.Load(destination, modId);
				}
				catch
				{
					Directory.Delete(destination, true);
					throw;
				}
			}
			finally
			{
				if (Directory.Exists(staging))
					Directory.Delete(staging, true);
			}
		}

		public static void Delete(string modId, string id)
		{
			var root = Path.GetFullPath(Path.Combine(PackDirectory(modId), "experience-packs"));
			var path = Path.GetFullPath(Path.Combine(root, id));
			if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(path))
				throw new InvalidDataException("Capability pack is outside the managed package directory or does not exist.");

			Directory.Delete(path, true);
		}

		static string FindManifestRoot(string staging)
		{
			if (File.Exists(Path.Combine(staging, "pack.yaml")))
				return staging;

			var manifests = Directory.GetFiles(staging, "pack.yaml", SearchOption.AllDirectories);
			if (manifests.Length != 1)
				throw new InvalidDataException("Capability pack must contain exactly one pack.yaml manifest.");

			return Path.GetDirectoryName(manifests[0]);
		}

		static void ExtractArchive(string source, string destination)
		{
			using var archive = ZipFile.OpenRead(source);
			long totalBytes = 0;
			foreach (var entry in archive.Entries)
			{
				totalBytes += entry.Length;
				if (totalBytes > MaximumPackBytes)
					throw new InvalidDataException("Capability pack archive expands beyond 512 MB.");

				var target = Path.GetFullPath(Path.Combine(destination, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
				if (!target.StartsWith(Path.GetFullPath(destination) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
					throw new InvalidDataException("Capability pack archive contains an unsafe path.");

				if (string.IsNullOrEmpty(entry.Name))
				{
					Directory.CreateDirectory(target);
					continue;
				}

				Directory.CreateDirectory(Path.GetDirectoryName(target));
				using var input = entry.Open();
				using var output = File.Create(target);
				input.CopyTo(output);
			}
		}

		static void CopyDirectory(string source, string destination)
		{
			Directory.CreateDirectory(destination);
			foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
				Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));

			foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
				File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
		}
	}
}
