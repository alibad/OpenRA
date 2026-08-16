# Faction contract: Israel (`israel`)

Status: **sourced outline only.**

Per the assignment governing this workstream, this document is deliberately held at
outline depth. It expands to a full per-actor contract only after
`docs/faction-spec-usa.md` has been reviewed and its three blocking decisions
(§8.1, §8.3, §8.4 there) are answered, because those rulings set precedent for the
Abrams-style silhouette-duplication problem, the trademark/naming policy, and the
attack-helicopter exclusion — all three of which recur here.

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
| Aircraft | `ILF35` | Strike fighter | F-35I |
| Aircraft | `ILUAV` | Long-endurance UAS | Heron-class |
| Aircraft | `ILUTIL` | Utility transport | Utility helicopter |
| Aircraft | `ILAH64` | Attack helicopter | **Provisional — see §4.1, likely to be cut** |
| Navy | `ILCORV` | Corvette with point defense | Sa'ar 6-class with a shipborne interception layer |
| Navy | `ILSUB` | Submarine | Dolphin-class |
| Navy | `ILPATROL` | Patrol / landing craft | Coastal patrol |
| Buildings | `FACT`, `WEAP`, `TENT`, `HPAD`, `SYRD` | Stock production | — |
| Defenses | `ILDOME` | Point interception battery | Iron Dome-style, abstracted |
| Defenses | `ILSENSOR` | Sensor / EW node | Detection and jamming |
| Defenses | `ILWALL` | Protected anti-armor position | Hardened fighting position |

Reserved husk and sink ids: `ILMBT.Husk`, `ILAPC.Husk`, `ILWHEEL.Husk`, `ILSPH.Husk`,
`ILUGV.Husk`, `ILF35.Husk`, `ILUAV.Husk`, `ILUTIL.Husk`, `ILAH64.Husk`, `ILCORV.Sink`,
`ILSUB.Sink`, `ILPATROL.Sink`.

Roster shape: 4 infantry, 5 vehicles, 3–4 aircraft, 3 naval — matches the roadmap's
preferred proportions.

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
Turkey packs.

### 4.1 `ILAH64` — likely cut

Saudi's `AH64SA` ships with the display name "AH-64E Apache". Stock Allies `HELI` is a
Longbow Apache. Turkey's `TURNAAH` is a third attack helicopter. `faction-spec-usa.md`
§8.4 already recommends excluding a U.S. Apache for exactly this reason. **Whatever is
ruled there applies here.** Provisional recommendation: cut `ILAH64`, ship three
aircraft, and put the anti-armor air role on `ILUAV`.

### 4.2 Active protection — precedent needed

Trophy-family active protection is central to the Israeli armor identity, and Israel MOD
material describes the Namer as carrying it. But `faction-spec-usa.md` proposes an
active-protection mechanic for `USMBT`. Two factions with the same defensive mechanic is
acceptable only if their parameters and visual reads differ deliberately. **This needs a
cross-faction ruling, not two independent decisions.**

### 4.3 Interception versus shipped point defense

`docs/experience-capability-packs.md` lists an existing **Point-defense interception**
capability module, and Saudi's `SA_FRGT` already has a finite defensive-interceptor
magazine. `ILDOME` must be built on that existing contract rather than inventing a
parallel one, and must not become a strictly better version of the Saudi frigate's
ability.

### 4.4 Wheeled carrier crowding

`ILWHEEL` (Eitan) joins Turkey's `ARAS8` and the proposed U.S. `USICV` as a third
eight-wheeled carrier. See `faction-spec-usa.md` §8.3 — if `USICV` is cut for
duplication, `ILWHEEL` is the next candidate for the same treatment.

### 4.5 Recon / EW infantry crowding

`ILDRONE` would be the seventh observer-or-controller infantry actor in the catalog
(`SAJTAC`, `YSPOT`, `TRDRONEOP`, `IRDC`, `CNNETWORK`, proposed `USJTAC`). See
`faction-spec-usa.md` §8.6.

### 4.6 F-35 duplication

`ILF35` and the proposed `USF35` are the same airframe family, and the Koreas contract
proposes a third. A single shared visual treatment with per-faction remap and marking
differences is not acceptable under the review rubric. **A cross-faction ruling on
shared airframes is required** — see §5.4.

---

## 5. Open questions for the product owner

1. **Attack helicopter:** cut `ILAH64`, or accept a fourth Apache-family silhouette?
   (Recommendation: cut.)
2. **Active protection precedent:** which faction owns the mechanic, and how do the
   others differ? (Recommendation: Israel owns the *carrier-mounted* version, the U.S.
   owns the *tank-mounted* version, with different magazine and recharge parameters.)
3. **Interception presentation:** may recognisable real missile-defence system names be
   used in display text, or is the abstraction requirement in the roadmap
   ("model counterplay, not promise literal real-world interception performance")
   to be enforced with fully generic names? (Recommendation: generic names, real basis
   documented in this contract only.)
4. **Shared airframes across factions:** F-35 appears in the proposed U.S., Israeli, and
   South Korean rosters. Ruling needed on whether a shared airframe may appear in
   multiple packs at all, and if so what per-faction visual delta is mandatory.
5. **Wheeled carrier count:** how many eight-wheeled carriers may the catalog hold?
6. **Depiction posture:** the roadmap requires that claims about current conflicts do not
   become faction stereotypes. An explicit editorial guideline for this faction's
   descriptions, voice lines, and unit naming should be agreed before text is written,
   not after.

---

## 6. Ready to freeze checklist

- [ ] `docs/faction-spec-usa.md` reviewed and its blocking decisions answered
- [ ] §5.1 attack helicopter ruled
- [ ] §5.2 active protection precedent ruled
- [ ] §5.3 interception naming ruled
- [ ] §5.4 shared airframe policy ruled
- [ ] §5.6 depiction posture agreed in writing
- [ ] This document expanded to a full per-actor contract matching the U.S. format
- [ ] Every actor given an official source with fielded/ordered/demonstrated/conceptual status and an access date
- [ ] Visual landmarks written for native scale and reviewed by the art workstream
- [ ] Actor-id collision audit re-run at freeze time
- [ ] Faction internal name `israel`, side, pool, and doctrine string confirmed unused at freeze time

**Not frozen. Not approved. Outline only.**
