# United States (`usa`) concept board — Checkpoint B

**Status: concept board ready for product-owner review. Not approved.** No full
frame sheet may be generated until the product owner approves this board
([roadmap](../../modern-faction-roadmap.md) Checkpoint B, gate 2). This directory
is review material only: it adds no SHP, sequence, rule, Experience, or catalog
entry.

Contract: [`faction-spec-usa.md`](../../faction-spec-usa.md) (§3 roster, §5 landmarks
and player-colour zones, §7 overlap matrix, §8.1 and §8.3 comparison gates).

## The boards

Every board exists at native scale and as a 3x nearest-neighbour copy (`@3x`).
Judge legibility on the native file at 100% zoom first; use the 3x file to
inspect pixels.

| File | What it shows |
| --- | --- |
| `usa-concept-board.png` | **The single review board.** All 21 roster actors at native scale on snow, temperate and desert terrain, each in player blue (row 1) and player red (row 2), with a role-paired native reference leading every domain group, a black-fill silhouette row, and a key row. |
| `usa-comparisons-mbt-icv.png` | The two mandatory side-by-side comparisons: `USMBT` beside Saudi `M1A2S` (§8.1) and `USICV` beside Turkish `ARAS8` (§8.3), eight native facings, all three theatres, both player colours, silhouettes, and a 5x detail with the numbered deltas. |
| `usa-facings-states.png` | Every actor and every gameplay state that changes the outline (launcher raised, pod raised, mast stowed/raised, boom working, doors open, ramp lowered, launch/deploy poses), eight native facings each, next to the black-fill silhouettes; stock reference rows (`E1`, `2TNK`, `MIG`, `TRAN`, `DD`, `DOME`) lead each domain. |
| `usa-player-colour-zones.png` | Each actor's remap pixels painted magenta, then the frame remapped with five lobby preset colours (blue, red, green, gold, dark maroon), with the share of opaque pixels on the remap ramp. `M1A2S` and `ARAS8` are included for comparison. |
| `usa-silhouette-matrix.png` | Each U.S. actor's silhouette (three facings) beside the silhouette of every neighbour named in the contract's §7.1/§7.2 overlap matrix, at native scale. |
| `manifest.json` | Generator version, product and engine commits, SHA-256 of every generator script and palette, player colours, remap shares, and SHA-256/size of every board. |

Total size is about 3.9 MB for ten images.

## How to read the board

- **Native frame order.** Facing rows run N, NW, W, SW, S, SE, E, NE — the
  ClassicFacing order Red Alert stores (frames 0, 4, …, 28 of a 32-facing ring;
  0, 2, …, 14 of a 16-facing ring). The line-up uses the SE three-quarter view.
- **Units always use the player palette.** Red Alert draws every unit with
  `temperat.pal` plus the remap ramp (indexes 80–95) on every tileset; only the
  terrain changes. `snow.pal` is byte-identical to `temperat.pal` (see the
  manifest), so the snow theatre differs by its tiles' white indexes, not by
  its palette. `desert.pal` is the Tiberian Dawn desert palette the RA desert
  tileset loads.
- **Every U.S. pixel is a real sprite pixel.** Each frame is rendered, then
  quantized into an indexed frame exactly as a shipping SHP would store it
  (index 0 transparent, index 4 shadow, 80–95 player colour, water-rotation and
  light-rotator indexes excluded), then drawn with a port of OpenRA's
  `PlayerColorRemap`.
- **References.** Project-generated modern-faction art (`M1A2S`, `ARAS8` and
  the other matrix neighbours in `mods/ra/bits`) is drawn in full colour exactly
  as the engine draws it, including `ARAS8`'s turret offset. Stock Red Alert
  art and the upstream sprites derived from it are drawn only as flat grey
  **scale ghosts** or black silhouettes, so this repository does not
  redistribute that artwork. They exist on the board for scale.
- **Terrain.** Backgrounds are synthesized from the exact palette-index
  distribution of each theatre's clear-ground (`clear1.*`) and water (`w1.*`)
  templates and drawn through that theatre's terrain palette. No tile is copied.
  Temperate is the dark case, snow the light case, desert the warm mid case.
- **Aircraft** are lifted 10–14 px with a ground shadow (in-game altitude is
  much higher; it is compressed here to keep rows compact).
- **Labels** are kept out of the art so legibility can be judged unlabelled;
  names live in the key row and the left column.

## Checkpoint B checklist

| Requirement (roadmap gate 2 / contract) | Where |
| --- | --- |
| Every roster actor at approximately in-game scale | Line-up board; 21 actors from §3 (4 infantry, 6 vehicles, 4 aircraft, 3 ships, `USTOC` and 3 defenses). Stock buildings (`FACT`, `WEAP`, `TENT`, `HPAD`, `SYRD`) are unchanged and not redrawn. |
| Snow, temperate and desert palettes | Line-up and comparison boards |
| Player-colour regions marked intentionally, two+ player colours | Zone board (magenta map + five colours); line-up (blue and red) |
| Headgear / weapon / hull / turret / planform / ship profile legible without labels | Line-up, facings and silhouette rows |
| Silhouette-only (black fill) row | Line-up bottom row; facings board right block; matrix |
| `USMBT` beside `M1A2S`, native scale, all three palettes, deltas called out | Comparison board, top half and 5x detail (1 APS boxes, 2 slatted roof screen, 3 bustle rack with stowage) |
| `USICV` beside `ARAS8`, native scale, all three palettes | Comparison board, second pair and 5x detail |
| At least four facings for vehicles, aircraft and ships | Eight on the facings and comparison boards |
| Role-paired native reference per domain | `E1`, `2TNK`, `MIG` + `TRAN`, `DD`, `DOME` ghosts |
| Distinct from every overlap-matrix actor; no palette-only variants | Silhouette matrix |

### Scale check (bounding boxes at native scale)

| U.S. actor | Measured | Contract reference | Result |
| --- | --- | --- | --- |
| `USMBT` | 35 × 19 px side-on | ≈15% longer hull than `2TNK` (24 px); `M1A2S` 34 px | Matches `M1A2S` length; hull alone ≈ +17% over `2TNK` |
| `USIFV` | 24 px side-on | slightly shorter than `2TNK`, taller | 24 px including the gun; hull shorter, much taller |
| `USICV` | 28 px, roof 1.16 units | `ARAS8` length (26 px), lower roofline | Comparable length, lower roof, no central turret |
| `USHIMARS` | 33 px | `V2RL` (32 px) | Match |
| `USF35` | 30 px span | slightly smaller than `MIG` (33 px) | Match |
| `USMQ9` | 48 px span | noticeably wider than `MIG` | +45% |
| `USAC130` | 56 px span | ≈1.6× `MIG` | 1.7× |
| `USUH60` | 41 px long | rotor disc comparable to `TRAN` (41 px) | Match |
| `USDDG` | 66 px | between `DD` (62) and `CA` (70) | Match |
| `USSSN` | 60 px | ≈1.4× `SS` (45 px) | 1.33× |
| `USLPD` | 73 px | comparable to `CA` (70 px), much taller | Match |

## Mandatory comparisons

**`USMBT` vs `M1A2S` (§8.1).** The three mandatory deltas are all present on
every facing: (1) two squared APS launcher boxes standing proud of the turret
cheeks, (2) a slatted roof screen reading as a light hatched rectangle, and
(3) a deep bustle rack whose stowage lump breaks the rear outline. Beyond the
deltas the two tanks separate strongly on material colour: the U.S. set uses
the palette's cool green ramp, `M1A2S` renders as a sand hull that is **55%
player colour** in the running game, while `USMBT` confines player colour to its
contract zones (15%). Silhouettes remain the closest pair on the board — both
are Abrams — so the deltas carry the separation, as §8.1 accepted.

**`USICV` vs `ARAS8` (§8.3).** Separated by the dark V-hull wedge (clearest on
front and rear facings and as a tapering dark band above the wheels on side
facings), a lower flat roofline with no central turret, a small weapon station
far forward and offset right, eight evenly spaced large wheels, and colour
(`ARAS8` is 54% player colour in game). `USSHORAD` shares the chassis
deliberately and is separated from `USICV` by its mast.

## Per-actor notes

Remap = share of opaque pixels on the player-colour ramp, averaged over eight
facings (one for buildings).

| Actor | Landmarks realised (contract §5) | Player-colour zones | Remap | States on the board | Notes |
| --- | --- | --- | --- | --- | --- |
| `USRIFLE` | Squared tan-green helmet with forward optic and mandible cover; chunky rifle with fat suppressor and boxy sight; front-heavy plate carrier with two square pouches | Helmet band, pouch faces, upper sleeves | 6.1% | stand, burst fire | Figures are taller and slimmer than the squat shipped custom infantry, which separates the roster by silhouette. |
| `USJAV` | Fat square tube carried steeply; command launch unit with eyepiece hood; crouched wide stance | Helmet band, launcher rear grip housing | 4.0% | stand, top-attack launch | Tube reads as a large olive mass over the shoulder; the tube stays neutral olive as required. |
| `USJTAC` | Tall whip antenna 6–7 px above the helmet (never trimmed); chest-high tablet; bump helmet with headset boom | Shoulder panels, tablet back shell | 6.1% | stand, deployed | The antenna is the strongest infantry read on the board. |
| `TALONSIX` | Bump helmet with quad-tube NVG (four lens caps); compact suppressed carbine; slim body with thigh rig | Helmet cover strip, thigh rig | 10.8% | stand, crouch-walk | Darker ranger-green kit separates the operator from line infantry. |
| `USMBT` | See §8.1 above | Turret side walls below the APS boxes, bustle-rack frame, hull-front chevron | 14.8% | idle | |
| `USIFV` | Narrow tall slab turret, thin autocannon, boxy twin launcher on the right flank; steep glacis with driver's block offset left; tracks behind skirt panels | Turret side slabs, skirt band | 11.2% | idle, launcher raised | The raise telegraph changes the outline on 5 of 8 facings; it is subtle on the far-side facings. |
| `USICV` | See §8.3 above | Upper hull side, weapon station body | 11.3% | idle | |
| `USHIMARS` | One squared pod carried high; six large wheels, armoured cab well forward with a visible gap; pod elevates steeply | Cab door panels, pod launch-face frame | 10.1% | stowed, pod raised | |
| `USSHORAD` | Tall mast with flat panel radar (tallest U.S. ground element); twin stubby missile pods flanking a short gun; `USICV` chassis without troop doors or ramp | Hull side (matching `USICV`), turret cheeks | 9.3% | mast raised, mast stowed | |
| `USRECOV` | A-frame boom folded flat, raising to a tall triangle; blunt turretless hull with broad dozer blade; spooled cable drum at the rear | Casemate side panels, boom frame members | 16.3% | boom stowed, boom working | No catalog neighbour. |
| `USF35` | Broad chined lifting body blending into the wing; two canted fins forming a shallow V; clean underside; dark finish | Band across the fins, spine stripe | 6.8% | transit, bay open | The bay-open state is under the fuselage and does not change the outline from the RA camera (see open questions). |
| `USMQ9` | Very long thin straight wing; downward-canted V tail with pusher prop; chin sensor ball under a drooped nose | Wingtip panels, tail V faces | 18.2% | transit, loiter bank | |
| `USUH60` | Wide flat low cabin with big square doors that visibly open; canted tail rotor high on a swept fin; long tail-wheel arm | Door frames, tail fin | 8.9% | flight, doors open | Unarmed transport read; see the `MH60` note below. |
| `USAC130` | High straight wing with four propellers; long fat fuselage with tall square fin; gun ports on the left side only | Tail fin, engine nacelle bands | 10.4% | transit, left orbit (banked) | Largest aircraft on the board. |
| `USDDG` | Blocky superstructure with four flat radar panels on its chamfered corners (two visible from any facing); hatched VLS decks fore and aft; single squat funnel; one bow gun | Superstructure side band, funnel cap | 3.4% | idle | Low share because the hull is large; the band and cap still show clearly on side and three-quarter views. |
| `USSSN` | Smooth fat teardrop hull; short thick sail well forward; row of circular payload hatches forward | Sail sides, narrow deck stripe | 11.4% | surfaced | |
| `USLPD` | Tall enclosed faceted mast; long flat flight deck over the whole stern; stern ramp that lowers into the water | Waterline band, mast faces | 8.9% | idle, ramp lowered | |
| `USTOC` | Two shelter containers with a covered walkway; large mesh dish on a short mast; cable run and generator skid | Container end walls, dish mount | 15.0% | active | Lower and wider than `DOME`. |
| `USIAMD` | Large panel array tilted back steeply; separate four-canister launcher that elevates; low power unit with exhaust stack | Array frame edge, launcher box sides | 26.1% | ready, launcher elevated | |
| `USCUAS` | Small squared radar face on a post; tight bundle of thin tubes pointed steeply up; sandbag ring | Post collar, bundle frame | 12.9% | ready | Single-cell, `AGUN`-scale. |
| `USNODE` | Tall lattice mast with guy wires; drum sensor head; tiny ground shelter | Shelter box, band near the mast top | 25.3% | active | Tallest thin silhouette in the faction. |

## Open questions for the product owner

1. **Player-colour coverage.** Following the contract's zones strictly gives
   4–26% remap (infantry 4–11%). Stock Red Alert units are far heavier
   (`E1` ≈ 62%, `2TNK` ≈ 73%), and the shipped `M1A2S`/`ARAS8` are ≈ 55%.
   Ownership reads on vehicles, aircraft, ships and structures; on infantry it
   is marginal at native zoom. Approve the contract zones as drawn, or widen
   them (for example the whole plate carrier and helmet cover on infantry, the
   skirt band on `USMBT`)?
2. **U.S. material colour.** The concept uses a cool forest green, which is
   what separates the U.S. vehicles from sand `M1A2S` and warm-olive Turkish
   vehicles before shape does. Keep green, or prefer desert tan (which would
   put more weight on the §8.1 deltas)?
3. **Camera.** The modern-faction renderer views from about 35° elevation,
   which makes vehicles read taller than stock Red Alert art. The concept keeps
   that camera for consistency with the shipped modern factions. Keep it, or
   adopt a steeper stock-like camera for the U.S. sheets?
4. **`USF35` weapons bay.** The bay-open state is invisible from above. Keep it
   as a colour-only cue, or add a visible external store during the attack
   pass (the contract's clean-underside landmark applies to transit only)?
5. **`MH60` overlap.** The upstream `MH60` Black Hawk gunship is buildable by
   any Allied faction with a helipad (`~hpad`) but is not in the contract's
   overlap matrix. `USUH60` is separated by being unarmed with visible open
   doors, a flat cabin and no stub wings (see the silhouette matrix), but a U.S.
   player would see two Black Hawks in one build palette unless gameplay
   integration excludes `MH60` for `usa`. That is a gameplay-integration
   decision, recorded here because it affects the silhouette review.
6. **Infantry proportions.** U.S. infantry are taller and slimmer than both
   stock and shipped custom infantry. That separates them clearly, but the
   final sheets need the full 713-frame contract; confirm the proportions
   before animation work starts.

## Observations for the art audit (no action taken here)

- Shipped `M1A2S` and `ARAS8` frames are ≈ 55% player colour because
  nearest-colour quantization lands sand and olive pixels on the remap ramp
  (80–95); the U.S. generator excludes the ramp except for authored zones.
- `scripts/red_sea_directional_vehicle.py` `_render` culls and depth-sorts
  against `(0, -0.82, 0.57)` while its projection views along `(0, 0.82, 0.57)`,
  so north-facing faces are painted over roofs. The U.S. renderer uses the
  matching camera.
- The `SAHINX` and `F15SA` wing polygons (and apparently `IRAZAR` and
  `IRMOHAJER`) face downward and are culled, so their planforms render as thin
  fuselage lines at most facings (visible in the silhouette matrix).

## Originality and provenance

All U.S. geometry is original low-poly project geometry authored in
`OpenRA-AI/scripts/usa_concept_actors.py`; no Command & Conquer asset, other
mod, photograph, logo or third-party artwork was used as a source, and no
image model was used. Real-world equipment informs only the landmarks the
contract lists. Westwood content is read only at build time for palettes,
terrain statistics and the scale ghosts.

## How to regenerate

From the product repository (`OpenRA-AI`, generator commit in `manifest.json`):

```powershell
# 1. Build the engine checkout that will receive the boards (OpenRA.Utility).
dotnet build -c Release -p:TargetPlatform=win-x64            # in the engine checkout
# 2. Render. --support-dir must contain Content/ra/v2 (owned Red Alert content);
#    it is only read - the script copies it into a private scratch support dir.
python scripts/build-usa-concept-board.py `
    --engine <engine checkout> `
    --support-dir <directory containing Content/ra/v2> `
    --output <engine checkout>/docs/concept/usa
# 3. Check the renderer contract.
python -m pytest tests/test_usa_concept_models.py
```

The output is deterministic: two independent runs produce byte-identical
boards (verified 2026-09-28). The manifest records the SHA-256 of each
generator script (line-ending independent), each palette and each board.

Regeneration writes only this directory. Edit the geometry in
`usa_concept_actors.py`, the renderer and quantizer in `usa_concept_models.py`,
the player-colour port in `usa_concept_render.py`, and the board layout in
`build-usa-concept-board.py`.
