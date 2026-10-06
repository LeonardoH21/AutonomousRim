using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    public sealed class ConstructionTask : IExposable
    {
        public ThingDef Def;
        public ThingDef Stuff;
        public IntVec3 Position;
        public Rot4 Rotation = Rot4.North;
        public Thing Pending;
        public bool Issued;
        public bool CancelledByPlayer;
        public void ExposeData()
        {
            Scribe_Defs.Look(ref Def, "def"); Scribe_Defs.Look(ref Stuff, "stuff");
            Scribe_Values.Look(ref Position, "position"); Scribe_Values.Look(ref Rotation, "rotation");
            Scribe_References.Look(ref Pending, "pending");
            Scribe_Values.Look(ref Issued, "issued"); Scribe_Values.Look(ref CancelledByPlayer, "cancelledByPlayer");
        }
        public bool Complete(Map map) => Position.InBounds(map) && Position.GetThingList(map).Exists(t =>
            t.def == Def && t.Position == Position && t.Rotation == Rotation && t.Faction == Faction.OfPlayer);
    }

    public sealed class RoomProject : IExposable
    {
        public string Kind;
        public IntVec3 Origin;
        public int InteriorSize;
        public List<ConstructionTask> Shell = new List<ConstructionTask>();
        public List<ConstructionTask> Furniture = new List<ConstructionTask>();
        public List<IntVec3> RoofOrders = new List<IntVec3>();
        public Zone_Stockpile Stockpile;
        public bool Started;
        public bool Completed;
        public CellRect Interior => new CellRect(Origin.x + 1, Origin.z + 1, InteriorSize, InteriorSize);
        public CellRect Footprint => new CellRect(Origin.x, Origin.z, InteriorSize + 2, InteriorSize + 2);
        public void ExposeData()
        {
            Scribe_Values.Look(ref Kind, "kind"); Scribe_Values.Look(ref Origin, "origin");
            Scribe_Values.Look(ref InteriorSize, "interiorSize");
            Scribe_Collections.Look(ref Shell, "shell", LookMode.Deep);
            Scribe_Collections.Look(ref Furniture, "furniture", LookMode.Deep);
            Scribe_Collections.Look(ref RoofOrders, "roofOrders", LookMode.Value);
            Scribe_References.Look(ref Stockpile, "stockpile");
            Scribe_Values.Look(ref Started, "started"); Scribe_Values.Look(ref Completed, "completed");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Shell = Shell ?? new List<ConstructionTask>(); Furniture = Furniture ?? new List<ConstructionTask>();
                RoofOrders = RoofOrders ?? new List<IntVec3>();
            }
        }
    }
}
