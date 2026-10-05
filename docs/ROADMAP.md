# Roadmap

## Milestone 0 — Bootstrap
- [x] Mod metadata
- [x] C# project
- [x] Harmony bootstrap
- [x] 1.6 load folder
- [x] Map component

## Milestone 1 — Perception
- [x] Initial colony scanner
- [x] Basic pawn combat scoring
- [ ] Pawn traits/health/genes analyzer
- [x] Initial equipment analyzer (weapon DPS, penetration, condition, garment armor and advisory matching)
- [x] Initial threat scanner (active hostile pawns, race/weapon composition and heuristic risk)
- [ ] Resource/food security model (stored nutrition and baseline days implemented; consumption and spoilage pending)
- [ ] Season/environment scanner
- [x] Debug inspector UI

## Local validation — 2026-10-05
- Release compilation against RimWorld 1.6.4633: zero warnings, zero errors.
- Installed DLL hash verified against build output.
- Game startup with an isolated profile: `[AutonomousRim] Loaded successfully.` confirmed.
- Graphics-enabled startup with Harmony, Core and all five DLCs: no exceptions or XML/configuration errors in the log. An earlier `-nographics` run produced engine texture/shader errors, so it was replaced by the graphics-enabled check.
- Colony scanning has now been verified repeatedly in an automatically generated test colony with three colonists and two combat-ready pawns. Inspector rendering and behavior during live threats still need in-game validation.
- Thirteen checks cover burst damage calculations, invalid data and threat-risk classification boundaries.

## Milestone 2 — Combat foundation
- [ ] Raid incident detection (active-hostile appearance/disappearance implemented)
- [ ] Threat classification
- [ ] Tactical weapon-role matching (initial skill-based stored-weapon recommendations implemented)
- [ ] Cover scoring
- [ ] Line-of-fire analysis
- [ ] Tactical position scoring
- [ ] Automatic draft/undraft
- [ ] Focus-fire selection
- [ ] Fallback / retreat logic
- [ ] Rescue wounded logic

## Milestone 3 — Base planning
- [ ] Map terrain analysis
- [ ] Chokepoint detection
- [ ] Base-site scoring
- [ ] Functional room graph
- [ ] Blueprint generation
- [ ] Defense layout planning
- [ ] Power planning

## Milestone 4 — Colony management
- [ ] Work priorities
- [ ] Food production
- [ ] Bills/crafting
- [ ] Research selection
- [ ] Clothing/equipment assignments
- [ ] Medical management
- [ ] Recruitment policy

## Milestone 5 — Strategic autonomy
- [ ] Seasonal forecasting
- [ ] Resource reserve targets
- [ ] Expansion decisions
- [ ] Wealth/risk management
- [ ] Caravan planning
- [ ] Long-term military planning

## Milestone 6 — Full autonomous mode
- [ ] Start a colony and play without user commands
- [ ] Decision logging
- [ ] Replayable benchmarks
- [ ] Survival metrics and regression tests
