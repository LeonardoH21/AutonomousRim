using System;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    // Loaded only in the isolated test add-on. Never installed in the production mod.
    public sealed class ColonyManagementChecks : MapComponent
    {
        private bool tested;
        public ColonyManagementChecks(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (tested || !GenCommandLine.CommandLineArgPassed("autonomousrimtest") || Find.TickManager.TicksGame < 100) return;
            tested = true;
            try { Run(); Log.Message("[AutonomousRim.Tests] PASS: ground/corpse loot, food demand, production, work override and rollback, hunting selection."); }
            catch (Exception error) { Log.Error("[AutonomousRim.Tests] FAIL: " + error); }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private IntVec3 EmptyCell(Pawn pawn)
        {
            for (int radius = 2; radius < 15; radius++)
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, radius, true))
                    if (cell.InBounds(map) && cell.Standable(map) && !cell.Fogged(map) && cell.GetEdifice(map) == null && !cell.GetThingList(map).Any()) return cell;
            throw new InvalidOperationException("No clear test cell.");
        }

        private void Run()
        {
            Pawn colonist = map.mapPawns.FreeColonistsSpawned.First();
            Thing gun = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle"));
            GenSpawn.Spawn(gun, EmptyCell(colonist), map); gun.SetForbidden(true, false);
            var parkaDef = DefDatabase<ThingDef>.GetNamed("Apparel_Parka");
            Thing parka = ThingMaker.MakeThing(parkaDef, ThingDefOf.Cloth);
            GenSpawn.Spawn(parka, EmptyCell(colonist), map); parka.SetForbidden(true, false);
            ColonyState state = ColonyStateScanner.Scan(map);
            Check(state.Loot.Any(l => l.Name == gun.LabelCap.ToString() && l.Forbidden), "Forbidden enemy ground weapon not identified.");
            Check(state.Loot.Any(l => l.Name == parka.LabelCap.ToString() && l.Kind == "Roupa"), "Ground apparel not identified.");
            Check(state.DailyFoodNutrition > 0f, "Actual colonist food demand missing.");

            Pawn raider = PawnGenerator.GeneratePawn(PawnKindDefOf.Villager, Faction.OfAncientsHostile);
            var corpseParka = (Apparel)ThingMaker.MakeThing(parkaDef, ThingDefOf.Cloth);
            raider.apparel.Wear(corpseParka, false);
            GenSpawn.Spawn(raider, EmptyCell(colonist), map);
            raider.Kill(null);
            state = ColonyStateScanner.Scan(map);
            Check(state.Loot.Any(l => l.Location.Contains("No cadáver de") && l.Name == corpseParka.LabelCap.ToString()), "Corpse clothing not identified.");

            var butcher = (Building_WorkTable)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ButcherSpot"));
            butcher.SetFaction(Faction.OfPlayer);
            ThingDef stoveDef = DefDatabase<ThingDef>.GetNamed("ElectricStove");
            var stove = (Building_WorkTable)ThingMaker.MakeThing(stoveDef, stoveDef.MadeFromStuff ? ThingDefOf.Steel : null);
            stove.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(stove, EmptyCell(colonist), map);
            // The stove occupies multiple cells; place the one-cell spot afterwards
            // so its footprint cannot wipe the test butcher spot.
            GenSpawn.Spawn(butcher, EmptyCell(colonist), map);
            var component = map.GetComponent<AutonomousRimMapComponent>();
            Pawn worker = map.mapPawns.FreeColonistsSpawned.First(p => WorkPriorityManager.CanWork(p) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Research));
            worker.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
            int originalResearch = worker.workSettings.GetPriority(WorkTypeDefOf.Research);
            Pawn hunter = map.mapPawns.FreeColonistsSpawned.First(p => PawnAnalyzer.IsCombatReady(p) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Hunting));
            ThingWithComps originalWeapon = hunter.equipment.Primary;
            if (originalWeapon != null) hunter.equipment.Remove(originalWeapon);
            gun.DeSpawn(); gun.SetForbidden(false, false);
            hunter.equipment.AddEquipment((ThingWithComps)gun);
            component.SetAutomation(true, true);
            Check(Find.PlaySettings.useWorkPriorities, "Numeric priorities were not activated.");
            Check(stove.BillStack.Bills.Any(b => b.recipe.defName == "CookMealSimple"), "Simple-meal bill missing.");
            Check(butcher.BillStack.Bills.Any(b => b.recipe.defName == "ButcherCorpseFlesh"), "Butchering bill missing.");
            Check(WorkPriorityManager.CanHunt(hunter), "Prepared ranged hunter is not eligible.");
            Pawn prey = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Hare"));
            GenSpawn.Spawn(prey, EmptyCell(hunter), map);
            var testHunts = new System.Collections.Generic.List<Pawn>();
            var testBills = new System.Collections.Generic.List<ManagedFoodBill>();
            map.designationManager.RemoveAllDesignationsOfDef(DesignationDefOf.Hunt);
            state = ColonyStateScanner.Scan(map); state.FoodNutrition = 0f; state.EstimatedFoodDays = 0f;
            FoodManager.Apply(map, state, testHunts, testBills);
            Check(testHunts.Count > 0 && testHunts.All(p => !p.RaceProps.predator && p.RaceProps.manhunterOnDamageChance <= 0f), "No safe wild prey designated during food shortage.");
            FoodManager.CancelHunts(map, testHunts);
            prey.Destroy();
            component.SetAutomation(false, false);
            Check(worker.workSettings.GetPriority(WorkTypeDefOf.Research) == originalResearch, "Original work priority not restored.");
            Check(!stove.BillStack.Bills.Any() && !butcher.BillStack.Bills.Any(), "Owned production bills not removed on disable.");

            component.SetAutomation(true, true);
            int applied = worker.workSettings.GetPriority(WorkTypeDefOf.Research);
            int manual = applied == 4 ? 1 : 4;
            worker.workSettings.SetPriority(WorkTypeDefOf.Research, manual);
            component.SetAutomation(false, false);
            Check(worker.workSettings.GetPriority(WorkTypeDefOf.Research) == manual, "Manual work override lost on disable.");
            Check(!ColonyPolicy.SafePrey(true, 0f, false, false, false), "Predator permitted for automatic hunting.");
            Check(!ColonyPolicy.SafePrey(false, 0.01f, false, false, false), "Retaliating animal permitted for automatic hunting.");
            Check(ColonyPolicy.SafePrey(false, 0f, false, false, false), "Passive wild prey incorrectly rejected.");
            hunter.equipment.Remove((ThingWithComps)gun);
            if (originalWeapon != null) hunter.equipment.AddEquipment(originalWeapon);
            gun.Destroy(); parka.Destroy(); stove.Destroy(); butcher.Destroy();
        }
    }
}
