using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    public static class FoodManager
    {
        public static string Apply(Map map, ColonyState state, List<Pawn> ownedHunts, List<ManagedFoodBill> ownedBills)
        {
            ownedHunts.RemoveAll(p => p == null || !p.Spawned || p.Dead || map.designationManager.DesignationOn(p, DesignationDefOf.Hunt) == null);
            ownedBills.RemoveAll(b => b?.Bill == null || b.Bill.DeletedOrDereferenced || !b.Matches);
            if (state.HostilePawnCount > 0)
            {
                CancelHunts(map, ownedHunts);
                return "Caça suspensa: há hostis no mapa. Ordens de produção permanecem nas bancadas.";
            }
            EnsureMeals(map, state, ownedBills);
            EnsureClothes(map, state, ownedBills);
            bool butcherAvailable = EnsureBill(map, "ButcherCorpseFlesh", state, ownedBills);
            if (state.DailyFoodNutrition <= 0f || state.EstimatedFoodDays >= ColonyPolicy.TargetFoodDays)
            {
                CancelHunts(map, ownedHunts);
                return "Reserva de comida suficiente; nenhuma nova caça necessária.";
            }
            if (!butcherAvailable) return "Comida baixa: construa uma mesa ou ponto de abate para aproveitar a caça.";
            var hunters = map.mapPawns.FreeColonistsSpawned.Where(p => WorkPriorityManager.CanHunt(p) &&
                p.workSettings.Initialized && p.workSettings.GetPriority(WorkTypeDefOf.Hunting) > 0).ToList();
            if (hunters.Count == 0) return "Comida baixa: falta um caçador apto, com arma de fogo e trabalho de caça habilitado.";
            int active = map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt).Count();
            if (active >= 2) return $"Caças pendentes: {active}. A IA aguarda antes de marcar novos animais.";
            Pawn center = hunters.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).First();
            var candidates = map.mapPawns.AllPawnsSpawned.Where(p => Eligible(p, map))
                .OrderByDescending(p => p.GetStatValue(StatDefOf.MeatAmount) / (10f + p.Position.DistanceTo(center.Position))).Take(20);
            float pendingNutrition = ownedHunts.Sum(p => Yield(p));
            float deficit = state.DailyFoodNutrition * ColonyPolicy.TargetFoodDays - state.FoodNutrition;
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
            return active > 0 ? $"Caças pendentes: {active}. Seleção limitada a animais selvagens não predadores e sem revanche ao sofrer dano." :
                "Comida baixa: nenhum animal elegível e acessível encontrado. Verifique cultivo e ingredientes.";
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

        private static void EnsureMeals(Map map, ColonyState state, List<ManagedFoodBill> ownedBills)
        {
            var tables = map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>()
                .Where(t => !t.IsForbidden(Faction.OfPlayer) && t.def.AllRecipes.Any(r => r.defName == "CookMealSimpleBulk")).ToList();
            if (tables.Count == 0) return;
            bool manual = tables.SelectMany(t => t.BillStack.Bills).Any(b => b.recipe.products?.Any(p => p.thingDef.IsNutritionGivingIngestible &&
                p.thingDef.ingestible.foodType.HasFlag(FoodTypeFlags.Meal)) == true && !ownedBills.Any(o => o.Bill == b && o.Matches));
            if (manual) return;
            var fine = DefDatabase<RecipeDef>.GetNamedSilentFail("CookMealFineBulk");
            var cooking = DefDatabase<WorkTypeDef>.GetNamed("Cooking");
            var ingredients = map.listerThings.AllThings.Where(t => t.Spawned && t.def.ingestible?.HumanEdible == true && !t.def.IsCorpse && !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer) &&
                !t.IsNotFresh() && tables.Any(table => table.Position.DistanceTo(t.Position) <= 60f) &&
                map.mapPawns.FreeColonistsSpawned.Any(p => WorkPriorityManager.CanWork(p) && !p.WorkTypeIsDisabled(cooking) &&
                    p.CanReach(t, PathEndMode.Touch, Danger.None))).ToList();
            bool mix = fine != null && tables.Any(t => t.def.AllRecipes.Contains(fine)) &&
                map.mapPawns.FreeColonistsSpawned.Any(p => WorkPriorityManager.CanWork(p) && !p.WorkTypeIsDisabled(cooking) &&
                    p.workSettings.GetPriority(cooking) > 0 && p.skills.GetSkill(SkillDefOf.Cooking).Level >= 6) &&
                fine.ingredients.All(i => ingredients.Where(t => i.filter.Allows(t) && !IsHumanIngredient(t.def))
                    .Sum(t => t.stackCount * t.GetStatValue(StatDefOf.Nutrition)) >= i.GetBaseCount()) &&
                map.mapPawns.FreeColonistsSpawned.Where(p => p.needs?.food != null).All(p => FoodUtility.WillEat(p, ThingDefOf.MealFine, p));
            string recipeName = mix ? "CookMealFineBulk" : "CookMealSimpleBulk";
            foreach (var old in ownedBills.Where(o => o.Matches && o.Bill.recipe.defName.StartsWith("CookMeal") && o.Bill.recipe.defName != recipeName).ToList())
            {
                if (map.mapPawns.FreeColonistsSpawned.Any(p => p.CurJob?.bill == old.Bill)) return;
                if (!DeleteBillSafely(old.Bill)) return;
                ownedBills.Remove(old);
            }
            EnsureBill(map, recipeName, state, ownedBills, 20);
            // Native bills count their own product. This shared cap also counts the
            // previous meal type when switching between simple and mixed recipes.
            int meals = map.listerThings.AllThings.Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer) &&
                t.def.ingestible?.IsMeal == true && !t.IsNotFresh()).Sum(t => t.stackCount);
            foreach (var owned in ownedBills.Where(o => o.Matches && o.Bill.recipe.defName == recipeName))
            { owned.Bill.suspended = meals >= 20; owned.Signature = ManagedFoodBill.Describe(owned.Bill); }
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
