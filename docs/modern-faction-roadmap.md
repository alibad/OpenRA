# Modern faction graphics roadmap

This roadmap extends the optional Experience faction system without replacing
the classic Red Alert factions. Fresh installs select **World War III**, which
enables the five completed built-in factions and the reusable capability
portfolio. Players can select **AI Assistant Only** for the classic base
simulation without optional faction or capability packs.

The next production phase is graphics-first, but not graphics-only: a faction
is not complete until its art, sequences, weapons, abilities, AI metadata, and
live gameplay all agree. Actor ids and visual contracts are frozen before
sprite production so research and implementation can proceed in parallel
without creating incompatible assets.

## Contract status

Gate 1 (research and identity contract) has been delivered for all four production
units. The contracts are the authority on rosters, actor ids, and visual landmarks;
this roadmap remains the authority on sequencing and gates.

| Faction | Contract | State |
| --- | --- | --- |
| United States (`usa`) | [`faction-spec-usa.md`](faction-spec-usa.md) | **Complete.** All decisions settled. One blocker before freeze: the source links that block automated retrieval must be opened and confirmed by a human (see its §9.1) |
| Israel (`israel`) | [`faction-spec-israel.md`](faction-spec-israel.md) | Sourced outline. Cross-faction rulings applied; expands after the U.S. contract is frozen |
| Germany (Modern) (`bundeswehr`) | [`faction-spec-bundeswehr.md`](faction-spec-bundeswehr.md) | Sourced outline. The only faction whose entire source set was directly retrievable |
| The Koreas (`northkorea`, `southkorea`) | [`faction-spec-koreas.md`](faction-spec-koreas.md) | Sourced outline. Carries a two-source corroboration policy for every North Korean actor |

No actor has been removed from any roster in this programme. Where a proposal would have
cut one, it is recorded with its reasoning and the roster kept intact.

## Programme-wide rules

These bind every faction pack and were settled during the U.S. contract rather than
being answered separately four times. Each is reversible, but reversing one that removes
content requires product-owner approval.

- **Shared airframes.** An airframe family may repeat across packs provided each
  appearance has a **distinct tactical role** and ships its **own sprite set**.
  Palette-only variants remain forbidden. **The rule never applies to art that already
  exists or is in progress** — anything drawn is grandfathered permanently.
- **Mobile air defense.** Any faction may field one. Distinctness is enforced on
  **mechanics**, not by rationing the slot.
- **Active protection.** The United States owns the turret-mounted, tank-only version;
  Israel owns the hull-mounted carrier version with coverage extending to nearby
  infantry. Different magazines, recharges, and mount points.
- **Naming and trademarks.** Generic display names for buildings and defenses; real
  designations for vehicles, aircraft, and ships. Real-world basis stays documented in
  the contract regardless.
- **Equipment status discipline.** Prototypes and procurement are labelled as such and
  never presented as current inventory. This already applies to M1E3, XM30,
  Skyranger 30, Chunmoo 3.0, and every proposed Korean domestic programme.
- **Depiction.** Unit descriptions state gameplay function and do not editorialise. No
  pack in this programme gets a fanatic, human-wave, or morale-suicide mechanic, and no
  unit, name, or line references a real operation, place, or event.

## Current baseline

- China, Iran, Saudi Arabia, Yemen, and Turkey already have distinct infantry
  rosters governed by `docs/custom-infantry-identities.md`.
- Their infantry have native-scale world sheets, production icons, remap
  coverage, complete action sets, and role-specific weapons or abilities.
- Their vehicles, aircraft, ships, structures, icons, and faction previews
  still require a complete cross-faction visual audit before that art can be
  treated as final.
- There is no selectable United States faction. The internal `allies` faction
  is a hidden shared base, not a country roster.
- The existing selectable `germany` faction is the classic Red Alert
  Chronoshift/Chrono Tank subfaction. It must remain compatible and unchanged.
  A contemporary German roster will use a separate internal id such as
  `bundeswehr` and the display name **Germany (Modern)**.

## Priority order

1. **Close the existing art gap.** Audit every China, Iran, Red Sea, and Turkey
   vehicle, aircraft, ship, structure, icon, and preview. Repair bad facings,
   scale, pivots, palette remap, silhouettes, and repeated art before expanding
   the catalog.
2. **United States.** Add a new optional `usa` faction rather than repurposing
   the hidden Allies base. This is the first wholly new graphics set.
   *Gate 1 complete — see `faction-spec-usa.md`. Doctrine is networked joint
   fires built on one mechanic, fires-network coverage, which was verified
   buildable from existing engine traits with no new code.*
3. **Israel.** Build a defensive combined-arms faction around armored
   protection, reconnaissance, unmanned systems, and layered interception.
   *Sourced outline delivered.*
4. **Germany (Modern).** Add `bundeswehr` while retaining classic Germany.
   Emphasize protected mechanized teams, precision fires, and air defense.
   *Sourced outline delivered.*
5. **The Koreas as a pair.** Design North Korea and South Korea together so
   artillery pressure, fortification, reconnaissance, counterbattery fire, and
   missile defense have deliberate counters rather than one-sided gimmicks.
   *Sourced outline delivered, with the heaviest remaining research burden of
   the four — every North Korean actor needs two independent sources.*
6. **Later candidates.** Taiwan and India/Pakistan are stronger faction-pair
   candidates than isolated additions. Ukraine, Russia, Sudan, Myanmar, the
   Sahel, and eastern DRC are better evaluated first as scenario or coalition
   packs; an active conflict alone is not a sufficient faction design.

## Research rosters to freeze before drawing

These are candidate production rosters, not permission to copy proprietary
game art or a claim that every prototype is fielded. Each research contract
must distinguish current service, procurement, prototype, and fictionalized
near-future equipment.

### United States (`usa`)

Doctrine: networked joint fires with strong reconnaissance and logistics, but
expensive frontline platforms and dependency on connected support units.

| Domain | Candidate visual set |
| --- | --- |
| Infantry | Rifle squad, Javelin team, JTAC/forward observer, special-operations commando |
| Vehicles | M1A2 SEP v3 or explicitly near-future M1E3, Bradley or XM30, Stryker, HIMARS, mobile short-range air defense, recovery/logistics vehicle |
| Aircraft | F-35A, AH-64E, MQ-9-class unmanned aircraft, UH-60 or CH-47 transport |
| Navy | Arleigh Burke-class destroyer, Virginia-class submarine, amphibious assault/support ship |
| Defenses | Patriot/LTAMDS-style battery, counter-UAS emplacement, networked sensor node |

The M1 decision is a required accuracy gate: use the fielded M1A2 if the pack
represents the present day, or label the M1E3 and XM30 honestly as near-future
systems. They must not silently appear as mature current inventory.

**Resolved.** The pack uses the fielded **M1A2 SEP v3**, keeping the roster present-day
and consistent with every shipped pack. M1E3 and XM30 are recorded as prototypes and are
not in the roster. Because Saudi Arabia already ships `M1A2S`, the U.S. tank carries
three mandatory visual deltas and a concept-board comparison gate; see
`faction-spec-usa.md` §8.1. The frozen roster and actor ids live in that contract, not in
the candidate table above.

### Israel (`israel`)

Doctrine: protected maneuver and rapid intelligence-to-fire response backed by
layered point defense; vulnerable when interceptors, sensors, or supply are
overwhelmed.

| Domain | Candidate visual set |
| --- | --- |
| Infantry | Rifle squad, Spike team, drone/EW operator, special-operations commando |
| Vehicles | Merkava Mk 4 Barak, Namer, Eitan, self-propelled artillery, reconnaissance/unmanned ground platform |
| Aircraft | F-35I, AH-64, Heron-class unmanned aircraft, utility transport |
| Navy | Sa'ar 6-class corvette with C-Dome, Dolphin-class submarine, patrol/landing craft |
| Defenses | Iron Dome-style point defense, heavier layered-defense support power, sensor/EW node |

Strategic missile-defense names may be used only where licensing, presentation,
and abstraction are appropriate. The skirmish game should model counterplay,
not promise literal real-world interception performance.

### Germany (Modern) (`bundeswehr`)

Doctrine: protected mechanized combined arms, networked infantry, precision
artillery, and strong air defense; high unit quality, cost, and repair burden.

| Domain | Candidate visual set |
| --- | --- |
| Infantry | Panzergrenadier, MELLS anti-tank team, drone/EW operator, KSK commando |
| Vehicles | Leopard 2A7V or A8, Puma, Boxer, PzH 2000 or RCH 155, Skyranger-class air-defense vehicle, engineering/recovery vehicle |
| Aircraft | Eurofighter, H145M, CH-47F procurement-era transport, reconnaissance drone |
| Navy | F125/F126-style frigate, Type 212A submarine, Braunschweig-class corvette |
| Defenses | IRIS-T-style battery, radar/sensor emplacement, protected anti-armor position |

### North and South Korea (`northkorea`, `southkorea`)

North Korea should use massed conventional artillery, concealment, tunnels,
fortification, and older but numerous platforms. Strategic nuclear weapons are
scenario-level narrative devices, not ordinary skirmish superweapons. South
Korea should answer with reconnaissance, mobile armor, counterbattery fires,
air power, and layered air and missile defense.

The paired research contract should consider K2, K9, Chunmoo, K21, Apache,
F-35A, Aegis ships, and modern submarines for South Korea. North Korean actor
choices require a separate source audit because public naming and capability
claims are less reliable; visual contracts should be based on corroborated
platform evidence, not a single adversarial description.

## Per-faction production gates

Every faction passes the following gates in order. A later gate cannot be used
to conceal an incomplete earlier one.

### 1. Research and identity contract

- Freeze the internal faction id, doctrine, tradeoffs, random pool, and actor
  ids before creating art.
- For each actor, record role, real-world inspiration, service/prototype status,
  scale reference, visual landmarks, weapon or ability, counterplay, and how it
  differs from every existing actor.
- Prefer four readable infantry roles, five to seven vehicles, three or four
  aircraft, and three naval actors. Add more only when each has a distinct
  tactical and visual job.
- Do not reuse a stock weapon under a new name as the sole faction identity.

### 2. Concept and silhouette review

- Produce a single review board showing all units at approximately in-game
  scale on the snow, temperate, and desert palettes.
- Check recognizability before animation: infantry headgear/weapon/equipment,
  vehicle hull and turret geometry, aircraft planform, and ship profile must be
  legible without labels.
- Mark player-color regions intentionally. Remap color must identify ownership
  without erasing national/material color or important equipment.
- Reject silhouette duplicates and palette-only variants before building full
  frame sheets.

### 3. Shipping art

- Generate deterministic indexed assets from project-owned source art and keep
  the generator inputs under version control.
- Infantry use the faction's YAML-authoritative native canvas (normally 50x39),
  a complete 713-frame action contract, eight genuinely different facings,
  readable weapons/equipment, and 64x48 indexed production icons.
- Vehicles use the exact sequence counts declared by YAML. Directional vehicles
  normally require 32 hull facings and, where applicable, a separately pivoted
  32-facing turret with correct OpenRA handedness.
- Aircraft and ships receive the same full-facing, fixed-pivot, no-clipping,
  remap, shadow, and icon checks appropriate to their sequence contract.
- Buildings and defenses must have construction, idle, damaged, destroyed, and
  faction-specific active states wherever their gameplay exposes those states.
- Faction preview art is presentation-only and must be original, licensed, and
  declared by the Experience pack.

### 4. Structural art audit

- Verify file hashes and dimensions, indexed palettes, transparent index,
  remap-ramp use, frame counts, sequence reachability, fixed pivots, shadows,
  and bounds.
- Render contact sheets for all facings and animation groups. Review them at
  nearest-neighbor native scale and at the normal game zoom.
- Compare every unit with a role-paired native OpenRA reference. A technically
  valid file does not pass if it is unreadable in motion.
- Treat icons as separate authored assets; a scaled world frame is not an
  acceptable production portrait.

### 5. Gameplay and AI integration

- Add faction-local rules, weapons, sequences, localization, voices where
  available, build prerequisites, starting units, tech progression, support
  powers, strategic roles, and bot production metadata.
- Validate every weapon target filter, minimum range, reload, projectile,
  condition, duration, deploy state, and status decoration against the actor's
  visual promise.
- Check captured-tech behavior and random-pool membership without editing
  another optional faction's private rules.
- Run deterministic bot-versus-bot and player-start smokes on land and naval
  maps; record units the bot cannot build, use, counter, repair, or transport.

### 6. Live acceptance

- Inspect all facings in a running game, including movement transitions,
  turret tracking, recoil, firing, prone/death states, embark/disembark, damage,
  cloaking, deployment, and player-color changes.
- Test against light and dark terrain and at least two player colors.
- A human playtest must be able to name the broad unit role from the sprite and
  distinguish all same-domain faction units without selecting them.
- Only then update the Experience catalog version and integrate the faction.

## Parallel ownership model

Parallel work is safe only across stable boundaries:

| Workstream | Owns | Must not edit |
| --- | --- | --- |
| Research/specification | Official-source dossier, actor-id proposal, role/ability/counterplay matrix, overlap review, descriptions | Binary art, sequence YAML, generated manifests, shared catalogs |
| Art production | Source art, deterministic generators, SHP/PNG outputs, icons, previews, sequence/frame contracts, visual audits | Unfrozen actor ids or gameplay claims |
| Gameplay implementation | Faction-local rule/weapon YAML, balance constants, AI roles, prerequisites, tests | Binary art or shared catalogs while art is changing |
| Integration | Experience catalog, shared dependencies, conflict resolution, full validation, canonical `main` | Unreviewed research or incomplete assets |

One integration owner accepts changes in that order. Global files such as
`mods/ra/experiences.yaml`, shared sequence catalogs, and generator manifests
are integration-owned even when another workstream proposes their contents.

**Removals are product decisions, not workstream decisions.** A workstream may propose
cutting a unit and must record the reasoning, but it does not act on that proposal.
This applies to proposed content as much as to shipped content — the habit is what
matters, because the cost of getting it wrong rises sharply once art exists.

## Planning checkpoints

- **Checkpoint A:** existing optional factions have complete domain-wide art
  audits and an explicit remaining-gap list. *Not started.*
- **Checkpoint B:** the U.S. contract and concept board are approved; only then
  generate the full U.S. sheets. *Contract half delivered and settled; blocked
  only on human source verification. Concept board not started, and it carries
  two mandatory side-by-side comparisons: `USMBT` beside `M1A2S`, and `USICV`
  beside Turkey's `ARAS8`, both at native scale on all three palettes.*
- **Checkpoint C:** the U.S. faction passes live acceptance and becomes the
  quality bar for Israel and Germany (Modern).
- **Checkpoint D:** Israel and Germany pass independently without altering the
  classic Germany experience.
- **Checkpoint E:** both Korean contracts are approved together, then their art
  and gameplay are balanced as a pair.

## Official research anchors

These links are starting points, not a substitute for the per-actor evidence
table. Re-check them at contract freeze time because equipment status changes.

**Verification caveat, learned the hard way.** `army.mil`, `af.mil`, `afsoc.af.mil`,
`navy.mil`, `dia.mil`, `media.defense.gov`, `crsreports.congress.gov`, and `mod.gov.il`
all return HTTP 403 to automated requests. Their URLs can be confirmed as well-formed and
on the right domain, but the pages cannot be read that way. `bundeswehr.de`,
`dapa.go.kr`, `mnd.go.kr`, `idf.il`, `dvidshub.net`, and `rtx.com` do respond.

This is not a formality: the one manufacturer page that *could* be fetched during the
U.S. research pass immediately contradicted a claim taken from a search summary, and the
claim was retracted. Treat any citation that was never opened directly as unverified
until a human opens it.

- U.S. Army: [Army Transformation Initiative](https://www.army.mil/article-amp/285100/letter_to_the_force_army_transformation_initiative), [ground combat platforms](https://cpeground.army.mil/Combat-Platforms/), [M1E3 early prototype](https://www.army.mil/article-amp/290052/us_army_unveils_early_abrams_prototype_at_north_american_international_auto_show), and [Project ARIA](https://www.army.mil/article/290864/harnessing_ai_for_the_future_army_unveils_project_aria).
- Bundeswehr: [Leopard 2](https://www.bundeswehr.de/de/ausruestung-technik-bundeswehr/landsysteme-bundeswehr/leopard-2), [Puma](https://www.bundeswehr.de/de/ausruestung-technik-bundeswehr/landsysteme-bundeswehr/schuetzenpanzer-puma), and [procurement overview](https://www.bundeswehr.de/de/beschaffung/beschaffung-planungsprozess).
- Israel Ministry of Defense: [armored production plan](https://mod.gov.il/en/press-releases/press-room/israel-defense-procurement-committee-approves-approximately-15b-plan-to-expand-tank-and-apc-production), [missile-defense directorate](https://mod.gov.il/en/departments/directorate-of-defense-research-development-ddrd), and [David's Sling upgrade tests](https://mod.gov.il/en/press-releases/press-room/davids-sling-air-and-missile-defense-system-successfully-completes-advanced-upgrade-tests).
- Republic of Korea Ministry of National Defense: [force-improvement systems](https://www.mnd.go.kr/mnd/235/subview.do) and [AI/unmanned defense priorities](https://www.mnd.go.kr/mnd/176/subview.do).
