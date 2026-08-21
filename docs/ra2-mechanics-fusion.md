# RA2 mechanics fusion

This fork vendors the gameplay code from the upstream [OpenRA `ra2` mod](https://github.com/OpenRA/ra2)
as `OpenRA.Mods.RA2` and wires its mechanics into the existing experience
components, which were already shaped to receive them.

Nothing here needs retail Red Alert 2 content. Only the mod's own C# is used; the
RA2 asset-import code is present but not compiled.

## Why the code needed porting

The donor mod targets `release-20231010`. This fork is on net10 bleed, so the
vendored source was adapted:

| Upstream API | Current API |
| --- | --- |
| `[Sync]` | `[VerifySync]` |
| `INotifyHarvesterAction` | `INotifyHarvestAction` |
| `INotifyHarvesterAction.MovingToRefinery` | `INotifyDockClientMoving.MovingToDock` |
| `DeliverResources` activity | `MoveToDock` + `FindAndDeliverResources` |
| `RenderPreviewVoxels(init, ...)` | `RenderPreviewVoxels(IModelCache, init, ...)` |
| `World.ModelCache` | `WorldActor.Trait<IModelCache>()` |
| `IEnumerable<WPos>.PositionClosestTo` | `ClosestToIgnoringPath` |
| `Mobile.Nudge(actor)` | `Nudge` activity |
| `WarheadDebugOverlay.AddImpact(pos, WDist[], ...)` | takes `ImmutableArray<WDist>` |

Two files carried most of the drift and are excluded from compilation in
`OpenRA.Mods.RA2.csproj`: `FileSystem/BagFile.cs` and
`UtilityCommands/ImportRA2MapCommand.cs`. Both only read retail RA2 containers
and maps. Re-enable and port them if RA2 asset import is ever wanted.

## The armament naming constraint

`AttackBase.Armaments` defaults to `["primary", "secondary"]` and
`InitializeGetArmaments` filters an actor's armaments to that list. Both
`MindController` and `CarrierParent` hook `INotifyAttack.Attacking` and gate on
`ArmamentNames.Contains(a.Info.Name)`, so an armament with any other name is
never fired, the trait never sees an attack, and the mechanic is silently dead.

Both were initially wired with descriptive names (`seize`, `drones`). Every trait
resolved correctly under `--resolved-rules` and every lint passed, but neither
mechanic did anything. Both now use `secondary`, leaving each unit's original
weapon on `primary`.

This is only catchable by running the game, which is why the mechanics were
verified in a live headless match rather than by rules resolution alone.

Two deliberate fork changes are marked `// FORK:` in the vendored source:

- `MindController` logs and declines the shot when a target has no
  `MindControllable`, instead of throwing. A rules gap should not end a match in
  a mod with five factions the donor never saw.
- `MindController` capacity follows the `mind-control-and-disguise` component's
  **Control capacity** parameter, falling back to the actor's yaml value. Without
  this the slider would silently stop governing anything.

## What each component gained

| Component | Mechanic | Where it shows up |
| --- | --- | --- |
| `mind-control-and-disguise` | `MindController` / `MindControllable` | Turkey's **TRDRONEOP** seizes enemy vehicles |
| `carrier-and-drone-wing` | `CarrierParent` / `CarrierChild` | China's **CNHAIWANG** launches and recovers real drones |
| `status-and-thermal-system` | `TintedCellsLayer`, `CreateTintedCells` | Atomic superweapon leaves fallout; infantry take damage in it |
| `teleport-network` | `ChronoResourceDelivery` | **HARV** jumps the last leg of a delivery run |
| `salvage-and-scrap-economy` | `SpawnSurvivors` | Destroyed structures eject surviving crew |

### Mind control

Every `^Vehicle` gained `MindControllable`, is tinted while controlled, and
reverts to Creeps if the controlling player is defeated. `TRDRONEOP` fires
`TurkeySeizeLink` through RA2's `ArcLaserZap` from a `seize` armament, shows a
control arc and capacity pips, and moves slower while holding a link.

`turkey-faction` now depends on `mind-control-and-disguise`, so enabling Turkey
enables the mechanic.

### Carrier

`CNHAIWANG`'s drone armament became a dummy launcher (`ChinaHaiwangDroneLaunch`)
so the drones deal the damage rather than the ship double-dipping with a missile.
The armament requires `drones-loaded` and pauses on `launching-drones`.
`CNHAIWANGDRONE` lost `Rearmable`: it rearms at the carrier, not at a helipad.

The older `CarrierWingSpawner` trait in
`OpenRA.Mods.Common/Traits/WorldWarIII/ExperienceGameplaySystems.cs` is now
unreferenced and can be removed.

## A pre-existing bug this surfaced

Wiring Turkey to `mind-control-and-disguise` pulled `targeted-unit-abilities` and
`status-and-thermal-system` into an enabled set for the first time, and exposed
11543 lint warnings that had always been latent: `^ReusableStatusReceiver` grants
`status-heat`, `status-suppressed`, `status-sensor-jammed` and
`status-weapon-locked`, but `^Infantry` never consumed heat, `^Vehicle` never
consumed suppression, and `^Ship`/`^Plane` consumed neither suppression nor
weapon-lock. Each template now consumes every condition it is granted:
suppression and weapon-lock degrade rate of fire, heat degrades speed.

## Seeing it in game

Three of the five mechanics are live under the currently saved component
selection, because `turkey-faction` and `china-faction` pull them in:

- mind control, carrier, and radiation — **on**
- chrono harvester — needs `teleport-network`
- building survivors — needs `salvage-and-scrap-economy`

Enable those two in **Settings > Experience**, or pick the **World War III**
profile, which enables everything.

### Checking each one by hand

**Mind control.** Skirmish as **Turkey**. Build a barracks, then a drone operator
(`TRDRONEOP`, 550, needs a radar dome). Order it to attack an enemy *vehicle* —
vehicles only, infantry and buildings are not valid targets. It will also seize
on its own when an enemy vehicle wanders into range. Expect a cyan arc
from the operator to the vehicle, the vehicle turning your colour with a purple
tint, and capacity pips under the operator when selected. It holds two at once;
taking a third releases the oldest. Kill the operator and the vehicles revert.

**Carrier.** Skirmish as **China**. Build a shipyard plus a tech centre, then the
Haiwang (`CNHAIWANG`, 2800). Order it to attack something within 14 cells: it
launches drones that fly out, strike, and come back to the ship to rearm. The
pips under the ship show how many are docked. The ship itself no longer fires a
missile — the drones are the weapon now.

**Radiation.** Any faction. Fire the atom bomb superweapon. The crater keeps a
green tint that fades over roughly 200 ticks, and infantry that walk through it
take damage. Vehicles are unaffected.

**Chrono harvester** (needs `teleport-network`). Watch any harvester finish a
load. Instead of driving the last stretch back, it warps to the refinery when the
delivery cell is free, with the chrono sound.

**Building survivors** (needs `salvage-and-scrap-economy`). Destroy any structure.
Two riflemen walk out, owned by whoever owned the building.

## Validating

```
dotnet build OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Debug
ENGINE_DIR=.. dotnet bin/OpenRA.Utility.dll ra --check-yaml
ENGINE_DIR=.. dotnet bin/OpenRA.Utility.dll all --check-explicit-interfaces
ENGINE_DIR=.. dotnet bin/OpenRA.Utility.dll all --check-conditional-trait-interface-overrides
```

To confirm a trait actually resolves onto an actor:

```
OPENRA_UTILITY_EXPERIENCE_PROFILE=world-war-iii ENGINE_DIR=.. \
  dotnet bin/OpenRA.Utility.dll ra --resolved-rules TRDRONEOP
```

To run a headless match (the branded launcher needs the companion bypass):

```
OPENRA_AI_COMPANION=1 OPENRA_UTILITY_EXPERIENCE_PROFILE=world-war-iii ENGINE_DIR=.. \
  ./bin/OpenRA-AI.exe 'Engine.LaunchPath=<abs path>\bin\OpenRA-AI.exe' \
  Game.Mod=ra Game.Platform=Null Engine.EngineDir=".." \
  Launch.Map=a-path-beyond.oramap Launch.Bots=Multi0:normal,Multi1:normal
```
