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
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class GearProductionChecks : MapComponent
    {
        private static bool started,done;
        private static int start,initialVests,completed;
        private static System.Collections.Generic.HashSet<int> initialBuildings;
        private static ResearchProjectDef initialResearch;
        private static float initialPoints;
        private static IntVec3 constructionCell;
        private static ThingDef constructionDef;
        public GearProductionChecks(Map map):base(map){}
        public static void Completed(Bill_Production __instance,Pawn billDoer)
        {
            if(!started || done || !GenCommandLine.CommandLineArgPassed("autonomousrimgearproductiontest"))return;
            if(__instance.recipe.products?.Any(p=>p.thingDef.IsWeapon || p.thingDef.IsApparel)!=true)return;
            completed++;
            Log.Message("[GearProduction] CRAFTED: "+__instance.recipe.defName+" by "+billDoer.LabelShort);
        }
        public override void MapComponentTick()
        {
            if(done || !EliteFixtureBaseline.Ready || !GenCommandLine.CommandLineArgPassed("autonomousrimgearproductiontest"))return;
            try
            {
                int vests=DefenseProductionPlan.Fighters(map).Where(p=>!DefenseProductionPlan.Melee(p)).Sum(p=>p.apparel.WornApparel.Count(a=>a.def.defName=="Apparel_FlakVest"));
                if(!started)
                {
                    started=true;start=Find.TickManager.TicksGame;initialVests=vests;
                    initialBuildings=map.listerBuildings.allBuildingsColonist.Select(b=>b.thingIDNumber).ToHashSet();
                    initialResearch=Find.ResearchManager.GetProject(); initialPoints=initialResearch==null?0:Find.ResearchManager.GetProgress(initialResearch);
                    if(GenCommandLine.CommandLineArgPassed("autonomousrimcoordinationregression"))
                    {
                        // V4 starts with no blueprints/frames. Add a small native order,
                        // without granting materials, workers, priorities or jobs.
                        var roster=EliteFixtureBaseline.Roster(map);
                        constructionDef=DefDatabase<ThingDef>.GetNamed("Stool");
                        constructionCell=GenRadial.RadialCellsAround(roster.First().Position,18,true).First(c=>c.InBounds(map) &&
                            !c.Fogged(map) && c.GetEdifice(map)==null && c.GetThingList(map).Count==0 &&
                            GenConstruct.CanPlaceBlueprintAt(constructionDef,c,Rot4.North,map,stuffDef:ThingDefOf.WoodLog) &&
                            roster.Any(p=>p.CanReach(c,PathEndMode.Touch,Danger.None)));
                        GenConstruct.PlaceBlueprintForBuild(constructionDef,constructionCell,map,Rot4.North,Faction.OfPlayer,ThingDefOf.WoodLog);
                        Log.Message("[GearProduction] FIXTURE: one wooden stool blueprint at "+constructionCell+"; existing V4 resources only.");
                    }
                    var peaceful=DefDatabase<DifficultyDef>.GetNamed("Peaceful");
                    Find.Storyteller.difficultyDef=peaceful;Find.Storyteller.difficulty.CopyFrom(peaceful);
                    new Harmony("autonomousrim.gearproduction.proof").Patch(AccessTools.Method(typeof(Bill_Production),nameof(Bill_Production.Notify_IterationCompleted)),postfix:new HarmonyMethod(typeof(GearProductionChecks),nameof(Completed)));
                    Log.Message("[GearProduction] START: original V4 colonists/resources; isolated peaceful production test; equipped ranged vests="+vests);
                }
                int elapsed=Find.TickManager.TicksGame-start;
                if(elapsed%3000==0)
                {
                    var state=ColonyStateScanner.Scan(map);
                    Log.Message("[GearProduction] SNAPSHOT: elapsed="+elapsed+" foodDays="+state.EstimatedFoodDays+" vests="+vests+" completed="+completed+" research="+Find.ResearchManager.GetProject()?.defName);
                    foreach(var p in map.mapPawns.FreeColonistsSpawned)
                        Log.Message("[GearProduction] PAWN: "+p.LabelShort+" job="+p.CurJob?.def.defName+" recipe="+p.CurJob?.bill?.recipe.defName+" canWork="+WorkPriorityManager.CanWork(p)+" recovery="+WorkReadiness.NeedsRecovery(p)+" smith="+WorkPriorityManager.RawPriority(p,WorkTypeDefOf.Smithing)+" tailor="+WorkPriorityManager.RawPriority(p,DefDatabase<WorkTypeDef>.GetNamed("Tailoring"))+" craft="+WorkPriorityManager.RawPriority(p,WorkTypeDefOf.Crafting));
                    foreach(var t in map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Where(t=>t.BillStack.Bills.Any(b=>b.recipe.products?.Any(p=>p.thingDef.IsApparel || p.thingDef.IsWeapon)==true)))
                        Log.Message("[GearProduction] BENCH: "+t.def.defName+" power="+t.TryGetComp<CompPowerTrader>()?.PowerOn+" bills="+string.Join(";",t.BillStack.Bills.Select(b=>b.recipe.defName+":doNow="+b.ShouldDoNow())));
                }
                if(completed>=2 && vests>=Math.Min(3,DefenseProductionPlan.Fighters(map).Count(p=>!DefenseProductionPlan.Melee(p))) && vests>initialVests)
                {
                    if(GenCommandLine.CommandLineArgPassed("autonomousrimcoordinationregression"))
                    {
                        float gain=initialResearch==null?0:Find.ResearchManager.GetProgress(initialResearch)-initialPoints;
                        int built=map.listerBuildings.allBuildingsColonist.Count(b=>!initialBuildings.Contains(b.thingIDNumber));
                        bool constructed=constructionCell.GetEdifice(map)?.def==constructionDef;
                        if(gain<25 || !constructed)
                        {if(elapsed>=60000)throw new Exception("Gear completed but parallel research/construction did not progress: points="+gain+" newBuildings="+built+" fixtureBuilt="+constructed);return;}
                        Log.Message("[GearProduction] COORDINATION PASS: research gained="+gain+" actual new buildings="+built+" fixtureBuilt="+constructed+" equipment completed="+completed);
                    }
                    Log.Message("[GearProduction] DONE: native equipment production="+completed+" and ranged vests equipped "+initialVests+" -> "+vests+" in "+elapsed+" ticks; no resources, research, needs or jobs granted.");done=true;return;
                }
                if(elapsed>=60000)throw new Exception("Equipment production/equipping stalled after one day: completed="+completed+" vests="+vests);
            }
            catch(Exception e){done=true;Log.Error("[GearProduction] FAIL: "+e);}
        }
    }
}
