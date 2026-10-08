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
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Experience;
using OpenRA.Mods.Common.FactionCatalog;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class FactionCatalogTest
	{
		const string CatalogJson = """
			{
			  "schemaVersion": 1,
			  "revision": "test",
			  "links": { "origin": "https://rtsai.net", "faction": "/factions/{factionId}?mode={mode}", "unit": "/units/{unitId}?mode={mode}" },
			  "modes": [ { "id": "ra", "name": "Classic" }, { "id": "ra2", "name": "Red Alert 2" } ],
			  "profiles": [ { "id": "modern", "mode": "ra", "factionIds": [ "alpha-nation" ] } ],
			  "theatres": [],
			  "factions": [
			    {
			      "id": "alpha-nation", "name": "Alpha Nation", "title": "First Test", "tagline": "Test tagline",
			      "story": "Story.", "playstyle": "Play.", "strengths": "Strong.", "counterplay": "Counter.", "accent": "#cb4c3b",
			      "unitIds": [ "alpha-tank" ],
			      "variants": {
			        "ra": { "status": "implemented", "profileId": "modern" },
			        "ra2": { "status": "unavailable", "profileId": null }
			      }
			    }
			  ],
			  "units": [
			    {
			      "id": "alpha-tank", "factionId": "alpha-nation", "name": "Alpha Tank", "role": "Test armor",
			      "story": "A tank.", "strengths": "Armor.", "counterplay": "Air.",
			      "variants": { "ra": { "actorId": "TANK" } }
			    }
			  ]
			}
			""";

		const string ExperienceYaml = """
			ExperienceCatalog:
				Mod: ra
				DefaultProfile: modern
				Components:
					alpha-faction:
						Title: Alpha
						Description: Test faction.
						Effects: Adds Alpha.
						Tradeoffs: Test only.
						Scope: Test fixtures only.
						Category: Faction packs
						Version: 1
						Kind: Faction
						Faction:
							InternalName: alpha
							Side: Allies
							RandomPool: RandomAllies
							Doctrine: test-doctrine
							Preview: alpha.png
							Roster:
								Infantry: RIFLE
								Vehicles: TANK
								Aircraft: TANK
								Navy: TANK
								Buildings: FACT, WEAP
								Defenses: WEAP
				Profiles:
					modern:
						Title: Modern
						Components: alpha-faction
			""";

		static T Info<T>(string yaml) where T : TraitInfo, new()
		{
			return FieldLoader.Load<T>(new MiniYaml(null, MiniYaml.FromString(yaml, "faction-catalog-test")));
		}

		static ActorInfo Buildable(string name, string queue, string prerequisites, params TraitInfo[] extra)
		{
			var buildable = Info<BuildableInfo>($"Queue: {queue}\nPrerequisites: {prerequisites}\nDescription: {name}-description");
			return new ActorInfo(name, extra.Prepend(buildable).Prepend(Info<ValuedInfo>("Cost: 100")).ToArray());
		}

		/// <summary>Two factions sharing a factory; each gets one exclusive vehicle.</summary>
		static Ruleset Rules(bool includeRifle = true)
		{
			var actors = new List<ActorInfo>
			{
				new("world",
					Info<FactionInfo>("InternalName: alpha\nName: Alpha\nSide: Allies"),
					Info<FactionInfo>("InternalName: beta\nName: Beta\nSide: Soviet"),
					Info<FactionInfo>("InternalName: RandomAllies\nName: Random\nRandomFactionMembers: alpha, beta"),
					Info<StartingUnitsInfo>("BaseActor: mcv")),
				new("player",
					Info<ProductionQueueInfo>("Type: Building"),
					Info<ProductionQueueInfo>("Type: Vehicle"),
					Info<ProductionQueueInfo>("Type: Infantry"),
					Info<ProvidesPrerequisiteInfo>("Prerequisite: faction.alpha\nFactions: alpha"),
					Info<ProvidesTechPrerequisiteInfo>("Name: High\nId: high\nPrerequisites: techlevel.high")),
				new("mcv", Info<TransformsInfo>("IntoActor: fact")),
				new("fact", Info<ProductionInfo>("Produces: Building, Infantry"), Info<ProvidesPrerequisiteInfo>("")),
				Buildable("weap", "Building", "fact",
					Info<ProductionInfo>("Produces: Vehicle"),
					Info<ProvidesPrerequisiteInfo>(""),
					Info<ProvidesPrerequisiteInfo>("Prerequisite: vehicles.alpha\nFactions: alpha"),
					Info<ProvidesPrerequisiteInfo>("Prerequisite: vehicles.beta\nFactions: beta")),
				Buildable("tank", "Vehicle", "~vehicles.alpha, ~techlevel.high",
					Info<StrategicRoleInfo>("Roles: main-battle-tank, armor\nCounters: armor\nDomain: ground")),
				Buildable("beast", "Vehicle", "~vehicles.beta"),
				Buildable("jeep", "Vehicle", "weap, ~!faction.alpha"),
				Buildable("boat", "Ship", "fact"),
				Buildable("secret", "Vehicle", "~disabled"),
			};

			if (includeRifle)
				actors.Add(Buildable("rifle", "Infantry", "fact"));

			return new Ruleset(actors.ToDictionary(a => a.Name), new Dictionary<string, WeaponInfo>(), null, null, null, null, null);
		}

		static ExperienceCatalog Experience()
		{
			return new ExperienceCatalog(MiniYaml.FromString(ExperienceYaml, "faction-catalog-test").Single().Value,
				new ExperienceSettings(), [], PresentationPackDefinition.Default, "modern");
		}

		static string[] Names(IEnumerable<ActorInfo> actors) => actors.Select(a => a.Name).Order().ToArray();

		[Test]
		public void TechTreeFollowsFactionFiltersTechLevelsAndInvertedPrerequisites()
		{
			var rules = Rules();
			Assert.That(Names(FactionTechTree.BuildableActors(rules, "alpha")), Is.EqualTo(new[] { "rifle", "tank", "weap" }));
			Assert.That(Names(FactionTechTree.BuildableActors(rules, "beta")), Is.EqualTo(new[] { "beast", "jeep", "rifle", "weap" }));
			Assert.That(Names(FactionTechTree.ExclusiveActors(rules, "alpha", ["alpha", "beta"])), Is.EqualTo(new[] { "tank" }));
			Assert.That(Names(FactionTechTree.ExclusiveActors(rules, "beta", ["alpha", "beta"])), Is.EqualTo(new[] { "beast", "jeep" }));
		}

		[Test]
		public void PrerequisiteGrammarMatchesTechTree()
		{
			var provided = new HashSet<string> { "fact", "faction.alpha" };
			Assert.That(FactionTechTree.Satisfied(["~fact"], provided), Is.True);
			Assert.That(FactionTechTree.Satisfied(["~!faction.alpha"], provided), Is.False);
			Assert.That(FactionTechTree.Satisfied(["!faction.beta", "FACT"], provided), Is.True);
			Assert.That(FactionTechTree.Satisfied(["~disabled"], provided), Is.False);
		}

		[Test]
		public void ParsesCatalogAndBuildsModeSpecificLinks()
		{
			var catalog = SharedFactionCatalog.Parse(CatalogJson, "test.json");
			Assert.That(catalog.Revision, Is.EqualTo("test"));
			Assert.That(catalog.Faction("alpha-nation").IsImplementedIn("ra"), Is.True);
			Assert.That(catalog.Faction("alpha-nation").IsImplementedIn("ra2"), Is.False);
			Assert.That(catalog.Unit("alpha-tank").ActorIn("ra"), Is.EqualTo("TANK"));
			Assert.That(catalog.Unit("alpha-tank").ActorIn("ra2"), Is.Null);
			Assert.That(catalog.Links.FactionUrl("alpha-nation", "ra2"), Is.EqualTo("https://rtsai.net/factions/alpha-nation?mode=ra2"));
			Assert.That(catalog.Links.UnitUrl("alpha-tank", "ra"), Is.EqualTo("https://rtsai.net/units/alpha-tank?mode=ra"));
			Assert.That(catalog.Links.FactionUrl("Bad Id", "ra"), Is.Null);
			Assert.That(catalog.Links.Problem(), Is.Null);
		}

		[Test]
		public void CatalogWithoutLinksUsesThePublicSiteDefaults()
		{
			var start = CatalogJson.IndexOf("\"links\"", System.StringComparison.Ordinal);
			var json = CatalogJson.Remove(start, CatalogJson.IndexOf("\"modes\"", System.StringComparison.Ordinal) - start);
			var catalog = SharedFactionCatalog.Parse(json);
			Assert.That(catalog.DeclaresLinks, Is.False);
			Assert.That(catalog.Links.FactionUrl("alpha-nation", "ra"), Is.EqualTo("https://rtsai.net/factions/alpha-nation?mode=ra"));
		}

		[TestCase("\"schemaVersion\": 1", "\"schemaVersion\": 2")]
		[TestCase("\"id\": \"alpha-tank\"", "\"id\": \"Alpha Tank\"")]
		[TestCase("\"modes\": [ {", "\"modes\": 3, \"unused\": [ {")]
		[TestCase("\"units\": [", "\"units\": [ { \"id\": \"alpha-tank\", \"variants\": {} },")]
		public void RejectsStructurallyInvalidCatalogs(string find, string replace)
		{
			Assert.That(CatalogJson, Does.Contain(find));
			Assert.Throws<InvalidDataException>(() => SharedFactionCatalog.Parse(CatalogJson.Replace(find, replace)));
		}

		[TestCase("https://rtsai.net/factions/china?mode=ra", null)]
		[TestCase("http://rtsai.net/factions/china?mode=ra", "https")]
		[TestCase("https://rtsai.net/factions/{factionId}?mode=ra", "placeholder")]
		[TestCase("https://localhost/factions/china?mode=ra", "public host")]
		[TestCase("https://rtsai.net/factions/china?mode=ra#units", "fragment")]
		[TestCase("https://example.com/factions/china?mode=ra", "does not point")]
		[TestCase("/factions/china", "absolute")]
		public void DetectsMalformedDeepLinks(string url, string problem)
		{
			var result = SharedCatalogLinks.LinkProblem(url, "https://rtsai.net");
			if (problem == null)
				Assert.That(result, Is.Null);
			else
				Assert.That(result, Does.Contain(problem));
		}

		[Test]
		public void RejectsLinkTemplatesWithoutPlaceholders()
		{
			const string Faction = "/factions/{factionId}?mode={mode}";
			const string Unit = "/units/{unitId}?mode={mode}";
			Assert.That(new SharedCatalogLinks("https://rtsai.net", "/factions/china", Unit).Problem(), Does.Contain("links.faction"));
			Assert.That(new SharedCatalogLinks("http://rtsai.net", Faction, Unit).Problem(), Does.Contain("origin"));
			Assert.That(new SharedCatalogLinks("https://rtsai.net/base", Faction, Unit).Problem(), Does.Contain("origin"));
		}

		[Test]
		public void LocatesInstalledCatalogInPackagedAndDevelopmentLayouts()
		{
			var engine = Path.Combine(Path.GetTempPath(), "openra-catalog-test", "product", "engine", "openra");
			var environment = new Dictionary<string, string>();
			string Env(string key) => environment.TryGetValue(key, out var value) ? value : null;
			var candidates = SharedFactionCatalog.CandidatePaths(engine, Env).ToArray();
			Assert.That(candidates, Is.EqualTo(new[]
			{
				Path.GetFullPath(Path.Combine(engine, "catalog", "factions.json")),
				Path.GetFullPath(Path.Combine(engine, "..", "..", "catalog", "factions.json")),
				Path.GetFullPath(Path.Combine(engine, "..", "OpenRA-AI", "catalog", "factions.json")),
			}));

			// Windows packages and the product submodule: <root>/engine/openra -> <root>/catalog.
			var windows = Path.GetFullPath(Path.Combine(engine, "..", "..", "catalog", "factions.json"));
			Assert.That(SharedFactionCatalog.Locate(engine, Env, path => path == windows), Is.EqualTo(windows));

			environment["OPENRA_AI_ROOT"] = Path.Combine(Path.GetTempPath(), "root");
			environment["OPENRA_AI_CATALOG"] = Path.Combine(Path.GetTempPath(), "explicit.json");
			candidates = SharedFactionCatalog.CandidatePaths(engine, Env).ToArray();
			Assert.That(candidates[0], Is.EqualTo(Path.GetFullPath(environment["OPENRA_AI_CATALOG"])));
			Assert.That(candidates[1], Is.EqualTo(Path.GetFullPath(Path.Combine(environment["OPENRA_AI_ROOT"], "catalog", "factions.json"))));
			Assert.That(SharedFactionCatalog.Locate(engine, Env, _ => false), Is.Null);
		}

		[Test]
		public void ViewIsModeAwareAndReadsLiveRules()
		{
			var rules = Rules();
			var catalog = SharedFactionCatalog.Parse(CatalogJson);
			var view = FactionCatalogView.Build("ra", rules, Experience(), catalog, key => key);
			var alpha = view.Factions.Single(f => f.Kind == CatalogFactionKind.Modern);
			Assert.That(alpha.Key, Is.EqualTo("alpha-nation"));
			Assert.That(alpha.InternalName, Is.EqualTo("alpha"));
			Assert.That(alpha.Availability, Is.EqualTo(CatalogAvailability.Active));
			Assert.That(alpha.WebUrl, Is.EqualTo("https://rtsai.net/factions/alpha-nation?mode=ra"));
			Assert.That(alpha.Accent, Is.EqualTo(Color.FromArgb(0xcb, 0x4c, 0x3b)));
			Assert.That(alpha.Doctrine, Is.EqualTo("Test doctrine"));

			var tank = alpha.Roster.Single(r => r.Actor == "tank");
			Assert.That(tank.IsSignature && tank.IsExclusive, Is.True);
			Assert.That(tank.Domain, Is.EqualTo("Vehicles"));
			Assert.That(tank.Role, Is.EqualTo("Test armor"));
			Assert.That(tank.WebUrl, Is.EqualTo("https://rtsai.net/units/alpha-tank?mode=ra"));
			Assert.That(tank.Stats.Cost, Is.EqualTo(100));
			Assert.That(tank.Stats.Counters, Is.EqualTo(new[] { "armor" }));
			Assert.That(alpha.Signature, Is.SameAs(tank));

			// Rules factions without a faction pack are listed as original sides with rules-derived rosters.
			var beta = view.Factions.Single(f => f.InternalName == "beta");
			Assert.That(beta.Kind, Is.EqualTo(CatalogFactionKind.Original));
			Assert.That(beta.Roster.Where(r => r.IsExclusive).Select(r => r.Actor).Order(), Is.EqualTo(new[] { "beast", "jeep" }));
			Assert.That(view.Factions.Any(f => f.InternalName == "RandomAllies"), Is.False);

			// The same catalog in a mode where the faction is unavailable lists it only as another mode's faction.
			var ra2 = FactionCatalogView.Build("ra2", rules, null, catalog, key => key);
			Assert.That(ra2.Factions.Any(f => f.Catalog != null), Is.False);
			Assert.That(ra2.OtherModeFactions.Select(f => f.Id), Is.EqualTo(new[] { "alpha-nation" }));
		}

		[Test]
		public void MissingCatalogFallsBackToRulesOnly()
		{
			var view = FactionCatalogView.Build("ra", Rules(), Experience(), null, key => key, "not installed");
			var alpha = view.Factions.Single(f => f.InternalName == "alpha");
			Assert.That(alpha.Catalog, Is.Null);
			Assert.That(alpha.WebUrl, Is.Null);
			Assert.That(alpha.Roster.Select(r => r.Actor), Does.Contain("tank"));
			Assert.That(view.CatalogError, Is.EqualTo("not installed"));
		}

		[Test]
		public void ValidatorAcceptsConsistentCatalog()
		{
			var issues = FactionCatalogValidator.Validate("ra", Rules(), Experience(), SharedFactionCatalog.Parse(CatalogJson));
			Assert.That(issues, Is.Empty, string.Join("\n", issues));
		}

		[TestCase("\"actorId\": \"TANK\"", "\"actorId\": \"MISSING\"", "unit-missing-actor")]
		[TestCase("\"actorId\": \"TANK\"", "\"actorId\": \"BEAST\"", "unit-not-buildable")]
		[TestCase("\"origin\": \"https://rtsai.net\"", "\"origin\": \"http://rtsai.net\"", "malformed-link")]
		[TestCase("\"faction\": \"/factions/{factionId}?mode={mode}\"", "\"faction\": \"/factions/china\"", "malformed-link")]
		[TestCase("\"variants\": { \"ra\": { \"actorId\": \"TANK\" } }",
			"\"variants\": { \"ra\": { \"actorId\": \"TANK\" }, \"ra2\": { \"actorId\": \"TANK\" } }", "unit-in-unavailable-mode")]
		[TestCase("\"ra\": { \"status\": \"implemented\", \"profileId\": \"modern\" }",
			"\"ra\": { \"status\": \"unavailable\", \"profileId\": null }", "faction-missing-from-catalog")]
		[TestCase("\"factionIds\": [ \"alpha-nation\" ]", "\"factionIds\": []", "profile-membership")]
		public void ValidatorRejectsInconsistentCatalog(string find, string replace, string code)
		{
			Assert.That(CatalogJson, Does.Contain(find));
			var catalog = SharedFactionCatalog.Parse(CatalogJson.Replace(find, replace));
			var issues = FactionCatalogValidator.Validate("ra", Rules(), Experience(), catalog);
			Assert.That(issues.Select(i => i.Code), Does.Contain(code), string.Join("\n", issues));
		}

		[Test]
		public void ValidatorRejectsBuildableFactionActorMissingFromCatalog()
		{
			// Add an alpha-only unit to the rules without listing it in the faction pack roster or shared catalog.
			var rules = Rules();
			var actors = rules.Actors.Values.ToDictionary(a => a.Name);
			actors["spy"] = Buildable("spy", "Infantry", "~faction.alpha");
			var extended = new Ruleset(actors, new Dictionary<string, WeaponInfo>(), null, null, null, null, null);
			var issues = FactionCatalogValidator.Validate("ra", extended, Experience(), SharedFactionCatalog.Parse(CatalogJson));
			Assert.That(issues.Single().Code, Is.EqualTo("buildable-missing-from-catalog"));
			Assert.That(issues.Single().Message, Does.Contain("`spy`"));
		}

		[Test]
		public void ValidatorRejectsRosterActorsMissingFromRules()
		{
			var issues = FactionCatalogValidator.Validate("ra", Rules(includeRifle: false), Experience(), SharedFactionCatalog.Parse(CatalogJson));
			Assert.That(issues.Select(i => i.Code), Is.EqualTo(new[] { "roster-missing-actor" }));
		}

		[Test]
		public void AirborneAndAirTargetProfilesBothCountAsAntiAir()
		{
			static WeaponInfo Weapon(string validTargets) => new(MiniYaml.FromString($"""
				Weapon:
					ValidTargets: {validTargets}
				""", "faction-catalog-test").Single().Value);

			// Classic marks flying actors AirborneActor; RA2 marks them Air.
			var domains = new CatalogTargetDomains([
				new BitSet<TargetableType>("GroundActor", "Vehicle"),
				new BitSet<TargetableType>("AirborneActor"),
				new BitSet<TargetableType>("Air"),
				new BitSet<TargetableType>("Ground", "Water")
			]);
			Assert.That(domains.Classify(Weapon("AirborneActor")), Is.EqualTo((true, false)));
			Assert.That(domains.Classify(Weapon("Air")), Is.EqualTo((true, false)));
			Assert.That(domains.Classify(Weapon("GroundActor, Water")), Is.EqualTo((false, true)));
			Assert.That(domains.Classify(Weapon("Ground, AirborneActor")), Is.EqualTo((true, true)));
		}

		[Test]
		public void PresentsRulesTextForPlayers()
		{
			Assert.That(CatalogUnitStats.ReadableTargets(["Ground", "GroundActor", "Water", "WaterActor", "Barrel"]), Is.EqualTo(new[] { "Ground", "Water" }));
			Assert.That(CatalogUnitStats.ReadableTargets(["AirborneActor"]), Is.EqualTo(new[] { "Air" }));
			Assert.That(CatalogUnitStats.Reflow("Infiltrates enemy structures for intel or\nsabotage.\nLoses disguise when attacking."),
				Is.EqualTo("Infiltrates enemy structures for intel or sabotage.\nLoses disguise when attacking."));
			Assert.That(CatalogUnitStats.Reflow("Provides radar\nSupports 4 aircraft."), Is.EqualTo("Provides radar\nSupports 4 aircraft."));
			Assert.That(CatalogUnitStats.FormatCells(new WDist(6656)), Is.EqualTo("6.5"));
			Assert.That(CatalogUnitStats.FormatCells(new WDist(6144)), Is.EqualTo("6"));
			Assert.That(CatalogUnitStats.Humanize("main-battle-tank"), Is.EqualTo("Main battle tank"));
		}

		[Test]
		public void DigestContainsRulesFactsButNoMatchState()
		{
			var view = FactionCatalogView.Build("ra", Rules(), Experience(), SharedFactionCatalog.Parse(CatalogJson), key => key);
			var json = FactionCatalogDigest.Serialize(FactionCatalogDigest.Create(view, "test", null, 40, "test"));
			Assert.That(json, Does.Contain("\"schema\":\"openra-ai.faction-catalog.live/1\""));
			Assert.That(json, Does.Contain("\"mode\":\"ra\""));
			Assert.That(json, Does.Contain("\"catalogUnitId\":\"alpha-tank\""));
			Assert.That(json, Does.Contain("\"counters\":[\"armor\"]"));
			foreach (var forbidden in new[] { "location", "health", "owner", "visible", "tick" })
				Assert.That(json, Does.Not.Contain($"\"{forbidden}\""));
		}
	}
}
