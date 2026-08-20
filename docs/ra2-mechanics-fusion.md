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
