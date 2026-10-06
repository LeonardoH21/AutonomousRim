using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    public static class LootAccessManager
    {
        public const int MaxStacksPerCycle = 2;
        private sealed class Candidate { public Thing Item; public int Priority; public string Reason; }

        public static string Apply(Map map, ColonyState state, bool building, IReadOnlyList<RoomProject> projects,
            List<ManagedApparel> apparel, Func<Pawn, bool> equipmentAllowed, List<Thing> releasedHistory, out int released)
        {
            released = 0;
            releasedHistory.RemoveAll(t => t == null || t.Destroyed);
            if (map.mapPawns.AllPawnsSpawned.Any(p => !p.Dead && !p.Downed && p.HostileTo(Faction.OfPlayer)))
                return "Allow suspenso: há hostis no mapa.";
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(WorkPriorityManager.CanWork).ToList();
            var ground = map.listerThings.AllThings.Where(t => t.Spawned && t.def.category == ThingCategory.Item &&
                t.def.EverHaulable && !(t is Corpse) && !t.Position.Fogged(map) && (t.Faction == null || t.Faction == Faction.OfPlayer)).ToList();
            var humanMeats = new HashSet<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.race?.Humanlike == true)
                .Select(d => d.race.meatDef).Where(d => d != null));
            var eaters = map.mapPawns.FreeColonistsSpawned.Where(p => p.needs?.food != null).ToList();
            TraitDef cannibalTrait = DefDatabase<TraitDef>.GetNamedSilentFail("Cannibal");
            bool acceptsPreparedHumanMeals = eaters.Count > 0 && eaters.All(p => cannibalTrait != null && p.story?.traits?.HasTrait(cannibalTrait) == true ||
                p.Ideo?.HasPrecept(PreceptDefOf.Cannibalism_Preferred) == true || p.Ideo?.HasPrecept(PreceptDefOf.Cannibalism_RequiredStrong) == true ||
                p.Ideo?.HasPrecept(PreceptDefOf.Cannibalism_RequiredRavenous) == true);
            Func<Thing, bool> edible = t => t.def.ingestible?.HumanEdible == true && t.def.ingestible.drugCategory == DrugCategory.None &&
                !humanMeats.Contains(t.def) && (t.TryGetComp<CompIngredients>()?.ingredients.Any(humanMeats.Contains) != true ||
                    acceptsPreparedHumanMeals && t.def.ingestible.IsMeal) &&
                (t.TryGetComp<CompRottable>() == null || t.TryGetComp<CompRottable>().Stage == RotStage.Fresh) &&
                pawns.Any(p => p.needs?.food != null && FoodUtility.WillEat(p, t, p, true, false) &&
                    (p.foodRestriction?.CurrentFoodPolicy == null || p.foodRestriction.CurrentFoodPolicy.filter.Allows(t)));
            float foodNeeded = Math.Max(0f, state.DailyFoodNutrition * ColonyPolicy.TargetFoodDays -
                ground.Where(t => !t.IsForbidden(Faction.OfPlayer) && edible(t)).Sum(t => t.GetStatValue(StatDefOf.Nutrition) * t.stackCount));
            int medicineNeeded = Math.Max(0, state.ColonistCount * 2 - ground.Where(t => t.def.IsMedicine && !t.IsForbidden(Faction.OfPlayer)).Sum(t => t.stackCount));
            var materials = new Dictionary<ThingDef, int>();
            if (building)
            {
                RoomProject next = projects.FirstOrDefault(p => !p.Completed);
                if (next != null && !next.Shell.Concat(next.Furniture).Any(t => t.CancelledByPlayer))
                {
                    Dictionary<ThingDef, int> available = BaseConstructionManager.Available(map);
                    foreach (var cost in next.Shell.Concat(next.Furniture).Where(t => !t.Complete(map) && t.Pending?.Spawned != true)
                        .SelectMany(t => CostListCalculator.CostListAdjusted(t.Def, t.Stuff)).GroupBy(c => c.thingDef))
                    { available.TryGetValue(cost.Key, out int amount); materials[cost.Key] = Math.Max(0, cost.Sum(c => c.count) - amount); }
                    foreach (var deficit in available.Where(p => p.Value < 0))
                        if (!materials.ContainsKey(deficit.Key)) materials[deficit.Key] = -deficit.Value;
                }
                int fuel = projects.SelectMany(p => p.Furniture).Where(t => t.Def.defName == "WoodFiredGenerator" && t.Complete(map))
                    .Select(t => t.Position.GetThingList(map).OfType<ThingWithComps>().First(b => b.def == t.Def).TryGetComp<CompRefuelable>())
                    .Where(c => c != null).Sum(c => c.GetFuelCountToFullyRefuel());
                int wood = ground.Where(t => t.def == ThingDefOf.WoodLog && !t.IsForbidden(Faction.OfPlayer)).Sum(t => t.stackCount);
                if (fuel > wood) { materials.TryGetValue(ThingDefOf.WoodLog, out int construction); materials[ThingDefOf.WoodLog] = Math.Max(construction, fuel - wood); }
            }
            var gear = EquipmentPlanner.Plan(map, apparel, true).Where(d => equipmentAllowed(d.Pawn)).GroupBy(d => d.Pawn)
                .Select(g => g.OrderByDescending(d => d.Gain).First()).Select(d => d.Item).ToHashSet();
            var candidates = ground.Where(t => t.IsForbidden(Faction.OfPlayer) && !releasedHistory.Contains(t)).Select(t =>
            {
                if (medicineNeeded > 0 && t.def.IsMedicine) return new Candidate { Item = t, Priority = 0, Reason = "reserva de remédios" };
                if (foodNeeded > 0f && edible(t)) return new Candidate { Item = t, Priority = 1, Reason = "reserva de comida" };
                if (materials.TryGetValue(t.def, out int needed) && needed > 0) return new Candidate { Item = t, Priority = 2, Reason = "material/combustível necessário" };
                if (gear.Contains(t)) return new Candidate { Item = t, Priority = 3, Reason = "equipamento adequado a um colono" };
                return null;
            }).Where(c => c != null).OrderBy(c => c.Priority).ThenBy(c => pawns.Count == 0 ? float.MaxValue : pawns.Min(p => p.Position.DistanceTo(c.Item.Position)))
                .ThenBy(c => c.Item.thingIDNumber).ToList();
            foreach (Candidate candidate in candidates)
            {
                if (released >= MaxStacksPerCycle) break;
                Thing item = candidate.Item;
                if ((candidate.Priority == 0 && medicineNeeded <= 0) || (candidate.Priority == 1 && foodNeeded <= 0f) ||
                    (candidate.Priority == 2 && materials[item.def] <= 0)) continue;
                if (!pawns.Any(p => p.Position.DistanceTo(item.Position) <= 60f && p.CanReserveAndReach(item, PathEndMode.Touch, Danger.None))) continue;
                item.SetForbidden(false, false);
                releasedHistory.Add(item); released++;
                if (candidate.Priority == 0) medicineNeeded -= item.stackCount;
                if (candidate.Priority == 1) foodNeeded -= item.GetStatValue(StatDefOf.Nutrition) * item.stackCount;
                if (materials.ContainsKey(item.def)) materials[item.def] -= item.stackCount;
                Log.Message($"[AutonomousRim] Allow: {item.LabelCap} — {candidate.Reason}.");
            }
            return $"Allow gradual: {released} pilhas liberadas neste ciclo (máximo 2/10 s de jogo). Reproibições manuais são respeitadas; itens já liberados permanecem disponíveis ao desligar.";
        }
    }
}
