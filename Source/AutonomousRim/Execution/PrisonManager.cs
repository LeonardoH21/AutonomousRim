using System;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    public static class PrisonManager
    {
        public static bool Reserved(Pawn p) => p.Map?.GetComponent<AutonomousRimMapComponent>()?.Prison.Orders.Any(o=>o.Helper==p)==true;
        public static bool ConfigureBed(Building_Bed bed)
        {
            var room=bed?.GetRoom();
            if(room==null || !Building_Bed.RoomCanBePrisonCell(room) || room.PsychologicallyOutdoors ||
                room.ContainedBeds.Any(b=>b!=bed && !b.ForPrisoners))return false;
            bed.ForPrisoners=true;
            // Match the native ownership UI: the setter alone leaves the prison-cell cache stale.
            bed.GetDistrict()?.Notify_RoomShapeOrContainedBedsChanged();
            room.Notify_RoomShapeChanged();
            return bed.Position.IsInPrisonCell(bed.Map);
        }
        public static int HousingTarget(Map map)
        {
            int population=map.mapPawns.FreeColonistsSpawnedCount;
            var ai=map.GetComponent<AutonomousRimMapComponent>();
            if(ai?.PrisonAutomation!=true || population>=ai.Prison.PopulationTarget || (ai.CurrentState?.EstimatedFoodDays??0)<3)return population;
            int waiting=ai.Prison.Prisoners.Count(r=>!r.Completed && !r.Manual && r.Destination==PrisonerDestination.Recruit);
            return Math.Min(ai.Prison.PopulationTarget,population+Math.Max(1,waiting));
        }
        private static bool Able(Pawn p) => p?.Spawned==true && !p.Dead && !p.Downed && !p.Drafted && !p.InMentalState &&
            p.health.capacities.CapableOf(PawnCapacityDefOf.Moving) && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) &&
            p.workSettings!=null && p.health.summaryHealth.SummaryHealthPercent>.75f && p.health.hediffSet.BleedRateTotal<.05f &&
            (p.needs?.rest?.CurLevel??1)>.3f && (p.needs?.food?.CurLevel??1)>.25f && (p.needs?.mood?.CurLevel??1)>.2f;
        private static bool Available(Pawn p,PrisonState state) => Able(p) && !p.InBed() && !Reserved(p) &&
            !MedicalRescueManager.Reserved(p) && !state.Excluded.Contains(p) && p.CurJob?.playerForced!=true &&
            !CommerceCaravanManager.Reserved(p.Map.GetComponent<AutonomousRimMapComponent>().Commerce,p);
        private static bool OwnPatients(Map map) => map.mapPawns.FreeColonistsSpawned.Any(p=>p.Downed && !p.InBed() ||
            p.health.hediffSet.BleedRateTotal>.1f && p.health.HasHediffsNeedingTend());
        private static float Route(Pawn helper,IntVec3 from,IntVec3 to,ColonyState colony)
        {
            if(!helper.CanReach(to,PathEndMode.Touch,Danger.None))return float.PositiveInfinity;
            using(var path=helper.Map.pathFinder.FindPathNow(from,to,TraverseParms.For(helper,Danger.None)))
            {
                if(!path.Found || path.NodesReversed.Any(c=>!MedicalRescueManager.Safe(helper.Map,c,colony.Threat)))return float.PositiveInfinity;
                return path.NodesReversed.Sum(c=>Math.Max(1,13+c.GetTerrain(helper.Map).pathCost))*4.6f/Math.Max(.5f,helper.GetStatValue(StatDefOf.MoveSpeed));
            }
        }
        private static void End(PrisonState state,PrisonOrder order,bool cancel)
        {
            if(cancel && order.JobId!=null && order.Helper?.CurJob?.GetUniqueLoadID()==order.JobId)order.Helper.jobs.EndCurrentJob(JobCondition.InterruptForced);
            state.Orders.Remove(order);
        }
        private static void Restore(PrisonerRecord record)
        {
            var guest=record.Pawn?.guest;
            if(!record.Manual && guest!=null && record.AppliedMode!=null && guest.ExclusiveInteractionMode==record.AppliedMode &&
                guest.ideoForConversion==record.AppliedConversion)
            {guest.SetExclusiveInteraction(record.OriginalMode??PrisonerInteractionModeDefOf.MaintainOnly);guest.ideoForConversion=record.OriginalConversion;}
            record.AppliedMode=null;record.AppliedConversion=null;
        }
        public static void Stop(PrisonState state)
        {
            foreach(var order in state.Orders.ToList())End(state,order,true);
            foreach(var record in state.Prisoners)Restore(record);
            state.Status="Prisão desligada; configurações próprias restauradas e construções existentes preservadas.";
        }
        public static void Resume(PrisonState state)
        {
            foreach(var record in state.Prisoners.Where(r=>r.Owned && !r.Manual && !r.Completed && r.AppliedMode==null && r.Pawn?.IsPrisonerOfColony==true))
                if(record.Pawn.guest.ExclusiveInteractionMode!=record.OriginalMode || record.Pawn.guest.ideoForConversion!=record.OriginalConversion)
                {record.Manual=true;record.Status="Configuração modificada enquanto desligado; controle manual.";}
        }
        private static bool Issue(PrisonState state,Pawn helper,Pawn patient,Building_Bed bed,Job job,string stage)
        {
            if(job==null)return false;
            var order=new PrisonOrder{Helper=helper,Patient=patient,Bed=bed,PreviousJobId=helper.CurJob?.GetUniqueLoadID(),Stage=stage};
            job.locomotionUrgency=LocomotionUrgency.Sprint;
            if(!helper.jobs.TryTakeOrderedJob(job,JobTag.Misc,false))return false;
            order.JobId=job.GetUniqueLoadID();state.Orders.Add(order);return true;
        }
        private static bool ClosedBed(Building_Bed bed) => bed.ForPrisoners && !bed.Destroyed && bed.Position.IsInPrisonCell(bed.Map) &&
            bed.GetRoom()?.PsychologicallyOutdoors==false && bed.GetRoom().ContainedBeds.All(b=>b.ForPrisoners) &&
            bed.Position.GetTemperature(bed.Map)>0 && bed.Position.GetTemperature(bed.Map)<35;
        private static bool RecruitmentRoom(Map map,ColonyState colony,PrisonState state)
        {
            int pending=state.Prisoners.Count(r=>!r.Completed && !r.Manual && r.Destination==PrisonerDestination.Recruit);
            int beds=map.listerThings.AllThings.OfType<Building_Bed>().Where(b=>b.Faction==Faction.OfPlayer && !b.ForPrisoners && !b.Medical && b.GetRoom()?.PsychologicallyOutdoors==false).Sum(b=>b.SleepingSlotsCount);
            return map.mapPawns.FreeColonistsSpawnedCount+pending<state.PopulationTarget &&
                beds>map.mapPawns.FreeColonistsSpawnedCount+pending && colony.EstimatedFoodDays>=3;
        }
        private static void Mode(PrisonerRecord record,PrisonerInteractionModeDef mode,Ideo conversion=null)
        {
            var guest=record.Pawn.guest;
            if(guest.ExclusiveInteractionMode==mode && guest.ideoForConversion==conversion && record.AppliedMode==mode)return;
            guest.SetExclusiveInteraction(mode);guest.ideoForConversion=conversion;
            record.AppliedMode=mode;record.AppliedConversion=conversion;
        }
        public static void Apply(Map map,ColonyState colony,System.Collections.Generic.List<RoomProject> projects,PrisonState state,bool build)
        {
            int now=Find.TickManager.TicksGame;
            foreach(var order in state.Orders.ToList())
            {
                var helper=order.Helper;
                if(helper?.CurJob?.GetUniqueLoadID()==order.JobId)order.PreviousJobId=null;
                bool manual=helper?.Drafted==true || helper?.CurJob?.playerForced==true && helper.CurJob.GetUniqueLoadID()!=order.JobId && helper.CurJob.GetUniqueLoadID()!=order.PreviousJobId;
                if(manual){if(!state.Excluded.Contains(helper))state.Excluded.Add(helper);End(state,order,false);continue;}
                if(!Able(helper) || order.Patient==null || order.Patient.Dead || colony.Threat.Immediate || EmergencyManager.LocalFire(map) || OwnPatients(map) ||
                    order.Bed!=null && (!order.Bed.Spawned || !ClosedBed(order.Bed) || float.IsPositiveInfinity(Route(helper,helper.Position,order.Bed.Position,colony))) ||
                    order.Patient.Spawned && float.IsPositiveInfinity(Route(helper,helper.Position,order.Patient.Position,colony)))
                {End(state,order,true);continue;}
                if(helper.CurJob?.GetUniqueLoadID()!=order.JobId && helper.CurJob?.GetUniqueLoadID()!=order.PreviousJobId)End(state,order,false);
            }
            foreach(var task in projects.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.PrisonerBed && t.Owned && t.Complete(map)).ToList())
            {
                var bed=task.Position.GetThingList(map).OfType<Building_Bed>().FirstOrDefault(b=>b.def==task.Def && b.Position==task.Position);
                if(bed?.ForPrisoners==true && !bed.Position.IsInPrisonCell(map))ConfigureBed(bed);
            }
            var prisoners=map.mapPawns.AllPawnsSpawned.Where(p=>p.IsPrisonerOfColony).ToList();
            var beds=map.listerThings.AllThings.OfType<Building_Bed>().Where(b=>b.Faction==Faction.OfPlayer && ClosedBed(b)).ToList();
            state.Capacity=beds.Count;
            if(build && !colony.Threat.Immediate && now-state.LastPlan>=600)
            {state.LastPlan=now;PrisonPlanner.Plan(map,projects,state);}
            foreach(var pawn in prisoners.Where(p=>!state.Prisoners.Any(r=>r.Pawn==p)))
                state.Prisoners.Add(new PrisonerRecord{Pawn=pawn,Manual=true,Destination=PrisonerDestination.Manual,Status="Controle manual preservado."});
            foreach(var record in state.Prisoners.Where(r=>!r.Completed).ToList())
            {
                var pawn=record.Pawn;
                if(pawn==null || pawn.Dead){record.Completed=true;record.Status="Falecido.";continue;}
                if(pawn.Faction==Faction.OfPlayer && !pawn.IsPrisoner)
                {record.Completed=true;record.Status="Recrutado; integrado à gestão da colônia.";state.Record(pawn.LabelShort+": recrutado.");continue;}
                if(!pawn.IsPrisonerOfColony)
                {
                    if(record.Owned)
                    {
                        record.Completed=true;record.Status="Saiu da prisão.";
                        int change=record.OriginalFaction!=null?record.OriginalFaction.GoodwillWith(Faction.OfPlayer)-record.ReleaseGoodwill:0;
                        state.Record(pawn.LabelShort+": saiu da prisão."+(record.AppliedMode==PrisonerInteractionModeDefOf.Release?" Variação observada da relação: "+change+".":""));
                    }
                    else if(!state.Orders.Any(o=>o.Patient==pawn))state.Prisoners.Remove(record);
                    continue;
                }
                if(record.Manual)continue;
                if(!record.Owned)
                {
                    record.Owned=true;record.CapturedTick=now;record.LastProgressTick=now;
                    record.OriginalMode=pawn.guest.ExclusiveInteractionMode;record.OriginalConversion=pawn.guest.ideoForConversion;
                    state.Record(pawn.LabelShort+": capturado para "+(record.Destination==PrisonerDestination.Recruit?"recrutamento":"tratamento e liberação")+".");
                }
                if(record.AppliedMode!=null && (pawn.guest.ExclusiveInteractionMode!=record.AppliedMode || pawn.guest.ideoForConversion!=record.AppliedConversion))
                {record.Manual=true;record.Status="Interação alterada pelo jogador; controle manual.";continue;}
                bool injured=pawn.health.HasHediffsNeedingTend() || pawn.Downed || pawn.health.hediffSet.BleedRateTotal>0 || HealthAIUtility.ShouldSeekMedicalRest(pawn);
                if(injured){Mode(record,PrisonerInteractionModeDefOf.MaintainOnly);record.Status="Tratar e recuperar antes de decidir.";}
                else if(colony.Threat.Immediate || map.GetComponent<AutonomousRimMapComponent>().SecondarySuspended){Mode(record,PrisonerInteractionModeDefOf.MaintainOnly);record.Status="Aguardar mapa seguro.";}
                else if(record.Destination==PrisonerDestination.TreatAndRelease || !pawn.guest.Recruitable)
                {
                    if(record.AppliedMode!=PrisonerInteractionModeDefOf.Release)record.ReleaseGoodwill=record.OriginalFaction?.GoodwillWith(Faction.OfPlayer)??0;
                    Mode(record,PrisonerInteractionModeDefOf.Release);
                    record.Status=record.OriginalFaction?.def.permanentEnemy==true?"Tratar e liberar; facção permanentemente hostil não rende diplomacia.":"Liberação nativa após recuperação.";
                }
                else
                {
                    var target=ModsConfig.IdeologyActive?Faction.OfPlayer.ideos?.PrimaryIdeo:null;
                    bool convert=target!=null && pawn.Ideo!=target;
                    Mode(record,convert?PrisonerInteractionModeDefOf.Convert:PrisonerInteractionModeDefOf.AttemptRecruit,convert?target:null);
                    record.Status=convert?"Converter para a ideologia da colônia.":"Reduzir resistência e recrutar.";
                }
                float resistance=pawn.guest.Resistance,certainty=pawn.ideo?.Certainty??0;
                if(resistance<record.LastResistance-.01f || certainty<record.LastCertainty-.01f)record.LastProgressTick=now;
                record.LastResistance=resistance;record.LastCertainty=certainty;
                if(now-record.LastProgressTick>GenDate.TicksPerDay*5 && record.Destination==PrisonerDestination.Recruit)
                    record.Status+=" Sem progresso há cinco dias: verificar carcereiro, ideologia, comida e interações.";
            }
            foreach(var old in state.Prisoners.Where(r=>r.Completed).OrderByDescending(r=>r.CapturedTick).Skip(40).ToList())state.Prisoners.Remove(old);
            state.Status=$"Prisão: {prisoners.Count}/{state.Capacity} camas; meta {state.PopulationTarget} colonos; {state.Orders.Count} atendimentos.";
            if(state.Capacity==0)state.Status+=" Aguardando celas fechadas, camas configuradas e temperatura segura.";
            if(colony.Threat.Immediate || EmergencyManager.LocalFire(map) || OwnPatients(map) || now<state.NextAction)return;
            state.NextAction=now+120;
            // Colony rescue and treatment always precede work on enemy survivors.
            foreach(var record in state.Prisoners.Where(r=>!r.Completed && !r.Manual && r.Pawn?.IsPrisonerOfColony==true).OrderBy(r=>HealthUtility.TicksUntilDeathDueToBloodLoss(r.Pawn)))
            {
                var patient=record.Pawn;
                if(state.Orders.Any(o=>o.Patient==patient))continue;
                foreach(var helper in map.mapPawns.FreeColonistsSpawned.Where(p=>Available(p,state)).OrderByDescending(p=>p.skills.GetSkill(SkillDefOf.Medicine).Level))
                {
                    if(float.IsPositiveInfinity(Route(helper,helper.Position,patient.Position,colony)) || !helper.CanReserve(patient))continue;
                    Job job=null;
                    if(patient.InBed() && patient.health.HasHediffsNeedingTend() && patient.playerSettings.medCare!=MedicalCareCategory.NoCare && !helper.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                    {
                        var tend=new WorkGiver_Tend();
                        if(tend.HasJobOnThing(helper,patient))job=tend.JobOnThing(helper,patient);
                        if(job?.targetB.Thing is Thing med && med.Spawned && float.IsPositiveInfinity(Route(helper,helper.Position,med.Position,colony)))job=null;
                    }
                    else if(!helper.WorkTypeIsDisabled(WorkTypeDefOf.Warden) && record.AppliedMode==PrisonerInteractionModeDefOf.Release)
                    {
                        job=new WorkGiver_Warden_ReleasePrisoner().JobOnThing(helper,patient);
                        if(job!=null && float.IsPositiveInfinity(Route(helper,patient.Position,job.targetB.Cell,colony)))job=null;
                    }
                    if(job==null && !helper.WorkTypeIsDisabled(WorkTypeDefOf.Warden) && (patient.needs?.food?.CurLevel??1)<.4f)
                    {
                        job=new WorkGiver_Warden_Feed().JobOnThing(helper,patient)??new WorkGiver_Warden_DeliverFood().JobOnThing(helper,patient);
                        if(job?.targetA.Thing is Thing source && source.Spawned && float.IsPositiveInfinity(Route(helper,helper.Position,source.Position,colony)))job=null;
                    }
                    if(job==null && !map.GetComponent<AutonomousRimMapComponent>().SecondarySuspended && !helper.WorkTypeIsDisabled(WorkTypeDefOf.Warden) &&
                        (patient.needs?.food?.CurLevel??1)>=.4f && !patient.health.HasHediffsNeedingTend())
                    {
                        if(record.AppliedMode==PrisonerInteractionModeDefOf.Convert && helper.Ideo==record.AppliedConversion)
                            job=new WorkGiver_Warden_Convert().JobOnThing(helper,patient);
                        else if(record.AppliedMode==PrisonerInteractionModeDefOf.AttemptRecruit)
                            job=new WorkGiver_Warden_Chat().JobOnThing(helper,patient);
                    }
                    if(Issue(state,helper,patient,null,job,"Atender prisioneiro"))return;
                }
            }
            if(map.GetComponent<AutonomousRimMapComponent>().SecondarySuspended)return;
            if(!map.mapPawns.FreeColonistsSpawned.Any(p=>Able(p) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)))
            {state.Status+=" Captura suspensa: sem médico apto.";return;}
            if(prisoners.Count+state.Prisoners.Count(r=>!r.Owned && !r.Manual && !r.Completed)>=Math.Min(4,beds.Count) || colony.EstimatedFoodDays<2)return;
            foreach(var patient in map.mapPawns.AllPawnsSpawned.Where(p=>p.Downed && !p.Dead && p.RaceProps.Humanlike && !p.IsPrisoner &&
                p.Faction!=null && p.HostileTo(Faction.OfPlayer) && !p.Position.Fogged(map) && p.CanBeCaptured()).OrderBy(p=>HealthUtility.TicksUntilDeathDueToBloodLoss(p)))
            {
                var previous=state.Prisoners.FirstOrDefault(r=>r.Pawn==patient);
                // A completed automatic record (escape/release) must not forever
                // blacklist a hostile survivor who can be safely captured again.
                if(previous!=null && (!previous.Completed || previous.Manual) || state.Orders.Any(o=>o.Patient==patient))continue;
                foreach(var helper in map.mapPawns.FreeColonistsSpawned.Where(p=>Available(p,state) && !p.WorkTagIsDisabled(WorkTags.Hauling)).OrderByDescending(p=>p.skills.GetSkill(SkillDefOf.Medicine).Level))
                {
                    if(!helper.CanReserve(patient) || !HealthAIUtility.CanRescueNow(helper,patient,true))continue;
                    var bed=RestUtility.FindBedFor(patient,helper,false,false,GuestStatus.Prisoner);
                    if(bed==null || !beds.Contains(bed) || !helper.CanReserve(bed))continue;
                    float transport=Route(helper,helper.Position,patient.Position,colony)+Route(helper,patient.Position,bed.Position,colony);
                    if(float.IsPositiveInfinity(transport))continue;
                    int death=HealthUtility.TicksUntilDeathDueToBloodLoss(patient);
                    float tend=600/Math.Max(.1f,helper.GetStatValue(StatDefOf.MedicalTendSpeed));
                    if(death<transport+tend+600 && patient.health.HasHediffsNeedingTend() && !helper.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                    {
                        if(Issue(state,helper,patient,bed,JobMaker.MakeJob(JobDefOf.TendPatient,patient),"Estabilizar sobrevivente"))return;
                        continue;
                    }
                    if(death<transport+600)continue;
                    var job=JobMaker.MakeJob(JobDefOf.Capture,patient,bed);job.count=1;
                    bool recruit=patient.guest?.Recruitable==true && RecruitmentRoom(map,colony,state) && PrisonPlanner.CandidateScore(map,patient)>=8;
                    if(Issue(state,helper,patient,bed,job,"Capturar"))
                    {
                        if(previous!=null)state.Prisoners.Remove(previous);
                        state.Prisoners.Add(new PrisonerRecord{Pawn=patient,OriginalFaction=patient.Faction,
                            Destination=recruit?PrisonerDestination.Recruit:PrisonerDestination.TreatAndRelease});return;
                    }
                }
            }
        }
    }
}
