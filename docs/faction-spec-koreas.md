# Paired faction contract: North Korea (`northkorea`) and South Korea (`southkorea`)

Status: **sourced outline only.**

Held at outline depth by the assignment governing this workstream. Expands to a full
per-actor contract only after `docs/faction-spec-usa.md` has been reviewed and its
blocking decisions answered.

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
| Vehicles | `KRSHORAD` | Mobile air defense |
| Aircraft | `KRF35` | Strike fighter |
| Aircraft | `KRAH64` | Attack helicopter — **provisional, see §4.2** |
| Aircraft | `KRUAV` | Reconnaissance UAS |
| Aircraft | `KRTRAN` | Transport helicopter — **provisional, see §4.2** |
| Navy | `KRDDG` | Aegis-equipped destroyer |
| Navy | `KRSUB` | Submarine |
| Navy | `KRPATROL` | Patrol craft |
| Buildings | `FACT`, `WEAP`, `TENT`, `HPAD`, `SYRD` | Stock production |
| Defenses | `KRSAM` | Layered air and missile defense battery |
| Defenses | `KRRADAR` | Counterbattery radar — the faction's identity defense |
| Defenses | `KRWALL` | Protected position |

Reserved husk and sink ids follow the shipped convention for every vehicle, aircraft,
and naval actor listed above.

Roster shapes: North Korea 4 infantry, 4 vehicles, 2 aircraft, 2 naval, 3 defenses
(deliberately air-poor and navy-poor). South Korea 4 infantry, 5 vehicles, 2–4 aircraft,
3 naval, 3 defenses.

**The asymmetric roster sizes are intentional and are themselves a design decision
needing approval** — see §5.1.

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

## 4. Known overlap hot spots

### 4.1 North Korean artillery versus shipped rocket artillery

`KPMRL` joins `CNPHL`, `YMLR`, `IRFAJR`, and the proposed `USHIMARS` as the fifth rocket
artillery vehicle. Its separator should be **volume**: many small rockets fired in a
long ripple from a wide, densely tubed pack, where `USHIMARS` is a single precise rocket
from one pod. That contrast is the whole point of the pair, and it must be visible in
the sprite, not just in the numbers.

### 4.2 South Korean aircraft — duplication risk

- `KRF35` is a third F-35 alongside proposed `USF35` and `ILF35`. See
  `faction-spec-israel.md` §5.4 — this needs a programme-level ruling on shared
  airframes.
- `KRAH64` would be a **fifth** Apache-family silhouette (`AH64SA`, stock `HELI`,
  `TURNAAH`, proposed `ILAH64`). `faction-spec-usa.md` §8.4 recommends excluding the
  U.S. one for this reason. **Provisional recommendation: cut `KRAH64`.**
- `KRTRAN` risks duplicating stock `TRAN`. **Provisional recommendation: cut**, and let
  the faction use stock transport.

If all three provisional cuts are taken, South Korea ships two aircraft (`KRF35`,
`KRUAV`), which is below the roadmap's preferred three-to-four. That is a real cost and
is raised in §5.

### 4.3 Counterbattery radar — new mechanic, no precedent

`KRRADAR` proposes a defense that reacts to enemy artillery fire by enabling friendly
counterfire. Nothing in the catalog does this. It needs engine feasibility confirmation
before it is treated as the faction's identity, exactly as the U.S. fires network does
(`faction-spec-usa.md` §8.8).

### 4.4 Tunnels — new mechanic, no precedent

`KPTUNNEL` proposes repositioning infantry between friendly tunnel entrances. The
catalog has no equivalent. `docs/experience-capability-packs.md` lists an existing
**Transit network** capability module, which is the obvious thing to build on rather
than inventing a parallel mechanic. Feasibility and module reuse both need confirmation.

### 4.5 Mobile air defense crowding

`KRSHORAD` would be the seventh mobile air-defense vehicle in the catalog. See
`faction-spec-bundeswehr.md` §4.4 — this needs a programme-level ruling.

### 4.6 Depiction

Both Korean factions describe an active, ongoing, and politically live confrontation.
The roadmap requires that claims about current conflicts do not become faction
stereotypes. For this pair specifically:

- North Korea is designed as an **artillery-and-fortification doctrine**, not as a
  caricature. No unit, description, or voice line should trade on the state's domestic
  conduct.
- Neither faction gets a "fanatic", "human wave", or morale-suicide mechanic.
- Unit descriptions state gameplay function. They do not editorialise.

This needs to be agreed in writing before any text is authored.

---

## 5. Open questions for the product owner

1. **Asymmetric roster sizes:** is North Korea allowed a deliberately smaller and
   cheaper roster (fewer aircraft, weaker navy) as a design statement, or must both
   halves of the pair hit the same roadmap-preferred counts?
2. **South Korean aircraft count:** if `KRAH64` and `KRTRAN` are cut for duplication,
   does the faction ship with two aircraft, or does a distinct third get designed?
3. **Shared airframe policy:** F-35 in three proposed rosters. Ruling required.
4. **Counterbattery mechanic:** engine feasibility confirmation needed before this
   becomes the South Korean identity.
5. **Tunnel mechanic:** build on the existing Transit network capability module, or
   design a faction-local mechanic? (Recommendation: reuse the module.)
6. **Mobile air defense:** programme-level ruling — see `faction-spec-bundeswehr.md` §4.4.
7. **North Korean sourcing budget:** dual-sourcing every North Korean actor under §0.2
   is substantially more research work than any other faction in this programme, and a
   more recent second source than the 2021 DIA assessment still needs to be identified.
   Confirm that this cost is accepted before expansion begins.
8. **Depiction posture:** §4.6 agreed in writing.
9. **Confirmation of §0.1:** the no-nuclear-superweapon rule is restated here as a
   standing constraint. Please confirm rather than leave implied, so it cannot be
   quietly revisited during balance work.

---

## 6. Ready to freeze checklist

- [ ] `docs/faction-spec-usa.md` reviewed and its blocking decisions answered
- [ ] §5.1 through §5.9 ruled
- [ ] §0.1 no-nuclear-superweapon constraint explicitly confirmed
- [ ] §0.2 corroboration policy explicitly confirmed, and a second, more recent North Korean source identified
- [ ] Every North Korean actor dual-sourced, with `claimed, uncorroborated` items clearly marked and excluded from gameplay capability
- [ ] Counterbattery and tunnel mechanics confirmed feasible by gameplay implementation
- [ ] Programme-level rulings made on shared airframes and mobile air defense
- [ ] Both documents expanded to full per-actor contracts matching the U.S. format
- [ ] The pairing table in §1.3 re-checked after expansion so neither side is one-sided
- [ ] Actor-id collision audit re-run at freeze time
- [ ] Faction internal names `northkorea` and `southkorea`, sides, pools, and doctrine strings confirmed unused at freeze time

**Not frozen. Not approved. Outline only. Neither faction may be approved
independently of the other.**
