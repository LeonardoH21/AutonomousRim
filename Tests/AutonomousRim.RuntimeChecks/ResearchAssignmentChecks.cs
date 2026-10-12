using System;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class ResearchAssignmentChecks : MapComponent
    {
        private static bool done, started, observed;
        private static int start;
        private static ResearchProjectDef project;
        private static float initial;
        public ResearchAssignmentChecks(Map map):base(map){}
        public override void MapComponentTick()
        {
            if(done || !EliteFixtureBaseline.Ready || !GenCommandLine.CommandLineArgPassed("autonomousrimresearchassignmenttest"))return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(!started)
                {
                    started=true;start=Find.TickManager.TicksGame;
                    project=Find.ResearchManager.GetProject();
                    if(project==null)throw new Exception("No current research in actual save.");
                    initial=Find.ResearchManager.GetProgress(project);
                    Log.Message("[ResearchAssignment] START: "+project.defName+" progress="+initial+" benches="+map.listerBuildings.AllBuildingsColonistOfClass<Building_ResearchBench>().Count());
                }
                int elapsed=Find.TickManager.TicksGame-start;
                foreach(var pawn in map.mapPawns.FreeColonistsSpawned)
                    if(!observed && pawn.CurJob?.workGiverDef?.workType==WorkTypeDefOf.Research)
                    {observed=true;Log.Message("[ResearchAssignment] NATIVE RESEARCH: "+pawn.LabelShort+" job="+pawn.CurJob.def.defName);}
                if(elapsed%1800==0)
                {
                    var state=ColonyStateScanner.Scan(map);
                    Log.Message("[ResearchAssignment] SNAPSHOT: elapsed="+elapsed+" phase="+ai.Emergency.Phase+" foodDays="+state.EstimatedFoodDays+" progress="+Find.ResearchManager.GetProgress(project));
                    foreach(var p in map.mapPawns.FreeColonistsSpawned)
                        Log.Message("[ResearchAssignment] PAWN: "+p.LabelShort+" job="+p.CurJob?.def.defName+" research="+WorkPriorityManager.RawPriority(p,WorkTypeDefOf.Research)+" build="+WorkPriorityManager.RawPriority(p,WorkTypeDefOf.Construction)+" grow="+WorkPriorityManager.RawPriority(p,WorkTypeDefOf.Growing)+" haul="+WorkPriorityManager.RawPriority(p,WorkTypeDefOf.Hauling)+" recovery="+WorkReadiness.NeedsRecovery(p));
                }
                float progress=Find.ResearchManager.GetProgress(project);
                if(observed && progress-initial>=25)
                {
                    var diagnostic=ai.Progress.Entries.FirstOrDefault(e=>e.Key=="research:"+project.defName);
                    if(diagnostic==null || diagnostic.Value<=initial || diagnostic.LastProgress<start || diagnostic.Stalled)
                        throw new Exception("Native research advanced but progress diagnostic did not follow it.");
                    Log.Message("[ResearchAssignment] PASS: progress diagnostic follows native research points without false stall.");
                    Log.Message("[ResearchAssignment] DONE: actual V3 native research advanced "+initial+" -> "+progress+" in "+elapsed+" ticks, without research cheats or forced jobs.");done=true;return;
                }
                if(elapsed>=30000)throw new Exception("Research still stalled: "+initial+" -> "+progress+" observed="+observed);
            }
            catch(Exception e){done=true;Log.Error("[ResearchAssignment] FAIL: "+e);}
        }
    }
}
