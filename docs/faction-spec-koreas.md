# Paired faction contract: North Korea (`northkorea`) and South Korea (`southkorea`)

Status: **sourced outline; configuration rulings applied, removal proposals awaiting
approval.**

Held at outline depth by the assignment governing this workstream.

**The rosters in §2 are intact. Nothing has been removed from them.** Two actors were
*added* — `KRFIGHT` and `KRHELI` — as alternatives, not replacements.

| Item | Effect here |
| --- | --- |
| `docs/faction-spec-usa.md` §8.5 naming policy — **applied** | Generic display names for buildings and defenses; real designations for vehicles, aircraft, and ships. Removes nothing |
| Local: roster asymmetry — **applied** | North Korea ships fewer aircraft and a weaker navy; that asymmetry *is* the design, not a gap to be filled. Adds a constraint, removes nothing |
| §8.9 shared-airframe cap — **proposal only** | If approved, `KRAH64`, `KRTRAN`, and `KRF35` would all be cut. **All three remain in the roster below** until you decide |
| §8.10 mobile air-defense niche gate — **proposal only** | If approved, `KRSHORAD` would be cut and `KPAAA` admitted. **`KRSHORAD` remains in the roster below** |

`KRFIGHT` (a domestically produced fighter) and `KRHELI` (a domestic light armed
helicopter) are listed as **additions**, both collision-audited. If §8.9 is approved they
become South Korea's air roster; if it is rejected they are optional extras, and the
faction chooses between them and the F-35/Apache slots rather than being forced. Neither
is sourced yet — see §4.2.

These two factions are specified in one document because the roadmap requires them to be
designed as a pair: "artillery pressure, fortification, reconnaissance, counterbattery
fire, and missile defense have deliberate counters rather than one-sided gimmicks." A
one-sided pair is a failed design, and neither half may be approved alone.

Sources accessed **2026-08-16**. Re-check at freeze time.

---

## 0. Two hard rules that govern this document

### 0.1 No strategic nuclear weapons as skirmish superweapons

Per the roadmap, strategic nuclear weapons are **scenario-level narrative devices, not
ordinary skirmish superweapons**. Neither faction receives a nuclear support power, a
nuclear-tipped missile actor, or a fallout mechanic in skirmish. This is not negotiable
in this contract, and any later proposal to add one must be raised as a separate product
decision with its own review — not slipped in as a balance patch.

The North Korean pressure identity is delivered by **massed conventional artillery,
concealment, and fortification**, which is both the historically dominant conventional
capability and the more interesting RTS mechanic.

### 0.2 North Korean corroboration policy

The assignment requires extra caution on North Korean claims. This document adopts a
concrete rule that must survive to expansion:

> **Every North Korean actor requires two independent authoritative sources for its
> platform identity, and at least one must be non-DPRK. Capability claims sourced only
> to DPRK state media, only to a parade appearance, or only to a single adversarial
> assessment are recorded as `claimed, uncorroborated` and may not be turned into a
> gameplay capability.**

Practical consequences already visible at outline stage:

- Platform *existence* and rough class (a tracked self-propelled gun, a multiple rocket
  launcher, a coastal submarine) is generally well corroborated.
- Specific performance figures — range, guidance, penetration, accuracy — frequently are
  not. The contract will therefore specify North Korean actors by **role and silhouette**
  and set their numbers as **game-balance choices**, explicitly labelled as such rather
  than presented as real performance.
- Visual contracts are built on corroborated platform evidence, never on a single
  adversarial description or a single parade photograph.

This is the honest posture, and it also happens to make the faction easier to design:
its identity is *how it fights*, not what its equipment allegedly achieves.

---

## 1. Doctrine sketches

### 1.1 North Korea (`northkorea`)

**Massed pressure from concealment.** Numerous cheap platforms, heavy fortification,
tunnelled repositioning, and continuous conventional artillery pressure. Wins by making
the opponent unable to hold forward ground. Loses to counterbattery fire, air power, and
anything that finds it before it fires.

Provisional tradeoffs:

- Individually weak units; the faction lives or dies on count and position.
- Excellent static defense and concealment; poor mobile combined-arms.
- Almost no air power and weak air defense at high tech — the South Korean and U.S.
  answer.
- Artillery is the identity: it must be genuinely oppressive **and** genuinely
  counterable by the paired faction.

Provisional side and pool: `Side: Soviet`, `RandomPool: RandomSoviet`,
`Doctrine: massed-fortification` (unused string — verified).

### 1.2 South Korea (`southkorea`)

**Find it and kill it first.** Reconnaissance, counterbattery fire, mobile armor, air
power, and layered air and missile defense. Wins by locating and destroying artillery
before it accumulates. Loses to being overwhelmed at multiple points at once, because
its answer is precise rather than broad.

Provisional tradeoffs:

- Expensive, high-quality, low-count.
- The only faction with a purpose-built counterbattery mechanic.
- Strong air and missile defense, weak against ground saturation.
- Its counterbattery advantage should be **reactive** — it answers artillery that has
  already fired, which means the North Korean player always gets the first volley.

Provisional side and pool: `Side: Allies`, `RandomPool: RandomAllies`,
`Doctrine: counterbattery` (unused string — verified).

### 1.3 The pairing

| North Korean pressure | South Korean answer |
| --- | --- |
| Massed rocket artillery volleys | Counterbattery fire that triggers on detected launches |
| Concealed and dug-in positions | Reconnaissance and cloak detection |
| Tunnelled repositioning | Sensor coverage and area denial |
| Numerous cheap armor | Higher-quality mobile armor and air power |
| Ballistic and rocket saturation | Layered missile defense with a finite magazine |

And in the other direction, so the pair is not one-sided:

| South Korean pressure | North Korean answer |
| --- | --- |
| Air superiority and strike | Massed cheap AA guns; hardened, repairable positions |
| Precision counterbattery | Decoy and dispersal; artillery that relocates after firing |
| Expensive high-quality armor | Attrition by numbers and terrain |
| Sensor dominance | Concealment, tunnels, and forcing engagements at short range |

---

## 2. Proposed rosters and actor ids

All ids checked with a whole-word recursive search across `*.yaml`, `*.lua`, and `*.cs`
at tree state `a0f751b972`. **Zero collisions.** The `KP` and `KR` prefixes are unused.

### 2.1 North Korea

| Domain | Actor id | Slot |
| --- | --- | --- |
| Infantry | `KPRIFLE` | Cheap massed line infantry |
| Infantry | `KPAT` | Short-range anti-armor team |
| Infantry | `KPSAPPER` | Fortification / tunnel engineer |
| Infantry | `WINTERFOX` | Commando (build limit 1), original project character |
| Vehicles | `KPMBT` | Main battle tank |
| Vehicles | `KPMRL` | Multiple rocket launcher — the faction's identity unit |
| Vehicles | `KPSPG` | Self-propelled gun |
| Vehicles | `KPAAA` | Mobile anti-aircraft gun |
| Aircraft | `KPFIGHT` | Interceptor — deliberately dated and few |
| Aircraft | `KPUAV` | Small reconnaissance drone |
| Navy | `KPSUB` | Coastal submarine |
| Navy | `KPPATROL` | Fast attack craft |
| Buildings | `FACT`, `WEAP`, `BARR`, `AFLD`, `SPEN` | Stock production |
| Defenses | `KPBUNKER` | Hardened fighting position |
| Defenses | `KPAAGUN` | Massed anti-aircraft gun emplacement |
| Defenses | `KPTUNNEL` | Tunnel entrance — repositioning, not a weapon |

### 2.2 South Korea

| Domain | Actor id | Slot |
| --- | --- | --- |
| Infantry | `KRRIFLE` | Line infantry |
| Infantry | `KRAT` | Guided anti-armor team |
| Infantry | `KRSCOUT` | Reconnaissance / observer |
| Infantry | `STORMCROW` | Commando (build limit 1), original project character |
| Vehicles | `KRMBT` | Main battle tank (K2 family) |
| Vehicles | `KRIFV` | Tracked IFV (K21 family) |
| Vehicles | `KRSPH` | Self-propelled howitzer (K9 family) |
| Vehicles | `KRMLRS` | Guided multiple rocket launcher (Chunmoo family) |
| Vehicles | `KRSHORAD` | Mobile air defense — **flagged** by the §8.10 proposal, still in the roster |
| Aircraft | `KRF35` | Strike fighter — **flagged** by the §8.9 proposal, still in the roster |
| Aircraft | `KRAH64` | Attack helicopter — **flagged** by the §8.9 proposal, still in the roster |
| Aircraft | `KRUAV` | Reconnaissance UAS |
| Aircraft | `KRTRAN` | Transport helicopter — **flagged** by the §8.9 proposal, still in the roster |
| Aircraft | `KRFIGHT` | Fighter, domestically produced programme — **added as an alternative**, see §4.2 |
| Aircraft | `KRHELI` | Light armed / scout helicopter, domestic programme — **added as an alternative**, see §4.2 |
| Navy | `KRDDG` | Aegis-equipped destroyer |
| Navy | `KRSUB` | Submarine |
| Navy | `KRPATROL` | Patrol craft |
| Buildings | `FACT`, `WEAP`, `TENT`, `HPAD`, `SYRD` | Stock production |
| Defenses | `KRSAM` | Layered air and missile defense battery |
| Defenses | `KRRADAR` | Counterbattery radar — the faction's identity defense |
| Defenses | `KRWALL` | Protected position |

Reserved husk and sink ids follow the shipped convention for every vehicle, aircraft,
and naval actor listed above.

`KRFIGHT` and `KRHELI` were collision-audited on the same tree: **zero matches** for both.

Roster shapes as listed: North Korea 4 infantry, 4 vehicles, 2 aircraft, 2 naval,
3 defenses (deliberately air-poor and navy-poor). South Korea 4 infantry, 5 vehicles,
5 aircraft as listed, 3 naval, 3 defenses — five is above the roadmap's preferred range
because `KRFIGHT` and `KRHELI` are listed alongside the flagged actors rather than
instead of them. The §8.9 decision resolves that count one way or the other.

**The asymmetric roster sizes are intentional and approved** — see §4.1.

---

## 3. Source anchors

### 3.1 South Korea

| Topic | Source | Accessed |
| --- | --- | --- |
| Force-improvement systems | [Force improvement systems, ROK Ministry of National Defense](https://www.mnd.go.kr/mnd/235/subview.do) | 2026-08-16 |
| AI and unmanned defence priorities | [AI/unmanned defense priorities, ROK Ministry of National Defense](https://www.mnd.go.kr/mnd/176/subview.do) | 2026-08-16 |
| Acquisition authority | [Defense Acquisition Program Administration (DAPA)](https://www.dapa.go.kr/dapa_en/main.do) | 2026-08-16 |
| Force improvement programme | [Force Improvement, DAPA](https://www.dapa.go.kr/dapa_en/page/selectPage.do?menuSeq=3474&pageSeq=3542) | 2026-08-16 |
| Defence R&D | [Research and Development, DAPA](https://www.dapa.go.kr/dapa_en/page/selectPage.do?menuSeq=3475&pageSeq=3543) | 2026-08-16 |

Still to source per actor: K2, K21, K9, Chunmoo, the mobile air defense system, F-35A in
ROK service, AH-64E in ROK service, the Aegis destroyer class, and the submarine class.
Each needs an official ROK MND, DAPA, or manufacturer citation with a
fielded/ordered/demonstrated status.

Note recorded at outline stage: DAPA material describes **Chunmoo 3.0** as a *future*
version of the Chunmoo multiple rocket launcher system including a loitering
precision-guided weapon. Any Chunmoo-derived actor must distinguish the fielded system
from the future version and must not present the latter as current inventory.

### 3.2 North Korea

| Topic | Source | Accessed |
| --- | --- | --- |
| Unclassified assessment of DPRK military capability | [North Korea Military Power, Defense Intelligence Agency (PDF)](https://www.dia.mil/Portals/110/Documents/News/North_Korea_Military_Power.pdf) | 2026-08-16 |
| Release context and scope | [DIA releases report: North Korea Military Power](https://www.dia.mil/News-Features/Articles/Article-View/Article/2812198/defense-intelligence-agency-releases-report-north-korea-military-power/) | 2026-08-16 |
| Assessment series index | [Military Power Publications, DIA](https://www.dia.mil/Military-Power-Publications/) | 2026-08-16 |
| Reports to Congress on DPRK military developments | [Military and security developments involving the DPRK (PDF)](https://media.defense.gov/2018/May/22/2001920587/-1/-1/1/REPORT-TO-CONGRESS-MILITARY-AND-SECURITY-DEVELOPMENTS-INVOLVING-THE-DEMOCRATIC-PEOPLES-REPUBLIC-OF-KOREA-2017.PDF) | 2026-08-16 |

**Link-check result, 2026-08-16:** the `mnd.go.kr` and `dapa.go.kr` links returned
HTTP 200. All four North Korean source links — three on `dia.mil` and one on
`media.defense.gov` — returned HTTP 403 to automated requests and were **not** opened
directly. See `docs/faction-spec-usa.md` §9.1. This compounds the §0.2 problem: the
faction with the strictest corroboration requirement also has the least directly
verifiable source set, and every one of these documents must be opened and read by a
human before any North Korean actor is written up.

**Caution recorded now, not later:** the DIA assessment linked above was released in
2021 and the report to Congress is older still. Both are authoritative for doctrine,
force structure, and broad capability class, and both are **stale for specific equipment
status**. The second corroborating source required by §0.2 must be more recent for any
actor whose identity depends on recent equipment. The ROK Defense White Paper is the
obvious candidate and needs to be located and cited at expansion.

Under §0.2, no North Korean actor may be written up until its second source exists.
**At outline stage, none of the four North Korean vehicle slots has been dual-sourced
yet.** That work is the bulk of the remaining research effort for this document.

---

## 4. Rulings and remaining overlap hot spots

### 4.1 Roster asymmetry — **RULED: approved**

North Korea ships two aircraft and two naval actors against South Korea's three and
three. That is below the roadmap's preferred shape, and it is the correct answer rather
than a gap: a faction whose stated doctrine is massed ground artillery under
concealment, with almost no air power, should not be handed a full air roster to satisfy
a count. The asymmetry is the pairing.

Binding constraint: asymmetry in *breadth* is approved; asymmetry in *viability* is not.
North Korea must be able to win. The §1.3 counter-tables are the check, and they are
re-examined after expansion.

### 4.2 South Korean aircraft — **FLAGGED, your call. All four still in the roster**

Three of the four proposed South Korean aircraft would be affected if the §8.9
shared-airframe cap is approved:

- `KRF35` — the F-35 family would be capped at two, and `usa` and `israel` hold them.
- `KRAH64` — the Apache family would be closed at stock `HELI` plus Saudi's `AH64SA`.
- `KRTRAN` — a second Chinook in the same transport role as stock `TRAN`.

**None of them has been removed.** All four are listed in §2.2 with their slots intact.

Because approving §8.9 would leave the faction with `KRUAV` alone, two alternatives have
been **added** so the choice is not between a rule and an empty roster:

- **`KRFIGHT`** — a domestically produced fighter programme. Arguably a better outcome
  than a third F-35 regardless of §8.9: a distinct twin-engine planform rather than a
  fourth single-engine stealth silhouette, and an identity no other pack can duplicate.
  **Status discipline is mandatory** — a domestic programme moving through flight test
  and early production must be labelled by its actual status, not presented as a mature
  fleet.
- **`KRHELI`** — a light armed or scout helicopter from a domestic programme, covering
  the rotary slot without touching the Apache family.

Neither is sourced yet. Both need official ROK MND, DAPA, or manufacturer citations
before they can be written up, and that is the first research task at expansion.

Possible outcomes, all live:

| §8.9 outcome | South Korean air roster |
| --- | --- |
| Cap at two | `KRFIGHT`, `KRHELI`, `KRUAV` |
| Cap at three | `KRF35` or `KRAH64` returns alongside the domestic pair |
| Role-only, no cap | Keep `KRF35`, `KRAH64`, `KRTRAN`, `KRUAV`, each with its own sprite set and a distinct role |
| Grandfather drawn art only | Nothing here has art yet, so this behaves like the chosen cap |

### 4.3 Mobile air defense — **`KRSHORAD` flagged, `KPAAA` unaffected**

- **`KRSHORAD`** would be cut under the §8.10 proposal, on the grounds that South Korea's
  layered air and missile defense is expressed through the static `KRSAM` battery and its
  naval Aegis hull, and that a seventh mobile air-defense vehicle in the catalog is a lot.
  **It has not been removed** — it is still listed in §2.2.
- **`KPAAA`** survives under either outcome. It occupies the massed-light-gun-without-radar
  niche that nothing else holds, and it is North Korea's only answer to South Korean and
  U.S. air power. Cutting it would break the pairing, so it is not proposed for cutting.

### 4.4 North Korean artillery versus shipped rocket artillery — **Watch**

`KPMRL` joins `CNPHL`, `YMLR`, `IRFAJR`, and `USHIMARS` as the fifth rocket artillery
vehicle. Its separator is **volume**: many small rockets in a long ripple from a wide,
densely tubed pack, against `USHIMARS`'s single precise rocket from one pod. That
contrast is the point of the pair and it must be visible in the sprite, not only in the
numbers.

### 4.5 Counterbattery radar — **new mechanic, feasibility now has a precedent**

`KRRADAR` reacts to enemy artillery fire by enabling friendly counterfire. Nothing in the
catalog does this, so it still needs its own feasibility pass — but the U.S. fires
network turned out to be buildable from existing traits alone
(`docs/faction-spec-usa.md` §8.8), and the same trait family is the obvious starting
point here: a proximity-granted condition plus condition-gated multipliers. Whoever
expands this document should begin from that pattern rather than assuming new engine
work, and should confirm rather than assume.

### 4.6 Tunnels — **RULED: reuse the existing module**

`docs/experience-capability-packs.md` lists an existing **Transit network** capability
module. `KPTUNNEL` builds on it rather than inventing a parallel mechanic. Confirm the
module's actual contract at expansion.

### 4.7 Recon / EW infantry — **RULED: the area niche is taken**

§8.6 keeps `USJTAC` as the only observer projecting an **area** condition consumed by a
restricted weapon list. `KRSCOUT` must be a point-marker or a scout. Given that South
Korea's identity is reactive counterbattery, a spotter that reveals recently-fired
artillery is the natural fit and does not collide with the U.S. mechanic.

### 4.8 Depiction — **proposed guideline, needs one confirmation programme-wide**

Both Korean factions describe an active, politically live confrontation. The roadmap
requires that claims about current conflicts do not become faction stereotypes. Proposed
guideline, identical to the one in `docs/faction-spec-israel.md` §5.1 and to be confirmed
once for the whole programme rather than three times:

- North Korea is an **artillery-and-fortification doctrine**, not a caricature. No unit,
  description, or line trades on the state's domestic conduct.
- Neither faction gets a "fanatic", "human wave", or morale-suicide mechanic. Neither does
  any other pack in this programme.
- Unit descriptions state gameplay function. They do not editorialise.
- No unit, name, or line references a real operation, place, or event.

---

## 5. Remaining open questions

Five of the nine questions previously listed here have been ruled. What remains:

1. **§0.1 and §0.2 confirmation.** The no-nuclear-superweapon constraint and the
   two-source corroboration policy are restated as standing constraints. Please confirm
   both explicitly rather than leave them implied, so neither can be quietly revisited
   during balance work. This document will not treat silence as agreement.
2. **North Korean sourcing budget.** Dual-sourcing every North Korean actor under §0.2 is
   substantially more research than any other faction here, and a second source more
   recent than the 2021 DIA assessment still has to be identified. Confirm the cost is
   accepted before expansion begins. This is the single largest remaining unknown in the
   whole programme.
3. **`KRFIGHT` and `KRHELI` sourcing.** Both were added by the §4.2 ruling and neither is
   sourced yet. They are the first research task at expansion.
4. **Depiction posture** (§4.8) — confirm once, programme-wide.

---

## 6. Ready to freeze checklist

Done:

- [x] Applied ruling from `docs/faction-spec-usa.md` §8.5 — removes nothing
- [x] Full rosters preserved; `KRF35`, `KRAH64`, `KRTRAN`, `KRSHORAD` flagged rather than cut
- [x] `KRFIGHT` and `KRHELI` added as alternatives and collision-audited
- [x] Roster asymmetry ruled — approved, with a viability constraint attached
- [x] Tunnel mechanic ruled — reuse the existing Transit network module
- [x] Counterbattery feasibility given a concrete starting pattern
- [x] Actor-id collision audit run — zero collisions, `KP` and `KR` prefixes unused
- [x] Source anchors gathered and link-checked

Remaining:

- [ ] §8.9 shared-airframe proposal decided — determines whether `KRF35`, `KRAH64`, and `KRTRAN` stay
- [ ] §8.10 mobile air-defense proposal decided — determines whether `KRSHORAD` stays
- [ ] §5.1 §0.1 and §0.2 explicitly confirmed
- [ ] §5.2 North Korean sourcing budget accepted, and a post-2021 second source identified
- [ ] §5.3 `KRFIGHT` and `KRHELI` sourced with status labels
- [ ] §5.4 depiction posture confirmed once, programme-wide
- [ ] Every `dia.mil` and `media.defense.gov` document opened by a human and read
- [ ] Every North Korean actor dual-sourced, with `claimed, uncorroborated` items marked and excluded from gameplay capability
- [ ] Counterbattery and tunnel mechanics confirmed feasible against the engine, following the §8.8 method
- [ ] Both documents expanded to full per-actor contracts matching the U.S. format
- [ ] The §1.3 pairing tables re-checked after expansion so neither side is one-sided
- [ ] Actor-id collision audit re-run at freeze time
- [ ] Faction internal names `northkorea` and `southkorea`, sides, pools, and doctrine strings confirmed unused at freeze time

**Not frozen. Outline depth by design. Neither faction may be approved independently of
the other.**
