using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Planning
{
    public static class BasePlanner
    {
        public static string LastRoomFailure { get; private set; }
        public static bool ClearSite(Map map, CellRect rect, IEnumerable<RoomProject> projects)
        {
            if (projects.Any(p => p.Footprint.ExpandedBy(2).Overlaps(rect))) return false;
            foreach (IntVec3 cell in rect.ExpandedBy(1))
            {
                if (!cell.InBounds(map) || cell.CloseToEdge(map, 10) || cell.Fogged(map) || !cell.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy) ||
                    cell.GetZone(map) != null || map.areaManager.NoRoof[cell] || cell.Roofed(map)) return false;
                // Native workers clear plants and haul permitted items aside without destroying them.
                // Forbidden items, buildings, existing plans and player zones remain obstacles.
                if (cell.GetThingList(map).Any(t => !(t is Pawn) && !(t is Plant) &&
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
            if (kind == "Quarto") project.Furniture.Add(new ConstructionTask { Def = ThingDefOf.Bed,
                Stuff = material, Position = origin + new IntVec3(2, 0, 3) });
            if (kind == "Cozinha")
            {
                project.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("FueledStove"),
                    Position = origin + new IntVec3(2, 0, 3), Stuff = ThingDefOf.Steel });
                project.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("TableButcher"),
                    Position = origin + new IntVec3(2, 0, 1), Stuff = material, Rotation = Rot4.South });
            }
            if (kind == "Sala social")
            {
                project.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("Table2x2c"), Stuff = material, Position = origin + new IntVec3(2, 0, 2) });
                foreach (int z in new[] { 2, 3 })
                    project.Furniture.Add(new ConstructionTask { Def = ThingDefOf.Stool, Stuff = material, Position = origin + new IntVec3(1, 0, z), Rotation = Rot4.East });
                project.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("ChessTable"), Stuff = material, Position = origin + new IntVec3(5, 0, 5) });
                project.Furniture.Add(new ConstructionTask { Def = ThingDefOf.Stool, Stuff = material, Position = origin + new IntVec3(5, 0, 4) });
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
                if (occupied.Contains(task.Position + ((ThingDef)task.Def).interactionCellOffset.RotatedBy(task.Rotation))) { LastRoomFailure = kind + ": interaction " + task.Def.defName; return null; }
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
            if (!map.zoneManager.AllZones.OfType<Zone_Stockpile>().Any() && !projects.Any(p => p.Kind == "Estoque"))
                requests.Add(new KeyValuePair<string, int>("Estoque", 6));
            bool stove = map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Any(b => b.def.AllRecipes.Any(r => r.defName == "CookMealSimple"));
            bool butcher = map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Any(b => b.def.AllRecipes.Any(r => r.defName == "ButcherCorpseFlesh"));
            if ((!stove || !butcher) && !projects.Any(p => p.Kind == "Cozinha")) requests.Add(new KeyValuePair<string, int>("Cozinha", 4));
            if (!projects.Any(p => p.Kind == "Sala social") && !map.listerBuildings.AllBuildingsColonistOfClass<Building>().Any(b => b.GetRoom()?.Role?.defName == "RecRoom"))
                requests.Add(new KeyValuePair<string, int>("Sala social", 6));
            if (requests.Count > 0)
            {
                List<RoomProject> compact = CompactBasePlanner.Find(map, projects, anchorPawn.Position, requests);
                if (compact != null)
                {
                    projects.AddRange(compact);
                    return $"Plano compacto: {compact.Count} módulos novos, corredor de 2 células e duas saídas. {CompactBasePlanner.ClimateSummary(map)}";
                }
                return "Sem terreno contínuo livre/acessível para o bloco compacto; plano existente preservado. Remova obstáculos manualmente ou escolha outro local.";
            }
            return $"Plano: {projects.Count} módulos. {CompactBasePlanner.ClimateSummary(map)}";
        }
    }
}
