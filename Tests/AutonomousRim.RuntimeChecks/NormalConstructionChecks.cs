using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    // Observer/automation controls only: no resource spawning, map clearing, skill edits,
    // need refills, artificial research or frame completion. Normal pawns do every job.
    public sealed class NormalConstructionChecks : MapComponent
    {
        private bool started, finished, initialRecorded;
        private int startTick, lastProgressTick, completed;
        private readonly List<Thing> baselineGathering = new List<Thing>();
        private readonly List<Thing> baselineReleasedLoot = new List<Thing>();
        private int baselineLootTick = -600;
        private static bool baselineLoaded, checkpointObserved;
        private float lastPauseAttempt;
        private List<Pawn> initialColonists = new List<Pawn>();
        private string baselineStatus;
        public NormalConstructionChecks(Map map) : base(map) { }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref started, "normalCheckStarted");
            Scribe_Values.Look(ref finished, "normalCheckFinished");
            Scribe_Values.Look(ref initialRecorded, "normalCheckInitialRecorded");
            Scribe_Values.Look(ref startTick, "normalCheckStartTick");
            Scribe_Values.Look(ref lastProgressTick, "normalCheckLastProgressTick");
            Scribe_Values.Look(ref completed, "normalCheckCompleted");
            Scribe_Collections.Look(ref initialColonists, "normalCheckColonists", LookMode.Reference);
        }
        public override void MapComponentUpdate()
        {
            if (!finished && started && GenCommandLine.CommandLineArgPassed("autonomousrimnormaltest") && Find.TickManager.Paused)
            {
                float now = UnityEngine.Time.realtimeSinceStartup;
                if (now - lastPauseAttempt < 5f) return;
                lastPauseAttempt = now;
                foreach (var window in Find.WindowStack.Windows.Where(w => w.forcePause).ToList())
                {
                    if (window is Dialog_NamePlayerFactionAndSettlement naming)
                    {
                        // Accept the ordinary generated suggestions, as the game's OK
                        // button does. Closing alone makes this mandatory prompt reopen.
                        string first = (string)AccessTools.Field(typeof(Dialog_GiveName), "curName").GetValue(naming);
                        string second = (string)AccessTools.Field(typeof(Dialog_GiveName), "curSecondName").GetValue(naming);
                        AccessTools.Method(typeof(Dialog_NamePlayerFactionAndSettlement), "Named").Invoke(naming, new object[] { first });
                        AccessTools.Method(typeof(Dialog_NamePlayerFactionAndSettlement), "NamedSecond").Invoke(naming, new object[] { second });
                        naming.Close(false);
                        Log.Message("[AutonomousRim.NormalTests] NAMING: accepted native faction and settlement name suggestions.");
                        continue;
                    }
                    if (window is Dialog_NodeTree dialogue)
                    {
                        var node = (DiaNode)AccessTools.Field(typeof(Dialog_NodeTree), "curNode").GetValue(dialogue);
                        var exits = node.options.Where(o => !o.disabled && o.resolveTree && o.link == null && o.linkLateBind == null).ToList();
                        var exit = exits.FirstOrDefault(o => o.action == null) ?? exits.FirstOrDefault(o =>
                            new[] { "Disconnect".Translate().ToString(), "Leave".Translate().ToString(), "Cancel".Translate().ToString(), "Close".Translate().ToString(), "Reject".Translate().ToString(), "Reject" }.Any(label => (string)AccessTools.Field(typeof(DiaOption), "text").GetValue(o) == label));
                        if (exit != null)
                        {
                            Log.Message("[AutonomousRim.NormalTests] DIALOGUE EXIT: " + AccessTools.Field(typeof(DiaOption), "text").GetValue(exit) + "; activating its ordinary exit option.");
                            AccessTools.Method(typeof(DiaOption), "Activate").Invoke(exit, null);
                            continue;
                        }
                        Log.Message("[AutonomousRim.NormalTests] DIALOGUE OPTIONS: " + string.Join(" | ", node.options.Select(o => AccessTools.Field(typeof(DiaOption), "text").GetValue(o) + "/exit=" + o.resolveTree)));
                    }
                    Log.Message("[AutonomousRim.NormalTests] DISMISS: " + window.GetType().Name + "; closing the test-only blocking window with the native Close method.");
                    window.Close(false);
                }
                Log.Message("[AutonomousRim.NormalTests] RESUME: ordinary game clock paused by an event; continuing at normal third speed.");
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
            }
        }
        public override void MapComponentTick()
        {
            if (finished || !GenCommandLine.CommandLineArgPassed("autonomousrimnormaltest") || Find.TickManager.TicksGame % 120 != 0) return;
            bool baseline = GenCommandLine.CommandLineArgPassed("autonomousrimbaseline");
            var component = map.GetComponent<AutonomousRimMapComponent>();
            int ticks = Find.TickManager.TicksGame;
            try
            {
                if (initialColonists == null) initialColonists = new List<Pawn>();
                if (!baselineLoaded && GenCommandLine.CommandLineArgPassed("autonomousrimloadcheckpoint"))
                {
                    baselineLoaded = true;
                    GameDataSaveLoader.LoadGame("ConstructionCheckpoint");
                    return;
                }
                if (!checkpointObserved && GenCommandLine.CommandLineArgPassed("autonomousrimloadcheckpoint"))
                {
                    checkpointObserved = true;
                    lastProgressTick = ticks; // A new recovery trial gets its own watchdog window; total elapsed is preserved.
                    Log.Message("[AutonomousRim.NormalTests] RECOVERY TRIAL: native checkpoint resumed; total construction time remains unchanged.");
                }
                if (!started)
                {
                    if (ticks < 120 || map.mapPawns.FreeColonistsSpawnedCount == 0) return;
                    if ((baseline || GenCommandLine.CommandLineArgPassed("autonomousrimloadstart")) && !baselineLoaded)
                    {
                        baselineLoaded = true;
                        GameDataSaveLoader.LoadGame("ConstructionStart");
                        return;
                    }
                    if (!baseline && (!GenCommandLine.CommandLineArgPassed("autonomousrimloadstart") || GenCommandLine.CommandLineArgPassed("autonomousrimpeaceful")))
                    {
                        if (GenCommandLine.CommandLineArgPassed("autonomousrimpeaceful"))
                        {
                            // An ordinary selectable difficulty preset, set before the
                            // untouched start save. Baseline reuses the same preset/save.
                            var preset = DefDatabase<DifficultyDef>.GetNamed("Peaceful");
                            Find.Storyteller.difficultyDef = preset;
                            Find.Storyteller.difficulty.CopyFrom(preset);
                        }
                        GameDataSaveLoader.SaveGame("ConstructionStart");
                    }
                    initialColonists = map.mapPawns.FreeColonistsSpawned.ToList();
                    started = true; startTick = ticks; lastProgressTick = ticks;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                    Log.Message("[AutonomousRim.NormalTests] START: " + (baseline ? "previous executor, same saved start/layout/gathering" : "new executor") +
                        "; scenario=" + Current.Game.Scenario.name + "; difficulty=" + Find.Storyteller.difficultyDef.defName + "; research=" + DefDatabase<ResearchProjectDef>.GetNamed("AirConditioning").IsFinished +
                        "; pawns=" + string.Join(",", map.mapPawns.FreeColonistsSpawned.Select(p => p.LabelShort + "/Construction=" + p.skills.GetSkill(SkillDefOf.Construction).Level)));
                    component.SetAutomation(true, true); component.SetLootAutomation(!baseline);
                    component.SetEquipmentAutomation(true);
                    if (baseline) component.PreviewBase(); else component.SetBaseAutomation(true);
                    Log.Message("[AutonomousRim.NormalTests] INITIAL ITEMS: " + string.Join(",", map.listerThings.AllThings.Where(t => t.def.category == ThingCategory.Item && t.Spawned).GroupBy(t => t.def).Select(g => g.Key.defName + "=" + g.Sum(t => t.stackCount))));
                }
                if (baseline && ticks % 600 == 0)
                {
                    if (component.BaseProjects.Count == 0) component.PreviewBase();
                    baselineStatus = LegacyConstructionExecutor.Apply(map, component.BaseProjects.ToList());
                    ConstructionResourceManager.Apply(map, component.BaseProjects, baselineGathering);
                }
                if (baseline && ticks - baselineLootTick >= 600)
                {
                    // The frozen executor runs with the new executor disabled. Still pass
                    // its actual building demand to the same gradual-allow policy.
                    baselineLootTick = ticks;
                    LootAccessManager.Apply(map, component.CurrentState, true, component.BaseProjects,
                        new List<ManagedApparel>(), _ => true, baselineReleasedLoot, out int released);
                }
                if (component.BaseProjects.Count == 0)
                {
                    if (ticks - startTick > 12000) throw new InvalidOperationException("No buildable initial plan: " + component.BaseStatus);
                    return;
                }
                if (!baseline && component.BaseProjects.Count(p => p.State == ConstructionState.Active) > 3) throw new InvalidOperationException("More than three active projects.");
                int now = component.BaseProjects.SelectMany(BaseConstructionManager.Tasks).Count(t => t.Complete(map));
                if (now != completed) { lastProgressTick = ticks; completed = now; }
                if (ticks % 3600 == 0)
                {
                    Log.Message("[AutonomousRim.NormalTests] PROGRESS: elapsed=" + (ticks - startTick) + "; completed=" + completed + "; " + (baseline ? baselineStatus : component.BaseStatus) +
                        "; " + component.GatheringStatus + "; " + component.ManagementStatus + "; " + string.Join(" | ", component.BaseProjects.Where(p => !p.Completed).Select(p => p.Kind + ":" + p.State + ":" + p.BlockReason)) +
                        "; jobs=" + string.Join(",", map.mapPawns.FreeColonistsSpawned.Select(p => p.LabelShort + ":" + p.CurJob?.def.defName + "/" + p.CurJob?.workGiverDef?.defName +
                            "/C=" + p.workSettings.GetPriority(WorkTypeDefOf.Construction) + "/H=" + p.workSettings.GetPriority(WorkTypeDefOf.Hauling) + "/queued=" + p.jobs.jobQueue.Count + "/forced=" + p.CurJob?.playerForced + "/hp=" + p.health.summaryHealth.SummaryHealthPercent.ToString("0.00"))));
                }
                if (!baseline && ticks % 18000 == 0) GameDataSaveLoader.SaveGame("ConstructionCheckpoint");
                if (ticks % 18000 == 0)
                {
                    foreach (var p in component.BaseProjects.Where(p => !p.Completed && p.Priority <= ConstructionPriority.High))
                    {
                        var pending = BaseConstructionManager.Tasks(p).Where(t => t.Pending?.Spawned == true).Select(t => t.Pending).Distinct().ToList();
                        foreach (var target in pending)
                        {
                            var frame = target as Frame;
                            var materials = frame == null ? ((Blueprint_Build)target).TotalMaterialCost() : frame.TotalMaterialCost()
                                .Select(c => new ThingDefCountClass(c.thingDef, frame.ThingCountNeeded(c.thingDef))).ToList();
                            Log.Message("[AutonomousRim.NormalTests] PENDING: " + p.Kind + "/" + target.def.defName + "@" + target.Position +
                                "; missing=" + string.Join(",", materials.Where(c => c.count > 0).Select(c => c.thingDef.defName + "=" + c.count)) +
                                "; work=" + frame?.workDone + "; nativeFinish=" + string.Join(",", map.mapPawns.FreeColonistsSpawned.Select(pawn => pawn.LabelShort + ":" +
                                    ((WorkGiver_Scanner)DefDatabase<WorkGiverDef>.GetNamed("ConstructFinishFrames").Worker).JobOnThing(pawn, target, false)?.def.defName)));
                        }
                    }
                    Log.Message("[AutonomousRim.NormalTests] ROLES: " + string.Join(" | ", map.mapPawns.FreeColonistsSpawned.Select(pawn => pawn.LabelShort +
                        ":C=" + pawn.skills.GetSkill(SkillDefOf.Construction).Level + "; " + string.Join(",", new[] { "Construction", "Hauling", "Cooking", "Hunting", "Mining", "PlantCutting" }
                            .Select(name => name + "=" + pawn.workSettings.GetPriority(DefDatabase<WorkTypeDef>.GetNamed(name)))))));
                    foreach (var building in map.listerBuildings.allBuildingsColonist.Where(b => b.def.defName == "Cooler" || b.def.defName == "WoodFiredGenerator"))
                    {
                        var power = building.GetComp<CompPowerTrader>();
                        Log.Message("[AutonomousRim.NormalTests] POWER: " + building.def.defName + "@" + building.Position +
                            "; on=" + power?.PowerOn + "; output=" + power?.PowerOutput + "; fuel=" + building.GetComp<CompRefuelable>()?.Fuel +
                            "; network=" + power?.PowerNet?.CurrentEnergyGainRate() + "; temp=" + building.Position.GetTemperature(map));
                    }
                    foreach (var freezerProject in component.BaseProjects.Where(p => p.Kind == "Freezer"))
                        Log.Message("[AutonomousRim.NormalTests] FREEZER: temp=" + freezerProject.Interior.CenterCell.GetTemperature(map) +
                            "; roof=" + freezerProject.Interior.CenterCell.Roofed(map));
                }
                var required = component.BaseProjects.Where(p => p.Kind == "Estoque" || p.Kind == "Quarto" || p.Kind == "Cozinha" || p.Kind == "Freezer" || p.Kind == "Corredor" || p.Kind == "Energia e climatização").ToList();
                var basic = required.Where(p => p.Kind != "Corredor" && p.Kind != "Energia e climatização").ToList();
                if (!initialRecorded && basic.All(p => p.Completed) && basic.Any(p => p.Kind == "Freezer"))
                {
                    RoomProject freezer = basic.First(p => p.Kind == "Freezer");
                    bool powered = freezer.Shell.Where(t => t.Def.defName == "Cooler").All(t => t.Complete(map) &&
                        t.Position.GetThingList(map).OfType<ThingWithComps>().Single(b => b.def == t.Def).TryGetComp<CompPowerTrader>().PowerOn);
                    bool generation = component.BaseProjects.SelectMany(BaseConstructionManager.Tasks).Any(t => t.Def.defName == "WoodFiredGenerator" && t.Complete(map) &&
                        t.Position.GetThingList(map).OfType<ThingWithComps>().Single(b => b.def == t.Def).TryGetComp<CompPowerPlant>().PowerOutput > 0f);
                    if (powered && generation && freezer.Interior.CenterCell.GetTemperature(map) < 0)
                    {
                        initialRecorded = true;
                        string initial = (baseline ? "baseline" : "new") + "; initialTicks=" + (ticks - startTick) + "; freezer=" + freezer.Interior.CenterCell.GetTemperature(map).ToString("0.0");
                        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, baseline ? "baseline-initial-result.txt" : "new-initial-result.txt"), initial);
                        Log.Message("[AutonomousRim.NormalTests] INITIAL INFRASTRUCTURE: native stockpile, all bedrooms/beds, kitchen, subzero powered freezer and working generation; " + initial);
                    }
                }
                if (required.All(p => p.Completed) && required.Any(p => p.Kind == "Freezer") && required.Any(p => p.Kind == "Energia e climatização") &&
                    required.First(p => p.Kind == "Freezer").Interior.CenterCell.GetTemperature(map) < 0 &&
                    map.listerBuildings.allBuildingsColonist.Any(b => b.def.defName == "WoodFiredGenerator" && b.GetComp<CompPowerPlant>()?.PowerOutput > 0f))
                {
                    RoomProject freezer = required.First(p => p.Kind == "Freezer");
                    bool powered = freezer.Shell.Where(t => t.Def.defName == "Cooler").All(t => t.Position.GetThingList(map).OfType<ThingWithComps>().Single(b => b.def == t.Def).TryGetComp<CompPowerTrader>().PowerOn);
                    if (powered)
                    {
                        int elapsed = ticks - startTick;
                        var result = (baseline ? "baseline" : "new") + "; elapsedTicks=" + elapsed + "; completed=" + completed + "; freezer=" + freezer.Interior.CenterCell.GetTemperature(map).ToString("0.0");
                        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, baseline ? "baseline-result.txt" : "new-result.txt"), result);
                        GameDataSaveLoader.SaveGame(baseline ? "ConstructionBaselineFinished" : "ConstructionFinished");
                        finished = true; Log.Message("[AutonomousRim.NormalTests] PASS: native stockpile, shelter, beds, kitchen, powered freezer and generation; " + result);
                    }
                }
                if (ticks - lastProgressTick > 90000 || ticks - startTick > 1200000) throw new InvalidOperationException("Native initial infrastructure stalled/incomplete: " + component.BaseStatus);
                if (initialColonists.Any(p => p == null || p.Dead)) throw new InvalidOperationException("Colonist died during native construction run.");
            }
            catch (Exception e)
            {
                finished = true;
                string detail = "elapsedTicks=" + (ticks - startTick) + "; completed=" + completed + "; status=" +
                    (baseline ? baselineStatus : component.BaseStatus) + "; " + e;
                try
                {
                    File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, baseline ? "baseline-failure.txt" : "new-failure.txt"), detail);
                    GameDataSaveLoader.SaveGame(baseline ? "ConstructionBaselineFailed" : "ConstructionFailed");
                }
                catch (Exception saveError) { Log.Warning("[AutonomousRim.NormalTests] Could not preserve failure snapshot: " + saveError.Message); }
                Log.Error("[AutonomousRim.NormalTests] FAIL: " + detail);
            }
        }
    }
}
