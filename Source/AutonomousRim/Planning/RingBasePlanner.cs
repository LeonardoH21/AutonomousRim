using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Planning
{
    // Coordinates follow the approved drawing: X to the right, Y downwards.
    // One reserved envelope keeps later rooms, crops, exhaust and power out of each other's way.
    public static class RingBasePlanner
    {
        public const string ReservationKind = "Reserva da base em anel";
        private sealed class Slot
        {
            public string Id, Kind; public int X, Y, W, H; public int[][] Doors;
            public Slot(string id, string kind, int x, int y, int w, int h, params int[][] doors)
            { Id = id; Kind = kind; X = x; Y = y; W = w; H = h; Doors = doors; }
        }
        private static readonly Slot[] Slots = {
            new Slot("bed0","Quarto",14,0,7,7,new[]{17,6}), new Slot("bed1","Quarto",20,0,7,7,new[]{23,6}),
            new Slot("bed2","Quarto",26,0,7,7,new[]{29,6}), new Slot("shop0","Oficina",32,0,14,7,new[]{36,6}),
            new Slot("med","Medicamentos",8,6,7,7,new[]{11,12},new[]{14,8}),
            new Slot("hospital","Hospital",8,12,7,11,new[]{11,22}), new Slot("battery","Baterias",8,25,7,9,new[]{11,25}),
            new Slot("clothes","Roupas",18,9,14,6,new[]{25,14}),
            new Slot("stock","Estoque",18,14,14,13,new[]{18,24},new[]{31,24},new[]{25,26}),
            new Slot("weapons","Armas",18,26,14,8,new[]{25,26}),
            new Slot("cook","Cozinha",35,6,6,6,new[]{38,11}), new Slot("butcher","Abate",40,6,6,6,new[]{43,11}),
            new Slot("dining","Refeitório",35,11,11,12,new[]{39,22}), new Slot("rec","Sala social",45,6,11,17,new[]{50,22}),
            new Slot("research","Pesquisa",55,10,8,13,new[]{55,17}), new Slot("fabrication","Fabricação",55,25,8,8,new[]{55,28}),
            new Slot("freezer","Freezer",35,25,11,12,new[]{39,25},new[]{39,36}),
            new Slot("multi","Multiuso",47,25,9,12,new[]{50,25}),
            new Slot("bed3","Quarto",14,36,7,7,new[]{17,36}), new Slot("bed4","Quarto",20,36,7,7,new[]{23,36}),
            new Slot("bed5","Quarto",26,36,7,7,new[]{29,36}), new Slot("shop1","Oficina",32,36,14,7,new[]{36,36}),
            // A second bedroom row is reserved for population growth beyond six.
            new Slot("bed6","Quarto",14,-6,7,7,new[]{17,0}), new Slot("bed7","Quarto",20,-6,7,7,new[]{23,0}),
            new Slot("bed8","Quarto",26,-6,7,7,new[]{29,0}),
            new Slot("bed9","Quarto",14,42,7,7,new[]{17,42}), new Slot("bed10","Quarto",20,42,7,7,new[]{23,42}),
            new Slot("bed11","Quarto",26,42,7,7,new[]{29,42})
        };
        public static IntVec3 At(IntVec3 anchor, int x, int y) => anchor + new IntVec3(x, 0, 42 - y);
        private static bool Researched(string name) => DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name)?.IsFinished == true;
        public static int Rank(string kind) => kind == "Estoque" ? 0 : kind == "Cozinha" || kind == "Abate" || kind == "Refeitório" ? 1 :
            kind == "Apoio inicial" ? 2 : kind == "Quarto" ? 3 : kind == "Freezer" || kind == "Energia e climatização" ? 4 : 5;
        private static ConstructionPriority Priority(string kind) => Rank(kind) <= 2 ? ConstructionPriority.Critical :
            Rank(kind) <= 4 ? ConstructionPriority.High : kind == "Oficina" || kind == "Hospital" || kind == "Medicamentos" || kind == "Sala social" ? ConstructionPriority.Normal : ConstructionPriority.Low;
        private static ConstructionTask Task(string name, IntVec3 pos, ThingDef stuff = null, Rot4? rot = null)
        {
            var def = DefDatabase<ThingDef>.GetNamed(name);
            return new ConstructionTask { Def = def, Position = pos, Stuff = def.MadeFromStuff ? stuff ?? ThingDefOf.WoodLog : null, Rotation = rot ?? Rot4.North };
        }
        private static RoomProject Room(IntVec3 anchor, Slot slot, bool climate)
        {
            var p = new RoomProject { Kind = slot.Kind, LayoutSlot = slot.Id, Origin = At(anchor, slot.X, slot.Y + slot.H - 1),
                InteriorSize = slot.W - 2, InteriorHeight = slot.H - 2, Priority = Priority(slot.Kind), ReserveDoubleBed = slot.Kind == "Quarto" };
            var doors = new HashSet<IntVec3>(slot.Doors.Select(d => At(anchor, d[0], d[1])));
            foreach (var c in p.Footprint.EdgeCells)
                p.Shell.Add(Task(doors.Contains(c) ? "Door" : "Wall", c, rot: doors.Contains(c) && (c.x == p.Footprint.minX || c.x == p.Footprint.maxX) ? Rot4.East : Rot4.North));
            IntVec3 local(int x, int z) => p.Origin + new IntVec3(x, 0, z);
            if (p.Kind == "Quarto") p.Furniture.Add(Task("Bed", local(4, 5), rot: Rot4.South));
            if (p.Kind == "Cozinha") { p.Furniture.Add(Task("FueledStove", local(2, 3), ThingDefOf.Steel)); p.Furniture.Add(Task("Stool", local(2, 2))); }
            if (p.Kind == "Abate") p.Furniture.Add(Task("TableButcher", local(2, 3)));
            if (p.Kind == "Refeitório" || p.Kind == "Sala social")
            {
                p.Furniture.Add(Task("Table2x2c", local(2, 2)));
                foreach (int z in new[] {2,3}) p.Furniture.Add(Task("Stool", local(1, z), rot: Rot4.East));
                if (p.Kind == "Sala social") { p.Furniture.Add(Task("ChessTable", local(5, 5))); p.Furniture.Add(Task("Stool", local(5, 4))); }
            }
            if (p.Kind == "Hospital") foreach (int x in new[] {1,4}) { var t = Task("Bed", local(x, p.Height), rot: Rot4.South); t.MedicalBed = true; p.Furniture.Add(t); }
            if (p.Kind == "Medicamentos") for (int x = 2; x <= 4; x++) p.StorageCells.Add(local(x, 1));
            if (p.Kind == "Oficina")
            {
                if (Researched("Stonecutting")) p.Furniture.Add(Task("TableStonecutter", local(3, 1), rot: Rot4.South));
                if (Researched("ComplexClothing")) p.Furniture.Add(Task("HandTailoringBench", local(3, 5)));
            }
            if (p.Kind == "Pesquisa") p.Furniture.Add(Task("SimpleResearchBench", local(3, 3)));
            if (p.Kind == "Baterias" && DefDatabase<ThingDef>.GetNamed("Battery").IsResearchFinished) p.Furniture.Add(Task("Battery", local(2, 3)));
            if (p.Kind == "Freezer" && climate)
                foreach (var t in p.Shell.Where(t => t.Position.x == p.Footprint.maxX && (t.Position.z == p.Origin.z + 3 || t.Position.z == p.Origin.z + 4)))
                { t.Def = DefDatabase<ThingDef>.GetNamed("Cooler"); t.Stuff = null; t.Rotation = Rot4.East; t.TargetTemperature = -2; }
            // A separate north/south wall shared with another room must not vent food into the freezer.
            if (climate && p.Kind != "Freezer")
            {
                var door = p.Shell.First(t => t.Def == ThingDefOf.Door);
                var candidate = p.Shell.FirstOrDefault(t => t.Def == ThingDefOf.Wall && t.Position.DistanceToSquared(door.Position) == 1 &&
                    (t.Position.x == door.Position.x || t.Position.z == door.Position.z) &&
                    t.Position.x != p.Footprint.minX && t.Position.x != p.Footprint.maxX);
                if (candidate != null) { candidate.Def = DefDatabase<ThingDef>.GetNamed("Vent"); candidate.Stuff = null; }
            }
            return p;
        }
        public static CellRect Envelope(IntVec3 anchor) => new CellRect(anchor.x - 4, anchor.z - 11, 84, 64);
        private static bool Site(Map map, IntVec3 anchor)
        {
            foreach (var c in Envelope(anchor).ExpandedBy(1))
            {
                if (!c.InBounds(map) || c.CloseToEdge(map, 10) || c.Fogged(map) || c.GetZone(map) != null || c.Roofed(map) || map.areaManager.NoRoof[c]) return false;
                if (!c.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy)) return false;
                if (c.GetThingList(map).Any(t => !(t is Pawn) && !(t is Plant) && !(t is Filth) &&
                    !(t.def.category == ThingCategory.Item && t.def.EverHaulable && !t.IsForbidden(Faction.OfPlayer)) &&
                    !(t is Mineable && t.Faction == null))) return false;
            }
            return map.mapPawns.FreeColonistsSpawned.Any(p => p.CanReach(At(anchor, 14, 24), PathEndMode.Touch, Danger.None));
        }
        public static List<RoomProject> Create(Map map, IntVec3 anchor, int bedrooms, bool validate = true)
        {
            bool climate = Researched("Electricity") && Researched("AirConditioning") && Researched("ComplexFurniture");
            var envelope = Envelope(anchor);
            var reservation = new RoomProject { Kind = ReservationKind, Origin = envelope.Min, InteriorSize = envelope.Width - 2,
                InteriorHeight = envelope.Height - 2, RequiresRoof = false, LayoutAnchor = anchor, Completed = true };
            var result = new List<RoomProject> {reservation};
            int bed = 0;
            foreach (var slot in Slots)
            {
                if (slot.Kind == "Quarto" && bed++ >= Math.Min(bedrooms, 12)) continue;
                if (slot.Kind == "Freezer" && !climate) continue;
                if (slot.Kind == "Baterias" && !Researched("Electricity")) continue;
                result.Add(Room(anchor, slot, climate));
            }
            // Normalize shared walls to one definition before creating corridor aliases.
            var shared = new Dictionary<IntVec3, ConstructionTask>();
            foreach (var p in result) foreach (var t in p.Shell)
            {
                if (!shared.TryGetValue(t.Position, out var prior) || prior.Def == ThingDefOf.Wall && t.Def != ThingDefOf.Wall) shared[t.Position] = t;
            }
            foreach (var p in result) for (int i = 0; i < p.Shell.Count; i++) p.Shell[i] = shared[p.Shell[i].Position];
            var entrance = new RoomProject { Kind = "Corredor de entrada", LayoutSlot = "entry", Origin = At(anchor,0,25), InteriorSize = 13, InteriorHeight = 2, Priority = ConstructionPriority.High };
            foreach(var c in entrance.Footprint.EdgeCells)
            {
                if(shared.TryGetValue(c,out var t))entrance.Shell.Add(t);
                else
                {
                    bool door=(c.x==anchor.x || c.x==anchor.x+14) && (c.z==At(anchor,0,23).z || c.z==At(anchor,0,24).z);
                    entrance.Shell.Add(Task(door ? "Door" : "Wall",c,rot:door ? Rot4.East : Rot4.North));
                }
            }
            foreach(var t in entrance.Shell)shared[t.Position]=t;
            result.Add(entrance);
            var corridor = new RoomProject { Kind = "Corredor", LayoutSlot = "hall", Origin = At(anchor, 14, 36), InteriorSize = 40, InteriorHeight = 29 };
            foreach (var c in corridor.Footprint.EdgeCells)
            {
                if (shared.TryGetValue(c, out var t)) corridor.Shell.Add(t);
                else
                {
                    bool door = (c.x == anchor.x + 14 || c.x == anchor.x + 55) && (c.z == At(anchor, 0, 23).z || c.z == At(anchor, 0, 24).z) ||
                        Slots.Where(s => s.Kind == "Quarto" && s.Y >= 0 && s.Y <= 36).SelectMany(s => s.Doors).Any(d => At(anchor,d[0],d[1]) == c);
                    corridor.Shell.Add(Task(door ? "Door" : "Wall", c, rot: door ? Rot4.East : Rot4.North));
                }
            }
            corridor.RoofCells = corridor.Interior.Where(c => !(c.x == anchor.x + 46 && c.z <= At(anchor,0,25).z) &&
                !Slots.Any(s => new CellRect(anchor.x+s.X, At(anchor,0,s.Y+s.H-1).z,s.W,s.H).Contains(c))).ToList();
            result.Add(corridor);
            AddPower(map, anchor, result, climate);
            var comfort = new RoomProject { Kind = "Conforto dos quartos", LayoutSlot = "comfort", Origin = anchor, InteriorSize = 1, RequiresRoof = false, Priority = ConstructionPriority.Low };
            if (Researched("ComplexFurniture")) foreach (var p in result.Where(p => p.Kind == "Quarto"))
            {
                comfort.Furniture.Add(Task("EndTable", p.Origin + new IntVec3(3,0,5)));
                comfort.Furniture.Add(Task("Dresser", p.Origin + new IntVec3(1,0,5)));
                comfort.Furniture.Add(Task("PlantPot", p.Origin + new IntVec3(1,0,1)));
            }
            if (comfort.Furniture.Count > 0) result.Add(comfort);
            var shelves = new RoomProject { Kind = "Prateleiras", LayoutSlot = "shelves", Origin = anchor, InteriorSize = 1, RequiresRoof = false, Priority = ConstructionPriority.Low };
            if (Researched("ComplexFurniture")) foreach (var p in result.Where(p => p.Kind == "Estoque" || p.Kind == "Freezer" || p.Kind == "Armas" || p.Kind == "Roupas"))
                for (int x = 1; x <= p.InteriorSize; x += 2)
                { var t = Task("ShelfSmall", p.Origin + new IntVec3(x,0,p.Height-1)); t.StorageKind = p.Kind; shelves.Furniture.Add(t); }
            if (shelves.Furniture.Count > 0) result.Add(shelves);
            var finish = new RoomProject { Kind = "Pisos e acabamento", LayoutSlot = "finish", Origin = anchor, InteriorSize = 1, RequiresRoof = false, Priority = ConstructionPriority.Low };
            foreach (var c in result.Where(p => p.RequiresRoof).SelectMany(p => p.RoofArea).Distinct())
                finish.Furniture.Add(new ConstructionTask { Def = DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor"), Position = c, OriginalTerrain = c.GetTerrain(map) });
            result.Add(finish);
            var perimeter = new RoomProject { Kind = "Muro externo", LayoutSlot = "wall", Origin = envelope.Min, InteriorSize = envelope.Width-2, InteriorHeight = envelope.Height-2,
                RequiresRoof = false, Priority = ConstructionPriority.Low };
            foreach (var c in envelope.EdgeCells)
            {
                bool door = (c.x == envelope.minX || c.x == envelope.maxX) && (c.z == At(anchor,0,23).z || c.z == At(anchor,0,24).z);
                perimeter.Shell.Add(Task(door ? "Door" : "Wall", c, rot: door ? Rot4.East : Rot4.North));
            }
            result.Add(perimeter);
            AddCrops(anchor, result);
            var dump = new RoomProject { Kind = "Despejo", LayoutSlot = "dump", Origin = At(anchor, 46, 0), InteriorSize = 3, RequiresRoof = false, Priority = ConstructionPriority.High };
            dump.StorageCells = new CellRect(anchor.x+47, At(anchor,0,-1).z,3,3).Cells.ToList(); result.Add(dump);
            var support = new RoomProject { Kind = "Apoio inicial", LayoutSlot = "support", Origin = anchor, InteriorSize = 1, RequiresRoof = false, Priority = ConstructionPriority.Critical };
            support.Furniture.AddRange(result.Where(p => p.Kind == "Cozinha" || p.Kind == "Abate" || p.Kind == "Refeitório" || p.Kind == "Quarto").OrderBy(p => Rank(p.Kind)).SelectMany(p => p.Furniture));
            result.Add(support);
            var preparation = new RoomProject { Kind = "Preparação do terreno", LayoutSlot = "prepare", Origin = anchor, InteriorSize = 1, RequiresRoof = false, Priority = ConstructionPriority.Critical };
            preparation.MineCells = result.SelectMany(p => p.Shell.Concat(p.Furniture)).Where(t => t.Def is ThingDef).SelectMany(t => GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size))
                .Concat(result.Where(p=>p.RequiresRoof).SelectMany(p=>p.RoofArea)).Distinct().Where(c => c.GetEdifice(map) is Mineable).ToList();
            if (preparation.MineCells.Count > 0) result.Add(preparation);
            return !validate || Validate(map, result) ? result.OrderBy(p => p.Priority).ThenBy(p => Rank(p.Kind)).ToList() : null;
        }
        private static void AddPower(Map map, IntVec3 anchor, List<RoomProject> result, bool climate)
        {
            if (!Researched("Electricity")) return;
            var power = new RoomProject { Kind = "Energia e climatização", LayoutSlot = "power", Origin = At(anchor,66,20), InteriorSize = 11, InteriorHeight = 10, RequiresRoof = false };
            var generator = DefDatabase<ThingDef>.GetNamed("WoodFiredGenerator");
            float peak = (climate ? 2*DefDatabase<ThingDef>.GetNamed("Cooler").GetCompProperties<CompProperties_Power>().PowerConsumption +
                2*DefDatabase<ThingDef>.GetNamed("Heater").GetCompProperties<CompProperties_Power>().PowerConsumption : 0) +
                result.Count(p => p.RequiresRoof)*DefDatabase<ThingDef>.GetNamed("StandingLamp").GetCompProperties<CompProperties_Power>().PowerConsumption;
            int count = Math.Min(6,Math.Max(2,(int)Math.Ceiling(peak*1.2f/-generator.GetCompProperties<CompProperties_Power>().PowerConsumption)));
            for(int i=0;i<count;i++)power.Furniture.Add(Task("WoodFiredGenerator",At(anchor,67+(i%3)*4,10+(i/3)*5)));
            var cables = new HashSet<IntVec3>();
            void connect(IntVec3 c) { var trunk=At(anchor,33,24); for(int x=Math.Min(trunk.x,c.x);x<=Math.Max(trunk.x,c.x);x++)cables.Add(new IntVec3(x,0,trunk.z));
                for(int z=Math.Min(trunk.z,c.z);z<=Math.Max(trunk.z,c.z);z++)cables.Add(new IntVec3(c.x,0,z)); }
            foreach(var p in result.Where(p=>p.RequiresRoof && p.Kind!="Corredor"))
            {
                var occupied = result.SelectMany(r=>r.Furniture).Where(t=>t.Def is ThingDef).SelectMany(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size)).ToHashSet();
                var cell=p.Interior.First(c=>!occupied.Contains(c) && (!p.ReserveDoubleBed || !p.DoubleBedSpace.Contains(c)) &&
                    !p.StorageCells.Contains(c) && c != p.Origin+new IntVec3(1,0,1) && c != p.Origin+new IntVec3(3,0,5) && c != p.Origin+new IntVec3(2,0,5));
                var lamp=Task("StandingLamp",cell);power.Furniture.Add(lamp);connect(cell);
                foreach(var cooler in p.Shell.Where(t=>t.Def.defName=="Cooler"))connect(cooler.Position);
                foreach(var battery in p.Furniture.Where(t=>t.Def.defName=="Battery"))connect(battery.Position);
            }
            if(climate)foreach(int y in new[]{7,34}){var heater=Task("Heater",At(anchor,33,y));heater.TargetTemperature=20;power.Furniture.Add(heater);connect(heater.Position);}
            foreach(var t in power.Furniture)connect(t.Position);
            var generators=power.Furniture.Where(t=>t.Def==generator).Select(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size)).ToList();
            foreach(var c in cables.Where(c=>!generators.Any(r=>r.Contains(c))))power.Furniture.Add(Task("PowerConduit",c));
            result.Add(power);
        }
        private static void AddCrops(IntVec3 anchor,List<RoomProject> result)
        {
            ThingDef crop(string token,string fallback) => DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(d=>d.plant?.Sowable==true && d.defName.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0) ?? DefDatabase<ThingDef>.GetNamed(fallback);
            void add(string id,ThingDef plant,params int[][] rects)
            {
                var cells=rects.SelectMany(r=>new CellRect(anchor.x+r[0],At(anchor,0,r[1]+r[3]-1).z,r[2],r[3]).Cells).ToList();
                var p=new RoomProject {Kind="Plantação",LayoutSlot=id,Crop=plant,Origin=cells[0],InteriorSize=1,RequiresRoof=false,Priority=ConstructionPriority.High,StorageCells=cells};result.Add(p);
            }
            add("rice",DefDatabase<ThingDef>.GetNamed("Plant_Rice"),new[]{0,0,14,6},new[]{0,6,8,16});
            add("textile",crop("flax","Plant_Cotton"),new[]{46,0,17,6},new[]{56,6,7,4});
            add("potato",DefDatabase<ThingDef>.GetNamed("Plant_Potato"),new[]{0,26,8,17},new[]{8,34,6,9});
            add("medicine",DefDatabase<ThingDef>.GetNamed("Plant_Healroot"),new[]{56,33,7,4},new[]{46,37,15,6});
            add("hemp",crop("hemp","Plant_Smokeleaf"),new[]{61,37,2,6});
        }
        public static bool Validate(Map map,List<RoomProject> projects)
        {
            var occupied = new Dictionary<IntVec3,ConstructionTask>();
            foreach(var t in projects.SelectMany(p=>p.Shell.Concat(p.Furniture)).Where(t=>t.Def is ThingDef && t.Def.defName!="PowerConduit").GroupBy(t=>new{t.Def,t.Stuff,t.Position,t.Rotation}).Select(g=>g.First()))
            {
                foreach(var c in GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size))
                { if(occupied.ContainsKey(c)) {Log.Message("[AutonomousRim] Ring collision: "+occupied[c].Def.defName+"/"+t.Def.defName+" at "+c);return false;}occupied[c]=t; }
                var report=GenConstruct.CanPlaceBlueprintAt(t.Def,t.Position,t.Rotation,map,stuffDef:t.Stuff);
                if(!report && !t.Complete(map) && !t.Position.GetThingList(map).Any(b=>(b is Blueprint || b is Frame) && b.def.entityDefToBuild==t.Def && b.Rotation==t.Rotation) &&
                    !GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size).Any(c=>c.GetEdifice(map) is Mineable) && !t.Position.GetThingList(map).Any(b=>b.def==ThingDefOf.Wall && t.Def==ThingDefOf.Door && projects.Any(p=>p.ClearCells.Contains(t.Position))))
                { Log.Message("[AutonomousRim] Ring placement: "+t.Def.defName+" at "+t.Position+": "+report.Reason);return false; }
            }
            foreach(var t in projects.SelectMany(p=>p.Furniture).Where(t=>t.Def is ThingDef def && def.hasInteractionCell))
            {
                var cell=t.Position+((ThingDef)t.Def).interactionCellOffset.RotatedBy(t.Rotation);
                if(occupied.TryGetValue(cell,out var obstruction) && ((ThingDef)obstruction.Def).building?.isSittable!=true)
                {Log.Message("[AutonomousRim] Ring interaction blocked: "+t.Def.defName+" by "+obstruction.Def.defName);return false;}
            }
            return !projects.Where(p=>p.Crop!=null).SelectMany(p=>p.StorageCells).Any(occupied.ContainsKey);
        }
        public static string Plan(Map map,List<RoomProject> projects)
        {
            var pawn=map.mapPawns.FreeColonistsSpawned.FirstOrDefault();if(pawn==null)return "Sem colonos neste mapa.";
            var saved=projects.FirstOrDefault(p=>p.Kind==ReservationKind);
            if(saved!=null)
            {
                var candidate=Create(map,saved.LayoutAnchor,map.mapPawns.FreeColonistsSpawnedCount,false);
                foreach(var room in candidate.Where(p=>p.LayoutSlot!=null && p.Kind!=ReservationKind && !projects.Any(old=>old.LayoutSlot==p.LayoutSlot)))
                {
                    var replacements=new Dictionary<IntVec3,ConstructionTask>();
                    foreach(var door in room.Shell.Where(t=>t.Def==ThingDefOf.Door && t.Position.GetEdifice(map)?.def==ThingDefOf.Wall))
                        if(projects.SelectMany(p=>p.Shell).Any(t=>t.Position==door.Position && t.Def==ThingDefOf.Wall && t.Owned && t.Complete(map)))
                        { room.ClearCells.Add(door.Position); replacements[door.Position]=door; }
                    if(!Validate(map,new List<RoomProject>{room}))continue;
                    foreach(var p in projects)for(int i=0;i<p.Shell.Count;i++)
                        if(replacements.TryGetValue(p.Shell[i].Position,out var replacement))p.Shell[i]=replacement;
                    projects.Add(room);
                }
                foreach(var next in candidate.Where(p=>p.LayoutSlot!=null && p.Kind!=ReservationKind && p.Crop==null))
                {
                    var old=projects.FirstOrDefault(p=>p.LayoutSlot==next.LayoutSlot);if(old==null)continue;
                    foreach(var cell in next.MineCells.Where(c=>!old.MineCells.Contains(c))) {old.MineCells.Add(cell);old.Completed=false;}
                    foreach(var task in next.Furniture.Where(t=>!old.Furniture.Any(o=>o.Def==t.Def && o.Position==t.Position && o.Rotation==t.Rotation)))
                    {
                        if(task.Def is ThingDef && !Validate(map,new List<RoomProject>{new RoomProject{Furniture=new List<ConstructionTask>{task}}}))continue;
                        old.Furniture.Add(task);old.Completed=false;
                    }
                }
                return "Plano em anel salvo: estoque e alimentação primeiro; quartos conforme colonos; obras existentes preservadas.";
            }
            int beds=map.mapPawns.FreeColonistsSpawnedCount;
            foreach(var offset in GenRadial.RadialCellsAround(IntVec3.Zero,65,true).Where(c=>c.x%4==0 && c.z%4==0))
            {
                var anchor=pawn.Position+offset-new IntVec3(30,0,22);
                if(!Site(map,anchor))continue;
                var plan=Create(map,anchor,beds);if(plan==null)continue;
                projects.AddRange(plan);return "Plano em anel: estoque, cozinha, abate e refeitório prioridade 1; quartos por colono (até 12); reserva para energia, lavouras e muro.";
            }
            return "A base em anel requer uma clareira de 84×64 células, sem zonas/estruturas do jogador. Procurando terreno firme; obstáculos minerais podem ser escavados por trabalho nativo.";
        }
    }
}
