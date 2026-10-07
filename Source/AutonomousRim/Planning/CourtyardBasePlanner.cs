using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Execution;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Planning
{
    // Coordinates match docs/base-nucleo-patios.html. All dimensions include a
    // one-cell boundary; adjacent slots reuse the exact same boundary cells.
    public static class CourtyardBasePlanner
    {
        public const string ReservationKind = "Reserva do núcleo com pátios";
        public sealed class Slot
        {
            public string Id, Kind; public int X, Y, W, H; public char Door;
            public Slot(string id, string kind, int x, int y, int w, int h, char door)
            { Id=id; Kind=kind; X=x; Y=y; W=w; H=h; Door=door; }
        }
        public static readonly Slot[] Slots = {
            new Slot("stock","Estoque",23,11,26,11,'e'), new Slot("freezer","Freezer",52,11,25,10,'w'),
            new Slot("cook","Cozinha",52,20,6,6,'w'), new Slot("butcher","Abate",52,25,6,6,'w'),
            new Slot("dining","Refeitório",34,35,15,14,'s'), new Slot("rec","Sala social",52,37,15,12,'s'),
            new Slot("hospital","Hospital",34,52,15,12,'n'), new Slot("research","Pesquisa",52,52,15,12,'n'),
            new Slot("shop0","Oficina",10,31,11,18,'e'), new Slot("clothes","Roupas",10,52,11,17,'e'),
            new Slot("battery","Baterias",80,39,6,10,'s'), new Slot("weapons","Armas",23,77,26,12,'e'),
            new Slot("fabrication","Fabricação",52,77,25,12,'w'),
            new Slot("bed0","Quarto",41,21,8,8,'e'), new Slot("bed1","Quarto",41,28,8,8,'e'),
            new Slot("bed2","Quarto",20,41,8,8,'s'), new Slot("bed3","Quarto",27,41,8,8,'s'),
            new Slot("bed4","Quarto",66,41,8,8,'s'), new Slot("bed5","Quarto",73,41,8,8,'s'),
            new Slot("bed6","Quarto",20,52,8,8,'n'), new Slot("bed7","Quarto",27,52,8,8,'n'),
            new Slot("bed8","Quarto",66,52,8,8,'n'), new Slot("bed9","Quarto",73,52,8,8,'n'),
            new Slot("bed10","Quarto",41,63,8,8,'e'), new Slot("bed11","Quarto",41,70,8,8,'e'),
            new Slot("bed12","Quarto",52,63,8,8,'w'), new Slot("bed13","Quarto",52,70,8,8,'w')
        };
        private static readonly int[][] Outline = {
            new[]{6,27},new[]{10,27},new[]{10,19},new[]{19,19},new[]{19,10},new[]{27,10},new[]{27,6},
            new[]{73,6},new[]{73,10},new[]{81,10},new[]{81,19},new[]{90,19},new[]{90,27},new[]{94,27},
            new[]{94,73},new[]{90,73},new[]{90,81},new[]{81,81},new[]{81,90},new[]{73,90},new[]{73,94},
            new[]{27,94},new[]{27,90},new[]{19,90},new[]{19,81},new[]{10,81},new[]{10,73},new[]{6,73}
        };
        public static IntVec3 At(IntVec3 anchor,int x,int y) => anchor+new IntVec3(x,0,100-y);
        public static CellRect Rect(IntVec3 anchor,int x,int y,int w,int h) => new CellRect(anchor.x+x,At(anchor,0,y+h-1).z,w,h);
        private static ConstructionTask Task(string name,IntVec3 c,ThingDef stuff=null,Rot4? rotation=null)
        {
            var d=DefDatabase<ThingDef>.GetNamed(name);
            return new ConstructionTask{Def=d,Position=c,Stuff=d.MadeFromStuff?stuff??ThingDefOf.WoodLog:null,Rotation=rotation??Rot4.North};
        }
        public static List<RoomProject> Create(Map map,IntVec3 anchor,int bedrooms)
        {
            var result=new List<RoomProject>();
            var envelope=Rect(anchor,5,5,91,91);
            result.Add(new RoomProject{Kind=ReservationKind,LayoutSlot="courtyard",LayoutAnchor=anchor,Origin=envelope.Min,
                InteriorSize=envelope.Width-2,InteriorHeight=envelope.Height-2,RequiresRoof=false,Completed=true});
            int count=0;
            foreach(var slot in Slots)
            {
                if(slot.Kind=="Quarto" && count++>=Math.Min(14,Math.Max(1,bedrooms)))continue;
                var p=new RoomProject{Kind=slot.Kind,LayoutSlot=slot.Id,LayoutAnchor=anchor,Origin=Rect(anchor,slot.X,slot.Y,slot.W,slot.H).Min,
                    InteriorSize=slot.W-2,InteriorHeight=slot.H-2,ReserveDoubleBed=slot.Kind=="Quarto",
                    Priority=slot.Kind=="Estoque"||slot.Kind=="Cozinha"||slot.Kind=="Abate"||slot.Kind=="Refeitório"?ConstructionPriority.Critical:
                        slot.Kind=="Quarto"||slot.Kind=="Pesquisa"||slot.Kind=="Freezer"?ConstructionPriority.High:ConstructionPriority.Normal};
                int dx=slot.Door=='w'?slot.X:slot.Door=='e'?slot.X+slot.W-1:slot.X+slot.W/2;
                int dy=slot.Door=='n'?slot.Y:slot.Door=='s'?slot.Y+slot.H-1:slot.Y+slot.H/2;
                foreach(var c in p.Footprint.EdgeCells)
                    p.Shell.Add(Task(c==At(anchor,dx,dy)?"Door":"Wall",c,rotation:(slot.Door=='e'||slot.Door=='w')&&c==At(anchor,dx,dy)?Rot4.East:Rot4.North));
                result.Add(p);
            }
            var shared=new Dictionary<IntVec3,ConstructionTask>();
            foreach(var p in result)foreach(var t in p.Shell)
                if(!shared.TryGetValue(t.Position,out var old)||t.Def==ThingDefOf.Door)shared[t.Position]=t;
            foreach(var p in result)for(int i=0;i<p.Shell.Count;i++)p.Shell[i]=shared[p.Shell[i].Position];
            if(result.Where(p=>p.RequiresRoof).SelectMany(p=>p.Interior).Any(shared.ContainsKey))throw new InvalidOperationException("Courtyard wall/interior collision");

            // Reserve freezer exhaust by using the exterior north wall. Cooler
            // blueprints obey real research; no research is completed here.
            var freezer=result.First(p=>p.Kind=="Freezer");
            foreach(int x in new[]{62,69})
            {
                var wall=shared[At(anchor,x,11)];wall.Def=DefDatabase<ThingDef>.GetNamed("Cooler");wall.Stuff=null;
                wall.Rotation=Rot4.North;wall.TargetTemperature=-2;
            }
            // Separate two-door antecâmara inside the west end of the freezer.
            int lockY=16;
            foreach(int y in Enumerable.Range(12,8))
                freezer.Shell.Add(Task(y==lockY?"Door":"Wall",At(anchor,55,y),rotation:y==lockY?Rot4.East:Rot4.North));
            freezer.StorageCells=freezer.Interior.Where(c=>c.x>=anchor.x+56).ToList();
            var allBlocked=new HashSet<IntVec3>(result.SelectMany(p=>p.Shell).Select(t=>t.Position));
            var corridorSpace=Rect(anchor,49,11,3,78).Cells.Concat(Rect(anchor,10,49,80,3)).ToHashSet();
            foreach(var p in result.Where(p=>p.RequiresRoof&&p.Kind!="Freezer"))
            {
                var door=p.Shell.First(t=>t.Def==ThingDefOf.Door);
                var vent=p.Shell.FirstOrDefault(t=>t.Def==ThingDefOf.Wall&&t.Position.DistanceToSquared(door.Position)==1&&
                    GenAdj.CellsAdjacentCardinal(t.Position,Rot4.North,IntVec2.One).Any(c=>corridorSpace.Contains(c)));
                if(vent!=null){vent.Def=DefDatabase<ThingDef>.GetNamed("Vent");vent.Stuff=null;}
            }
            foreach(int x in new[]{62,69})freezer.NoRoofCells.Add(At(anchor,x,10));
            var furniture=new Dictionary<IntVec3,ConstructionTask>();
            bool place(RoomProject p,string name,IntVec3 preferred,ThingDef stuff=null,Rot4? rotation=null,bool allowBedSpace=false)
            {
                var t=Task(name,preferred,stuff,rotation);
                foreach(var cell in p.Interior.OrderBy(c=>c.DistanceToSquared(preferred)))
                {
                    var footprint=GenAdj.OccupiedRect(cell,t.Rotation,t.Def.Size);
                    var def=(ThingDef)t.Def;
                    var interaction=cell+def.interactionCellOffset.RotatedBy(t.Rotation);
                    if(footprint.Any(c=>!p.Interior.Contains(c)||allBlocked.Contains(c)||furniture.ContainsKey(c)||
                        p.StorageCells.Contains(c)&&p.Kind=="Medicamentos"||!allowBedSpace&&p.ReserveDoubleBed&&p.DoubleBedSpace.Contains(c)))continue;
                    if(def.hasInteractionCell&&(!p.Interior.Contains(interaction)||allBlocked.Contains(interaction)||
                        furniture.TryGetValue(interaction,out var blocker)&&((ThingDef)blocker.Def).building?.isSittable!=true))continue;
                    if(furniture.Values.Distinct().Any(o=>((ThingDef)o.Def).hasInteractionCell&&footprint.Contains(o.Position+((ThingDef)o.Def).interactionCellOffset.RotatedBy(o.Rotation))))continue;
                    t.Position=cell;p.Furniture.Add(t);foreach(var c in footprint)furniture[c]=t;return true;
                }
                Log.Warning("[AutonomousRim] Courtyard furniture space: "+p.Kind+"/"+name);return false;
            }
            foreach(var p in result.Where(p=>p.RequiresRoof).ToList())
            {
                IntVec3 local(int x,int z)=>p.Origin+new IntVec3(x,0,z);
                if(p.Kind=="Quarto")
                {
                    place(p,"Bed",local(4,p.Height),rotation:Rot4.South,allowBedSpace:true);
                    place(p,"EndTable",local(3,p.Height));place(p,"Dresser",local(1,p.Height));place(p,"PlantPot",local(1,1));
                }
                if(p.Kind=="Cozinha")place(p,"FueledStove",local(2,3),ThingDefOf.Steel);
                if(p.Kind=="Abate")place(p,"TableButcher",local(2,3));
                if(p.Kind=="Refeitório"||p.Kind=="Sala social")
                {
                    place(p,"Table2x2c",local(4,4));
                    foreach(var c in new[]{local(3,4),local(3,5),local(6,4),local(6,5),local(4,3)})place(p,"Stool",c);
                    if(p.Kind=="Sala social"){place(p,"ChessTable",local(9,7));place(p,"Stool",local(9,6));place(p,"HorseshoesPin",local(2,8));}
                    place(p,"PlantPot",local(1,1));
                }
                if(p.Kind=="Hospital")
                {
                    foreach(int x in new[]{2,5,8})
                    {place(p,"Bed",local(x,p.Height),rotation:Rot4.South);p.Furniture.Last().MedicalBed=true;}
                    var meds=new RoomProject{Kind="Medicamentos",LayoutSlot="med",Origin=p.Origin,InteriorSize=1,RequiresRoof=false,Priority=ConstructionPriority.High};
                    foreach(int x in new[]{2,3,4})meds.StorageCells.Add(local(x,1));result.Add(meds);
                }
                if(p.Kind=="Pesquisa")place(p,"SimpleResearchBench",local(3,3));
                if(p.Kind=="Oficina"){place(p,"TableStonecutter",local(3,3));place(p,"HandTailoringBench",local(3,9));}
                if(p.Kind=="Fabricação")place(p,"HandTailoringBench",local(3,3));
                if(p.Kind=="Baterias")foreach(int z in new[]{2,5})place(p,"Battery",local(2,z));
                if(p.Kind=="Estoque"||p.Kind=="Freezer"||p.Kind=="Roupas"||p.Kind=="Armas")
                    for(int x=2;x<p.InteriorSize-1;x+=3)
                    {if(place(p,"ShelfSmall",local(x,p.Height-1)))p.Furniture.Last().StorageKind=p.Kind;}
            }
            var hallCells=Rect(anchor,49,11,3,78).Cells.Concat(Rect(anchor,10,49,80,3)).Where(c=>!allBlocked.Contains(c)).Distinct().ToList();
            var hall=new RoomProject{Kind="Corredor",LayoutSlot="hall",Origin=anchor,InteriorSize=1,RoofCells=hallCells,Priority=ConstructionPriority.High};
            // Roofed corridors are bounded by the rooms and these outer doors.
            foreach(var c in new[]{At(anchor,49,11),At(anchor,50,11),At(anchor,51,11),At(anchor,49,88),At(anchor,50,88),At(anchor,51,88),
                At(anchor,10,49),At(anchor,10,50),At(anchor,10,51),At(anchor,89,49),At(anchor,89,50),At(anchor,89,51)})
                hall.Shell.Add(Task("Door",c,rotation:c.x==anchor.x+10||c.x==anchor.x+89?Rot4.East:Rot4.North));
            result.Add(hall);
            var power=new RoomProject{Kind="Energia e climatização",LayoutSlot="courtyard-power",Origin=anchor,InteriorSize=1,RequiresRoof=false,Priority=ConstructionPriority.High};
            foreach(var c in new[]{At(anchor,15,29),At(anchor,84,34)})power.Furniture.Add(Task("WoodFiredGenerator",c));
            foreach(var c in new[]{At(anchor,17,24),At(anchor,83,24),At(anchor,17,75),At(anchor,83,75)})power.Furniture.Add(Task("SolarGenerator",c));
            foreach(var t in power.Furniture)power.NoRoofCells.AddRange(GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size));
            var cables=new HashSet<IntVec3>();
            void connect(IntVec3 c)
            {
                var trunk=At(anchor,50,50);
                for(int x=Math.Min(c.x,trunk.x);x<=Math.Max(c.x,trunk.x);x++)cables.Add(new IntVec3(x,0,trunk.z));
                for(int z=Math.Min(c.z,trunk.z);z<=Math.Max(c.z,trunk.z);z++)cables.Add(new IntVec3(c.x,0,z));
            }
            foreach(var p in result.Where(p=>p.RequiresRoof && p.Kind!="Corredor").ToList())
            {
                if(place(p,"StandingLamp",p.Interior.CenterCell))connect(p.Furniture.Last().Position);
                foreach(var t in p.Shell.Concat(p.Furniture).Where(t=>((ThingDef)t.Def).GetCompProperties<CompProperties_Power>()!=null))connect(t.Position);
            }
            foreach(var c in new[]{At(anchor,50,25),At(anchor,50,74)})
            {var t=Task("Heater",c);t.TargetTemperature=20;power.Furniture.Add(t);connect(c);}
            foreach(var t in power.Furniture)connect(t.Position);
            var generators=power.Furniture.Where(t=>t.Def.defName=="WoodFiredGenerator"||t.Def.defName=="SolarGenerator").Select(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size)).ToList();
            foreach(var c in cables.Where(c=>!generators.Any(r=>r.Contains(c))))power.Furniture.Add(Task("PowerConduit",c));
            result.Add(power);

            var cropRects=new[]{new[]{21,22,19,16},new[]{59,21,21,16},new[]{21,60,19,17},new[]{60,60,20,17}};
            var crops=new[]{"Plant_Rice","Plant_Cotton","Plant_Potato","Plant_Healroot"};
            var names=new[]{"rice","textile","potato","medicine"};
            var occupiedSlots=Slots.Select(s=>Rect(anchor,s.X,s.Y,s.W,s.H)).ToList();
            var cropCells=new HashSet<IntVec3>();
            for(int i=0;i<4;i++)
            {
                var r=cropRects[i];var cells=Rect(anchor,r[0],r[1],r[2],r[3]).Where(c=>!occupiedSlots.Any(s=>s.Contains(c))).ToList();
                var plant=DefDatabase<ThingDef>.GetNamed(crops[i]);
                if(i==1)plant=DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(d=>d.plant?.Sowable==true&&d.defName.IndexOf("flax",StringComparison.OrdinalIgnoreCase)>=0)??plant;
                if(i==3)
                {
                    var hemp=cells.Where(c=>c.x>=anchor.x+77).ToList();cells=cells.Except(hemp).ToList();
                    var hempDef=DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(d=>d.plant?.Sowable==true&&d.defName.IndexOf("hemp",StringComparison.OrdinalIgnoreCase)>=0)??DefDatabase<ThingDef>.GetNamed("Plant_Smokeleaf");
                    result.Add(Crop("hemp",hempDef,hemp));cropCells.UnionWith(hemp);
                }
                result.Add(Crop(names[i],plant,cells));cropCells.UnionWith(cells);
            }
            var dump=new RoomProject{Kind="Despejo",LayoutSlot="dump",Origin=At(anchor,82,64),InteriorSize=1,RequiresRoof=false,Priority=ConstructionPriority.High,
                StorageCells=Rect(anchor,82,55,6,11).Cells.ToList()};result.Add(dump);
            var floor=new RoomProject{Kind="Pisos e acabamento",LayoutSlot="finish",Origin=anchor,InteriorSize=1,RequiresRoof=false,Priority=ConstructionPriority.Low};
            var woodCells=result.Where(p=>p.RequiresRoof).SelectMany(p=>p.RoofArea).Where(c=>!allBlocked.Contains(c)&&!hall.Shell.Any(t=>t.Position==c)).Distinct().ToHashSet();
            foreach(var c in woodCells)floor.Furniture.Add(new ConstructionTask{Def=DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor"),Position=c,OriginalTerrain=c.GetTerrain(map)});
            // Open paths occupy only the useful lanes, not every spare outdoor tile.
            var openPaths=Rect(anchor,8,48,84,5).Cells.Concat(Rect(anchor,48,7,5,86)).Concat(Rect(anchor,20,37,14,4))
                .Concat(Rect(anchor,67,37,14,4)).Concat(Rect(anchor,20,59,14,4)).Concat(Rect(anchor,67,59,14,4));
            foreach(var c in openPaths.Distinct().Where(c=>!woodCells.Contains(c)&&!allBlocked.Contains(c)&&!hall.Shell.Any(t=>t.Position==c)&&!cropCells.Contains(c)&&!occupiedSlots.Any(r=>r.Contains(c))))
                floor.Furniture.Add(new ConstructionTask{Def=DefDatabase<TerrainDef>.GetNamed("Concrete"),Position=c,OriginalTerrain=c.GetTerrain(map)});
            result.Add(floor);
            var perimeter=new RoomProject{Kind="Muro externo",LayoutSlot="wall",Origin=anchor,InteriorSize=1,RequiresRoof=false,Priority=ConstructionPriority.Low};
            var boundary=new HashSet<IntVec3>();
            for(int i=0;i<Outline.Length;i++)
            {var a=Outline[i];var b=Outline[(i+1)%Outline.Length];for(int x=Math.Min(a[0],b[0]);x<=Math.Max(a[0],b[0]);x++)for(int y=Math.Min(a[1],b[1]);y<=Math.Max(a[1],b[1]);y++)boundary.Add(At(anchor,x,y));}
            foreach(var c in boundary)
            {
                bool door=c.x>=anchor.x+49&&c.x<=anchor.x+51&&(c.z==At(anchor,0,6).z||c.z==At(anchor,0,94).z)||
                    c.z>=At(anchor,0,51).z&&c.z<=At(anchor,0,49).z&&(c.x==anchor.x+6||c.x==anchor.x+94);
                perimeter.Shell.Add(Task(door?"Door":"Wall",c,rotation:door&&(c.x==anchor.x+6||c.x==anchor.x+94)?Rot4.East:Rot4.North));
            }
            result.Add(perimeter);
            var support=new RoomProject{Kind="Apoio inicial",LayoutSlot="support",Origin=anchor,InteriorSize=1,RequiresRoof=false,Priority=ConstructionPriority.Critical};
            support.Furniture.AddRange(result.Where(p=>p.Kind=="Cozinha"||p.Kind=="Abate"||p.Kind=="Refeitório"||p.Kind=="Quarto").SelectMany(p=>p.Furniture).Where(t=>t.Def==ThingDefOf.Bed||t.Def.defName=="FueledStove"||t.Def.defName=="TableButcher"||t.Def.defName=="Table2x2c"||t.Def==ThingDefOf.Stool));
            result.Insert(1,support);
            var preparation=new RoomProject{Kind="Preparação do terreno",LayoutSlot="prepare",Origin=anchor,InteriorSize=1,RequiresRoof=false,Priority=ConstructionPriority.Critical};
            var buildCells=result.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.Def is ThingDef).SelectMany(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size)).Concat(woodCells)
                .Concat(result.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.Def is ThingDef d&&d.hasInteractionCell).Select(t=>t.Position+((ThingDef)t.Def).interactionCellOffset.RotatedBy(t.Rotation))).Distinct().ToList();
            preparation.PlantCells=buildCells.Where(c=>c.GetThingList(map).OfType<Plant>().Any()).ToList();
            preparation.MineCells=result.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.Def is ThingDef).SelectMany(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size))
                .Concat(woodCells).Concat(floor.Furniture.Select(t=>t.Position)).Concat(cropCells).Distinct().Where(c=>c.GetEdifice(map) is Mineable).ToList();
            preparation.ClearCells=result.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.Def is ThingDef).SelectMany(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size))
                .Concat(woodCells).Concat(floor.Furniture.Select(t=>t.Position)).Concat(cropCells).Distinct()
                .Where(c=>c.GetEdifice(map) is Building b && !(b is Mineable)&&b.Faction==null&&b.DeconstructibleBy(Faction.OfPlayer)).ToList();
            if(preparation.MineCells.Count>0||preparation.ClearCells.Count>0||preparation.PlantCells.Count>0)result.Insert(1,preparation);
            return result.OrderBy(p=>p.Priority).ThenBy(p=>RingBasePlanner.Rank(p.Kind)).ToList();
        }
        private static RoomProject Crop(string id,ThingDef def,List<IntVec3> cells)=>new RoomProject{Kind=id=="rice"?"Plantação inicial":"Plantação",LayoutSlot=id,Crop=def,
            Origin=cells.First(),InteriorSize=1,RequiresRoof=false,Priority=ConstructionPriority.High,StorageCells=cells};

        private static string siteFailure;
        private static bool Site(Map map,IntVec3 anchor)
        {
            var rect=Rect(anchor,5,5,91,91);
            if(!rect.InBounds(map)||rect.Cells.Any(c=>c.CloseToEdge(map,10))){siteFailure="edge";return false;}
            // Firm support is required where structures actually stand, not in
            // every garden or unused corner of the enclosing rectangle.
            var wallCells=Slots.SelectMany(s=>Rect(anchor,s.X,s.Y,s.W,s.H).EdgeCells).ToHashSet();
            var usedCells=Slots.SelectMany(s=>Rect(anchor,s.X,s.Y,s.W,s.H).Cells).Concat(Rect(anchor,49,11,3,78)).Concat(Rect(anchor,10,49,80,3)).Distinct();
            foreach(var c in usedCells)
            {
                if(c.Fogged(map)&&!(c.GetEdifice(map) is Mineable)){siteFailure="fog";return false;}
                if(wallCells.Contains(c)&&!c.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy)){siteFailure="terrain:"+c.GetTerrain(map).defName;return false;}
                if(!c.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Light)){siteFailure="floor:"+c.GetTerrain(map).defName;return false;}
                if(c.GetZone(map)!=null||map.areaManager.NoRoof[c]){siteFailure="zone";return false;}
                bool obstacle(Thing t)=>!(t is Pawn)&&!(t is Plant)&&!(t is Filth)&&!(t is Mote)&&!(t is Mineable&&t.Faction==null)&&
                    !(t is Building b&&b.Faction==null&&b.DeconstructibleBy(Faction.OfPlayer))&&!(t.def.category==ThingCategory.Item&&t.def.EverHaulable);
                if(c.GetThingList(map).Any(obstacle)){siteFailure="obstacle:"+c.GetThingList(map).First(obstacle).def.defName;return false;}
            }
            bool reachable=map.mapPawns.FreeColonistsSpawned.Any(p=>p.CanReach(At(anchor,50,50),PathEndMode.Touch,Danger.None));siteFailure="access";return reachable;
        }
        public static string Plan(Map map,List<RoomProject> projects)
        {
            var reservation=projects.FirstOrDefault(p=>p.Kind==ReservationKind);
            if(reservation!=null)
            {
                int beds=Math.Max(map.mapPawns.FreeColonistsSpawnedCount,projects.Count(p=>p.Kind=="Quarto"));
                var next=Create(map,reservation.LayoutAnchor,beds);
                var existing=projects.SelectMany(BaseConstructionManager.Tasks).GroupBy(t=>new{t.Def,t.Position,t.Rotation,t.Stuff}).ToDictionary(g=>g.Key,g=>g.First());
                foreach(var p in next.Where(p=>!projects.Any(o=>o.LayoutSlot==p.LayoutSlot)))
                {
                    foreach(var list in new[]{p.Shell,p.Furniture})for(int i=0;i<list.Count;i++)
                    {var t=list[i];if(existing.TryGetValue(new{t.Def,t.Position,t.Rotation,t.Stuff},out var saved))list[i]=saved;}
                    if(!RingBasePlanner.Validate(map,projects.Concat(new[]{p}).ToList()))continue;
                    projects.Add(p);
                }
                return "Núcleo com quatro pátios salvo: paredes únicas, quartos 6×6, cozinha/abate 4×4, baterias 4×8; obra nativa em etapas.";
            }
            if(projects.Count>0)return "Plano existente preservado.";
            var pawn=map.mapPawns.FreeColonistsSpawned.FirstOrDefault();if(pawn==null)return "Sem colonos.";
            var candidates=new List<IntVec3>();
            var rejected=new Dictionary<string,int>();
            for(int x=10;x+100<map.Size.x-10;x+=4)for(int z=10;z+100<map.Size.z-10;z+=4)candidates.Add(new IntVec3(x,0,z));
            foreach(var anchor in candidates.OrderBy(c=>At(c,50,50).DistanceToSquared(pawn.Position)))
            {
                if(!Site(map,anchor)){rejected.TryGetValue(siteFailure,out int n);rejected[siteFailure]=n+1;continue;}
                var plan=Create(map,anchor,map.mapPawns.FreeColonistsSpawnedCount);
                if(!RingBasePlanner.Validate(map,plan))continue;
                projects.AddRange(plan);return "Núcleo com quatro pátios: solo firme; árvores e rochas removidas por trabalho normal. Estoque, alimentação e quartos primeiro.";
            }
            return "Procurando área de solo firme para núcleo de 89×89. Vegetação e rochas mineráveis são aceitas; estruturas existentes são preservadas. Rejeições: "+string.Join(", ",rejected.Select(p=>p.Key+"="+p.Value));
        }
    }
}
