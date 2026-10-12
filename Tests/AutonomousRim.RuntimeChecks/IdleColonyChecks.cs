using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    // Regression on a copy of the actual stalled save, no spawned supplies/equipment.
    public sealed class IdleColonyChecks : MapComponent
    {
        private static bool done,setup;
        private static int start;
        private static HashSet<string> worked=new HashSet<string>();
        public IdleColonyChecks(Map map):base(map){}
        public override void MapComponentTick()
        {
            if(done || !EliteFixtureBaseline.Ready || !GenCommandLine.CommandLineArgPassed("autonomousrimidlecolonytest"))return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                var pawns=map.mapPawns.FreeColonistsSpawned.ToList();
                if(!setup)
                {
                    if(pawns.Count!=6)throw new Exception("Original six colonists missing.");
                    var work=(List<WorkPriorityChange>)typeof(AutonomousRimMapComponent).GetField("workChanges",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ai);
                    var schedules=(List<ScheduleChange>)typeof(AutonomousRimMapComponent).GetField("scheduleChanges",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ai);
                    var state=ColonyStateScanner.Scan(map);
                    Log.Message("[IdleColony] BEFORE: "+schedules.Count(s=>s.PersonalRecovery)+" sticky recoveries; food days="+state.EstimatedFoodDays);
                    WorkPriorityManager.Apply(map,state,work,ai.BaseAutomation,true,ai.StrategyAutomation);
                    ScheduleManager.Apply(map,state,schedules,true);
                    foreach(var p in pawns)
                    {
                        Log.Message("[IdleColony] PAWN: "+p.LabelShort+" serious="+WorkReadiness.SeriousMedicalNeed(p)+" rest="+p.needs.rest.CurLevelPercentage+" food="+p.needs.food.CurLevelPercentage+" mood="+p.needs.mood.CurLevelPercentage+" workHours="+Enumerable.Range(0,24).Count(h=>p.timetable.GetAssignment(h)==TimeAssignmentDefOf.Work)+" enabled="+DefDatabase<WorkTypeDef>.AllDefsListForReading.Count(w=>!p.WorkTypeIsDisabled(w)&&WorkPriorityManager.RawPriority(p,w)>0));
                        if(!WorkReadiness.NeedsRecovery(p) && Enumerable.Range(0,24).Count(h=>p.timetable.GetAssignment(h)==TimeAssignmentDefOf.Work)<7)throw new Exception("Able colonist still has no work block: "+p.LabelShort);
                        if(!WorkReadiness.NeedsRecovery(p) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction) && WorkPriorityManager.RawPriority(p,WorkTypeDefOf.Construction)==0)throw new Exception("Low mood/hunger still disables construction.");
                        if(!WorkReadiness.NeedsRecovery(p) && schedules.Any(s=>s.Pawn==p && s.PersonalRecovery))throw new Exception("Stale personal recovery retained.");
                    }
                    var routines=work.Select(c=>c.Pawn.GetUniqueLoadID()+"/"+c.Work.defName+"/"+c.Routine).ToList();
                    WorkPriorityManager.Apply(map,state,work,ai.BaseAutomation,true,ai.StrategyAutomation);
                    if(!routines.SequenceEqual(work.Select(c=>c.Pawn.GetUniqueLoadID()+"/"+c.Work.defName+"/"+c.Routine)))throw new Exception("Routine roles churned on reevaluation.");
                    foreach(var p in pawns.Where(WorkPriorityManager.CanWork))p.jobs.EndCurrentJob(JobCondition.InterruptForced,false);
                    setup=true;start=Find.TickManager.TicksGame;
                    Log.Message("[IdleColony] PASS: legacy priorities repaired, work blocks restored and stable role ownership initialized without equipment/needs cheats.");
                }
                foreach(var p in pawns)
                    if(p.CurJob?.workGiverDef!=null)
                    {if(worked.Add(p.GetUniqueLoadID()))Log.Message("[IdleColony] WORK: "+p.LabelShort+" "+p.CurJob.def.defName+" via "+p.CurJob.workGiverDef.defName);}
                if(Find.TickManager.TicksGame-start<12000)return;
                if(worked.Count<2)throw new Exception("Fewer than two colonists resumed native work; observed="+worked.Count+" jobs="+string.Join(",",pawns.Select(p=>p.LabelShort+":"+p.CurJob?.def.defName)));
                GameDataSaveLoader.SaveGame("AutonomousRim-TesteV2-Retomado");
                Log.Message("[IdleColony] DONE: "+worked.Count+" original colonists resumed native work in the actual stalled save; no equipment/resources spawned.");done=true;
            }
            catch(Exception e){done=true;Log.Error("[IdleColony] FAIL: "+e);}
        }
    }
}
