using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Planning
{
    public static class BasePlanner
    {
        public static bool ClearSite(Map map, CellRect rect, IEnumerable<RoomProject> projects)
        {
            if (projects.Any(p => p.Footprint.ExpandedBy(2).Overlaps(rect))) return false;
            foreach (IntVec3 cell in rect.ExpandedBy(1))
            {
                if (!cell.InBounds(map) || cell.CloseToEdge(map, 10) || cell.Fogged(map) || !cell.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy) ||
                    cell.GetZone(map) != null || map.areaManager.NoRoof[cell] || cell.Roofed(map)) return false;
                // Native construction workers clear vegetation. Preserve buildings,
                // items, existing plans and player zones rather than clearing them.
                if (cell.GetThingList(map).Any(t => !(t is Pawn) && !(t is Plant))) return false;
            }
            return true;
        }

        public static RoomProject CreateRoom(Map map, IntVec3 origin, int interior, string kind, ThingDef material)
        {
            var project = new RoomProject { Origin = origin, InteriorSize = interior, Kind = kind };
            int edge = interior + 1;
            foreach (IntVec3 cell in project.Footprint.EdgeCells)
            {
                bool door = cell.x == origin.x + edge && cell.z == origin.z + 2;
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
            var occupied = new HashSet<IntVec3>();
            foreach (ConstructionTask task in project.Shell.Concat(project.Furniture))
            {
                if (!task.Def.MadeFromStuff) task.Stuff = null;
                if (!GenConstruct.CanPlaceBlueprintAt(task.Def, task.Position, task.Rotation, map, stuffDef: task.Stuff)) return null;
                foreach (IntVec3 cell in GenAdj.OccupiedRect(task.Position, task.Rotation, task.Def.size))
                    if (!occupied.Add(cell) || !project.Footprint.Contains(cell)) return null;
            }
            foreach (ConstructionTask task in project.Furniture.Where(t => t.Def.hasInteractionCell))
                if (occupied.Contains(task.Position + task.Def.interactionCellOffset.RotatedBy(task.Rotation))) return null;
            return project;
        }

        private static RoomProject FindRoom(Map map, List<RoomProject> projects, IntVec3 anchor, int size, string kind, ThingDef material)
        {
            // Search bounded, deterministic candidates; leave two cells between room footprints.
            for (int radius = 8; radius <= 50; radius += 3)
                foreach (IntVec3 offset in GenRadial.RadialCellsAround(IntVec3.Zero, radius, false)
                    .Where(c => c.x % 3 == 0 && c.z % 3 == 0 && c.LengthHorizontal >= radius - 3))
                {
                    IntVec3 origin = anchor + offset;
                    var rect = new CellRect(origin.x, origin.z, size + 2, size + 2);
                    if (!ClearSite(map, rect, projects)) continue;
                    IntVec3 entrance = origin + new IntVec3(size + 2, 0, 2);
                    if (!map.mapPawns.FreeColonistsSpawned.Any(p => p.CanReach(entrance, PathEndMode.OnCell, Danger.None))) continue;
                    RoomProject project = CreateRoom(map, origin, size, kind, material);
                    if (project != null) return project;
                }
            return null;
        }

        public static string Plan(Map map, List<RoomProject> projects)
        {
            Pawn anchorPawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (anchorPawn == null) return "Sem colonos neste mapa.";
            // Start with readily available wood; stone/steel room materials are a later policy.
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
            int added = 0;
            foreach (var request in requests)
            {
                RoomProject room = FindRoom(map, projects, anchorPawn.Position, request.Value, request.Key, ThingDefOf.WoodLog);
                if (room == null) return $"Plano: {added} novos módulos. Sem terreno livre/acessível para {request.Key}; construções existentes preservadas.";
                projects.Add(room); added++;
            }
            return $"Plano: {projects.Count} módulos; {added} novos. Quartos 5×5, estoque 6×6 e cozinha 4×4 internos.";
        }
    }
}
