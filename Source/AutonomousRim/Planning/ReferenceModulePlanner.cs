using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Execution;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    // Actual furniture and partition coordinates, not an approximation of the screenshot.
    public static class ReferenceModulePlanner
    {
        private static bool Structure(string name) => name == "Wall" || name == "Door" || name == "Autodoor" || name == "Cooler" || name == "Vent";
        private static RoomProject Reserve(Map map,List<RoomProject> projects,IntVec3 root,string kind,int parts)
        {
            if(!ModularBasePlanner.AddRoom(map,projects,root,kind,parts))return null;
            var p=projects.Last(); p.Shell.Clear();p.Furniture.Clear();p.StorageCells.Clear();p.NoRoofCells.Clear();
            return p;
        }
        private static ConstructionTask Make(RoomProject p,ReferenceLayoutData.Entry e,string name=null)
        {
            var t=ModularBasePlanner.Thing(name??e.Def,p.Origin+new IntVec3(e.X,0,e.Z),new Rot4(e.Rotation));
            if(e.Def=="Cooler")t.TargetTemperature=-2;
            if(e.Def=="Heater")t.TargetTemperature=21;
            if(e.Def=="HospitalBed")t.MedicalBed=true;
            if(e.Def=="Shelf")t.StorageKind=p.Kind=="Hospital"?"Medicamentos":p.Kind=="Estoque"?"Estoque":"Freezer";
            return t;
        }
        private static void Floors(Map map,RoomProject p)
        {
            var floor=DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor");
            var blocked=p.Shell.Where(t=>t.Def is ThingDef d && !d.IsDoor).Select(t=>t.Position).ToHashSet();
            foreach(var c in p.Interior.Cells.Concat(p.Shell.Where(t=>((ThingDef)t.Def).IsDoor).Select(t=>t.Position)).Distinct().Where(c=>!blocked.Contains(c)))
                p.Furniture.Add(new ConstructionTask{Def=floor,Position=c,OriginalTerrain=c.GetTerrain(map)});
            p.RoofCells=p.Interior.Cells.ToList();
        }
        private static void Shell(RoomProject p,IEnumerable<ReferenceLayoutData.Entry> entries)
        {
            foreach(var e in entries.Where(e=>Structure(e.Def)))
            {
                // Normal doors retain the airlock before Autodoors are researched.
                p.Shell.Add(Make(p,e,e.Def=="Autodoor"?"Door":null));
                if(e.Def=="Cooler")p.NoRoofCells.Add(p.Origin+new IntVec3(e.X,0,e.Z)+new Rot4(e.Rotation).FacingCell);
            }
        }
        private static void PrepareEntrances(Map map,List<RoomProject> projects,RoomProject room)
        {
            var cells=new HashSet<IntVec3>(room.NoRoofCells);
            foreach(var door in room.Shell.Where(t=>t.Def is ThingDef d && d.IsDoor && room.Footprint.EdgeCells.Contains(t.Position)))
            foreach(var direction in GenAdj.CardinalDirections.Where(d=>!room.Footprint.Contains(door.Position+d)))
                for(int step=1;step<=3;step++)cells.Add(door.Position+direction*step);
            var prep=new RoomProject{Kind="Preparação do terreno",LayoutSlot="entry:"+room.LayoutSlot,Origin=room.Origin,
                InteriorSize=room.InteriorSize,InteriorHeight=room.Height,RequiresRoof=false,Priority=ConstructionPriority.High};
            foreach(var c in cells.Where(c=>c.InBounds(map)))
            {
                if(c.GetThingList(map).OfType<Plant>().Any())prep.PlantCells.Add(c);
                if(c.GetEdifice(map) is Mineable)prep.MineCells.Add(c);
                else if(c.GetEdifice(map) is Building b && b.Faction==null && b.DeconstructibleBy(Faction.OfPlayer))prep.ClearCells.Add(c);
            }
            if(prep.PlantCells.Count+prep.MineCells.Count+prep.ClearCells.Count>0)projects.Add(prep);
        }
        public static void AddStores(Map map,List<RoomProject> projects,IntVec3 root)
        {
            // Existing player/built storage is preserved; new colonies reserve both stores early.
            if(projects.Any(p=>p.Kind=="Estoque" && p.ReferenceLayout==null))return;
            foreach(var id in new[]{"workshop_storage_a","workshop_storage_b"})
            {
                if(projects.Any(p=>p.ReferenceLayout==id))continue;
                var first=projects.FirstOrDefault(p=>p.ReferenceLayout=="workshop_storage_a");
                if(!ModularBasePlanner.AddRoom(map,projects,root,"Estoque",4,first?.Origin))return;
                var p=projects.Last();p.ReferenceLayout=id;p.Shell.Clear();p.Furniture.Clear();p.StorageCells.Clear();
                foreach(var c in p.Footprint.EdgeCells)
                {
                    var local=c-p.Origin;
                    bool door=(local.x==6 && (local.z==0 || local.z==12)) || (local.z==6 && (local.x==0 || local.x==12));
                    p.Shell.Add(ModularBasePlanner.Thing(door?"Door":"Wall",c,door && local.z==6?Rot4.East:Rot4.North));
                }
                Floors(map,p);
                foreach(var e in new[]{new ReferenceLayoutData.Entry("WallLamp",5,11,0),new ReferenceLayoutData.Entry("WallLamp",5,1,2),new ReferenceLayoutData.Entry("Heater",7,1,0)})
                    p.Furniture.Add(Make(p,e));
                // Reserve native footprints AND work cells, including benches not yet researched.
                var benches=StoreBenches(id).Select(e=>Make(p,e)).ToList();
                var excluded=benches.SelectMany(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size)).ToHashSet();
                excluded.UnionWith(benches.Where(t=>((ThingDef)t.Def).hasInteractionCell).Select(t=>t.Position+((ThingDef)t.Def).interactionCellOffset.RotatedBy(t.Rotation)));
                excluded.UnionWith(p.Furniture.Where(t=>t.Def is ThingDef).SelectMany(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size)));
                p.StorageCells=p.Interior.Cells.Where(c=>!excluded.Contains(c) && c.x!=p.Origin.x+6 && c.z!=p.Origin.z+6).ToList();
                PrepareEntrances(map,projects,p);
                projects.Add(new RoomProject{Kind=id.EndsWith("a")?"Oficina":"Fabricação",ReferenceLayout="storage-service",LayoutSlot="storage-service:"+p.LayoutSlot,
                    Origin=p.Origin,InteriorSize=11,InteriorHeight=11,RequiresRoof=false});
            }
        }
        private static ReferenceLayoutData.Entry[] StoreBenches(string id) => id.EndsWith("a") ? new[]{
            new ReferenceLayoutData.Entry("ElectricSmithy",2,1,2),new ReferenceLayoutData.Entry("ElectricTailoringBench",9,1,2),
            new ReferenceLayoutData.Entry("DrugLab",2,11,0),new ReferenceLayoutData.Entry("FabricationBench",9,10,0)
        } : new[]{
            new ReferenceLayoutData.Entry("TableStonecutter",1,3,3),new ReferenceLayoutData.Entry("TableMachining",11,3,1),
            new ReferenceLayoutData.Entry("TableSculpting",2,11,0),new ReferenceLayoutData.Entry("ElectricSmelter",11,9,1),
            new ReferenceLayoutData.Entry("BiofuelRefinery",2,8,3),new ReferenceLayoutData.Entry("Brewery",8,11,0)
        };
        private static void ReconcileStore(Map map,RoomProject p)
        {
            var entries=StoreBenches(p.ReferenceLayout);
            foreach(var e in entries)
            {
                string basic=e.Def=="ElectricSmithy"?"FueledSmithy":e.Def=="ElectricTailoringBench"?"HandTailoringBench":null;
                var existing=p.Furniture.FirstOrDefault(t=>t.Position==p.Origin+new IntVec3(e.X,0,e.Z) && t.Def is ThingDef && t.Def.defName!="WallLamp" && t.Def.defName!="Shelf");
                if(existing!=null){if(basic!=null)Upgrade(existing,e.Def);continue;}
                var def=DefDatabase<ThingDef>.GetNamed(e.Def);
                if(!def.IsResearchFinished && basic!=null)p.Furniture.Add(Make(p,e,basic));
                else AddUnlocked(map,p,e);
            }
            var reserved=entries.Select(e=>Make(p,e)).ToList();
            var occupied=reserved.Concat(p.Furniture.Where(t=>t.Def is ThingDef)).SelectMany(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size)).ToHashSet();
            occupied.UnionWith(reserved.Where(t=>((ThingDef)t.Def).hasInteractionCell).Select(t=>t.Position+((ThingDef)t.Def).interactionCellOffset.RotatedBy(t.Rotation)));
            var shelf=DefDatabase<ThingDef>.GetNamed("Shelf");
            if(!shelf.IsResearchFinished)return;
            foreach(int x in new[]{2,5,8})foreach(int z in new[]{3,4,8,9})
            {
                var e=new ReferenceLayoutData.Entry("Shelf",x,z,2);var task=Make(p,e);
                var rect=GenAdj.OccupiedRect(task.Position,task.Rotation,shelf.size);
                if(rect.Any(c=>occupied.Contains(c) || !p.Interior.Contains(c) || c.x==p.Origin.x+6 || c.z==p.Origin.z+6))continue;
                AddUnlocked(map,p,e,"Estoque");occupied.UnionWith(rect);
            }
        }
        public static void AddFood(Map map,List<RoomProject> projects,IntVec3 root)
        {
            var p=Reserve(map,projects,root,"Freezer",4);if(p==null)return;
            p.ReferenceLayout="kitchen_butcher_freezer";
            var entries=ReferenceLayoutData.Get(p.ReferenceLayout); Shell(p,entries);Floors(map,p);
            p.StorageCells=p.Interior.Cells.Where(c=>
                (c.x-p.Origin.x>=9 || c.z-p.Origin.z>=4) &&
                !(c.x-p.Origin.x<=2 && c.z-p.Origin.z>=8) &&
                !p.Shell.Any(t=>t.Position==c)).ToList();
            foreach(var e in entries.Where(e=>e.Def=="WallLamp" && e.Z!=2))p.Furniture.Add(Make(p,e));
            var cook=new RoomProject{Kind="Cozinha",ReferenceLayout="food-service",LayoutSlot="service:cook:"+p.LayoutSlot,
                LayoutAnchor=root,Origin=p.Origin+new IntVec3(4,0,0),InteriorSize=3,InteriorHeight=2};
            var butcher=new RoomProject{Kind="Abate",ReferenceLayout="food-service",LayoutSlot="service:butcher:"+p.LayoutSlot,
                LayoutAnchor=root,Origin=p.Origin,InteriorSize=3,InteriorHeight=2};
            cook.Shell=p.Shell.Where(t=>cook.Footprint.Contains(t.Position)).ToList();
            butcher.Shell=p.Shell.Where(t=>butcher.Footprint.Contains(t.Position)).ToList();
            foreach(var e in entries.Where(e=>e.Def=="ElectricStove"))cook.Furniture.Add(Make(p,e,"FueledStove"));
            foreach(var e in entries.Where(e=>e.Def=="TableButcher" || e.Def=="WallLamp" && e.Z==2))butcher.Furniture.Add(Make(p,e));
            projects.Add(cook);projects.Add(butcher);PrepareEntrances(map,projects,p);
        }
        public static void AddEnergy(Map map,List<RoomProject> projects,IntVec3 root,bool auxiliary=false)
        {
            var p=Reserve(map,projects,root,auxiliary?"Gerador auxiliar":"Energia e climatização",auxiliary?2:4);if(p==null)return;
            var origin=p.Origin;var entries=ReferenceLayoutData.Get("energy_hospital");
            p.ReferenceLayout="energy_bank";p.InteriorHeight=5;
            if(!auxiliary)p.LayoutSlot=p.LayoutSlot.Substring(0,p.LayoutSlot.LastIndexOf(':')+1)+"3";
            Shell(p,entries.Where(e=>e.Z<=6));Floors(map,p);
            p.NoRoofCells.Add(p.Origin+new IntVec3(13,0,3));PrepareEntrances(map,projects,p);
            p.Furniture.Add(Make(p,entries.First(e=>e.Def=="WoodFiredGenerator")));
            if(auxiliary)return;
            var hospital=new RoomProject{Kind="Hospital",ReferenceLayout="hospital",LayoutSlot=p.LayoutSlot.Substring(0,p.LayoutSlot.LastIndexOf(':')+1)+"12",
                LayoutAnchor=root,Origin=origin+new IntVec3(0,0,6),InteriorSize=11,InteriorHeight=5};
            foreach(var e in entries.Where(e=>e.Z>=6 && Structure(e.Def)))hospital.Shell.Add(Make(p,e));
            Floors(map,hospital);
            foreach(var e in entries.Where(e=>e.Def=="HospitalBed").Take(2))hospital.Furniture.Add(Make(p,e,"Bed"));
            foreach(var e in entries.Where(e=>e.Def=="WallLamp" || e.Def=="Heater"))hospital.Furniture.Add(Make(p,e));
            projects.Add(hospital);
            PrepareEntrances(map,projects,hospital);
            var shelf=entries.First(e=>e.Def=="Shelf");
            projects.Add(new RoomProject{Kind="Medicamentos",LayoutSlot="medicine:"+hospital.LayoutSlot,Origin=hospital.Origin,InteriorSize=1,
                RequiresRoof=false,StorageCells=GenAdj.OccupiedRect(origin+new IntVec3(shelf.X,0,shelf.Z),new Rot4(shelf.Rotation),DefDatabase<ThingDef>.GetNamed("Shelf").size).Cells.ToList()});
        }
        public static void AddResearch(Map map,List<RoomProject> projects,IntVec3 root)
        {
            var p=Reserve(map,projects,root,"Pesquisa",4);if(p==null)return;
            p.ReferenceLayout="research_two_bedrooms";var entries=ReferenceLayoutData.Get(p.ReferenceLayout);
            Shell(p,entries);Floors(map,p);
            foreach(var e in entries.Where(e=>!Structure(e.Def) && e.Def!="HiTechResearchBench" && e.Def!="Bookcase"))
                p.Furniture.Add(Make(p,e,e.Def=="DoubleBed"?"Bed":e.Def=="PlantPot_Bonsai"?"PlantPot":null));
            PrepareEntrances(map,projects,p);
        }
        public static void AddDining(Map map,List<RoomProject> projects,IntVec3 root)
        {
            var p=Reserve(map,projects,root,"Refeitório e recreação",4);if(p==null)return;
            p.ReferenceLayout="dining_recreation";var entries=ReferenceLayoutData.Get(p.ReferenceLayout);
            Shell(p,entries);Floors(map,p);
            foreach(var e in entries.Where(e=>e.Def=="Table2x4c" || e.Def=="Stool" && e.Z>=8 && e.X<=6 ||
                e.Def=="WallLamp" || e.Def=="Heater" || e.Def=="ChessTable" || e.Def=="HoopstoneRing" || e.Def=="PartySpot" || e.Def=="MarriageSpot"))p.Furniture.Add(Make(p,e));
            PrepareEntrances(map,projects,p);
        }
        private static void Upgrade(ConstructionTask task,string name)
        {
            var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if(def==null || !def.IsResearchFinished || task.CancelledByPlayer || task.UpgradeDeclined || task.Def==def || task.UpgradeTo!=null)return;
            if(task.Issued || task.WasCompleted){if(task.Owned)task.UpgradeTo=def;}
            else{task.Def=def;task.Stuff=def.MadeFromStuff?ThingDefOf.WoodLog:null;}
        }
        private static void AddUnlocked(Map map,RoomProject p,ReferenceLayoutData.Entry e,string kind=null)
        {
            var def=DefDatabase<ThingDef>.GetNamedSilentFail(e.Def);
            if(def==null || !def.IsResearchFinished)return;
            var task=Make(p,e);if(kind!=null)task.StorageKind=kind;
            if(p.Furniture.Any(t=>t.Position==task.Position && t.Def==task.Def))return;
            p.Furniture.Add(task);p.Completed=false;
        }
        public static bool FillEnergy(Map map,List<RoomProject> projects)
        {
            foreach(var p in projects.Where(p=>p.ReferenceLayout=="energy_bank"))
            foreach(var e in ReferenceLayoutData.Get("energy_hospital").Where(e=>e.Def=="WoodFiredGenerator"))
                if(!p.Furniture.Any(t=>t.Def.defName==e.Def && t.Position==p.Origin+new IntVec3(e.X,0,e.Z)))
                {p.Furniture.Add(Make(p,e));p.Completed=false;return true;}
            return false;
        }
        public static void Reconcile(Map map,List<RoomProject> projects)
        {
            foreach(var p in projects.Where(p=>p.ReferenceLayout!=null).ToList())
            {
                bool ready=p.Completed;
                if(p.ReferenceLayout=="workshop_storage_a" || p.ReferenceLayout=="workshop_storage_b")ReconcileStore(map,p);
                if(p.ReferenceLayout=="food-service")
                    foreach(var t in p.Furniture.Where(t=>t.Def.defName=="FueledStove"))Upgrade(t,"ElectricStove");
                if(p.ReferenceLayout=="energy_bank")
                {
                    // Storage buffer grows with generation, preserving all seven reserved positions.
                    int target=Math.Min(7,Math.Max(2,p.Furniture.Count(t=>t.Def.defName=="WoodFiredGenerator")+1));
                    foreach(var e in ReferenceLayoutData.Get("energy_hospital").Where(e=>e.Def=="Battery").Take(target))AddUnlocked(map,p,e);
                }
                if(p.ReferenceLayout=="research_two_bedrooms")
                {
                    foreach(var e in ReferenceLayoutData.Get(p.ReferenceLayout).Where(e=>e.Def=="HiTechResearchBench" || e.Def=="Bookcase"))AddUnlocked(map,p,e);
                    var old=p.Furniture.FirstOrDefault(t=>t.Position==p.Origin+new IntVec3(10,0,8) && t.Def.defName=="SimpleResearchBench");
                    if(old!=null)Upgrade(old,"MultiAnalyzer");
                    // Same research room evolves; no detached laboratory is required.
                    if(DefDatabase<ThingDef>.GetNamed("HiTechResearchBench").IsResearchFinished && !projects.Any(r=>r.LayoutSlot=="research-upgrade:"+p.LayoutSlot))
                        projects.Add(new RoomProject{Kind="Laboratório",ReferenceLayout="research-upgrade",LayoutSlot="research-upgrade:"+p.LayoutSlot,
                            Origin=p.Origin+new IntVec3(0,0,6),InteriorSize=11,InteriorHeight=5,RequiresRoof=false});
                }
                if(p.ReferenceLayout=="kitchen_butcher_freezer" && ready)
                {
                    int stacks=map.listerThings.AllThings.Count(t=>t.Spawned && StoragePolicy.IsFood(t.def));
                    int target=Math.Min(27,Math.Max(3,(stacks+3)/4));
                    foreach(var e in ReferenceLayoutData.Get(p.ReferenceLayout).Where(e=>e.Def=="Shelf").Take(Math.Min(target,p.Furniture.Count(t=>t.Def.defName=="Shelf")+2)))AddUnlocked(map,p,e,"Freezer");
                }
                if(p.ReferenceLayout=="hospital" && ready)
                {
                    var entries=ReferenceLayoutData.Get("energy_hospital");var root=p.Origin-new IntVec3(0,0,6);
                    foreach(var t in p.Furniture.Where(t=>t.MedicalBed && t.Def.defName=="Bed"))Upgrade(t,"HospitalBed");
                    // Use relative coordinates for the upper half, preserving bedside monitor geometry.
                    foreach(var e in entries.Where(e=>e.Def=="HospitalBed" || e.Def=="VitalsMonitor" || e.Def=="Shelf"))
                    {
                        var adjusted=e;adjusted.Z-=6;
                        if(e.Def=="HospitalBed" && p.Furniture.Any(t=>t.MedicalBed && t.Position==root+new IntVec3(e.X,0,e.Z)))continue;
                        if(e.Def=="VitalsMonitor" && !DefDatabase<ThingDef>.GetNamed("HospitalBed").IsResearchFinished)continue;
                        AddUnlocked(map,p,adjusted,"Medicamentos");
                    }
                }
                if(p.ReferenceLayout=="dining_recreation" && ready)
                    foreach(var e in ReferenceLayoutData.Get(p.ReferenceLayout).Where(e=>!Structure(e.Def)))AddUnlocked(map,p,e);
                if(p.Furniture.Any(t=>t.UpgradeTo!=null))p.Completed=false;
            }
        }
    }
}
