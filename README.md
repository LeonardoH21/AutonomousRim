# AutonomousRim

Autonomous colony-management and tactical AI mod for **RimWorld 1.6**.

The long-term goal is a full autonomous player capable of perceiving the map, evaluating pawns and equipment, planning the colony, reacting to threats, fighting raids and preparing for long-term risks such as seasons and resource shortages.

## Current milestone

**Milestone 0 / 1 — Foundation & Perception**

- RimWorld 1.6 mod metadata
- C# project targeting .NET Framework 4.7.2
- Harmony bootstrap
- Map-level AI component
- Initial colony-state scanner
- In-game `Rim AI` inspector with pawn skills, health, traits, weapons, range and apparel
- Combat readiness filtering and initial weapon-aware skill scoring
- Stored-food nutrition and baseline reserve estimate
- Projectile burst DPS, armor penetration and per-garment armor ratings
- Advisory weapon recommendations from accessible stored equipment
- Hostile composition, heuristic risk and presence transitions checked every 120 ticks
- Structured project layout for future AI systems

## Planned architecture

```text
Core/
  AutonomousRimMod
  AutonomousRimMapComponent
  ColonyState

Perception/
  ColonyStateScanner
  PawnAnalyzer
  EquipmentAnalyzer
  ThreatScanner
  MapAnalyzer

Planning/
  StrategicPlanner
  BasePlanner
  SeasonPlanner

Combat/
  CombatDirector
  TacticalAnalyzer
  CoverAnalyzer
  ThreatMap

Execution/
  JobExecutor
  ConstructionExecutor
  CombatExecutor

UI/
  AIInspector
  DecisionLog
```

## Development setup

1. Install RimWorld 1.6 and the Harmony mod.
2. Install a recent .NET SDK/Visual Studio toolchain capable of building `net472`.
3. Set `RimWorldDir` to your RimWorld installation folder or pass it during build:

```powershell
dotnet build Source/AutonomousRim/AutonomousRim.csproj -p:RimWorldDir="C:\Program Files (x86)\Steam\steamapps\common\RimWorld"
```

The compiled DLL is copied to `1.6/Assemblies/`.

The build downloads .NET Framework reference assemblies automatically. Harmony is detected in either `Mods/Harmony/Current/Assemblies` or `Mods/2009463077/Current/Assemblies`. For another location, pass `-p:HarmonyDir="..."`.

Reusable PowerShell commands:

```powershell
.\scripts\Build.ps1 -RimWorldDir "C:\Path\To\RimWorld"
.\scripts\Install.ps1 -RimWorldDir "C:\Path\To\RimWorld"
.\scripts\SmokeTest.ps1 -RimWorldDir "C:\Path\To\RimWorld"
```

Close RimWorld before installation. The installer copies only the mod files and verifies the installed DLL hash. A local SDK in `.tools/dotnet` is used when available; otherwise the build uses `dotnet` on PATH.

The smoke test starts a temporary colony using the game's `-quicktest` option, under a separate `.tools/perception-test` save-data folder. It enables only Harmony, Core, installed DLCs and AutonomousRim; waits for two successful scanner cycles; and closes its own game process. It does not use your normal saves or mod configuration. Inspect its log for failures. This test does not validate the inspector's visual layout or combat behavior.

## First in-game test

1. Enable Harmony and AutonomousRim in the Mods menu and restart the game.
2. Open a colony, then click **Rim AI** on the bottom bar.
3. Check the observed colonists, resources and equipment. The snapshot refreshes every 600 game ticks (10 seconds at normal speed); pausing also pauses refreshes.
4. Enable developer mode to see `[AutonomousRim] Scan:` entries in `Player.log`.

The current version observes only. It does not draft colonists, change equipment or issue construction orders. Food reserve uses 1.6 nutrition per colonist per day and excludes genes, animals, guests and spoilage; it is an approximate diagnostic. Combat values are provisional heuristics, not predictions of raid outcomes. Hostile count excludes dead and downed pawns. The threat scanner classifies pawn composition, not raid incidents, and does not yet model turrets, traps, cover or special abilities. Threats refresh every 120 ticks; allied profiles refresh every 600 ticks.

Weapon recommendations compare theoretical sustained damage, short-distance accuracy, the pawn's skill/accuracy, range and penetration. They use a 12-cell reference for ranged attacks and require a score improvement above 15%. Only stored, allowed weapons the pawn can equip, reserve and reach are considered; reachability checks are limited to the strongest five improvements. Explosives and nonstandard attack verbs are excluded. Recommendations are independent for each pawn, so multiple colonists can be offered the same weapon. They do not reserve or equip items, model a specific target, or account for ammunition systems from other mods. Armor is listed per garment rather than summed across body coverage.

## Calculation checks

```powershell
dotnet run --project Tests/AutonomousRim.Checks
```

These checks cover burst-cycle damage, invalid inputs and threat-risk boundaries. A separate isolated RimWorld quicktest has confirmed repeated colony scans with Harmony and all DLCs. Inspector rendering, recommendation quality and live hostile transitions still require in-game validation.

## Status

Early development. The AI currently observes the colony; autonomous decisions and actions will be added incrementally.
