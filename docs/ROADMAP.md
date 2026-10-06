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
- [ ] Full pawn traits/health/genes analyzer (active trait modifiers, suppressed traits, effective stats and Brawler/Nudist preferences implemented)
- [x] Initial equipment analyzer (weapon DPS, penetration, condition, garment armor and advisory matching)
- [x] Initial threat scanner (active hostile pawns, race/weapon composition and heuristic risk)
- [ ] Full resource/food security model (colonist hunger, diet-aware stored reserves implemented; animal/visitor demand and spoilage prediction pending)
- [ ] Season/environment scanner
- [x] Debug inspector UI

## Local validation — 2026-10-05
- Release compilation against RimWorld 1.6.4633: zero warnings, zero errors.
- Installed DLL hash verified against build output.
- Game startup with an isolated profile: `[AutonomousRim] Loaded successfully.` confirmed.
- Graphics-enabled startup with Harmony, Core and all five DLCs: no exceptions or XML/configuration errors in the log. An earlier `-nographics` run produced engine texture/shader errors, so it was replaced by the graphics-enabled check.
- Colony scanning has now been verified repeatedly in an automatically generated test colony with three colonists and two combat-ready pawns. Inspector rendering and behavior during live threats still need in-game validation.
- Thirty-five checks cover combat calculations, threat boundaries, prey exclusions, meal targets and profession scoring.
- In-game runtime checks pass for ground/corpse loot, food demand, production orders, hunting designation, work overrides and restoration.
- Native weapon pickup and armor wear passed in the generated colony, including active trait stats, Brawler preference, player-forced/apparel policy guards, biocoding, shield compatibility, cooldown and AI force cleanup.

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
- [x] Ordered base/production specification: [PLANO-BASE-AUTONOMA.md](PLANO-BASE-AUTONOMA.md) (design only; execution pending)
- [ ] Map terrain analysis
- [ ] Chokepoint detection
- [ ] Base-site scoring
- [ ] Functional room graph
- [ ] Blueprint generation
- [ ] Defense layout planning
- [ ] Power planning

## Milestone 4 — Colony management
- [x] Initial optional skill/passion-based work priorities with manual overrides and restoration
- [x] Initial optional food production: passive-wildlife hunting, butchering and simple-meal orders
- [ ] Full agricultural planning and diet-aware recipe optimization
- [ ] Bills/crafting
- [ ] Research selection
- [x] Initial optional native weapon/apparel assignments with player-policy and forced-item protection
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
