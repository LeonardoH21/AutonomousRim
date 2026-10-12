using System;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class ProgressDiagnosticsChecks : MapComponent
    {
        private static int stage, started;
        private static bool workSampled, workVerified;
        private static float workLeft;
        private Building_WorkTable table;
        private Pawn crafter;
        private Bill_Production bill;
        public ProgressDiagnosticsChecks(Map map) : base(map) { }
        public override void ExposeData()
        { Scribe_References.Look(ref table, "diagnosticTable"); Scribe_References.Look(ref crafter, "diagnosticCrafter"); Scribe_References.Look(ref bill, "diagnosticBill"); }
        private void Check(bool value, string text) { if (!value) throw new Exception(text); Log.Message("[ProgressDiagnostics] PASS: " + text); }
        public override void MapComponentUpdate()
        {
            if (!GenCommandLine.CommandLineArgPassed("autonomousrimprogressdiagnosticstest")) return;
            foreach (var window in Find.WindowStack.Windows.Where(w => w.forcePause).ToList()) window.Close(false);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
        }
        public override void MapComponentTick()
        {
            if (stage == 99 || !EliteFixtureBaseline.Ready || !GenCommandLine.CommandLineArgPassed("autonomousrimprogressdiagnosticstest") || Find.TickManager.TicksGame < 300) return;
            try
            {
                var ai = map.GetComponent<AutonomousRimMapComponent>();
                if (stage == 2)
                {
                    Check(ai.Progress.Get("bill:" + bill.GetUniqueLoadID()).Completions >= 1, "native completion counter survives save/load");
                    Check(ai.Progress.Events.Any(e => e.Text == "Roundtrip marker"), "diagnostic ledger survives save/load");
                    Check(ai.Progress.Events.Count <= 64, "ledger remains bounded");
                    EliteFixtureBaseline.VerifyOriginalsPresent(map);
                    stage = 99; Log.Message("[ProgressDiagnostics] DONE"); return;
                }
                if (stage == 1)
                {
                    var entry = ai.Progress.Get("bill:" + bill.GetUniqueLoadID());
                    var driver = crafter.jobs.curDriver as JobDriver_DoBill;
                    if (!workSampled && driver != null && crafter.CurJob.bill == bill && driver.workLeft > 0)
                    {
                        ColonyProgressMonitor.Sample(map, ai); workLeft = driver.workLeft;
                        entry.EligibleTicks = ProgressState.StallTicks; workSampled = true;
                    }
                    else if (workSampled && !workVerified && driver != null && driver.workLeft < workLeft - 1 && driver.workLeft > 0)
                    {
                        ColonyProgressMonitor.Sample(map, ai);
                        Check(!entry.Stalled && entry.EligibleTicks < ProgressState.StallTicks, "native decrease in remaining recipe work clears stall before completion");
                        workVerified = true;
                    }
                    if (entry.Completions > 0)
                    {
                        Check(workVerified, "in-flight native recipe progress was observed");
                        ColonyProgressMonitor.Sample(map, ai);
                        Check(map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("BlocksMarble")).Count > 0, "native bill produced actual marble blocks");
                        Check(!entry.Stalled, "working/finished production is not a stall");
                        ai.Progress.Record(Find.TickManager.TicksGame, "Roundtrip marker");
                        stage = 2; GameDataSaveLoader.SaveGame("ProgressDiagnosticsEvidence"); GameDataSaveLoader.LoadGame("ProgressDiagnosticsEvidence");
                    }
                    else if (Find.TickManager.TicksGame - started > 10000) throw new Exception("No native stonecutting completion; job=" + crafter.CurJob?.def);
                    return;
                }
                var roster = EliteFixtureBaseline.Roster(map);
                ai.DisableAll(); Find.PlaySettings.useWorkPriorities = true;
                foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().SelectMany(t => t.BillStack.Bills)) b.suspended = true;
                var policy = new ProgressState();
                for (int t = 0; t <= 10800; t += 600) policy.Observe("test", "Fixture", 0, true, "ready", "Available", t);
                Check(policy.Get("test").Stalled, "watchdog detects four available work hours without progress");
                policy.Observe("test", "Fixture", 1, true, "ready", "Available", 11400);
                Check(!policy.Get("test").Stalled && policy.Get("test").EligibleTicks == 0, "real progress clears the stall");
                for (int t = 12000; t < 30000; t += 600) policy.Observe("test", "Fixture", 1, false, "sleep", "Sleeping", t);
                Check(policy.Get("test").EligibleTicks == 0 && !policy.Get("test").Stalled, "sleep/emergency do not accumulate stall time");
                policy.Observe("room", "Room", 0, false, "blocked", "Waiting", 30000, true);
                int eventCount = policy.Events.Count;
                policy.Observe("room", "Room", 0, false, "blocked", "Waiting", 30600, true);
                Check(policy.Events.Count == eventCount, "unchanged construction stall does not flood the ledger");
                for (int i = 0; i < 100; i++) policy.Record(i, "Event " + i);
                Check(policy.Events.Count == 64, "history evicts old events at 64 entries");
                foreach (var p in roster)
                {
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced); p.drafter.Drafted = false;
                    foreach (var work in DefDatabase<WorkTypeDef>.AllDefsListForReading) p.workSettings.SetPriority(work, 0);
                    for (int hour = 0; hour < 24; hour++) p.timetable.SetAssignment(hour, TimeAssignmentDefOf.Anything);
                    p.needs.food.CurLevel = 1; p.needs.rest.CurLevel = 1; p.needs.joy.CurLevel = 1;
                }
                crafter = roster.First(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Crafting));
                var cell = map.Center + new IntVec3(20, 0, 20);
                foreach (var c in new CellRect(cell.x - 7, cell.z - 7, 15, 15).Cells)
                {
                    foreach (var thing in c.GetThingList(map).Where(t => !(t is Pawn)).ToList()) thing.Destroy(DestroyMode.Vanish);
                    map.terrainGrid.SetTerrain(c, TerrainDefOf.Soil); map.roofGrid.SetRoof(c, null); map.fogGrid.Unfog(c);
                }
                table = (Building_WorkTable)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("TableStonecutter"), ThingDefOf.WoodLog);
                table.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(table, cell, map);
                bill = new Bill_Production(DefDatabase<RecipeDef>.GetNamed("Make_StoneBlocksAny"));
                bill.repeatMode = BillRepeatModeDefOf.RepeatCount; bill.repeatCount = 1; bill.ingredientSearchRadius = 6;
                table.BillStack.AddBill(bill);
                var stove = (Building_WorkTable)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ElectricStove"));
                stove.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(stove, cell + new IntVec3(-4, 0, 0), map);
                var cooking = new Bill_Production(DefDatabase<RecipeDef>.GetNamed("CookMealSimple")); stove.BillStack.AddBill(cooking);
                var powerDiagnostic=BillDiagnostics.Inspect(map,stove,cooking);
                Check(powerDiagnostic.Code=="bench" && powerDiagnostic.Detail.Contains("energia"),"unpowered electric bench identifies power specifically");
                stove.Destroy(DestroyMode.Vanish);
                Check(BillDiagnostics.Inspect(map, table, bill).Code == "worker", "unassigned production identifies missing worker");
                crafter.workSettings.SetPriority(WorkTypeDefOf.Crafting, 1);
                // Explicit no-stock fixture: the original map contains chunks outside
                // the radius, which the richer diagnostic correctly distinguishes.
                foreach(var existing in map.listerThings.AllThings.Where(t=>t.def.category==ThingCategory.Item && bill.recipe.ingredients.Any(i=>i.filter.Allows(t))).ToList())existing.Destroy(DestroyMode.Vanish);
                var missing = BillDiagnostics.Inspect(map, table, bill);
                Check(missing.Detail.Contains("sem estoque conhecido"), "missing stock explanation is specific: "+missing.Detail);
                Check(missing.Code == "ingredients", "native ingredient search identifies missing stock in radius (" + missing.Code + ": " + missing.Detail + ")");
                var chunk = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ChunkMarble"));
                GenSpawn.Spawn(chunk, cell + new IntVec3(3, 0, 0), map); bill.ingredientFilter.SetAllow(chunk.def, false);
                Check(BillDiagnostics.Inspect(map, table, bill).Code == "ingredients", "ingredient excluded by bill filter is unavailable to native search");
                Check(BillDiagnostics.Inspect(map,table,bill).Detail.Contains("excluído pelo filtro"),"bill filter exclusion is distinguished from missing stock");
                bill.ingredientFilter.SetAllow(chunk.def, true);
                chunk.Position=cell+new IntVec3(9,0,0);map.fogGrid.Unfog(chunk.Position);
                Check(BillDiagnostics.Inspect(map,table,bill).Detail.Contains("fora do raio"),"ingredient beyond native search radius is distinguished");
                chunk.Position=cell+new IntVec3(3,0,0);
                Check(BillDiagnostics.Inspect(map, table, bill).Code == "ready", "allowed reachable ingredient makes bill ready");
                bill.suspended = true;
                Check(BillDiagnostics.Inspect(map, table, bill).Code == "suspended" && bill.suspended, "manual suspension is reported and preserved");
                bill.suspended = false; bill.allowedSkillRange = new IntRange(0, 0); crafter.skills.GetSkill(SkillDefOf.Crafting).Level = 20;
                Check(BillDiagnostics.Inspect(map, table, bill).Code == "skill", "bill skill restriction is diagnosed");
                bill.allowedSkillRange = new IntRange(0, 20);
                bill.repeatMode = BillRepeatModeDefOf.TargetCount; bill.targetCount = 0; bill.pauseWhenSatisfied = true; bill.paused = false;
                Check(BillDiagnostics.Inspect(map, table, bill).Code == "satisfied" && !bill.paused, "satisfied bill does not mutate native pause flag");
                bill.repeatMode = BillRepeatModeDefOf.RepeatCount; bill.pauseWhenSatisfied = false;
                for (int hour = 0; hour < 24; hour++) crafter.timetable.SetAssignment(hour, TimeAssignmentDefOf.Sleep);
                Check(BillDiagnostics.Inspect(map, table, bill).Code == "shift", "sleeping worker does not trigger an eligible production stall");
                for (int hour = 0; hour < 24; hour++) crafter.timetable.SetAssignment(hour, TimeAssignmentDefOf.Anything);
                crafter.jobs.EndCurrentJob(JobCondition.InterruptForced);
                foreach(var existing in map.listerBuildings.AllBuildingsColonistOfClass<Building_ResearchBench>().ToList())existing.Destroy(DestroyMode.Vanish);
                var research=(Building_ResearchBench)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SimpleResearchBench"),ThingDefOf.WoodLog);
                research.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(research,cell+new IntVec3(5,0,0),map);
                var selected=DefDatabase<ResearchProjectDef>.AllDefsListForReading.FirstOrDefault(r=>!r.IsFinished && r.CanStartNow && r.CanBeResearchedAt(research,false));
                Check(selected!=null,"fixture provides an unfinished available research compatible with native simple bench");
                Find.ResearchManager.SetCurrentProject(selected);
                Check(ResearchDiagnostics.Inspect(map,ai).Code=="worker","research diagnoses missing assigned worker");
                var scientist=roster.First(p=>p!=crafter && !p.WorkTypeIsDisabled(WorkTypeDefOf.Research));scientist.workSettings.SetPriority(WorkTypeDefOf.Research,3);
                Check(ResearchDiagnostics.Inspect(map,ai).Eligible,"reachable research bench is eligible without issuing a job");
                scientist.workSettings.SetPriority(WorkTypeDefOf.Construction,1);
                Check(ResearchDiagnostics.Inspect(map,ai).Detail.Contains("prioridades concorrentes"),"research exposes competing work priorities");
                map.areaManager.TryMakeNewAllowed(out var limitedArea);
                limitedArea[scientist.Position]=true;scientist.playerSettings.AreaRestrictionInPawnCurrentMap=limitedArea;
                var blockedResearch=ResearchDiagnostics.Inspect(map,ai);
                Check(blockedResearch.Code=="route" && !blockedResearch.Eligible,"research outside allowed area does not accumulate eligible stall time: "+blockedResearch.Code+" "+blockedResearch.Detail);
                scientist.playerSettings.AreaRestrictionInPawnCurrentMap=null;limitedArea.Delete();
                var obstruction=(Building)ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.WoodLog);obstruction.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(obstruction,research.InteractionCell,map);
                Check(ResearchDiagnostics.Inspect(map,ai).Code=="route","blocked research interaction cell is distinguished");obstruction.Destroy(DestroyMode.Vanish);
                var reserver=roster.First(p=>p!=scientist && p!=crafter);
                reserver.Reserve(research,JobMaker.MakeJob(JobDefOf.Research,research));
                Check(ResearchDiagnostics.Inspect(map,ai).Code=="reservation","reserved research bench is distinguished from missing worker");
                map.reservationManager.ReleaseAllForTarget(research);
                scientist.workSettings.SetPriority(WorkTypeDefOf.Research,0);scientist.workSettings.SetPriority(WorkTypeDefOf.Construction,0);research.Destroy(DestroyMode.Vanish);
                var originalProjects=ai.BaseProjects.ToList();var temporaryProjects=(System.Collections.Generic.List<RoomProject>)ai.BaseProjects;temporaryProjects.Clear();
                temporaryProjects.Add(new RoomProject{Kind="Fixture",Origin=cell,InteriorSize=1,LayoutSlot="fixture:a",RequiresRoof=false,Stalled=true});
                temporaryProjects.Add(new RoomProject{Kind="Fixture",Origin=cell,InteriorSize=1,LayoutSlot="fixture:b",RequiresRoof=false,Stalled=true});
                AccessTools.Field(typeof(AutonomousRimMapComponent),"baseAutomation").SetValue(ai,true);
                crafter.workSettings.SetPriority(WorkTypeDefOf.Construction,1);
                for(int h=0;h<24;h++)crafter.timetable.SetAssignment(h,TimeAssignmentDefOf.Sleep);
                ColonyProgressMonitor.Sample(map,ai);
                Check(ai.Progress.Entries.Any(e=>e.Key=="room:fixture:a") && ai.Progress.Entries.Any(e=>e.Key=="room:fixture:b"),"same-origin industrial projects retain independent progress entries");
                Check(!ai.Progress.Get("room:fixture:a").Stalled && !ai.Progress.Get("room:fixture:b").Stalled,"sleeping builders do not display an eligible construction stall");
                for(int h=0;h<24;h++)crafter.timetable.SetAssignment(h,TimeAssignmentDefOf.Anything);
                ColonyProgressMonitor.Sample(map,ai);
                Check(ai.Progress.Get("room:fixture:a").Stalled,"existing construction watchdog is exposed when a capable builder is on shift");
                crafter.workSettings.SetPriority(WorkTypeDefOf.Construction,0);AccessTools.Field(typeof(AutonomousRimMapComponent),"baseAutomation").SetValue(ai,false);
                temporaryProjects.Clear();temporaryProjects.AddRange(originalProjects);
                started = Find.TickManager.TicksGame; stage = 1;
            }
            catch (Exception e) { stage = 99; Log.Error("[ProgressDiagnostics] FAIL: " + e); }
        }
    }
}
