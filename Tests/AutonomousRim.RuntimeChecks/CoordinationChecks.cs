using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using AutonomousRim.Planning;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class CoordinationChecks : MapComponent
    {
        private static bool done;
        public CoordinationChecks(Map map) : base(map) { }
        private void Check(bool condition, string text) { if (!condition) throw new Exception(text); Log.Message("[Coordination] PASS: " + text); }
        public override void MapComponentTick()
        {
            if (done || !EliteFixtureBaseline.Ready || !GenCommandLine.CommandLineArgPassed("autonomousrimcoordinationtest") || Find.TickManager.TicksGame < 300) return;
            done = true;
            try
            {
                var roster = EliteFixtureBaseline.Roster(map);
                var ai = map.GetComponent<AutonomousRimMapComponent>(); ai.DisableAll(); ai.SetEquipmentAutomation(true);
                Find.PlaySettings.useWorkPriorities = true;
                foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().SelectMany(t => t.BillStack.Bills)) b.suspended = true;
                foreach (var t in map.listerThings.AllThings.Where(t => t is Blueprint_Build || t is Frame || t.def == ThingDefOf.Cloth).ToList()) t.Destroy(DestroyMode.Vanish);
                foreach (var p in roster)
                {
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced); p.drafter.Drafted = false;
                    p.needs.rest.CurLevel = 1; p.needs.food.CurLevel = 1; p.needs.joy.CurLevel = 1;
                    for (int h = 0; h < 24; h++) p.timetable.SetAssignment(h, TimeAssignmentDefOf.Anything);
                    foreach (var w in DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w => !p.WorkTypeIsDisabled(w))) p.workSettings.SetPriority(w, 3);
                }
                var cell = map.Center + new IntVec3(20, 0, 20);
                foreach (var c in new CellRect(cell.x - 5, cell.z - 5, 13, 13).Cells)
                {
                    foreach (var t in c.GetThingList(map).Where(t => !(t is Pawn)).ToList()) t.Destroy(DestroyMode.Vanish);
                    map.terrainGrid.SetTerrain(c, TerrainDefOf.Soil); map.roofGrid.SetRoof(c, null); map.fogGrid.Unfog(c);
                }
                var table = (Building_WorkTable)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("HandTailoringBench"), ThingDefOf.WoodLog);
                table.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(table, cell, map);
                var bill = new Bill_Production(DefDatabase<RecipeDef>.GetNamed("Make_Apparel_Pants"));
                bill.repeatMode = BillRepeatModeDefOf.RepeatCount; bill.repeatCount = 2;
                bill.ingredientFilter.SetDisallowAll(); bill.ingredientFilter.SetAllow(ThingDefOf.Cloth, true);
                table.BillStack.AddBill(bill);
                ((List<ManagedFoodBill>)ai.DefenseBills).Add(new ManagedFoodBill { Bill = bill, Signature = ManagedFoodBill.Describe(bill) });
                var changes = new List<WorkPriorityChange>(); var state = ColonyStateScanner.Scan(map);
                ai.Coordination.Refresh(map, changes, true);
                Check(!ai.Coordination.ReadyWorks.Contains(DefDatabase<WorkTypeDef>.GetNamed("Tailoring")), "missing ingredients do not reserve equipment specialist");
                int recipeCost = bill.recipe.ingredients.First().CountRequiredOfFor(ThingDefOf.Cloth, bill.recipe, bill);
                var chair = DefDatabase<ThingDef>.GetNamed("Armchair");
                int chairCost = CostListCalculator.CostListAdjusted(chair, ThingDefOf.Cloth).Where(c => c.thingDef == ThingDefOf.Cloth).Sum(c => c.count);
                var partial=ThingMaker.MakeThing(ThingDefOf.Cloth);partial.stackCount=1;GenPlace.TryPlaceThing(partial,cell+new IntVec3(3,0,0),map,ThingPlaceMode.Near);
                ai.Coordination.Refresh(map,changes,true);
                Check(!ai.Coordination.ReadyWorks.Contains(DefDatabase<WorkTypeDef>.GetNamed("Tailoring")) &&
                    ai.Coordination.Materials.TryGetValue(ThingDefOf.Cloth,out int partialHeld) && partialHeld==1,
                    "partial stock is protected without claiming a ready job or more material than exists");
                int total = chairCost + recipeCost - 2;
                while (total > 0)
                {
                    var cloth = ThingMaker.MakeThing(ThingDefOf.Cloth); cloth.stackCount = Math.Min(total, cloth.def.stackLimit); total -= cloth.stackCount;
                    GenPlace.TryPlaceThing(cloth, cell + new IntVec3(3, 0, 0), map, ThingPlaceMode.Near);
                }
                ai.Coordination.Refresh(map, changes, true);
                Check(ai.Coordination.ReadyWorks.Contains(DefDatabase<WorkTypeDef>.GetNamed("Tailoring")), "native ingredient allocation makes equipment demand viable");
                Check(ai.Coordination.Materials.TryGetValue(ThingDefOf.Cloth, out int held) && held == recipeCost, "soft budget protects exactly next native recipe iteration");
                var project = new RoomProject { Kind = "Teste orçamento", Origin = cell + new IntVec3(-3, 0, 3), InteriorSize = 3, RequiresRoof = false, Priority = ConstructionPriority.Low };
                var task = new ConstructionTask { Def = chair, Stuff = ThingDefOf.Cloth, Position = cell + new IntVec3(-3, 0, 3), Rotation = Rot4.North };
                project.Furniture.Add(task);
                BaseConstructionManager.Apply(map, new List<RoomProject> { project });
                Check(task.Pending == null, "optional native blueprint cannot consume protected crafting material");
                project.Priority = ConstructionPriority.High;
                BaseConstructionManager.Apply(map, new List<RoomProject> { project });
                Check(task.Pending==null,"high-priority optional expansion still protects urgent equipment inputs");
                project.Priority = ConstructionPriority.Critical;
                BaseConstructionManager.Apply(map, new List<RoomProject> { project });
                Check(task.Pending?.Spawned == true, "critical project can override advisory crafting reserve");
                task.Pending.Destroy(DestroyMode.Vanish); task.Pending = null;
                WorkPriorityManager.Apply(map, state, changes, true, false, true);
                Check(ai.Coordination.Researcher == null || !ai.Coordination.Equipment.Any(d => d.ReadyPawns.Contains(ai.Coordination.Researcher) &&
                    d.Works.Any(w => WorkPriorityManager.RawPriority(ai.Coordination.Researcher, w) == 1)), "research specialist is distinct from focused equipment crafter");
                var focused=ai.Coordination.Roles.Select(r=>r.Split(';')[0]).ToList();
                WorkPriorityManager.Apply(map,state,changes,true,false,true);
                Check(ai.Coordination.Roles.Select(r=>r.Split(';')[0]).SequenceEqual(focused),"unchanged demand preserves role allocation instead of oscillating");
                Check(ai.Coordination.Roles.Any(r=>r.Contains("suplente")),"role report exposes backup availability");
                var tailor = roster.First(p => WorkPriorityManager.RawPriority(p, DefDatabase<WorkTypeDef>.GetNamed("Tailoring")) == 1);
                Check(!ConstructionWorkManager.CanDispatch(tailor, WorkTypeDefOf.Construction), "construction cannot steal viable focused crafter in job gap");
                bill.ingredientFilter.SetAllow(ThingDefOf.Cloth, false);
                ai.Coordination.Refresh(map, changes, true);
                Check(ai.Coordination.Materials.Count == 0, "material budget releases when recipe is blocked");
                Check(!ai.Coordination.ProtectFromConstruction(tailor, WorkTypeDefOf.Construction), "blocked crafting allows alternative work");
                bill.ingredientFilter.SetAllow(ThingDefOf.Cloth, true);
                bill.suspended = true;
                ai.Coordination.Refresh(map, changes, true);
                Check(ai.Coordination.Equipment.All(d => d.Bill != bill), "manual suspension removes demand without changing the bill");
                bill.suspended = false;
                foreach (var p in roster) p.workSettings.SetPriority(DefDatabase<WorkTypeDef>.GetNamed("Tailoring"), 0);
                WorkPriorityManager.Apply(map, state, changes, true, false, true);
                Check(roster.All(p => WorkPriorityManager.RawPriority(p, DefDatabase<WorkTypeDef>.GetNamed("Tailoring")) == 0) &&
                    !ai.Coordination.ReadyWorks.Contains(DefDatabase<WorkTypeDef>.GetNamed("Tailoring")), "manual disabling is recognized before selecting roles");
                EliteFixtureBaseline.VerifyOriginalsPresent(map);
                GameDataSaveLoader.SaveGame("CoordinationEvidence"); Log.Message("[Coordination] DONE");
            }
            catch (Exception e) { Log.Error("[Coordination] FAIL: " + e); }
        }
    }
}
