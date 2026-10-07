using System.Linq;
using AutonomousRim.Execution;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    public static class StrategicInfrastructure
    {
        // Additive upgrades preserve working equipment and the approved sector boundaries.
        public static bool Add(Map map,System.Collections.Generic.List<RoomProject> rooms)
        {
            if(!rooms.Any(r=>r.Kind==RingBasePlanner.ReservationKind))return false;
            bool changed=false;
            void add(string slot,string name,int preferredX,int preferredZ)
            {
                var room=rooms.FirstOrDefault(r=>r.LayoutSlot==slot);
                var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if(room==null || def==null || !def.IsResearchFinished || rooms.SelectMany(BaseConstructionManager.Tasks).Any(t=>t.Def==def) ||
                    map.listerBuildings.allBuildingsColonist.Any(b=>b.def==def))return;
                var candidates=room.Interior.Cells.OrderBy(c=>c.DistanceTo(room.Origin+new IntVec3(preferredX,0,preferredZ))).ToList();
                ConstructionTask chosen=null;
                foreach(var cell in candidates)
                {
                    var t=new ConstructionTask{Def=def,Position=cell,Rotation=Rot4.North,Stuff=def.MadeFromStuff?ThingDefOf.Steel:null};
                    room.Furniture.Add(t);
                    if(GenAdj.OccupiedRect(t.Position,t.Rotation,def.Size).Cells.All(room.Interior.Contains) && RingBasePlanner.Validate(map,rooms))
                    { chosen=t; changed=true; break; }
                    room.Furniture.Remove(t);
                }
                if(chosen==null)return;
                room.Completed=false; changed=true;
                if (slot == "fabrication") room.Priority = ConstructionPriority.Normal;
                var power=rooms.FirstOrDefault(r=>r.LayoutSlot=="power");
                var reservation=rooms.FirstOrDefault(r=>r.Kind==RingBasePlanner.ReservationKind);
                if(power!=null && reservation!=null && def.GetCompProperties<CompProperties_Power>()!=null)
                {
                    var trunk=RingBasePlanner.At(reservation.LayoutAnchor,33,24);
                    var cable=DefDatabase<ThingDef>.GetNamed("HiddenConduit");
                    void connect(IntVec3 c)
                    {
                        if(rooms.SelectMany(BaseConstructionManager.Tasks).Any(existing=>existing.Def==cable && existing.Position==c))return;
                        if(c.GetEdifice(map)?.def.GetCompProperties<CompProperties_Power>()?.transmitsPower==true)return;
                        power.Furniture.Add(new ConstructionTask{Def=cable,Position=c});power.Completed=false;
                    }
                    for(int cx=System.Math.Min(trunk.x,chosen.Position.x);cx<=System.Math.Max(trunk.x,chosen.Position.x);cx++)connect(new IntVec3(cx,0,trunk.z));
                    for(int cz=System.Math.Min(trunk.z,chosen.Position.z);cz<=System.Math.Max(trunk.z,chosen.Position.z);cz++)connect(new IntVec3(chosen.Position.x,0,cz));
                }
            }
            var research=rooms.FirstOrDefault(r=>r.LayoutSlot=="research");
            if(research!=null && !research.Furniture.Any(t=>t.Complete(map)))research.Priority=ConstructionPriority.High;
            add("research","HiTechResearchBench",3,8);
            add("research","MultiAnalyzer",5,5);
            add("fabrication","FabricationBench",3,4);
            add("shop0","TableMachining",8,1);
            add("shop0","ElectricSmithy",8,5);
            add("shop1","DrugLab",8,1);
            return changed;
        }
    }
}
