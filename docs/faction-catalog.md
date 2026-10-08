# Native Faction Catalog

The Faction Catalog is a native screen that presents the product's shared
faction/unit catalog (`OpenRA-AI/catalog/factions.json`) for the loaded game
mode. The engine's loaded rules are authoritative. The catalog adds only
editorial text, signature units, mode availability and public web links.

## Code

| Path | Role |
|---|---|
| `OpenRA.Mods.Common/FactionCatalog/SharedFactionCatalog.cs` | Parses the catalog JSON, builds deep links from its `links` templates (default `https://rtsai.net/factions/{factionId}?mode={mode}` and `/units/{unitId}?mode={mode}`), and locates the installed file. |
| `FactionTechTree.cs` | Works out which actors a faction can build from the loaded rules. It uses `TechTree`'s prerequisite grammar (`~` hides, `!` inverts), the faction filters of `ProvidesPrerequisite` and `ProvidesFactionDoctrine`, starting units and their transforms, every tech level, and default lobby prerequisites. It never assumes grants earned during a match (captures, promotions, support powers). |
| `CatalogUnitStats.cs` | Reads each unit's cost, build time, HP, armor, speed, sight, power, cargo, strategic roles and counters, prerequisites, and per-weapon range, damage, reload and targets. Air reach accepts both Classic `AirborneActor` and RA2 `Air` target profiles. |
| `FactionCatalogView.cs` | Joins catalog factions to Experience faction packs (by unit actor, then id), keeps faction packs the catalog lacks visible from rules alone, adds the original countries with rules-derived rosters, and hides catalog factions that are unavailable in this mode. |
| `FactionCatalogValidator.cs`, `UtilityCommands/CheckFactionCatalogCommand.cs` | `OpenRA.Utility <mod> --check-faction-catalog [factions.json] [--report out.json] [--digest out.json]` |
| `FactionCatalogDigest.cs` | Rules-derived facts sent to the companion (`POST {OPENRA_AI_CONSOLE_URL}v1/factions/live`) from the main menu, the catalog and the in-match companion HUD. |
| `Widgets/Logic/FactionCatalogLogic.cs`, `mods/common/chrome/faction-catalog.yaml`, `mods/common/fluent/faction-catalog.ftl` | The screen. |

Classic declares the chrome and messages in `mods/ra/mod.yaml`. The product's
`scripts/prepare-ra2.py` adds them to the integrated RA2 manifest. Other mods
that share `common|chrome/mainmenu.yaml` hide the **Factions** button, and the
Quit button moves up to fill the gap.

## Catalog location

The engine tries these in order and uses the first that exists:

1. `OPENRA_AI_CATALOG`
2. `$OPENRA_AI_ROOT/catalog/factions.json`
3. `EngineDir/catalog/factions.json` (macOS bundle, where Resources is the engine dir)
4. `EngineDir/../../catalog/factions.json` (Windows package, product submodule)
5. `EngineDir/../OpenRA-AI/catalog/factions.json` (canonical sibling checkout)

If none exists, the screen still works from rules alone and hides web links.

## Validation rules

`--check-faction-catalog` fails when:

- the catalog has no entry for the mode;
- the links or profile membership are malformed;
- a catalog faction implemented in the mode has no faction pack, or its pack is not loaded;
- a faction pack is missing from the catalog;
- a catalog unit is missing from the rules, cannot be built by its faction, or is absent from its pack roster;
- a roster actor is missing from the rules;
- an actor only one faction can build is missing from that faction's roster and catalog;
- a unit has a variant for a mode where its faction is unavailable.

Set `OPENRA_UTILITY_EXPERIENCE_PROFILE` to the catalog profile for the mode:
`world-war-iii` for Classic, `ra2-modern` for RA2.

## Deterministic captures

- `OPENRA_AI_START_FACTION_CATALOG=1` opens the catalog from the main menu.
- `OPENRA_AI_CAPTURE_FACTION_CATALOG=<steps>` takes a screenshot after each
  step. Steps are comma-separated:
  - `faction[:actor]` selects a faction and optionally a unit;
  - `@key:RIGHT` sends any `Keycode` name;
  - `@back` and `@experience` press those buttons.
- `OPENRA_AI_CAPTURE_FACTION_CATALOG_EXIT=1` quits the game after the last step.
- `OPENRA_AI_CAPTURE_OPEN_FACTION_CATALOG=1` works with the existing
  `OPENRA_AI_CAPTURE_FACTION_PACK` Experience capture: it follows the pack's
  **Open in Faction Catalog** button.
