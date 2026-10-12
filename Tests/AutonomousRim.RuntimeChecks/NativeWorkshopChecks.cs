using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    // Resume the natural six-colonist campaign in a copy: no free research, materials,
    // completed structures, needs, skills or jobs. Observe the real production chain.
    public sealed class NativeWorkshopChecks : MapComponent
    {
        private bool started,done;
        private int start;
        private readonly HashSet<int> initialTables=new HashSet<int>();
        private readonly HashSet<string> initialResearch=new HashSet<string>();
        private readonly List<string> evidence=new List<string>();
        public NativeWorkshopChecks(Map map):base(map){}
        public static void BillCompleted(Bill_Production __instance,Pawn billDoer)
        {
            var observer=billDoer.Map?.GetComponent<NativeWorkshopChecks>();
            var table=__instance.billStack?.billGiver as Building_WorkTable;
            if(observer?.started!=true || observer.done || table==null || observer.initialTables.Contains(table.thingIDNumber) ||
                !ModularFacilities.WorkshopBenches.Contains(table.def.defName))return;
            var prerequisites=(table.def.researchPrerequisites??new List<ResearchProjectDef>())
                .Concat(__instance.recipe.researchPrerequisites??new List<ResearchProjectDef>())
                .Concat(__instance.recipe.researchPrerequisite==null?Enumerable.Empty<ResearchProjectDef>():new[]{__instance.recipe.researchPrerequisite});
            if(!prerequisites.Any(r=>r.IsFinished && !observer.initialResearch.Contains(r.defName)))return;
            observer.evidence.Add(table.def.defName+" -> "+__instance.recipe.defName+" por "+billDoer.LabelShort);
        }
        public override void MapComponentTick()
        {
            if(done || !GenCommandLine.CommandLineArgPassed("autonomousrimnativeworkshoptest") || !EliteFixtureBaseline.Ready || Find.TickManager.TicksGame<300)return;
            try
            {
                if(!started)
                {
                    EliteFixtureBaseline.Roster(map);start=Find.TickManager.TicksGame;started=true;
                    foreach(var b in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>())initialTables.Add(b.thingIDNumber);
                    foreach(var r in DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r=>r.IsFinished))initialResearch.Add(r.defName);
                    new Harmony("autonomousrim.nativeworkshop.proof").Patch(AccessTools.Method(typeof(Bill_Production),nameof(Bill_Production.Notify_IterationCompleted)),
                        postfix:new HarmonyMethod(typeof(NativeWorkshopChecks),nameof(BillCompleted)));
                    map.GetComponent<AutonomousRimMapComponent>().EnableAll();
                    Log.Message("[NativeWorkshopTests] START: original campaign copy; no resources/research/buildings granted.");
                }
                if(Find.TickManager.TicksGame%300!=0)return;
                EliteFixtureBaseline.VerifyOriginalsPresent(map);
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                var tables=map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
                    .Where(b=>!initialTables.Contains(b.thingIDNumber) && ModularFacilities.WorkshopBenches.Contains(b.def.defName)).ToList();
                var research=DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r=>r.IsFinished && !initialResearch.Contains(r.defName)).ToList();
                if(Find.TickManager.TicksGame%6000==0)
                {
                    Log.Message("[NativeWorkshopTests] Progress elapsed="+(Find.TickManager.TicksGame-start)+" research="+Find.ResearchManager.GetProject()?.defName+
                        " finished="+string.Join(",",research.Select(r=>r.defName))+" tables="+string.Join(",",tables.Select(b=>b.def.defName+":"+b.BillStack.Bills.Count))+
                        " workshop="+string.Join(";",ai.BaseProjects.Where(p=>p.Kind=="Oficina").Select(p=>p.State+" "+p.BlockReason)));
                    GameDataSaveLoader.SaveGame("NativeWorkshopCheckpoint");
                }
                if(research.Count>0 && evidence.Count>0)
                {
                    Log.Message("[NativeWorkshopTests] PASS: native research completed="+string.Join(",",research.Select(r=>r.defName))+"; new bench production="+string.Join(";",evidence));
                    GameDataSaveLoader.SaveGame("NativeWorkshopEvidence");done=true;Log.Message("[NativeWorkshopTests] DONE");
                }
                else if(Find.TickManager.TicksGame-start>120000)throw new InvalidOperationException("Two-day observation incomplete; checkpoint retained.");
            }
            catch(Exception e){done=true;Log.Error("[NativeWorkshopTests] FAIL "+e);GameDataSaveLoader.SaveGame("NativeWorkshopFailure");}
        }
    }
}
