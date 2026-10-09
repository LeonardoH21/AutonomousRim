using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    // Observe real bill completions; equipment acquired from raids cannot satisfy crafting proof.
    [StaticConstructorOnStartup]
    public static class ResearchCraftTrialObserver
    {
        static ResearchCraftTrialObserver()
        {
            if (!GenCommandLine.CommandLineArgPassed("autonomousrimprogressiontrial")) return;
            var h = new Harmony("autonomousrim.nativecraft.observer");
            h.Patch(AccessTools.Method(typeof(Bill_Production), nameof(Bill_Production.Notify_IterationCompleted)),
                postfix: new HarmonyMethod(typeof(ResearchCraftTrialObserver), nameof(CompletedBill)));
        }
        private static void CompletedBill(Bill_Production __instance, Pawn billDoer)
        {
            if (billDoer.Faction != Faction.OfPlayer) return;
            var proof=billDoer.Map.GetComponent<ModularConstructionTrial>();
            proof.CompletedRecipes.Add(__instance.recipe.defName);
            Log.Message("[AutonomousRim.CraftTrial] NATIVE BILL COMPLETE: " + __instance.recipe.defName + "; worker=" + billDoer.LabelShort);
            foreach (var product in __instance.recipe.products)
            {
                proof.Crafted.Add(product.thingDef.defName);
            }
        }
        public static bool Complete(Map map, AutonomousRimMapComponent ai)
        {
            var proof = map.GetComponent<ModularConstructionTrial>();
            foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                if (pawn.CurJob?.def == JobDefOf.Research && pawn.CurJob.targetA.Thing is Building_ResearchBench bench) proof.UsedBenches.Add(bench.thingIDNumber);
            bool research = ai.BaseProjects.Any(p => p.Kind == "Pesquisa" && p.Completed) &&
                map.listerBuildings.AllBuildingsColonistOfClass<Building_ResearchBench>().Count() >= 2 && proof.UsedBenches.Count >= 2 &&
                DefDatabase<ResearchProjectDef>.AllDefsListForReading.Count(r => r.IsFinished) > proof.InitialResearch;
            bool workshop = ai.BaseProjects.Any(p => p.Kind == "Oficina" && p.Completed) &&
                map.listerBuildings.allBuildingsColonist.Any(b => b.def.defName == "FueledSmithy") &&
                map.listerBuildings.allBuildingsColonist.Any(b => b.def.defName == "HandTailoringBench");
            bool storage = map.listerBuildings.allBuildingsColonist.Any(b => b.def.defName == "ShelfSmall");
            bool crafted = new[] { "Apparel_SimpleHelmet", "Apparel_PlateArmor", "MeleeWeapon_LongSword" }.All(proof.Crafted.Contains);
            bool dressed = map.mapPawns.FreeColonistsSpawned.Any(p => p.apparel.WornApparel.Any(a => a.def.defName == "Apparel_SimpleHelmet" && a.Stuff == ThingDefOf.Steel && !a.WornByCorpse) &&
                p.apparel.WornApparel.Any(a => a.def.defName == "Apparel_PlateArmor" && a.Stuff == ThingDefOf.Steel && !a.WornByCorpse) &&
                p.equipment?.Primary?.def.defName == "MeleeWeapon_LongSword" && p.equipment.Primary.Stuff == ThingDefOf.Steel &&
                p.apparel.WornApparel.Any(a => a.def.defName == "Apparel_Pants") &&
                p.apparel.WornApparel.Any(a => a.def.defName == "Apparel_CollarShirt" || a.def.defName == "Apparel_BasicShirt"));
            if (Find.TickManager.TicksGame % 2490 == 0)
            {
                Log.Message("[AutonomousRim.CraftTrial] CHECKPOINT research=" + research + "; benchesUsed=" + proof.UsedBenches.Count + "; workshop=" + workshop + "; storage=" + storage + "; crafted=" + crafted + "; equipped=" + dressed + "; research=" + Find.ResearchManager.GetProject()?.defName);
                var colonists=map.mapPawns.FreeColonistsSpawned.ToList();
                float record(string name)=>colonists.Sum(p=>p.records.GetValue(DefDatabase<RecordDef>.GetNamed(name)));
                Log.Message("[AutonomousRim.CraftTrial] NATIVE RECORDS animalKills="+record("KillsAnimals")+"; huntingTicks="+record("TimeHunting")+
                    "; mealsCooked="+record("MealsCooked")+"; recipes="+string.Join(",",proof.CompletedRecipes));
                foreach (var prep in ai.BaseProjects.Where(p => p.Kind == "Preparação do terreno" && !p.Completed))
                    Log.Message("[AutonomousRim.CraftTrial] CLEARANCE " + prep.LayoutSlot + ": " + string.Join(";", prep.MineCells.Where(c => c.GetEdifice(map) is Mineable).Select(c => c + ":designated=" + (map.designationManager.DesignationAt(c, DesignationDefOf.Mine) != null) + ":reachable=" + map.mapPawns.FreeColonistsSpawned.Any(p => p.CanReach(c, Verse.AI.PathEndMode.Touch, Danger.None)))));
            }
            return research && workshop && storage && crafted && dressed && ai.BaseProjects.Where(p => p.Kind == "Quarto" || p.Kind == "Cozinha" || p.Kind == "Abate" || p.Kind == "Estoque").All(p => p.Completed);
        }
    }
}
