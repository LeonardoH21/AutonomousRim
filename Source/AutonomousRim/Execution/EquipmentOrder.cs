using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    public sealed class EquipmentOrder : IExposable
    {
        public Pawn Pawn;
        public Thing Item;
        public string JobId;
        public int IssuedTick;
        public bool Wear;
        public bool Pending;
        public bool WasForced;
        public List<Apparel> ReleasedApparel = new List<Apparel>();

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_References.Look(ref Item, "item");
            Scribe_Values.Look(ref JobId, "jobId");
            Scribe_Values.Look(ref IssuedTick, "issuedTick");
            Scribe_Values.Look(ref Wear, "wear");
            Scribe_Values.Look(ref Pending, "pending");
            Scribe_Values.Look(ref WasForced, "wasForced");
            Scribe_Collections.Look(ref ReleasedApparel, "releasedApparel", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) ReleasedApparel = ReleasedApparel ?? new List<Apparel>();
        }
    }
}
