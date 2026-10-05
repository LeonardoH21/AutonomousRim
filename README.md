# AutonomousRim

Autonomous colony-management and tactical AI mod for **RimWorld 1.6**.

The long-term goal is a full autonomous player capable of perceiving the map, evaluating pawns and equipment, planning the colony, reacting to threats, fighting raids and preparing for long-term risks such as seasons and resource shortages.

## Current milestone

**Foundation, perception and initial colony management**

- RimWorld 1.6 mod metadata
- C# project targeting .NET Framework 4.7.2
- Harmony bootstrap
- Map-level AI component
- Initial colony-state scanner
- In-game `Rim AI` inspector with pawn skills, health, traits, weapons, range and apparel
- Combat readiness filtering and initial weapon-aware skill scoring
- Stored-food nutrition, food-policy filtering and reserve estimate using actual colonist hunger rates
- Projectile burst DPS, armor penetration and per-garment armor ratings
- Identification of visible weapons/apparel on the ground and on corpses, including forbidden loot
- Weapon/apparel recommendations and optional automatic native equip/wear jobs
- Trait-aware weapon roles, effective aiming/accuracy and audited active trait modifiers
- Optional automatic hunting, butchering/meal bills and skill/passion-based work priorities
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

Observation runs by default. Enable food automation and/or work management in **Rim AI** for each map; these settings persist in saves. Food management maintains a three-day simple-meal target when an appropriate colony cooking station exists, and adds a butchering order when a suitable station exists. Existing player bills are respected. AI bills exclude humanlike corpses/meat. Bill counts follow colonist demand while the AI bill remains unedited. When reserves are low, it designates up to two pending hunts for accessible wild, non-predatory animals with zero species retaliation chance, within 60 cells of an equipped hunter. It requires a butchering station and an enabled, capable ranged hunter. Hostile presence stops new hunting and removes AI hunting designations; native jobs already running remain under RimWorld's job system. Direct combat control is not implemented.

Work management enables numerical priorities, prioritizes emergency tasks and medicine, selects specialists by skill/passion/health, and penalizes accumulating several specialties on one pawn. Cooking/growing gain urgency when food is low. Disabled work, drafted/downed pawns and mental states are respected. Manual priority changes are retained; disabling automation restores untouched AI priority changes. The global numerical-priority mode remains enabled unless the player turns it off; turning it off pauses automatic updates. Disabling food automation removes AI hunting designations and unedited AI production bills, preserving existing player bills.

Food reserves use stored, permitted, unspoiled food accepted by current colonist diets and food policies, and colonists' fed-state hunger rates. The estimate excludes guests, animals, physical food reachability and future spoilage; differing diets can still make shared-stock allocation approximate. Combat values remain provisional heuristics. Hostile count excludes dead/downed pawns; turrets, traps and special abilities are not modeled. Threats refresh every 120 ticks; profiles and management refresh every 600 ticks.

Equipment automation is optional per map, with a separate opt-out for each pawn. It issues at most two native pickup/wear jobs per scan and waits 1,800 ticks (30 seconds at normal speed) between changes on one pawn. Improvements must exceed 20% plus 0.25 score to reduce repeated swapping. Jobs pause under hostile presence and respect player orders, queued jobs, urgent food/rest needs, medical tasks, apparel policies and player-forced/locked clothing. Forbidden, tainted, foreign-biocoded and persona gear is excluded. Conflicting shields and ranged weapons are rejected. AI-selected clothing is marked as AI-owned forced apparel to prevent the native optimizer immediately undoing its choice; disabling automation releases only those markers and leaves equipment on the pawn.

Weapon scores use the game's current aiming delay, shooting accuracy, melee hit/damage/cooldown and a 12-cell accuracy reference. Brawlers favor melee; other colonists can prefer melee when their skills strongly support it. Apparel scores consider covered body parts, armor, current outdoor temperature, condition and movement penalties. Nudists receive a clothing penalty in temperate conditions. The inspector exposes trait modifiers, suppressed traits and current derived stats; numerical trait effects are read through game stats, without applying them twice. This does not model every trait's mood or strategic behavior, nor a specific enemy, future season, ideology/royalty preference or modded ammunition system.

Accessible allowed ground gear is considered, including items outside storage; reachability checks are limited to the five strongest weapon and apparel candidates. Recommendations may suggest the same item to several pawns, but execution gives each item to at most one pawn per scan and respects reservations. The loot inventory also identifies forbidden gear and corpse apparel without unforbidding or stripping it. Explosives and nonstandard attack verbs remain excluded. Loot is paginated in the inspector.

## Calculation checks

```powershell
dotnet run --project Tests/AutonomousRim.Checks
```

The 35 checks cover burst-cycle damage, invalid inputs, threat-risk boundaries, hunting/work policies, weapon role preferences, upgrade thresholds and aiming-delay effects. A separate isolated RimWorld quicktest has confirmed repeated colony scans with Harmony and all DLCs. Inspector rendering, recommendation quality and live hostile transitions still require in-game validation.

Runtime checks also exercise forbidden ground gear, corpse clothing, actual food demand, production bills, hunting, work priorities, real weapon pickup/armor wear, trait stat changes, policy/forced clothing protection, foreign biocoding, shield incompatibility, order cooldown and ownership cleanup. To run them, build the add-on and request it in the smoke test:

```powershell
dotnet build Tests/AutonomousRim.RuntimeChecks -c Release -p:RimWorldDir="C:\Path\To\RimWorld"
.\scripts\SmokeTest.ps1 -RimWorldDir "C:\Path\To\RimWorld" -RuntimeChecks
```

The test add-on is installed temporarily and removed when the smoke test ends. It mutates only the generated test colony. Long-running food sufficiency, hostile interruptions, save/load round trips and the inspector's visual layout still need broader validation.

## Status

Early development. Optional food/work/equipment management is available; combat control and base construction remain on the roadmap.
