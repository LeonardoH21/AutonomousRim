using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using RimWorld;
using Verse;
using HarmonyLib;
using AutonomousRim.Planning;

namespace AutonomousRim.RuntimeChecks
{
    // Runs only on the exported generated colony. No materials, pawns, needs,
    // skills, research or structures are fabricated for this production check.
    public static class CraftProductionChecks
    {
        private static bool loaded, started, finished, roundtripSaved;
        private static int startTick;
        private static readonly List<ManagedFoodBill> bills = new List<ManagedFoodBill>();
        private static readonly List<Pawn> hunts = new List<Pawn>();
        public static void Tick(Map map)
        {
            if (finished || Find.TickManager.TicksGame % 120 != 0) return;
            try
            {
                if (roundtripSaved)
                {
                    var component = map.GetComponent<AutonomousRimMapComponent>();
                    var restored = (List<ManagedFoodBill>)AccessTools.Field(typeof(AutonomousRimMapComponent), "ownedBills").GetValue(component);
                    var clothes = restored.Where(b => b.Bill?.recipe.products?.Any(p => p.thingDef.IsApparel) == true).ToList();
                    Check(clothes.Count == 3, "Clothing ownership lost during save/load: " + clothes.Count);
                    foreach (var order in clothes) Check(order.Matches, "Loaded ownership mismatch: " + Detail(order));
                    var savedClothes = clothes.Select(b => b.Bill).ToList();
                    component.SetAutomation(false, false);
                    Check(savedClothes.All(b => b.DeletedOrDereferenced), "Loaded clothing bills were not removed on disable.");
                    component.SetAutomation(true, true);
                    component.SetLootAutomation(true);
                    component.SetEquipmentAutomation(true);
                    component.SetBaseAutomation(true);
                    GameDataSaveLoader.SaveGame("ConstructionFinishedUpdated");
                    finished = true;
                    Log.Message("[AutonomousRim.NormalTests] PASS: native pants/collar-shirt/outerwear 3/3, ownership over game ticks and native save/load, no duplicates and cleanup. Existing fixture materials only; not a sustained clothing production benchmark.");
                    return;
                }
                if (!loaded)
                {
                    loaded = true; GameDataSaveLoader.LoadGame("AutonomousRim-Teste-BasePronta"); return;
                }
                if (!started)
                {
                    var component = map.GetComponent<AutonomousRimMapComponent>();
                    component.DisableAll();
                    foreach (var project in component.BaseProjects.Where(p => p.Stockpile != null)) StoragePolicy.Configure(project.Stockpile.GetStoreSettings(), project.Kind);
                    foreach (var task in component.BaseProjects.SelectMany(BaseConstructionManager.Tasks).Where(t => t.StorageKind != null && t.Complete(map)))
                        StoragePolicy.Configure(task.Position.GetThingList(map).OfType<Building_Storage>().Single(b => b.Position == task.Position).GetStoreSettings(), task.StorageKind, shelf: true);
                    foreach (var table in map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>())
                        foreach (var bill in table.BillStack.Bills.Where(b => b.recipe.products?.Any(p => p.thingDef.IsApparel) == true).ToList())
                            table.BillStack.Delete(bill); // Old orders in this generated fixture only.
                    FoodManager.Apply(map, ColonyStateScanner.Scan(map), hunts, bills);
                    var clothes = bills.Where(b => b.Bill.recipe.products?.Any(p => p.thingDef.IsApparel) == true).ToList();
                    Check(clothes.Count == 3, "Three clothing recipes were not created.");
                    foreach (var order in clothes) Check(order.Matches, "Ownership immediately invalid: " + Detail(order));
                    started = true; startTick = Find.TickManager.TicksGame;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                    return;
                }
                if (Find.TickManager.TicksGame - startTick < 720) return;
                foreach (var order in bills) Check(order.Matches, "Native bill changed ownership: " + Detail(order));
                FoodManager.Apply(map, ColonyStateScanner.Scan(map), hunts, bills);
                var current = bills.Where(b => b.Bill.recipe.products?.Any(p => p.thingDef.IsApparel) == true).ToList();
                Check(current.Count == 3 && current.All(b => b.Bill.targetCount == 3 && b.Bill.unpauseWhenYouHave == 3 && b.Bill.pauseWhenSatisfied), "Clothing targets/ownership changed.");
                var saved = current.Select(b => b.Bill).ToList();
                FoodManager.RemoveOwnedBills(bills);
                Check(saved.All(b => b.DeletedOrDereferenced), "Owned clothing bills were not removed on disable.");
                FoodManager.CancelHunts(map, hunts);
                map.GetComponent<AutonomousRimMapComponent>().SetAutomation(true, true);
                GameDataSaveLoader.SaveGame("ClothingRoundtrip");
                roundtripSaved = true;
                GameDataSaveLoader.LoadGame("ClothingRoundtrip");
            }
            catch (Exception error) { finished = true; Log.Error("[AutonomousRim.NormalTests] FAIL: clothing production: " + error); }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static string Detail(ManagedFoodBill bill) => bill.Bill.recipe.defName + "; deleted=" + bill.Bill.DeletedOrDereferenced +
            "; expected=" + bill.Signature + "; actual=" + ManagedFoodBill.Describe(bill.Bill);
    }
}
