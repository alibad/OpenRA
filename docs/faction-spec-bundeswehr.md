# Faction contract: Germany (Modern) (`bundeswehr`)

Status: **sourced outline; inherited and local rulings applied.**

Held at outline depth by the assignment governing this workstream. The blocking
decisions in `docs/faction-spec-usa.md` have now been ruled, and this document's own
configuration questions are ruled below rather than left open:

**The roster in §2 is intact. Nothing has been removed from it.**

| Item | Effect here |
| --- | --- |
| Local: Leopard configuration — **applied** | **2A7V (fielded)**, consistent with choosing the fielded M1A2 SEP v3 for the U.S. tank. A configuration choice, not a removal |
| Local: artillery — **applied** | **PzH 2000**, not RCH 155 — avoids a third Boxer-hulled vehicle in a six-vehicle roster. A configuration choice between two candidates for one slot; the slot stays |
| Local: display language — **applied** | German proper nouns with an English role gloss, matching Turkey's shipped practice |
| `docs/faction-spec-usa.md` §8.5 naming policy — **applied** | Generic display names for buildings and defenses; real designations for vehicles, aircraft, and ships |
| §8.9 shared airframes — **settled: grandfather + role-only** | **`BWTRAN` keeps its slot.** No cap. It must occupy a role distinct from stock `TRAN` and ship its own sprite set — see §4.3, where the tandem-rotor legibility problem is a concept-board judgement, not a reason to drop it |
| §8.10 mobile air defense — **settled: mechanics-only** | `BWSHORAD` keeps its slot, as it would have under either option. It needs a mechanic no other faction holds; radar-directed gun-only is unoccupied and is the natural fit |

Sources accessed **2026-08-16**. Re-check at freeze time.

---

## 0. Hard constraint: classic Germany is untouchable

The existing selectable `germany` faction is the classic Red Alert
Chronoshift/Chrono Tank subfaction, declared at `mods/ra/rules/world.yaml:112` with
`InternalName: germany` and membership in `RandomAllies` via
`RandomFactionMembers: england, france, germany`.

This faction pack:

- uses the internal id **`bundeswehr`** and the display name **Germany (Modern)**;
- must not edit, rename, reweight, or remove the `germany` faction entry;
- must not alter `RandomAllies` by replacement — it appends via
  `RandomFactionMemberOf: RandomAllies` as required by
  `docs/experience-capability-packs.md`;
- must not touch `CTNK`, `STNK`, `PDOX`, or any Chronoshift actor or rule.

Verified: `bundeswehr` is unused as a faction internal name anywhere under `mods/`.

A player must be able to select classic Germany and Germany (Modern) in the same lobby
and get two completely different games. If that is not true at acceptance, the faction
is not shippable.

---

## 1. Doctrine sketch

**Protected mechanized combined arms.** High unit quality and high unit cost. Strong
protected infantry-plus-vehicle teams, precise artillery, and the best dedicated
short-range air defense in the catalog. Slow to mass, expensive to replace, punishing
if allowed to set up.

Provisional tradeoffs:

- Highest per-unit cost band in the catalog; the smallest army at any given economy.
- Excellent at holding and advancing a line, poor at raiding and map control.
- High repair burden — the faction should want a repair/engineering vehicle in every
  push, which is a real logistical tax rather than a stat.
- Weak once its air defense umbrella is broken.

Provisional side and pool: `Side: Allies`, `RandomPool: RandomAllies`,
`Doctrine: protected-mechanized` (unused string — verified).

---

## 2. Proposed roster and actor ids

All ids checked with a whole-word recursive search across `*.yaml`, `*.lua`, and `*.cs`
at tree state `a0f751b972`. **Zero collisions.** The `BW` prefix is unused.

| Domain | Actor id | Slot | Candidate basis |
| --- | --- | --- | --- |
| Infantry | `BWGREN` | Line infantry | Panzergrenadier |
| Infantry | `BWMELLS` | Anti-armor team | MELLS guided missile team |
| Infantry | `BWDRONE` | Recon / EW operator | Small UAS and EW operator |
| Infantry | `IRONBRAND` | Commando (build limit 1) | Original project character, call-sign convention |
| Vehicles | `BWMBT` | Main battle tank | **Leopard 2A7V** (ruled, fielded configuration) |
| Vehicles | `BWIFV` | Tracked IFV | Puma |
| Vehicles | `BWWHEEL` | Wheeled carrier | GTK Boxer — subject to the §4.5 wheeled-carrier allocation |
| Vehicles | `BWSPH` | Self-propelled artillery | **PzH 2000** (ruled, tracked) |
| Vehicles | `BWSHORAD` | Air-defense vehicle | Skyranger 30 on a Boxer carrier |
| Vehicles | `BWENG` | Engineering / recovery | Armoured engineering vehicle |
| Aircraft | `BWEF` | Multirole fighter | Eurofighter |
| Aircraft | `BWHELI` | Light utility / armed helicopter | H145M |
| Aircraft | `BWUAV` | Reconnaissance UAS | Reconnaissance drone |
| Aircraft | `BWTRAN` | Heavy transport helicopter | CH-47F — kept; needs a role distinct from stock `TRAN`, see §4.3 |
| Navy | `BWFRIG` | Frigate | F125 or F126-class |
| Navy | `BWSUB` | Submarine | Type 212A |
| Navy | `BWCORV` | Corvette | Braunschweig-class |
| Buildings | `FACT`, `WEAP`, `TENT`, `HPAD`, `SYRD` | Stock production | — |
| Defenses | `BWSAM` | Air-defense battery | IRIS-T-style, abstracted |
| Defenses | `BWRADAR` | Sensor emplacement | Radar / sensor node |
| Defenses | `BWBUNKER` | Protected anti-armor position | Hardened fighting position |

Reserved husk and sink ids: `BWMBT.Husk`, `BWIFV.Husk`, `BWWHEEL.Husk`, `BWSPH.Husk`,
`BWSHORAD.Husk`, `BWENG.Husk`, `BWEF.Husk`, `BWHELI.Husk`, `BWUAV.Husk`, `BWTRAN.Husk`,
`BWFRIG.Sink`, `BWSUB.Sink`, `BWCORV.Sink`.

Roster shape: 4 infantry, 6 vehicles, 4 aircraft, 3 naval. Two actors carry open flags
rather than cuts: `BWTRAN` (§4.3) and `BWWHEEL` (§4.5).

---

## 3. Source anchors

| Topic | Source | Accessed |
| --- | --- | --- |
| Leopard 2 | [Ausrüstung und Technik: Leopard 2, Bundeswehr](https://www.bundeswehr.de/de/ausruestung-technik-bundeswehr/landsysteme-bundeswehr/leopard-2) | 2026-08-16 |
| Puma | [Ausrüstung und Technik: Schützenpanzer Puma, Bundeswehr](https://www.bundeswehr.de/de/ausruestung-technik-bundeswehr/landsysteme-bundeswehr/schuetzenpanzer-puma) | 2026-08-16 |
| GTK Boxer | [Ausrüstung und Technik: GTK Boxer, Bundeswehr](https://www.bundeswehr.de/de/ausruestung-technik-bundeswehr/landsysteme-bundeswehr/gtk-boxer) | 2026-08-16 |
| PzH 2000 | [Ausrüstung und Technik: Die Panzerhaubitze 2000, Bundeswehr](https://www.bundeswehr.de/de/ausruestung-technik-bundeswehr/landsysteme-bundeswehr/panzerhaubitze-2000) | 2026-08-16 |
| Skyranger 30 | [Skyranger 30: Bundeswehr erhält 19 neue Flugabwehrpanzer](https://www.bundeswehr.de/de/meldungen/skyranger-30-bundeswehr-erhaelt-19-neue-flugabwehrpanzer) | 2026-08-16 |
| Army air-defence reorganisation | [Bundeswehr stellt Heeresflugabwehrtruppe neu auf](https://www.bundeswehr.de/de/organisation/heer/aktuelles/heeresflugabwehrtruppe-wird-neu-aufgestellt-5777698) | 2026-08-16 |
| Eurofighter | [Ausrüstung und Technik: Der Kampfjet Eurofighter, Bundeswehr](https://www.bundeswehr.de/de/ausruestung-technik-bundeswehr/luftsysteme-bundeswehr/eurofighter) | 2026-08-16 |
| Land systems index | [Ausrüstung und Technik: Landsysteme der Bundeswehr](https://www.bundeswehr.de/de/ausruestung-technik-bundeswehr/landsysteme-bundeswehr) | 2026-08-16 |
| Procurement process | [Beschaffung und Planungsprozess, Bundeswehr](https://www.bundeswehr.de/de/beschaffung/beschaffung-planungsprozess) | 2026-08-16 |

**Link-check result, 2026-08-16:** all nine `bundeswehr.de` links returned HTTP 200.
This is the only faction in the programme whose entire source set was directly
retrievable. See `docs/faction-spec-usa.md` §9.1 for the method.

Initial status readings from the sources above, to be tightened at expansion:

- **Skyranger 30:** ordered, not yet a mature in-service fleet. The Bundeswehr page
  describes an initial order of 19 vehicles on a Boxer carrier for €650 million. This is
  **procurement**, and the contract must label it as such rather than as inventory.
- **Eurofighter:** fielded. The Bundeswehr page describes 138 aircraft forming the
  backbone of the Luftwaffe fighter fleet.
- **Boxer, PzH 2000, Puma, Leopard 2:** fielded families; specific configuration status
  (2A7V versus A8, Puma construction lots) still needs its own citation per actor.

Still to source before expansion: the RCH 155 status if chosen for `BWSPH`, the
engineering/recovery vehicle, the reconnaissance UAS, H145M, F125/F126, Type 212A, and
Braunschweig-class. Each needs an official Bundeswehr or manufacturer citation with a
fielded/ordered/demonstrated status.

---

## 4. Rulings and remaining overlap hot spots

### 4.1 Leopard configuration — **RULED: 2A7V, fielded**

The fielded reference point rather than the A8 procurement configuration. This is
consistent with the U.S. contract choosing the fielded M1A2 SEP v3 over the M1E3
prototype, and it keeps the whole programme present-day rather than mixing fielded and
future equipment with no principled line between them.

Overlap: this is the catalog's fifth or sixth main battle tank (`M1A2S`, `BOZKIR`,
`CNQILIN`, `IRKARR`, plus `USMBT` and `ILMBT`). The Leopard 2's boxy turret with a
stepped front and a long thin barrel is a genuinely different silhouette from the Abrams
family, which is why this one is a **Watch** rather than an **Escalate** — but the
concept board must still prove it.

### 4.2 PzH 2000 versus RCH 155 — **RULED: PzH 2000**

A heavy tracked howitzer, not a wheeled turreted gun on a Boxer chassis. `BWWHEEL` and
`BWSHORAD` already share the Boxer hull; a third Boxer-hulled vehicle in a six-vehicle
roster would make half the faction one silhouette. The tracked/wheeled split is the
cheapest possible way to keep the roster readable.

### 4.3 `BWTRAN` — **SETTLED: stays, with a distinct role required**

Stock Allies `TRAN` is a tandem-rotor Chinook, so `BWTRAN` is a second Chinook
silhouette. Under the §8.9 role-only rule that is allowed, and German CH-47F procurement
is real and recent, so a faction-specific heavy lift is a legitimate identity element.

Two conditions attach:

- **Distinct tactical role.** It cannot be stock `TRAN` in different colours. Candidate
  directions that no transport in the catalog currently occupies: forward rearming of
  `BWSHORAD` and `BWSAM` magazines in the field, or lifting a vehicle rather than only
  infantry. Choose one at expansion.
- **Own sprite set**, judged on the concept board.

Stated plainly: separating two tandem-rotor transports at native scale is the hardest art
problem in this document. If the concept board cannot do it, the honest fix is to revise
the design — a visibly different loadout, a slung load, a different rotor-to-fuselage
proportion — or to bring the removal question back as a fresh decision. It is not a
reason to drop the actor now.

### 4.4 Mobile air defense — **`BWSHORAD` keeps its slot either way**

Under the §8.10 proposal a faction gets a mobile air-defense vehicle only if air defense
is central to its doctrine and the engagement niche is free. Both hold here: air defense
is named in this faction's doctrine, and the **radar-directed gun-only** niche is
unoccupied — the four shipped systems (`GOKKALKAN`, `SADS`, `CNMANTIS`, `IRRAAD`) are all
missile-only. So `BWSHORAD` survives whether §8.10 is approved or not, and this section
needs no decision from you.

Skyranger 30's distinguishing read is the large rotating gun turret with a prominent
radar panel on a wheeled hull. **Status discipline:** the Bundeswehr source describes an
initial order of 19 vehicles for €650 million. That is **procurement**, not a mature
in-service fleet, and every mention of this actor must say so.

### 4.5 Wheeled carrier crowding — **DEFERRED, with the principle ruled and a claim recorded**

`USICV` has been cut (§8.3), so the live question is Germany's `BWWHEEL` (Boxer) versus
Israel's `ILWHEEL` (Eitan) versus the shipped `ARAS8`.

**Principle ruled:** the catalog holds at most **two** eight-wheeled carrier silhouettes.
`ARAS8` holds one. The second goes to whichever of the Germany or Israel contracts is
expanded first; the other must differentiate by turret and unit class or drop the slot.

**This faction has the stronger claim**, and the reason is recorded here so the argument
does not have to be reconstructed: `BWWHEEL` and `BWSHORAD` share the Boxer hull and earn
a deliberate two-actor family read. That is exactly the justification `USICV` lost the
moment it stood alone in its roster. Germany losing Boxer would also orphan the Skyranger
carrier, which Israel losing Eitan would not do — Israel keeps `ILAPC` either way.

Allocation is nonetheless **deferred** rather than self-awarded, because neither faction
has a per-actor contract yet and ruling it now would be deciding without the evidence.

### 4.6 Recon / EW infantry crowding — **RULED: the area niche is taken**

§8.6 keeps `USJTAC` as the only observer projecting an **area** condition consumed by a
restricted weapon list. `BWDRONE` must therefore be a point-marker, a scout, or an EW
disruptor. The specific choice is part of this document's expansion.

---

## 5. Remaining open questions

Six of the seven questions previously listed here have been ruled. What remains:

1. **Lobby clarity.** How do "Germany" and "Germany (Modern)" read side by side in the
   faction picker without confusing a player who just wants Chrono Tanks? Proposed, for
   confirmation: keep the display names as the roadmap specifies and give each a
   description line that names its era and signature capability — classic Germany's
   Chronoshift, modern Germany's protected mechanized combined arms. This is a text
   decision, not a design one, and it should be checked live in the lobby at gate 6.
2. **Wheeled carrier allocation** (§4.5) — deferred by design until either this document
   or the Israel document is expanded.
3. **Depiction posture.** The same editorial guideline proposed in
   `docs/faction-spec-israel.md` §5.1 should be confirmed once and applied to every pack
   in this programme rather than agreed per faction.

---

## 6. Ready to freeze checklist

Done:

- [x] Applied ruling from `docs/faction-spec-usa.md` §8.5 — removes nothing
- [x] Leopard configuration ruled — 2A7V, fielded
- [x] Artillery ruled — PzH 2000
- [x] Display language ruled — German proper nouns with an English role gloss
- [x] Mobile air defense — `BWSHORAD` keeps its slot under either outcome of §8.10
- [x] Actor-id collision audit run — zero collisions, `BW` prefix unused
- [x] Source anchors gathered and link-checked — all nine returned HTTP 200
- [x] Full roster preserved — nothing cut
- [x] §8.9 settled — `BWTRAN` keeps its slot, needs a distinct role and its own sprite set
- [x] §8.10 settled — `BWSHORAD` keeps its slot

Remaining:

- [ ] `BWTRAN`'s distinct role chosen (§4.3)
- [ ] §5.1 lobby clarity text agreed
- [ ] §5.2 wheeled carrier allocation settled between this document and the Israel one
- [ ] §5.3 depiction posture agreed once, programme-wide
- [ ] Classic `germany` faction verified untouched, including `RandomAllies` membership
- [ ] Lobby side-by-side check performed with both German factions selectable
- [ ] This document expanded to a full per-actor contract matching the U.S. format
- [ ] Every actor given an official source with fielded/ordered/demonstrated/conceptual status and an access date
- [ ] Skyranger 30 explicitly labelled procurement, not inventory, wherever it appears
- [ ] Actor-id collision audit re-run at freeze time
- [ ] Faction internal name `bundeswehr`, side, pool, and doctrine string confirmed unused at freeze time

**Not frozen. Outline depth by design — but no longer blocked on cross-faction rulings.**
