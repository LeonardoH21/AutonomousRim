using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Execution;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    // Supplements use actual native room cells, never a rectangular approximation of
    // an irregular player room. Existing furniture and manual settings stay untouched.
    public static class ExistingFacilityImprovements
    {
        public static void Plan(Map map, List<RoomProject> projects, ExistingFacilities inventory)
        {
            foreach(var service in inventory.Services.Where(s=>s.Room!=null && !s.Room.PsychologicallyOutdoors &&
                projects.Any(p=>p.Kind==s.Kind && p.ExistingFacility && p.ExistingFacilityAnchor==s.Anchor)))
            {
                var room=service.Room;
                var buildings=map.listerBuildings.allBuildingsColonist.Where(b=>b.GetRoom()==room).ToList();
                int Planned(string name)=>projects.SelectMany(BaseConstructionManager.Tasks).Count(t=>t.Def.defName==name && !t.Complete(map) && room.ContainsCell(t.Position));
                void Ensure(string name,int target,bool medical=false,string storage=null,IntVec3? near=null)
                {
                    var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);
                    if(def==null || !def.IsResearchFinished)return;
                    int actual=buildings.Count(b=>b.def==def && (!medical || b is Building_Bed bed && bed.Medical && bed.ForOwnerType==BedOwnerType.Colonist));
                    for(int count=actual+Planned(name);count<target;count++)
                        if(!Place(map,projects,service,def,medical,storage,near))break;
                }
                if(service.Kind=="Hospital")
                {
                    int target=Math.Max(1,(map.mapPawns.FreeColonistsSpawnedCount+3)/4);
                    int existing=buildings.OfType<Building_Bed>().Count(b=>b.Medical && b.ForOwnerType==BedOwnerType.Colonist);
                    string bed=DefDatabase<ThingDef>.GetNamed("HospitalBed").IsResearchFinished?"HospitalBed":"Bed";
                    // Increase capacity, without replacing the player's ordinary medical beds.
                    Ensure(bed,Math.Max(0,target-existing)+buildings.OfType<Building_Bed>().Count(b=>b.def.defName==bed && b.Medical && b.ForOwnerType==BedOwnerType.Colonist),true);
                    if(bed=="HospitalBed")
                    {
                        Ensure("HospitalBed",1,true);
                        var improved=buildings.OfType<Building_Bed>().FirstOrDefault(b=>b.def.defName=="HospitalBed" && b.Medical && b.ForOwnerType==BedOwnerType.Colonist);
                        if(improved!=null)Ensure("VitalsMonitor",1,near:improved.Position);
                    }
                    if(!inventory.Services.Any(s=>s.Kind=="Medicamentos" && s.Room==room) &&
                        !projects.SelectMany(BaseConstructionManager.Tasks).Any(t=>t.StorageKind=="Medicamentos" && room.ContainsCell(t.Position)))
                    {
                        if(DefDatabase<ThingDef>.GetNamed("Shelf").IsResearchFinished)Ensure("Shelf",buildings.Count(b=>b.def.defName=="Shelf")+1,storage:"Medicamentos");
                        else if(!projects.Any(p=>p.LayoutSlot=="existing:medicine:"+service.Anchor))
                        {
                            var cells=room.Cells.Where(c=>c.Standable(map) && c.GetZone(map)==null && c.GetEdifice(map)==null &&
                                !c.GetThingList(map).Any(t=>t is Blueprint || t is Frame) &&
                                !map.listerBuildings.allBuildingsColonist.Any(b=>b.def.IsDoor && b.Position.AdjacentToCardinal(c)))
                                .OrderBy(c=>c.DistanceToSquared(service.Anchor)).Take(2).ToList();
                            if(cells.Count==2)projects.Add(new RoomProject{Kind="Medicamentos",LayoutSlot="existing:medicine:"+service.Anchor,
                                ExistingFacilityKind=service.Kind,ExistingFacilityAnchor=service.Anchor,
                                Origin=service.Anchor,InteriorSize=1,RequiresRoof=false,StorageCells=cells,Priority=ConstructionPriority.High});
                        }
                    }
                }
                if(service.Kind=="Pesquisa")
                {
                    Ensure("HiTechResearchBench",1);
                    var bench=buildings.FirstOrDefault(b=>b.def.defName=="HiTechResearchBench");
                    if(bench!=null)Ensure("MultiAnalyzer",1,near:bench.Position);
                }
                if(service.Kind=="Energia e climatização")Ensure("Battery",2);
                if(service.Kind=="Estoque")Ensure("Shelf",Math.Max(1,Math.Min(4,room.CellCount/20)),storage:"Estoque");
                if(service.Kind=="Freezer")Ensure("Shelf",Math.Max(1,Math.Min(4,room.CellCount/20)),storage:"Freezer");
                if(service.Kind=="Oficina")
                    foreach(var name in new[]{"TableStonecutter","HandTailoringBench","FueledSmithy","TableMachining","DrugLab","FabricationBench"})
                    {
                        var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);
                        if(def!=null && !map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Any(b=>b.def.AllRecipes.Intersect(def.AllRecipes).Any()))Ensure(name,1);
                    }
            }
        }
        private static bool Place(Map map,List<RoomProject> projects,ExistingFacilities.Service service,ThingDef def,bool medical,string storage,IntVec3? near)
        {
            var occupied=new HashSet<IntVec3>(projects.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.Def is ThingDef)
                .SelectMany(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size)));
            var access=map.listerBuildings.allBuildingsColonist.Where(b=>b.def.hasInteractionCell).Select(b=>b.InteractionCell).ToHashSet();
            foreach(var task in projects.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.Def is ThingDef d && d.hasInteractionCell))
                access.Add(task.Position+((ThingDef)task.Def).interactionCellOffset.RotatedBy(task.Rotation));
            var doors=map.listerBuildings.allBuildingsColonist.Where(b=>b.def.IsDoor).Select(b=>b.Position).ToList();
            foreach(var cell in service.Room.Cells.OrderBy(c=>c.DistanceToSquared(near??service.Anchor)))
            foreach(var rot in new[]{Rot4.North,Rot4.East,Rot4.South,Rot4.West})
            {
                var footprint=GenAdj.OccupiedRect(cell,rot,def.Size);
                if(near.HasValue && cell.DistanceToSquared(near.Value)>16)continue;
                if(footprint.Any(c=>!service.Room.ContainsCell(c) || occupied.Contains(c) || access.Contains(c) || c.GetEdifice(map)!=null ||
                    doors.Any(d=>d.AdjacentToCardinal(c)) || c.GetThingList(map).Any(t=>t is Blueprint || t is Frame)))continue;
                var interaction=cell+def.interactionCellOffset.RotatedBy(rot);
                if(def.hasInteractionCell && (!service.Room.ContainsCell(interaction) || !interaction.Standable(map) || occupied.Contains(interaction)))continue;
                ThingDef stuff=def.MadeFromStuff?GenStuff.DefaultStuffFor(def):null;
                if(!GenConstruct.CanPlaceBlueprintAt(def,cell,rot,map,stuffDef:stuff))continue;
                string slot="existing:add:"+service.Kind+":"+def.defName+":"+cell;
                if(projects.Any(p=>p.LayoutSlot==slot))continue;
                projects.Add(new RoomProject{Kind="Complemento de "+service.Kind,LayoutSlot=slot,Origin=cell,InteriorSize=1,RequiresRoof=false,
                    ExistingFacilityKind=service.Kind,ExistingFacilityAnchor=service.Anchor,
                    Priority=ConstructionPriority.High,Furniture=new List<ConstructionTask>{new ConstructionTask{Def=def,Stuff=stuff,Position=cell,Rotation=rot,MedicalBed=medical,StorageKind=storage}}});
                return true;
            }
            return false;
        }
    }
}
