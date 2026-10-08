# AutonomousRim

Autonomous colony-management and tactical AI mod for **RimWorld 1.6**.

The long-term goal is a full autonomous player capable of perceiving the map, evaluating pawns and equipment, planning the colony, reacting to threats, fighting raids and preparing for long-term risks such as seasons and resource shortages.

## Current milestone

**Foundation, perception and long-term colony management**

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
- Dynamic cooking targets and food reserve classes based on colonist consumption, seasonal/climate risk, ingredients, perishability and freezer capacity
- Dynamic Work Priorities using skills, passions, role fit, urgency, pending workload, health, mood, rest, food and resource reserves; manual edits are preserved
- Dynamic colonist schedule using Sleep, Work, Recreation, Anything and Meditation when available, with emergency and post-emergency recovery modes
- Strategic research route with real prerequisite and hidden-prerequisite ordering, unlock auditing, short/medium/long-term goals and a saved victory route
- Adaptive 500-day strategic horizon with stability scoring, current/next focus, resource/risk context and a no-rush gate for long-term/victory research
- Mountain-site assessment that records thick-roof/open-space/entrance evidence and keeps major excavation locked until escape routes, chokepoints, temperature and infestation risks are validated
- Failure analysis with recoverable-risk detection, categorized causal reports, expected/actual/improvement comparisons, learned adjustments and persisted raid checkpoints
- Additive incorporation of researched benches/equipment into the approved base after geometry validation
- Landing-area gradual Allow with a per-cycle cap and expanding radius; manual re-forbids remain protected
- New-save modular 13×13 planner with shared partitions, 5×5 bedrooms, 11×5 merged rooms, separate kitchen/butchery, wooden floors and three-cell circulation; existing courtyard/ring plans remain compatible
- HUD buttons for base, equipment, food, work priorities, schedule, strategic planning, failure diagnosis, ground-item allow, plan preview and disable-all
- Hostile composition and emergency response checked every 15 ticks, independent of slower colony planning
- Structured project layout for future AI systems

New colonies start with base construction, strategic planning, schedule and landing-area Allow enabled; explicit saved toggles and invested layouts remain respected. The current construction revision is documented in [REVISAO-MODULAR-13.md](docs/REVISAO-MODULAR-13.md), including native trial results (five furnished bedrooms in 10.51 game hours; initial core with working freezer in 61.37 hours) and remaining scope. Older ring and courtyard design documents describe preserved legacy layouts.

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
3. Check the observed colonists, resources and equipment. The snapshot refreshes every 360 game ticks (6 seconds at normal speed); pausing also pauses refreshes.
4. Enable developer mode to see `[AutonomousRim] Scan:` entries in `Player.log`.

Observation runs by default. Use the fixed controls in **Rim AI** to enable food, work, equipment and/or initial base construction for each map; these settings persist in saves. Food management calculates a seasonal/climate-aware target in survival days, classifies the reserve as critical, low, normal, high or excessive, and changes Cooking bills with colonist demand, consumption, ingredients, spoilage risk and available cooks. It prefers Simple, Fine or Lavish meals according to scarcity and ingredients, and creates a separate Pemmican/Packaged Survival Meal reserve when there is room for a long-life stock. Existing player bills are respected. AI bills exclude humanlike corpses/meat. When reserves are low, it designates up to two pending hunts for accessible wild, non-predatory animals with zero species retaliation chance, within 60 cells of an equipped hunter. It requires a butchering station and an enabled, capable ranged hunter. Hostile presence stops new hunting and removes AI hunting designations; native jobs already running remain under RimWorld's job system. Direct combat control is not implemented.

Work management enables numerical priorities and selects specialists by skill, passion, effective work speed, health, needs and existing role load. Food reserves, medical needs, pending crops, construction, resource designations and active production bills change assignments; exhausted or unwell workers retain self-care and leave productive roles. The schedule protects immediate needs, respects night owls, assigns meditation to psycasters and holds recovery until needs improve. Manual edits, including restoring an original hour, are preserved across save/load. Disabling automation restores untouched AI changes. The global numerical-priority mode remains enabled unless the player turns it off; turning it off pauses automatic work updates. See [work and schedule behavior and native checks](docs/TRABALHO-AGENDA.md). Disabling food automation removes AI hunting designations and unedited AI production bills, preserving existing player bills.

Food reserves use permitted, unspoiled food accepted by current colonist diets and food policies, including stored and accessible ground food, and colonists' fed-state hunger rates. The scanner separates perishables, long-life food, near-spoilage nutrition, meal stacks and cold-storage cells. Freezers are kept above Normal priority and become Critical when nearly full; general AI stockpiles exclude food. When freezer capacity is tight, the planner queues researched shelves in the AI freezer and pauses excessive primary cooking. Guests, animals, future spoilage beyond the current rot stage and mod-specific diet interactions remain approximate. Combat values remain provisional heuristics. Hostile count excludes dead/downed pawns; turrets, traps and special abilities are not modeled. Threats refresh every 120 ticks; profiles and management refresh every 360 ticks; construction executes every 120 ticks.

Equipment automation is optional per map, with a separate opt-out for each pawn. It issues at most two native pickup/wear jobs per scan and waits 1,800 ticks (30 seconds at normal speed) between changes on one pawn. Improvements must exceed 20% plus 0.25 score to reduce repeated swapping. Jobs pause under hostile presence and respect player orders, queued jobs, urgent food/rest needs, medical tasks, apparel policies and player-forced/locked clothing. Forbidden, tainted, foreign-biocoded and persona gear is excluded. Conflicting shields and ranged weapons are rejected. AI-selected clothing is marked as AI-owned forced apparel to prevent the native optimizer immediately undoing its choice; disabling automation releases only those markers and leaves equipment on the pawn.

Weapon scores use the game's current aiming delay, shooting accuracy, melee hit/damage/cooldown and a 12-cell accuracy reference. Brawlers favor melee; other colonists can prefer melee when their skills strongly support it. Apparel scores consider covered body parts, armor, current outdoor temperature, condition and movement penalties. Nudists receive a clothing penalty in temperate conditions. The inspector exposes trait modifiers, suppressed traits and current derived stats; numerical trait effects are read through game stats, without applying them twice. This does not model every trait's mood or strategic behavior, nor a specific enemy, future season, ideology/royalty preference or modded ammunition system.

Accessible allowed ground gear is considered, including items outside storage; reachability checks are limited to the five strongest weapon and apparel candidates. Recommendations may suggest the same item to several pawns, but execution gives each item to at most one pawn per scan and respects reservations. The loot inventory also identifies forbidden gear and corpse apparel without stripping it. The separate optional ground-item allow toggle can release useful forbidden loot. Explosives and nonstandard attack verbs remain excluded. Loot is paginated in the inspector.

## Gradual ground-item allow

**Itens do chão / allow gradual** releases at most two visible, reachable stacks every 360 game ticks (6 seconds at normal speed). It prioritizes a medicine reserve, food deficit, current construction materials/generator fuel and useful equipment upgrades. Food policies, gear eligibility and per-pawn equipment opt-outs are respected. Hostiles pause releases; candidates must be within 60 cells of an available colonist with safe reachability. Unrelated items remain forbidden until there is a supported need.

Re-forbidding an already tracked item prevents that exact item being released again. Disabling preserves released items. This does not strip corpses, force hauling or split stacks; native work handles collection. Flower pots use the native daylily grower and require Growing work, suitable light and temperature.

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

Early development. Optional food/work/equipment management and compact base construction include bedrooms, stockroom, kitchen, social room, corridors, comfort/floors, medical beds/medicine storage, workshop, weapons storage and researched electric climate/power/lighting. Native food/clothing bills, researched freezers and construction resource gathering are included. Optional cooperative combat includes cover, melee interception and protected withdrawal. Agriculture and perimeter/killbox remain on the roadmap.

## Cooperative combat

Enable **Rim AI → Combate cooperativo: LIGADO**. Armed capable colonists form a defensive group, using native attack and movement jobs. Ranged fighters evaluate cover, weapon range, line of sight and approaching melee threats; melee fighters protect nearby allies and guard narrow passages. Injuries and unfavorable local odds trigger withdrawal, favoring reachable enclosed shelters. Existing manual drafts/orders are preserved. The independent combat setting starts disabled; automatic emergency response is described below. **Desligar todas** releases AI-owned drafts.

**Emergência automática** now starts enabled and delegates to cooperative combat during immediate danger. A shared per-colony-map state coordinates threat classification, vulnerable colonists, conservative rescue routes, suspension of secondary work and staged recovery. Threat scans run every 60 ticks and include manhunters, insects, active mechanoids, visible hives and hostile structures. Clearing a threat requires 600 safe ticks before recovery; normal expansion/secondary production resume after at least 3,000 recovery ticks and resolution of critical needs/temporary injuries. Turning cooperative combat off also disables automatic emergency response; **Desligar todas** releases both. See [emergency behavior and native validation](docs/EMERGENCIA-COLONIA.md). Run `scripts/CombatTest.ps1 -EmergencyChecks` for the isolated detection/rescue/recovery fixture.

Three isolated native combat scenarios passed on 06/10/2026: interception (3 against 1), ranged cover (3 against 2), and withdrawal into a closed-door shelter (3 against 9, including an injured defender). No participating colonist died or was downed. The last scenario tests escape for 1,800 ticks, not elimination of the larger force. See [behavior, measured results and limitations](docs/COMBATE-COOPERATIVO.md). Run `scripts/CombatTest.ps1` after building/installing the mod and runtime checks; it uses a private profile and never loads the player's save. Explosives, special abilities, other combat mods, planned ambushes and broad battle/save-load coverage remain unvalidated.

A subsequent five-case mixed-equipment trial with two melee/two ranged colonists, all wearing flak vests and steel helmets, exposed substantial failures: two unresolved battles and three defeats, with 14 downed participants across five independent teams. No complete victory or safe withdrawal occurred. See [the full negative results and equipment/seed records](docs/CINCO-COMBATES-MISTOS.md). Run `scripts/FiveCombatTests.ps1 -Disadvantage` to repeat that configuration. This broader evidence limits the conclusions from the earlier simple passing fixtures; combat is not reliable for those larger engagements.

The 08/10/2026 melee revision adds coordinated approaches, distinct flank cells, equipment/armor analysis, route exposure checks, stable orders and joint withdrawal when a partner is unavailable. A pawn caught by a faster melee attacker fights in contact instead of repeatedly trying to outrun it without cover from another interceptor. Target ranking can use a bounded background worker with value-only snapshots; native game operations remain on the main thread. See [the revision, Peaceful level-20 fixtures, measured results and limitations](docs/REVISAO-COMBATE-MELEE.md). Run `scripts/CombatTest.ps1 -TrialCase 1 -MeleeRevision` (cases 1–5), or add `-Synchronous` to exercise its direct calculation alternative. These fixtures supply participants/arena/equipment at setup and preserve native damage afterward; they are not survival or FPS benchmarks.

## Initial base construction

New colonies now use the [approved four-courtyard core](docs/BASE-NUCLEO-PATIOS.md). It validates firm support for walls and furniture, accepts trees, mineable rocks and neutral deconstructible debris, and clears those obstacles through native colonist work. It reserves future bedrooms and uses shared partitions. Existing ring/compact plans retain their earlier geometry. The [full normal-construction trial](docs/TESTE-NUCLEO-PATIOS.md) is in progress; its geometry passed, but complete construction has not yet been verified. The descriptions of 5×5 bedrooms and older modules below refer to retained legacy plans.

Use **Planejar base** to preview modules without opening construction work. **Base automática** enables execution. New components enable base, strategy, schedule and ground-item access by default; food/work/equipment management have separate toggles. Saved settings are preserved. The controls stay at the top of Rim AI while the inspector scrolls; **Desligar todas** disables every managed function. Hauling remains under native pawn work.

Rooms use interior dimensions excluding walls: bedrooms 5×5, stockroom and social room 6×6, kitchen 4×4 with fueled stove and butcher table. New rooms face a two-cell covered corridor with two exterior exits. Existing qualifying bedrooms, stockpiles and cooking/butchering infrastructure are considered. Stone walls/floors are preferred when research and sufficient existing resources permit; the early fallback is wood. The queue starts with protected storage, then bedrooms, kitchen and social space. Kitchen/storage occupy the entrance side, bedrooms the farther wing. Comfort furniture and floors follow shelter. New bedrooms have a single bed head against the north wall, end table, dresser and flower pot, plus a clear 2×2 footprint for a future double bed. Replacement is not automated; old rooms are preserved. Kitchen work points have a stool. Storage keeps the cell beside its door clear and excludes corpses, chemfuel and mortar shells. Construction deficits can designate native tree cutting/mining, with up to 16 pending resource marks; no resources are spawned.

The planner reserves a visible, accessible, clear compact block near colonists and preserves existing buildings/zones and old plans. Corridor boundaries shared with rooms are constructed once. It finances individual batches, subtracts outstanding player/AI construction material needs, opens at most six blueprints per cycle and limits pending AI works to eighteen (six per project). Up to three projects run concurrently; blocked/cancelled projects yield a slot. Storage zones become usable before the shell/roof is finished. Secondary work preserves essential material margins. Colonists build through RimWorld's normal construction jobs. Roof orders follow shell completion; actual buildings/furniture/floors and roofing are verified. Food production remains a separate toggle.

With Electricity, AirConditioning and ComplexFurniture researched when generating a new block, vents connect living rooms directly to the corridor. The freezer has two separate exterior coolers targeting −2 °C and no vent into the heated corridor. Climate coolers also exhaust into unroofed exterior cells. Two heaters near opposite entrances target 20 °C and corridor coolers 24 °C; later manual setpoint changes are preserved. Fueled generators are sized against peak electrical demand plus 20%, and conduits connect climate devices and room lights. Fuel, labor and adequate thermal capacity remain necessary. Seasonal tile estimates and current outdoor temperature appear in the HUD. Airlocks, research automation, retrofitting old blocks, fuel alternatives and thermal capacity upgrades are not implemented. See [the compact layout guide](docs/LAYOUT-COMPACTO.md) for style choices, cautions and biome limitations.

New general stockrooms exclude food; food-only freezers have Important priority over Normal general storage. A nine-cell outdoor dump accepts only stone chunks and animal corpses near the kitchen/workshop. Later modules include two ordinary Medical beds, three Critical medicine cells and an Important weapons-only room. Small shelves with the corresponding sector filter follow essential construction when ComplexFurniture is researched. These settings are applied once; player edits and existing zones are preserved. Gradual allow releases up to eight needed stacks per cycle, respecting access, threats and manual reprohibitions.

**Comida / roupas** manages native target-count bills. The meal target is derived from daily nutrition and the desired survival-day reserve, with a minimum working stock of 20 and a cap of 400. Bills pause when their cooked product reaches the target and resume below it; raw ingredients do not satisfy the cooked-meal target. Simple meals are favored during scarcity, Fine during normal supply and Lavish only with abundance and a qualified cook. Pemmican or Packaged Survival Meals receive a separate long-life reserve target when capacity allows. Switching recipes removes only untouched AI food bills; native x4 batches can overshoot the target by up to three. Clothing bills keep three spare pants, button-down shirts and a temperature-appropriate outer garment, using native tailoring tables, research, materials and work. No clothing resources are spawned. Existing player bills and edits remain authoritative. Selecting ingredients strictly by expiry and physically expanding a full freezer remain pending improvements.

Player-cancelled works pause their room instead of being recreated. Disabling cancels the tracked AI blueprints and pending AI roof orders, preserving player blueprints, completed structures, stockpiles and frames with materials already invested. Frames can still finish under native work settings. Construction emits no new work under hostile presence. The initial layout is not a complete strategic base-site analysis, seasonal survival system or guaranteed construction-time estimate.

Runtime checks also cover compact room/corridor connections, two exits, direct vents, native floors, furniture facility links, power connections, heating/cooling, preview/duplicate prevention, budgets, construction limits, stockpile creation, cancellation and toggle cleanup. A real pawn builds a full bedroom with bed/roof. Other modules use accelerated native frame fixtures, followed by actual power/temperature simulation. The trial uses a skilled builder, supplied materials and maintained food/rest; it is not a long-term all-biome survival trial. Request visual rendering/screenshot checks with `-RuntimeChecks -Visible -TimeoutSeconds 300`; normal runtime checks stay hidden.

## Native construction validation

See [execution, recovery and performance measurement](docs/EXECUCAO-CONSTRUCAO.md). The dedicated test observes normal Crashlanded pawns building storage, shelter/beds, kitchen, powered freezer and generation, using available resources and native gathering. It does not clear terrain, spawn resources, refill needs, alter skills/research or complete frames.

```powershell
.\scripts\NormalConstructionTest.ps1 -RimWorldDir "C:\Path\To\RimWorld" -TimeoutSeconds 2400
.\scripts\NormalConstructionTest.ps1 -RimWorldDir "C:\Path\To\RimWorld" -LoadStart -TimeoutSeconds 2400
.\scripts\NormalConstructionTest.ps1 -RimWorldDir "C:\Path\To\RimWorld" -Baseline -TimeoutSeconds 2400
```

The isolated profile retains its start/final saves and measured tick counts locally. Baseline uses the previous executor with the same saved start/layout and native gathering. Earlier accelerated runtime fixtures remain useful for individual mechanics; they are not evidence of normal autonomous colony construction. The approximately 40% improvement is a measurement target, not a guarantee for every colony.

The explicitly requested functional fixture uses `-SkilledFixture -Peaceful`. It has a separate `.tools/skilled-construction` profile and start/checkpoint/final saves, three native Crashlanded colonists set to skill 20 without traits or disabling backstories, and third speed. It preserves native resources, transport, costs, research, needs and construction work. Its expanded checks cover storage filters, medical beds/medicine slots, shelves, weapons storage, −2 °C thermostats and production bills. It is not the ordinary-skill speed benchmark.

See [the preference test results and exported saves](docs/TESTE-PREFERENCIAS-BASE.md). Full functional recovery, a fresh-plan/native-zone check and clothing bill ownership/cleanup through native save/load passed. Sustained crafting, mixed-meal production and all-biome thermal behavior still require longer trials.
