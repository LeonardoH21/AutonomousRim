using System.Collections.Generic;
using AutonomousRim.Execution;
using Verse;

namespace AutonomousRim.Core
{
    public enum EmergencyPhase { Normal, Danger, Securing, Recovery }
    public sealed class EmergencyState : IExposable
    {
        public EmergencyPhase Phase;
        public int ClearSince = -1, RecoverySince = -1;
        public string Decision = "Rotina normal";
        public int DeferredRescues, MissingShelters;
        public List<WorkPriorityChange> Work = new List<WorkPriorityChange>();
        public List<CombatOrder> Evacuations = new List<CombatOrder>();
        public List<Pawn> Excluded = new List<Pawn>();
        public List<MedicalOrder> Medical = new List<MedicalOrder>();
        public bool BlockSecondary => Phase == EmergencyPhase.Danger || Phase == EmergencyPhase.Securing;
        public bool BlockExpansion => BlockSecondary;
        public void Update(bool danger, bool needsCare, int tick)
        {
            if (danger) { Phase=EmergencyPhase.Danger; ClearSince=RecoverySince=-1; return; }
            if (Phase==EmergencyPhase.Danger) { Phase=EmergencyPhase.Securing; ClearSince=tick; }
            if (Phase==EmergencyPhase.Securing && tick-ClearSince>=600)
            { Phase=EmergencyPhase.Recovery; RecoverySince=tick; }
            if (Phase==EmergencyPhase.Recovery && tick-RecoverySince>=3000 && !needsCare) Phase=EmergencyPhase.Normal;
        }
        public void ExposeData()
        {
            Scribe_Values.Look(ref Phase,"phase"); Scribe_Values.Look(ref ClearSince,"clearSince",-1);
            Scribe_Values.Look(ref RecoverySince,"recoverySince",-1); Scribe_Values.Look(ref Decision,"decision");
            Scribe_Collections.Look(ref Work,"work",LookMode.Deep);
            Scribe_Collections.Look(ref Evacuations,"evacuations",LookMode.Deep);
            Scribe_Collections.Look(ref Excluded,"excluded",LookMode.Reference);
            Scribe_Collections.Look(ref Medical,"medical",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)
            {
                Work=Work??new List<WorkPriorityChange>(); Evacuations=Evacuations??new List<CombatOrder>(); Excluded=Excluded??new List<Pawn>();
                Medical=Medical??new List<MedicalOrder>();Medical.RemoveAll(o=>o.Helper==null||o.Patient==null);
                Work.RemoveAll(c=>c.Pawn==null || c.Work==null); Evacuations.RemoveAll(c=>c.Pawn==null); Excluded.RemoveAll(p=>p==null);
            }
        }
    }
}
