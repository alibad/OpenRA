# Faction contract: Israel (`israel`)

Status: **sourced outline; inherited rulings applied.**

Per the assignment governing this workstream, this document is held at outline depth
until the U.S. contract is reviewed. The blocking decisions in `docs/faction-spec-usa.md`
have now been ruled, and the four that bind this faction are applied below rather than
left as open questions:

| Inherited ruling | Effect here |
| --- | --- |
| §8.9 airframe cap of two, second must differ in role | **`ILAH64` is cut** — the Apache family is closed at `HELI` + `AH64SA`. `ILF35` is confirmed as the second and final F-35 slot, in a strike-and-escort role distinct from `USF35`'s ground-strike-only role |
| §8.10 mobile air defense needs a doctrine gate and a free niche | Israel gets **no** mobile air-defense vehicle. Its interception identity runs through the static `ILDOME` and the naval `ILCORV`, which is the more distinctive expression anyway |
| §8.11 active protection, two owners | Israel owns the **hull-mounted carrier** version on `ILAPC`: smaller magazine, faster recharge, coverage extending to nearby friendly infantry. The U.S. owns the turret-mounted tank-only version |
| §8.5 naming policy | Generic display names for buildings and defenses; real designations for vehicles, aircraft, and ships |

Aircraft roster after the cut: `ILF35`, `ILUAV`, `ILUTIL` — three, within the roadmap's
preferred range.

Sources accessed **2026-08-16**. Re-check at freeze time.

---

## 1. Doctrine sketch

**Protected maneuver with layered interception.** Heavy protection on a small number of
expensive platforms, fast sensor-to-shooter response, and a defensive interception layer
that blunts the artillery and rocket pressure other packs rely on — until it is
saturated or its supply is exhausted.

The intended failure mode is explicit and must survive to implementation: **interception
is finite.** Magazines, reload windows, and a supply dependency are what make this
faction beatable. A faction that intercepts indefinitely is not a design, it is a wall.

Provisional tradeoffs:

- Small unit count; every loss is expensive.
- Very strong against sustained indirect fire, weak against simultaneous saturation.
- Strong reconnaissance and unmanned coverage, weak sustained heavy-armor attrition.
- Interception coverage should be **visible to both players**, following the same
  principle applied to U.S. network coverage.

Provisional side and pool: `Side: Allies`, `RandomPool: RandomAllies`,
`Doctrine: protected-maneuver` (unused string — verified).

---

## 2. Proposed roster and actor ids

All ids below were checked with a whole-word recursive search across `*.yaml`, `*.lua`,
and `*.cs` at tree state `a0f751b972`. **Zero collisions.** The `IL` prefix is unused
and is deliberately distinct from Iran's `IR`.

| Domain | Actor id | Slot | Candidate basis |
| --- | --- | --- | --- |
| Infantry | `ILRIFLE` | Line infantry | Standard rifle section |
| Infantry | `ILSPIKE` | Anti-armor team | Spike-family guided missile team |
| Infantry | `ILDRONE` | Recon / EW operator | Small UAS and electronic-warfare operator |
| Infantry | `SILENTEDGE` | Commando (build limit 1) | Original project character, call-sign convention |
| Vehicles | `ILMBT` | Main battle tank | Merkava Mk 4 Barak |
| Vehicles | `ILAPC` | Heavy tracked carrier | Namer |
| Vehicles | `ILWHEEL` | Wheeled carrier | Eitan, including the 30 mm turreted configuration |
| Vehicles | `ILSPH` | Self-propelled artillery | Self-propelled howitzer |
| Vehicles | `ILUGV` | Unmanned ground vehicle | Reconnaissance / armed UGV |
| Aircraft | `ILF35` | Strike and escort fighter | F-35I |
| Aircraft | `ILUAV` | Long-endurance UAS | Heron-class |
| Aircraft | `ILUTIL` | Utility transport | Utility helicopter |
| ~~Aircraft~~ | ~~`ILAH64`~~ | ~~Attack helicopter~~ | **CUT** by the §8.9 airframe cap. Id reserved, not reused |
| Navy | `ILCORV` | Corvette with point defense | Sa'ar 6-class with a shipborne interception layer |
| Navy | `ILSUB` | Submarine | Dolphin-class |
| Navy | `ILPATROL` | Patrol / landing craft | Coastal patrol |
| Buildings | `FACT`, `WEAP`, `TENT`, `HPAD`, `SYRD` | Stock production | — |
| Defenses | `ILDOME` | Point interception battery | Iron Dome-style, abstracted |
| Defenses | `ILSENSOR` | Sensor / EW node | Detection and jamming |
| Defenses | `ILWALL` | Protected anti-armor position | Hardened fighting position |

Reserved husk and sink ids: `ILMBT.Husk`, `ILAPC.Husk`, `ILWHEEL.Husk`, `ILSPH.Husk`,
`ILUGV.Husk`, `ILF35.Husk`, `ILUAV.Husk`, `ILUTIL.Husk`, `ILCORV.Sink`, `ILSUB.Sink`,
`ILPATROL.Sink`.

Roster shape after the §4.1 cut: 4 infantry, 5 vehicles, **3** aircraft, 3 naval —
matches the roadmap's preferred proportions. `ILWHEEL` is subject to the §4.4 wheeled
carrier allocation and may yet be re-scoped or dropped, which would take vehicles to 4.

---

## 3. Source anchors

These are the starting points for the per-actor evidence table, not a substitute for it.
Each actor will need its own citation with a service/procurement/prototype status when
this document is expanded.

| Topic | Source | Accessed |
| --- | --- | --- |
| Armored vehicle production programme | [Israel Defense Procurement Committee approves approximately $1.5B plan to expand tank and APC production](https://mod.gov.il/en/press-releases/press-room/israel-defense-procurement-committee-approves-approximately-15b-plan-to-expand-tank-and-apc-production) | 2026-08-16 |
| Armored vehicle directorate | [Merkava and Armored Vehicles Directorate, Israel MOD](https://mod.gov.il/en/departments/merkava-and-armored-vehicles-directorate) | 2026-08-16 |
| Merkava Mk 4 Barak | [Meet the Merkava Mk. 4 Barak, IDF](https://www.idf.il/en/mini-sites/technology-and-innovation/meet-the-merkava-mk-4-barak/) | 2026-08-16 |
| Merkava / Namer component contracts | [Israel MOD secures approximately $26 million deal for Merkava tank and Namer APC components](https://mod.gov.il/en/press-releases/press-room/israel-mod-secures-approximately-26-million-deal-for-merkava-tank-and-namer-apc-components) | 2026-08-16 |
| Missile-defence R&D authority | [Directorate of Defense Research and Development, Israel MOD](https://mod.gov.il/en/departments/directorate-of-defense-research-development-ddrd) | 2026-08-16 |
| Layered missile defence upgrade testing | [David's Sling air and missile defense system successfully completes advanced upgrade tests](https://mod.gov.il/en/press-releases/press-room/davids-sling-air-and-missile-defense-system-successfully-completes-advanced-upgrade-tests) | 2026-08-16 |

**Link-check result, 2026-08-16:** the `idf.il` link returned HTTP 200. All five
`mod.gov.il` links returned HTTP 403 to automated requests — that host blocks automated
retrieval and the pages were **not** opened directly. See `docs/faction-spec-usa.md`
§9.1 for the full method and its limits; the same rule applies here, and every
`mod.gov.il` link must be opened by a human before this document is expanded.

Still to source before expansion: the self-propelled artillery slot, the UGV slot, the
Sa'ar 6 shipborne interception configuration, the Dolphin-class boat, the Heron-class
UAS, the utility helicopter, and the F-35I configuration. Each needs an official
Israeli MOD, IDF, or manufacturer citation with a fielded/ordered/demonstrated status.

---

## 4. Known overlap hot spots

Checked against stock Allies/Soviets and the shipped China, Iran, Saudi, Yemen, and
Turkey packs. Items resolved by an inherited ruling are marked **RULED**; items that
still need this document expanded before they can be answered honestly are marked
**DEFERRED**, with the reason.

### 4.1 Attack helicopter — **RULED: `ILAH64` cut**

The Apache family is closed at stock `HELI` plus Saudi's `AH64SA` under the §8.9 cap.
The anti-armor air role moves to `ILUAV`. Aircraft roster is `ILF35`, `ILUAV`, `ILUTIL`.

### 4.2 Active protection — **RULED: Israel owns the carrier-mounted version**

Per §8.11: `ILAPC` carries hull-mounted active protection with a smaller magazine, a
faster recharge, and coverage that extends to nearby friendly infantry. `USMBT` carries
the turret-mounted, tank-only version with a larger magazine and a longer recharge.
Different mount, different parameters, different tactical use.

Sourcing note for expansion: Israel MOD material describes the Namer as equipped with an
active protection system, which makes this the better-sourced of the two — the U.S. side
is deliberately held at low-medium confidence.

### 4.3 Interception versus shipped point defense — **RULED: reuse the existing module**

`docs/experience-capability-packs.md` lists an existing **Point-defense interception**
capability module, and Saudi's `SA_FRGT` already has a finite defensive-interceptor
magazine. `ILDOME` builds on that contract rather than inventing a parallel one, and must
not be a strictly better `SA_FRGT` ability. Naming follows §8.5: generic display name,
real basis documented here only.

### 4.4 Wheeled carrier crowding — **DEFERRED, with the principle ruled**

`USICV` has been cut (§8.3), so the live question is Israel's `ILWHEEL` (Eitan) versus
Germany's `BWWHEEL` (Boxer) versus the shipped `ARAS8`.

**Principle ruled now:** the catalog holds at most **two** eight-wheeled carrier
silhouettes. `ARAS8` holds one. The second is allocated to whichever of the Israel or
Germany contracts is expanded first, and the other faction must either differentiate by
turret and unit class — a wheeled *gun* vehicle is not a wheeled *carrier* — or drop the
slot.

**Allocation deferred**, deliberately. Unlike the U.S. decisions, neither faction has a
per-actor contract yet, so ruling this now would be deciding without the evidence that
makes the decision meaningful. Recorded for whoever expands first: **Germany currently
has the stronger claim**, because `BWWHEEL` and `BWSHORAD` share the Boxer hull and earn
a deliberate two-actor family read, which is exactly the argument `USICV` could not make
once it stood alone. Israel retains `ILAPC` either way, so it is not left carrier-less.

### 4.5 Recon / EW infantry crowding — **RULED: the area niche is taken**

§8.6 keeps `USJTAC` on the binding condition that it is the only observer projecting an
**area** condition consumed by a restricted weapon list. `ILDRONE` must therefore be a
point-marker, a scout, or an EW disruptor — not a second area-network source. That is the
constraint; the specific choice is part of this document's expansion.

### 4.6 F-35 duplication — **RULED: `ILF35` is the second and final slot**

Under the §8.9 cap the F-35 family gets two appearances. They are allocated to `usa`
(ground strike only, accuracy gated on its network) and `israel` (strike and escort,
paired with the interception layer). South Korea must pick a different fighter — see
`docs/faction-spec-koreas.md`. Both F-35 actors ship their own sprite set; a palette-only
variant is already forbidden by the roadmap and is forbidden again here.

---

## 5. Remaining open questions

The four questions previously listed here have been ruled and moved into §4. Two remain,
and one is new.

1. **Depiction posture.** The roadmap requires that claims about current conflicts do not
   become faction stereotypes. An explicit editorial guideline for this faction's
   descriptions, voice lines, and unit naming should be agreed **before** text is written,
   not after. Proposed guideline, for confirmation: unit descriptions state gameplay
   function only; no unit, name, or line references a real operation, place, or event; no
   faction-wide morale, zeal, or fanaticism mechanic on any pack in this programme.
2. **Wheeled carrier allocation** (§4.4) — deferred by design until either this document
   or the Bundeswehr document is expanded.
3. **`ILUGV` scope.** An armed unmanned ground vehicle is the roster slot with no
   equivalent anywhere in the catalog and therefore the least constrained by precedent.
   Confirm at expansion whether it is a scout, a weapon platform, or an expendable
   breaching unit — these are three different actors and only one should ship.

---

## 6. Ready to freeze checklist

Done:

- [x] Inherited rulings from `docs/faction-spec-usa.md` §8.5, §8.9, §8.10, §8.11 applied
- [x] `ILAH64` cut; aircraft roster resolved to three
- [x] Active protection precedent resolved
- [x] Mobile air-defense vehicle resolved — Israel gets none
- [x] Actor-id collision audit run — zero collisions, `IL` prefix unused
- [x] Source anchors gathered and link-checked

Remaining:

- [ ] §5.1 depiction posture agreed in writing
- [ ] §5.2 wheeled carrier allocation settled between this document and the Bundeswehr one
- [ ] §5.3 `ILUGV` scope chosen
- [ ] Every `mod.gov.il` link opened by a human and the sentence it supports confirmed
- [ ] This document expanded to a full per-actor contract matching the U.S. format
- [ ] Every actor given an official source with fielded/ordered/demonstrated/conceptual status and an access date
- [ ] Visual landmarks written for native scale and reviewed by the art workstream
- [ ] Actor-id collision audit re-run at freeze time
- [ ] Faction internal name `israel`, side, pool, and doctrine string confirmed unused at freeze time

**Not frozen. Outline depth by design — but no longer blocked on cross-faction rulings.**
