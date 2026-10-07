using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    public sealed class ScheduleChange : IExposable
    {
        public Pawn Pawn;
        public List<TimeAssignmentDef> Original = new List<TimeAssignmentDef>();
        public List<TimeAssignmentDef> Applied = new List<TimeAssignmentDef>();
        public List<bool> UserOverride = new List<bool>();
        public bool Recovery;
        public bool PersonalRecovery;
        public int RecoveryUntil;
        public string LastReason;
        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn,"pawn");
            Scribe_Collections.Look(ref Original,"original",LookMode.Def);
            Scribe_Collections.Look(ref Applied,"applied",LookMode.Def);
            Scribe_Collections.Look(ref UserOverride,"userOverride",LookMode.Value);
            Scribe_Values.Look(ref Recovery,"recovery"); Scribe_Values.Look(ref RecoveryUntil,"recoveryUntil");
            Scribe_Values.Look(ref PersonalRecovery,"personalRecovery");
            Scribe_Values.Look(ref LastReason,"lastReason");
            if(Scribe.mode==LoadSaveMode.PostLoadInit)
            {
                Original=Original??new List<TimeAssignmentDef>(); Applied=Applied??new List<TimeAssignmentDef>(); UserOverride=UserOverride??new List<bool>();
                while(Original.Count<24)Original.Add(TimeAssignmentDefOf.Anything); while(Applied.Count<24)Applied.Add(TimeAssignmentDefOf.Anything); while(UserOverride.Count<24)UserOverride.Add(false);
                if(Original.Count>24)Original.RemoveRange(24,Original.Count-24); if(Applied.Count>24)Applied.RemoveRange(24,Applied.Count-24); if(UserOverride.Count>24)UserOverride.RemoveRange(24,UserOverride.Count-24);
            }
        }
    }
}
