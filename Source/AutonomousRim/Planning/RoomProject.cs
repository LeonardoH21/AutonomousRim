using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    public enum ConstructionState { Planned, Active, WaitingMaterials, Blocked, Paused, Completed }
    public enum ConstructionPriority { Critical, High, Normal, Low }
    public sealed class ConstructionTask : IExposable
    {
        public BuildableDef Def;
        public TerrainDef OriginalTerrain;
        public ThingDef Stuff;
        public IntVec3 Position;
        public Rot4 Rotation = Rot4.North;
        public Thing Pending;
        public bool Issued;
        public bool Owned;
        public bool WasCompleted;
        public bool CancelledByPlayer;
        public float TargetTemperature = -999f;
        public bool TemperatureConfigured;
        public bool MedicalBed;
        public string StorageKind;
        public bool SettingsConfigured;
        public int RetryAfter;
        public int FailedJobs;
        public string LastFailure;
        public void ExposeData()
        {
            ThingDef thingDef = Def as ThingDef;
            TerrainDef floorDef = Def as TerrainDef;
            Scribe_Defs.Look(ref thingDef, "def"); Scribe_Defs.Look(ref floorDef, "floorDef");
            Def = (BuildableDef)thingDef ?? floorDef;
            Scribe_Defs.Look(ref Stuff, "stuff");
            Scribe_Defs.Look(ref OriginalTerrain, "originalTerrain");
            Scribe_Values.Look(ref Position, "position"); Scribe_Values.Look(ref Rotation, "rotation");
            Scribe_References.Look(ref Pending, "pending");
            Scribe_Values.Look(ref Issued, "issued"); Scribe_Values.Look(ref CancelledByPlayer, "cancelledByPlayer");
            Scribe_Values.Look(ref Owned, "owned");
            Scribe_Values.Look(ref WasCompleted, "wasCompleted");
            Scribe_Values.Look(ref TargetTemperature, "targetTemperature", -999f);
            Scribe_Values.Look(ref TemperatureConfigured, "temperatureConfigured");
            Scribe_Values.Look(ref MedicalBed, "medicalBed");
            Scribe_Values.Look(ref StorageKind, "storageKind");
            Scribe_Values.Look(ref SettingsConfigured, "settingsConfigured");
            Scribe_Values.Look(ref RetryAfter, "retryAfter");
            Scribe_Values.Look(ref FailedJobs, "failedJobs");
            Scribe_Values.Look(ref LastFailure, "lastFailure");
        }
        public bool Complete(Map map) => Position.InBounds(map) && (Def is TerrainDef terrain ? Position.GetTerrain(map) == terrain : Position.GetThingList(map).Exists(t =>
            t.def == Def && t.Position == Position && t.Rotation == Rotation && t.Faction == Faction.OfPlayer));
    }

    public sealed class RoomProject : IExposable
    {
        public string Kind;
        public IntVec3 Origin;
        public int InteriorSize;
        public int InteriorHeight;
        public bool RequiresRoof = true;
        public bool ReserveDoubleBed;
        public CellRect DoubleBedSpace => new CellRect(Origin.x + 4, Origin.z + 4, 2, 2);
        public List<ConstructionTask> Shell = new List<ConstructionTask>();
        public List<ConstructionTask> Furniture = new List<ConstructionTask>();
        public List<IntVec3> RoofOrders = new List<IntVec3>();
        public Zone_Stockpile Stockpile;
        public List<IntVec3> StorageCells = new List<IntVec3>();
        public string LayoutSlot;
        public IntVec3 LayoutAnchor;
        public List<IntVec3> RoofCells = new List<IntVec3>();
        public IEnumerable<IntVec3> RoofArea => RoofCells.Count > 0 ? RoofCells : Interior.Cells;
        public List<IntVec3> MineCells = new List<IntVec3>();
        public List<IntVec3> OwnedMineCells = new List<IntVec3>();
        public List<IntVec3> ClearCells = new List<IntVec3>();
        public List<IntVec3> OwnedClearCells = new List<IntVec3>();
        public ThingDef Crop;
        public Zone_Growing GrowingZone;
        public bool Started;
        public bool Completed;
        public ConstructionState State;
        public ConstructionPriority Priority = ConstructionPriority.High;
        public string BlockReason;
        public int LastProgressTick;
        public float LastProgress;
        public int LastRecoveryTick;
        public bool Stalled;
        public bool FunctionalStorage;
        public int Height => InteriorHeight > 0 ? InteriorHeight : InteriorSize;
        public CellRect Interior => new CellRect(Origin.x + 1, Origin.z + 1, InteriorSize, Height);
        public CellRect Footprint => new CellRect(Origin.x, Origin.z, InteriorSize + 2, Height + 2);
        public void ExposeData()
        {
            Scribe_Values.Look(ref Kind, "kind"); Scribe_Values.Look(ref Origin, "origin");
            Scribe_Values.Look(ref InteriorSize, "interiorSize");
            Scribe_Values.Look(ref InteriorHeight, "interiorHeight");
            Scribe_Values.Look(ref RequiresRoof, "requiresRoof", true);
            Scribe_Values.Look(ref ReserveDoubleBed, "reserveDoubleBed");
            Scribe_Collections.Look(ref Shell, "shell", LookMode.Deep);
            Scribe_Collections.Look(ref Furniture, "furniture", LookMode.Deep);
            Scribe_Collections.Look(ref RoofOrders, "roofOrders", LookMode.Value);
            Scribe_References.Look(ref Stockpile, "stockpile");
            Scribe_Collections.Look(ref StorageCells, "storageCells", LookMode.Value);
            Scribe_Values.Look(ref LayoutSlot, "layoutSlot");
            Scribe_Values.Look(ref LayoutAnchor, "layoutAnchor");
            Scribe_Collections.Look(ref RoofCells, "roofCells", LookMode.Value);
            Scribe_Collections.Look(ref MineCells, "mineCells", LookMode.Value);
            Scribe_Collections.Look(ref OwnedMineCells, "ownedMineCells", LookMode.Value);
            Scribe_Collections.Look(ref ClearCells, "clearCells", LookMode.Value);
            Scribe_Collections.Look(ref OwnedClearCells, "ownedClearCells", LookMode.Value);
            Scribe_Defs.Look(ref Crop, "crop");
            Scribe_References.Look(ref GrowingZone, "growingZone");
            Scribe_Values.Look(ref Started, "started"); Scribe_Values.Look(ref Completed, "completed");
            Scribe_Values.Look(ref State, "state"); Scribe_Values.Look(ref Priority, "priority", ConstructionPriority.High);
            Scribe_Values.Look(ref BlockReason, "blockReason");
            Scribe_Values.Look(ref LastProgressTick, "lastProgressTick"); Scribe_Values.Look(ref LastProgress, "lastProgress");
            Scribe_Values.Look(ref LastRecoveryTick, "lastRecoveryTick"); Scribe_Values.Look(ref FunctionalStorage, "functionalStorage");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Shell = Shell ?? new List<ConstructionTask>(); Furniture = Furniture ?? new List<ConstructionTask>();
                RoofOrders = RoofOrders ?? new List<IntVec3>();
                StorageCells = StorageCells ?? new List<IntVec3>();
                RoofCells = RoofCells ?? new List<IntVec3>();
                MineCells = MineCells ?? new List<IntVec3>();
                OwnedMineCells = OwnedMineCells ?? new List<IntVec3>();
                ClearCells = ClearCells ?? new List<IntVec3>();
                OwnedClearCells = OwnedClearCells ?? new List<IntVec3>();
            }
        }
    }
}
