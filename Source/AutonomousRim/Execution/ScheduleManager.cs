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
        private static TimeAssignmentDef A(string name)=>DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(name) ?? TimeAssignmentDefOf.Anything;
        private static bool CanMeditate(Pawn pawn)=>pawn.psychicEntropy!=null;
        private static TimeAssignmentDef Normal(Pawn pawn,int hour,float rest,float joy)
        {
            if(rest<0.22f && (hour>=19 || hour<8))return TimeAssignmentDefOf.Sleep;
            if(rest<0.12f)return TimeAssignmentDefOf.Sleep;
            if(joy<0.22f && (hour>=17 && hour<21))return TimeAssignmentDefOf.Joy;
            if(CanMeditate(pawn) && hour>=12 && hour<13)return TimeAssignmentDefOf.Meditate;
            if(hour<6 || hour>=22)return TimeAssignmentDefOf.Sleep;
            if(hour>=8 && hour<17)return TimeAssignmentDefOf.Work;
            if(hour>=17 && hour<19)return TimeAssignmentDefOf.Joy;
            return TimeAssignmentDefOf.Anything;
        }
        private static TimeAssignmentDef EmergencyAssignment(Pawn pawn,int hour,float rest,float mood)
        {
            if(rest<0.18f || (rest<0.28f && hour>=21))return TimeAssignmentDefOf.Sleep;
            if(mood<0.25f && hour>=18 && hour<21)return TimeAssignmentDefOf.Joy;
            return TimeAssignmentDefOf.Work;
        }
        private static TimeAssignmentDef RecoveryAssignment(Pawn pawn,int hour)
        {
            if(hour<8 || hour>=21)return TimeAssignmentDefOf.Sleep;
            if(hour>=12 && hour<14)return TimeAssignmentDefOf.Joy;
            if(CanMeditate(pawn) && hour>=14 && hour<15)return TimeAssignmentDefOf.Meditate;
            if(hour>=8 && hour<17)return TimeAssignmentDefOf.Anything;
            return TimeAssignmentDefOf.Joy;
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
                if(emergency){change.Recovery=false;change.RecoveryUntil=0;change.LastReason=HasFire(map)?"incêndio":state.HostilePawnCount>0?"hostis":"colono ferido";}
                else if(change.LastReason!=null && !change.Recovery){change.Recovery=true;change.RecoveryUntil=tick+6000;change.LastReason="recuperação após emergência";}
                if(change.Recovery && tick>=change.RecoveryUntil){change.Recovery=false;change.LastReason=null;}
                if(change.Recovery)recovering++;
                float rest=pawn.needs?.rest?.CurLevelPercentage??1f;float joy=pawn.needs?.joy?.CurLevelPercentage??1f;float mood=pawn.needs?.mood?.CurLevelPercentage??1f;
                for(int hour=0;hour<24;hour++)
                {
                    var now=pawn.timetable.GetAssignment(hour);
                    if(now!=change.Applied[hour] && now!=change.Original[hour])change.UserOverride[hour]=true;
                    if(change.UserOverride[hour])continue;
                    var desired=emergency?EmergencyAssignment(pawn,hour,rest,mood):change.Recovery?RecoveryAssignment(pawn,hour):Normal(pawn,hour,rest,joy);
                    if(desired!=now){pawn.timetable.SetAssignment(hour,desired);changed++;}
                    change.Applied[hour]=desired;
                }
            }
            changes.RemoveAll(c=>c.Pawn==null || c.Pawn.Dead);
            return emergency?$"Agenda de emergência: {changed} horários ajustados; descanso crítico preservado.":recovering>0?$"Recuperação pós-emergência: {recovering} colonos com sono/recreação prioritários.":$"Agenda dinâmica: {changed} horários ajustados por necessidades e descanso.";
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
