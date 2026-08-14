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

## First-class faction packs

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

`Preview` must be a declared PNG inside the pack. The six roster categories are
required and drive the card summary and validation; actor ids may refer to base
actors or actors declared by the pack. The rules must register the matching
faction and random-pool membership:

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
