using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class StrategicChecks : MapComponent
    {
        private static int stage;
        public StrategicChecks(Map map):base(map){}
        private static void Check(bool ok,string msg){if(!ok)throw new InvalidOperationException(msg);}
        public override void MapComponentTick()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimstrategytest") || stage==99 || Find.TickManager.TicksGame%60!=0 || map.mapPawns.FreeColonistsSpawnedCount==0)return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(stage==1)
                {
                    Check(!ai.BaseAutomation && !ai.StrategyAutomation && ai.ScheduleAutomation,"Saved automation toggles changed on load.");
                    Check(ai.Strategy.Landing.IsValid && ai.Strategy.Route.Count>0,"Strategic anchor/route missing on load.");
                    Check(ai.Strategy.HorizonDays==StrategicPlan.DefaultHorizonDays && ai.Strategy.EstimatedDaysRemaining>=0,"500-day planning horizon missing on load.");
                    Check(!string.IsNullOrEmpty(ai.Strategy.StabilityStatus) && !string.IsNullOrEmpty(ai.Strategy.CurrentFocus),"Stability/focus state missing on load.");
                    Check(!string.IsNullOrEmpty(ai.Strategy.MountainPlanStatus),"Mountain planning status missing on load.");
                    Check(ai.Strategy.ResearchOverride,"Manual research override missing on load.");
                    Check(!ai.Strategy.Route.Any(StrategicPlanner.IsVictoryResearch) && ai.Strategy.Goals.Count(g=>g.Id.StartsWith("defense-checkpoint-"))==5,"Automatic strategy lost defense checkpoints or advanced to ship research.");
                    Check(ai.BaseProjects.SelectMany(BaseConstructionManager.Tasks).Any(t=>t.Def.defName=="HiTechResearchBench"),"Advanced upgrade missing on load.");
                    stage=99;Log.Message("[AutonomousRim.StrategyTests] PASS: prerequisite DAG including hidden edges, defense checkpoints before ship research, needs and goals, native research selection without free progress, manual ownership, additive validated upgrades without duplication, bounded allow/reprohibition and native save/load.");return;
                }
                Check(ai.StrategyAutomation && ai.LootAutomation,"New colony strategy/initial allow defaults disabled.");
                ai.DisableAll();
                Find.PlaySettings.useWorkPriorities=true;
                foreach(var capable in map.mapPawns.FreeColonistsSpawned)
                {
                    capable.story.Childhood=DefDatabase<BackstoryDef>.AllDefsListForReading.First(b=>b.slot==BackstorySlot.Childhood && b.workDisables==WorkTags.None);
                    if(capable.story.Adulthood!=null)capable.story.Adulthood=DefDatabase<BackstoryDef>.AllDefsListForReading.First(b=>b.slot==BackstorySlot.Adulthood && b.workDisables==WorkTags.None);
                    capable.Notify_DisabledWorkTypesChanged();capable.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
                    foreach(var skill in capable.skills.skills)skill.Level=20;
                }
                var dynamicState=ColonyStateScanner.Scan(map);dynamicState.DailyFoodNutrition=1f;dynamicState.EstimatedFoodDays=0.25f;
                var dynamicChanges=new List<WorkPriorityChange>();
                WorkPriorityManager.Apply(map,dynamicState,dynamicChanges,false,true,false);
                var cooking=DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
                Check(map.mapPawns.FreeColonistsSpawned.Any(p=>cooking!=null && !p.WorkTypeIsDisabled(cooking) && p.workSettings.GetPriority(cooking)<=1),"Food shortage did not raise Cooking: days="+dynamicState.EstimatedFoodDays+" daily="+dynamicState.DailyFoodNutrition+" def="+(cooking?.defName??"null")+" eligible="+map.mapPawns.FreeColonistsSpawned.Count(WorkPriorityManager.CanWork)+" disabled="+string.Join(",",map.mapPawns.FreeColonistsSpawned.Select(p=>p.WorkTypeIsDisabled(cooking)))+" priorities="+string.Join(",",map.mapPawns.FreeColonistsSpawned.Select(p=>p.workSettings.GetPriority(cooking))));
                Check(map.mapPawns.FreeColonistsSpawned.Any(p=>!p.WorkTypeIsDisabled(WorkTypeDefOf.Growing) && p.workSettings.GetPriority(WorkTypeDefOf.Growing)<=1),"Food shortage did not raise Growing: "+string.Join(",",map.mapPawns.FreeColonistsSpawned.Select(p=>p.workSettings.GetPriority(WorkTypeDefOf.Growing))));
                dynamicState.EstimatedFoodDays=3f;dynamicState.DownedColonists=1;
                WorkPriorityManager.Apply(map,dynamicState,dynamicChanges,false,false,false);
                Check(map.mapPawns.FreeColonistsSpawned.Any(p=>!p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && p.workSettings.GetPriority(WorkTypeDefOf.Doctor)<=1),"Medical urgency did not raise Doctor.");
                var schedule=new List<ScheduleChange>();dynamicState.DownedColonists=0;dynamicState.HostilePawnCount=1;
                string emergencyStatus=ScheduleManager.Apply(map,dynamicState,schedule,true);
                Check(emergencyStatus.Contains("emergência") && map.mapPawns.FreeColonistsSpawned.All(p=>p.timetable.GetAssignment(12)==TimeAssignmentDefOf.Work || p.timetable.GetAssignment(12)==TimeAssignmentDefOf.Sleep),"Emergency schedule did not prioritize work/rest.");
                var manualPawn=map.mapPawns.FreeColonistsSpawned.First();manualPawn.timetable.SetAssignment(12,TimeAssignmentDefOf.Sleep);
                dynamicState.HostilePawnCount=0;string recoveryStatus=ScheduleManager.Apply(map,dynamicState,schedule,true);
                Check(recoveryStatus.Contains("Recuperação") && manualPawn.timetable.GetAssignment(12)==TimeAssignmentDefOf.Sleep,"Recovery schedule or manual schedule protection failed.");
                ai.SetScheduleAutomation(true);
                if(Find.ResearchManager.GetProject()!=null)Find.ResearchManager.StopProject(Find.ResearchManager.GetProject());
                Find.ResearchManager.ResetAllProgress();
                var pawn=map.mapPawns.FreeColonistsSpawned.First();
                var anchor=pawn.Position-new IntVec3(30,0,22);
                foreach(var c in RingBasePlanner.Envelope(anchor).ExpandedBy(1))
                {
                    Check(c.InBounds(map),"Fixture outside map.");
                    foreach(var t in c.GetThingList(map).Where(t=>!(t is Pawn)).ToList()){if(t.def.destroyable)t.Destroy();else t.DeSpawn();}
                    map.terrainGrid.SetTerrain(c,DefDatabase<TerrainDef>.GetNamed("Soil"));map.roofGrid.SetRoof(c,null);map.fogGrid.Unfog(c);
                }
                foreach(var hostile in map.mapPawns.AllPawnsSpawned.Where(p=>p.HostileTo(Faction.OfPlayer)).ToList())hostile.DeSpawn();
                var shipNames=new[]{"ShipBasics","ShipCryptosleep","ShipReactor","ShipEngine","ShipComputerCore","ShipSensorCluster"};
                var route=StrategicPlanner.ResearchRoute(shipNames.Select(n=>DefDatabase<ResearchProjectDef>.GetNamed(n)));
                Check(shipNames.All(n=>route.Any(r=>r.defName==n)),"Missing ship research def.");
                Check(route.Distinct().Count()==route.Count,"Duplicate route nodes.");
                foreach(var r in route)foreach(var dep in StrategicPlanner.Dependencies(r))Check(dep.IsFinished || route.IndexOf(dep)<route.IndexOf(r),"Dependency placed after project: "+r.defName);
                ai.SetStrategyAutomation(true);
                Check(ai.Strategy.HorizonDays==StrategicPlan.DefaultHorizonDays && ai.Strategy.EstimatedDaysRemaining>=0,"Planner did not initialize the 500-day horizon.");
                Check(!string.IsNullOrEmpty(ai.Strategy.StabilityStatus) && !string.IsNullOrEmpty(ai.Strategy.CurrentFocus) && !string.IsNullOrEmpty(ai.Strategy.NextFocus),"Planner did not publish dynamic focus and stability state.");
                Check(ai.Strategy.Goals.Any(g=>g.Id=="stability" && !string.IsNullOrEmpty(g.ResourceNeed) && !string.IsNullOrEmpty(g.Risk)),"Stability goal is missing resource/risk context.");
                Check(!string.IsNullOrEmpty(ai.Strategy.MountainPlanStatus),"Planner did not publish the mountain assessment.");
                var selected=Find.ResearchManager.GetProject();
                Check(selected!=null && selected.CanStartNow,"No native available research selected: "+ai.Strategy.ResearchStatus);
                Check(Find.ResearchManager.GetProgress(selected)==0,"Planner granted free research progress.");
                var manual=DefDatabase<ResearchProjectDef>.AllDefsListForReading.First(r=>r!=selected && r.CanStartNow);
                Find.ResearchManager.SetCurrentProject(manual);ai.EvaluateStrategy();
                Check(ai.Strategy.ResearchOverride && Find.ResearchManager.GetProject()==manual,"Manual research overwritten.");
                ai.SetStrategyAutomation(false);Check(Find.ResearchManager.GetProject()==manual,"Disable removed manual research.");
                ai.Strategy.Landing=pawn.Position;
                var supplies=new List<Thing>();
                for(int i=0;i<20;i++)
                {var t=ThingMaker.MakeThing(ThingDefOf.Steel);t.stackCount=10;GenSpawn.Spawn(t,pawn.Position+new IntVec3(i+1,0,0),map);t.SetForbidden(true,false);supplies.Add(t);}
                var far=ThingMaker.MakeThing(ThingDefOf.Steel);GenSpawn.Spawn(far,pawn.Position+new IntVec3(35,0,0),map);far.SetForbidden(true,false);
                var history=map.listerThings.AllThings.Where(t=>t.def.category==ThingCategory.Item && !supplies.Contains(t) && t!=far).ToList();
                LootAccessManager.Apply(map,ColonyStateScanner.Scan(map),false,new List<RoomProject>(),new List<ManagedApparel>(),p=>true,history,out int released);
                Check(released==LootAccessManager.MaxStacksPerCycle && supplies.Count(t=>!t.IsForbidden(Faction.OfPlayer))==released && far.IsForbidden(Faction.OfPlayer),"Initial allow cap or landing radius failed.");
                var reblocked=supplies.First(t=>!t.IsForbidden(Faction.OfPlayer));reblocked.SetForbidden(true,false);
                LootAccessManager.Apply(map,ColonyStateScanner.Scan(map),false,new List<RoomProject>(),new List<ManagedApparel>(),p=>true,history,out released);
                Check(reblocked.IsForbidden(Faction.OfPlayer),"Manual reprohibition overridden.");
                foreach(string n in new[]{"Electricity","Batteries","AirConditioning","ComplexFurniture","ComplexClothing","Stonecutting","MicroelectronicsBasics","MultiAnalyzer","Fabrication","DrugProduction","Machining","Smithing"})
                    Find.ResearchManager.FinishProject(DefDatabase<ResearchProjectDef>.GetNamed(n),false,null,false);
                var rooms=RingBasePlanner.Create(map,anchor,3);
                Check(rooms!=null,"Approved base invalid in strategy fixture.");
                Check(StrategicInfrastructure.Add(map,rooms),"No researched upgrades incorporated.");
                foreach(string n in new[]{"HiTechResearchBench","MultiAnalyzer","FabricationBench","TableMachining","ElectricSmithy","DrugLab"})
                    Check(rooms.SelectMany(BaseConstructionManager.Tasks).Any(t=>t.Def.defName==n),"Upgrade not placed: "+n);
                int count=rooms.SelectMany(BaseConstructionManager.Tasks).Count();
                Check(!StrategicInfrastructure.Add(map,rooms) && count==rooms.SelectMany(BaseConstructionManager.Tasks).Count(),"Repeated review duplicates upgrades.");
                Check(RingBasePlanner.Validate(map,rooms),"Upgrade geometry overlaps.");
                AccessTools.Field(typeof(AutonomousRimMapComponent),"baseProjects").SetValue(ai,rooms);
                ai.EvaluateStrategy();
                Check(ai.Strategy.Goals.Any(g=>g.Horizon=="Curto") && ai.Strategy.Goals.Any(g=>g.Horizon=="Médio") &&
                    ai.Strategy.Goals.Count(g=>g.Id.StartsWith("defense-checkpoint-"))==5 && ai.Strategy.Goals.Any(g=>g.Id=="winter"),"Missing staged defense or winter plan.");
                Check(ai.Strategy.Unlocks.Count>0,"Missing unlock inventory.");
                GameDataSaveLoader.SaveGame("StrategyRoundtrip");stage=1;GameDataSaveLoader.LoadGame("StrategyRoundtrip");
            }
            catch(Exception e){stage=99;Log.Error("[AutonomousRim.StrategyTests] FAIL: "+e);}
        }
    }
}
