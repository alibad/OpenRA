# Faction contract: United States (`usa`)

Status: **complete; all decisions settled. Ready to freeze once §9.1 source verification
is done by a human.**
Gate: roadmap gate 1 (research and identity contract) for `docs/modern-faction-roadmap.md`
priority 2.

**The roster in §3 is the full roster. No actor has been removed from any faction in this
programme.**

Every §8 decision is now settled, in one of three ways:

- **Ruled** — configuration and rules choices that add or constrain: §8.1, §8.2, §8.5,
  §8.6, §8.7, §8.11.
- **Verified** — §8.8, the engine-feasibility question, answered by reading the engine
  rather than by judgement.
- **Decided by the product owner** — §8.3: `USICV` stays, with a concept-board gate.
- **Defaulted to the non-destructive option** — §8.4, §8.9, §8.10. No preference was
  expressed on these three, and all three were removal proposals, so each defaults to the
  choice that deletes nothing. Each is labelled and reversible.

The stricter variants of §8.9 and §8.10 remain recorded so they can be adopted later, but
adopting one would be a removal decision and would need its own approval.

All sources in this document were accessed **2026-08-16**. Equipment status changes;
re-check every source at freeze time as required by the roadmap.

This document is specification only. It creates no art, no sequence YAML, no rules,
and no Experience catalog entry. Nothing here may be treated as implemented.

---

## 1. Doctrine, strengths, tradeoffs, tech shape, counterplay

### 1.1 Doctrine statement

**Networked joint fires.** The United States pack wins by *seeing first and shooting
once*. Its economy is not larger than anyone else's, and its individual platforms are
not individually unbeatable. What it has is a **fires network**: a chain of sensors and
controllers that, while intact, makes its guided weapons faster, longer-ranged, and more
accurate than anyone else's guided weapons. When that chain is broken, the faction still
fights, but it fights as an expensive conventional army with fewer units than its
opponent bought for the same money.

This is the same real doctrinal idea the Army describes for itself — deep sensing feeding
long-range precision fires through an intelligence ground station — abstracted to a
skirmish-legible mechanic.

Sources for the doctrinal framing:
[Army Transformation Initiative letter to the force](https://www.army.mil/article-amp/285100/letter_to_the_force_army_transformation_initiative),
[TITAN system being developed to tie 'deep sensing' to long-range fires](https://www.army.mil/article/228867/titan_system_being_developed_to_tie_deep_sensing_to_long_range_fires),
[Project ARIA](https://www.army.mil/article/290864/harnessing_ai_for_the_future_army_unveils_project_aria).
Accessed 2026-08-16.

### 1.2 The one faction mechanic

**Fires network coverage.** One condition, granted by four sources, consumed by a
restricted list of weapons.

| Network source | Actor | Coverage |
| --- | --- | --- |
| Tactical operations center | `USTOC` (building) | Large static radius around the base |
| Networked sensor node | `USNODE` (defense) | Medium static radius, cheap, forward-placeable |
| Forward observer | `USJTAC` (infantry) | Small mobile radius, dies to anything |
| Armed reconnaissance UAS | `USMQ9` (aircraft) | Small mobile radius, dies to any real air defense |

Rules that make this a design and not a blanket buff:

1. Coverage improves **only guided or observed weapons** — `USHIMARS` rockets,
   `USJAV` missiles, `USF35` standoff strike, `USDDG` land-attack fire, `USIAMD`
   intercepts. It must never improve plain gunfire, ramming, cannon, or repair.
2. Coverage does **not** stack. Four sources over one unit is the same as one.
3. Uncovered guided weapons are **deliberately mediocre** — worse than the equivalent
   Turkish or Chinese weapon at the same cost. The faction is priced assuming coverage.
4. Coverage is **visible to both players** — a ground decoration or overlay on covered
   units — so the opponent can see what to kill.

**Engine feasibility: verified, not assumed.** All four rules above are buildable from
traits that already ship and are already combined this way by China, Turkey, and the Red
Sea pack. No new engine code is required. The trait-by-trait mapping and the shipped
precedents are in §8.8.

### 1.3 Strengths

- Best-in-catalog standoff artillery and precision strike **while networked**.
- The only roster with a genuine layered air and missile defense chain
  (`USSHORAD` mobile, `USCUAS` counter-drone, `USIAMD` high-tier) that answers the
  drone-heavy Iran, Yemen, Turkey, and China packs.
- Strong armored survivability at the top of the tech tree.
- Good, but not free, force projection: one purpose-built amphibious/logistics hull.

### 1.4 Tradeoffs (these are the loss conditions, and they must stay real)

- **Expensive.** Comparable slots cost roughly 10–25% more than the Turkish or Chinese
  equivalent. The pack should lose most even-economy brawls it enters unprepared.
- **Network-dependent.** Loss of `USTOC` plus forward nodes removes the faction's
  entire competitive edge in one stroke.
- **Jammable by systems that already ship.** Turkey's `SANCAK` electronic-warfare
  carrier, China's `CNSPECTRUM` control node, and Iran's `IRDC` drone controller are
  all existing actors whose stated identity is signal disruption. This is a feature: it
  gives three shipped factions a new reason to build a unit they own. It is also
  **free** — `SANCAK` and stock `MRJ` already grant a generic `jammed` condition, so
  gating the U.S. multipliers on `!jammed` makes them counters without touching a single
  line of another faction's rules. Verified in §8.8.
- **Fragile enablers.** `USJTAC` and `USMQ9` are soft. Killing them is cheap.
- **Thin low tech.** Before a radar dome, the U.S. roster is one carrier and one rifle
  squad and is beatable by early aggression.

### 1.5 Tech shape

| Tier | Prerequisite pattern | What unlocks |
| --- | --- | --- |
| `~techlevel.infonly` | `~tent` | `USRIFLE` |
| `~techlevel.low` | — | `USICV`, `USTOC`, plus stock `JEEP` (see below) |
| `~techlevel.medium` | `dome` | `USJAV`, `USJTAC`, `USIFV`, `USSHORAD`, `USRECOV`, `USMQ9`, `USUH60`, `USNODE`, `USCUAS`, `USLPD` |
| `~techlevel.medium` | `fix` | `USMBT` |
| `~techlevel.high` | `atek` | `TALONSIX`, `USHIMARS`, `USF35`, `USAC130`, `USDDG`, `USSSN`, `USIAMD` |

The shape is deliberately **back-loaded**: the faction's identity lives at `atek`.
Everything before that is competent and unexciting.

**Note on stock vehicle availability.** Two facts were verified against the shipped rules
while §8.3 was being considered. They hold whichever way §8.3 is decided, so they are
recorded here rather than inside the decision:

- Stock `APC` is gated `~vehicles.soviet, ~techlevel.low` (`mods/ra/rules/vehicles.yaml:468`).
  An Allies-side faction never had it, so the U.S. cannot fall back on it as a carrier.
- Stock `JEEP` is gated `~vehicles.allies, ~techlevel.low` (`mods/ra/rules/vehicles.yaml:418`).
  The U.S. rules should **not** add a `~!vehicles.usa` exclusion for it, the way
  `mods/ra/rules/turkey.yaml:966` does for Turkey. `JEEP` is a cheap low-tier scout and
  harass option and costs the faction nothing to keep.

### 1.6 Intended counterplay (what the opponent is supposed to do)

| Opponent problem | Intended answer |
| --- | --- |
| U.S. artillery outranges me | Kill `USJTAC`/`USMQ9`/`USNODE` first; uncovered `USHIMARS` is ordinary |
| U.S. air defense is layered | Saturate one layer at a time; `USCUAS` cannot engage crewed aircraft, `USSHORAD` has a shallow magazine |
| U.S. tanks shrug off my rockets | Active protection has a finite magazine per engagement; volley it, or use guns |
| U.S. units are individually better | Buy more units; the U.S. player cannot afford parity in count |
| U.S. network is everywhere | Build the jammer your faction already has (`SANCAK`, `CNSPECTRUM`, `IRDC`) |

---

## 2. Faction registration proposal

```yaml
InternalName: usa
Display name: United States
Side: Allies
RandomPool: RandomAllies
Doctrine: joint-fires
```

- `usa` is unused. Verified: no match for `usa`, `israel`, `bundeswehr`, `northkorea`,
  or `southkorea` as a faction internal name anywhere under `mods/`.
- `joint-fires` is a new doctrine string. Existing strings in use are
  `expeditionary` (Saudi), `asymmetric-defense` (Yemen), `mobile-defense` (Turkey),
  `layered-denial` (Iran), `networked-combined-arms` (China). No collision.
- The pack must use `RandomFactionMemberOf: RandomAllies` so it appends to the pool
  instead of replacing it, per `docs/experience-capability-packs.md`.
- The hidden internal `allies` faction is **not** touched. Classic `england`,
  `france`, and `germany` are **not** touched.

---

## 3. Proposed roster

| Domain | Actors | Count |
| --- | --- | --- |
| Infantry | `USRIFLE`, `USJAV`, `USJTAC`, `TALONSIX` | 4 |
| Vehicles | `USMBT`, `USIFV`, `USICV`, `USHIMARS`, `USSHORAD`, `USRECOV` | 6 |
| Aircraft | `USF35`, `USMQ9`, `USUH60`, `USAC130` | 4 |
| Navy | `USDDG`, `USSSN`, `USLPD` | 3 |
| Buildings | `FACT`, `WEAP`, `TENT`, `HPAD`, `SYRD`, `USTOC` | 5 stock + 1 new |
| Defenses | `USIAMD`, `USCUAS`, `USNODE` | 3 |

This matches the roadmap's stated preference (four infantry, five to seven vehicles,
three or four aircraft, three naval actors).

Husk and sink actors follow the shipped convention and are reserved here so they cannot
be claimed by another workstream: `USMBT.Husk`, `USIFV.Husk`, `USICV.Husk`,
`USHIMARS.Husk`, `USSHORAD.Husk`, `USRECOV.Husk`, `USF35.Husk`, `USMQ9.Husk`,
`USUH60.Husk`, `USAC130.Husk`, `USDDG.Sink`, `USSSN.Sink`, `USLPD.Sink`.

---

## 4. Actor-id collision audit

Method: every proposed id was checked with a whole-word recursive search across
`*.yaml`, `*.lua`, and `*.cs` in the repository working tree at
`a0f751b972` (`docs: plan modern faction graphics`), plus a full extraction of the
500 top-level keys currently defined across `mods/ra/rules/*.yaml`.

**Result: zero collisions for all 21 proposed ids.**

| Id | Matches in tree |
| --- | --- |
| `USRIFLE`, `USJAV`, `USJTAC`, `TALONSIX` | 0 |
| `USMBT`, `USIFV`, `USICV`, `USHIMARS`, `USSHORAD`, `USRECOV` | 0 |
| `USF35`, `USMQ9`, `USUH60`, `USAC130` | 0 |
| `USDDG`, `USSSN`, `USLPD` | 0 |
| `USTOC`, `USIAMD`, `USCUAS`, `USNODE` | 0 |

Near-miss ids that exist and were checked explicitly so a future rename cannot
silently collide: `U2` (Allies spy plane), `UTILPOL1`, `UTILPOL2`, `M1A2S` (Saudi
Abrams), `F15SA`, `AH64SA`, `SADS`, `SAM`, `SAMAD`.

The `US` prefix is unused by every shipped pack. Existing prefixes are `CN` (China),
`IR` (Iran), `SA`/`SA_` (Saudi), `YM`/`YE_` (Yemen), and `TR` plus Turkish proper
nouns (Turkey). `TALONSIX` follows the shipped commando convention of a call-sign-style
name rather than a prefix — `REDSPEAR`, `SHADOWONE`, `FALCON1`, `WADIGHOST`,
`GREYWOLF`.

**This audit must be re-run immediately before freeze.** It is valid only for the tree
state named above.

---

## 5. Per-actor contracts

Cost bands used below: **Low** 100–600, **Medium** 600–1400, **High** 1400–2600.
Reference points from the shipped Turkey pack: `TRRIFLE` 140, `ARAS8` 900,
`BOZKIR` 1400, `TURNAAH` 1750, `MARMARA` 2200, `SAHINX` 2300.

Infantry scale reference for every entry below: the faction-authoritative native canvas
of **50x39**, with the complete 713-frame action contract, eight genuinely distinct
stand facings, and 64x48 indexed production icons, per
`docs/custom-infantry-identities.md`. Vehicle scale references are given relative to
shipped sprites so the art workstream has a concrete comparison rather than a
real-world measurement.

### 5.1 Infantry

#### `USRIFLE` — Squad Automatic Rifleman

| Field | Contract |
| --- | --- |
| Role | Baseline anti-infantry line unit, cheap, buildable from minute one |
| Real-world basis | U.S. Army close-combat force rifleman with the 6.8 mm Next Generation Squad Weapon family |
| Status | **Fielded.** The rifle and automatic rifle received type classification approval in May 2025 as the M7 and M250 and are being fielded across the Close Combat Force to replace the M4A1 and M249 |
| Source | [PM Soldier Lethality announces type classification approval for NGSW](https://www.army.mil/article/285678/project_manager_soldier_lethality_announces_type_classification_approval_for_next_generation_squad_weapons_ngsw); [XM7 Next Generation Squad Weapon Rifle, PEO Soldier](https://www.peosoldier.army.mil/Equipment/Equipment-Portfolio/Project-Manager-Soldier-Lethality-Portfolio/XM7-Next-Generation-Squad-Weapon-Rifle/). Accessed 2026-08-16 |
| Visual landmarks (pick 3, must survive 50x39) | 1. **Squared-off tan/green helmet with a bulky forward-tilted optic and a stubby mandible cover** — reads as a solid block above the shoulders, unlike China's visor slit or Yemen's headwrap. 2. **Chunky rifle with a fat suppressor can and a boxy sight riding high on the receiver** — the silhouette is thicker and shorter than every other rifle in the catalog. 3. **Front-heavy plate carrier with two square chest pouches**, giving a distinctly rectangular torso |
| Scale reference | Same body mass as `E1`; roughly 6–8% taller in the stand pose than `IRBAS` to read as heavier-kitted |
| Player-color zones | Helmet cover band, the two chest pouch faces, and the upper sleeve. Never the weapon, never the optic, never the boots |
| Animation needs | Full 713-frame contract. Distinct visible **three-round burst** muzzle sequence with a light-tan tracer, not the stock white flash. Prone, death, parachute, garrison |
| Weapon / ability | Three-round burst, light armor-piercing bias so it is not helpless against light vehicles. **No network interaction** — this is deliberate; the baseline rifleman must not be a network dependant |
| Counterplay | Massed cheap infantry beats it on cost; flame, artillery, and any vehicle-mounted autocannon beat it outright |
| Tech tier | `~techlevel.infonly`, prerequisite `~tent`, `~infantry.usa` |
| Cost band | **Low**, 150 |

#### `USJAV` — Javelin Missile Team

| Field | Contract |
| --- | --- |
| Role | Dedicated anti-armor, the faction's answer to massed tanks |
| Real-world basis | FGM-148 Javelin close-combat missile system, dismounted two-soldier team |
| Status | **Fielded**, in service since 1996; the current variant is lighter with improved optics, and a vehicle-mounted CROWS-J configuration exists |
| Source | [Through Lockheed and Raytheon collaboration, the West Point Museum unveils Javelin exhibit](https://www.army.mil/article/270870/through_lockheed_and_raytheon_collaboration_the_west_point_museum_unveils_javelin_exhibit); [Stryker Brigade Combat Team equips modernized missile system](https://www.army.mil/article/256358/stryker_brigade_combat_team_equips_modernized_missile_system). Accessed 2026-08-16 |
| Visual landmarks | 1. **Fat, blunt, square-shouldered launch tube carried at a steep upward angle**, unmistakably different from Iran's long thin tripod ATGM and Yemen's slender RPG. 2. **Boxy command launch unit clamped under the tube with a visible squared eyepiece hood.** 3. **Low crouched stance with a wide leg base** — the unit reads as planted even while walking |
| Scale reference | Same canvas as `USRIFLE`; the tube adds roughly 9 px of horizontal silhouette on the east/west facings |
| Player-color zones | Helmet band and the launcher's rear grip housing. Not the tube body — the tube must stay a neutral olive so its shape stays readable on every terrain |
| Animation needs | Full contract, plus a **top-attack launch arc**: the missile leaves the tube, climbs visibly, then dives. This arc is the unit's signature and must be readable at normal zoom. Reload animation with the soldier lowering the tube and fitting a new round |
| Weapon / ability | Top-attack missile, heavy-armor specialisation, pronounced minimum range. **Networked:** inside coverage the missile gains lock speed and range. Outside coverage it is slow enough to dodge |
| Counterplay | Close the distance inside minimum range; infantry, flame, and artillery kill it trivially; active protection systems on the target should reduce its first shot |
| Tech tier | `~techlevel.medium`, prerequisite `dome`, `~tent`, `~infantry.usa` |
| Cost band | **Low**, 450 |

#### `USJTAC` — Joint Terminal Attack Controller

| Field | Contract |
| --- | --- |
| Role | Mobile network source, reconnaissance, cloak detection. The faction's most important and most fragile unit |
| Real-world basis | Joint terminal attack controller / forward observer directing joint fires |
| Status | **Fielded role**, long-standing. The supporting ground-station architecture it represents (TITAN) is at **advanced prototype**, not full fielding — see `USNODE` |
| Source | [TITAN system being developed to tie 'deep sensing' to long-range fires](https://www.army.mil/article/228867/titan_system_being_developed_to_tie_deep_sensing_to_long_range_fires); [1st Multi-Domain Task Force adopts TITAN](https://www.army.mil/article/282253/1st_multi_domain_task_force_mdtf_adopts_titan_a_game_changer_in_intelligence_and_targeting). Accessed 2026-08-16 |
| Visual landmarks | 1. **Tall thin whip antenna rising well above the helmet** — the single most identifiable element, and the one thing that must never be trimmed for canvas reasons. 2. **Flat slab tablet held chest-high in both hands** when deployed. 3. **Low-profile bump helmet with a headset boom**, visually lighter than `USRIFLE`'s armored helmet |
| Scale reference | Same canvas; the antenna occupies the top 6–7 px of the 39 px height and must not clip |
| Player-color zones | Shoulder panel and the tablet's back shell. Not the antenna |
| Animation needs | Full contract, plus a **deploy/undeploy pair** and a persistent **ground ring decoration** showing network coverage. A visible pulse on the ring when a covered guided weapon fires |
| Weapon / ability | Weak two-shot self-defence carbine. Primary function: projects the fires network, detects cloaked units, and has the longest ground sight range in the faction |
| Counterplay | Extremely soft. Any sniper, any mortar, any strafing run kills it. Existing jammers (`SANCAK`, `CNSPECTRUM`, `IRDC`) must be able to suppress its ring without killing it |
| Tech tier | `~techlevel.medium`, prerequisite `dome`, `~tent`, `~infantry.usa` |
| Cost band | **Medium**, 575 |

#### `TALONSIX` — Talon Six

| Field | Contract |
| --- | --- |
| Role | Faction commando, one per player, sabotage and precision elimination |
| Real-world basis | Generic U.S. special-operations operator. **Deliberately not** named after, badged as, or modelled on any real unit, and given a call-sign-style name to match `REDSPEAR`, `SHADOWONE`, `FALCON1`, `WADIGHOST`, and `GREYWOLF` |
| Status | **Original project character.** No capability claim is made about any real formation |
| Source | Not applicable — this is an authored game character, and this document explicitly declines to attribute real-unit capabilities to it |
| Visual landmarks | 1. **Low-profile bump helmet with a quad-tube night-vision assembly flipped down over the eyes** — a four-pronged front silhouette no other roster uses. 2. **Compact suppressed carbine held tight to the chest**, shorter than every other weapon in the catalog. 3. **Slim, low-bulk body with a thigh rig** — reads fast and light against `USRIFLE`'s blocky torso |
| Scale reference | Same canvas; visibly narrower shoulders than `USRIFLE` |
| Player-color zones | Helmet cover strip and thigh rig. Not the night-vision assembly, not the weapon |
| Animation needs | Full contract, plus **crouch-walk**, **suppressed double-tap** with a small dark muzzle bloom, **building-entry** and **charge-placement** sequences |
| Weapon / ability | Suppressed precision double-tap effective against infantry and light vehicles; structure demolition. **Networked:** inside coverage, gains a one-shot marked-target designation that lets `USHIMARS` or `USF35` strike a marked building at improved accuracy. Outside coverage it is a plain commando |
| Counterplay | Dogs and any cloak detector find it; it dies to sustained fire; build limit of one caps its impact |
| Tech tier | `~techlevel.high`, prerequisite `atek`, `~tent`, `~infantry.usa`, build limit 1 |
| Cost band | **High**, 1900 |

### 5.2 Vehicles

#### `USMBT` — Main Battle Tank

| Field | Contract |
| --- | --- |
| Role | Heavy armor anchor. Absorbs anti-tank fire so the rest of the roster survives |
| Real-world basis | **Recommended: M1A2 SEP v3, fielded configuration** — see §8.1 for the required product decision and the rejected alternative |
| Status | **Fielded.** The Army accepted the first M1A2 SEP v3 initial production vehicles in October 2017 and planned brigade-set fielding from FY2020. The M1E3 is a **prototype**, not inventory: General Dynamics Land Systems delivered a first prototype in late 2025, the Army unveiled an early prototype in January 2026, FY2026 funding covers up to four prototypes, and initial operational capability is anticipated in the early 2030s |
| Source | [Army rolls out latest version of iconic Abrams Main Battle Tank](https://www.army.mil/article/194952/army_rolls_out_latest_version_of_iconic_abrams_main_battle_tank); [Army announces plans for M1E3 Abrams tank modernization](https://www.army.mil/article/269706/army_announces_plans_for_m1e3_abrams_tank_modernization); [U.S. Army unveils early Abrams prototype at North American International Auto Show](https://www.army.mil/article/290052/us_army_unveils_early_abrams_prototype_at_north_american_international_auto_show); [The Army's M-1E3 Abrams Tank Modernization Program (CRS IF12495)](https://crsreports.congress.gov/product/pdf/IF/IF12495/2). Accessed 2026-08-16 |
| Visual landmarks | The hard problem is that the Saudi pack already ships `M1A2S`, an Abrams. The three landmarks below exist **specifically to separate the two at 32-facing scale**: 1. **A pair of squared active-protection launcher boxes standing proud of the turret cheeks**, present on all facings, absent on `M1A2S`. 2. **A slatted roof screen over the turret roof**, reading as a light hatched rectangle from above. 3. **A deep rear bustle rack packed with a distinct stowage lump**, breaking the rear outline |
| Scale reference | Hull footprint approximately 15% longer than `2TNK`; turret mass comparable to `3TNK` |
| Player-color zones | Turret side panel below the APS boxes, rear bustle rack frame, and a small hull-front chevron. Not the barrel, not the tracks, not the APS boxes |
| Animation needs | 32 hull facings, separately pivoted 32-facing turret with correct OpenRA handedness, main-gun recoil, muzzle bloom, and a distinct **APS intercept flash** near the incoming projectile — a small bright burst offset from the hull, not a hull-centred explosion |
| Weapon / ability | Main gun with a slow, heavy shot. **Active protection:** the first N incoming rocket or missile projectiles per engagement window are destroyed short of the hull, then the system needs to recharge. This is the tank's identity and is **not** network-dependent |
| Counterplay | Volley anti-armor fire to exhaust the APS window, then kill it; use guns and cannon, which APS never stops; artillery and air; the cost means the U.S. player fields fewer of them |
| Tech tier | `~techlevel.medium`, prerequisite `fix`, `~vehicles.usa` |
| Cost band | **High**, 1500 |
| Confidence | Configuration and fielding: **high**. Trophy-family active protection on Abrams: **medium** — official coverage confirms Army testing at Fort Bliss and an Army APS development effort, but this document does **not** assert a current fleet-wide fitment. See [1-1 CAV tests Trophy Active Protection System for tanks (DVIDS)](https://www.dvidshub.net/news/307665/1-1-cav-tests-trophy-active-protection-system-tanks) and [Army developing improved active protection systems for vehicle armor](https://www.army.mil/article/198005/army_developing_improved_active_protection_systems_for_vehicle_armor). Accessed 2026-08-16 |

#### `USIFV` — Infantry Fighting Vehicle

| Field | Contract |
| --- | --- |
| Role | Tracked infantry carrier with a real gun; the mid-game workhorse |
| Real-world basis | **Recommended: M2A4 Bradley, fielded configuration** — see §8.2 |
| Status | **Fielded.** The Army equipped its first unit with the modernized Bradley, and an A4 production contract covering 109 M2A4 and six M7A4 vehicles was awarded with deliveries beginning in early 2025. The **XM30** replacement is at prototype/design stage and its programme direction was under active reassessment as of early 2026 — it is **not** current inventory |
| Source | [U.S. Army equips first unit with modernized Bradley](https://www.army.mil/article/255980/us_army_equips_first_unit_with_modernized_bradley); [Army awards Bradley A4 production contract](https://www.army.mil/article/269660/army_awards_bradley_a4_production_contract); [The Army's XM-30 Mechanized Infantry Combat Vehicle (CRS IF12094)](https://crsreports.congress.gov/product/pdf/IF/IF12094/9). Accessed 2026-08-16 |
| Visual landmarks | 1. **Narrow, tall, slab-sided turret with a thin autocannon barrel and a boxy twin missile launcher folded on its right flank** — the folded launcher box is the read. 2. **Steeply sloped glacis with a squared-off driver's block offset to the left.** 3. **Tracked running gear with visible skirt panels** — after the §8.3 cut this is the faction's only tracked carrier, and the tracks are what separate it from `USSHORAD`, the roster's one remaining wheeled vehicle |
| Scale reference | Hull slightly shorter than `2TNK`, noticeably taller; turret much smaller than `USMBT`'s |
| Player-color zones | Turret side slab and hull skirt band |
| Animation needs | 32 hull facings, 32-facing turret, autocannon burst muzzle, a **launcher-box raise/lower** transition when switching to the missile, troop load/unload |
| Weapon / ability | Autocannon burst against infantry and light vehicles, plus a **short missile burst against armor that requires the launcher box to visibly raise first** — a real telegraph the opponent can react to. Carries infantry |
| Counterplay | Heavy tanks beat it; catch it mid-launcher-raise; it is far more fragile than `USMBT` and cannot absorb a tank line |
| Tech tier | `~techlevel.medium`, prerequisite `dome`, `~vehicles.usa` |
| Cost band | **Medium**, 1150 |

#### `USICV` — Wheeled Infantry Carrier

| Field | Contract |
| --- | --- |
| Role | Cheap, fast, early wheeled transport; the faction's only sub-`dome` vehicle |
| Real-world basis | Stryker family, including the 30 mm gun-armed Infantry Carrier Vehicle – Dragoon and the double-V hull design |
| Status | **Fielded.** The double-V hull was adopted from proven blast-deflecting design practice; the first 30 mm cannon prototype was delivered in 2016 and Dragoon fielding was planned from FY2018 |
| Source | [First Stryker vehicle prototype with 30 mm cannon delivered to Army](https://www.army.mil/article/177472/first_stryker_vehicle_prototype_with_30_mm_cannon_delivered_to_army); [Army's Stryker Double V-Hull is a resounding success](https://www.army.mil/article/92154/armys_stryker_double_v_hull_is_a_resounding_success). Accessed 2026-08-16 |
| Visual landmarks | 1. **Eight large road wheels in a long, flat, unbroken line** — instantly separates it from Turkey's `ARAS8` only if paired with landmark 2, so both are mandatory. 2. **Pronounced V-shaped hull underside visible as a deep dark wedge on the side facings.** 3. **Small remote weapon station perched well forward on the roof**, offset from the hull centreline |
| Scale reference | Hull length comparable to `ARAS8`; noticeably lower roofline |
| Player-color zones | Upper hull side band above the wheels, and the weapon station body |
| Animation needs | 32 hull facings, small 32-facing weapon-station turret, troop load/unload, dust trail on movement |
| Weapon / ability | Light autocannon. Fast on roads and open terrain, poor cross-country. Carries infantry. **No network interaction** |
| Counterplay | Any dedicated anti-armor weapon kills it; it is a transport with a gun, not a fighting vehicle |
| Tech tier | `~techlevel.low`, prerequisite `~vehicles.usa` |
| Cost band | **Medium**, 900 |
| Overlap warning | Turkey's `ARAS8` is already an eight-wheeled IFV. Both landmarks 1 and 2 above must ship, and the concept board must show the two side by side, or this actor should be cut. See §8.3 |

#### `USHIMARS` — Rocket Artillery System

| Field | Contract |
| --- | --- |
| Role | The faction's payoff unit. Long-range indirect fire that is dominant while networked and mediocre while not |
| Real-world basis | M142 HIMARS launcher; the Precision Strike Missile is the long-range munition family it fires |
| Status | **Fielded launcher.** PrSM Increment 1 has been delivered and demonstrated — including a first launch from an Australian HIMARS during Talisman Sabre 2025, described as capable of neutralizing targets at standoffs greater than 400 km. Increment 4, targeting ranges beyond 1,000 km, is **not fielded**: initial project awards were anticipated in late FY2026 |
| Source | [Army announces first Precision Strike Missiles delivery](https://www.army.mil/article/272301/army_announces_first_precision_strike_missiles_delivery); [Precision Strike Missile success at Talisman Sabre](https://www.army.mil/article/291029/precision_strike_missile_success_at_talisman_sabre_accelerating_army_long_range_precision_fires_modernization); [PAE Fires hosts Precision Strike Missile Increment 4 industry day](https://www.army.mil/article/291321/portfolio_acquisition_executive_for_fires_hosts_precision_strike_missile_increment_4_industry_day). Accessed 2026-08-16 |
| Visual landmarks | 1. **A single squared launch pod carried high on a short wheeled chassis** — one box, not the multi-tube bundle of `YMLR`, `CNPHL`, or `IRFAJR`. 2. **Six large wheels with an armored cab set well forward**, leaving a visible gap between cab and pod. 3. **The pod elevates to a steep angle before firing** and is the unit's silhouette change |
| Scale reference | Hull length comparable to `V2RL`; the raised pod roughly doubles its vertical silhouette |
| Player-color zones | Cab door panel and the pod's end cap frame. Not the pod faces — they carry the raise/lower read |
| Animation needs | 32 hull facings, **pod raise and lower** as separate reachable sequences, single-rocket launch with a heavy smoke plume, and a distinct **stow** state used while moving |
| Weapon / ability | Long-range single rocket, long reload, cannot fire while stowed. **Networked:** inside coverage it gains meaningfully improved accuracy and range against a spotted target. Outside coverage it is inaccurate enough that mobile targets escape it |
| Counterplay | Kill the spotters, not the launcher; it cannot fire on the move; it is nearly defenceless in melee; air and fast raiders punish it |
| Tech tier | `~techlevel.high`, prerequisite `atek`, `~vehicles.usa` |
| Cost band | **High**, 1400 |

#### `USSHORAD` — Mobile Air Defense Vehicle

| Field | Contract |
| --- | --- |
| Role | Mobile air defense that moves with the armored line; the mid layer of the faction's air answer |
| Real-world basis | SGT STOUT (formerly M-SHORAD), a Stryker-based system combining guns, missiles, and on-board sensors |
| Status | **Fielded.** Renamed from M-SHORAD in honour of a Medal of Honor recipient; battalions fielded in Germany, at Fort Sill, and at Fort Cavazos; a NATO live fire was conducted in Norway; FY2026 funding supports further procurement |
| Source | [Army renames air defense system after Vietnam War Medal of Honor recipient](https://www.army.mil/article/277091/army_renames_air_defense_system_after_vietnam_war_medal_of_honor_recipient); [M-SHORAD system bolsters Army's air defense capabilities](https://www.army.mil/article/245530/m_shorad_system_bolsters_armys_air_defense_capabilities); [First to fire: air defenders conduct first NATO live fire with SGT STOUT in Norway](https://www.army.mil/article/285707/first_to_fire_air_defenders_conduct_first_nato_live_fire_with_sgt_stout_in_norway). Accessed 2026-08-16 |
| Visual landmarks | 1. **A tall mission-equipment mast rising off the rear roof with a flat panel radar face** — the tallest element on any U.S. ground vehicle. 2. **Twin stubby missile pods flanking a short gun barrel** on a compact turret. 3. **Shares the eight-wheel `USICV` chassis, but with no troop doors and no rear ramp** — this is a *deliberate* shared family read; the mast separates it from `USICV`, and the sealed flanks separate it from Turkey's `ARAS8` |
| Scale reference | Same hull as `USICV`; the mast adds roughly 30% to vertical silhouette |
| Player-color zones | Hull side band (matching `USICV` so the family reads), and the turret cheeks |
| Animation needs | 32 hull facings, 32-facing turret, **mast raise/lower** tied to deploy, gun burst and missile launch as distinct armament sequences, and a rotating radar panel on idle |
| Weapon / ability | Two armaments with different jobs: a gun burst for drones and helicopters at short range, missiles for fast aircraft at longer range. **Shallow magazine with a visible reload** — it cannot hold a lane alone |
| Counterplay | Saturate it — the magazine is the point; kill it with ground fire, which it answers poorly; it is not a general-purpose vehicle |
| Tech tier | `~techlevel.medium`, prerequisite `dome`, `~vehicles.usa` |
| Cost band | **Medium**, 1250 |
| Overlap warning | Turkey's `GOKKALKAN`, Saudi's `SADS`, China's `CNMANTIS`, and Iran's `IRRAAD` are all mobile air defense vehicles, and all four are missile-only. `USSHORAD` is proposed as gun-and-missile mixed, which is a distinct engagement niche. The mast is the mandatory visual separator; the shallow-magazine-with-reload mechanic is the tactical one. See the §8.10 proposal |

#### `USRECOV` — Armored Recovery Vehicle

| Field | Contract |
| --- | --- |
| Role | Field repair and recovery. The unit that makes an expensive armored line economically viable |
| Real-world basis | M88-family Heavy Equipment Recovery Combat Utility Lift and Evacuation System |
| Status | **M88A2 fielded.** The **M88A3** is in test — reliability and maintainability testing at Yuma Proving Ground and performance testing at Aberdeen, with reported goals including a modernized powertrain, a seventh road wheel, hydro-pneumatic suspension, and an increase in towing capacity |
| Source | [Modernized M88 Recovery Vehicle variant aims to eliminate gaps](https://www.army.mil/article/275819/modernized_m88_recovery_vehicle_variant_aims_to_eliminate_gaps); [Support operations in an ABCT: maintenance and mobility with the Hercules and LET](https://www.army.mil/article/289388/support_operations_in_an_abct_maintenance_and_mobility_with_the_hercules_and_let). Accessed 2026-08-16 |
| Visual landmarks | 1. **A heavy A-frame boom folded flat along the hull top**, raising into a tall triangle when working — the single clearest read. 2. **A blunt turretless hull with a broad front dozer blade.** 3. **Thick spooled cable drum visible on the hull rear** |
| Scale reference | Hull mass comparable to `USMBT` but with no turret; visually the widest U.S. tracked vehicle |
| Player-color zones | Hull side panel and the boom's frame members |
| Animation needs | 32 hull facings, **boom raise/lower** and a working loop, blade lower/raise, and a repair-spark effect at the target |
| Weapon / ability | Unarmed. Repairs vehicles in the field over time; can drag a disabled friendly vehicle out of contact. **No network interaction** |
| Counterplay | It is unarmed. Kill it, and the U.S. armored line has to walk home to `fix` |
| Tech tier | `~techlevel.medium`, prerequisite `dome`, `~vehicles.usa` |
| Cost band | **Medium**, 1000 |

### 5.3 Aircraft

Roster note: the roadmap's candidate air set named an AH-64E. **It is excluded** by the
§8.4 ruling — Saudi's `AH64SA` is already literally named "AH-64E Apache", stock Allies
`HELI` is a Longbow Apache, and Turkey's `TURNAAH` is a third attack helicopter. A fourth
would fail the review rubric's silhouette-duplicate test. `USAC130` takes the slot, and
it now carries a real Air Force fact sheet rather than the placeholder index link this
document originally flagged as unacceptable.

#### `USF35` — Stealth Strike Fighter

| Field | Contract |
| --- | --- |
| Role | High-tier precision strike aircraft; expensive, few, decisive |
| Real-world basis | F-35A Lightning II |
| Status | **Fielded.** Described by the Air Force as its latest fifth-generation fighter — a stealthy, multirole, all-weather air-to-air and surface attack fighter replacing the F-16 and A-10 |
| Source | [F-35A Lightning II fact sheet, U.S. Air Force](https://www.af.mil/About-Us/Fact-Sheets/Display/Article/478441/f-35a-lightning-ii/). Accessed 2026-08-16 |
| Visual landmarks | 1. **A single broad chined nose blending into the wing root** — no separate fuselage tube, which no other aircraft in the catalog has. 2. **Two strongly canted tail fins forming a shallow V from above.** 3. **A clean underside with no external stores** in the transit state, gaining a small visible weapon only in the attack pass |
| Scale reference | Planform slightly smaller than `MIG`; visibly wider in chord |
| Player-color zones | A band across the tail fins and a thin spine stripe. The planform must otherwise stay dark so the silhouette reads |
| Animation needs | Per the plane sequence contract, plus a **weapons-bay-open** state during the attack run and a distinct high-altitude transit sprite |
| Weapon / ability | One standoff precision strike per sortie against a ground target, then return to `HPAD`. **Networked:** against a target inside coverage, the strike is accurate and lands on the first pass. Outside coverage it scatters |
| Counterplay | It is not invisible — it is *late-detected*. Air defense that already exists (`SAM`, `AGUN`, `CNSKYSHIELD`, `IRRAAD`, `GOKKALKAN`) must be able to kill it on approach. Kill the network and its strike becomes a coin flip |
| Tech tier | `~techlevel.high`, prerequisite `atek`, `~hpad`, `~aircraft.usa` |
| Cost band | **High**, 2400 |

#### `USMQ9` — Armed Reconnaissance UAS

| Field | Contract |
| --- | --- |
| Role | Persistent airborne network source plus light precision anti-armor. The cheap enabler that the opponent must hunt |
| Real-world basis | MQ-9-class medium-altitude long-endurance remotely piloted aircraft |
| Status | **Fielded.** Described by the Air Force as an armed, multi-mission, medium-altitude, long-endurance remotely piloted aircraft employed primarily against dynamic execution targets and secondarily as an intelligence collection asset |
| Source | [MQ-9 Reaper fact sheet, U.S. Air Force](https://www.af.mil/About-Us/Fact-Sheets/Display/Article/104470/). Accessed 2026-08-16 |
| Visual landmarks | 1. **Very long, very thin straight wing** — the highest aspect ratio in the catalog by a wide margin. 2. **Downward-canted V-tail with a pusher propeller behind it.** 3. **A bulbous chin sensor ball under a drooped nose** |
| Scale reference | Wingspan noticeably wider than `MIG`, fuselage far thinner. Must not be confused with Iran's `IRMOHAJER` or Turkey's `KUZGUNM` — see §7 |
| Player-color zones | Wingtip panels and the tail V faces |
| Animation needs | Per the plane contract, plus a slow **loiter orbit** distinct from the attack run, and a persistent ground ring decoration for network coverage matching `USJTAC`'s |
| Weapon / ability | Projects mobile network coverage while loitering; carries a small number of precision anti-armor missiles with a visible reload trip to `HPAD` |
| Counterplay | Slow and fragile. Every air defense in the game beats it. Killing it is the single cheapest way to degrade U.S. artillery |
| Tech tier | `~techlevel.medium`, prerequisite `dome`, `~hpad`, `~aircraft.usa` |
| Cost band | **Medium**, 1100 |

#### `USUH60` — Utility Helicopter

| Field | Contract |
| --- | --- |
| Role | Air assault transport and casualty/crew recovery. The faction's only rotary asset |
| Real-world basis | UH-60M Black Hawk |
| Status | **Fielded.** A twin-engine single-rotor helicopter designed to support the Army's air-mobility doctrine; UH-60M deliveries continue under the Utility Helicopters project office |
| Source | [PEO Aviation year in review](https://www.army.mil/article/215068/peo_aviation_year_in_review); [CPE Aviation](https://www.army.mil/cpe-avn). Accessed 2026-08-16 |
| Visual landmarks | 1. **A wide, flat, low cabin with big square door openings** — the doors are the read, and they must visibly open on unload. 2. **A canted tail rotor mounted high on a swept fin.** 3. **Fixed tricycle gear with a distinctly long tail wheel arm** |
| Scale reference | Rotor disc comparable to `TRAN`; cabin far shorter, single rotor rather than tandem |
| Player-color zones | Cabin door frames and the tail fin |
| Animation needs | Per the helicopter contract, plus **door open/close**, troop rappel or step-off unload, and a rotor-wash ground effect on landing |
| Weapon / ability | Light door-gun self-defence only. Carries infantry. Can retrieve a crew from a destroyed friendly vehicle husk, returning a fraction of its cost — the faction's answer to its own expense |
| Counterplay | Unarmoured and slow while loaded; any air defense kills it; catching it on the unload is a free trade |
| Tech tier | `~techlevel.medium`, prerequisite `dome`, `~hpad`, `~aircraft.usa` |
| Cost band | **Medium**, 1000 |

#### `USAC130` — Gunship

| Field | Contract |
| --- | --- |
| Role | Orbiting close air support. Mechanically unlike anything else in the catalog |
| Real-world basis | AC-130J Ghostrider, a heavily modified C-130J |
| Status | **Fielded.** The Air Force describes its primary missions as close air support, air interdiction, and armed reconnaissance, and the aircraft as an expeditionary, **persistent** direct-fire platform delivering precision low-yield munitions against ground targets. "Persistent direct fire" is precisely the orbit-and-sustain mechanic proposed below, which is why this actor survived review |
| Source | [AC-130J Ghostrider fact sheet, U.S. Air Force](https://www.af.mil/About-Us/Fact-Sheets/Display/Article/467756/ac-130j-ghostrider/); [AC-130J Ghostrider, Air Force Special Operations Command](https://www.afsoc.af.mil/About-Us/Fact-Sheets/Display/Article/2547234/ac-130j-ghostrider/). Accessed 2026-08-16. **Resolved:** the placeholder index link in the first draft has been replaced with two named fact sheets, per the §8.4 ruling |
| Visual landmarks | 1. **A high straight wing carrying four propellers** — the only propeller aircraft in any modern pack. 2. **A long fat fuselage with a tall square tail fin.** 3. **Visible gun ports along the left side only**, which makes the orbit direction readable |
| Scale reference | The largest aircraft sprite in the catalog; roughly 1.6x `MIG` in wingspan |
| Player-color zones | Tail fin and engine nacelle bands |
| Animation needs | Per the plane contract, plus a **sustained left-hand orbit** loop over a target point with repeated side-firing muzzle flashes, and a distinct arrival and departure transit |
| Weapon / ability | Orbits a commanded ground point for a fixed duration, firing continuously into it, then leaves. Devastating against static and slow targets, poor against anything that walks out of the circle. **No network interaction** — it is the faction's non-networked backup punch |
| Counterplay | It is huge, slow, and predictable. Every SAM in the game should kill it. Move out of the orbit circle. Its long approach is a visible warning |
| Tech tier | `~techlevel.high`, prerequisite `atek`, `~hpad`, `~aircraft.usa` |
| Cost band | **High**, 2600 |

### 5.4 Navy

#### `USDDG` — Guided Missile Destroyer

| Field | Contract |
| --- | --- |
| Role | Fleet backbone: area air defense plus land attack. The naval half of the faction's layered-defense identity |
| Real-world basis | Arleigh Burke-class destroyer, DDG 51, Flight III |
| Status | **Fielded class, Flight III in construction/delivery.** Flight III adds the SPY-6 air and missile defense radar, providing improved sensitivity for long-range detection and engagement; DDG 125 is the first ship built in the Flight III configuration, enabling simultaneous anti-air warfare and ballistic missile defense |
| Source | [Destroyers (DDG 51) fact file, U.S. Navy](https://www.navy.mil/Resources/Fact-Files/Display-FactFiles/Article/2169871/destroyers-ddg-51/); [Arleigh Burke-class destroyer Flight III progressing on schedule](https://www.navy.mil/Press-Office/Press-Releases/display-pressreleases/Article/2423252/arleigh-burke-class-destroyer-flight-iii-progressing-on-schedule/). Accessed 2026-08-16 |
| Visual landmarks | 1. **A blocky superstructure with four large flat radar panels set into its faces**, two visible from any given facing. 2. **Flat vertical-launch cell decks fore and aft**, reading as two hatched rectangles on the deck. 3. **A single squat funnel amidships and one clean gun mount on the bow** |
| Scale reference | Hull length between `DD` and `CA`; visually much boxier than either |
| Player-color zones | Superstructure side band and the funnel cap. Not the radar panel faces |
| Animation needs | Per the ship contract: turret traverse for the bow gun, vertical-launch cell doors opening with a distinct upward missile plume, wake, damage states, sink |
| Weapon / ability | Three jobs, all real: bow gun for surface targets, area anti-air interception with a finite magazine, and **networked** land-attack fire that only reaches inland targets while coverage exists |
| Counterplay | Submarines — it has no dedicated anti-submarine weapon in this contract, which is `USSSN`'s job to compensate for. Saturate the interception magazine. It is expensive enough that losing one hurts |
| Tech tier | `~techlevel.high`, prerequisite `atek`, `~syrd`, `~ships.usa` |
| Cost band | **High**, 2400 |

#### `USSSN` — Attack Submarine

| Field | Contract |
| --- | --- |
| Role | Submerged strike and area denial; escorts the fleet against enemy submarines |
| Real-world basis | Virginia-class attack submarine, SSN 774 |
| Status | **Fielded.** Navy fact file lists Tomahawk missiles with vertical launch tubes, Virginia Payload Tubes on later hulls, and Mk 48 advanced-capability torpedoes with four torpedo tubes; the class is replacing Los Angeles-class boats as they retire |
| Source | [Attack Submarines – SSN fact file, U.S. Navy](https://www.navy.mil/resources/fact-files/display-factfiles/article/2169558/attack-submarines-ssn/). Accessed 2026-08-16 |
| Visual landmarks | 1. **A smooth teardrop hull with no deck clutter at all** — the cleanest silhouette in the naval catalog. 2. **A short, thick, rounded sail set well forward** with no diving planes visible on the sail itself. 3. **A visible row of small circular payload hatches on the forward deck** when surfaced |
| Scale reference | Hull length roughly 1.4x `SS`; noticeably fatter |
| Player-color zones | Sail sides and a narrow hull deck stripe, visible only when surfaced |
| Animation needs | Per the submarine contract: submerge and surface transitions, periscope wake while submerged, torpedo launch, a **separate vertical missile launch** breaching the surface, damage, sink |
| Weapon / ability | Torpedoes against ships and submarines; a small number of **networked** land-attack missiles that require surfacing to fire — a real, visible commitment |
| Counterplay | Any sonar or depth-charge capability finds it; it is helpless once detected and surfaced; its land attack telegraphs its position |
| Tech tier | `~techlevel.high`, prerequisite `atek`, `~syrd`, `~ships.usa` |
| Cost band | **High**, 1900 |

#### `USLPD` — Amphibious Transport Dock

| Field | Contract |
| --- | --- |
| Role | Naval logistics and amphibious landing; the faction's force-projection hull |
| Real-world basis | San Antonio-class amphibious transport dock, LPD 17 |
| Status | **Fielded.** Navy fact file describes amphibious transport docks as warships that embark, transport, and land elements of a landing force for expeditionary warfare missions |
| Source | [Amphibious Transport Dock – LPD fact file, U.S. Navy](https://www.navy.mil/Resources/Fact-Files/Display-FactFiles/Article/2222713/amphibious-transport-dock-lpd/). Accessed 2026-08-16 |
| Visual landmarks | 1. **A tall enclosed faceted mast structure with sharply angled flat faces** — the most distinctive superstructure in the naval catalog. 2. **A long flat helicopter deck occupying the entire stern.** 3. **A stern ramp that visibly lowers into the water on unload** |
| Scale reference | The largest U.S. naval sprite; hull comparable to `CA` in length, much taller |
| Player-color zones | Hull side band along the waterline and the mast faces |
| Animation needs | Per the ship contract, plus **stern ramp lower/raise**, a deck landing spot that `USUH60` can visibly use, and a repair-in-progress effect |
| Weapon / ability | Very light self-defence gun only. Carries vehicles and infantry across water; **repairs and rearms friendly ships and aircraft in a radius**, which is what makes it worth its cost |
| Counterplay | It is a fat, slow, near-unarmed target. Any warship or submarine kills it. Sinking it strands a landing force |
| Tech tier | `~techlevel.medium`, prerequisite `dome`, `~syrd`, `~ships.usa` |
| Cost band | **High**, 1600 |

### 5.5 Buildings and defenses

#### `USTOC` — Tactical Operations Center

| Field | Contract |
| --- | --- |
| Role | The faction's identity building. Projects base-wide fires network coverage. Killing it is the primary way to beat this faction |
| Real-world basis | Brigade tactical operations center hosting an intelligence ground station of the TITAN type |
| Status | **Prototype/limited fielding for the underlying ground station.** TITAN prototypes have been delivered — a first ground-station prototype at Joint Base Lewis-McChord, and the 1st Multi-Domain Task Force was issued a TITAN Advanced Prototype. This contract therefore presents the building as a **command post**, not as a fielded programme of record |
| Source | [Army's first TITAN ground station prototype delivered at JBLM](https://www.army.mil/article/278482/armys_first_titan_ground_station_prototype_delivered_at_jblm); [Army TITAN ground station prototype award](https://www.army.mil/article/274301/army_tactical_intelligence_targeting_access_node_titan_ground_station_prototype_award); [1st MDTF adopts TITAN](https://www.army.mil/article/282253/1st_multi_domain_task_force_mdtf_adopts_titan_a_game_changer_in_intelligence_and_targeting). Accessed 2026-08-16 |
| Visual landmarks | 1. **Two boxy shelter containers set side by side with a covered walkway between them.** 2. **A large mesh dish on a short mast**, slowly rotating on idle. 3. **A cable run and a generator skid on the ground beside the shelters** |
| Scale reference | Footprint comparable to `DOME`; visually lower and wider |
| Player-color zones | Shelter container end walls and the dish mount |
| Animation needs | Make (construction), idle with rotating dish, an **active** state with a visible antenna glow while coverage is live, a **suppressed** state when jammed, damaged, and destroyed |
| Weapon / ability | Unarmed. Projects the largest network coverage radius in the faction. When jammed by an enemy EW actor, the coverage ring visibly collapses instead of silently switching off |
| Counterplay | Undefended, it is a soft building. Commandos, air, and artillery all reach it. Jammers neutralise it without destroying it, which is a cheaper answer |
| Tech tier | `~techlevel.low`, prerequisite `~structures.usa` |
| Cost band | **Medium**, 1200 |

#### `USIAMD` — Integrated Air and Missile Defense Battery

| Field | Contract |
| --- | --- |
| Role | High-tier static air and missile defense; the top layer of the faction's air answer |
| Real-world basis | Patriot-family battery paired with a next-generation lower-tier sensor |
| Status | **Patriot fielded; LTAMDS in operational assessment, not full-rate production.** The manufacturer describes the sensor as a radar designed to defeat advanced and next-generation threats including hypersonic weapons, built around three antenna arrays — a primary array on the front and two secondary arrays on the back — working together to detect and engage threats from any direction simultaneously, and states that it preserves existing customers' investment in the Patriot system. A 360-degree engagement capability was demonstrated in an August 2025 flight test at White Sands as part of the operational assessment required before full-rate production. **Correction recorded:** an earlier draft of this entry stated that LTAMDS is "intended to replace the current Patriot radar." The manufacturer page does not say that, and the claim has been removed |
| Source | [Army successfully demonstrates LTAMDS 360-degree capability](https://www.army.mil/article/287888/army_successfully_demonstrates_ltamds_360_degree_capability); [LTAMDS, Raytheon/RTX](https://www.rtx.com/raytheon/what-we-do/integrated-air-and-missile-defense/ltamds). Accessed 2026-08-16 |
| Visual landmarks | 1. **A large flat panel array tilted back at a steep angle**, dominating the footprint. 2. **A separate four-canister launcher box that elevates to near-vertical to fire.** 3. **A low power unit with a visible exhaust stack** completing the three-piece emplacement |
| Scale reference | Footprint comparable to `SAM` plus one cell; visually far taller |
| Player-color zones | Array frame edge and the launcher box sides. Not the array face |
| Animation needs | Make, idle with a slow array sweep, **launcher elevate/fire/lower**, an **active radiating** state, damaged, destroyed |
| Weapon / ability | Long-range anti-air with a **finite magazine and a long, visible reload**. **Networked:** while covered, it also intercepts a limited number of incoming enemy artillery or missile projectiles crossing its radius |
| Counterplay | Saturation is the answer, and the magazine and reload exist precisely to make saturation work; ground assault beats it outright; jamming removes the projectile-interception half |
| Tech tier | `~techlevel.high`, prerequisite `atek`, `~structures.usa` |
| Cost band | **High**, 1400 |
| Naming note | This contract uses the neutral id `USIAMD` and a generic display name rather than a trademark. The real-world basis is documented here; the shipped presentation must not depend on any brand mark. See §8.5 |

#### `USCUAS` — Counter-UAS Emplacement

| Field | Contract |
| --- | --- |
| Role | Cheap, narrow, dedicated anti-drone defense. Answers the four shipped drone factions without being general-purpose air defense |
| Real-world basis | Fixed-site low, slow, small-UAS integrated defeat system with a radar-guided interceptor |
| Status | **Fielded / rapid acquisition.** The interceptor is described as a ground-launched, radar-guided interceptor with kinetic and non-kinetic variants, integrated into fixed-site and mobile small-UAS integrated defeat systems; a rapid acquisition authority contract was announced for the interceptors |
| Source | [Army announces rapid acquisition authority contract for Coyote interceptors](https://www.army.mil/article/273625/army_announces_rapid_acquisition_authority_contract_for_coyote_interceptors); [Joint Counter-Small UAS Office conducts successful counter drone-swarm demonstration](https://www.army.mil/article/278404/joint_counter_small_uas_office_conducts_successful_counter_drone_swarm_demonstration). Accessed 2026-08-16 |
| Visual landmarks | 1. **A small squared radar face on a short post**, deliberately much smaller than `USIAMD`'s array. 2. **A cluster of thin tube launchers pointed steeply upward** in a tight bundle. 3. **A sandbagged or gabion base ring**, which is the cheap-emplacement read |
| Scale reference | Single-cell footprint, comparable to `AGUN` |
| Player-color zones | Post collar and the launcher bundle frame |
| Animation needs | Make, idle with a small rotating radar face, rapid multi-tube launch, damaged, destroyed |
| Weapon / ability | **Can only target unmanned aircraft and loitering munitions.** This restriction is the design: it must never engage `MIG`, `HIND`, `USF35`, or any crewed aircraft. Fast reload, short range |
| Counterplay | Ignore it with crewed aircraft; overrun it with ground forces; it is worthless against anything but drones |
| Tech tier | `~techlevel.medium`, prerequisite `dome`, `~structures.usa` |
| Cost band | **Medium**, 700 |

#### `USNODE` — Networked Sensor Node

| Field | Contract |
| --- | --- |
| Role | Cheap forward extension of the fires network. The thing the U.S. player creeps toward the enemy and the opponent snipes |
| Real-world basis | Forward-deployed element of an intelligence ground station / sensor relay |
| Status | **Prototype**, same evidence basis and same caution as `USTOC` |
| Source | [TITAN pre-prototype illuminates the way forward for the U.S. Army's 'King of Battle'](https://www.army.mil/article/274785/titan_pre_prototype_illuminates_the_way_forward_for_the_us_armys_king_of_battle). Accessed 2026-08-16 |
| Visual landmarks | 1. **A single lattice mast, taller than it is wide**, with guy wires. 2. **A small drum-shaped sensor head at the top.** 3. **A tiny ground shelter box at the base**, which is all the footprint it has |
| Scale reference | Single-cell footprint; tallest thin silhouette in the faction |
| Player-color zones | Ground shelter box and a band near the mast top |
| Animation needs | Make, idle with a slow sensor-head rotation, **active** state with a coverage ring, **suppressed** state under jamming, damaged, destroyed |
| Weapon / ability | Unarmed. Projects medium-radius network coverage and detects cloaked units inside it |
| Counterplay | It is a stick with a box on it. Anything kills it. Jamming suppresses it for free |
| Tech tier | `~techlevel.medium`, prerequisite `dome`, `~structures.usa` |
| Cost band | **Medium**, 600 |

---

## 6. Non-duplication justification

Requirement 6 of the assignment: every unit that is not merely another tank, rifleman,
missile launcher, or renamed stock weapon must be justified.

| Actor | Why it is not a reskin |
| --- | --- |
| `USRIFLE` | It *is* a plain rifleman, and that is deliberate — the faction needs one unremarkable line unit so the network mechanic has a contrast case. Its only claim is a distinct three-round burst read. **Weakest actor in the roster by design.** |
| `USJAV` | The visible climb-then-dive top-attack arc is a projectile behaviour nothing in the catalog has; Iran's `IRATGM` tracks flat and slows its target, Yemen's `YRPG` is short-range, Turkey's `TRAT` is a concealed ambusher, China's `CNPORTABLE` toggles AT/AA |
| `USJTAC` | Overlaps Saudi's `SAJTAC` conceptually. The separator: `SAJTAC` marks one target to amplify Red Sea guided strikes; `USJTAC` projects an **area** condition consumed by a defined weapon list. Different shape entirely — point versus area. Still flagged in §7 |
| `TALONSIX` | Quad-tube night-vision silhouette is unused; the marked-target-into-artillery combination is a two-unit interaction, not a stat |
| `USMBT` | Active protection with a finite intercept window is a defensive mechanic no shipped tank has. Visual duplication with `M1A2S` is a real risk and is escalated in §8.1 |
| `USIFV` | The raise-the-launcher-box telegraph before anti-armor fire is a readable commitment window; nothing else in the catalog makes the player choose between two armaments with a visible delay |
| `USICV` | The thinnest justification in the roster, and this is stated plainly rather than argued away. Its job is "cheap early wheels" and Turkey's `ARAS8` already occupies that visual space. What earns its place is structural rather than tactical: it gives `USSHORAD` a deliberate family hull, and it is the faction's only pre-`dome` vehicle. §8.3 kept it on those grounds, with a concept-board gate |
| `USHIMARS` | Single-pod launcher versus every other multi-tube rocket vehicle; and it is the only artillery in the game whose accuracy depends on a separate unit staying alive |
| `USSHORAD` | Shallow magazine plus visible reload makes it beatable by saturation, unlike the shipped air-defense vehicles which sustain fire. The mast silhouette is the visual claim |
| `USRECOV` | The catalog has no armored recovery vehicle. Field repair plus dragging a disabled vehicle out of contact is a new verb |
| `USF35` | Late-detection rather than invisibility, and a strike whose accuracy is gated on the network. No other aircraft has an accuracy that another unit controls |
| `USMQ9` | A *mobile* network source that also shoots. Iran's `IRMOHAJER` scouts, `IRLOITER` is expendable ordnance, Turkey's `KUZGUNM` is a strike drone, China's `CNCLOUD` is a UAV — none project a persistent condition |
| `USUH60` | Crew recovery from friendly husks is a unique economic verb and directly answers the faction's own expense problem |
| `USAC130` | The sustained left-hand orbit is a movement and attack pattern nothing else in OpenRA uses. It is the roster's clearest mechanical novelty, and the Air Force's own description of a *persistent* direct-fire platform is what the mechanic models |
| `USDDG` | The only ship that does area anti-air, surface gunnery, and network-gated land attack from one hull. Saudi's `SA_FRGT` is the nearest neighbour — see §7 |
| `USSSN` | Surfacing to fire land-attack missiles is a visible commitment that trades its own concealment. Iran's `IRGHADIR` and China's `CNJIAOLONG` are pure torpedo boats |
| `USLPD` | Repair-and-rearm radius for ships and aircraft is a naval support role only Saudi's `SA_FSS` shares — see §7 |
| `USTOC` | A building whose destruction removes a faction-wide mechanic. Nothing in the catalog has that dependency shape |
| `USIAMD` | Anti-air plus limited projectile interception in one emplacement, with a magazine and a reload that make saturation the intended answer |
| `USCUAS` | A defense that **can only** shoot drones. A deliberate anti-generalist |
| `USNODE` | A defense with no weapon. Its only output is information |

---

## 7. Overlap matrix

Legend: **Clear** = no meaningful conflict. **Watch** = conceptual neighbour, ships only
with the stated separator. No actor is removed on the strength of any row here.

### 7.1 Against stock Allies and Soviets

| U.S. actor | Nearest stock actor | Verdict | Separator |
| --- | --- | --- | --- |
| `USRIFLE` | `E1` rifle infantry | Watch | Burst read, heavier kit, no rocket capability |
| `USJAV` | `E3` rocket soldier | Watch | Top-attack arc, minimum range, network dependency |
| `USJTAC` | `SPY` / `MGG` | Clear | Not disguise, not gap generation; it is an area condition |
| `TALONSIX` | `E7` Tanya | Watch | Both are build-limit commandos; `TALONSIX` has no anti-vehicle burst and gains a designation instead |
| `USMBT` | `3TNK` heavy tank | Clear | Active protection versus raw HP; different silhouette family |
| `USIFV` | `APC` | Clear | `APC` has no turret and no anti-armor option |
| `USICV` | `APC` | Watch | Wheels versus tracks; gun versus none |
| `USHIMARS` | `ARTY` / `V2RL` | Watch | Single pod, network-gated accuracy, cannot fire while stowed |
| `USSHORAD` | `FTRK` | Watch | Two armaments, mast, magazine and reload |
| `USRECOV` | none | Clear | No stock recovery vehicle exists |
| `USF35` | `MIG` / `YAK` | Clear | Different planform class and a ground-strike-only role |
| `USMQ9` | `U2` spy plane | Watch | `U2` is a one-shot reveal; `USMQ9` loiters and shoots |
| `USUH60` | `TRAN` Chinook, `HELI` Longbow | Watch | Single rotor short cabin versus tandem rotor; no attack role |
| `USAC130` | `BADR.Bomber` | Clear | Orbit-and-sustain versus straight-line bomb run |
| `USDDG` | `DD`, `CA` | Watch | Radar-panel superstructure and VLS decks; area anti-air is new |
| `USSSN` | `SS`, `MSUB` | Watch | Larger clean hull; surfaced land attack |
| `USLPD` | `LST` | Watch | `LST` is pure transport; `USLPD` repairs and rearms |
| `USTOC` | `DOME` | Watch | `DOME` reveals map; `USTOC` grants a weapon-restricted condition |
| `USIAMD` | `SAM`, `AGUN` | Watch | Magazine, reload, projectile interception |
| `USCUAS` | `AGUN` | Clear | Drone-only targeting restriction |
| `USNODE` | `GAP`, `SONAR` | Clear | Unarmed area condition, not shroud manipulation |

### 7.2 Against the China, Iran, Saudi, Yemen, and Turkey packs

| U.S. actor | Existing neighbour(s) | Verdict | Required separator |
| --- | --- | --- | --- |
| `USRIFLE` | `CNRIFLE`, `IRBAS`, `TRRIFLE`, `SANG`, `YMR` | Watch | Five riflemen already ship. `USRIFLE` must be the only one with a light armor-piercing bias and must not gain any aura, deploy, or suppression effect |
| `USJAV` | `IRATGM`, `TRAT`, `SAAT`, `YRPG`, `CNPORTABLE` | Watch | Top-attack **arc** is the visual claim; all five existing teams fire flat |
| `USJTAC` | `SAJTAC`, `YSPOT`, `TRDRONEOP`, `IRDC`, `CNNETWORK` | Watch | Resolved by §8.6: kept, because it is the only one projecting an **area** condition consumed by a restricted weapon list. The other five mark a point target |
| `TALONSIX` | `REDSPEAR`, `SHADOWONE`, `FALCON1`, `WADIGHOST`, `GREYWOLF` | Watch | Quad-tube NVG silhouette; no cloak (unlike `SHADOWONE`, `WADIGHOST`), no aura (unlike `REDSPEAR`, `GREYWOLF`), no airstrike power (unlike `FALCON1`) |
| `USMBT` | `M1A2S`, `BOZKIR`, `CNQILIN`, `IRKARR` | Watch | Resolved by §8.1: fielded M1A2 SEP v3 retained, with the three visual deltas in §5.2 mandatory and a side-by-side concept-board check against `M1A2S` as an art-workstream gate |
| `USIFV` | `CNZBD`, `DENIZKAPLAN` | Clear | Both existing are amphibious IFVs with different hull reads; `USIFV` is not amphibious |
| `USICV` | `ARAS8` | Watch | Both are eight-wheeled carriers. **Decided by §8.3: kept**, with the V-hull wedge and forward-offset weapon station mandatory and a concept-board comparison against `ARAS8` gating frame generation |
| `USHIMARS` | `CNPHL`, `YMLR`, `IRFAJR` | Watch | Single pod versus multi-tube bundle; stow state; network gating |
| `USSHORAD` | `GOKKALKAN`, `SADS`, `CNMANTIS`, `IRRAAD` | Watch | Mast silhouette plus magazine-and-reload mechanic. Four mobile AA vehicles already ship, all in the missile-only niche; §8.10 closes that niche and admits `USSHORAD` in the gun-and-missile one |
| `USRECOV` | none | Clear | Genuinely new |
| `USF35` | `SAHINX`, `F15SA`, `CNSKYSPEAR`, `IRAZAR`, `KUZGUNM` | Watch | Blended chined planform and canted V tails; ground-strike only, no air-to-air role |
| `USMQ9` | `IRMOHAJER`, `IRLOITER`, `CNCLOUD`, `KUZGUNM`, `SAMAD` | Watch | Extreme aspect ratio wing plus V-tail and pusher prop; persistent area condition |
| `USUH60` | `AH64SA`, `TURNAAH`, `IRTOUFAN`, `CNCRANE` | Clear | The only rotary transport among five rotary actors; unarmed but for door guns |
| `USAC130` | none | Clear | No propeller aircraft and no orbiting attacker exists |
| `USDDG` | `SA_FRGT`, `MARMARA`, `CNLUYANG` | Watch | `SA_FRGT` already does radar-plus-interceptor-magazine. `USDDG` must add surface gunnery and land attack, and its interception must be area rather than self-defence |
| `USSSN` | `IRGHADIR`, `CNJIAOLONG` | Clear | Larger hull, surfaced land attack |
| `USLPD` | `SA_FSS`, `CNKUNLUN` | Watch | Resolved by §8.7: keeps both the transport and the repair-and-rearm role, priced in the high band and given effectively no weapon, so it is a fat escort-dependent target rather than a free advantage |
| `USTOC` | `CNSPECTRUM` | Watch | `CNSPECTRUM` is a control node defense; `USTOC` is a production-adjacent building granting a condition, and must be jammable *by* `CNSPECTRUM` |
| `USIAMD` | `CNSKYSHIELD`, `SAM` | Watch | Magazine, reload, and projectile interception; `CNSKYSHIELD` is sustained-fire |
| `USCUAS` | `CNBASTION`, `AGUN` | Clear | Drone-only restriction |
| `USNODE` | `CNSPECTRUM`, `SONAR` | Watch | Unarmed; grants coverage rather than disruption |

---

## 8. Decisions

Split into two kinds, and the split is the important part:

**Applied** — configuration and rules choices that add or constrain, but remove nothing:
§8.1, §8.2, §8.5, §8.6, §8.7, §8.8, §8.11. These are settled and workable.

**Proposals awaiting product-owner approval** — anything that would remove an actor from
a roster: §8.3, §8.4, §8.9, §8.10. **Nothing is removed.** Every actor remains in §3 and
§5 with its full contract intact. These sections record a recommendation and its
reasoning so the decision can be made quickly, not so it can be made silently.

An earlier revision of this document applied the removal proposals directly. That was
wrong: cutting proposed content is a product call, not a delivery-agent call, and the
distinction matters more once art exists. The removals have been reverted and every cut
actor restored with its original text.

### 8.1 The Abrams collision — **RULED: keep the fielded M1A2 SEP v3 (Option A)**

Saudi's `M1A2S` is already an Abrams. Three options were on the table: ship the fielded
SEP v3 with mandatory visual deltas, ship the M1E3 concept hull with a near-future
label, or drop the U.S. tank entirely.

**Decision: Option A.** Every shipped pack is present-day — Karrar, Bozkir, Qilin,
M1A2S. Putting a prototype hull in the flagship U.S. slot would break that consistency
and would make the accuracy gate *harder*, not easier, because the roster would then mix
fielded and prototype equipment with no principled line between them. Option C was never
serious: a U.S. pack without a tank fails its own doctrine.

Consequences that are now binding:

- The three visual deltas in §5.2 — APS launcher boxes on the turret cheeks, the slatted
  roof screen, the deep rear bustle rack — are **mandatory, not preferred**.
- A side-by-side concept-board comparison of `USMBT` and `M1A2S` at native scale is an
  **art-workstream gate** before any frame sheet is generated. This is a gate, not a
  product decision, so it does not block freezing this contract.
- **Honesty constraint retained:** the pack *depicts* the tank with active protection
  fitted. Official sources confirm the Army pursued, developed, and tested APS on the
  Abrams; they do not establish fleet-wide fitment, and this contract makes no such
  claim. See the §9.2 confidence row, which is deliberately left at low-medium.

Cost of this ruling: two Abrams silhouettes exist in the catalog and the art workstream
carries the burden of separating them. That is a real cost and it is accepted knowingly.

### 8.2 The Bradley/XM30 choice — **RULED: fielded M2A4 Bradley**

Unchanged from the original recommendation, and now consistent with §8.1. The XM30 is at
prototype/design stage with its programme direction under reassessment; presenting it as
current inventory would fail the accuracy gate.

### 8.3 `USICV` versus Turkey's `ARAS8` — **DECIDED by product owner: keep**

`USICV` stays in the roster. The eight-wheeled overlap with Turkey's shipped `ARAS8` is
answered with **art direction, not removal**.

Binding consequences:

- The two landmarks in §5.2 are **mandatory, not preferred**: the pronounced V-shaped
  hull underside reading as a deep dark wedge on the side facings, and the small remote
  weapon station perched well forward and offset from the hull centreline.
- **Concept-board gate:** `USICV` must be shown beside `ARAS8` at native scale, on the
  snow, temperate, and desert palettes, and judged legible without labels *before* any
  frame sheet is generated. This is an art-workstream gate under roadmap gate 2. It does
  not block freezing this contract.
- If the concept board fails, the answer is to revise the art, not to silently drop the
  actor. Any later proposal to remove it comes back here as a fresh decision.

Two things this preserves that an earlier revision had undersold:

- `USSHORAD` keeps its shared eight-wheel family hull, so the mast is a *differentiator*
  within a deliberate family rather than the sole thing carrying the silhouette.
- The faction keeps a purpose-built vehicle before `dome`. Stock `APC` is
  `~vehicles.soviet` only (`mods/ra/rules/vehicles.yaml:468`), so an Allies-side faction
  has no fallback carrier — without `USICV` the early game would have been thinner than
  §1.4 bargained for.

Implementation note that holds regardless: the U.S. rules should not exclude stock `JEEP`
the way `mods/ra/rules/turkey.yaml:966` excludes it for Turkey.

### 8.4 The Apache and `USAC130` — **applied in part; Apache defaulted to "no", reversible**

**Applied:** `USAC130`'s citation is fixed. The first draft cited only a fact-sheet index
page, which this document itself called unacceptable. It now carries two named Air Force
fact sheets (§5.3), and the official description of a **persistent** direct-fire platform
is what the orbit-and-sustain mechanic models.

**Defaulted, not ruled:** no U.S. AH-64E. The product owner expressed no preference, so
this defaults to the non-additive option — aircraft stay at four (`USF35`, `USMQ9`,
`USUH60`, `USAC130`), which is the roadmap's preferred upper count.

Why "no" is the safe default here rather than "yes": this is an **add** decision and
nothing exists yet, so choosing not to add costs nothing and removes nothing. Adding the
actor later is a text edit; adding its *art* is the expensive, hard-to-reverse part. The
default therefore avoids committing art budget to a silhouette the catalog already holds
three times.

**To reverse:** say so, and the roster takes a fifth aircraft. The honest way to make it
distinct is manned-unmanned teaming — a U.S. Apache that launches and controls a small
air-launched effect, a real capability that none of stock `HELI`, Saudi's `AH64SA`, or
Turkey's `TURNAAH` has. It would still share the Apache shape, which is the cost.

### 8.5 Naming and trademark posture — **RULED**

**Generic display names for buildings and defenses; real designations for vehicles,
aircraft, and ships.** This matches the majority of shipped practice (`M1A2S`, `F15SA`,
`AH64SA` use real designations; `CNBASTION`, `SADS`, `CNSKYSHIELD` use generic ones) and
keeps the trademark surface on the defensive systems, where the roadmap's abstraction
requirement is strongest. Real-world basis stays documented in this contract regardless.

This ruling binds the Israel, Bundeswehr, and Korea contracts.

### 8.6 The sixth observer infantry — **RULED: keep `USJTAC`**

**Decision: keep.** The fires network is the faction's entire identity and it needs a
mobile source that the opponent can kill. Cutting `USJTAC` would have pushed the whole
mobile-network role onto `USMQ9`, which is an aircraft — meaning a player with no air
defense could never contest it, and a player with any air defense could shut it off
completely. A ground source is what makes the mechanic contestable at an even rate.

Binding constraint: `USJTAC` must remain the **only** one of the six projecting an area
condition consumed by a restricted weapon list. The other five mark a point target. If
implementation drifts toward point-marking, the actor loses its justification.

Cost accepted: the observer-infantry visual space is now genuinely crowded, and
§5.1's landmark list for `USJTAC` — especially the tall whip antenna — is doing more
work than any other infantry silhouette in the roster.

### 8.7 `USLPD` doing two jobs — **RULED: keep both roles**

**Decision: keep transport and repair-and-rearm in one hull**, priced in the high band
with effectively no weapon. The alternative — a pure transport — would have left the
three-ship navy with no support role at all, and the roadmap explicitly wants naval
actors to have distinct jobs.

Binding constraint: it must be a fat, escort-dependent target. If playtesting shows it
surviving unescorted, the answer is to reduce its survivability, not to remove a role.

### 8.8 Network mechanic scope — **RESOLVED: feasible with existing traits, no engine work**

This was the only item that was a question of fact rather than taste, and it has been
verified against the engine and the shipped mod data rather than assumed. All four
sub-requirements are already proven in shipping content:

| Requirement | Mechanism | Proven by |
| --- | --- | --- |
| (a) Area condition from multiple source types, non-stacking | `ProximityExternalCondition` grants a named condition to actors in `Range` with `ValidRelationships`; consumers declare `ExternalCondition` and gate on `RequiresCondition: <name>`, which is boolean — a second grant of the same name does not double the effect | `REDSPEAR` grants `china-command-network` (`mods/ra/rules/china.yaml:385`); `^ChinaNetworked` consumes it (`mods/ra/rules/china.yaml:69`) |
| (b) Effect restricted to a named weapon list | The multipliers (`FirepowerMultiplier`, `ReloadDelayMultiplier`, `RangeMultiplier`, `InaccuracyMultiplier`) attach only to opted-in actors via an inherited template, and `ArmamentInfo` derives from `PausableConditionalTraitInfo`, so an individual armament can carry its own `RequiresCondition` | `^ChinaNetworked` is exactly this template pattern; `OpenRA.Mods.Common/Traits/Armament.cs:29` |
| (c) Enemy EW suppresses without destroying the source | A second `ProximityExternalCondition` with `ValidRelationships: Enemy, Neutral` granting a `jammed` condition; U.S. multipliers gate on `usa-fires-network && !jammed` | `SANCAK` grants `jammed` (`mods/ra/rules/turkey.yaml:430`); stock `MRJ` does the same (`mods/ra/rules/vehicles.yaml:642`) |
| (d) Coverage visible to both players | `WithRangeCircle` exposes `ValidRelationships` and `Visible`, where `RangeCircleVisibility` is `{ Always, WhenSelected }`. Setting `ValidRelationships: Ally, Neutral, Enemy` and `Visible: Always` shows the ring to the opponent | `OpenRA.Mods.Common/Traits/Render/WithRangeCircle.cs:20,45,48`; existing use at `mods/ra/rules/china.yaml:391` and `mods/ra/rules/red-sea.yaml:523` |

**Conclusion: the fires network requires no new engine traits.** It is a rules-only
mechanic built from four traits that already ship and are already combined this way by
China, Turkey, and the Red Sea pack. The implementation shape is a `^UsaNetworked`
inherited template mirroring `^ChinaNetworked`.

One consequence worth naming: because the existing jammers already grant a generic
`jammed` condition, Turkey's `SANCAK`, China's `CNSPECTRUM`, and Iran's `IRDC` will
counter the U.S. network **without any edit to their own faction files**, provided the
U.S. multipliers gate on `!jammed`. That satisfies the §1.4 tradeoff at zero cost to the
parallel-ownership boundary.

### 8.9 Programme rule: shared airframes — **DEFAULTED to grandfather + role-only. Nothing is removed.**

The product owner expressed no preference among the four options, so this defaults to the
one that deletes nothing.

**Rule in force:**

> An airframe family may appear any number of times across the catalog, provided each
> appearance occupies a **distinct tactical role** and ships its **own sprite set**.
> Palette-only variants remain forbidden by the roadmap.
>
> **The rule never applies to art that already exists or is in progress.** Anything
> already drawn is grandfathered permanently and is out of scope for this or any future
> version of this rule.

Effect on every roster: **none are cut.** All of these keep their slots —

| Actor | Document | Required distinct role |
| --- | --- | --- |
| `ILAH64` | Israel | Must differ in role from stock `HELI`, Saudi `AH64SA`, Turkey `TURNAAH` |
| `KRAH64` | South Korea | Same, and from `ILAH64` |
| `ILF35` | Israel | Strike and escort, versus `USF35`'s ground-strike-only |
| `KRF35` | South Korea | A third distinct role — air superiority is the natural fit |
| `BWTRAN` | Germany | Must differ in role from stock `TRAN`, not just in markings |
| `KRTRAN` | South Korea | Same |

The cost is art: every one of these needs its own sprite set rather than a shared sheet.
That cost is accepted as the price of removing nothing.

Where this rule *does* bite is the concept board, not the roster. `BWTRAN` and `KRTRAN`
against stock `TRAN` is a genuinely hard tandem-rotor legibility problem and should be
judged on drawn art rather than argued in text. If a concept board fails, the answer is
to revise the art or the role, and any proposal to drop an actor comes back as a fresh
decision here.

**To tighten later:** the stricter variants — cap at three, or cap at two — are recorded
in this document's history and can be adopted at any time. Adopting one would be a
removal decision and would need its own approval.

### 8.10 Programme rule: mobile air-defense vehicles — **DEFAULTED to mechanics-only. Nothing is removed.**

No preference expressed, so this defaults to the non-destructive option.

**Rule in force:**

> Any faction may field a mobile air-defense vehicle. Each must behave differently from
> every other one in the catalog, even where silhouettes are similar. Distinctness is
> enforced on **mechanics**, not by rationing the slot.

Effect: **nothing is cut.** Israel and South Korea keep their mobile air-defense slots
alongside the four shipped systems and the three new proposals.

The engagement niches identified during research stay useful as a *design aid* rather
than a gate, because they are a ready-made way to satisfy the distinctness requirement:

| Niche | Occupant |
| --- | --- |
| Missile-only, medium range | `CNMANTIS`, `IRRAAD`, `SADS`, `GOKKALKAN` (all shipped) |
| Gun and missile mixed | `USSHORAD` |
| Radar-directed gun only | `BWSHORAD` |
| Massed light gun, no radar | `KPAAA` |
| Unassigned | Israel and South Korea, to be chosen at expansion |

Israel and South Korea should pick a mechanic no one else holds — layered handoff to a
static battery, or an interceptor-magazine model shared with their naval point defense
are both unoccupied. That is a design task at expansion, not a gate on whether the actor
exists.

### 8.11 Applied: active protection systems — two owners, different mounts

Removes nothing, so it is applied.

> The United States owns the turret-mounted, tank-only version on `USMBT`: larger
> intercept magazine, longer recharge, protects itself. Israel owns the hull-mounted
> carrier version on `ILAPC`: smaller magazine, faster recharge, coverage extending to
> nearby friendly infantry.

Different parameters, different mount points, different tactical use. Both are grounded
in their respective real systems, and neither copies the other's mechanic.

## 9. Confidence levels

### 9.1 Source verification method, and its limits

Every URL cited in this document was link-checked on 2026-08-16. The result matters for
how much weight the citations carry, so it is recorded rather than hidden:

| Domain group | Link-check result | What this means |
| --- | --- | --- |
| `bundeswehr.de`, `dapa.go.kr`, `mnd.go.kr`, `idf.il`, `dvidshub.net` | HTTP 200 | Retrieved successfully |
| `rtx.com` | HTTP 200, page fetched and read | Content verified directly; produced the correction in §5.5 |
| `army.mil`, `af.mil`, `afsoc.af.mil`, `navy.mil`, `peosoldier.army.mil`, `dia.mil`, `media.defense.gov`, `crsreports.congress.gov`, `mod.gov.il` | HTTP 403 to automated requests | **Not dead.** These hosts uniformly block automated retrieval. The URLs were obtained from official-domain search indexing and their titles and summaries match the claims made here, but the pages were **not** opened directly during this research pass |

**Consequence for review:** every 403-group link in this document must be opened by a
human before the contract is frozen, and the specific sentence it supports confirmed.
The link-check tells us the URLs are well-formed and on the right official domains; it
does not prove the page content. The one page that *was* fetched directly immediately
produced a correction, which is the reason this limitation is stated plainly instead of
being treated as a formality.

### 9.2 Claim confidence

| Claim | Confidence | Basis |
| --- | --- | --- |
| M1A2 SEP v3 is fielded | High | army.mil rollout article |
| M1E3 is a prototype, not inventory | High | army.mil prototype unveiling, CRS IF12495 |
| XM30 is not current inventory and its direction was under reassessment in early 2026 | Medium-high | CRS IF12094; the Milestone B situation is reported inconsistently and this document deliberately does not state a single outcome |
| M2A4 Bradley is fielded | High | army.mil first-unit and contract articles |
| Trophy-family APS fitted fleet-wide to Abrams | **Low-medium** | Only testing and development coverage found from official sources. **This document does not assert fleet-wide fitment.** The gameplay mechanic is presented as a design choice with a documented real-world basis, not as a claim about current inventory |
| SGT STOUT is fielded | High | army.mil renaming, fielding, and NATO live-fire articles |
| M88A3 is in test, M88A2 fielded | High | army.mil modernization article |
| HIMARS fielded; PrSM Inc 1 delivered and demonstrated | High | army.mil delivery and Talisman Sabre articles |
| PrSM Increment 4 is not fielded | High | army.mil industry-day article states awards anticipated late FY2026 |
| M7/M250 type classified and fielding to the close-combat force | High | army.mil type-classification article, PEO Soldier page |
| Javelin fielded | High | army.mil articles |
| F-35A fielded | High | af.mil fact sheet |
| MQ-9 fielded | High | af.mil fact sheet |
| UH-60M fielded | High | PEO Aviation coverage |
| AC-130J fielded; missions are close air support, air interdiction, armed reconnaissance; persistent direct-fire platform | High | Air Force and AFSOC fact sheets, both named in §5.3. **Upgraded from Medium** after the §8.4 ruling forced the placeholder citation to be replaced |
| Fires-network mechanic is buildable with existing engine traits | High | Traits and shipped usages read directly in this tree; see the §8.8 table |
| DDG 51 Flight III with SPY-6, DDG 125 first of configuration | High | navy.mil fact file and press release |
| Virginia-class fielded with VLS, VPM, Mk 48 | High | navy.mil fact file |
| LPD 17 class fielded | High | navy.mil fact file |
| LTAMDS in operational assessment, not full-rate production | High | army.mil 360-degree demonstration article |
| LTAMDS three-array configuration | High | RTX page fetched and read directly on 2026-08-16 |
| LTAMDS replaces the Patriot radar | **Rejected** | The manufacturer page states the opposite emphasis — that it preserves existing Patriot investment. Claim removed from §5.5 |
| Counter-UAS interceptor fielded under rapid acquisition | Medium-high | army.mil rapid acquisition article |
| TITAN at prototype / advanced prototype, not full fielding | High | army.mil JBLM delivery and 1st MDTF articles |

Nothing in this document converts uncertain reporting into a factual capability claim.
Where reporting conflicts — notably XM30 Milestone B — the conflict is recorded rather
than resolved.

---

## 10. Ready to freeze checklist

Settled — and **no actor was removed to settle any of them**:

- [x] §8.1 Abrams configuration — fielded M1A2 SEP v3, three visual deltas mandatory
- [x] §8.2 Bradley/XM30 — fielded M2A4
- [x] §8.3 `USICV` — **kept** by product-owner decision, concept-board gate mandatory
- [x] §8.4 U.S. Apache — defaulted to "no", aircraft stay at four, reversible on request
- [x] §8.5 naming and trademark policy
- [x] §8.6 `USJTAC` kept, area-condition constraint binding
- [x] §8.7 `USLPD` keeps both roles, no weapon, high band
- [x] §8.8 network mechanic **verified feasible against the engine** — rules-only, no new traits
- [x] §8.9 shared airframes — defaulted to grandfather + role-only; every airframe in every roster survives
- [x] §8.10 mobile air defense — defaulted to mechanics-only; every slot in every roster survives
- [x] §8.11 active protection split between U.S. turret mount and Israeli hull mount
- [x] `USAC130` citation fixed — two named Air Force fact sheets replace the placeholder index link
- [x] Actor-id collision audit run — 21 ids, zero collisions
- [x] Roster counts confirmed against the roadmap's preferred shape
- [x] Faction internal name `usa`, side `Allies`, pool `RandomAllies`, doctrine `joint-fires` confirmed unused
- [x] Husk and sink id reservations recorded
- [x] Cost bands sanity-checked against the shipped Turkey and China numbers
- [x] Stock `APC` and `JEEP` availability verified in the shipped rules

Needs a human, not another research pass:

- [ ] Every §9.1 "403 group" link opened and the sentence it supports confirmed — **the one blocker left**
- [ ] Actor-id collision audit re-run at freeze time (this audit is valid only for `a0f751b972`)
- [ ] Access dates refreshed if freeze happens materially later than 2026-08-16

Art-workstream gates under roadmap gate 2 — these do **not** block freezing this contract,
and a failed board means revising art, never silently dropping an actor:

- [ ] `USMBT` shown beside `M1A2S` at native scale on all three palettes
- [ ] `USICV` shown beside `ARAS8` at native scale on all three palettes

**No content in this programme has been deleted or narrowed.** Where a decision was left
to a default, the default was the option that removes nothing, and it is labelled as such
so it can be changed later.

## 11. Handoff boundary

Per `docs/modern-faction-roadmap.md`, once this contract is approved the research
workstream's output is the frozen actor-id and visual contract above — nothing else.
This document does not authorise, and must not be read as authorising, edits to art,
sequence YAML, `mods/ra/experiences.yaml`, shared manifests, global rules, or any
existing faction file.
