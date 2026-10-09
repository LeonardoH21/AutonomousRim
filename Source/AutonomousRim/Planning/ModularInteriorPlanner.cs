using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Execution;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    // Finishing has its own project: research for a dresser must not block a habitable bedroom.
    public static class ModularInteriorPlanner
    {
        private static bool LampSupport(Map map, RoomProject room, IntVec3 cell)
        {
            if(!cell.InBounds(map))return false;
            var actual=cell.GetEdifice(map);
            if(actual!=null && actual.def.building?.supportsWallAttachments==true && actual.def.Fillage==FillCategory.Full)return true;
            if(actual!=null && !(actual is Mineable))return false;
            return room.Shell.Any(t=>t.Def==ThingDefOf.Wall && !t.CancelledByPlayer && t.Position==cell);
        }
        public static void Plan(Map map, List<RoomProject> projects)
        {
            foreach (var room in projects.Where(p => ModularBasePlanner.IsModular(p) && p.Crop == null && p.Completed).ToList())
            {
                string slot = "interior:" + room.LayoutSlot;
                if (projects.Any(p => p.LayoutSlot == slot) || room.State == ConstructionState.Paused) continue;
                var finish = new RoomProject { Kind = "Acabamento", LayoutSlot = slot, Origin = room.Origin,
                    InteriorSize = room.InteriorSize, InteriorHeight = room.Height, RequiresRoof = false,
                    Priority = room.Kind == "Oficina" || room.Kind == "Fabricação" || room.Kind == "Laboratório" ? ConstructionPriority.High : ConstructionPriority.Normal };
                void add(string def, int x, int z, Rot4? rot = null)
                {
                    var task = ModularBasePlanner.Thing(def, room.Origin + new IntVec3(x, 0, z), rot);
                    var occupied = projects.SelectMany(p => p.Furniture).Concat(finish.Furniture)
                        .Where(t => t.Def is ThingDef && !t.Def.defName.Contains("Conduit"))
                        .SelectMany(t => GenAdj.OccupiedRect(t.Position, t.Rotation, t.Def.Size)).ToHashSet();
                    var free = room.Interior.Cells.OrderBy(c => c.DistanceToSquared(task.Position)).Where(c =>
                        GenAdj.OccupiedRect(c, task.Rotation, task.Def.Size).All(n => room.Interior.Contains(n) &&
                            !occupied.Contains(n) && n.GetEdifice(map) == null) &&
                        (def != "WallLamp" || room.Footprint.EdgeCells.Contains(c + IntVec3.North) && LampSupport(map,room,c + IntVec3.North)))
                        .DefaultIfEmpty(IntVec3.Invalid).First();
                    if (free.IsValid) { task.Position = free; finish.Furniture.Add(task); }
                }
                // North-facing wall lamps occupy the cell immediately south of their supporting wall.
                add("WallLamp", room.InteriorSize - 1, room.Height);
                if (room.Kind == "Quarto")
                {
                    add("EndTable", 2, 5); // Adjacent to the existing single bed head.
                    add("Dresser", 4, 3);
                    add("PlantPot", 1, 1);
                }
                if (room.Kind == "Refeitório e recreação")
                {
                    add("Table3x3c", 6, 3);
                    for(int z=2;z<=4;z++){add("DiningChair",4,z,Rot4.East);add("DiningChair",8,z,Rot4.West);}
                    for(int x=5;x<=7;x++){add("DiningChair",x,1,Rot4.North);add("DiningChair",x,5,Rot4.South);}
                    add("ChessTable", 2, 3);
                    add("DiningChair", 1, 3, Rot4.East); add("DiningChair", 3, 3, Rot4.West);
                    add("PlantPot", 1, 5);
                }
                if (room.Kind == "Hospital")
                {
                    add("Bed", 4, 4); finish.Furniture.Last().MedicalBed = true;
                    projects.Add(new RoomProject { Kind = "Medicamentos", LayoutSlot = "medicine:" + room.LayoutSlot,
                        Origin = room.Origin, InteriorSize = 1, RequiresRoof = false, Priority = ConstructionPriority.High,
                        StorageCells = new List<IntVec3> { room.Origin + new IntVec3(9, 0, 4), room.Origin + new IntVec3(10, 0, 4) } });
                }
                if (room.Kind == "Laboratório")
                {
                    add("HiTechResearchBench", 3, 8); add("HiTechResearchBench", 9, 8); add("MultiAnalyzer", 6, 6);
                }
                if (room.Kind == "Fabricação") add("FabricationBench", 3, 3);
                if (room.Kind == "Oficina")
                {
                    add("HandTailoringBench", 2, 8); add("FueledSmithy", 6, 8);
                    add("TableMachining", 10, 8); add("TableStonecutter", 2, 3);
                    add("CraftingSpot", 6, 3);
                }
                projects.Add(finish);
            }
            foreach (var room in projects.Where(p => ModularBasePlanner.IsModular(p) && p.Completed).ToList())
            {
                string name = room.Kind == "Energia e climatização" ? "Battery" : null;
                if (name == null || !DefDatabase<ThingDef>.GetNamed(name).IsResearchFinished || projects.Any(p => p.LayoutSlot == "upgrade:" + room.LayoutSlot)) continue;
                var upgrade = new RoomProject { Kind = "Infraestrutura", LayoutSlot = "upgrade:" + room.LayoutSlot,
                    Origin = room.Origin, InteriorSize = room.InteriorSize, InteriorHeight = room.Height, RequiresRoof = false, Priority = ConstructionPriority.High };
                upgrade.Furniture.Add(ModularBasePlanner.Thing(name, room.Origin + (name == "Battery" ? new IntVec3(1, 0, 1) : new IntVec3(9, 0, 1))));
                if (name == "Battery") upgrade.Furniture.Add(ModularBasePlanner.Thing(name, room.Origin + new IntVec3(5, 0, 1)));
                else upgrade.Furniture.Add(ModularBasePlanner.Thing("MultiAnalyzer", room.Origin + new IntVec3(6, 0, 4)));
                projects.Add(upgrade);
            }
            // Only consider room heating after nine genuinely enclosed, roofed rooms exist.
            var closed = projects.Where(p => ModularBasePlanner.IsModular(p) && p.Crop == null && p.RequiresRoof &&
                p.Shell.All(t => t.Complete(map)) && p.RoofArea.All(c => c.Roofed(map))).ToList();
            if (closed.Count >= 9 && map.mapTemperature.OutdoorTemp < 16)
                foreach (var room in closed.Where(p => p.Kind != "Freezer" && p.Kind != "Energia e climatização"))
                {
                    string slot = "heat:" + room.LayoutSlot;
                    if (projects.Any(p => p.LayoutSlot == slot)) continue;
                    var occupied = projects.SelectMany(p => p.Furniture).Where(t => t.Def is ThingDef)
                        .SelectMany(t => GenAdj.OccupiedRect(t.Position, t.Rotation, t.Def.Size)).ToHashSet();
                    var cell = room.Interior.Cells.Where(c => !occupied.Contains(c) && c.GetEdifice(map) == null &&
                        !room.Shell.Any(t => t.Def == ThingDefOf.Door && c.AdjacentToCardinal(t.Position)))
                        .DefaultIfEmpty(IntVec3.Invalid).First();
                    if (!cell.IsValid) continue;
                    var heat = new RoomProject { Kind = "Climatização", LayoutSlot = slot, Origin = room.Origin,
                        InteriorSize = 1, RequiresRoof = false, Priority = ConstructionPriority.Normal };
                    heat.Furniture.Add(ModularBasePlanner.Thing("Heater", cell));
                    heat.Furniture[0].TargetTemperature = 21;
                    projects.Add(heat);
                }
            // Keep built furniture intact when migrating finishing plans from older saves.
            foreach (var finish in projects.Where(p => p.LayoutSlot?.StartsWith("interior:") == true).ToList())
            {
                var room = projects.FirstOrDefault(p => "interior:" + p.LayoutSlot == finish.LayoutSlot);
                if (room == null) continue;
                if (room.Kind == "Oficina" || room.Kind == "Fabricação" || room.Kind == "Laboratório") finish.Priority = ConstructionPriority.High;
                foreach (var task in finish.Furniture.Where(t => !t.Issued && !t.WasCompleted && !t.CancelledByPlayer))
                {
                    var occupied = projects.SelectMany(p => p.Furniture).Where(t => t != task && t.Def is ThingDef &&
                        !t.Def.defName.Contains("Conduit")).SelectMany(t => GenAdj.OccupiedRect(t.Position, t.Rotation, t.Def.Size)).ToHashSet();
                    if (!GenAdj.OccupiedRect(task.Position, task.Rotation, task.Def.Size).Any(occupied.Contains) &&
                        (task.Def.defName!="WallLamp" || LampSupport(map,room,task.Position+task.Rotation.FacingCell))) continue;
                    var free = room.Interior.Cells.OrderBy(c => c.DistanceToSquared(task.Position)).Where(c =>
                        GenAdj.OccupiedRect(c, task.Rotation, task.Def.Size).All(n => room.Interior.Contains(n) && !occupied.Contains(n) && n.GetEdifice(map) == null) &&
                        (task.Def.defName != "WallLamp" || room.Footprint.EdgeCells.Contains(c + IntVec3.North) && LampSupport(map,room,c + IntVec3.North)))
                        .DefaultIfEmpty(IntVec3.Invalid).First();
                    if (free.IsValid) task.Position = free;
                }
            }
            // Migrate unissued heaters from older plans if a later storage upgrade claimed the cell.
            foreach (var heat in projects.Where(p => p.LayoutSlot?.StartsWith("heat:") == true).ToList())
            {
                var room = projects.FirstOrDefault(p => "heat:" + p.LayoutSlot == heat.LayoutSlot);
                if (room == null) continue;
                var occupied = projects.Where(p => p != heat).SelectMany(p => p.Furniture)
                    .Where(t => t.Def is ThingDef && !t.Def.defName.Contains("Conduit"))
                    .SelectMany(t => GenAdj.OccupiedRect(t.Position, t.Rotation, t.Def.Size)).ToHashSet();
                foreach (var task in heat.Furniture.Where(t => !t.Issued && !t.WasCompleted && !t.CancelledByPlayer && occupied.Contains(t.Position)))
                {
                    var free = room.Interior.Cells.Where(c => !occupied.Contains(c) && c.GetEdifice(map) == null &&
                        !room.Shell.Any(t => t.Def == ThingDefOf.Door && c.AdjacentToCardinal(t.Position)))
                        .DefaultIfEmpty(IntVec3.Invalid).First();
                    if (free.IsValid) { task.Position = free; occupied.Add(free); }
                }
            }
        }

        public static void UpdatePower(Map map, List<RoomProject> projects)
        {
            var power = projects.FirstOrDefault(p => p.LayoutSlot == "modular-cables");
            if (power == null) return;
            var generator = projects.SelectMany(p => p.Furniture).FirstOrDefault(t => t.Def.defName == "WoodFiredGenerator");
            if (generator == null) return;
            var generators = projects.SelectMany(p => p.Furniture).Where(t => t.Def.defName == "WoodFiredGenerator")
                .SelectMany(t => GenAdj.OccupiedRect(t.Position, t.Rotation, t.Def.Size)).ToHashSet();
            foreach (var old in power.Furniture.Where(t => generators.Contains(t.Position)).ToList())
            {
                if (old.Owned && old.Pending is Blueprint_Build blueprint && blueprint.Spawned && BaseConstructionManager.Matches(old, blueprint)) blueprint.Destroy(DestroyMode.Cancel);
                power.Furniture.Remove(old);
            }
            var walls = projects.SelectMany(p => p.Shell).Where(t => t.Def == ThingDefOf.Wall).Select(t => t.Position).ToHashSet();
            string cable(IntVec3 c) => walls.Contains(c) || c.GetEdifice(map)?.def == ThingDefOf.Wall ? "PowerConduit" : "HiddenConduit";
            var cells = power.Furniture.Select(t => t.Position).Concat(generators).ToHashSet();
            // Auxiliary generators must join the main grid. Their occupied cells
            // alone are separate islands, not usable branch origins for the grid.
            var connected = new HashSet<IntVec3> { generator.Position };
            void flood()
            {
                var queue = new Queue<IntVec3>(connected);
                while (queue.Count > 0)
                {
                    var at = queue.Dequeue();
                    foreach (var next in GenAdj.CardinalDirections.Select(d => at + d))
                        if (cells.Contains(next) && connected.Add(next)) queue.Enqueue(next);
                }
            }
            flood();
            foreach (var source in projects.SelectMany(p => p.Furniture).Where(t => t.Def.defName == "WoodFiredGenerator").ToList())
            {
                if (connected.Contains(source.Position)) continue;
                var start = connected.OrderBy(c => c.DistanceToSquared(source.Position)).First();
                var branch = new List<IntVec3>();
                for (int x = Math.Min(start.x, source.Position.x); x <= Math.Max(start.x, source.Position.x); x++) branch.Add(new IntVec3(x, 0, start.z));
                for (int z = Math.Min(start.z, source.Position.z); z <= Math.Max(start.z, source.Position.z); z++) branch.Add(new IntVec3(source.Position.x, 0, z));
                foreach (var c in branch.Where(c => cells.Add(c)))
                { power.Furniture.Add(ModularBasePlanner.Thing(cable(c), c)); power.Completed = false; }
                flood();
            }
            foreach (var consumer in projects.SelectMany(p => p.Furniture).Where(t => t.Def is ThingDef d &&
                d.comps?.OfType<CompProperties_Power>().Any(c => c.PowerConsumption > 0) == true).ToList())
            {
                var dest = consumer.Position;
                // Connect to the existing network rather than drawing every branch back to the generator.
                var start = cells.OrderBy(c => c.DistanceToSquared(dest)).DefaultIfEmpty(generator.Position).First();
                var branch = new List<IntVec3>();
                for (int x = Math.Min(start.x, dest.x); x <= Math.Max(start.x, dest.x); x++) branch.Add(new IntVec3(x, 0, start.z));
                for (int z = Math.Min(start.z, dest.z); z <= Math.Max(start.z, dest.z); z++) branch.Add(new IntVec3(dest.x, 0, z));
                foreach (var c in branch.Where(c => cells.Add(c)))
                { power.Furniture.Add(ModularBasePlanner.Thing(cable(c), c)); power.Completed = false; }
            }
            foreach (var t in power.Furniture.Where(t => !t.Issued && !t.WasCompleted && !t.CancelledByPlayer && !t.Complete(map)))
                t.Def = DefDatabase<ThingDef>.GetNamed(cable(t.Position));
        }
    }
}
