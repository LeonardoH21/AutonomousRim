using Verse;
namespace AutonomousRim.Execution
{
    public sealed class CombatOrder : IExposable
    {
        public Pawn Pawn;
        public Pawn MeleeTarget;
        public int MeleeTargetTick;
        public string JobId, Role;
        public IntVec3 Anchor;
        public int LastOrderTick;
        public int RetreatUntilTick;
        public bool Retreated;
        public bool OriginalFireAtWill;
        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_References.Look(ref MeleeTarget, "meleeTarget");
            Scribe_Values.Look(ref MeleeTargetTick, "meleeTargetTick");
            Scribe_Values.Look(ref JobId, "jobId");
            Scribe_Values.Look(ref Role, "role");
            Scribe_Values.Look(ref Anchor, "anchor");
            Scribe_Values.Look(ref LastOrderTick, "lastOrderTick");
            Scribe_Values.Look(ref RetreatUntilTick, "retreatUntilTick");
            Scribe_Values.Look(ref Retreated, "retreated");
            Scribe_Values.Look(ref OriginalFireAtWill, "originalFireAtWill");
        }
    }
}
