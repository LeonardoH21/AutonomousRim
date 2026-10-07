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
    public static class FoodManager
    {
        public static string Apply(Map map, ColonyState state, List<Pawn> ownedHunts, List<ManagedFoodBill> ownedBills, IReadOnlyList<RoomProject> baseProjects = null, bool survivalOnly = false)
        {
            ownedHunts.RemoveAll(p => p == null || !p.Spawned || p.Dead || map.designationManager.DesignationOn(p, DesignationDefOf.Hunt) == null);
            ownedBills.RemoveAll(b => b?.Bill == null || b.Bill.DeletedOrDereferenced || !b.Matches);
            if (state.HostilePawnCount > 0)
            {
                CancelHunts(map, ownedHunts);
                return "Caça suspensa: há hostis no mapa. Ordens de produção permanecem nas bancadas.";
            }
            string foodStatus = EnsureMeals(map, state, ownedBills);
            StoragePolicy.ManageFoodStorage(map, state, baseProjects);
            if(!survivalOnly)EnsureClothes(map, state, ownedBills);
            bool butcherAvailable = EnsureBill(map, "ButcherCorpseFlesh", state, ownedBills);
            if (state.DailyFoodNutrition <= 0f || state.EstimatedFoodDays >= state.TargetFoodDays)
            {
                CancelHunts(map, ownedHunts);
                return foodStatus + " Reserva de comida suficiente; nenhuma nova caça necessária.";
            }
            if (!butcherAvailable) return foodStatus + " Comida baixa: construa uma mesa ou ponto de abate para aproveitar a caça.";
            var hunters = map.mapPawns.FreeColonistsSpawned.Where(p => WorkPriorityManager.CanHunt(p) &&
                p.workSettings.Initialized && p.workSettings.GetPriority(WorkTypeDefOf.Hunting) > 0).ToList();
            if (hunters.Count == 0) return foodStatus + " Comida baixa: falta um caçador apto, com arma de fogo e trabalho de caça habilitado.";
            int active = map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt).Count();
            if (active >= 2) return foodStatus + $" Caças pendentes: {active}. A IA aguarda antes de marcar novos animais.";
            Pawn center = hunters.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).First();
            var candidates = map.mapPawns.AllPawnsSpawned.Where(p => Eligible(p, map))
                .OrderByDescending(p => p.GetStatValue(StatDefOf.MeatAmount) / (10f + p.Position.DistanceTo(center.Position))).Take(20);
            float pendingNutrition = ownedHunts.Sum(p => Yield(p));
            float deficit = state.DailyFoodNutrition * state.TargetFoodDays - state.FoodNutrition;
            foreach (Pawn prey in candidates)
            {
                if (active >= 2 || pendingNutrition >= deficit) break;
                if (!map.mapPawns.FreeColonistsSpawned.Any(e => e.needs?.food != null && FoodUtility.WillEat(e, prey.RaceProps.meatDef, e, false, false))) continue;
                if (!hunters.Any(h => h.Position.DistanceTo(prey.Position) <= 60f && h.CanReserveAndReach(prey, PathEndMode.Touch, Danger.None))) continue;
                map.designationManager.AddDesignation(new Designation(prey, DesignationDefOf.Hunt));
                ownedHunts.Add(prey);
                pendingNutrition += Yield(prey);
                active++;
            }
            return active > 0 ? foodStatus + $" Caças pendentes: {active}. Seleção limitada a animais selvagens não predadores e sem revanche ao sofrer dano." :
                foodStatus + " Comida baixa: nenhum animal elegível e acessível encontrado. Verifique cultivo e ingredientes.";
        }

        private static bool Eligible(Pawn prey, Map map)
        {
            return prey.RaceProps.Animal && !prey.Dead && !prey.Position.Fogged(map) && !prey.IsForbidden(Faction.OfPlayer) &&
                !prey.InMentalState && prey.RaceProps.meatDef != null &&
                ColonyPolicy.SafePrey(prey.RaceProps.predator, prey.RaceProps.manhunterOnDamageChance,
                    prey.Faction != null, prey.Downed, prey.HostileTo(Faction.OfPlayer)) &&
                map.designationManager.DesignationOn(prey, DesignationDefOf.Hunt) == null;
        }

        private static float Yield(Pawn prey)
        {
            return prey.GetStatValue(StatDefOf.MeatAmount) * prey.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition) * 0.75f;
        }

        private sealed class FoodRecipeChoice
        {
            public RecipeDef Recipe;
            public ThingDef Product;
            public float NutritionPerOutput;
        }

        private static string EnsureMeals(Map map, ColonyState state, List<ManagedFoodBill> ownedBills)
        {
            var tables = map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>()
                .Where(t => !t.IsForbidden(Faction.OfPlayer) && t.def.AllRecipes.Any(IsFoodRecipe)).ToList();
            if (tables.Count == 0)
            {
                state.PreferredMeal = "Nenhuma bancada com receita de comida disponível.";
                state.CookingTargetCount = 0;
                return "Cooking aguardando uma bancada com receita de comida.";
            }
            bool manual = tables.SelectMany(t => t.BillStack.Bills).Any(IsFoodBill) &&
                tables.SelectMany(t => t.BillStack.Bills).Any(b => IsFoodBill(b) && !ownedBills.Any(o => o.Bill == b && o.Matches));
            if (manual)
            {
                state.PreferredMeal = "Bills manuais preservadas.";
                return "Cooking manual detectado; Bills do jogador preservadas.";
            }

            var primary = ChoosePrimaryRecipe(map, state, tables);
            if (primary == null)
            {
                state.PreferredMeal = "Nenhuma receita com ingredientes e dieta compatíveis.";
                state.CookingTargetCount = 0;
                return "Cooking sem receita compatível; aguardando ingredientes, pesquisa ou cozinheiro.";
            }
            string recipeName = primary.Recipe.defName;
            foreach (var old in ownedBills.Where(o => o.Matches && IsFoodBill(o.Bill) && o.Bill.recipe != primary.Recipe && !IsLongLifeRecipe(o.Bill.recipe)).ToList())
            {
                if (map.mapPawns.FreeColonistsSpawned.Any(p => p.CurJob?.bill == old.Bill)) continue;
                if (DeleteBillSafely(old.Bill)) ownedBills.Remove(old);
            }

            int target = DynamicMealTarget(state, primary.NutritionPerOutput);
            state.CookingTargetCount = target;
            state.PreferredMeal = primary.Product?.LabelCap.ToString() ?? primary.Recipe.LabelCap.ToString();
            // Raw rice/meat contributes to survival reserves, but cannot satisfy a
            // bill for cooked meals. Native TargetCount counts the chosen product.
            bool targetReached = map.listerThings.ThingsOfDef(primary.Product)
                .Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer) && !t.IsNotFresh()).Sum(t => t.stackCount) >= target;
            bool excessive = state.FreezerNearFull && targetReached;
            EnsureBill(map, recipeName, state, ownedBills, target);
            foreach (var owned in ownedBills.Where(o => o.Matches && o.Bill.recipe == primary.Recipe))
            {
                owned.Bill.targetCount = target;
                owned.Bill.pauseWhenSatisfied = true;
                owned.Bill.unpauseWhenYouHave = target;
                owned.Bill.suspended = excessive || targetReached;
                owned.Signature = ManagedFoodBill.Describe(owned.Bill);
            }

            EnsureStrategicReserve(map, state, ownedBills, tables);
            string status = $"Cooking: {state.PreferredMeal} até {target}; reserva {state.FoodReserveLevel.ToLowerInvariant()} ({state.EstimatedFoodDays:0.0}/{state.TargetFoodDays:0.0} dias).";
            if (excessive) status += " Produção principal pausada por excesso/capacidade do freezer.";
            else if (targetReached) status += " Alvo atingido; Bill pausada.";
            else if (state.NearSpoilingNutrition > 0f) status += " Processando ingredientes próximos de estragar primeiro.";
            return status;
        }

        private static FoodRecipeChoice ChoosePrimaryRecipe(Map map, ColonyState state, List<Building_WorkTable> tables)
        {
            var candidates = tables.SelectMany(t => t.def.AllRecipes.Where(IsFoodRecipe).Select(r => new { Table = t, Recipe = r }))
                .GroupBy(x => x.Recipe).Select(g => new FoodRecipeChoice
                {
                    Recipe = g.Key,
                    Product = FoodProduct(g.Key),
                    NutritionPerOutput = FoodNutrition(g.Key)
                }).Where(c => c.Product != null && c.NutritionPerOutput > 0f &&
                    ColonistsAccept(map, c.Product) && (c.Recipe.researchPrerequisite == null || c.Recipe.researchPrerequisite.IsFinished) &&
                    (c.Recipe.researchPrerequisites == null || c.Recipe.researchPrerequisites.All(r => r.IsFinished)) &&
                    map.mapPawns.FreeColonistsSpawned.Any(p => WorkPriorityManager.CanWork(p) &&
                        (c.Recipe.skillRequirements == null || c.Recipe.skillRequirements.All(r => p.skills.GetSkill(r.skill).Level >= r.minLevel)))).ToList();
            if (candidates.Count == 0) return null;
            var mealCandidates = candidates.Where(c => c.Product.ingestible?.foodType.HasFlag(FoodTypeFlags.Meal) == true).ToList();
            if (mealCandidates.Count > 0) candidates = mealCandidates;
            var ingredientReady = candidates.Where(c => HasIngredients(map, c.Recipe, tables)).ToList();
            if (ingredientReady.Count > 0) candidates = ingredientReady;
            else return candidates.Where(c => c.Recipe.defName.IndexOf("Simple", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(c => c.Recipe.products.Sum(p => p.count)).FirstOrDefault() ?? candidates.First();
            bool scarce = state.FoodReserveLevel == "Crítica" || state.FoodReserveLevel == "Baixa";
            bool abundant = state.FoodReserveLevel == "Alta" || state.FoodReserveLevel == "Excessiva";
            var cooking = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
            bool skilled = cooking != null && map.mapPawns.FreeColonistsSpawned.Any(p => WorkPriorityManager.CanWork(p) &&
                !p.WorkTypeIsDisabled(cooking) && p.skills.GetSkill(SkillDefOf.Cooking).Level >= 6);
            var simple = candidates.Where(c => c.Recipe.defName.IndexOf("Simple", StringComparison.OrdinalIgnoreCase) >= 0 ||
                c.Recipe.defName.IndexOf("Pemmican", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var fine = candidates.Where(c => c.Recipe.defName.IndexOf("Fine", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var lavish = candidates.Where(c => c.Recipe.defName.IndexOf("Lavish", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (scarce) return simple.OrderByDescending(c => c.Recipe.defName.IndexOf("Bulk", StringComparison.OrdinalIgnoreCase) >= 0).ThenByDescending(c => c.NutritionPerOutput).FirstOrDefault() ?? candidates.OrderBy(c => c.NutritionPerOutput).First();
            if (abundant && skilled && lavish.Count > 0) return lavish.OrderByDescending(c => c.NutritionPerOutput).First();
            if (skilled && fine.Count > 0) return fine.OrderByDescending(c => c.NutritionPerOutput).First();
            return simple.OrderByDescending(c => c.Recipe.defName.IndexOf("Bulk", StringComparison.OrdinalIgnoreCase) >= 0).ThenByDescending(c => c.NutritionPerOutput).FirstOrDefault() ??
                candidates.OrderBy(c => c.NutritionPerOutput).First();
        }

        private static void EnsureStrategicReserve(Map map, ColonyState state, List<ManagedFoodBill> ownedBills, List<Building_WorkTable> tables)
        {
            bool needReserve = state.EstimatedFoodDays >= state.TargetFoodDays &&
                state.LongLifeFoodNutrition / Math.Max(0.01f, state.DailyFoodNutrition) < state.StrategicReserveDays &&
                state.FoodReserveLevel != "Excessiva" && !state.FreezerNearFull;
            var recipe = tables.SelectMany(t => t.def.AllRecipes).FirstOrDefault(r =>
                IsFoodRecipe(r) && IsLongLifeRecipe(r) && HasIngredients(map, r, tables) && ColonistsAccept(map, FoodProduct(r)));
            if (recipe == null) return;
            int target = Math.Max(4, ColonyPolicy.MealTarget(state.DailyFoodNutrition, FoodNutrition(recipe), state.StrategicReserveDays));
            EnsureBill(map, recipe.defName, state, ownedBills, target);
            foreach (var owned in ownedBills.Where(o => o.Matches && o.Bill.recipe == recipe))
            {
                owned.Bill.targetCount = target;
                owned.Bill.pauseWhenSatisfied = true;
                owned.Bill.unpauseWhenYouHave = target;
                owned.Bill.suspended = !needReserve;
                owned.Signature = ManagedFoodBill.Describe(owned.Bill);
            }
            state.StrategicMealTargetCount = target;
        }

        private static int DynamicMealTarget(ColonyState state, float nutritionPerOutput)
        {
            int target = ColonyPolicy.MealTarget(state.DailyFoodNutrition, nutritionPerOutput, state.TargetFoodDays);
            return Math.Min(400, Math.Max(20, target));
        }

        private static bool IsFoodRecipe(RecipeDef recipe) => recipe?.products?.Any(p => p.thingDef?.ingestible?.HumanEdible == true &&
            (p.thingDef.ingestible.IsMeal || IsLongLifeProduct(p.thingDef))) == true;

        private static bool IsFoodBill(Bill bill) => IsFoodRecipe(bill?.recipe);

        private static ThingDef FoodProduct(RecipeDef recipe) => recipe?.products?.FirstOrDefault(p =>
            p.thingDef?.ingestible?.HumanEdible == true && p.thingDef.IsNutritionGivingIngestible)?.thingDef;

        // Do-until-X counts items, even for a four-meal batch.
        private static float FoodNutrition(RecipeDef recipe) => FoodProduct(recipe)?.GetStatValueAbstract(StatDefOf.Nutrition) ?? 0f;

        private static bool IsLongLifeRecipe(RecipeDef recipe) => IsLongLifeProduct(FoodProduct(recipe));

        private static bool IsLongLifeProduct(ThingDef def)
        {
            string name = def?.defName ?? string.Empty;
            return name.IndexOf("Pemmican", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("SurvivalMeal", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ColonistsAccept(Map map, ThingDef product)
        {
            return product != null && map.mapPawns.FreeColonistsSpawned.Where(p => p.needs?.food != null)
                .All(p => FoodUtility.WillEat(p, product, p));
        }

        private static bool HasIngredients(Map map, RecipeDef recipe, List<Building_WorkTable> tables)
        {
            var ingredients = map.listerThings.AllThings.Where(t => t.Spawned && t.def.ingestible?.HumanEdible == true &&
                !t.def.IsCorpse && !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer) && !t.IsNotFresh() &&
                tables.Any(table => table.Position.DistanceTo(t.Position) <= 60f) &&
                map.mapPawns.FreeColonistsSpawned.Any(p => p.CanReach(t, PathEndMode.Touch, Danger.None))).ToList();
            var remaining = ingredients.Where(t => !IsHumanIngredient(t.def)).ToDictionary(t => t, t => t.stackCount);
            foreach (var ingredient in (recipe.ingredients ?? new List<IngredientCount>()).OrderBy(i => i.filter.AllowedDefCount))
            {
                float required = 1f;
                foreach (var t in ingredients.Where(t => remaining.ContainsKey(t) && ingredient.filter.Allows(t)))
                {
                    // The game's count includes the recipe's nutrition-based
                    // ingredient conversion and bulk output requirements.
                    float count = ingredient.CountRequiredOfFor(t.def, recipe);
                    if (count <= 0) continue;
                    int use = Math.Min(remaining[t], (int)Math.Ceiling(required * count));
                    remaining[t] -= use; required -= use / count;
                    if (required <= 0.0001f) break;
                }
                if (required > 0.0001f) return false;
            }
            return true;
        }

        private static bool IsHumanIngredient(ThingDef def) => DefDatabase<ThingDef>.AllDefsListForReading.Any(r =>
            r.race?.Humanlike == true && (r.race.meatDef == def || r.race.corpseDef == def));

        private static void EnsureClothes(Map map, ColonyState state, List<ManagedFoodBill> ownedBills)
        {
            string outer = state.OutdoorTemperature < 10 ? "Apparel_Parka" : "Apparel_Duster";
            foreach (string product in new[] { "Apparel_Pants", "Apparel_CollarShirt", outer })
            {
                RecipeDef recipe = map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>()
                    .Where(t => !t.IsForbidden(Faction.OfPlayer)).SelectMany(t => t.def.AllRecipes).FirstOrDefault(r =>
                        r.products?.Any(p => p.thingDef.defName == product) == true &&
                        (r.researchPrerequisites == null || r.researchPrerequisites.All(p => p.IsFinished)));
                if (recipe != null) EnsureBill(map, recipe.defName, state, ownedBills, 3);
            }
        }

        private static bool EnsureBill(Map map, string recipeName, ColonyState state, List<ManagedFoodBill> ownedBills, int target = 0)
        {
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeName);
            if (recipe == null) return false;
            var tables = map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>()
                .Where(t => t.def.AllRecipes.Contains(recipe) && !t.IsForbidden(Faction.OfPlayer)).ToList();
            if (tables.Count == 0) return false;
            // Only untouched AI bills follow the configured production targets.
            foreach (ManagedFoodBill owned in ownedBills.Where(b => b.Bill.recipe == recipe && b.Matches))
            {
                if (target > 0) { owned.Bill.targetCount = target; owned.Bill.pauseWhenSatisfied = true; owned.Bill.unpauseWhenYouHave = target; }
                owned.Signature = ManagedFoodBill.Describe(owned.Bill);
            }
            // Existing player orders are authoritative; never rewrite their ingredients or counts.
            if (tables.Any(t => t.BillStack.Bills.Any(b => b.recipe == recipe))) return true;
            Building_WorkTable table = tables.First();
            var bill = (Bill_Production)recipe.MakeNewBill();
            bill.repeatMode = target > 0 ? BillRepeatModeDefOf.TargetCount : BillRepeatModeDefOf.Forever;
            if (target > 0) { bill.targetCount = target; bill.pauseWhenSatisfied = true; bill.unpauseWhenYouHave = target; }
            bill.includeTainted = false;
            foreach (ThingDef race in DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.race?.Humanlike == true))
            {
                if (race.race.corpseDef != null) bill.ingredientFilter.SetAllow(race.race.corpseDef, false);
                if (race.race.meatDef != null) bill.ingredientFilter.SetAllow(race.race.meatDef, false);
                if (race.race.leatherDef != null) bill.ingredientFilter.SetAllow(race.race.leatherDef, false);
            }
            table.BillStack.AddBill(bill);
            ownedBills.Add(new ManagedFoodBill { Bill = bill, Signature = ManagedFoodBill.Describe(bill) });
            return true;
        }

        public static void CancelHunts(Map map, List<Pawn> ownedHunts)
        {
            foreach (Pawn prey in ownedHunts)
                if (prey != null && prey.Map == map) map.designationManager.TryRemoveDesignationOn(prey, DesignationDefOf.Hunt);
            ownedHunts.Clear();
        }

        public static void RemoveOwnedBills(List<ManagedFoodBill> ownedBills)
        {
            foreach (ManagedFoodBill owned in ownedBills)
                if (owned != null && owned.Matches) DeleteBillSafely(owned.Bill);
            ownedBills.Clear();
        }

        private static bool DeleteBillSafely(Bill_Production bill)
        {
            var pawns = Find.Maps.SelectMany(m => m.mapPawns.AllPawnsSpawned).Where(p => p.jobs != null).ToList();
            // A player's forced/queued order takes ownership of its bill. Leave it
            // intact rather than canceling that explicit command on automation off.
            if (pawns.Any(p => p.CurJob?.bill == bill && p.CurJob.playerForced ||
                p.jobs.jobQueue.Any(q => q.job.bill == bill && q.job.playerForced))) return false;
            foreach (var pawn in pawns)
            {
                pawn.jobs.jobQueue.RemoveAll(pawn, j => j.bill == bill);
                if (pawn.CurJob?.bill == bill) pawn.jobs.EndCurrentJob(JobCondition.InterruptOptional, startNewJob: false);
            }
            // End jobs while the native bill still exists: their cleanup and saved
            // references must not point to a deleted production order.
            if (!bill.DeletedOrDereferenced) bill.billStack.Delete(bill);
            return true;
        }
    }
}
