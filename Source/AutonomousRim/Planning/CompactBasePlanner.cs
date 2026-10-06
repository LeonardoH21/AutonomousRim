using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Planning
{
    public static class CompactBasePlanner
    {
        private static string lastPlacementFailure;
        private static bool Researched(string name) => DefDatabase<ResearchProjectDef>.GetNamed(name).IsFinished;
        private static int HeaterCount(int length) => Math.Max(2, (length + 6) / 7);
        private static int GeneratorCount(int length, int rooms)
        {
            float peak = HeaterCount(length) * DefDatabase<ThingDef>.GetNamed("Heater").GetCompProperties<CompProperties_Power>().PowerConsumption +
                2 * DefDatabase<ThingDef>.GetNamed("Cooler").GetCompProperties<CompProperties_Power>().PowerConsumption +
                rooms * DefDatabase<ThingDef>.GetNamed("StandingLamp").GetCompProperties<CompProperties_Power>().PowerConsumption;
            return (int)Math.Ceiling(peak * 1.2f / -DefDatabase<ThingDef>.GetNamed("WoodFiredGenerator").GetCompProperties<CompProperties_Power>().PowerConsumption);
        }
        public static string ClimateSummary(Map map)
        {
            float low = GenTemperature.MinTemperatureAtTile(map.Tile), high = GenTemperature.MaxTemperatureAtTile(map.Tile);
            return $"Clima sazonal estimado: {low:0} a {high:0} °C; exterior atual {map.mapTemperature.OutdoorTemp:0} °C. " +
                (Researched("Electricity") && Researched("AirConditioning") && Researched("ComplexFurniture")
                    ? "Novos blocos incluem ventilação, geradores a lenha e termostatos 20/24 °C; combustível e capacidade térmica precisam ser suficientes."
                    : "Climatização elétrica dos novos blocos requer Eletricidade, Ar-condicionado e Móveis complexos; pesquisas não são iniciadas automaticamente.");
        }

        public static List<RoomProject> Find(Map map, List<RoomProject> existing, IntVec3 anchor, List<KeyValuePair<string, int>> requests)
        {
            int length = 0;
            for (int i = 0; i < requests.Count; i += 2)
                length += Math.Max(requests[i].Value, i + 1 < requests.Count ? requests[i + 1].Value : 0) + 2;
            bool climate = Researched("Electricity") && Researched("AirConditioning") && Researched("ComplexFurniture");
            int width = Math.Max(18, climate ? 12 + GeneratorCount(length, requests.Count) * 3 : 18);
            lastPlacementFailure = null;
            int sites = 0;
            // Reserve the entire block and the outdoor generator/exhaust before accepting any room.
            for (int radius = 8; radius <= 65; radius += 3)
                foreach (IntVec3 offset in GenRadial.RadialCellsAround(IntVec3.Zero, radius, false)
                    .Where(c => c.x % 3 == 0 && c.z % 3 == 0 && c.LengthHorizontal >= radius - 3))
                {
                    IntVec3 origin = anchor + offset;
                    if (!BasePlanner.ClearSite(map, new CellRect(origin.x, origin.z - 1, width, length + 7), existing)) continue;
                    sites++;
                    if (!map.mapPawns.FreeColonistsSpawned.Any(p => p.CanReach(origin + new IntVec3(8, 0, -2), PathEndMode.OnCell, Danger.None))) continue;
                    List<RoomProject> result = Create(map, origin, requests, length);
                    if (result != null) return result;
                }
            if (Prefs.DevMode) Log.Message("[AutonomousRim] Compact placement: clear sites=" + sites + "; " + lastPlacementFailure);
            return null;
        }

        public static List<RoomProject> Create(Map map, IntVec3 origin, List<KeyValuePair<string, int>> requests, int length)
        {
            bool climate = Researched("Electricity") && Researched("AirConditioning") && Researched("ComplexFurniture");
            var result = new List<RoomProject>();
            var walls = new Dictionary<IntVec3, ConstructionTask>();
            ThingDef material = ThingDefOf.WoodLog;
            // A coherent stone exterior when resources already exist; wood remains the early fallback.
            int wallBudget = requests.Sum(r => (r.Value + 1) * 4 * 5) + length * 10;
            ThingDef stone = map.listerThings.AllThings.Where(t => t.def.IsStuff && t.def.stuffProps?.categories?.Contains(StuffCategoryDefOf.Stony) == true &&
                !t.IsForbidden(Faction.OfPlayer) && !t.Position.Fogged(map)).GroupBy(t => t.def)
                .Where(g => g.Sum(t => t.stackCount) >= wallBudget).OrderBy(g => g.Key.defName).Select(g => g.Key).FirstOrDefault();
            int row = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                if (i > 0 && i % 2 == 0) row += Math.Max(requests[i - 2].Value, requests[i - 1].Value) + 2;
                var request = requests[i];
                bool right = i % 2 == 1;
                IntVec3 roomOrigin = origin + new IntVec3(right ? 10 : 8 - (request.Value + 2), 0, row);
                RoomProject room = BasePlanner.CreateRoom(map, roomOrigin, request.Value, request.Key, material, right);
                if (room == null) { lastPlacementFailure = BasePlanner.LastRoomFailure; return null; }
                foreach (ConstructionTask task in room.Shell)
                {
                    if (stone != null && task.Def == ThingDefOf.Wall) task.Stuff = stone;
                    if (climate && task.Position.x == origin.x + (right ? 10 : 7) && task.Position.z == roomOrigin.z + 4)
                    {
                        task.Def = DefDatabase<ThingDef>.GetNamed("Vent"); task.Stuff = null; task.Rotation = Rot4.East;
                    }
                    walls[task.Position] = task;
                }
                result.Add(room);
            }
            var corridor = new RoomProject { Kind = "Corredor", Origin = origin + new IntVec3(7, 0, -1), InteriorSize = 2, InteriorHeight = length };
            foreach (IntVec3 cell in corridor.Footprint.EdgeCells)
            {
                if (walls.TryGetValue(cell, out ConstructionTask shared))
                {
                    // Dependency only: rooms are built first. Never emit a second blueprint for this shared wall.
                    corridor.Shell.Add(new ConstructionTask { Def = shared.Def, Stuff = shared.Stuff, Position = cell, Rotation = shared.Rotation });
                    continue;
                }
                bool end = cell.z == corridor.Footprint.minZ || cell.z == corridor.Footprint.maxZ;
                bool door = end && cell.x == origin.x + 8;
                bool cooler = climate && end && cell.x == origin.x + 9;
                corridor.Shell.Add(new ConstructionTask { Def = cooler ? DefDatabase<ThingDef>.GetNamed("Cooler") : door ? ThingDefOf.Door : ThingDefOf.Wall,
                    Stuff = cooler ? null : door ? material : stone ?? material, Position = cell,
                    Rotation = cooler && cell.z == corridor.Footprint.minZ ? Rot4.South : Rot4.North,
                    TargetTemperature = cooler ? 24f : -999f });
            }
            result.Add(corridor);
            if (climate)
            {
                var power = new RoomProject { Kind = "Energia e climatização", Origin = origin + new IntVec3(13, 0, length + 2), InteriorSize = 1, RequiresRoof = false };
                ThingDef generatorDef = DefDatabase<ThingDef>.GetNamed("WoodFiredGenerator"), heaterDef = DefDatabase<ThingDef>.GetNamed("Heater"), lampDef = DefDatabase<ThingDef>.GetNamed("StandingLamp");
                int heaters = HeaterCount(length);
                int generators = GeneratorCount(length, requests.Count);
                power.InteriorSize = generators * 3; power.InteriorHeight = 1;
                for (int i = 0; i < generators; i++)
                    power.Furniture.Add(new ConstructionTask { Def = generatorDef, Position = origin + new IntVec3(13 + i * 3, 0, length + 3) });
                var cableCells = new HashSet<IntVec3>();
                // Continuous feeder down the corridor, crossing doors without blocking walking.
                for (int z = -1; z <= length + 3; z++) cableCells.Add(origin + new IntVec3(8, 0, z));
                for (int x = 8; x <= 13 + (generators - 1) * 3; x++) cableCells.Add(origin + new IntVec3(x, 0, length + 3));
                var lamps = result.Where(p => p != corridor).Select(room => new ConstructionTask { Def = lampDef,
                    Position = room.Origin + new IntVec3(room.Origin.x >= origin.x + 10 ? 1 : room.InteriorSize - 1, 0, room.InteriorSize) }).ToList();
                foreach (ConstructionTask lamp in lamps)
                    for (int x = Math.Min(origin.x + 8, lamp.Position.x); x <= Math.Max(origin.x + 8, lamp.Position.x); x++)
                        cableCells.Add(new IntVec3(x, 0, lamp.Position.z));
                var generatorFootprints = power.Furniture.Select(t => GenAdj.OccupiedRect(t.Position, Rot4.North, generatorDef.size)).ToList();
                foreach (IntVec3 cell in cableCells.Where(c => !generatorFootprints.Any(rect => rect.Contains(c))).OrderBy(c => c.z).ThenBy(c => c.x))
                    power.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("PowerConduit"), Position = cell });
                for (int i = 0; i < heaters; i++)
                    power.Furniture.Add(new ConstructionTask { Def = heaterDef,
                        Position = origin + new IntVec3(9, 0, Math.Min(length - 2, 2 + i * 7)), TargetTemperature = 20f });
                power.Furniture.AddRange(lamps);
                result.Add(power);
            }
            // Comfort follows shelter and power; no bedhead or doorway is obstructed.
            if (Researched("ComplexFurniture"))
            {
                var comfort = new RoomProject { Kind = "Conforto dos quartos", Origin = origin, InteriorSize = 1, RequiresRoof = false };
                foreach (RoomProject bedroom in result.Where(p => p.Kind == "Quarto"))
                {
                    comfort.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("EndTable"), Stuff = material, Position = bedroom.Origin + new IntVec3(3, 0, 3) });
                    comfort.Furniture.Add(new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("Dresser"), Stuff = material, Position = bedroom.Origin + new IntVec3(4, 0, 1) });
                }
                if (comfort.Furniture.Count > 0) result.Add(comfort);
            }
            var finish = new RoomProject { Kind = "Pisos e acabamento", Origin = origin, InteriorSize = 1, RequiresRoof = false };
            int floorCells = result.Where(p => p.RequiresRoof).Sum(p => p.Interior.Count());
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor");
            if (Researched("Stonecutting"))
            {
                TerrainDef tile = DefDatabase<TerrainDef>.AllDefsListForReading.Where(t => t.defName.StartsWith("Tile", StringComparison.Ordinal) &&
                    t.costList?.Count == 1 && t.costList[0].thingDef.stuffProps?.categories?.Contains(StuffCategoryDefOf.Stony) == true)
                    .OrderBy(t => t.defName == "TileMarble" ? 0 : 1).ThenBy(t => t.defName).FirstOrDefault(t =>
                        map.listerThings.AllThings.Where(item => item.def == t.costList[0].thingDef && !item.IsForbidden(Faction.OfPlayer))
                            .Sum(item => item.stackCount) >= floorCells * t.costList[0].count + (stone == t.costList[0].thingDef ? wallBudget : 0));
                if (tile != null) floor = tile;
            }
            foreach (RoomProject room in result.Where(p => p.RequiresRoof))
                foreach (IntVec3 cell in room.Interior)
                    finish.Furniture.Add(new ConstructionTask { Def = floor, OriginalTerrain = cell.GetTerrain(map), Position = cell });
            result.Add(finish);
            // Validate native placement including the generator, vents, both exhaust outlets and comfort furniture.
            foreach (ConstructionTask task in result.SelectMany(p => p.Shell.Concat(p.Furniture)))
            {
                AcceptanceReport placement = GenConstruct.CanPlaceBlueprintAt(task.Def, task.Position, task.Rotation, map, stuffDef: task.Stuff);
                if (!placement) { lastPlacementFailure = task.Def.defName + " at " + task.Position + ": " + placement.Reason; return null; }
            }
            return result;
        }
    }
}

