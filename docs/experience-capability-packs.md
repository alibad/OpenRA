# Experience capability packs

The in-game **AI > Experience Builder > Import Module** action installs reusable,
data-only OpenRA modules. A module may contribute rules, weapons, sequences,
cursors, chrome, voices, notifications, and music. Imported files are mounted
below `experience-packs/<id>/`, so they cannot silently replace built-in OpenRA
data. Compiled or executable code is rejected.

## Package layout

Create a folder or ZIP containing one `pack.yaml` and the declared files:

```yaml
CapabilityPack:
	Id: example-module
	Title: Example Module
	Version: 1
	Author: Your Name
	License: GPL-3.0-or-later
	Source: Original work
	SourceUrl: https://example.invalid/project
	TargetMod: ra
	EngineApi: experience-v2
	RightsAcknowledged: true
	Component:
		Title: Example gameplay module
		Description: Adds one reusable gameplay capability.
		Effects: Adds the example capability to compatible actors.
		Tradeoffs: Has no effect until an actor opts into its rules.
		Scope: Compatible actors and authored maps only.
		Category: Units and effects
		Preview: preview.png
		Version: 1
		Source: Original work
		License: GPL-3.0-or-later
		Rules: rules.yaml
		Weapons: weapons.yaml
		Dependencies: faction-and-subfaction-contract
		Parameters:
			strength:
				Title: Effect strength
				Description: Percentage applied by the module's runtime behavior.
				Group: Balance
				Type: Integer
				Default: 100
				Minimum: 25
				Maximum: 200
				Step: 5
				Unit: percent
```

Paths inside the module must be relative to the package. References from one
module file to another use the mounted name, for example
`experience-packs/example-module/images/icon.png`.

Every selectable module must declare a `Preview` PNG inside its package. This
image is shown on the Experience Builder detail card, and the pack is rejected
if the declaration is absent, names a non-PNG file, or points to a missing file.

The bundled capability previews under `mods/ra/uibits/experience-previews/`
are original project presentation generated with OpenAI GPT Image on
2026-08-14. They do not reuse presentation assets from the surveyed mods or
from the Command & Conquer games.

## First-class faction packs

`Kind: Module` (the default) is a concrete gameplay capability. Its card must
identify the units or map objects changed, how a player uses the feature, and
its costs or counters. Use `Kind: Authoring` for reusable contracts, metadata or
mission-only building blocks that need compatible authored content. These stay
selectable under Tools & Compatibility, but do not inflate the number of active
gameplay capabilities. `Kind: Internal` is a dependency managed automatically;
it disappears when the last component requiring it is disabled.

Dependencies are loaded before their dependents. A conflict declared by either
component applies in both directions, including conflicts within dependency
closures. Selecting a conflicting module removes the old module and components
that can no longer satisfy their dependencies. Contradictory presets, cycles,
missing dependencies and internally conflicting dependency closures are rejected
before gameplay files load. Saved selections retain unrelated modules and their
parameter choices.

In World War III, faction selection alone does not add the optional stock
Artillery/Tesla upgrades. China's carrier pack explicitly includes its real drone
wing implementation. The default World War III experience still includes the
full selection; each gameplay card now describes the concrete changes rather
than presenting an implemented power as a future framework.

Set `Kind: Faction` to make a capability pack appear as a faction card in the
Experience Builder. A faction pack is still data-only and removable, but it has
a stricter contract so the engine can present, validate, randomize, and compose
it without hard-coded country names.

```yaml
		Kind: Faction
		Rules: rules.yaml
		Weapons: weapons.yaml
		Sequences: sequences.yaml
		Dependencies: faction-and-subfaction-contract, naval-combat-archetypes
		Faction:
			InternalName: example-country
			Side: Allies
			RandomPool: RandomAllies
			Doctrine: mobile-defense
			Preview: preview.png
			Roster:
				Infantry: EXRIFLE, EXENGINEER
				Vehicles: EXTANK, EXARTILLERY
				Aircraft: EXFIGHTER
				Navy: EXCORVETTE
				Buildings: FACT, WEAP, TENT, HPAD, SYRD
				Defenses: PBOX, GUN, AGUN
```

Faction packs declare their preview inside `Faction` instead of at the component
root. The six roster categories are required and drive the card summary and
validation; actor ids may refer to base actors or actors declared by the pack.
The rules must register the matching faction and random-pool membership:

```yaml
World:
	Faction@example-country:
		Name: Example Country
		InternalName: example-country
		Side: Allies
		Description: A mobile combined-arms faction.
		RandomFactionMemberOf: RandomAllies

Player:
	ProvidesFactionDoctrine@EXAMPLE:
		Factions: example-country
		Prerequisites: side.allies, country.example-country, doctrine.mobile-defense
	CapturedTechnologyManager@EXAMPLE:
		Factions: example-country
```

`RandomFactionMemberOf` appends the faction to an existing random pool at
runtime, so a pack never has to replace `RandomAllies` or `RandomSoviet`.
Strategic AI classifies actors by `StrategicRole.Domain`, allowing new air and
naval actors to work without editing central actor-id lists. If a pack integrates
with captured technology, give it a namespaced `CapturedTechnologyManager`
instance as shown above.

Keep faction-specific actor hooks in the faction's own rules file. Generic air,
naval, weapon, and mission modules should expose abstract contracts and must not
name actors owned by an optional faction. Bundled maps that require a faction
declare the faction's rule, weapon, and sequence files in the map package; the
loader de-duplicates files that are already active through the current
experience.

## Reusing another mod

Credit alone does not create permission. Before setting
`RightsAcknowledged: true`, verify the license of the source code and each asset:

- preserve the upstream copyright and license notices;
- comply with the source license, including source-sharing obligations for
  derivatives when applicable;
- do not redistribute proprietary Command & Conquer art, audio, video, fonts,
  trademarks, or third-party material merely because another mod contains it;
- record `Source`, `SourceUrl`, `Author`, and `License` accurately;
- keep replacements for art, audio, video, palettes, and cursors in a
  Presentation Pack when they do not change gameplay.

When a source mod contains custom C# traits, port those traits into the engine
and review them like normal source code. Capability packs intentionally cannot
load arbitrary binaries. This keeps imported modules inspectable, multiplayer
deterministic, and removable from one place.

Imported gameplay content participates in the Experience fingerprint. Any file
change produces a different multiplayer fingerprint, even if the package id and
version were not changed.
