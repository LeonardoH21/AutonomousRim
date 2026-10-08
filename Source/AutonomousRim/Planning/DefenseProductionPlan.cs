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
        public static Dictionary<string, int> Targets(Map map, int stage) => LoadoutProgression.Analyze(map,stage).BillTargets;
        public static bool Equipped(Map map,int stage) => Fighters(map).Count>0 &&
            map.GetComponent<Core.AutonomousRimMapComponent>().Commerce.ExpeditionPawns.All(p=>p==null || p.Dead || p.Spawned && p.Map==map) &&
            Fighters(map).All(p=>LoadoutProgression.Equipped(p,stage));
        public static int Stage(Map map) => LoadoutProgression.Stage(map);
        public static IEnumerable<string> Research(Map map)
        {
            if(Fighters(map).Count==0)return Enumerable.Empty<string>();
            int stage=Stage(map);
            return stage==0?new[]{"ComplexClothing","Smithing","PlateArmor","LongBlades"}:
                stage==1?new[]{"Machining","FlakArmor","GasOperation"}:
                stage==2?new[]{"PrecisionRifling"}:
                stage==3?new[]{"MicroelectronicsBasics","MultiAnalyzer","Fabrication","AdvancedFabrication","ShieldBelt","PoweredArmor","ChargedShot"}:
                new[]{"CataphractArmor"};
        }
        public static void Apply(Map map, Core.ColonyState state, List<ManagedFoodBill> bills)
        {
            if (state.HostilePawnCount > 0) return;
            bills.RemoveAll(b => b?.Bill == null || b.Bill.DeletedOrDereferenced || !b.Matches);
            int stage=Stage(map);
            var checkpoint=LoadoutProgression.Analyze(map,stage);
            var targets=checkpoint.BillTargets;
            bool stable=state.EstimatedFoodDays>=3 && state.DownedColonists==0 &&
                map.mapPawns.FreeColonistsSpawned.All(p=>p.health.hediffSet.BleedRateTotal==0);
            // Stages are demand-driven. Late upgrades wait for stability and an established industry.
            if(stage>=3 && !stable)targets=new Dictionary<string,int>();
            if(stage>=3 && stable)
                foreach(var material in checkpoint.Materials.Where(p=>p.Key=="ComponentIndustrial" || p.Key=="ComponentSpacer"))
                    targets[material.Key]=Math.Max(material.Key=="ComponentIndustrial"?6:3,material.Value);
            foreach (var pair in targets)
            {
                var recipe = map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>()
                    .Where(t => !t.IsForbidden(Faction.OfPlayer)).SelectMany(t => t.def.AllRecipes)
                    .FirstOrDefault(r => r.AvailableNow && r.products?.Any(p => p.thingDef.defName == pair.Key) == true);
                if (recipe == null) continue;
                FoodManager.EnsureBill(map, recipe.defName, state, bills, pair.Value);
                foreach (var owned in bills.Where(b => b.Matches && b.Bill.recipe == recipe))
                {
                    if (stage == 0 && (pair.Key == "Apparel_PlateArmor" || pair.Key == "Apparel_SimpleHelmet" || pair.Key == "MeleeWeapon_LongSword"))
                        foreach (var stuff in DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsStuff && d != ThingDefOf.Steel))
                            owned.Bill.ingredientFilter.SetAllow(stuff, false);
                    owned.Bill.includeEquipped = true; owned.Bill.includeTainted = false;
                    owned.Bill.hpRange = new FloatRange(0.51f, 1f);
                    owned.Bill.suspended = state.EstimatedFoodDays < 1f || stage>=3 && !stable;
                    owned.Signature = ManagedFoodBill.Describe(owned.Bill);
                }
            }
            // Retire only untouched AI targets from earlier tiers; never modify player bills.
            foreach (var owned in bills.Where(b => b.Matches && b.Bill.recipe.products?.Any(p => p.thingDef.IsApparel || p.thingDef.IsWeapon ||
                p.thingDef==ThingDefOf.ComponentIndustrial || p.thingDef.defName=="ComponentSpacer") == true))
                if (!owned.Bill.recipe.products.Any(p => targets.ContainsKey(p.thingDef.defName)))
                { owned.Bill.suspended = true; owned.Signature = ManagedFoodBill.Describe(owned.Bill); }
        }
    }
}
