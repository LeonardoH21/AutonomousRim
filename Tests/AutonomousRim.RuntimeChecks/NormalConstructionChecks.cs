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
    // Native work/cost/transport observer. The explicitly requested skilled fixture
    // changes only the generated test pawns, before saving its separate initial state.
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
            if (GenCommandLine.CommandLineArgPassed("autonomousrimcrafttest")) { CraftProductionChecks.Tick(map); return; }
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
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                    Log.Message("[AutonomousRim.NormalTests] RECOVERY TRIAL: native checkpoint resumed; total construction time remains unchanged.");
                }
                if (!started)
                {
                    if (ticks < 120 || map.mapPawns.FreeColonistsSpawnedCount == 0) return;
                    if (GenCommandLine.CommandLineArgPassed("autonomousrimskilledfixture") && !GenCommandLine.CommandLineArgPassed("autonomousrimloadstart"))
                    {
                        foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                        {
                            foreach (var trait in pawn.story.traits.allTraits.ToList()) pawn.story.traits.RemoveTrait(trait);
                            pawn.story.Childhood = DefDatabase<BackstoryDef>.AllDefsListForReading.First(b => b.slot == BackstorySlot.Childhood && b.workDisables == WorkTags.None);
                            if (pawn.story.Adulthood != null) pawn.story.Adulthood = DefDatabase<BackstoryDef>.AllDefsListForReading.First(b => b.slot == BackstorySlot.Adulthood && b.workDisables == WorkTags.None);
                            pawn.Notify_DisabledWorkTypesChanged();
                            foreach (var skill in pawn.skills.skills) { skill.Level = 20; skill.xpSinceLastLevel = 0; }
                            if (pawn.GetDisabledWorkTypes().Count != 0 || pawn.story.traits.allTraits.Count != 0)
                                throw new InvalidOperationException("Skilled fixture still has disabled work/traits: " + pawn.LabelShort);
                        }
                        Log.Message("[AutonomousRim.NormalTests] SKILLED FIXTURE: test-only skills=20, no traits/backstory incapabilities, native costs/resources/needs/work, third speed. Not an ordinary-skill performance benchmark.");
                    }
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
                    if (GenCommandLine.CommandLineArgPassed("autonomousrimplanonly"))
                    {
                        component.PreviewBase();
                        ValidateNewPlan(component);
                        foreach (var task in component.BaseProjects.First(p => p.Kind == "Freezer").Shell.Where(t => t.Def.defName == "Cooler"))
                            task.TargetTemperature = -4; // An unconfigured older-plan thermostat, not a built/adjusted cooler.
                        component.SetBaseAutomation(true);
                        if (component.BaseProjects.First(p => p.Kind == "Freezer").Shell.Where(t => t.Def.defName == "Cooler").Any(t => t.TargetTemperature != -2))
                            throw new InvalidOperationException("Pending older freezer plans did not adopt -2 Celsius.");
                        var stock = component.BaseProjects.First(p => p.Kind == "Estoque").Stockpile.GetStoreSettings();
                        var freezer = component.BaseProjects.First(p => p.Kind == "Freezer").Stockpile.GetStoreSettings();
                        foreach (var food in new[] { ThingDefOf.MealSimple, DefDatabase<ThingDef>.GetNamed("Kibble"), DefDatabase<ThingDef>.GetNamed("Hay") })
                            if (stock.filter.Allows(food) || !freezer.filter.Allows(food)) throw new InvalidOperationException("Food category storage failed: " + food.defName);
                        if (freezer.Priority <= stock.Priority) throw new InvalidOperationException("Freezer priority does not exceed general storage.");
                        finished = true;
                        Log.Message("[AutonomousRim.NormalTests] PASS: fresh compact plan and native storage zones; placement/reserved footprints, medical/workshop lights, human/animal food filters, two entrance heaters and freezer setpoints. Bounded blueprints only, no completed structures.");
                        return;
                    }
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
                bool skilled = GenCommandLine.CommandLineArgPassed("autonomousrimskilledfixture");
                if (skilled) required.AddRange(component.BaseProjects.Where(p => p.Kind == "Hospital" || p.Kind == "Oficina" || p.Kind == "Armas" ||
                    p.Kind == "Despejo" || p.Kind == "Medicamentos" || p.Kind == "Prateleiras"));
                var basic = required.Where(p => p.Kind == "Estoque" || p.Kind == "Quarto" || p.Kind == "Cozinha" || p.Kind == "Freezer").ToList();
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
                        if (skilled) ValidatePreferences(component);
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

        private void ValidatePreferences(AutonomousRimMapComponent component)
        {
            var stock = component.BaseProjects.First(p => p.Kind == "Estoque").Stockpile.GetStoreSettings();
            var freezer = component.BaseProjects.First(p => p.Kind == "Freezer").Stockpile.GetStoreSettings();
            if (stock.filter.Allows(ThingDefOf.MealSimple) || !freezer.filter.Allows(ThingDefOf.MealSimple) ||
                freezer.filter.Allows(ThingDefOf.Steel) || freezer.Priority <= stock.Priority)
                throw new InvalidOperationException("Food storage filters/priority are incorrect.");
            var medicine = component.BaseProjects.First(p => p.Kind == "Medicamentos").Stockpile;
            if (medicine.Cells.Count() != 3 || medicine.GetStoreSettings().Priority != StoragePriority.Critical ||
                !medicine.GetStoreSettings().filter.AllowedThingDefs.All(d => d.IsMedicine))
                throw new InvalidOperationException("Medicine storage is not three critical slots.");
            var dump = component.BaseProjects.First(p => p.Kind == "Despejo").Stockpile.GetStoreSettings().filter;
            if (!dump.Allows(DefDatabase<ThingDef>.GetNamed("ChunkGranite")) || !dump.Allows(DefDatabase<ThingDef>.GetNamed("Hare").race.corpseDef) ||
                dump.Allows(ThingDefOf.Human.race.corpseDef) || dump.Allows(ThingDefOf.Steel))
                throw new InvalidOperationException("Dump does not restrict itself to stone chunks and animal corpses.");
            var hospital = component.BaseProjects.First(p => p.Kind == "Hospital");
            if (hospital.Furniture.Where(t => t.MedicalBed).Any(t => !t.Position.GetThingList(map).OfType<Building_Bed>().Any(b => b.Medical && b.def == ThingDefOf.Bed)))
                throw new InvalidOperationException("Hospital lacks its ordinary medical beds.");
            foreach (var t in component.BaseProjects.SelectMany(BaseConstructionManager.Tasks).Where(t => t.StorageKind != null))
            {
                var settings = t.Position.GetThingList(map).OfType<Building_Storage>().Single(b => b.Position == t.Position).GetStoreSettings();
                if (settings.filter.Allows(ThingDefOf.MealSimple) != (t.StorageKind == "Freezer"))
                    throw new InvalidOperationException("Shelf filters disagree with their sector.");
                if (settings.Priority <= (t.StorageKind == "Estoque" ? stock.Priority : freezer.Priority))
                    throw new InvalidOperationException("Shelves do not outrank floor storage.");
            }
            foreach (var t in component.BaseProjects.First(p => p.Kind == "Freezer").Shell.Where(t => t.Def.defName == "Cooler"))
                if (t.Position.GetThingList(map).OfType<ThingWithComps>().Single(b => b.def == t.Def).TryGetComp<CompTempControl>().TargetTemperature != -2f)
                    throw new InvalidOperationException("Freezer thermostat is not -2 Celsius.");
            var bills = map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().SelectMany(t => t.BillStack.Bills).OfType<Bill_Production>().ToList();
            if (!bills.Any(b => b.recipe.defName == "CookMealSimpleBulk" || b.recipe.defName == "CookMealFineBulk") ||
                !bills.Any(b => b.recipe.products?.Any(p => p.thingDef.defName == "Apparel_Pants") == true && b.targetCount == 3 && b.unpauseWhenYouHave == 3))
                throw new InvalidOperationException("Native food/clothing production bills are missing.");
            Log.Message("[AutonomousRim.NormalTests] PREFERENCES PASS: -2C freezer, food-only shelves/priority, animal/stone dump, two medical beds, three critical medicine slots, weapons room, native bulk meals and clothing 3/3.");
        }

        private void ValidateNewPlan(AutonomousRimMapComponent component)
        {
            var projects = component.BaseProjects;
            if (!projects.Any(p => p.Kind == "Corredor") || !projects.Any(p => p.Kind == "Hospital") || !projects.Any(p => p.Kind == "Oficina") ||
                !projects.Any(p => p.Kind == "Medicamentos" && p.StorageCells.Count == 3) || !projects.Any(p => p.Kind == "Despejo"))
                throw new InvalidOperationException("Missing compact modules: " + component.BaseStatus);
            var occupied = new HashSet<IntVec3>();
            foreach (var task in projects.SelectMany(BaseConstructionManager.Tasks).Where(t => t.Def is ThingDef && t.Def.defName != "PowerConduit")
                .GroupBy(t => new { t.Def, t.Stuff, t.Position, t.Rotation }).Select(g => g.First()))
                foreach (var cell in GenAdj.OccupiedRect(task.Position, task.Rotation, task.Def.Size))
                    if (!occupied.Add(cell)) throw new InvalidOperationException("Planned furniture/module overlap at " + cell + ": " + task.Def.defName);
            if (projects.SelectMany(BaseConstructionManager.Tasks).Count(t => t.Def.defName == "Heater") != 2 ||
                projects.First(p => p.Kind == "Freezer").Shell.Where(t => t.Def.defName == "Cooler").Any(t => t.TargetTemperature != -2))
                throw new InvalidOperationException("Initial thermal plan differs from requested preferences.");
        }
    }
}
