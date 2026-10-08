using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Perception;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    public sealed class MedicalOrder : IExposable
    {
        public Pawn Helper,Patient;
        public Building_Bed Bed;
        public string JobId,PreviousJobId,Stage;
        public void ExposeData()
        {
            Scribe_References.Look(ref Helper,"helper");Scribe_References.Look(ref Patient,"patient");
            Scribe_References.Look(ref Bed,"bed");Scribe_Values.Look(ref JobId,"jobId");Scribe_Values.Look(ref Stage,"stage");
            Scribe_Values.Look(ref PreviousJobId,"previousJobId");
        }
    }
    public static class MedicalRescueManager
    {
        public static bool Reserved(Pawn p)=>p.Map?.GetComponent<AutonomousRimMapComponent>()?.Emergency.Medical.Any(o=>o.Helper==p)==true;
        private static bool Pinned(Pawn enemy,ThreatState threat)=>threat.Fighters.Any(a=>a!=enemy && !Reserved(a) &&
            a.health.summaryHealth.SummaryHealthPercent>.65f && a.CurJob?.def==JobDefOf.AttackMelee &&
            a.CurJob.targetA.Thing==enemy && a.Position.DistanceTo(enemy.Position)<1.6f);
        public static bool Safe(Map map,IntVec3 c,ThreatState threat)
        {
            if(!c.InBounds(map)||c.GetThingList(map).Any(t=>t is Fire))return false;
            foreach(var source in threat.Sources.Where(t=>t.Spawned))
            {
                if(source is Pawn enemy && !ThreatScanner.Active(enemy))continue;
                if(source is Pawn held && Pinned(held,threat) && c.DistanceTo(held.Position)>=4)continue;
                if(!EmergencyManager.Safe(map,c,new ThreatState{Sources=new List<Thing>{source}}))return false;
            }
            return true;
        }
        private static float RouteTicks(Pawn helper,IntVec3 from,IntVec3 to,ThreatState threat)
        {
            if(!helper.CanReach(to,PathEndMode.OnCell,Danger.Deadly))return float.PositiveInfinity;
            using(var path=helper.Map.pathFinder.FindPathNow(from,to,TraverseParms.For(helper,Danger.Deadly)))
            {
                if(!path.Found || path.NodesReversed.Any(c=>!Safe(helper.Map,c,threat)))return float.PositiveInfinity;
                return path.NodesReversed.Sum(c=>Math.Max(1,13+c.GetTerrain(helper.Map).pathCost))*
                    4.6f/Math.Max(.5f,helper.GetStatValue(StatDefOf.MoveSpeed));
            }
        }
        private static bool Able(Pawn p)=>p!=null && p.Spawned && !p.Dead && !p.Downed && !p.InMentalState &&
            p.health.capacities.CapableOf(PawnCapacityDefOf.Moving) && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
        private static bool Available(AutonomousRimMapComponent ai,Pawn p)=>Able(p) && p.workSettings!=null &&
            !p.InBed() && !p.WorkTagIsDisabled(WorkTags.Caring) && !p.WorkTagIsDisabled(WorkTags.Hauling) &&
            !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && p.health.summaryHealth.SummaryHealthPercent>.8f &&
            p.health.hediffSet.BleedRateTotal<.1f && (p.needs?.rest?.CurLevel??1)>.25f &&
            !ai.Emergency.Excluded.Contains(p) && !Reserved(p) && ai.CanAssignMedical(p);
        private static void Release(EmergencyState state,MedicalOrder order,bool cancel)
        {
            Log.Message($"[AutonomousRim.Medical] Liberar {order.Helper?.LabelShort}: {order.Stage}; cancel={cancel}; job={order.Helper?.CurJob?.def.defName}; bleed={order.Patient?.health.hediffSet.BleedRateTotal}");
            if(cancel && order.Helper?.CurJob?.GetUniqueLoadID()==order.JobId)
                order.Helper.jobs.EndCurrentJob(JobCondition.InterruptForced);
            state.Medical.Remove(order);
        }
        public static void Stop(EmergencyState state)
        { foreach(var order in state.Medical.ToList())Release(state,order,true); }
        private static Job Tend(Pawn helper,Pawn patient,ThreatState threat,bool urgent)
        {
            var job=new WorkGiver_Tend().JobOnThing(helper,patient,true);
            var medicine=job.targetB.Thing;
            if(urgent || medicine!=null && (!medicine.Spawned || !Safe(helper.Map,medicine.Position,threat) ||
                float.IsPositiveInfinity(RouteTicks(helper,helper.Position,medicine.Position,threat))))
                job=JobMaker.MakeJob(JobDefOf.TendPatient,patient);
            return job;
        }
        private static bool Issue(MedicalOrder order,Job job,string stage)
        {
            job.locomotionUrgency=LocomotionUrgency.Sprint;
            string previous=order.Helper.CurJob?.GetUniqueLoadID();
            if(!order.Helper.jobs.TryTakeOrderedJob(job,JobTag.Misc,false))return false;
            order.PreviousJobId=previous;order.JobId=job.GetUniqueLoadID();order.Stage=stage;
            Log.Message($"[AutonomousRim.Medical] {order.Helper.LabelShort}: {stage} {order.Patient.LabelShort}; morte por sangramento em {HealthUtility.TicksUntilDeathDueToBloodLoss(order.Patient)} ticks.");
            return true;
        }
        public static void Apply(Map map,EmergencyState state,ThreatState threat)
        {
            var ai=map.GetComponent<AutonomousRimMapComponent>();
            foreach(var order in state.Medical.ToList())
            {
                var h=order.Helper;var p=order.Patient;
                if(h?.CurJob?.GetUniqueLoadID()==order.JobId)order.PreviousJobId=null;
                if(!Able(h)||h.health.summaryHealth.SummaryHealthPercent<.55f||p==null||p.Dead||
                    !p.Spawned && h.carryTracker.CarriedThing!=p)
                {Release(state,order,true);continue;}
                if(h.CurJob?.playerForced==true && h.CurJob.GetUniqueLoadID()!=order.JobId && h.CurJob.GetUniqueLoadID()!=order.PreviousJobId || h.Drafted)
                {state.Excluded.Add(h);Release(state,order,false);continue;}
                if(p.Spawned && (!Safe(map,p.Position,threat)||float.IsPositiveInfinity(RouteTicks(h,h.Position,p.Position,threat))))
                {Release(state,order,true);continue;}
                if(p.InBed() && order.Stage=="Resgatar")
                {
                    if(HealthAIUtility.ShouldBeTendedNowByPlayer(p))
                    {if(h.CanReserve(p))Issue(order,Tend(h,p,threat,false),"Tratar");continue;}
                    Release(state,order,false);continue;
                }
                if(h.CurJob?.GetUniqueLoadID()==order.JobId || h.CurJob?.GetUniqueLoadID()==order.PreviousJobId)continue;
                Release(state,order,false); // Completed care or stabilization; reevaluate patient immediately.
            }
            state.DeferredRescues=0;
            var patients=threat.Colonists.Where(p=>!p.Dead && (p.Downed && !p.InBed() || p.InBed() && HealthAIUtility.ShouldBeTendedNowByPlayer(p)))
                .OrderBy(HealthUtility.TicksUntilDeathDueToBloodLoss).ThenByDescending(p=>p.health.hediffSet.BleedRateTotal);
            foreach(var victim in patients)
            {
                if(state.Medical.Any(o=>o.Patient==victim))continue;
                state.DeferredRescues++;
                if(!Safe(map,victim.Position,threat))continue;
                int death=HealthUtility.TicksUntilDeathDueToBloodLoss(victim);
                var choices=new List<Tuple<Pawn,Job,float,bool>>();
                foreach(var helper in threat.Colonists.Where(p=>p!=victim && Available(ai,p)))
                {
                    var defenders=ai.CombatOrders.Where(o=>o.Pawn!=helper && !o.Retreated && PawnAnalyzer.IsCombatReady(o.Pawn) && !Reserved(o.Pawn)).Select(o=>o.Pawn).ToList();
                    var active=threat.Sources.OfType<Pawn>().Where(e=>ThreatScanner.Active(e) &&
                        (e.Position.DistanceTo(victim.Position)<40 || e.Position.DistanceTo(helper.Position)<40)).ToList();
                    // Also count ready undrafted defenders before the controller's first draft pass.
                    defenders.AddRange(threat.Fighters.Where(p=>p!=helper && !Reserved(p) && !defenders.Contains(p) && !ai.CombatOrders.Any(o=>o.Pawn==p && o.Retreated)));
                    if(active.Count>0)defenders=defenders.Where(p=>active.Any(e=>e.Position.DistanceTo(p.Position)<40)).ToList();
                    if(active.Count>0 && (threat.Structures>0 || !RescuePolicy.CanDetach(defenders.Count,active.Count,defenders.Sum(PawnAnalyzer.EstimateCombatValue),active.Sum(PawnAnalyzer.EstimateCombatValue))))continue;
                    float travel=RouteTicks(helper,helper.Position,victim.Position,threat);
                    if(float.IsPositiveInfinity(travel)||!helper.CanReserve(victim))continue;
                    float tend=600/Math.Max(.1f,helper.GetStatValue(StatDefOf.MedicalTendSpeed));
                    Job job=null;bool stabilize=false;
                    if(victim.InBed())job=Tend(helper,victim,threat,death<travel+tend+600);
                    else
                    {
                        var rescue=new WorkGiver_RescueDowned();
                        if(rescue.HasJobOnThing(helper,victim,true))
                        {
                            job=rescue.JobOnThing(helper,victim,true);
                            if(job.targetB.Thing==null || !helper.CanReserve(job.targetB.Thing) ||
                                float.IsPositiveInfinity(RouteTicks(helper,victim.Position,job.targetB.Cell,threat)))job=null;
                        }
                        float transport=job==null?float.PositiveInfinity:travel+RouteTicks(helper,victim.Position,job.targetB.Cell,threat)+180;
                        stabilize=victim.health.hediffSet.BleedRateTotal>0 && HealthAIUtility.ShouldBeTendedNowByPlayer(victim) && RescuePolicy.StabilizeFirst(death,transport,tend,job!=null);
                        if(stabilize)job=Tend(helper,victim,threat,true);
                    }
                    if(job==null || death!=int.MaxValue && death<travel+tend+60)continue;
                    float score=RescuePolicy.HelperScore(helper.skills?.GetSkill(SkillDefOf.Medicine).Level??0,PawnAnalyzer.EstimateCombatValue(helper),travel,death);
                    choices.Add(Tuple.Create(helper,job,score,stabilize));
                }
                foreach(var candidate in choices.OrderByDescending(c=>c.Item3))
                {
                    var order=new MedicalOrder{Helper=candidate.Item1,Patient=victim,Bed=candidate.Item2.targetB.Thing as Building_Bed};
                    if(!ai.ReleaseCombatForMedical(order.Helper))continue;
                    state.Medical.Add(order);
                    if(Issue(order,candidate.Item2,candidate.Item4?"Estabilizar":victim.InBed()?"Tratar":"Resgatar")){state.DeferredRescues--;break;}
                    state.Medical.Remove(order);
                }
            }
        }
    }
}
