using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using RimWorld;
using UnityEngine;
using Verse;

namespace AutonomousRim.Planning
{
    public static class DefenseProductionPlan
    {
        public static List<Pawn> Fighters(Map map) => map.mapPawns.AllPawnsSpawned.Where(p =>
            p.IsFreeColonist && !p.Dead && p.ageTracker.AgeBiologicalYears >= 18 && !p.WorkTagIsDisabled(WorkTags.Violent)).ToList();
        public static bool Melee(Pawn pawn)
        {
            if (pawn.Map == null) return TraitAnalyzer.PreferMelee(pawn);
            var fighters = Fighters(pawn.Map);
            return fighters.OrderByDescending(p => TraitAnalyzer.HasActiveTrait(p, "Brawler") ? 100 : 0)
                .ThenByDescending(p => (p.skills?.GetSkill(SkillDefOf.Melee).Level ?? 0) - (p.skills?.GetSkill(SkillDefOf.Shooting).Level ?? 0))
                .ThenBy(p => p.thingIDNumber).Take(Math.Min(2, (fighters.Count + 1) / 2)).Contains(pawn);
        }
        public static bool Usable(Thing t) => (!(t is Apparel a) || !a.WornByCorpse) &&
            (!t.def.useHitPoints || t.HitPoints > t.MaxHitPoints * 0.51f);
        public static bool Owned(Map map, string name) => Available(map).Any(t => t.def.defName == name && Usable(t));
        private static IEnumerable<Thing> Available(Map map) => map.listerThings.AllThings.Where(t =>
            t.Spawned && t.def.category == ThingCategory.Item && !t.IsForbidden(Faction.OfPlayer) && !t.Position.Fogged(map))
            .Concat(map.mapPawns.FreeColonistsSpawned.SelectMany(p => p.apparel?.WornApparel.Cast<Thing>() ?? Enumerable.Empty<Thing>()))
            .Concat(map.mapPawns.FreeColonistsSpawned.Select(p => p.equipment?.Primary).Where(t => t != null));
        public static Dictionary<string, int> Targets(Map map, int stage)
        {
            var fighters = Fighters(map); int melee = fighters.Count(Melee), ranged = fighters.Count - melee;
            var result = new Dictionary<string, int>();
            void need(string name, int n) { if (n > 0 && DefDatabase<ThingDef>.GetNamedSilentFail(name) != null) result[name] = n; }
            need("Apparel_CollarShirt", fighters.Count); need("Apparel_Pants", fighters.Count);
            if (stage == 0)
            {
                need("Apparel_PlateArmor", melee); need("Apparel_SimpleHelmet", fighters.Count);
                need("MeleeWeapon_LongSword", melee);
                // A ranged role must have a producible starter weapon before the next checkpoint can unlock.
                int rangedGuns = Available(map).Where(t => Usable(t) && t.def.IsRangedWeapon && t.def.defName != "Bow_Short")
                    .Sum(t => t.stackCount);
                need("Bow_Short", Math.Max(0, ranged - rangedGuns));
            }
            else if (stage == 1)
            {
                need("Apparel_PlateArmor", melee); need("Apparel_FlakVest", ranged);
                need("Apparel_FlakPants", ranged); need("Apparel_AdvancedHelmet", fighters.Count);
                need("MeleeWeapon_LongSword", melee); need("Gun_AssaultRifle", ranged);
            }
            else
            {
                bool cat = stage >= 3 && DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_ArmorCataphract") != null;
                need(cat ? "Apparel_ArmorCataphract" : "Apparel_PowerArmor", fighters.Count);
                need(cat ? "Apparel_ArmorHelmetCataphract" : "Apparel_PowerArmorHelmet", fighters.Count);
                need("Apparel_ShieldBelt", melee);
                need("ComponentIndustrial", 12); need("ComponentSpacer", 8);
                need("MeleeWeapon_LongSword", melee); need("Gun_AssaultRifle", ranged);
            }
            return result;
        }
        public static bool Equipped(Map map, int stage)
        {
            var fighters = Fighters(map);
            if (fighters.Count == 0) return false;
            return fighters.All(p =>
            {
                var worn = p.apparel?.WornApparel.Where(Usable).Select(a => a.def.defName).ToHashSet() ?? new HashSet<string>();
                bool heavy = worn.Contains("Apparel_PowerArmor") || worn.Contains("Apparel_ArmorCataphract");
                bool helmet = worn.Contains("Apparel_PowerArmorHelmet") || worn.Contains("Apparel_ArmorHelmetCataphract");
                bool body = stage <= 1 ? heavy || worn.Contains(Melee(p) ? "Apparel_PlateArmor" : "Apparel_FlakVest") : heavy;
                bool head = stage <= 1 ? helmet || worn.Contains("Apparel_AdvancedHelmet") || stage == 0 && worn.Contains("Apparel_SimpleHelmet") : helmet;
                if (stage >= 3 && DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_ArmorCataphract") != null)
                { body = worn.Contains("Apparel_ArmorCataphract"); head = worn.Contains("Apparel_ArmorHelmetCataphract"); }
                return (stage == 0 && !Melee(p) ? heavy || worn.Contains("Apparel_CollarShirt") || worn.Contains("Apparel_BasicShirt") : body) && head && p.equipment?.Primary != null && Usable(p.equipment.Primary) &&
                    (Melee(p) ? !p.equipment.Primary.def.IsRangedWeapon : p.equipment.Primary.def.IsRangedWeapon) &&
                    (stage <= 1 || !Melee(p) || worn.Contains("Apparel_ShieldBelt"));
            });
        }
        public static int Stage(Map map) => !Equipped(map, 0) ? 0 : !Equipped(map, 1) ? 1 : !Equipped(map, 2) ? 2 : 3;
        public static IEnumerable<string> Research(Map map)
        {
            int stage = Stage(map);
            return stage == 0 ? new[] { "ComplexClothing", "Smithing", "PlateArmor", "LongBlades" } :
                stage == 1 ? new[] { "Machining", "FlakArmor", "PrecisionRifling" } :
                stage == 2 ? new[] { "MicroelectronicsBasics", "MultiAnalyzer", "Fabrication", "AdvancedFabrication", "ShieldBelt", "PoweredArmor" } :
                new[] { "CataphractArmor" };
        }
        public static void Apply(Map map, Core.ColonyState state, List<ManagedFoodBill> bills)
        {
            if (state.HostilePawnCount > 0) return;
            bills.RemoveAll(b => b?.Bill == null || b.Bill.DeletedOrDereferenced || !b.Matches);
            var targets = Targets(map, Stage(map));
            foreach (var pair in targets)
            {
                var recipe = map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>()
                    .Where(t => !t.IsForbidden(Faction.OfPlayer)).SelectMany(t => t.def.AllRecipes)
                    .FirstOrDefault(r => r.AvailableNow && r.products?.Any(p => p.thingDef.defName == pair.Key) == true);
                if (recipe == null) continue;
                FoodManager.EnsureBill(map, recipe.defName, state, bills, pair.Value);
                foreach (var owned in bills.Where(b => b.Matches && b.Bill.recipe == recipe))
                {
                    if (Stage(map) == 0 && (pair.Key == "Apparel_PlateArmor" || pair.Key == "Apparel_SimpleHelmet" || pair.Key == "MeleeWeapon_LongSword"))
                        foreach (var stuff in DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsStuff && d != ThingDefOf.Steel))
                            owned.Bill.ingredientFilter.SetAllow(stuff, false);
                    owned.Bill.includeEquipped = true; owned.Bill.includeTainted = false;
                    owned.Bill.hpRange = new FloatRange(0.51f, 1f);
                    owned.Bill.suspended = state.EstimatedFoodDays < 1f;
                    owned.Signature = ManagedFoodBill.Describe(owned.Bill);
                }
            }
            // Retire only untouched AI targets from earlier tiers; never modify player bills.
            foreach (var owned in bills.Where(b => b.Matches && b.Bill.recipe.products?.Any(p => p.thingDef.IsApparel || p.thingDef.IsWeapon) == true))
                if (!owned.Bill.recipe.products.Any(p => targets.ContainsKey(p.thingDef.defName)))
                { owned.Bill.suspended = true; owned.Signature = ManagedFoodBill.Describe(owned.Bill); }
        }
    }
}
