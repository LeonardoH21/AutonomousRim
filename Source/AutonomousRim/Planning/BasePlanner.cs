using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Planning
{
    public static class BasePlanner
    {
        private static void AddInitialSupport(Map map, List<RoomProject> rooms, List<RoomProject> existing)
        {
            if (existing.Any(p => p.Kind == "Apoio inicial") || rooms.Count == 0 ||
                map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Count(b => !b.Medical && !b.ForPrisoners) >= map.mapPawns.FreeColonistsSpawnedCount) return;
            // Use the furniture already reserved in the final layout. No extra beds,
            // materials or temporary buildings: native workers build these pieces early.
            var support = new RoomProject { Kind = "Apoio inicial", Origin = rooms[0].Origin, InteriorSize = 1,
                RequiresRoof = false, Priority = ConstructionPriority.Critical };
            foreach (var task in rooms.Where(p => p.Kind == "Quarto" || p.Kind == "Cozinha" || p.Kind == "Sala social").SelectMany(p => p.Furniture))
                support.Furniture.Add(new ConstructionTask { Def = task.Def, Stuff = task.Stuff, Position = task.Position, Rotation = task.Rotation });
            if (support.Furniture.Count > 0) rooms.Insert(0, support);
        }
        public static string LastRoomFailure { get; private set; }
        private static bool Researched(string name) => DefDatabase<ResearchProjectDef>.GetNamed(name).IsFinished;

        private static void AddUtilityModules(Map map, List<RoomProject> rooms, List<RoomProject> existing)
        {
            if (rooms.Count == 0) return;
            var hospital = rooms.FirstOrDefault(p => p.Kind == "Hospital");
            if (hospital != null)
            {
                var medicine = new RoomProject { Kind = "Medicamentos", Origin = hospital.Origin,
                    InteriorSize = 1, RequiresRoof = false, Priority = ConstructionPriority.Normal };
                for (int x = 2; x <= 4; x++) medicine.StorageCells.Add(hospital.Origin + new IntVec3(x, 0, 1));
                rooms.Add(medicine);
            }
            if (Researched("ComplexFurniture"))
            {
                var shelves = new RoomProject { Kind = "Prateleiras", Origin = rooms[0].Origin,
                    InteriorSize = 1, RequiresRoof = false, Priority = ConstructionPriority.Low };
                foreach (var room in rooms.Where(p => p.Kind == "Estoque" || p.Kind == "Freezer" || p.Kind == "Armas"))
                    foreach (int x in new[] { 1, 3, 5 }.Where(x => x <= room.InteriorSize))
                        shelves.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("ShelfSmall"), Stuff = ThingDefOf.WoodLog,
                            Position = room.Origin + new IntVec3(x, 0, room.InteriorSize - 1), StorageKind = room.Kind });
                if (shelves.Furniture.Count > 0) rooms.Add(shelves);
            }
            var kitchen = rooms.Concat(existing).FirstOrDefault(p => p.Kind == "Cozinha");
            if (kitchen == null || existing.Any(p => p.Kind == "Despejo")) return;
            var workshop = rooms.Concat(existing).FirstOrDefault(p => p.Kind == "Oficina");
            var obstacles = rooms.Concat(existing).ToList();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(kitchen.Origin, 24, true)
                .OrderBy(c => c.DistanceTo(kitchen.Origin) + (workshop == null ? 0 : c.DistanceTo(workshop.Origin))))
            {
                var rect = new CellRect(cell.x, cell.z, 3, 3);
                // A dump may contain existing haulable chunks/items. It creates a
                // zone only; it neither destroys nor unforbids those things.
                if (obstacles.Any(p => p.Footprint.ExpandedBy(1).Overlaps(rect)) || rect.Cells.Any(c => !c.InBounds(map) || c.CloseToEdge(map, 10) ||
                    c.Fogged(map) || c.Roofed(map) || c.GetZone(map) != null || !c.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy) ||
                    c.GetThingList(map).Any(t => !(t is Pawn) && !(t is Plant) && !(t is Filth) && !(t.def.category == ThingCategory.Item && t.def.EverHaulable))) ||
                    !map.mapPawns.FreeColonistsSpawned.Any(p => p.CanReach(cell, PathEndMode.OnCell, Danger.None))) continue;
                rooms.Add(new RoomProject { Kind = "Despejo", Origin = cell - new IntVec3(1, 0, 1), InteriorSize = 3,
                    RequiresRoof = false, Priority = ConstructionPriority.High, StorageCells = rect.Cells.ToList() });
                break;
            }
        }
        public static void RepairUnissuedKitchenConflicts(Map map, IReadOnlyList<RoomProject> projects)
        {
            foreach (var room in projects.Where(p => (p.Kind == "Hospital" || p.Kind == "Oficina") && p.State != ConstructionState.Paused))
                foreach (var task in room.Furniture.Where(t => t.MedicalBed || t.Def.defName == "HandTailoringBench"))
                {
                    if (task.Issued || task.Pending != null || task.CancelledByPlayer || task.Complete(map) ||
                        GenConstruct.CanPlaceBlueprintAt(task.Def, task.Position, task.Rotation, map, stuffDef: task.Stuff)) continue;
                    var candidates = task.MedicalBed ? new[] { room.Origin + new IntVec3(2, 0, 5), room.Origin + new IntVec3(3, 0, 5) } :
                        new[] { room.Origin + new IntVec3(3, 0, 4) };
                    foreach (var cell in candidates)
                    {
                        var rect = GenAdj.OccupiedRect(cell, task.Rotation, task.Def.Size);
                        if (!rect.Cells.All(c => room.Interior.Contains(c)) || room.Furniture.Any(t => t != task &&
                            GenAdj.OccupiedRect(t.Position, t.Rotation, t.Def.Size).Overlaps(rect)) ||
                            !GenConstruct.CanPlaceBlueprintAt(task.Def, cell, task.Rotation, map, stuffDef: task.Stuff)) continue;
                        Log.Message("[AutonomousRim] Recovery: móvel ainda não emitido reposicionado em " + room.Kind + ": " + task.Def.defName + " " + task.Position + " -> " + cell);
                        task.Position = cell; break;
                    }
                }
            // Migrate the former kitchen layout only before a table has been ordered.
            // Existing furniture, invested frames and player cancellations stay intact.
            foreach (var room in projects.Where(p => p.Kind == "Cozinha" && p.State != ConstructionState.Paused))
            {
                var oldPosition = room.Origin + new IntVec3(2, 0, 1);
                var table = room.Furniture.FirstOrDefault(t => t.Def.defName == "TableButcher" && t.Position == oldPosition);
                if (table == null) continue;
                var aliases = projects.SelectMany(p => p.Furniture).Where(t => t.Def == table.Def && t.Position == oldPosition).ToList();
                if (aliases.Any(t => t.Issued || t.Pending != null || t.CancelledByPlayer || t.Complete(map))) continue;
                var oldReport = GenConstruct.CanPlaceBlueprintAt(table.Def, table.Position, table.Rotation, map, stuffDef: table.Stuff);
                var destination = room.Origin + new IntVec3(3, 0, 1);
                if (oldReport || !GenConstruct.CanPlaceBlueprintAt(table.Def, destination, table.Rotation, map, stuffDef: table.Stuff)) continue;
                foreach (var task in aliases) task.Position = destination;
                Log.Message("[AutonomousRim] Recovery: bancada ainda não emitida reposicionada dentro da cozinha: " + oldReport.Reason);
            }
        }
        public static bool ClearSite(Map map, CellRect rect, IEnumerable<RoomProject> projects)
        {
            if (projects.Any(p => p.Footprint.ExpandedBy(2).Overlaps(rect))) return false;
            foreach (IntVec3 cell in rect.ExpandedBy(1))
            {
                if (!cell.InBounds(map) || cell.CloseToEdge(map, 10) || cell.Fogged(map) || !cell.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy) ||
                    cell.GetZone(map) != null || map.areaManager.NoRoof[cell] || cell.Roofed(map)) return false;
                // Native workers clear plants and haul permitted items aside without destroying them.
                // Forbidden items, buildings, existing plans and player zones remain obstacles.
                if (cell.GetThingList(map).Any(t => !(t is Pawn) && !(t is Plant) && !(t is Filth) &&
                    !(t.def.category == ThingCategory.Item && t.def.EverHaulable && !t.IsForbidden(Faction.OfPlayer)))) return false;
            }
            return true;
        }

        public static RoomProject CreateRoom(Map map, IntVec3 origin, int interior, string kind, ThingDef material, bool westDoor = false)
        {
            LastRoomFailure = null;
            var project = new RoomProject { Origin = origin, InteriorSize = interior, Kind = kind };
            int edge = interior + 1;
            foreach (IntVec3 cell in project.Footprint.EdgeCells)
            {
                bool door = cell.x == origin.x + (westDoor ? 0 : edge) && cell.z == origin.z + 2;
                project.Shell.Add(new ConstructionTask { Def = door ? ThingDefOf.Door : ThingDefOf.Wall,
                    Stuff = material, Position = cell, Rotation = door ? Rot4.East : Rot4.North });
            }
            if (kind == "Quarto")
            {
                project.ReserveDoubleBed = true;
                project.Furniture.Add(new ConstructionTask { Def = ThingDefOf.Bed,
                    Stuff = material, Position = origin + new IntVec3(4, 0, 5), Rotation = Rot4.South });
            }
            if (kind == "Cozinha")
            {
                project.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("FueledStove"),
                    Position = origin + new IntVec3(2, 0, 3), Stuff = ThingDefOf.Steel });
                project.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("TableButcher"),
                    Position = origin + new IntVec3(3, 0, 1), Stuff = material, Rotation = Rot4.South });
                project.Furniture.Add(new ConstructionTask { Def = ThingDefOf.Stool, Stuff = material, Position = origin + new IntVec3(2, 0, 2) });
            }
            if (kind == "Sala social")
            {
                project.Priority = ConstructionPriority.Normal;
                project.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("Table2x2c"), Stuff = material, Position = origin + new IntVec3(2, 0, 2) });
                foreach (int z in new[] { 2, 3 })
                    project.Furniture.Add(new ConstructionTask { Def = ThingDefOf.Stool, Stuff = material, Position = origin + new IntVec3(1, 0, z), Rotation = Rot4.East });
                project.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("ChessTable"), Stuff = material, Position = origin + new IntVec3(5, 0, 5) });
                project.Furniture.Add(new ConstructionTask { Def = ThingDefOf.Stool, Stuff = material, Position = origin + new IntVec3(5, 0, 4) });
            }
            if (kind == "Hospital")
            {
                project.Priority = ConstructionPriority.Normal;
                foreach (int x in new[] { 1, 4 })
                    project.Furniture.Add(new ConstructionTask { Def = ThingDefOf.Bed, Stuff = material,
                        Position = origin + new IntVec3(x, 0, 5), Rotation = Rot4.South, MedicalBed = true });
            }
            if (kind == "Armas" || kind == "Oficina") project.Priority = ConstructionPriority.Normal;
            if (kind == "Oficina")
            {
                if (Researched("Stonecutting")) project.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("TableStonecutter"),
                    Stuff = material, Position = origin + new IntVec3(3, 0, 1), Rotation = Rot4.South });
                if (Researched("ComplexClothing")) project.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("HandTailoringBench"),
                    Stuff = material, Position = origin + new IntVec3(3, 0, 5) });
            }
            var occupied = new HashSet<IntVec3>();
            foreach (ConstructionTask task in project.Shell.Concat(project.Furniture))
            {
                if (!task.Def.MadeFromStuff) task.Stuff = null;
                AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(task.Def, task.Position, task.Rotation, map, stuffDef: task.Stuff);
                if (!report) { LastRoomFailure = kind + ": " + task.Def.defName + " " + report.Reason; return null; }
                foreach (IntVec3 cell in GenAdj.OccupiedRect(task.Position, task.Rotation, ((ThingDef)task.Def).size))
                    if (!occupied.Add(cell) || !project.Footprint.Contains(cell)) { LastRoomFailure = kind + ": footprint " + task.Def.defName; return null; }
            }
            foreach (ConstructionTask task in project.Furniture.Where(t => t.Def is ThingDef thing && thing.hasInteractionCell))
            {
                IntVec3 interaction = task.Position + ((ThingDef)task.Def).interactionCellOffset.RotatedBy(task.Rotation);
                if (project.Shell.Concat(project.Furniture).Any(t => GenAdj.OccupiedRect(t.Position, t.Rotation, ((ThingDef)t.Def).size).Contains(interaction) &&
                    ((ThingDef)t.Def).building?.isSittable != true)) { LastRoomFailure = kind + ": interaction " + task.Def.defName; return null; }
            }
            return project;
        }

        public static string Plan(Map map, List<RoomProject> projects)
        {
            Pawn anchorPawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (anchorPawn == null) return "Sem colonos neste mapa.";
            int existingBedrooms = map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                .Where(b => !b.Medical && !b.ForPrisoners && b.GetRoom()?.Role == RoomRoleDefOf.Bedroom && b.GetRoom().CellCount >= 25)
                .Select(b => b.GetRoom()).Distinct().Count();
            int missing = System.Math.Max(0, map.mapPawns.FreeColonistsSpawnedCount - existingBedrooms - projects.Count(p => p.Kind == "Quarto" && !p.Completed));
            var requests = new List<KeyValuePair<string, int>>();
            for (int i = 0; i < System.Math.Min(missing, 12); i++) requests.Add(new KeyValuePair<string, int>("Quarto", 5));
            if (!map.zoneManager.AllZones.OfType<Zone_Stockpile>().Any(z => z.GetStoreSettings().AllowedToAccept(ThingDefOf.WoodLog) &&
                z.GetStoreSettings().AllowedToAccept(ThingDefOf.Steel)) && !projects.Any(p => p.Kind == "Estoque"))
                requests.Add(new KeyValuePair<string, int>("Estoque", 6));
            bool stove = map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Any(b => b.def.AllRecipes.Any(r => r.defName == "CookMealSimple"));
            bool butcher = map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Any(b => b.def.AllRecipes.Any(r => r.defName == "ButcherCorpseFlesh"));
            if ((!stove || !butcher) && !projects.Any(p => p.Kind == "Cozinha")) requests.Add(new KeyValuePair<string, int>("Cozinha", 4));
            if (!projects.Any(p => p.Kind == "Freezer") && DefDatabase<ResearchProjectDef>.GetNamed("AirConditioning").IsFinished &&
                !map.listerBuildings.AllBuildingsColonistOfClass<Building>().Any(b => b.def.defName == "Cooler" && b.GetRoom()?.Temperature < 0))
                requests.Add(new KeyValuePair<string, int>("Freezer", 6));
            if (!projects.Any(p => p.Kind == "Sala social") && !map.listerBuildings.AllBuildingsColonistOfClass<Building>().Any(b => b.GetRoom()?.Role?.defName == "RecRoom"))
                requests.Add(new KeyValuePair<string, int>("Sala social", 6));
            if (!projects.Any(p => p.Kind == "Hospital") && !map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Any(b => b.Medical))
                requests.Add(new KeyValuePair<string, int>("Hospital", 5));
            if (!projects.Any(p => p.Kind == "Oficina") && (Researched("ComplexClothing") || Researched("Stonecutting")) &&
                !map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Any(b => b.def.defName == "HandTailoringBench" || b.def.defName == "ElectricTailoringBench"))
                requests.Add(new KeyValuePair<string, int>("Oficina", 5));
            if (!projects.Any(p => p.Kind == "Armas") && !map.zoneManager.AllZones.OfType<Zone_Stockpile>().Any(z =>
                z.GetStoreSettings().filter.AllowedThingDefs.Any() && z.GetStoreSettings().filter.AllowedThingDefs.All(d => d.IsWeapon)))
                requests.Add(new KeyValuePair<string, int>("Armas", 4));
            requests = requests.OrderBy(r => r.Key == "Cozinha" ? 0 : r.Key == "Oficina" ? 1 : r.Key == "Estoque" ? 2 : r.Key == "Freezer" ? 3 : r.Key == "Quarto" ? 4 : 5).ToList();
            if (requests.Count > 0)
            {
                List<RoomProject> compact = CompactBasePlanner.Find(map, projects, anchorPawn.Position, requests);
                if (compact != null)
                {
                    AddUtilityModules(map, compact, projects);
                    AddInitialSupport(map, compact, projects);
                    projects.AddRange(compact);
                    return $"Plano compacto: {compact.Count} módulos novos, corredor de 2 células e duas saídas. {CompactBasePlanner.ClimateSummary(map)}";
                }
                // Do not make all basic storage depend on finding a large uninterrupted rectangle.
                var independent = new List<RoomProject>();
                foreach (var request in requests)
                {
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(anchorPawn.Position, 45f, true))
                    {
                        bool freezer = request.Key == "Freezer";
                        if (!ClearSite(map, new CellRect(cell.x - (freezer ? 6 : 0), cell.z, request.Value + 2 + (freezer ? 6 : 0), request.Value + 2), projects) ||
                            !map.mapPawns.FreeColonistsSpawned.Any(p => p.CanReach(cell + new IntVec3(request.Value + 2, 0, 2), PathEndMode.OnCell, Danger.None))) continue;
                        RoomProject room = CreateRoom(map, cell, request.Value, request.Key, ThingDefOf.WoodLog);
                        if (room == null) continue;
                        if (freezer)
                        {
                            foreach (var wall in room.Shell.Where(t => t.Position.x == cell.x && (t.Position.z == cell.z + 3 || t.Position.z == cell.z + 4)))
                            { wall.Def = DefDatabase<ThingDef>.GetNamed("Cooler"); wall.Stuff = null; wall.Rotation = Rot4.West; wall.TargetTemperature = -2; }
                            var supply = new RoomProject { Kind = "Energia e climatização", Origin = cell + new IntVec3(-6, 0, 2), InteriorSize = 2, InteriorHeight = 2, RequiresRoof = false };
                            supply.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("WoodFiredGenerator"), Position = cell + new IntVec3(-4, 0, 4) });
                            for (int x = -2; x <= 0; x++) supply.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("PowerConduit"), Position = cell + new IntVec3(x, 0, 4) });
                            projects.Add(supply);
                        }
                        projects.Add(room); independent.Add(room); break;
                    }
                }
                var supportOnly = new List<RoomProject>(independent);
                var utilities = new List<RoomProject>(independent);
                AddUtilityModules(map, utilities, projects.Except(independent).ToList());
                projects.AddRange(utilities.Except(independent));
                AddInitialSupport(map, supportOnly, projects);
                if (supportOnly.FirstOrDefault()?.Kind == "Apoio inicial") projects.Insert(0, supportOnly[0]);
                return projects.Count > 0 ? "Plano por salas independentes: terreno não comporta bloco compacto; módulos básicos preservam acessos." :
                    "Sem terreno livre/acessível nem para sala inicial; limpar obstáculos pelo trabalho normal ou escolher outro local.";
            }
            return $"Plano: {projects.Count} módulos. {CompactBasePlanner.ClimateSummary(map)}";
        }
    }
}
