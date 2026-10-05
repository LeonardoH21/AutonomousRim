using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;

namespace AutonomousRim.Perception
{
    public static class FoodScanner
    {
        public static void Scan(Map map, ColonyState state)
        {
            var eaters = map.mapPawns.FreeColonistsSpawned.Where(p => p.needs?.food != null).ToList();
            var personalSupplies = eaters.ToDictionary(p => p, p => 0f);
            foreach (Pawn pawn in eaters)
                state.DailyFoodNutrition += pawn.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed, true) * 60000f;
            foreach (Thing food in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (food.def.ingestible == null || !food.def.ingestible.HumanEdible || food.IsForbidden(Faction.OfPlayer) ||
                    food.Position.Fogged(map) || !food.IsInAnyStorage()) continue;
                if (food.TryGetComp<CompRottable>()?.Stage == RotStage.Rotting || food.TryGetComp<CompRottable>()?.Stage == RotStage.Dessicated) continue;
                bool accepted = false;
                float nutrition = food.GetStatValue(StatDefOf.Nutrition) * food.stackCount;
                foreach (Pawn eater in eaters)
                {
                    if (!FoodUtility.WillEat(eater, food, eater, true, false) ||
                        (eater.foodRestriction?.CurrentFoodPolicy != null && !eater.foodRestriction.CurrentFoodPolicy.filter.Allows(food))) continue;
                    personalSupplies[eater] += nutrition;
                    accepted = true;
                }
                if (accepted) state.FoodNutrition += nutrition;
            }
            state.EstimatedFoodDays = state.DailyFoodNutrition > 0f ? state.FoodNutrition / state.DailyFoodNutrition : 0f;
            foreach (Pawn eater in eaters)
            {
                float daily = eater.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed, true) * 60000f;
                if (daily > 0f) state.EstimatedFoodDays = System.Math.Min(state.EstimatedFoodDays, personalSupplies[eater] / daily);
            }
        }
    }
}
