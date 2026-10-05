using Verse;

namespace AutonomousRim.Execution
{
    public sealed class WorkPriorityChange : IExposable
    {
        public Pawn Pawn;
        public WorkTypeDef Work;
        public int Original;
        public int Applied;
        public bool UserOverride;

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_Defs.Look(ref Work, "work");
            Scribe_Values.Look(ref Original, "original");
            Scribe_Values.Look(ref Applied, "applied");
            Scribe_Values.Look(ref UserOverride, "userOverride");
        }
    }
}
