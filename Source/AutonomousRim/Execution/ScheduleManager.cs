using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    public static class ScheduleManager
    {
        public static bool HasFire(Map map)
        {
            var fire=DefDatabase<ThingDef>.GetNamedSilentFail("Fire");
            return fire!=null && map.listerThings.AllThings.Any(t=>t.def==fire && t.Spawned);
        }
        public static bool Emergency(Map map,ColonyState state)=>map.GetComponent<AutonomousRimMapComponent>().SecondarySuspended || state.HostilePawnCount>0 || EmergencyManager.LocalFire(map) || state.DownedColonists>0;
        private static bool CanMeditate(Pawn pawn) => ModsConfig.RoyaltyActive && pawn.GetPsylinkLevel() > 0 &&
            !HealthAIUtility.ShouldSeekMedicalRest(pawn);
        private static TimeAssignmentDef Normal(Pawn pawn, int hour)
        {
            bool nightOwl = pawn.story?.traits?.allTraits.Any(t => t.def.defName == "NightOwl") == true;
            int routineHour = nightOwl ? (hour + 12) % 24 : hour;
            if (routineHour < 6 || routineHour >= 22) return TimeAssignmentDefOf.Sleep;
            if (CanMeditate(pawn) && routineHour == 12) return TimeAssignmentDefOf.Meditate;
            if (routineHour >= 8 && routineHour < 17) return TimeAssignmentDefOf.Work;
            if (routineHour >= 17 && routineHour < 19) return TimeAssignmentDefOf.Joy;
            return TimeAssignmentDefOf.Anything;
        }
        private static TimeAssignmentDef Assignment(Pawn pawn, int hour, bool emergency, bool recovery)
        {
            // Needs override the current and following hour, not tomorrow's whole timetable.
            int current = GenLocalDate.HourOfDay(pawn);
            if (hour == current || hour == (current + 1) % 24)
            {
                if (HealthAIUtility.ShouldSeekMedicalRest(pawn) || pawn.Downed) return TimeAssignmentDefOf.Anything;
                if ((pawn.needs?.food?.CurLevelPercentage ?? 1f) < .15f) return TimeAssignmentDefOf.Anything;
                if ((pawn.needs?.rest?.CurLevelPercentage ?? 1f) < (recovery ? .6f : .25f)) return TimeAssignmentDefOf.Sleep;
                if ((pawn.needs?.joy?.CurLevelPercentage ?? 1f) < (recovery ? .45f : .2f) ||
                    (pawn.needs?.mood?.CurLevelPercentage ?? 1f) < .3f) return TimeAssignmentDefOf.Joy;
            }
            var normal = Normal(pawn, hour);
            if (recovery) return normal == TimeAssignmentDefOf.Work ? TimeAssignmentDefOf.Anything : normal;
            if (emergency) return TimeAssignmentDefOf.Work;
            return normal;
        }
        private static void Ensure(ScheduleChange change,Pawn pawn)
        {
            change.Pawn=pawn;
            while(change.Original.Count<24)change.Original.Add(pawn.timetable.GetAssignment(change.Original.Count));
            while(change.Applied.Count<24)change.Applied.Add(pawn.timetable.GetAssignment(change.Applied.Count));
            while(change.UserOverride.Count<24)change.UserOverride.Add(false);
        }
        public static string Apply(Map map,ColonyState state,List<ScheduleChange> changes,bool enabled)
        {
            if(!enabled)return "Agenda automática desligada; agenda do jogador preservada.";
            bool emergency=Emergency(map,state);int tick=Find.TickManager.TicksGame;int changed=0,recovering=0;
            var colonists=map.mapPawns.FreeColonistsSpawned.Where(p=>p.timetable!=null && !p.Dead && !p.IsPrisoner).ToList();
            foreach(var pawn in colonists)
            {
                var change=changes.FirstOrDefault(c=>c.Pawn==pawn); if(change==null){change=new ScheduleChange();changes.Add(change);} Ensure(change,pawn);
                if (WorkReadiness.NeedsRecovery(pawn)) change.PersonalRecovery = true;
                else if (WorkReadiness.Restored(pawn)) change.PersonalRecovery = false;
                if(emergency){change.Recovery=false;change.RecoveryUntil=0;change.LastReason=HasFire(map)?"incêndio":state.HostilePawnCount>0?"hostis":"colono ferido";}
                else if(change.LastReason!=null && !change.Recovery){change.Recovery=true;change.RecoveryUntil=tick+6000;change.LastReason="recuperação após emergência";}
                if(change.Recovery && tick>=change.RecoveryUntil && WorkReadiness.Restored(pawn) && map.GetComponent<AutonomousRimMapComponent>().Emergency.Phase==EmergencyPhase.Normal){change.Recovery=false;change.LastReason=null;}
                if(change.Recovery || change.PersonalRecovery)recovering++;

                for(int hour=0;hour<24;hour++)
                {
                    var now=pawn.timetable.GetAssignment(hour);
                    if(now!=change.Applied[hour])change.UserOverride[hour]=true;
                    if(change.UserOverride[hour])continue;
                    var desired=Assignment(pawn,hour,emergency,change.Recovery || change.PersonalRecovery);
                    if(desired!=now){pawn.timetable.SetAssignment(hour,desired);changed++;}
                    change.Applied[hour]=desired;
                }
            }
            changes.RemoveAll(c=>c.Pawn==null || c.Pawn.Dead);
            return emergency?$"Agenda de emergência: {changed} horários ajustados; descanso crítico preservado.":recovering>0?$"Recuperação: {recovering} colonos com sono/recreação prioritários.":$"Agenda dinâmica: {changed} horários ajustados por necessidades e descanso.";
        }
        public static void Restore(List<ScheduleChange> changes)
        {
            foreach(var change in changes.Where(c=>c.Pawn?.timetable!=null))
                for(int hour=0;hour<Math.Min(24,Math.Min(change.Original.Count,change.Applied.Count));hour++)
                    if(!change.UserOverride.ElementAtOrDefault(hour) && change.Pawn.timetable.GetAssignment(hour)==change.Applied[hour])change.Pawn.timetable.SetAssignment(hour,change.Original[hour]);
            changes.Clear();
        }
    }
}
