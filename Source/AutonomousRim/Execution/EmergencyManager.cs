using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace AutonomousRim.Execution
{
    public static class EmergencyManager
    {
        public static bool Retreat(ThreatState threat) => threat.Structures>0 || threat.Fighters.Count==0 ||
            threat.Sources.OfType<Pawn>().Count()>threat.Fighters.Count*2 ||
            threat.EnemyStrength>Math.Max(1,threat.FriendlyStrength)*1.65f;
        public static bool LocalFire(Map map) => map.listerThings.AllThings.Any(t=>t is Fire &&
            (map.mapPawns.FreeColonistsSpawned.Any(p=>p.Position.DistanceTo(t.Position)<15) ||
             map.listerBuildings.allBuildingsColonist.Any(b=>b.Position.DistanceTo(t.Position)<12)));
        // The colony-wide recovery gate protects patients. Individual
        // fatigue and mood remain handled by WorkReadiness and
        // ScheduleManager; they must not disable healthy workers indefinitely.
        public static bool NeedsCare(Map map) => map.mapPawns.FreeColonistsSpawned.Any(p=>WorkReadiness.SeriousMedicalNeed(p) ||
            p.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury injury && !injury.IsPermanent() && injury.Severity>0));
        private static bool Essential(WorkTypeDef work, bool recovery, bool foodCritical, bool foodLow = false) =>
            work==WorkTypeDefOf.Doctor || work.defName=="Patient" || work.defName=="PatientBedRest" || work.defName=="BasicWorker" ||
            work.defName=="Firefighter" || (recovery || foodCritical) && work.defName=="Cooking" ||
            recovery && (work==WorkTypeDefOf.Hauling || work==WorkTypeDefOf.Cleaning || work==WorkTypeDefOf.Growing || foodLow && work==WorkTypeDefOf.Hunting);

        public static void RestoreWork(EmergencyState state) => WorkPriorityManager.Restore(state.Work);
        public static void Stop(EmergencyState state)
        {
            RestoreWork(state); CombatManager.Stop(state.Evacuations); state.Excluded.Clear();
        }
        public static bool Safe(Map map,IntVec3 cell,ThreatState threat)
        {
            if(!cell.InBounds(map) || cell.GetThingList(map).Any(t=>t is Fire))return false;
            return threat.Sources.All(t=>t.Spawned && (cell.DistanceTo(t.Position)> SafetyDistance(t) ||
                cell.DistanceTo(t.Position)>3 && !GenSight.LineOfSight(t.Position,cell,map)));
        }
        private static float SafetyDistance(Thing source)
        {
            if(source is Pawn pawn)
                return !CombatManager.Ranged(pawn)?Math.Max(10,pawn.GetStatValue(StatDefOf.MoveSpeed)*2+2):
                    Math.Max(25,(pawn.equipment.Primary.TryGetComp<CompEquippable>()?.PrimaryVerb?.verbProps.range??25)+3);
            return 60; // Conservatively avoid unidentified hostile structure firing lanes.
        }
        public static bool Route(Pawn pawn,IntVec3 cell,ThreatState threat,bool escaping=false)
        {
            if(!pawn.CanReach(cell,PathEndMode.OnCell,Danger.None))return false;
            using(var path=pawn.Map.pathFinder.FindPathNow(pawn.Position,cell,TraverseParms.For(pawn,Danger.None)))
                return path.Found && path.NodesReversed.All(c=>!c.GetThingList(pawn.Map).Any(t=>t is Fire) && (Safe(pawn.Map,c,threat) || escaping && threat.Sources.All(t=>c.DistanceTo(t.Position)>=pawn.Position.DistanceTo(t.Position)-0.5f)));
        }
        private static IntVec3 Shelter(Pawn pawn,ThreatState threat)
        {
            var map=pawn.Map;
            return GenRadial.RadialCellsAround(pawn.Position,35,true).Where(c=>c.InBounds(map) && c.Standable(map) && !c.Fogged(map) &&
                c.Roofed(map) && c.GetRoom(map)?.PsychologicallyOutdoors==false && Safe(map,c,threat) &&
                !c.GetThingList(map).OfType<Pawn>().Any(p=>p!=pawn && !p.Downed) && !(c.GetEdifice(map) is Building_Door))
                .OrderBy(c=>c.DistanceTo(pawn.Position)).Take(16).Where(c=>Route(pawn,c,threat,true)).DefaultIfEmpty(IntVec3.Invalid).First();
        }
        private static void Evacuate(EmergencyState state,ThreatState threat)
        {
            state.DeferredRescues=state.MissingShelters=0;
            foreach(var order in state.Evacuations.ToList())
            {
                var pawn=order.Pawn;
                if(order.Role=="Resgate seguro" && pawn?.CurJob?.GetUniqueLoadID()!=order.JobId)
                { state.Evacuations.Remove(order); continue; }
                if(pawn==null || !pawn.Spawned || pawn.Dead || pawn.Downed ||
                    pawn.CurJob?.playerForced==true && pawn.CurJob.GetUniqueLoadID()!=order.JobId ||
                    order.Role=="Abrigo civil" && !pawn.Drafted)
                {
                    // Preserve later manual orders and exclude them for this incident.
                    CombatManager.Stop(new List<CombatOrder>{order}); state.Evacuations.Remove(order);
                    if(pawn!=null)state.Excluded.Add(pawn);
                }
            }
            var rescuer=new WorkGiver_RescueDowned();
            foreach(var victim in threat.Colonists.Where(p=>p.Downed && !p.InBed()).OrderByDescending(p=>p.health.hediffSet.BleedRateTotal))
            {
                if(state.Evacuations.Any(o=>o.Pawn.CurJob?.targetA.Thing==victim))continue;
                state.DeferredRescues++;
                if(!Safe(victim.Map,victim.Position,threat))continue;
                foreach(var helper in threat.Colonists.Where(p=>WorkPriorityManager.CanWork(p) && !p.InBed() &&
                    !p.WorkTagIsDisabled(WorkTags.Caring) && !p.WorkTagIsDisabled(WorkTags.Hauling) &&
                    p.health.summaryHealth.SummaryHealthPercent>0.8f && p.health.hediffSet.BleedRateTotal<0.1f &&
                    (p.needs?.rest?.CurLevel??1)>0.25f && p.CurJob?.playerForced!=true && !state.Excluded.Contains(p) &&
                    !state.Evacuations.Any(o=>o.Pawn==p)).OrderBy(p=>p.Position.DistanceTo(victim.Position)))
                {
                    if(!Route(helper,victim.Position,threat) || !rescuer.HasJobOnThing(helper,victim))continue;
                    var job=rescuer.JobOnThing(helper,victim);
                    if(job==null || !job.targetB.IsValid || !Safe(helper.Map,job.targetB.Cell,threat) || !Route(helper,job.targetB.Cell,threat))continue;
                    if(helper.jobs.TryTakeOrderedJob(job,JobTag.Misc,false))
                    {
                        state.Evacuations.Add(new CombatOrder{Pawn=helper,JobId=job.GetUniqueLoadID(),Role="Resgate seguro",OriginalFireAtWill=helper.drafter?.FireAtWill??true});
                        state.DeferredRescues--;
                    }
                    break;
                }
            }
            foreach(var pawn in threat.Colonists.Where(p=>!p.Dead && !p.Downed && !p.InMentalState && !p.Drafted && p.drafter!=null &&
                p.CurJob?.playerForced!=true && !state.Excluded.Contains(p) && !state.Evacuations.Any(o=>o.Pawn==p) &&
                (threat.Vulnerable.Contains(p) || threat.Structures>0) && !Safe(p.Map,p.Position,threat)))
            {
                var cell=Shelter(pawn,threat);
                if(!cell.IsValid) { state.MissingShelters++; continue; } // Never force a rescue/escape through a firing lane.
                bool fire=pawn.drafter.FireAtWill; pawn.drafter.Drafted=true; pawn.drafter.FireAtWill=false;
                var job=JobMaker.MakeJob(JobDefOf.Goto,cell); job.locomotionUrgency=LocomotionUrgency.Sprint;
                if(pawn.jobs.TryTakeOrderedJob(job,JobTag.Misc,false))
                    state.Evacuations.Add(new CombatOrder{Pawn=pawn,JobId=job.GetUniqueLoadID(),Role="Abrigo civil",OriginalFireAtWill=fire});
                else { pawn.drafter.Drafted=false; pawn.drafter.FireAtWill=fire; }
            }
        }
        public static void Apply(Map map,ColonyState colony,EmergencyState state)
        {
            var threat=colony.Threat;
            state.Decision=threat.Immediate?(Retreat(threat)?"Recuar / abrigar; resgatar somente por rota segura":"Defender em cobertura / reorganizar"):
                state.Phase==EmergencyPhase.Danger?"Incêndio próximo: proteger colonos e combater o fogo":state.Phase==EmergencyPhase.Securing?"Confirmar segurança; atender feridos":state.Phase==EmergencyPhase.Recovery?"Recuperar saúde, descanso e alimentação":"Rotina normal";
            if(state.Phase==EmergencyPhase.Normal) { Stop(state); return; }
            bool recovery=state.Phase==EmergencyPhase.Recovery;
            bool criticalFood=colony.StoredMealCount==0 && colony.EstimatedFoodDays<0.5f;
            bool foodLow=colony.EstimatedFoodDays<colony.TargetFoodDays;
            var doctor=threat.Colonists.Where(p=>WorkPriorityManager.CanWork(p) && !WorkReadiness.NeedsRecovery(p) &&
                !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)).OrderByDescending(p=>WorkPriorityManager.Score(p,WorkTypeDefOf.Doctor,0)).FirstOrDefault();
            foreach(var pawn in threat.Colonists.Where(p=>p.workSettings!=null && !p.Dead))
            {
                pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
                foreach(var work in DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w=>!pawn.WorkTypeIsDisabled(w)))
                {
                    int desired=Essential(work,recovery,criticalFood,foodLow)?1:0;
                    if(work==WorkTypeDefOf.Hunting && !WorkPriorityManager.CanHunt(pawn))desired=0;
                    if(work==WorkTypeDefOf.Doctor && pawn!=doctor)desired=2;
                    if((work.defName=="Patient" || work.defName=="PatientBedRest") && pawn==doctor && !WorkReadiness.SeriousMedicalNeed(pawn))desired=2;
                    if (WorkReadiness.NeedsRecovery(pawn) && !WorkReadiness.SelfCare(work) &&
                        !(criticalFood && work.defName=="Cooking" && WorkReadiness.CanProduceEmergencyFood(pawn))) desired=0;
                    // Urgent cooking is permitted only at a safe, enclosed location.
                    if(!recovery && work.defName=="Cooking" && (!Safe(map,pawn.Position,threat) || pawn.Position.GetRoom(map)?.PsychologicallyOutdoors!=false))desired=0;
                    WorkPriorityManager.SetManagedPriority(pawn,work,desired,state.Work);
                }
                var job=pawn.CurJob;
                if(!pawn.Drafted && job?.playerForced!=true && job!=null &&
                    (job.def==JobDefOf.Wear || job.def==JobDefOf.Equip ||
                     job.workGiverDef?.workType!=null && !Essential(job.workGiverDef.workType,recovery,criticalFood,foodLow)))
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            if(threat.Immediate)Evacuate(state,threat);
            else { CombatManager.Stop(state.Evacuations); state.Excluded.Clear(); state.DeferredRescues=state.MissingShelters=0; }
        }
    }
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel),"TryGiveJob")]
    public static class EmergencyApparelPatch
    {
        public static bool Prefix(Pawn pawn,ref Job __result)
        {
            if(pawn.Map?.GetComponent<AutonomousRimMapComponent>()?.ExpansionSuspended!=true)return true;
            __result=null; return false;
        }
    }
    [HarmonyPatch(typeof(JobGiver_Work),nameof(JobGiver_Work.TryIssueJobPackage))]
    public static class EmergencyWorkRoutePatch
    {
        public static void Postfix(Pawn pawn,ref ThinkResult __result)
        {
            var ai=pawn.Map?.GetComponent<AutonomousRimMapComponent>();
            if(ai?.SecondarySuspended!=true || __result.Job==null || __result.Job.playerForced || ai.CurrentState?.Threat==null)return;
            var job=__result.Job;
            if(job.def==JobDefOf.BeatFire)return; // Native firefighting approaches the fire rather than standing inside it.
            foreach(var target in new[]{job.targetA,job.targetB})
                if(target.IsValid && target.Cell.InBounds(pawn.Map) && !EmergencyManager.Route(pawn,target.Cell,ai.CurrentState.Threat))
                { __result=ThinkResult.NoJob; return; }
        }
    }
}
