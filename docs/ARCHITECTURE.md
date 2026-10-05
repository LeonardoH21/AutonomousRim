# AutonomousRim Architecture

AutonomousRim is designed as a layered autonomous-agent system rather than a monolithic controller.

## 1. Perception

Reads game state without making decisions.

Planned modules:

- MapAnalyzer
- ColonyStateScanner
- PawnAnalyzer
- EquipmentAnalyzer
- ThreatScanner
- EnvironmentAnalyzer

## 2. Blackboard / world model

Stores normalized state used by the AI:

- colonists and their roles
- weapons and apparel
- map geometry
- resources and production
- current threats
- seasonal risks
- active strategic objectives

## 3. Planning

Three decision horizons are planned:

- **Tactical:** seconds/minutes, especially combat
- **Operational:** hours/days, colony workflow and recovery
- **Strategic:** quadrums/years, seasons, expansion, research and survival

The initial implementation will use deterministic scoring/Utility AI.
Higher-level planning can later add GOAP or hierarchical planning.

## 4. Execution

Planning code must not directly manipulate unrelated systems.
Dedicated executors will translate AI decisions into RimWorld actions.

Examples:

- CombatExecutor
- ConstructionExecutor
- WorkPriorityExecutor
- EquipmentExecutor

## 5. Safety principles

The AI's top-level priorities are:

1. Avoid colony-ending failure.
2. Preserve colonists.
3. Preserve military capability.
4. Preserve food/medical capability.
5. Protect critical infrastructure.
6. Grow and optimize only when survival is secure.

## Performance

Expensive analysis should be cached and scheduled at different intervals.
The entire map must not be recomputed every game tick.
