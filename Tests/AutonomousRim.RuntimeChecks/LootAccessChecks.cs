using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    public static class LootAccessChecks
    {
        private static void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        public static void Run(Map map, Pawn worker, AutonomousRimMapComponent component)
        {
            Check(!component.LootAutomation, "Allow must default to off.");
            var flags = map.listerThings.AllThings.Where(t => t.def.category == ThingCategory.Item &&
                (t.def.IsMedicine || t.def.ingestible?.HumanEdible == true)).ToDictionary(t => t, t => t.IsForbidden(Faction.OfPlayer));
            var created = new List<Thing>();
            Func<IntVec3> cell = () => GenRadial.RadialCellsAround(worker.Position, 30f, true).First(c => c.InBounds(map) && c.Standable(map) &&
                !c.Fogged(map) && !c.GetThingList(map).Any() && !component.BaseProjects.Any(p => p.Footprint.Contains(c)));
            Func<ThingDef, int, Thing> spawn = (def, count) =>
            {
                Thing item = ThingMaker.MakeThing(def); item.stackCount = count;
                GenSpawn.Spawn(item, cell(), map); item.SetForbidden(true, false); created.Add(item); return item;
            };
            try
            {
                foreach (Thing item in flags.Keys) item.SetForbidden(true, false);
                for (int i = 0; i < 4; i++) spawn(ThingDefOf.MedicineIndustrial, 2);
                Thing meal = spawn(ThingDefOf.MealSimple, 2);
                Thing chemfuel = spawn(DefDatabase<ThingDef>.GetNamed("Chemfuel"), 10);
                var history = new List<Thing>();
                ColonyState state = ColonyStateScanner.Scan(map);
                LootAccessManager.Apply(map, state, false, component.BaseProjects, new List<ManagedApparel>(), p => false, history, out int first);
                Check(first > 0 && first <= LootAccessManager.MaxStacksPerCycle, "Allow must release a bounded batch of needed items.");
                Thing medicine = history.FirstOrDefault(t => t.def.IsMedicine);
                Check(medicine != null, "Allow did not prioritize deficient medicine.");
                medicine.SetForbidden(true, false);
                LootAccessManager.Apply(map, state, false, component.BaseProjects, new List<ManagedApparel>(), p => false, history, out int second);
                Check(second <= LootAccessManager.MaxStacksPerCycle && medicine.IsForbidden(Faction.OfPlayer), "Allow ignored a player's reprohibition.");
                for (int cycle = 0; cycle < 4 && meal.IsForbidden(Faction.OfPlayer); cycle++)
                    LootAccessManager.Apply(map, state, false, component.BaseProjects, new List<ManagedApparel>(), p => false, history, out _);
                Check(!meal.IsForbidden(Faction.OfPlayer), "Allow did not release food after covering the medicine reserve.");
                Check(chemfuel.IsForbidden(Faction.OfPlayer), "Allow released an unrelated explosive resource.");
                component.SetLootAutomation(true);
                int permitted = map.listerThings.AllThings.Count(t => t.def.category == ThingCategory.Item && !t.IsForbidden(Faction.OfPlayer));
                component.SetLootAutomation(true);
                Check(permitted == map.listerThings.AllThings.Count(t => t.def.category == ThingCategory.Item && !t.IsForbidden(Faction.OfPlayer)), "Enabling twice bypassed the allow cooldown.");
                component.DisableAll();
                Check(!component.LootAutomation, "Disable-all failed to disable allow.");
                Log.Message("[AutonomousRim.AllowTests] PASS: medicine/food needs, bounded batches, irrelevant resource exclusion, manual reprohibition, cooldown and disable-all.");
            }
            finally
            {
                component.SetLootAutomation(false);
                foreach (var entry in flags) if (entry.Key.Spawned) entry.Key.SetForbidden(entry.Value, false);
                foreach (Thing item in created) if (!item.Destroyed) item.Destroy();
            }
        }
    }
}
