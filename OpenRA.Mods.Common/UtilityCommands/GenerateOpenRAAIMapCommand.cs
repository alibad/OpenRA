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
using System.Text.Json;
using OpenRA.FileSystem;
using OpenRA.Mods.Common.MapGenerator;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.UtilityCommands
{
	/// <summary>
	/// Generates an OpenRA AI mission using the engine's real random map generator.
	/// This deliberately lives in the engine so that terrain templates, transitions,
	/// spawn placement, resource placement and locomotor rules all come from the mod.
	/// </summary>
	sealed class GenerateOpenRAAIMapCommand : IUtilityCommand
	{
		string IUtilityCommand.Name => "--generate-openra-ai-map";

		sealed class Configuration
		{
			public string Output;
			public string Tileset = "TEMPERAT";
			public int Size = 96;
			public int Seed = 1;
			public int Attempts = 8;
			public string Title = "Earth Skirmish";
			public readonly Dictionary<string, string> Options = new(StringComparer.OrdinalIgnoreCase)
			{
				["TerrainType"] = "Plots",
				["Shape"] = "Square",
				["Players"] = "2",
				["Symmetry"] = "2Rotations",
				["Resources"] = "Medium",
				["Buildings"] = "Standard",
				["Density"] = "Players",
				["CivilianDensity"] = "Default",
				["DenyWalledArea"] = "True",
				["Roads"] = "True",
			};

			public static Configuration Parse(string[] args, bool reportErrors)
			{
				var config = new Configuration();
				foreach (var arg in args.Skip(1))
				{
					var separator = arg.IndexOf('=');
					if (separator < 3 || !arg.StartsWith("--", StringComparison.Ordinal))
						return Fail($"Expected --name=value, got `{arg}`");

					var name = arg[2..separator];
					var value = arg[(separator + 1)..];
					switch (name.ToLowerInvariant())
					{
						case "output": config.Output = value; break;
						case "tileset": config.Tileset = value.ToUpperInvariant(); break;
						case "size":
							if (!Exts.TryParseInt32Invariant(value, out config.Size))
								return Fail($"Invalid size `{value}`");
							break;
						case "seed":
							if (!Exts.TryParseInt32Invariant(value, out config.Seed))
								return Fail($"Invalid seed `{value}`");
							break;
						case "attempts":
							if (!Exts.TryParseInt32Invariant(value, out config.Attempts))
								return Fail($"Invalid attempts `{value}`");
							break;
						case "title": config.Title = value; break;
						case "terrain": config.Options["TerrainType"] = value; break;
						case "shape": config.Options["Shape"] = value; break;
						case "players": config.Options["Players"] = value; break;
						case "symmetry": config.Options["Symmetry"] = value; break;
						case "resources": config.Options["Resources"] = value; break;
						case "buildings": config.Options["Buildings"] = value; break;
						case "density": config.Options["Density"] = value; break;
						case "civilian-density": config.Options["CivilianDensity"] = value; break;
						case "roads": config.Options["Roads"] = value; break;
						case "deny-walled-areas": config.Options["DenyWalledArea"] = value; break;
						default: return Fail($"Unrecognized argument `{arg}`");
					}
				}

				if (string.IsNullOrWhiteSpace(config.Output))
					return Fail("--output is required");
				if (config.Size is < 48 or > 256)
					return Fail("--size must be between 48 and 256");
				if (config.Seed < 0)
					return Fail("--seed must be non-negative");
				if (config.Attempts is < 1 or > 32)
					return Fail("--attempts must be between 1 and 32");
				if ((long)config.Seed + config.Attempts - 1 > int.MaxValue)
					return Fail("--seed plus generation attempts must not exceed 2147483647");

				return config;

				Configuration Fail(string message)
				{
					if (reportErrors)
						Console.Error.WriteLine(message);
					return null;
				}
			}
		}

		sealed record PassabilityReport(
			bool Valid,
			string Unit,
			string Locomotor,
			int TotalSpawns,
			int ReachableSpawns,
			int ReachableCells,
			int MinimumSpawnZoneCells);

		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return Configuration.Parse(args, false) != null;
		}

		[Desc(
			"--output=PATH [--tileset=TILESET] [--size=CELLS] [--seed=SEED] [--terrain=PROFILE] " +
			"[--shape=SHAPE] [--players=COUNT] [--symmetry=MODE] [--resources=LEVEL] " +
			"[--buildings=LEVEL] [--density=LEVEL] [--civilian-density=LEVEL] [--attempts=COUNT] [--title=TITLE]",
			"Generate and validate an OpenRA AI map with the native classic map generator.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			var config = Configuration.Parse(args, true);
			var modData = Game.ModData = utility.ModData;
			if (!modData.DefaultTerrainInfo.ContainsKey(config.Tileset))
				throw new ArgumentException($"Unknown tileset `{config.Tileset}`");

			var generator = modData.DefaultRules.Actors[SystemActors.EditorWorld]
				.TraitInfos<IEditorMapGeneratorInfo>()
				.FirstOrDefault(info => info.Type == "classic")
				?? throw new ArgumentException("The classic OpenRA map generator is not available");

			Exception lastError = null;
			for (var attempt = 0; attempt < config.Attempts; attempt++)
			{
				var actualSeed = config.Seed + attempt;
				try
				{
					var terrainInfo = modData.DefaultTerrainInfo[config.Tileset];
					var optionValues = CompileOptions(generator, terrainInfo, config.Options);
					optionValues["Seed"] = actualSeed.ToString(System.Globalization.CultureInfo.InvariantCulture);
					ValidateOptions(generator, optionValues);
					var generationArgs = new MapGenerationArgs
					{
						Generator = generator.Type,
						Tileset = config.Tileset,
						Size = new Size(config.Size, config.Size),
						Options = optionValues,
						Title = config.Title.Replace('\n', ' ').Replace('\r', ' ').Trim(),
						Author = "OpenRA AI / OpenRA Classic Generator",
					};

					var map = generator.Generate(modData, generationArgs);
					var passability = ValidateTrackedPassability(map);
					if (!passability.Valid)
					{
						lastError = new InvalidDataException(
							$"Tracked-unit connectivity failed ({passability.ReachableSpawns}/{passability.TotalSpawns} spawns reachable)");
						continue;
					}

					var output = Path.GetFullPath(config.Output);
					Directory.CreateDirectory(Path.GetDirectoryName(output));
					if (File.Exists(output))
						File.Delete(output);

					using (var package = new ZipFileLoader.ReadWriteZipFile(output, true))
						map.Save(package);

					Console.WriteLine(JsonSerializer.Serialize(new
					{
						ok = true,
						engine_generator = "classic",
						requested_seed = config.Seed,
						actual_seed = actualSeed,
						tileset = config.Tileset,
						terrain = config.Options["TerrainType"],
						uid = map.Uid,
						output,
						passability = new
						{
							valid = passability.Valid,
							unit = passability.Unit,
							locomotor = passability.Locomotor,
							total_spawns = passability.TotalSpawns,
							reachable_spawns = passability.ReachableSpawns,
							reachable_cells = passability.ReachableCells,
							minimum_spawn_zone_cells = passability.MinimumSpawnZoneCells,
						},
					}));
					return;
				}
				catch (Exception ex) when (ex is MapGenerationException || ex is YamlException || ex is InvalidDataException)
				{
					lastError = ex;
				}
			}

			throw new InvalidDataException($"Native map generation failed after {config.Attempts} attempts", lastError);
		}

		// MapGeneratorBase expects a value for every option, including the hidden
		// defaults and tileset overrides that the editor UI normally supplies.
		static Dictionary<string, string> CompileOptions(
			IEditorMapGeneratorInfo generator,
			ITerrainInfo terrainInfo,
			Dictionary<string, string> configuredOptions)
		{
			var optionValues = new Dictionary<string, string>(configuredOptions, StringComparer.OrdinalIgnoreCase);
			var playerCount = optionValues.TryGetValue("Players", out var players)
				? FieldLoader.GetValue<int>("Players", players)
				: 0;

			foreach (var option in generator.Options)
			{
				if (optionValues.ContainsKey(option.Id))
					continue;

				optionValues[option.Id] = option switch
				{
					MapGeneratorBooleanOption booleanOption => FieldSaver.FormatValue(booleanOption.Default),
					MapGeneratorIntegerOption integerOption => FieldSaver.FormatValue(integerOption.Default),
					MapGeneratorMultiIntegerChoiceOption integerChoiceOption => FieldSaver.FormatValue(integerChoiceOption.Default),
					MapGeneratorMultiChoiceOption choiceOption => choiceOption.DefaultFor(terrainInfo, playerCount),
					_ => throw new ArgumentException($"Unsupported map generator option `{option.Id}`"),
				};
			}

			return optionValues;
		}

		// Upstream made MapGeneratorOption immutable: option values are no longer assigned
		// onto the option objects, they are carried as strings in MapGenerationArgs.Options
		// and converted by the generator. This only has to reject ids the generator lacks.
		static void ValidateOptions(IEditorMapGeneratorInfo generator, Dictionary<string, string> optionValues)
		{
			var known = generator.Options.Select(o => o.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
			var unknown = optionValues.Keys.Where(k => !known.Contains(k)).ToList();
			if (unknown.Count != 0)
				throw new ArgumentException($"Unknown map generator options: {string.Join(", ", unknown)}");
		}

		static PassabilityReport ValidateTrackedPassability(Map map)
		{
			const string ValidationUnit = "1tnk";
			var unitInfo = map.Rules.Actors[ValidationUnit];
			var mobileInfo = unitInfo.TraitInfo<MobileInfo>();
			var locomotor = mobileInfo.LocomotorInfo;
			var spawns = map.ActorDefinitions
				.Where(actor => actor.Value.Value == "mpspawn")
				.Select(actor => new ActorReference(actor.Key, actor.Value).Get<LocationInit>().Value)
				.ToArray();

			if (spawns.Length < 2 || spawns.Any(spawn => !IsPassable(spawn)))
				return new(false, ValidationUnit, locomotor.Name, spawns.Length, 0, 0, 0);

			var queue = new Queue<CPos>();
			var visited = new HashSet<CPos>();
			queue.Enqueue(spawns[0]);
			visited.Add(spawns[0]);
			var directions = new[] { new CVec(1, 0), new CVec(-1, 0), new CVec(0, 1), new CVec(0, -1) };
			while (queue.Count > 0)
			{
				var cell = queue.Dequeue();
				foreach (var direction in directions)
				{
					var neighbor = cell + direction;
					if (visited.Add(neighbor) && IsPassable(neighbor))
						queue.Enqueue(neighbor);
				}
			}

			var reachableSpawns = spawns.Count(visited.Contains);
			var minimumSpawnZoneCells = spawns.Min(spawn =>
				visited.Count(cell => Math.Abs(cell.X - spawn.X) <= 6 && Math.Abs(cell.Y - spawn.Y) <= 6));
			return new(
				reachableSpawns == spawns.Length && minimumSpawnZoneCells >= 40,
				ValidationUnit,
				locomotor.Name,
				spawns.Length,
				reachableSpawns,
				visited.Count,
				minimumSpawnZoneCells);

			bool IsPassable(CPos cell)
			{
				if (!map.Contains(cell))
					return false;

				var tileInfo = map.Rules.TerrainInfo.GetTerrainInfo(map.Tiles[cell]);
				var terrainType = map.Rules.TerrainInfo.TerrainTypes[tileInfo.TerrainType].Type;
				return locomotor.TerrainSpeeds.ContainsKey(terrainType);
			}
		}
	}
}
