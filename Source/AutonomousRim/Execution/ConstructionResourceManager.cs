using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Planning;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    public static class ConstructionResourceManager
    {
        public static string Apply(Map map, IReadOnlyList<RoomProject> projects, List<Thing> owned)
        {
            if (map.mapPawns.AllPawnsSpawned.Any(p => !p.Dead && !p.Downed && p.HostileTo(Faction.OfPlayer))) return "Coleta suspensa: hostis.";
            // Migrate only our earlier marks. CutPlant removes vegetation; HarvestPlant
            // is the native Chop wood order that actually collects the tree's yield.
            foreach (var tree in owned.Where(t => t?.Spawned == true && t is Plant p && p.HarvestableNow && p.def.plant.harvestedThingDef == ThingDefOf.WoodLog))
            {
                var removal = map.designationManager.DesignationOn(tree, DesignationDefOf.CutPlant);
                if (removal == null) continue;
                map.designationManager.RemoveDesignation(removal);
                if (map.designationManager.DesignationOn(tree, DesignationDefOf.HarvestPlant) == null)
                    map.designationManager.AddDesignation(new Designation(tree, DesignationDefOf.HarvestPlant));
            }
            owned.RemoveAll(t => t == null || t.Destroyed || !t.Spawned || !Designated(map, t));
            var budget = BaseConstructionManager.Available(map);
            bool essentialsPending = projects.Any(p => !p.Completed && p.Priority <= ConstructionPriority.High && p.State != ConstructionState.Paused);
            var forecast = projects.Where(p => !p.Completed && BaseConstructionManager.Tasks(p).Any() && (p.Priority <= ConstructionPriority.High || !essentialsPending || BaseConstructionManager.CanContinueExistingWork(p)) && p.State != ConstructionState.Paused)
                .OrderBy(p => p.Priority).Take(3).ToList();
            var costs = forecast.SelectMany(p => BaseConstructionManager.CurrentStage(map, p))
                .Where(t => !t.Complete(map) && t.Pending?.Spawned != true)
                .GroupBy(t => new { t.Position, t.Def, t.Stuff, t.Rotation }).Select(g => g.First()).SelectMany(t => CostListCalculator.CostListAdjusted(t.Def, t.Stuff))
                .GroupBy(c => c.thingDef).ToDictionary(g => g.Key, g => g.Sum(c => c.count));
            foreach (var material in costs.Keys.ToList())
                if (forecast.Any(p => p.Priority > ConstructionPriority.High && p.Shell.All(t => t.Complete(map)) &&
                    BaseConstructionManager.CurrentStage(map, p).Any(t => !t.Complete(map) && CostListCalculator.CostListAdjusted(t.Def, t.Stuff).Any(c => c.thingDef == material))))
                    costs[material] += material == ThingDefOf.WoodLog ? 60 : material == ThingDefOf.Steel ? 50 : material == ThingDefOf.ComponentIndustrial ? 2 : 0;
            foreach (var shortage in budget.Where(p => p.Value < 0)) { costs.TryGetValue(shortage.Key, out int required); costs[shortage.Key] = Math.Max(required, -shortage.Value); }
            int fuel = projects.SelectMany(BaseConstructionManager.Tasks).Where(t => t.Def.defName == "WoodFiredGenerator" && t.Complete(map))
                .Select(t => t.Position.GetThingList(map).OfType<ThingWithComps>().First(b => b.def == t.Def).TryGetComp<CompRefuelable>()).Where(c => c != null)
                .Sum(c => c.GetFuelCountToFullyRefuel());
            if (fuel > 0) { costs.TryGetValue(ThingDefOf.WoodLog, out int timber); costs[ThingDefOf.WoodLog] = timber + fuel; }
            int marked = 0;
            foreach (var cost in costs)
            {
                budget.TryGetValue(cost.Key, out int amount);
                int need = Math.Min(600, cost.Value - Math.Max(0, amount));
                if (need <= 0) continue;
                bool wood = cost.Key == ThingDefOf.WoodLog;
                WorkTypeDef work = wood ? WorkTypeDefOf.PlantCutting : WorkTypeDefOf.Mining;
                var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => WorkPriorityManager.CanWork(p) && p.workSettings.Initialized && !p.WorkTypeIsDisabled(work) && p.workSettings.GetPriority(work) > 0).ToList();
                if (pawns.Count == 0) continue;
                var sources = map.listerThings.AllThings.Where(t => t.Spawned && !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer) &&
                    (wood ? t is Plant plant && plant.def.plant.harvestedThingDef == cost.Key && plant.HarvestableNow : t.def.building?.mineableThing == cost.Key))
                    .OrderBy(t => pawns.Min(p => p.Position.DistanceToSquared(t.Position))).ToList();
                int pending = sources.Where(t => Designated(map, t))
                    .Sum(t => wood ? Math.Max(1, (int)t.def.plant.harvestYield) : Math.Max(1, t.def.building.mineableYield));
                need -= pending;
                foreach (var source in sources)
                {
                    if (need <= 0 || marked >= 4 || owned.Count >= 16) break;
                    if (Designated(map, source) || map.designationManager.AllDesignationsOn(source).Any() || !pawns.Any(p => p.Position.DistanceTo(source.Position) <= 60 &&
                        source.Position.IsInAllowedArea(p) && p.CanReach(source, PathEndMode.Touch, Danger.None))) continue;
                    map.designationManager.AddDesignation(wood ? new Designation(source, DesignationDefOf.HarvestPlant) : new Designation(source.Position, DesignationDefOf.Mine));
                    owned.Add(source); marked++;
                    need -= wood ? Math.Max(1, (int)source.def.plant.harvestYield) : Math.Max(1, source.def.building.mineableYield);
                }
            }
            return $"Coleta nativa: {marked} novas marcações; {owned.Count}/16 pendentes. Corte/mineração respeitam trabalho, acesso e recursos reais.";
        }
        public static void Stop(Map map, List<Thing> owned)
        {
            foreach (var t in owned.Where(t => t?.Spawned == true))
                foreach (var d in map.designationManager.AllDesignationsOn(t).Where(d => d.def == DesignationDefOf.CutPlant || d.def == DesignationDefOf.HarvestPlant || d.def == DesignationDefOf.Mine).ToList()) map.designationManager.RemoveDesignation(d);
            foreach (var t in owned.Where(t => t?.Spawned == true))
            { var d = map.designationManager.DesignationAt(t.Position, DesignationDefOf.Mine); if (d != null) map.designationManager.RemoveDesignation(d); }
            owned.Clear();
        }
        private static bool Designated(Map map, Thing t) => map.designationManager.DesignationOn(t, DesignationDefOf.CutPlant) != null || map.designationManager.DesignationOn(t, DesignationDefOf.HarvestPlant) != null ||
            map.designationManager.DesignationAt(t.Position, DesignationDefOf.Mine) != null;
    }
}
