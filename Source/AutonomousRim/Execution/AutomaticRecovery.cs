using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    public sealed class RecoveryAttempt : IExposable
    {
        public string Key, Action, JobId, Result;
        public Pawn Worker;
        public int Attempts, NextTick, IssuedAt, Completions, ProgressTick;
        public float WorkLeft=-1, Value;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Key,"key");Scribe_Values.Look(ref Action,"action");Scribe_Values.Look(ref JobId,"jobId");
            Scribe_Values.Look(ref Result,"result");Scribe_References.Look(ref Worker,"worker");
            Scribe_Values.Look(ref Attempts,"attempts");Scribe_Values.Look(ref NextTick,"nextTick");
            Scribe_Values.Look(ref IssuedAt,"issuedAt");Scribe_Values.Look(ref Completions,"completions");Scribe_Values.Look(ref WorkLeft,"workLeft",-1);
            Scribe_Values.Look(ref Value,"value");Scribe_Values.Look(ref ProgressTick,"progressTick");
        }
    }
    public sealed class RecoveryState : IExposable
    {
        public List<RecoveryAttempt> Attempts=new List<RecoveryAttempt>();
        public void ExposeData()
        {
            Scribe_Collections.Look(ref Attempts,"attempts",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit){Attempts=Attempts??new List<RecoveryAttempt>();Attempts.RemoveAll(a=>a==null||a.Key==null);}
        }
    }
    public static class AutomaticRecovery
    {
        private static bool Idle(Pawn p)=>!ActorCoordinator.Blocked(p,"producao") && WorkPriorityManager.CanWork(p) && BillDiagnostics.OnShift(p) &&
            p.jobs.jobQueue.Count==0 && (p.CurJob==null || p.CurJob.def==JobDefOf.Wait || p.CurJob.def==JobDefOf.Wait_Wander || p.CurJob.def==JobDefOf.GotoWander);
        public static void Apply(Map map,AutonomousRimMapComponent ai)
        {
            int tick=Find.TickManager.TicksGame;
            var owned=ai.ManagedProduction.Where(m=>m?.Matches==true).Select(m=>m.Bill).ToList();
            ai.Recovery.Attempts.RemoveAll(a=>!ai.Progress.Entries.Any(e=>e.Key==a.Key));
            // Verify issued jobs using native work/progress, never count dispatch as success.
            foreach(var attempt in ai.Recovery.Attempts.Where(a=>a.JobId!=null).ToList()) {
                var entry=ai.Progress.Entries.FirstOrDefault(e=>e.Key==attempt.Key);
                if(entry!=null && (entry.Value>attempt.Value+.01f || entry.Completions>attempt.Completions || entry.WorkLeft>=0 && attempt.WorkLeft>entry.WorkLeft+.01f)) {
                    attempt.Result="Avanço nativo confirmado após a correção.";attempt.JobId=null;attempt.Attempts=0;
                    ai.Progress.Record(tick,attempt.Result,"Recuperação",attempt.Key);continue;
                }
                if(attempt.Worker?.CurJob?.GetUniqueLoadID()==attempt.JobId)attempt.Result="Job iniciou; aguardando avanço/produto.";
                else if(tick-attempt.IssuedAt>=600) {attempt.Result="Sem avanço confirmado; tentar outro responsável após cooldown.";attempt.JobId=null;}
            }
            if(ai.SecondarySuspended || ai.CurrentState?.Threat?.Immediate==true)return;
            foreach(var entry in ai.Progress.Entries.Where(e=>e.Stalled && e.Eligible && (e.Key.StartsWith("bill:") || e.Key.StartsWith("research:"))).Take(2)) {
                var attempt=ai.Recovery.Attempts.FirstOrDefault(a=>a.Key==entry.Key);
                if(attempt==null){attempt=new RecoveryAttempt{Key=entry.Key};ai.Recovery.Attempts.Add(attempt);}
                attempt.ProgressTick=System.Math.Max(attempt.ProgressTick,0);
                if(entry.LastProgress>attempt.ProgressTick){attempt.Attempts=0;attempt.ProgressTick=entry.LastProgress;}
                if(attempt.JobId!=null || tick<attempt.NextTick || attempt.Attempts>=3)continue;
                Job job=null;Pawn selected=null;
                if(entry.Key.StartsWith("bill:")) {
                    var bill=owned.FirstOrDefault(b=>"bill:"+b.GetUniqueLoadID()==entry.Key);
                    if(bill==null || !BillDiagnostics.Requested(bill))continue;
                    var table=map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().FirstOrDefault(t=>t.BillStack.Bills.Contains(bill));
                    if(table==null)continue;
                    var givers=DefDatabase<WorkGiverDef>.AllDefsListForReading.Where(g=>g.workType!=null &&
                        typeof(WorkGiver_DoBill).IsAssignableFrom(g.giverClass) && g.fixedBillGiverDefs?.Contains(table.def)==true).ToList();
                    var candidates=map.mapPawns.FreeColonistsSpawned.Where(Idle).OrderBy(p=>p.thingIDNumber).ToList();
                    foreach(var pawn in candidates.Skip(attempt.Attempts%candidates.CountSafe()).Concat(candidates.Take(attempt.Attempts%candidates.CountSafe()))) {
                    foreach(var giver in givers.Where(g=>!pawn.WorkTypeIsDisabled(g.workType) && pawn.workSettings.GetPriority(g.workType)>0)) {
                        Job candidate;try{candidate=((WorkGiver_DoBill)giver.Worker).JobOnThing(pawn,table,false);}catch(Exception){continue;}
                        if(candidate?.bill!=bill)continue;
                        selected=pawn;job=candidate;break;
                    }
                    if(job!=null)break;
                    }
                }
                else if(entry.Key.StartsWith("research:") && ai.StrategyAutomation) {
                    var project=Find.ResearchManager.GetProject();
                    var giver=DefDatabase<WorkGiverDef>.AllDefsListForReading.FirstOrDefault(g=>g.giverClass==typeof(WorkGiver_Researcher));
                    if(project==null || giver==null)continue;
                    foreach(var pawn in map.mapPawns.FreeColonistsSpawned.Where(Idle).Where(p=>!p.WorkTypeIsDisabled(WorkTypeDefOf.Research) && p.workSettings.GetPriority(WorkTypeDefOf.Research)>0)) {
                    foreach(var bench in map.listerBuildings.AllBuildingsColonistOfClass<Building_ResearchBench>().Where(b=>project.CanBeResearchedAt(b,false))) {
                        Job candidate;try{candidate=((WorkGiver_Researcher)giver.Worker).JobOnThing(pawn,bench,false);}catch(Exception){continue;}
                        if(candidate==null)continue;selected=pawn;job=candidate;break;
                    }
                    if(job!=null)break;
                    }
                }
                attempt.Attempts++;attempt.NextTick=tick+2500;
                attempt.Result=job==null?"Sem alternativa ociosa e viável; conservar trabalhos e controles manuais.":"Ordem emitida; resultado ainda não confirmado.";
                if(job!=null)selected.jobs.StartJob(job,JobCondition.InterruptOptional,tag:JobTag.Misc,preToilReservationsCanFail:true);
                if(job!=null && (selected.CurJob?.GetUniqueLoadID()==job.GetUniqueLoadID() || selected.jobs.jobQueue.Any(q=>q.job==job))) {
                    attempt.Worker=selected;attempt.JobId=job.GetUniqueLoadID();attempt.IssuedAt=tick;
                    attempt.Completions=entry.Completions;attempt.WorkLeft=entry.WorkLeft;attempt.Value=entry.Value;attempt.Action="Retomar job nativo com responsável ocioso";
                }
                ai.Progress.Record(tick,attempt.Result,"Recuperação",entry.Key);
            }
            if(ai.Recovery.Attempts.Count>64)ai.Recovery.Attempts.RemoveRange(0,ai.Recovery.Attempts.Count-64);
        }
        private static int CountSafe<T>(this List<T> list)=>Math.Max(1,list.Count);
    }
}
