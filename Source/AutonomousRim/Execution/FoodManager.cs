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
            EnsureBill(map, "CookMealSimple", state, ownedBills);
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

        private static bool EnsureBill(Map map, string recipeName, ColonyState state, List<ManagedFoodBill> ownedBills)
        {
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeName);
            if (recipe == null) return false;
            var tables = map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>()
                .Where(t => t.def.AllRecipes.Contains(recipe) && !t.IsForbidden(Faction.OfPlayer)).ToList();
            if (tables.Count == 0) return false;
            // Only untouched AI bills follow changes in population/food demand.
            foreach (ManagedFoodBill owned in ownedBills.Where(b => b.Bill.recipe == recipe && b.Matches))
            {
                if (recipeName == "CookMealSimple")
                    owned.Bill.targetCount = ColonyPolicy.MealTarget(state.DailyFoodNutrition, ThingDefOf.MealSimple.GetStatValueAbstract(StatDefOf.Nutrition));
                owned.Signature = ManagedFoodBill.Describe(owned.Bill);
            }
            // Existing player orders are authoritative; never rewrite their ingredients or counts.
            if (tables.Any(t => t.BillStack.Bills.Any(b => b.recipe == recipe))) return true;
            Building_WorkTable table = tables.First();
            var bill = (Bill_Production)recipe.MakeNewBill();
            bill.repeatMode = recipeName == "CookMealSimple" ? BillRepeatModeDefOf.TargetCount : BillRepeatModeDefOf.Forever;
            if (recipeName == "CookMealSimple") bill.targetCount = ColonyPolicy.MealTarget(state.DailyFoodNutrition, ThingDefOf.MealSimple.GetStatValueAbstract(StatDefOf.Nutrition));
            foreach (ThingDef race in DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.race?.Humanlike == true))
            {
                if (race.race.corpseDef != null) bill.ingredientFilter.SetAllow(race.race.corpseDef, false);
                if (race.race.meatDef != null) bill.ingredientFilter.SetAllow(race.race.meatDef, false);
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
                if (owned != null && owned.Matches) owned.Bill.billStack.Delete(owned.Bill);
            ownedBills.Clear();
        }
    }
}
