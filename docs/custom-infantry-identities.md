# Custom infantry identity contract

The China, Iran, Red Sea, and Turkey infantry rosters use one native-scale
animation contract but must not use palette-only body copies. World art is
rebuilt by `packaging/artwork/generate_faction_infantry_art.py`, which authors
role-specific headgear, packs, antennas, armor, and weapon silhouettes and
rejects duplicate action masks across the complete 20-actor roster.

Every actor must expose at least two readable gameplay dimensions, including
one visible dimension such as a projectile, burst, beam, marker, overlay, or
deploy state. Cost, damage, range, reload, and renamed stock weapons do not by
themselves establish a custom identity.

## China

| Actor | Visual read | Weapon and tactical identity |
| --- | --- | --- |
| `CNRIFLE` | Visored helmet, armor, compact bullpup | Controlled two-round gold tracer burst specialized toward infantry and light vehicles; benefits from the command network. |
| `CNNETWORK` | Visor, terminal, antenna pack | Four-pulse cyan carbine; deploys into an immobile long-range sensor and missile-jamming node. |
| `CNPORTABLE` | Armored twin-tube launcher and battery pack | Player-controlled deploy toggle between distinct anti-armor and anti-air missiles. |
| `REDSPEAR` | Officer cap, command radio, long precision rifle | Visible precision double-tap against infantry and vehicles; firepower/reload command network for nearby Chinese units. |

## Iran

| Actor | Visual read | Weapon and tactical identity |
| --- | --- | --- |
| `IRBAS` | Headwrap, bandolier, long rifle | Three-round visible burst that suppresses infantry movement and return-fire cadence. |
| `IRATGM` | Scarf, battery, long optic and tripod | Long-range, minimum-range missile; cannot move while reloading and visibly tracks/slows vehicles. |
| `IRDC` | Headset, twin antennas, controller | Two-shot control carbine plus a visible signal spike that jams reload systems; detects cloaks and unlocks Simorgh support. |
| `SHADOWONE` | Hood, suppressed SMG, remote-charge pack | Cloaked burst commando with building sabotage, reload disruption, and demolition. |

## Saudi Arabia and Yemen

| Actor | Visual read | Weapon and tactical identity |
| --- | --- | --- |
| `SANG` | Heavy helmet, shoulder armor, ammunition pack | Three-round gold tracer burst; deploys to brace for range, fire cadence, and damage resistance at the cost of mobility. |
| `SAJTAC` | Headset, large radio, laser designator | Visible laser mark that only amplifies compatible Red Sea guided strikes; also provides reconnaissance and cloak detection. |
| `SAAT` | Armored optic helmet and tripod ATGM | Slow long-range missile with a pronounced minimum range and heavy-armor specialization. |
| `FALCON1` | Field cap, command rig, precision rifle | Suppressed precision double-tap, demolition, and a build-limit-one F-15SA precision strike. |
| `YMR` | Headwrap, shawl, satchel, long rifle | Dusty two-shot ambush volley with stationary camouflage and low-cost massing. |
| `YRPG` | Scarf, rocket pack, RPG silhouette | Short-range anti-armor projectile whose range and reload cadence improve inside a drone-guidance field. |
| `YSPOT` | Headwrap, twin antennas, drone tablet | Visible two-shot support carbine, long reconnaissance, cloak detection, and a guidance field for nearby launchers. |
| `WADIGHOST` | Hood, compact suppressed weapon, wired charge | Persistent camouflage, infiltration, suppressed double-tap, and delayed remote demolition. |

## Turkey

| Actor | Visual read | Weapon and tactical identity |
| --- | --- | --- |
| `TRRIFLE` | Helmet, large radio pack, compact rifle | Four-round red tracer burst; gains firepower, protection, and speed near mechanized transports. |
| `TRAT` | Camouflage hood, satchel, long launcher | Minimum-range anti-armor ambusher with stationary concealment and command-tempo synergy. |
| `TRDRONEOP` | Headset, twin antennas, drone tablet | Visible designator that makes a target take additional damage, plus long reconnaissance and cloak detection. |
| `GREYWOLF` | Beret, command rig, compact carbine | Tight three-round tracer burst, stationary concealment, and a movement/reload aura for nearby Turkish infantry. |

## Validation

- World sheets: 713 indexed frames, fixed 50x39 canvas, eight unique stand
  facings, complete upright/prone/death/parachute coverage, and no clipping.
- Production icons: indexed 64x48 native-style portrait treatment, correct
  label, and a role badge that matches the actor's equipment or ability.
- Player color: remap-ramp coverage remains present in every world sheet.
- Gameplay: descriptions must state the actual interaction; status duration,
  valid and invalid targets, decoration, stacking, and deploy behavior require
  live checks after YAML validation.
