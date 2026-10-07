using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class WorkScheduleChecks : MapComponent
    {
        private static int stage;
        private List<WorkPriorityChange> work = new List<WorkPriorityChange>();
        private List<ScheduleChange> schedules = new List<ScheduleChange>();
        private Pawn sleeper;
        private int sleepStart;
        public WorkScheduleChecks(Map map) : base(map) { }
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref work, "testWork", LookMode.Deep);
            Scribe_Collections.Look(ref schedules, "testSchedules", LookMode.Deep);
        }
        private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        private static void Pass(string message) => Log.Message("[AutonomousRim.WorkScheduleTests] PASS: " + message);
        private static void Healthy(Pawn pawn)
        {
            foreach (var h in pawn.health.hediffSet.hediffs.ToList()) pawn.health.RemoveHediff(h);
            pawn.needs.rest.CurLevel = 1;
            pawn.needs.joy.CurLevel = 1;
            pawn.needs.mood.CurLevel = 1;
            pawn.needs.food.CurLevel = 1;
        }
        public override void MapComponentUpdate()
        {
            if (stage == 99 || !GenCommandLine.CommandLineArgPassed("autonomousrimworkscheduletest")) return;
            foreach (var window in Find.WindowStack.Windows.Where(w => w.forcePause).ToList()) window.Close(false);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
        }
        public override void MapComponentTick()
        {
            if (stage == 99 || !GenCommandLine.CommandLineArgPassed("autonomousrimworkscheduletest") || Find.TickManager.TicksGame < 300) return;
            try
            {
                var ai = map.GetComponent<AutonomousRimMapComponent>();
                if (stage == 1)
                {
                    Check(work.Count > 0 && schedules.Count == 4 && schedules.Any(s => s.PersonalRecovery), "Saved ownership/recovery lost.");
                    Pass("native save/load retains manual overrides, work ownership and recovery");
                    sleeper = schedules.Last().Pawn; Healthy(sleeper); sleeper.needs.rest.CurLevel = .1f;
                    var bedCell = map.Center + new IntVec3(5, 0, 5);
                    foreach (var c in new CellRect(bedCell.x - 1, bedCell.z - 1, 4, 4).Cells)
                    {
                        foreach (var t in c.GetThingList(map).Where(t => !(t is Pawn)).ToList()) t.Destroy(DestroyMode.Vanish);
                        map.terrainGrid.SetTerrain(c, TerrainDefOf.Soil); map.fogGrid.Unfog(c);
                    }
                    var bed = (Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog); bed.SetFaction(Faction.OfPlayer);
                    GenSpawn.Spawn(bed, bedCell, map); sleeper.ownership.ClaimBedIfNonMedical(bed);
                    ScheduleManager.Apply(map, new ColonyState(), schedules, true);
                    sleeper.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    sleepStart = Find.TickManager.TicksGame; stage = 2; return;
                }
                if (stage == 2)
                {
                    if (Find.TickManager.TicksGame - sleepStart < 600) return;
                    Check(sleeper.CurJob?.def == JobDefOf.LayDown && sleeper.needs.rest.CurLevel > .1f, "Native schedule did not produce sleep/rest recovery.");
                    Pass("exhausted colonist naturally takes LayDown in assigned bed and recovers rest, without a forced sleep job");
                    var manual = schedules.First(s => s.UserOverride.Any(v => v));
                    int hour = manual.UserOverride.FindIndex(v => v);
                    var preserved = manual.Pawn.timetable.GetAssignment(hour);
                    ScheduleManager.Restore(schedules);
                    Check(manual.Pawn.timetable.GetAssignment(hour) == preserved, "Restore overwrote manual hour after load.");
                    var manualWork = work.First(c => c.UserOverride);
                    WorkPriorityManager.Restore(work);
                    Check(WorkPriorityManager.RawPriority(manualWork.Pawn, manualWork.Work) == manualWork.Original, "Restore overwrote manual work priority after load.");
                    Check(work.Count == 0 && schedules.Count == 0, "Restore did not release ownership.");
                    Pass("disable restores owned settings and preserves manual assignment after load");
                    stage = 99; Log.Message("[AutonomousRim.WorkScheduleTests] DONE"); return;
                }
                ai.DisableAll(); Find.PlaySettings.useWorkPriorities = true;
                foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList()) p.Destroy(DestroyMode.Vanish);
                foreach (var fixtureCell in new CellRect(map.Center.x - 16, map.Center.z - 16, 33, 33).Cells)
                {
                    foreach (var t in fixtureCell.GetThingList(map).Where(t => !(t is Pawn)).ToList()) t.Destroy(DestroyMode.Vanish);
                    map.terrainGrid.SetTerrain(fixtureCell, TerrainDefOf.Soil); map.roofGrid.SetRoof(fixtureCell, null); map.fogGrid.Unfog(fixtureCell);
                }
                var pawns = new List<Pawn>();
                for (int i = 0; i < 4; i++)
                {
                    Pawn pawn;
                    do { pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false)); }
                    while (pawn.WorkTypeIsDisabled(DefDatabase<WorkTypeDef>.GetNamed("Cooking")) || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Growing) || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Mining));
                    pawn.story.traits.allTraits.Clear();
                    foreach (var skill in pawn.skills.skills) { skill.Level = 5; skill.passion = Passion.None; }
                    Healthy(pawn); GenSpawn.Spawn(pawn, map.Center + new IntVec3(i, 0, 0), map);
                    pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
                    foreach (var type in DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w => !pawn.WorkTypeIsDisabled(w))) pawn.workSettings.SetPriority(type, 3);
                    pawns.Add(pawn);
                }
                pawns[0].skills.GetSkill(SkillDefOf.Cooking).Level = 6;
                pawns[1].skills.GetSkill(SkillDefOf.Cooking).passion = Passion.Major;
                pawns[2].skills.GetSkill(SkillDefOf.Plants).Level = 18;
                pawns[3].skills.GetSkill(SkillDefOf.Medicine).Level = 18;
                var state = new ColonyState { ColonistCount = 4, DailyFoodNutrition = 6, EstimatedFoodDays = .3f, DownedColonists = 0 };
                WorkPriorityManager.Apply(map, state, work);
                Check(WorkPriorityManager.RawPriority(pawns[1], DefDatabase<WorkTypeDef>.GetNamed("Cooking")) == 1, "Passionate cook not selected.");
                Check(pawns.Count(p => WorkPriorityManager.RawPriority(p, DefDatabase<WorkTypeDef>.GetNamed("Cooking")) == 1) == 1, "Cooking specialist not bounded.");
                Pass("food shortage promotes cooking using passion and skill, with bounded specialists");
                pawns[2].needs.food.CurLevel = .05f;
                WorkPriorityManager.Apply(map, state, work);
                Check(WorkPriorityManager.RawPriority(pawns[2], WorkTypeDefOf.Growing) == 1 && WorkPriorityManager.RawPriority(pawns[2], WorkTypeDefOf.Mining) == 0, "Hunger prevented emergency food production or allowed unrelated work.");
                Healthy(pawns[2]);
                Pass("hungry but otherwise fit farmer can produce emergency food while unrelated work stays suspended");
                state.EstimatedFoodDays = 5;
                WorkPriorityManager.Apply(map, state, work);
                Check(WorkPriorityManager.RawPriority(pawns[1], DefDatabase<WorkTypeDef>.GetNamed("Cooking")) > 1, "Food urgency remained fixed.");
                state.Resources[ThingDefOf.Steel.defName] = 0;
                WorkPriorityManager.Apply(map, state, work, gathering: true);
                Check(pawns.All(p => WorkPriorityManager.RawPriority(p, WorkTypeDefOf.Mining) > 1), "Empty mining backlog promoted idle work.");
                var cell = map.Center + new IntVec3(12, 0, 0);
                foreach (var t in cell.GetThingList(map).ToList()) t.Destroy(DestroyMode.Vanish);
                GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MineableSteel")), cell, map);
                map.designationManager.AddDesignation(new Designation(cell, DesignationDefOf.Mine));
                WorkPriorityManager.Apply(map, state, work, gathering: true);
                Check(pawns.Any(p => WorkPriorityManager.RawPriority(p, WorkTypeDefOf.Mining) == 1), "Mining backlog not promoted.");
                Pass("food priorities relax after replenishment; mining promotion requires actual pending work");
                var buildCell = map.Center + new IntVec3(12, 0, 4);
                foreach (var t in buildCell.GetThingList(map).ToList()) t.Destroy(DestroyMode.Vanish);
                GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Wall, buildCell, map, Rot4.North, Faction.OfPlayer, ThingDefOf.WoodLog);
                WorkPriorityManager.Apply(map, state, work, construction: true);
                Check(pawns.Any(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction) && WorkPriorityManager.RawPriority(p, WorkTypeDefOf.Construction) == 1), "Essential construction not promoted.");
                var treeCell = map.Center + new IntVec3(12, 0, 7);
                foreach (var t in treeCell.GetThingList(map).ToList()) t.Destroy(DestroyMode.Vanish);
                var tree = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Plant_TreeOak")); GenSpawn.Spawn(tree, treeCell, map);
                map.designationManager.AddDesignation(new Designation(tree, DesignationDefOf.CutPlant));
                WorkPriorityManager.Apply(map, state, work, gathering: true);
                Check(pawns.Any(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting) && WorkPriorityManager.RawPriority(p, WorkTypeDefOf.PlantCutting) == 1), "Tree cutting backlog not promoted.");
                var fireCell = map.Center + new IntVec3(0, 0, 3);
                var fire = GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Fire")), fireCell, map);
                WorkPriorityManager.Apply(map, state, work);
                Check(pawns.Any(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Firefighter) && WorkPriorityManager.RawPriority(p, WorkTypeDefOf.Firefighter) == 1), "Local fire not promoted.");
                fire.Destroy(DestroyMode.Vanish);
                Pass("essential blueprint, designated tree cutting and local fire change priorities");
                var tailoringCell = map.Center + new IntVec3(-12, 0, 4);
                foreach (var c in new CellRect(tailoringCell.x - 2, tailoringCell.z - 2, 6, 6).Cells)
                {
                    foreach (var t in c.GetThingList(map).Where(t => !(t is Pawn)).ToList()) t.Destroy(DestroyMode.Vanish);
                    map.terrainGrid.SetTerrain(c, TerrainDefOf.Soil);
                }
                var tailoring = (Building_WorkTable)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("HandTailoringBench"), ThingDefOf.WoodLog);
                tailoring.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(tailoring, tailoringCell, map);
                tailoring.BillStack.AddBill(tailoring.def.AllRecipes.First(r => r.workSkill == SkillDefOf.Crafting).MakeNewBill());
                pawns[1].skills.GetSkill(SkillDefOf.Crafting).Level = 18;
                WorkPriorityManager.Apply(map, state, work);
                var tailorWork = DefDatabase<WorkTypeDef>.GetNamed("Tailoring");
                Check(WorkPriorityManager.RawPriority(pawns[1], tailorWork) == 2, "Active tailoring bill did not select skilled crafter.");
                Pass("pending tailoring bill selects a skilled production specialist");
                var stoveCell = map.Center + new IntVec3(-12, 0, -5);
                var stove = (Building_WorkTable)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("FueledStove"));
                stove.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(stove, stoveCell, map);
                stove.BillStack.AddBill(DefDatabase<RecipeDef>.GetNamed("CookMealSimple").MakeNewBill());
                WorkPriorityManager.Apply(map, state, work, construction: true);
                Check(pawns.Any(p => WorkPriorityManager.RawPriority(p, DefDatabase<WorkTypeDef>.GetNamed("Cooking")) == 1 && WorkPriorityManager.RawPriority(p, WorkTypeDefOf.Construction) > 1), "Ready-meal shortage lost priority to construction despite abundant raw food.");
                stove.Destroy(DestroyMode.Vanish);
                Pass("ready-meal shortage protects cook from construction even when total nutrition reserves are high");
                var injury = (Hediff_Injury)pawns[0].health.AddHediff(DefDatabase<HediffDef>.GetNamed("Bruise"), pawns[0].RaceProps.body.AllParts.First(p => p.def.defName == "Torso"));
                injury.Severity = 15;
                WorkPriorityManager.Apply(map, state, work);
                Check(WorkPriorityManager.RawPriority(pawns[3], WorkTypeDefOf.Doctor) == 1, "Best doctor did not respond to injury.");
                Check(WorkPriorityManager.RawPriority(pawns[0], DefDatabase<WorkTypeDef>.GetNamed("PatientBedRest")) == 1, "Medical bed rest not prioritized.");
                foreach (var p in pawns.Skip(1))
                {
                    var bruise = (Hediff_Injury)p.health.AddHediff(DefDatabase<HediffDef>.GetNamed("Bruise"), p.RaceProps.body.corePart);
                    bruise.Severity = 3;
                }
                WorkPriorityManager.Apply(map, state, work);
                Check(pawns.Any(p => WorkPriorityManager.RawPriority(p, WorkTypeDefOf.Doctor) == 1), "Minor injuries on every colonist disabled all doctors.");
                foreach (var p in pawns) Healthy(p);
                Pass("minor injuries across the colony retain an able doctor instead of disabling all medical work");
                Healthy(pawns[0]); pawns[1].needs.rest.CurLevel = .1f;
                WorkPriorityManager.Apply(map, state, work);
                Check(WorkPriorityManager.RawPriority(pawns[1], DefDatabase<WorkTypeDef>.GetNamed("Cooking")) == 0, "Exhausted worker still cooking.");
                Pass("injury selects doctor and patient bed rest; exhausted workers leave productive roles");
                ScheduleManager.Apply(map, state, schedules, true);
                int current = GenLocalDate.HourOfDay(pawns[1]);
                Check(pawns[1].timetable.GetAssignment(current) == TimeAssignmentDefOf.Sleep, "Critical sleep not scheduled now.");
                Check(schedules.All(s => !s.Applied.Contains(TimeAssignmentDefOf.Meditate)), "Core-only colonist assigned meditation.");
                Healthy(pawns[1]); pawns[2].needs.joy.CurLevel = .1f;
                var cookChange = work.First(c => c.Pawn == pawns[1] && c.Work.defName == "Cooking");
                pawns[1].workSettings.SetPriority(cookChange.Work, cookChange.Original);
                WorkPriorityManager.Apply(map, state, work);
                Check(cookChange.UserOverride && WorkPriorityManager.RawPriority(pawns[1], cookChange.Work) == cookChange.Original, "Manual work priority not preserved.");
                ScheduleManager.Apply(map, state, schedules, true);
                Check(pawns[2].timetable.CurrentAssignment == TimeAssignmentDefOf.Joy, "Critical recreation not scheduled now.");
                Healthy(pawns[2]); pawns[2].needs.food.CurLevel = .05f;
                ScheduleManager.Apply(map, state, schedules, true);
                Check(pawns[2].timetable.CurrentAssignment == TimeAssignmentDefOf.Anything, "Hunger blocked by forced work.");
                Healthy(pawns[2]); pawns[0].story.traits.GainTrait(new Trait(DefDatabase<TraitDef>.GetNamed("NightOwl")));
                ScheduleManager.Apply(map, state, schedules, true);
                Check(pawns[0].timetable.GetAssignment(11) == TimeAssignmentDefOf.Sleep && pawns[0].timetable.GetAssignment(23) == TimeAssignmentDefOf.Work, "Night owl sleeps at wrong hours.");
                Pass("current-hour sleep, recreation and hunger overrides; night owl routine; no invalid meditation");
                pawns[2].needs.mood.CurLevel = .1f;
                WorkPriorityManager.Apply(map, state, work);
                ScheduleManager.Apply(map, state, schedules, true);
                Check(WorkPriorityManager.RawPriority(pawns[2], WorkTypeDefOf.Mining) == 0 && pawns[2].timetable.CurrentAssignment == TimeAssignmentDefOf.Joy, "Low mood ignored by work/schedule.");
                Healthy(pawns[2]);
                if (ModsConfig.RoyaltyActive)
                {
                    pawns[2].ChangePsylinkLevel(1, false); ScheduleManager.Apply(map, state, schedules, true);
                    Check(pawns[2].timetable.GetAssignment(12) == TimeAssignmentDefOf.Meditate, "Psylink colonist not assigned meditation.");
                    Healthy(pawns[2]);
                    Pass("low mood removes productive work; Royalty psylink enables meditation");
                }
                state.HostilePawnCount = 1;
                ScheduleManager.Apply(map, state, schedules, true);
                var manualChange = schedules.First(s => s.Pawn == pawns[0]);
                int manualHour = Enumerable.Range(0, 24).First(h => manualChange.Original[h] != manualChange.Applied[h]);
                pawns[0].timetable.SetAssignment(manualHour, manualChange.Original[manualHour]);
                state.HostilePawnCount = 0;
                pawns[3].needs.rest.CurLevel = .1f;
                ScheduleManager.Apply(map, state, schedules, true);
                foreach (var s in schedules) s.RecoveryUntil = Find.TickManager.TicksGame - 1;
                ScheduleManager.Apply(map, state, schedules, true);
                Check(schedules.First(s => s.Pawn == pawns[3]).Recovery, "Recovery timer ignored exhaustion.");
                Check(manualChange.UserOverride[manualHour], "Manual return to original assignment was not respected.");
                Pass("emergency transitions to recovery until needs recover; manual return to original hour remains protected");
                stage = 1; GameDataSaveLoader.SaveGame("WorkScheduleRoundtrip"); GameDataSaveLoader.LoadGame("WorkScheduleRoundtrip");
            }
            catch (Exception e) { stage = 99; Log.Error("[AutonomousRim.WorkScheduleTests] FAIL: " + e); }
        }
    }
}
