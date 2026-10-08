using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using AutonomousRim.Execution;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    public static class ExpansionRevisionChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        public static void Run(Map map)
        {
            var emergency = new EmergencyState { Phase = EmergencyPhase.Danger };
            emergency.Update(false, true, 1000); Check(emergency.BlockExpansion, "Securing must remain protected.");
            emergency.Update(false, true, 1600); Check(!emergency.BlockExpansion, "One patient must not block healthy builders in recovery.");
            emergency.Update(true, true, 1601); Check(emergency.BlockExpansion, "New danger must re-enable protection.");
            foreach (string kind in new[] { "Quarto", "Refeitório e recreação", "Hospital", "Oficina", "Fabricação", "Pesquisa", "Laboratório" })
            {
                var room = new RoomProject { Kind = kind, LayoutSlot = "mod:10:10:1", Origin = new IntVec3(30, 0, 30),
                    InteriorSize = kind == "Quarto" ? 5 : 11, InteriorHeight = (kind == "Oficina" || kind == "Laboratório") ? 11 : 5, Completed = true, RequiresRoof = false };
                if (kind == "Quarto" || kind == "Hospital") room.Furniture.Add(new ConstructionTask { Def = ThingDefOf.Bed, Stuff = ThingDefOf.WoodLog, Position = room.Origin + new IntVec3(1, 0, 4) });
                var projects = new List<RoomProject> { room };
                if (kind == "Quarto") projects.Add(new RoomProject { Kind = "Climatização", LayoutSlot = "heat:" + room.LayoutSlot,
                    Furniture = new List<ConstructionTask> { new ConstructionTask { Def = DefDatabase<ThingDef>.GetNamed("Heater"), Position = room.Origin + new IntVec3(1, 0, 1) } } });
                ModularInteriorPlanner.Plan(map, projects);
                var furniture = projects.SelectMany(p => p.Furniture).ToList();
                var occupied = new HashSet<IntVec3>();
                foreach (var task in furniture)
                    foreach (var cell in GenAdj.OccupiedRect(task.Position, task.Rotation, task.Def.Size))
                    { Check(room.Interior.Contains(cell), kind + " furniture outside room: " + task.Def.defName); Check(occupied.Add(cell), kind + " overlapping furniture: " + task.Def.defName); }
                int count = furniture.Count;
                ModularInteriorPlanner.Plan(map, projects);
                Check(projects.SelectMany(p => p.Furniture).Count() == count, "Repeated planning duplicates furnishings.");
                if (kind == "Hospital") Check(projects.Single(p => p.Kind == "Medicamentos").StorageCells.Count == 2, "Hospital must reserve exactly two medicine cells.");
                var light = furniture.Single(t => t.Def.defName == "WallLamp");
                Check(room.Footprint.EdgeCells.Contains(light.Position + IntVec3.North), "Wall lamp has no wall support.");
            }
            Check(!DefenseProductionPlan.Research(map).Any(n => n.StartsWith("Ship")), "Defense checkpoint includes spaceship research.");
            if (DefenseProductionPlan.Stage(map) == 0)
                Check(DefenseProductionPlan.Research(map).Contains("LongBlades"), "Steel sword research missing from first checkpoint.");
            Check(DefenseProductionPlan.Fighters(map).Count(DefenseProductionPlan.Melee) <= 2, "Too many planned interceptors.");
            Log.Message("[AutonomousRim.RevisionTests] PASS: recovery, renewed danger, furniture footprints, idempotence, medicine cells, lamp support, defense research and role limits.");
        }
    }
}
