using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Planning;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    public static class BaseConstructionManager
    {
        public const int MaxPending = 12;
        public const int MaxPerCycle = 6;

        private static bool Matches(ConstructionTask task, Thing thing)
        {
            ThingDef material = thing is Blueprint blueprint ? blueprint.EntityToBuildStuff() :
                thing is Frame frame ? frame.EntityToBuildStuff() : thing.Stuff;
            return thing.def.entityDefToBuild == task.Def && thing.Position == task.Position &&
                thing.Rotation == task.Rotation && material == task.Stuff && thing.Faction == Faction.OfPlayer;
        }

        private static void Track(Map map, ConstructionTask task)
        {
            if (task.Def is TerrainDef && task.OriginalTerrain != null && !task.Complete(map) && task.Position.GetTerrain(map) != task.OriginalTerrain)
                task.CancelledByPlayer = true;
            if (task.Complete(map) || !task.Issued) return;
            if (task.Pending?.Spawned == true)
            {
                if (!Matches(task, task.Pending)) task.CancelledByPlayer = true;
                return;
            }
            Thing frame = task.Position.GetThingList(map).FirstOrDefault(t => t is Frame &&
                Matches(task, t));
            if (frame != null) task.Pending = frame;
            else { task.Pending = null; task.CancelledByPlayer = true; }
        }

        public static Dictionary<ThingDef, int> Available(Map map)
        {
            var budget = map.listerThings.AllThings.Where(t => t.def.category == ThingCategory.Item && !t.IsForbidden(Faction.OfPlayer) && !t.Position.Fogged(map))
                .GroupBy(t => t.def).ToDictionary(g => g.Key, g => g.Sum(t => t.stackCount));
            // Reserve material for all player/AI blueprints and frames before opening more work.
            foreach (Thing pending in map.listerThings.AllThings.Where(t => t is Blueprint_Build || t is Frame))
            {
                List<ThingDefCountClass> costs = pending is Frame frame ? frame.TotalMaterialCost() : ((Blueprint_Build)pending).TotalMaterialCost();
                foreach (ThingDefCountClass cost in costs)
                {
                    int needed = pending is Frame f ? f.ThingCountNeeded(cost.thingDef) : cost.count;
                    budget.TryGetValue(cost.thingDef, out int held);
                    budget[cost.thingDef] = held - needed;
                }
            }
            return budget;
        }

        public static string Apply(Map map, List<RoomProject> projects)
        {
            if (map.mapPawns.AllPawnsSpawned.Any(p => !p.Dead && !p.Downed && p.HostileTo(Faction.OfPlayer))) return "Construção suspensa: há hostis no mapa.";
            if (!map.mapPawns.FreeColonistsSpawned.Any(p => WorkPriorityManager.CanWork(p) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction) &&
                p.workSettings?.Initialized == true && p.workSettings.GetPriority(WorkTypeDefOf.Construction) > 0))
                return "Construção aguardando um colono apto com trabalho Construção habilitado.";
            foreach (RoomProject room in projects)
                foreach (ConstructionTask task in room.Shell.Concat(room.Furniture))
                {
                    Track(map, task);
                    if (!task.TemperatureConfigured && task.TargetTemperature > -999f && task.Complete(map))
                    {
                        ThingWithComps building = task.Position.GetThingList(map).OfType<ThingWithComps>().First(t => t.def == task.Def);
                        CompTempControl thermostat = building.TryGetComp<CompTempControl>();
                        if (thermostat != null) thermostat.TargetTemperature = task.TargetTemperature;
                        task.TemperatureConfigured = true; // Player changes afterwards remain untouched.
                    }
                }
            RoomProject project = projects.FirstOrDefault(p => !p.Completed);
            if (project == null) return "Módulos concluídos. " + CompactBasePlanner.ClimateSummary(map) + " Freezer, plantações, hospital, craft e defesas ainda aguardam implementação.";
            if (project.Shell.Concat(project.Furniture).Any(t => t.CancelledByPlayer)) return $"{project.Kind}: projeto pausado após cancelamento/alteração de uma obra.";
            bool shellReady = project.Shell.All(t => t.Complete(map));
            if (shellReady && project.RequiresRoof)
            {
                foreach (IntVec3 cell in project.Interior)
                {
                    if (map.areaManager.NoRoof[cell]) return $"{project.Kind}: cobertura bloqueada por área Sem teto do jogador.";
                    if (!cell.Roofed(map) && !map.areaManager.BuildRoof[cell])
                    {
                        map.areaManager.BuildRoof[cell] = true; project.RoofOrders.Add(cell);
                    }
                }
            }
            if (shellReady && project.Furniture.All(t => t.Complete(map)) && (!project.RequiresRoof || project.Interior.All(c => c.Roofed(map))))
            {
                if (project.Kind == "Estoque" && project.Stockpile == null)
                {
                    if (project.Interior.Any(c => c.GetZone(map) != null)) return "Estoque: área ocupada por zona do jogador; nenhuma zona substituída.";
                    project.Stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
                    map.zoneManager.RegisterZone(project.Stockpile);
                    var doorCells = project.Shell.Where(t => t.Def == ThingDefOf.Door).Select(t => t.Position).ToList();
                    foreach (IntVec3 cell in project.Interior.Where(c => !doorCells.Any(d => c.AdjacentToCardinal(d)))) project.Stockpile.AddCell(cell);
                    project.Stockpile.GetStoreSettings().filter.SetAllow(ThingCategoryDefOf.Corpses, false);
                    ThingDef chemfuel = DefDatabase<ThingDef>.GetNamedSilentFail("Chemfuel");
                    if (chemfuel != null) project.Stockpile.GetStoreSettings().filter.SetAllow(chemfuel, false);
                    ThingCategoryDef shells = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("MortarShells");
                    if (shells != null) project.Stockpile.GetStoreSettings().filter.SetAllow(shells, false);
                }
                project.Completed = true;
                return $"{project.Kind} concluído: estrutura, móveis e cobertura verificados.";
            }
            var remaining = project.Shell.Concat(project.Furniture).Where(t => !t.Complete(map) && t.Pending?.Spawned != true).ToList();
            var costsLeft = remaining.SelectMany(t => CostListCalculator.CostListAdjusted(t.Def, t.Stuff)).GroupBy(c => c.thingDef)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.count));
            Dictionary<ThingDef, int> budget = Available(map);
            foreach (var cost in costsLeft)
                if (!budget.TryGetValue(cost.Key, out int amount) || amount < cost.Value)
                    return $"{project.Kind}: faltam {cost.Value - System.Math.Max(0, amount)} {cost.Key.label}. Reserva considera outras obras; etapa não duplicada.";
            int pendingCount = projects.Sum(p => p.Shell.Concat(p.Furniture).Count(t => t.Pending?.Spawned == true));
            int issued = 0;
            foreach (ConstructionTask task in (shellReady ? project.Furniture : project.Shell).Where(t => !t.Complete(map) && t.Pending?.Spawned != true))
            {
                if (issued >= MaxPerCycle || pendingCount >= MaxPending) break;
                if (task.Def.researchPrerequisites?.Any(r => !r.IsFinished) == true) return $"{project.Kind}: pesquisa pendente para {task.Def.label}.";
                if (!GenConstruct.CanPlaceBlueprintAt(task.Def, task.Position, task.Rotation, map, stuffDef: task.Stuff)) return $"{project.Kind}: local de {task.Def.label} bloqueado; nenhuma estrutura removida.";
                task.Pending = GenConstruct.PlaceBlueprintForBuild(task.Def, task.Position, map, task.Rotation, Faction.OfPlayer, task.Stuff);
                task.Issued = true; project.Started = true; issued++; pendingCount++;
            }
            return $"{project.Kind}: {issued} projetos emitidos; {pendingCount} obras pendentes. {(shellReady ? "Móveis e teto" : "Paredes e porta")}.";
        }

        public static void Stop(Map map, List<RoomProject> projects)
        {
            foreach (RoomProject project in projects)
            {
                foreach (ConstructionTask task in project.Shell.Concat(project.Furniture))
                {
                    Track(map, task);
                    if (task.Pending is Blueprint_Build bp && bp.Spawned && Matches(task, bp) && !task.CancelledByPlayer)
                    {
                        bp.Destroy(DestroyMode.Cancel); task.Pending = null; task.Issued = false;
                    }
                    // Preserve frames that already consumed resources and all completed structures.
                }
                foreach (IntVec3 cell in project.RoofOrders)
                    if (!cell.Roofed(map)) map.areaManager.BuildRoof[cell] = false;
                project.RoofOrders.Clear();
            }
        }
    }
}
