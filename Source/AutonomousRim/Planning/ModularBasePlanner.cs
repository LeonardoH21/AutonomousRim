using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Execution;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Planning
{
    // Thirteen cells include exterior walls. Quarter partitions share one wall:
    // 1 + 5 + 1 + 5 + 1. Three walkable cells separate adjacent modules.
    public static class ModularBasePlanner
    {
        public const string ReservationKind = "Núcleo modular 13x13";
        public const int Size = 13, Stride = 16;
        public static bool IsModular(RoomProject p) => p.LayoutSlot?.StartsWith("mod:") == true;
        public static IEnumerable<IntVec3> Grid()
        {
            yield return IntVec3.Zero;
            for (int r = 1; r <= 12; r++)
            {
                yield return new IntVec3(0, 0, r);
                yield return new IntVec3(0, 0, -r);
                yield return new IntVec3(-r, 0, 0);
                yield return new IntVec3(r, 0, 0);
                for (int i = -r; i <= r; i++)
                    for (int j = -r; j <= r; j++)
                        if (Math.Max(Math.Abs(i), Math.Abs(j)) == r && i != 0 && j != 0)
                            yield return new IntVec3(i, 0, j);
            }
        }
        internal static ConstructionTask Thing(string name, IntVec3 cell, Rot4? rotation = null)
        {
            var def = DefDatabase<ThingDef>.GetNamed(name);
            return new ConstructionTask { Def = def, Stuff = def.MadeFromStuff ? ThingDefOf.WoodLog : null,
                Position = cell, Rotation = rotation ?? Rot4.North };
        }
        private static bool Site(Map map, CellRect rect)
        {
            if (!rect.ExpandedBy(3).InBounds(map)) return false;
            foreach (var c in rect)
            {
                if (c.CloseToEdge(map, 10) || c.Fogged(map) && !(c.GetEdifice(map) is Mineable) ||
                    !c.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy) || c.GetZone(map) != null || map.areaManager.NoRoof[c]) return false;
                if (c.GetThingList(map).Any(t => !(t is Pawn) && !(t is Plant) && !(t is Filth) && !(t is Mote) &&
                    !(t is Mineable && t.Faction == null) && !(t is Building b && b.Faction == null && b.DeconstructibleBy(Faction.OfPlayer)) &&
                    !(t.def.category == ThingCategory.Item && t.def.EverHaulable))) return false;
            }
            return true;
        }
        public static int Rank(string kind) => kind == "Quarto" ? 0 : kind == "Cozinha" ? 1 : kind == "Abate" ? 2 :
            kind == "Estoque" ? 3 : kind == "Freezer" ? 4 : kind == "Energia e climatização" ? 5 : 6;
        private static bool Functional(Map map,RoomProject p) => p.Completed || (p.Kind == "Estoque" || p.Kind == "Freezer") &&
            !BaseConstructionManager.Tasks(p).Any(t => t.CancelledByPlayer) && p.Shell.All(t => t.Complete(map)) &&
            p.Furniture.Where(t => t.Def.defName != "ShelfSmall").All(t => t.Complete(map)) &&
            (!p.RequiresRoof || p.RoofArea.All(c => c.Roofed(map)));
        private static bool ShelterReady(Map map,List<RoomProject> projects)
        {
            int population=map.mapPawns.FreeColonistsSpawnedCount;
            var bedrooms=projects.Where(p=>IsModular(p) && p.Kind=="Quarto").OrderByDescending(p=>Functional(map,p)).Take(population).ToList();
            return bedrooms.Count==population && bedrooms.All(p=>Functional(map,p));
        }
        private static bool CoreServicesReady(Map map,List<RoomProject> projects) => ShelterReady(map,projects) &&
            projects.Where(p=>IsModular(p) && p.Crop==null && p.Kind!="Quarto").All(p=>Functional(map,p));
        private static bool InitialCoreReady(Map map,List<RoomProject> projects)
        {
            return ShelterReady(map,projects) && projects.Where(p=>IsModular(p) &&
                (p.Kind=="Cozinha" || p.Kind=="Abate" || p.Kind=="Estoque" || p.Kind=="Freezer" || p.Kind=="Pesquisa" ||
                 p.Kind=="Energia e climatização")).All(p=>Functional(map,p));
        }

        private static void AddCirculation(Map map, List<RoomProject> projects, IntVec3 root)
        {
            var modules = projects.Where(IsModular).Select(p => p.LayoutSlot.Split(':')).Select(p => new IntVec3(int.Parse(p[1]), 0, int.Parse(p[2]))).Distinct().ToList();
            var reserved = projects.SelectMany(p => p.Furniture).Where(t => t.Def is TerrainDef).Select(t => t.Position).ToHashSet();
            foreach (var grid in modules)
            {
                string slot = $"corridor:{grid.x}:{grid.z}";
                if (projects.Any(p => p.LayoutSlot == slot)) continue;
                var origin = root + new IntVec3(grid.x * Stride, 0, grid.z * Stride);
                var rect = new CellRect(origin.x, origin.z, Size, Size);
                var path = new RoomProject { Kind = "Corredor", LayoutSlot = slot, LayoutAnchor = root, Origin = origin,
                    InteriorSize = 11, RequiresRoof = false, Priority = ConstructionPriority.Low };
                foreach (var c in rect.ExpandedBy(3).Where(c => !rect.Contains(c)))
                {
                    if (!c.InBounds(map) || !c.GetTerrain(map).affordances.Contains(DefDatabase<TerrainDef>.GetNamed("Concrete").terrainAffordanceNeeded) || c.GetEdifice(map)?.Faction != null || !reserved.Add(c)) continue;
                    path.Furniture.Add(new ConstructionTask { Def = DefDatabase<TerrainDef>.GetNamed("Concrete"), Position = c, OriginalTerrain = c.GetTerrain(map) });
                }
                var prep = new RoomProject { Kind = "Preparação do terreno", LayoutSlot = "prep:" + slot,
                    Origin = origin, InteriorSize = 11, RequiresRoof = false, Priority = ConstructionPriority.Critical };
                foreach (var c in path.Furniture.Select(t => t.Position))
                    if (c.GetEdifice(map) is Mineable) prep.MineCells.Add(c);
                    else if (c.GetEdifice(map) is Building b && b.Faction == null && b.DeconstructibleBy(Faction.OfPlayer)) prep.ClearCells.Add(c);
                if (prep.MineCells.Count > 0 || prep.ClearCells.Count > 0) projects.Add(prep);
                projects.Add(path);
            }
        }

        private static void AddFoodGrowing(Map map, List<RoomProject> projects, IntVec3 root)
        {
            if (projects.Any(p => p.Crop != null) || projects.Any(p => p.Kind == "Quarto" && !p.Completed)) return;
            var rice = DefDatabase<ThingDef>.GetNamed("Plant_Rice");
            foreach (var grid in Grid())
            {
                string prefix = $"mod:{grid.x}:{grid.z}:";
                if (projects.Any(p => p.LayoutSlot?.StartsWith(prefix) == true)) continue;
                if (!projects.Where(IsModular).Any(p => Math.Abs(int.Parse(p.LayoutSlot.Split(':')[1]) - grid.x) + Math.Abs(int.Parse(p.LayoutSlot.Split(':')[2]) - grid.z) == 1)) continue;
                var origin = root + new IntVec3(grid.x * Stride, 0, grid.z * Stride);
                var rect = new CellRect(origin.x, origin.z, Size, Size);
                if (projects.Any(p=>p.LayoutSlot=="prison:reserve" && p.Footprint.ExpandedBy(2).Overlaps(rect)) || !Site(map, rect) || rect.Any(c => c.GetEdifice(map) != null || c.Roofed(map) || c.GetTerrain(map).fertility < rice.plant.fertilityMin)) continue;
                projects.Add(new RoomProject { Kind = "Plantação inicial", LayoutSlot = prefix + "15", LayoutAnchor = root, Origin = origin,
                    InteriorSize = 11, Crop = rice, RequiresRoof = false, StorageCells = rect.Cells.ToList(), NoRoofCells = rect.Cells.ToList(), Priority = ConstructionPriority.High });
                break;
            }
        }

        private static bool AddRoom(Map map, List<RoomProject> projects, IntVec3 anchor, string kind, int parts)
        {
            foreach (var grid in Grid())
            {
                string prefix = $"mod:{grid.x}:{grid.z}:";
                var existing = projects.Where(p => p.LayoutSlot?.StartsWith(prefix) == true).ToList();
                int used = existing.Aggregate(0, (mask, p) => mask | int.Parse(p.LayoutSlot.Split(':')[3]));
                var origin = anchor + new IntVec3(grid.x * Stride, 0, grid.z * Stride);
                if (existing.Count == 0 && projects.Any(IsModular) && !projects.Where(IsModular).Any(p =>
                    Math.Abs(int.Parse(p.LayoutSlot.Split(':')[1]) - grid.x) + Math.Abs(int.Parse(p.LayoutSlot.Split(':')[2]) - grid.z) == 1)) continue;
                if (projects.Any(p=>p.LayoutSlot=="prison:reserve" && p.Footprint.ExpandedBy(2).Overlaps(new CellRect(origin.x,origin.z,Size,Size))) || existing.Count == 0 && !Site(map, new CellRect(origin.x, origin.z, Size, Size))) continue;
                foreach (int mask in parts == 4 ? new[] { 15 } : parts == 2 ? new[] { 3, 12 } : new[] { 1, 2, 4, 8 })
                {
                    if ((used & mask) != 0) continue;
                    int x = mask == 2 || mask == 8 ? 6 : 0, z = mask >= 4 && mask != 15 ? 6 : 0;
                    var p = new RoomProject { Kind = kind, LayoutSlot = prefix + mask, LayoutAnchor = anchor,
                        Origin = origin + new IntVec3(x, 0, z), InteriorSize = parts > 1 ? 11 : 5,
                        InteriorHeight = parts == 4 ? 11 : 5,
                        Priority = kind == "Quarto" ? ConstructionPriority.Critical : ConstructionPriority.High };
                    if (existing.Count > 0 && !Site(map, p.Interior)) continue;
                    // Quarter doors always face outside their module, never an unused quarter.
                    var door = p.Origin + new IntVec3(3, 0, z == 0 ? 0 : p.Height + 1);
                    foreach (var c in p.Footprint.EdgeCells) p.Shell.Add(Thing(c == door ? "Door" : "Wall", c));
                    foreach (var c in p.Interior.Cells.Concat(new[] { door }))
                        p.Furniture.Add(new ConstructionTask { Def = DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor"), Position = c, OriginalTerrain = c.GetTerrain(map) });
                    var center = p.Origin + new IntVec3(3, 0, 3);
                    if (kind == "Quarto") p.Furniture.Add(Thing("Bed", p.Origin + new IntVec3(1, 0, 4)));
                    if (kind == "Cozinha") p.Furniture.Add(Thing("FueledStove", center));
                    if (kind == "Abate") p.Furniture.Add(Thing("TableButcher", center));
                    if (kind == "Pesquisa") { p.Furniture.Add(Thing("SimpleResearchBench", center)); p.Furniture.Add(Thing("SimpleResearchBench", p.Origin + new IntVec3(9, 0, 3))); }
                    if (kind == "Hospital") p.Furniture.Add(new ConstructionTask { Def = ThingDefOf.Bed, Stuff = ThingDefOf.WoodLog, Position = p.Origin + new IntVec3(1, 0, 4), MedicalBed = true });
                    if (kind == "Estoque" || kind == "Freezer") p.StorageCells = p.Interior.Cells.ToList();
                    if (kind == "Freezer")
                    {
                        var cooler = p.Shell.First(t => t.Position == p.Origin + new IntVec3(8, 0, 0));
                        cooler.Def = DefDatabase<ThingDef>.GetNamed("Cooler"); cooler.Stuff = null; cooler.Rotation = Rot4.South; cooler.TargetTemperature = -2;
                        p.NoRoofCells.Add(cooler.Position + IntVec3.South);
                    }
                    if (kind == "Energia e climatização" || kind == "Gerador auxiliar") p.Furniture.Add(Thing("WoodFiredGenerator", center));
                    // Reuse exact walls, including serialized task ownership in existing saves.
                    var shared = projects.SelectMany(r => r.Shell).GroupBy(t => t.Position).ToDictionary(g => g.Key, g => g.First());
                    for (int i = 0; i < p.Shell.Count; i++) if (shared.TryGetValue(p.Shell[i].Position, out var wall)) p.Shell[i] = wall;
                    var preparation = new RoomProject { Kind = "Preparação do terreno", LayoutSlot = "prep:" + p.LayoutSlot,
                        Origin = p.Origin, InteriorSize = p.InteriorSize, InteriorHeight = p.Height, RequiresRoof = false, Priority = p.Priority };
                    // Clear the actual entrance as well as the room. A valid floor site
                    // alone does not guarantee an accessible doorway after enclosing walls.
                    var approach = Enumerable.Range(1, 3).Select(i => door + new IntVec3(0, 0, z == 0 ? -i : i));
                    foreach (var c in p.Footprint.Cells.Concat(p.NoRoofCells).Concat(approach).Distinct())
                    {
                        if (c.GetThingList(map).OfType<Plant>().Any()) preparation.PlantCells.Add(c);
                        if (c.GetEdifice(map) is Mineable) preparation.MineCells.Add(c);
                        else if (c.GetEdifice(map) is Building b && b.Faction == null) preparation.ClearCells.Add(c);
                    }
                    if (preparation.MineCells.Count > 0 || preparation.ClearCells.Count > 0 || preparation.PlantCells.Count > 0) projects.Add(preparation);
                    projects.Add(p);
                    return true;
                }
            }
            return false;
        }
        public static string Plan(Map map, List<RoomProject> projects)
        {
            var pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (pawn == null) return "Sem colonos para planejar.";
            var marker = projects.FirstOrDefault(p => p.Kind == ReservationKind);
            if (marker == null)
            {
                var anchor = GenRadial.RadialCellsAround(pawn.Position, 70, true).Where(c => c.x % 2 == 0 && c.z % 2 == 0)
                    .Select(c => c - new IntVec3(6, 0, 6)).FirstOrDefault(c => Site(map, new CellRect(c.x, c.z, Size, Size)) &&
                    pawn.CanReach(c, PathEndMode.Touch, Danger.None));
                if (anchor == default(IntVec3)) return "Procurando solo firme para o primeiro módulo 13×13.";
                marker = new RoomProject { Kind = ReservationKind, LayoutSlot = "modular-root", LayoutAnchor = anchor,
                    Origin = anchor, InteriorSize = 11, RequiresRoof = false, Completed = true };
                projects.Add(marker);
            }
            var root = marker.LayoutAnchor;
            bool expansionGap(IntVec3 c) => ((c.x - root.x) % Stride + Stride) % Stride >= Size ||
                ((c.z - root.z) % Stride + Stride) % Stride >= Size;
            if (!projects.Any(p => p.Kind == "Recreação inicial"))
            {
                var pin = DefDatabase<ThingDef>.GetNamed("HorseshoesPin");
                var spot = GenRadial.RadialCellsAround(root + new IntVec3(-3, 0, 6), 12, true).Where(c => c.InBounds(map) &&
                    expansionGap(c) && c.GetEdifice(map) == null && GenConstruct.CanPlaceBlueprintAt(pin, c, Rot4.North, map, stuffDef: ThingDefOf.WoodLog) &&
                    !projects.Where(IsModular).Any(p => p.Footprint.Contains(c)))
                    .DefaultIfEmpty(IntVec3.Invalid).First();
                if (spot.IsValid)
                {
                    var recreation = new RoomProject { Kind = "Recreação inicial", LayoutSlot = "starter-joy", Origin = spot,
                        InteriorSize = 1, RequiresRoof = false, Priority = ConstructionPriority.Critical };
                    recreation.Furniture.Add(Thing("HorseshoesPin", spot));
                    var occupied = projects.SelectMany(BaseConstructionManager.Tasks).Where(t => t.Def is ThingDef)
                        .SelectMany(t => GenAdj.OccupiedRect(t.Position, t.Rotation, t.Def.Size)).ToHashSet();
                    var chess = GenRadial.RadialCellsAround(spot + new IntVec3(0, 0, 4), 8, true).Where(c =>
                        new[] { c, c + IntVec3.West, c + IntVec3.East }.All(v => v.InBounds(map) && expansionGap(v) && v.GetEdifice(map) == null &&
                            v != spot && !occupied.Contains(v) && !projects.Where(IsModular).Any(p => p.Footprint.Contains(v))))
                        .DefaultIfEmpty(IntVec3.Invalid).First();
                    if (chess.IsValid)
                    {
                        recreation.Furniture.Add(Thing("ChessTable", chess));
                        recreation.Furniture.Add(Thing("DiningChair", chess + IntVec3.West, Rot4.East));
                        recreation.Furniture.Add(Thing("DiningChair", chess + IntVec3.East, Rot4.West));
                    }
                    projects.Add(recreation);
                }
            }
            foreach (var path in projects.Where(p => p.Kind == "Corredor"))
                path.Furniture.RemoveAll(t => t.Def is TerrainDef floor && !t.Issued && !t.WasCompleted &&
                    !t.Position.GetTerrain(map).affordances.Contains(floor.terrainAffordanceNeeded));
            while (projects.Count(p => p.Kind == "Quarto") < PrisonManager.HousingTarget(map))
                if (!AddRoom(map, projects, root, "Quarto", 1)) return "Sem módulo acessível disponível para novos quartos.";
            foreach (var kind in new[] { "Cozinha", "Abate", "Estoque", "Freezer", "Energia e climatização" })
                if (!projects.Any(p => p.Kind == kind)) AddRoom(map, projects, root, kind, kind == "Estoque" ? 4 : kind == "Freezer" ? 2 : 1);
            bool coreReady = CoreServicesReady(map,projects);
            // Production must not wait for optional dining, hospital or generator modules.
            bool initialReady = InitialCoreReady(map,projects);
            if (initialReady && projects.Any(p => p.Kind == "Pesquisa" && p.Completed) && !projects.Any(p => p.Kind == "Oficina"))
                AddRoom(map, projects, root, "Oficina", 4);
            if (coreReady)
                foreach (var kind in new[] { "Refeitório e recreação", "Hospital" })
                    if (!projects.Any(p => p.Kind == kind)) { AddRoom(map, projects, root, kind, 2); break; }
            if (DefDatabase<ResearchProjectDef>.GetNamed("Fabrication").IsFinished && projects.Any(p => p.Kind == "Oficina" && p.Completed) && !projects.Any(p => p.Kind == "Fabricação"))
                AddRoom(map, projects, root, "Fabricação", 2);
            if (DefDatabase<ResearchProjectDef>.GetNamed("MicroelectronicsBasics").IsFinished && !projects.Any(p => p.Kind == "Laboratório"))
                AddRoom(map, projects, root, "Laboratório", 4);
            ModularInteriorPlanner.Plan(map, projects);
            float demand = projects.SelectMany(BaseConstructionManager.Tasks).Where(t => t.Def is ThingDef)
                .Sum(t => Math.Max(0, ((ThingDef)t.Def).GetCompProperties<CompProperties_Power>()?.PowerConsumption ?? 0));
            int generators = projects.SelectMany(p => p.Furniture).Count(t => t.Def.defName == "WoodFiredGenerator");
            bool runningGenerator = projects.SelectMany(p => p.Furniture).Any(t => t.Def.defName == "WoodFiredGenerator" && t.Complete(map));
            if ((coreReady || runningGenerator) && demand + 200 > generators * 1000 && generators < 6)
                AddRoom(map, projects, root, "Gerador auxiliar", 1);
            // Research is part of the initial core, even without a blocked blueprint.
            if (!projects.Any(p => p.Kind == "Pesquisa"))
                AddRoom(map, projects, root, "Pesquisa", 2);
            var generator = projects.SelectMany(p => p.Furniture).FirstOrDefault(t => t.Def.defName == "WoodFiredGenerator");
            var freezer = projects.FirstOrDefault(p => p.Kind == "Freezer");
            if (generator != null && freezer != null && !projects.Any(p => p.LayoutSlot == "modular-cables"))
            {
                var power = new RoomProject { Kind = "Conexão elétrica", LayoutSlot = "modular-cables", RequiresRoof = false,
                    Origin = generator.Position, InteriorSize = 1, Priority = ConstructionPriority.High };
                var cooler = freezer.Shell.First(t => t.Def.defName == "Cooler");
                for (int x = Math.Min(generator.Position.x, cooler.Position.x); x <= Math.Max(generator.Position.x, cooler.Position.x); x++)
                    power.Furniture.Add(Thing("HiddenConduit", new IntVec3(x, 0, generator.Position.z)));
                for (int z = Math.Min(generator.Position.z, cooler.Position.z); z <= Math.Max(generator.Position.z, cooler.Position.z); z++)
                    if (z != generator.Position.z) power.Furniture.Add(Thing("HiddenConduit", new IntVec3(cooler.Position.x, 0, z)));
                projects.Add(power);
            }
            ModularInteriorPlanner.UpdatePower(map, projects);
            AddFoodGrowing(map, projects, root);
            AddCirculation(map, projects, root);
            return "Módulos 13×13: quartos 5×5, paredes compartilhadas, corredores de 3 células; paredes → pisos → móveis.";
        }
    }
}
