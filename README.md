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

## Status

Early development. The AI currently observes the colony; autonomous decisions and actions will be added incrementally.
