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
