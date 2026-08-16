# Faction contract: Germany (Modern) (`bundeswehr`)

Status: **sourced outline only.**

Held at outline depth by the assignment governing this workstream. Expands to a full
per-actor contract only after `docs/faction-spec-usa.md` has been reviewed and its
blocking decisions answered.

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
| Vehicles | `BWMBT` | Main battle tank | Leopard 2A7V or A8 — configuration decision required, see §4.1 |
| Vehicles | `BWIFV` | Tracked IFV | Puma |
| Vehicles | `BWWHEEL` | Wheeled carrier | GTK Boxer |
| Vehicles | `BWSPH` | Self-propelled artillery | PzH 2000, or the wheeled RCH 155 — see §4.2 |
| Vehicles | `BWSHORAD` | Air-defense vehicle | Skyranger 30 on a Boxer carrier |
| Vehicles | `BWENG` | Engineering / recovery | Armoured engineering vehicle |
| Aircraft | `BWEF` | Multirole fighter | Eurofighter |
| Aircraft | `BWHELI` | Light utility / armed helicopter | H145M |
| Aircraft | `BWUAV` | Reconnaissance UAS | Reconnaissance drone |
| Aircraft | `BWTRAN` | Heavy transport helicopter | CH-47F — **likely cut, see §4.3** |
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

Roster shape: 4 infantry, 6 vehicles, 3–4 aircraft, 3 naval.

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

## 4. Known overlap hot spots and configuration decisions

### 4.1 Leopard 2 configuration

`BWMBT` must pick one configuration and label it honestly. 2A7V is the fielded reference
point; A8 is procurement. **Provisional recommendation: 2A7V**, consistent with the
present-day framing of every other shipped pack, and consistent with the recommendation
to use the fielded M1A2 SEP v3 for the U.S. tank.

Overlap: this is the catalog's fifth or sixth main battle tank (`M1A2S`, `BOZKIR`,
`CNQILIN`, `IRKARR`, proposed `USMBT`, proposed `ILMBT`). The Leopard 2's boxy turret
with a distinctly stepped front and a long thin barrel is a genuinely different
silhouette from the Abrams family, which helps — but the concept board must prove it.

### 4.2 PzH 2000 versus RCH 155

Two different vehicles with two different silhouettes: a heavy tracked howitzer versus a
wheeled turreted gun on a Boxer chassis. **Provisional recommendation: PzH 2000**, for
the tracked/wheeled silhouette split against `BWWHEEL` and `BWSHORAD`, which already
share the Boxer hull. Three Boxer-hulled vehicles in a six-vehicle roster is too many.

### 4.3 `BWTRAN` — likely cut

Stock Allies `TRAN` is a tandem-rotor Chinook. A Bundeswehr CH-47F would be a direct
silhouette duplicate. **Provisional recommendation: cut `BWTRAN`**, ship three aircraft,
and let the faction use stock `TRAN` if a transport is needed. This mirrors the U.S.
contract's reasoning for dropping the Apache.

### 4.4 Air defense crowding

`BWSHORAD` joins `GOKKALKAN`, `SADS`, `CNMANTIS`, `IRRAAD`, and the proposed `USSHORAD`
as the sixth mobile air-defense vehicle. This is the single most crowded slot in the
whole modern-faction programme. Either each one gets a genuinely distinct mechanic and
silhouette, or the programme should accept that some factions do not get a mobile AA
vehicle. **This needs a programme-level ruling, not six independent ones.**

Skyranger 30's distinguishing read is the large rotating gun turret with a prominent
radar panel on a wheeled Boxer hull — a gun-first system, where `USSHORAD` is
gun-and-missile and `CNMANTIS` is missile-first. That is a workable split if it is
enforced deliberately across all six.

### 4.5 Wheeled carrier crowding

`BWWHEEL` (Boxer) joins `ARAS8`, proposed `USICV`, and proposed `ILWHEEL`. See
`faction-spec-usa.md` §8.3.

### 4.6 Recon / EW infantry crowding

`BWDRONE` would be the eighth observer-or-controller infantry. See
`faction-spec-usa.md` §8.6.

---

## 5. Open questions for the product owner

1. **Leopard configuration:** 2A7V (fielded) or A8 (procurement)?
2. **Artillery:** PzH 2000 or RCH 155? (Recommendation: PzH 2000.)
3. **Transport helicopter:** cut `BWTRAN`, or accept a second Chinook silhouette?
   (Recommendation: cut.)
4. **Mobile air defense:** does every modern faction get one? If yes, what is the
   programme-level split of mechanics that keeps six of them distinct?
5. **Boxer hull reuse:** is a shared hull across `BWWHEEL` and `BWSHORAD` acceptable as
   a deliberate family read, as proposed for `USICV`/`USSHORAD`?
6. **Localisation:** German-language unit names in display text, or English throughout?
   The shipped packs are inconsistent — Turkey uses Turkish proper nouns
   (`Bozkir`, `Gokkalkan`), China uses English glosses (`Qilin Main Battle Tank`).
   A ruling is needed before any text is written.
7. **Lobby clarity:** how do "Germany" and "Germany (Modern)" appear side by side in the
   faction picker without confusing a player who just wants Chrono Tanks?

---

## 6. Ready to freeze checklist

- [ ] `docs/faction-spec-usa.md` reviewed and its blocking decisions answered
- [ ] §5.1 through §5.7 ruled
- [ ] Programme-level mobile air-defense ruling made (§4.4)
- [ ] Classic `germany` faction verified untouched, including `RandomAllies` membership
- [ ] Lobby side-by-side check performed with both German factions selectable
- [ ] This document expanded to a full per-actor contract matching the U.S. format
- [ ] Every actor given an official source with fielded/ordered/demonstrated/conceptual status and an access date
- [ ] Skyranger 30 explicitly labelled procurement, not inventory, wherever it appears
- [ ] Actor-id collision audit re-run at freeze time
- [ ] Faction internal name `bundeswehr`, side, pool, and doctrine string confirmed unused at freeze time

**Not frozen. Not approved. Outline only.**
