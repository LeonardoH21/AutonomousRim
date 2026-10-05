using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    public sealed class ManagedApparel : IExposable
    {
        public Pawn Pawn;
        public Apparel Apparel;
        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_References.Look(ref Apparel, "apparel");
        }
    }
}
