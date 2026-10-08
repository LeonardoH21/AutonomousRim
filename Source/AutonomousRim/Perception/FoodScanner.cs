using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using RimWorld;
using Verse;

namespace AutonomousRim.Perception
{
    public static class FoodScanner
    {
        public static void Scan(Map map, ColonyState state)
        {
            state.PrisonerCount=map.mapPawns.AllPawnsSpawned.Count(p=>p.IsPrisonerOfColony);
            var eaters = map.mapPawns.FreeColonistsSpawned.Concat(map.mapPawns.AllPawnsSpawned.Where(p=>p.IsPrisonerOfColony)).Where(p => p.needs?.food != null).Distinct().ToList();
            var personalSupplies = eaters.ToDictionary(p => p, p => 0f);
            state.TargetFoodDays = FoodReservePolicy.TargetDays(map, eaters.Count);
            state.StrategicReserveDays = Math.Max(1.5f, state.TargetFoodDays * 0.5f);
            foreach (Pawn pawn in eaters)
                state.DailyFoodNutrition += pawn.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed, true) * 60000f;

            var coldCells = FoodStorageCells(map, true);
            var acceptedFoods = new List<Thing>();
            foreach (Thing food in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (!IsUsableFood(food, map) || food.IsForbidden(Faction.OfPlayer)) continue;
                var rottable = food.TryGetComp<CompRottable>();
                bool spoiled = rottable?.Stage == RotStage.Rotting || rottable?.Stage == RotStage.Dessicated;
                bool nearSpoiling = rottable != null && rottable.Stage != RotStage.Fresh;
                float nutrition = food.GetStatValue(StatDefOf.Nutrition) * food.stackCount;
                bool accepted = false;
                foreach (Pawn eater in eaters)
                {
                    if (!FoodUtility.WillEat(eater, food, eater, true, false) ||
                        (eater.foodRestriction?.CurrentFoodPolicy != null && !eater.foodRestriction.CurrentFoodPolicy.filter.Allows(food))) continue;
                    personalSupplies[eater] += nutrition;
                    accepted = true;
                }
                if (!accepted) continue;
                if (nearSpoiling) state.NearSpoilingNutrition += nutrition;
                if (spoiled) continue;
                acceptedFoods.Add(food);
                state.FoodNutrition += nutrition;
                if (food.IsInAnyStorage()) state.StoredFoodItemCount++;
                if (food.def.ingestible?.IsMeal == true) state.StoredMealCount += food.stackCount;
                if (food.TryGetComp<CompRottable>() != null) state.PerishableFoodNutrition += nutrition;
                if (IsLongLife(food.def)) state.LongLifeFoodNutrition += nutrition;
            }

            state.FreezerCapacityCells = coldCells.Count;
            state.FreezerUsedCells = acceptedFoods.Count(food => coldCells.Contains(food.Position));
            state.FreezerFillRatio = state.FreezerCapacityCells > 0
                ? Math.Min(1f, (float)state.FreezerUsedCells / state.FreezerCapacityCells) : 0f;
            state.FreezerNearFull = state.FreezerCapacityCells > 0 && state.FreezerFillRatio >= 0.80f;
            state.EstimatedFoodDays = state.DailyFoodNutrition > 0f ? state.FoodNutrition / state.DailyFoodNutrition : 0f;
            foreach (Pawn eater in eaters)
            {
                float daily = eater.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed, true) * 60000f;
                if (daily > 0f) state.EstimatedFoodDays = Math.Min(state.EstimatedFoodDays, personalSupplies[eater] / daily);
            }
            state.FoodReserveLevel = ReserveLevel(state.EstimatedFoodDays, state.TargetFoodDays);
            state.FoodReserveStatus = ReserveDescription(state);
            state.FoodStorageStatus = StorageDescription(state);
        }

        private static bool IsUsableFood(Thing food, Map map)
        {
            return food != null && food.Spawned && !food.Position.Fogged(map) &&
                food.def.ingestible?.HumanEdible == true && !food.def.IsCorpse &&
                food.def.EverHaulable;
        }

        private static bool IsLongLife(ThingDef def)
        {
            string name = def?.defName ?? string.Empty;
            return name.IndexOf("Pemmican", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("SurvivalMeal", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool AllowsFood(StorageSettings settings)
        {
            if (settings == null) return false;
            if (settings.AllowedToAccept(ThingDefOf.MealSimple)) return true;
            return DefDatabase<ThingDef>.AllDefsListForReading.Any(d =>
                d.ingestible?.HumanEdible == true && !d.IsCorpse && settings.AllowedToAccept(d));
        }

        private static HashSet<IntVec3> FoodStorageCells(Map map, bool coldOnly)
        {
            var cells = new HashSet<IntVec3>();
            foreach (Zone_Stockpile zone in map.zoneManager.AllZones.OfType<Zone_Stockpile>())
            {
                if (!AllowsFood(zone.GetStoreSettings())) continue;
                foreach (IntVec3 cell in zone.Cells)
                    if (!coldOnly || cell.GetTemperature(map) <= 5f) cells.Add(cell);
            }
            foreach (Building_Storage shelf in map.listerBuildings.AllBuildingsColonistOfClass<Building_Storage>())
            {
                if (!AllowsFood(shelf.GetStoreSettings())) continue;
                foreach (IntVec3 cell in GenAdj.OccupiedRect(shelf.Position, shelf.Rotation, shelf.def.Size))
                    if (!coldOnly || cell.GetTemperature(map) <= 5f) cells.Add(cell);
            }
            return cells;
        }

        private static string ReserveLevel(float days, float target)
        {
            if (days < 1f) return "Crítica";
            if (days < target) return "Baixa";
            if (days < target + 2f) return "Normal";
            if (days < target + 5f) return "Alta";
            return "Excessiva";
        }

        private static string ReserveDescription(ColonyState state)
        {
            return $"Reserva {state.FoodReserveLevel.ToLowerInvariant()}: {state.EstimatedFoodDays:0.0} dias disponíveis de {state.TargetFoodDays:0.0} desejados; reserva estratégica longa {state.LongLifeFoodNutrition / Math.Max(0.01f, state.DailyFoodNutrition):0.0} dias.";
        }

        private static string StorageDescription(ColonyState state)
        {
            if (state.FreezerCapacityCells <= 0) return "Nenhuma capacidade refrigerada identificada; priorizar Freezer antes de acumular perecíveis.";
            string fullness = state.FreezerNearFull ? "Freezer quase cheio; prateleiras/expansão e redução de produção serão avaliadas." :
                $"Freezer {state.FreezerFillRatio:P0} ocupado ({state.FreezerUsedCells}/{state.FreezerCapacityCells} células).";
            if (state.NearSpoilingNutrition > 0f) fullness += " Há alimentos próximos de estragar; consumir ou processar primeiro.";
            return fullness;
        }
    }
}
