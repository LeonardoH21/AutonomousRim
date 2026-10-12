using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Execution;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    // Reconcile on every planning pass, including old saves and completed rooms.
    // Completion of research is not completion of its installation.
    public static class ModularFacilities
    {
        public static readonly string[] WorkshopBenches = {
            "TableStonecutter", "HandTailoringBench", "FueledSmithy", "TableMachining",
            "DrugLab", "FabricationBench", "ElectricSmelter", "BiofuelRefinery"
        };

        private static IEnumerable<ConstructionTask> Local(List<RoomProject> projects, RoomProject room) =>
            projects.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.Def is ThingDef &&
                !t.Def.defName.Contains("Conduit") && room.Footprint.Contains(t.Position));

        public static bool Place(Map map,List<RoomProject> projects,RoomProject room,string name,
            IntVec3 preferred,Rot4? rotation=null)
        {
            var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if(def==null || room.State==ConstructionState.Paused)return false;
            var rot=rotation??Rot4.North;
            var tasks=projects.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.Def is ThingDef && !t.Def.defName.Contains("Conduit")).ToList();
            var occupied=tasks.SelectMany(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size)).ToHashSet();
            occupied.UnionWith(map.listerBuildings.allBuildingsColonist.Where(b=>!b.def.defName.Contains("Conduit")).SelectMany(b=>b.OccupiedRect()));
            var interactions=tasks.Where(t=>((ThingDef)t.Def).hasInteractionCell)
                .Select(t=>t.Position+((ThingDef)t.Def).interactionCellOffset.RotatedBy(t.Rotation)).ToHashSet();
            var doors=room.Shell.Where(t=>t.Def is ThingDef d && d.IsDoor).Select(t=>t.Position).ToList();
            foreach(var cell in room.Interior.Cells.OrderBy(c=>c.DistanceToSquared(preferred)))
            {
                var rect=GenAdj.OccupiedRect(cell,rot,def.Size);
                if(rect.Any(c=>!room.Interior.Contains(c) || occupied.Contains(c) || interactions.Contains(c) ||
                    c.GetEdifice(map)!=null || c.GetThingList(map).Any(t=>t is Blueprint || t is Frame) ||
                    doors.Any(d=>c.AdjacentToCardinal(d))))continue;
                // A continuous access spine; small wall lights may border it.
                if(def.hasInteractionCell && rect.Any(c=>c.x==room.Interior.CenterCell.x))continue;
                var access=cell+def.interactionCellOffset.RotatedBy(rot);
                if(def.hasInteractionCell && (!room.Interior.Contains(access) || occupied.Contains(access) || access.GetEdifice(map)!=null))continue;
                if(name=="WallLamp" && !room.Shell.Any(t=>t.Def==ThingDefOf.Wall && !t.CancelledByPlayer && t.Position==cell+rot.FacingCell))continue;
                room.Furniture.Add(ModularBasePlanner.Thing(name,cell,rot));
                room.Completed=false;
                return true;
            }
            return false;
        }

        private static void Ensure(Map map,List<RoomProject> projects,RoomProject room,string name,int count,int x,int z,bool unlocked=true)
        {
            var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if(def==null || unlocked && !def.IsResearchFinished)return;
            int planned=Local(projects,room).Count(t=>t.Def==def);
            int actual=map.listerBuildings.allBuildingsColonist.Count(b=>b.def==def && room.Interior.Contains(b.Position));
            for(int i=Math.Max(planned,actual);i<count;i++)
                if(!Place(map,projects,room,name,room.Origin+new IntVec3(x+i*3,0,z)))break;
        }

        public static void Reconcile(Map map,List<RoomProject> projects)
        {
            if(!projects.Any(p=>p.Kind==ModularBasePlanner.ReservationKind))return;
            foreach(var room in projects.Where(p=>ModularBasePlanner.IsModular(p) && p.Crop==null && p.Shell.Count>0 && p.ReferenceLayout==null).ToList())
            {
                // Bring existing automatic lights under the room's completion check without replacing them.
                foreach(var finish in projects.Where(p=>p.LayoutSlot=="interior:"+room.LayoutSlot))
                    foreach(var lamp in finish.Furniture.Where(t=>t.Def.defName=="WallLamp").ToList())
                    { finish.Furniture.Remove(lamp);room.Furniture.Add(lamp);room.Completed=false; }
                // Essential light belongs to the room, not a later optional finishing project.
                Ensure(map,projects,room,"WallLamp",1,room.InteriorSize-1,room.Height,false);
                if(room.Kind=="Pesquisa")Ensure(map,projects,room,"SimpleResearchBench",2,3,3,false);
                if(room.Kind=="Refeitório e recreação")Dining(map,projects,room);
                if(room.Kind=="Laboratório")
                {
                    Ensure(map,projects,room,"HiTechResearchBench",2,2,8);
                    Ensure(map,projects,room,"MultiAnalyzer",1,5,5);
                }
                if(room.Kind=="Energia e climatização")
                {
                    // Half module (11x5 inside): generator at (3,3), battery bank along the far side, aisle retained.
                    Ensure(map,projects,room,"Battery",4,7,1);
                }
                // A previous completion flag never hides newly added or unfinished essentials.
                if(BaseConstructionManager.Tasks(room).Any(t=>!t.Complete(map)) || room.RequiresRoof && room.RoofArea.Any(c=>!c.Roofed(map)))room.Completed=false;
            }
            foreach(var name in projects.Any(p=>p.ReferenceLayout=="workshop_storage_a") ? new string[0] : WorkshopBenches)
            {
                var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if(name=="ElectricSmelter" && !map.listerThings.AllThings.Any(t=>t.Spawned && t.def.defName=="ChunkSlagSteel"))continue;
                if(name=="BiofuelRefinery" && !map.listerBuildings.allBuildingsColonist.OfType<ThingWithComps>().Any(b=>
                    b.TryGetComp<CompRefuelable>()?.Props.fuelFilter.Allows(DefDatabase<ThingDef>.GetNamed("Chemfuel"))==true))continue;
                if(def==null || !def.IsResearchFinished || projects.SelectMany(BaseConstructionManager.Tasks).Any(t=>t.Def==def) ||
                    map.listerBuildings.allBuildingsColonist.Any(b=>b.def==def))continue;
                // Each installable bench serves crafting, medicine, recycling or trade.
                var rooms=projects.Where(p=>p.Kind=="Oficina" || p.Kind=="Fabricação").ToList();
                if(rooms.Count==0)continue;
                if(rooms.Any(room=>Place(map,projects,room,name,room.Origin+new IntVec3(2,0,8))))continue;
                var root=projects.First(p=>p.Kind==ModularBasePlanner.ReservationKind).LayoutAnchor;
                if(ModularBasePlanner.AddRoom(map,projects,root,"Oficina",4))
                    Place(map,projects,projects.Last(),name,projects.Last().Origin+new IntVec3(2,0,8));
            }
            AddDump(map,projects);
        }

        public static string FunctionalStatus(Map map,IReadOnlyList<RoomProject> projects,RoomProject room)
        {
            if(!ModularBasePlanner.IsModular(room))return null;
            if(room.Crop!=null)
            {
                int planted=room.StorageCells.Count(c=>c.GetPlant(map)?.def==room.Crop);
                return $"Plantação: {room.GrowingZone?.Cells.Count??0}/169 células; plantadas {planted}; "+
                    (room.Completed?"zona e agricultor disponíveis":"aguardando zona/cultura/acesso/agricultor");
            }
            if(room.Shell.Any(t=>!t.Complete(map)))return "Estrutura em construção";
            if(room.Furniture.Where(t=>t.Def is TerrainDef).Any(t=>!t.Complete(map)))return "Piso incompleto";
            if(room.RequiresRoof && room.RoofArea.Any(c=>!c.Roofed(map)))return "Cobertura incompleta";
            if(room.Furniture.Any(t=>!t.Complete(map)))return "Instalações essenciais pendentes";
            var lights=map.listerBuildings.allBuildingsColonist.OfType<ThingWithComps>().Where(b=>
                room.Interior.Contains(b.Position) && b.TryGetComp<CompGlower>()!=null).ToList();
            if(!lights.Any(b=>b.TryGetComp<CompPowerTrader>()==null || b.TryGetComp<CompPowerTrader>().PowerOn))return "Iluminação sem energia ou ausente";
            if(room.Kind=="Oficina" || room.Kind=="Fabricação" || room.Kind=="Laboratório")
                if(map.listerBuildings.allBuildingsColonist.OfType<ThingWithComps>().Any(b=>room.Interior.Contains(b.Position) &&
                    b.TryGetComp<CompPowerTrader>()!=null && !b.TryGetComp<CompPowerTrader>().PowerOn))return "Bancada/instalação aguardando energia";
            return "Funcional: estrutura, piso e instalações verificados";
        }

        private static void Dining(Map map,List<RoomProject> projects,RoomProject room)
        {
            Ensure(map,projects,room,"Table3x3c",1,3,3,false);
            var table=Local(projects,room).FirstOrDefault(t=>t.Def.defName=="Table3x3c");
            if(table!=null)
            {
                var rect=GenAdj.OccupiedRect(table.Position,table.Rotation,table.Def.Size);
                // Seats must touch the table; never relocate them to arbitrary empty corners.
                foreach(var c in rect.ExpandedBy(1).EdgeCells.Where(c=>rect.Cells.Any(n=>n.AdjacentToCardinal(c))))
                {
                    if(!room.Interior.Contains(c) || room.Shell.Any(t=>t.Def==ThingDefOf.Door && c.AdjacentToCardinal(t.Position)) ||
                        Local(projects,room).Any(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size).Contains(c)) || c.GetEdifice(map)!=null)continue;
                    var rot=c.x<rect.minX?Rot4.East:c.x>rect.maxX?Rot4.West:c.z<rect.minZ?Rot4.North:Rot4.South;
                    room.Furniture.Add(ModularBasePlanner.Thing("DiningChair",c,rot));room.Completed=false;
                }
            }
            Ensure(map,projects,room,"ChessTable",1,9,2,false);
            var chess=Local(projects,room).FirstOrDefault(t=>t.Def.defName=="ChessTable");
            if(chess!=null)
                foreach(var direction in new[]{IntVec3.West,IntVec3.East})
                {
                    var c=chess.Position+direction;
                    if(room.Interior.Contains(c) && !Local(projects,room).Any(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size).Contains(c)) && c.GetEdifice(map)==null)
                    {room.Furniture.Add(ModularBasePlanner.Thing("DiningChair",c,direction==IntVec3.West?Rot4.East:Rot4.West));room.Completed=false;}
                }
            // Better recreation expands after basic dining works. Its cloth/electronics
            // must never become a prerequisite for eating at a table or adding a hospital.
            if(!room.Completed)return;
            string slot="leisure:"+room.LayoutSlot;
            var upgrade=projects.FirstOrDefault(p=>p.LayoutSlot==slot);
            if(upgrade==null)
            {
                upgrade=new RoomProject{Kind="Lazer avançado",LayoutSlot=slot,Origin=room.Origin,
                    InteriorSize=room.InteriorSize,InteriorHeight=room.Height,RequiresRoof=false,Priority=ConstructionPriority.Normal};
                projects.Add(upgrade);
            }
            room=upgrade;
            Ensure(map,projects,room,"BilliardsTable",1,8,8);
            var tv=DefDatabase<ThingDef>.GetNamedSilentFail("TubeTelevision");
            if(tv?.IsResearchFinished==true && !Local(projects,room).Any(t=>t.Def==tv))
                Place(map,projects,room,"TubeTelevision",room.Origin+new IntVec3(2,0,10),Rot4.South);
            // TV viewers get seats; the large module keeps the north leisure zone apart from dining.
            if(Local(projects,room).Any(t=>t.Def.defName=="TubeTelevision"))
                foreach(int x in new[]{1,2,3})
                {
                    var c=room.Origin+new IntVec3(x,0,7);
                    if(room.Interior.Contains(c) && !Local(projects,room).Any(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size).Contains(c)) && c.GetEdifice(map)==null)
                    {room.Furniture.Add(ModularBasePlanner.Thing("DiningChair",c));room.Completed=false;}
                }
        }

        private static void AddDump(Map map,List<RoomProject> projects)
        {
            if(projects.Any(p=>p.Kind=="Despejo"))return;
            var butcher=projects.FirstOrDefault(p=>p.Kind=="Abate");
            if(butcher==null)return;
            foreach(var cell in GenRadial.RadialCellsAround(butcher.Origin,40,true))
            {
                var rect=new CellRect(cell.x,cell.z,5,5);
                if(!rect.InBounds(map) || projects.Any(p=>p.Footprint.ExpandedBy(3).Overlaps(rect)) ||
                    rect.Any(c=>c.Fogged(map) || !c.Standable(map) || c.GetZone(map)!=null || c.Roofed(map)))continue;
                projects.Add(new RoomProject{Kind="Despejo",LayoutSlot="modular-dump",Origin=cell,
                    InteriorSize=3,RequiresRoof=false,StorageCells=rect.Cells.ToList(),Priority=ConstructionPriority.High});
                break;
            }
        }
    }
}
