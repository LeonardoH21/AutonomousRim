using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using AutonomousRim.UI;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    // Fixtures mutate only the generated -autonomousrimtest colony.
    public sealed class ConstructionChecks : MapComponent
    {
        private int stage;
        private int started;
        private Pawn worker;
        private ConstructionTask wall;
        private Blueprint_Build playerBlueprint;
        private AutonomousRimMapComponent component;
        private RoomProject trialRoom;
        private TimeSpeed previousSpeed;
        private Dictionary<Pawn, bool> drafts = new Dictionary<Pawn, bool>();
        private static int draws;
        private bool captureRequested;
        private RoomProject corridor;
        private float thermalStart;
        public ConstructionChecks(Map map) : base(map) { }
        public static void InspectorDrawn() { draws++; }
        private void Finish()
        {
            foreach (var entry in drafts) entry.Key.drafter.Drafted = entry.Value;
            stage = 99;
            Log.Message("[AutonomousRim.ConstructionTests] PASS: compact connected layout, shared boundaries, two exits, working vents/power/heating/cooling, comfort facilities, preview, budget gate, bounded blueprints, real pawn full bedroom, stockpile, cancellation and toggle cleanup.");
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        private IntVec3 EmptyCell()
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(worker.Position, 15f, true))
                if (cell.InBounds(map) && cell.Standable(map) && !cell.Fogged(map) && !cell.GetThingList(map).Any() &&
                    !component.BaseProjects.Any(p => p.Footprint.ExpandedBy(2).Contains(cell))) return cell;
            throw new InvalidOperationException("No empty resource test cell.");
        }
        private void Resource(ThingDef def, int count)
        {
            while (count > 0)
            {
                Thing item = ThingMaker.MakeThing(def); item.stackCount = Math.Min(count, def.stackLimit);
                count -= item.stackCount; GenSpawn.Spawn(item, EmptyCell(), map);
            }
        }
        private Frame PrepareFrame(ConstructionTask task)
        {
            // Accelerated fixture prerequisites only; the full native room trial
            // lets actual work givers clear vegetation and deliver materials.
            foreach (IntVec3 cell in GenAdj.OccupiedRect(task.Position, task.Rotation, task.Def is ThingDef thingDef ? thingDef.size : IntVec2.One))
            {
                foreach (Plant plant in cell.GetThingList(map).OfType<Plant>().ToList()) plant.Destroy();
                foreach (Thing item in cell.GetThingList(map).Where(t => t.def.category == ThingCategory.Item).ToList())
                {
                    Check(!item.IsForbidden(Faction.OfPlayer), "Accelerated fixture would move a forbidden item.");
                    IntVec3 destination = EmptyCell(); item.DeSpawn(); GenSpawn.Spawn(item, destination, map);
                }
                foreach (Pawn pawn in cell.GetThingList(map).OfType<Pawn>().ToList()) pawn.Position = EmptyCell();
            }
            var bp = task.Pending as Blueprint_Build ?? GenConstruct.PlaceBlueprintForBuild(task.Def, task.Position, map, task.Rotation, Faction.OfPlayer, task.Stuff);
            Check(bp.TryReplaceWithSolidThing(worker, out Thing solid, out bool failed), "Native blueprint-to-frame transition failed: " + task.Def.defName);
            Frame frame = solid as Frame;
            Check(frame != null && !failed, "Native transition did not produce a construction frame.");
            foreach (ThingDefCountClass cost in frame.TotalMaterialCost())
            {
                Thing material = ThingMaker.MakeThing(cost.thingDef); material.stackCount = cost.count;
                Check(frame.resourceContainer.TryAdd(material), "Could not supply test frame materials.");
            }
            task.Pending = frame; task.Issued = true;
            return frame;
        }

        private void PrepareBuildableSite()
        {
            // Quicktest can generate a heavily mountainous map. Keep production's refusal
            // intact, then prepare terrain only in this disposable fixture to test execution.
            IntVec3 origin = map.mapPawns.FreeColonistsSpawned.First().Position + new IntVec3(-42, 0, -30);
            var area = new CellRect(origin.x - 2, origin.z - 3, 26, 40);
            var cells = new HashSet<IntVec3>(area.Cells);
            for (int x = origin.x + 8; x <= worker.Position.x; x++)
                for (int z = worker.Position.z - 1; z <= worker.Position.z + 1; z++) cells.Add(new IntVec3(x, 0, z));
            for (int z = origin.z - 2; z <= worker.Position.z; z++)
                for (int x = origin.x + 7; x <= origin.x + 9; x++) cells.Add(new IntVec3(x, 0, z));
            foreach (IntVec3 cell in cells.Where(c => c.InBounds(map)))
            {
                foreach (Thing rock in cell.GetThingList(map).Where(t => t.def.category == ThingCategory.Building && t.Faction != Faction.OfPlayer).ToList()) rock.Destroy();
                foreach (Filth filth in cell.GetThingList(map).OfType<Filth>().ToList()) filth.Destroy();
                foreach (Thing item in cell.GetThingList(map).Where(t => t.def.category == ThingCategory.Item).ToList())
                { item.DeSpawn(); GenSpawn.Spawn(item, EmptyCell(), map); }
                if (cell.Roofed(map)) map.roofGrid.SetRoof(cell, null);
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                map.fogGrid.Unfog(cell);
            }
        }
        private void CompleteFixture(RoomProject project)
        {
            // Accelerates prerequisites only; the first wall is separately completed by a real pawn job.
            foreach (ConstructionTask task in project.Shell.Concat(project.Furniture))
                if (!task.Complete(map))
                {
                    PrepareFrame(task).CompleteConstruction(worker);
                }
            if (project.RequiresRoof) foreach (IntVec3 cell in project.Interior) map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
        }

        public override void MapComponentTick()
        {
            int ticks = Find.TickManager.TicksGame;
            if (GenCommandLine.CommandLineArgPassed("autonomousrimskiplegacyconstruction") || stage == 99 || !GenCommandLine.CommandLineArgPassed("autonomousrimtest") || ticks < 1400 || ticks % 20 != 0) return;
            try
            {
                if (stage == 0) { Setup(); started = ticks; stage = 1; }
                else if (stage == 1 && wall.Complete(map))
                {
                    component.SetBaseAutomation(false);
                    Check(wall.Complete(map), "Disabling construction removed the completed wall.");
                    Check(playerBlueprint.Spawned, "Disabling construction removed the player's blueprint.");
                    Check(!component.BaseProjects.SelectMany(p => p.Shell.Concat(p.Furniture)).Any(t => t.Pending is Blueprint_Build bp && bp.Spawned), "AI blueprints retained after disabling.");
                    VerifyModules();
                    var harmony = new Harmony("autonomousrim.runtimechecks.hud");
                    harmony.Patch(AccessTools.Method(typeof(MainTabWindow_Inspector), "DoWindowContents"), postfix: new HarmonyMethod(typeof(ConstructionChecks), nameof(InspectorDrawn)));
                    Find.MainTabsRoot.SetCurrentTab(DefDatabase<MainButtonDef>.GetNamed("AutonomousRimInspector"), false);
                    trialRoom = component.BaseProjects.First(p => p.Kind == "Quarto" && !p.Completed &&
                        p.Shell.Concat(p.Furniture).All(t => !t.CancelledByPlayer));
                    previousSpeed = Find.TickManager.CurTimeSpeed;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                    started = ticks;
                    stage = 2;
                }
                else if (stage == 2)
                {
                    if (ticks % 600 == 0)
                    {
                        // Keep the trial about construction, rather than unrelated meal/rest shortages.
                        worker.needs.food.CurLevel = worker.needs.food.MaxLevel; worker.needs.rest.CurLevel = 1f;
                        string progress = BaseConstructionManager.Apply(map, new List<RoomProject> { trialRoom });
                        Log.Message("[AutonomousRim.ConstructionTests] Native room: " + progress + "; job=" + worker.CurJob?.def.defName);
                    }
                    if (trialRoom.Completed)
                    {
                        VerifyCompact();
                        started = ticks;
                        stage = 4;
                    }
                }
                else if (stage == 4 && ticks - started >= 400)
                {
                    VerifyHeat();
                    started = ticks;
                    stage = 5;
                }
                else if (stage == 5 && ticks - started >= 400)
                {
                        Check(corridor.Interior.CenterCell.GetTemperature(map) < thermalStart, "Powered coolers did not lower corridor temperature.");
                        LootAccessChecks.Run(map, worker, component);
                        Log.Message("[AutonomousRim.LayoutTests] PASS: compact corridor enclosed, vents connected, generator powers all thermostats; native heating/cooling and comfort links verified.");
                        if (!GenCommandLine.CommandLineArgPassed("autonomousrimvisualtest"))
                        {
                            Find.TickManager.CurTimeSpeed = previousSpeed;
                            Finish();
                            return;
                        }
                        Check(trialRoom.Furniture.All(t => t.Complete(map)) && trialRoom.Interior.All(c => c.Roofed(map)), "Native room trial was marked complete prematurely.");
                        Find.TickManager.CurTimeSpeed = previousSpeed;
                        started = ticks;
                        draws = 0;
                        foreach (Window window in Find.WindowStack.Windows.ToList())
                            if (!(window is MainTabWindow_Inspector)) Find.WindowStack.TryRemove(window, false);
                        Find.MainTabsRoot.SetCurrentTab(DefDatabase<MainButtonDef>.GetNamed("AutonomousRimInspector"), false);
                        stage = 3;
                }
                else if (stage == 3 && draws >= 3 && !captureRequested)
                {
                    captureRequested = true;
                    ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "AutonomousRim-HUD.png"));
                }
                else if (stage == 3 && captureRequested && File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "AutonomousRim-HUD.png")))
                {
                    Log.Message("[AutonomousRim.HudTests] PASS: inspector rendered and screenshot captured.");
                    Finish();
                }
                if (stage != 99 && ticks - started > (stage == 2 ? 24000 : 2400)) throw new InvalidOperationException($"Construction timeout stage={stage}; job={worker.CurJob?.def.defName}; HUD draws={draws}.");
            }
            catch (Exception error) { stage = 99; Log.Error("[AutonomousRim.ConstructionTests] FAIL: " + error); }
        }

        private void Setup()
        {
            component = map.GetComponent<AutonomousRimMapComponent>();
            component.DisableAll();
            Check(!component.BaseAutomation, "Disable-all must stop base automation before preview.");
            worker = map.mapPawns.FreeColonistsSpawned.First(p => WorkPriorityManager.CanWork(p) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction));
            foreach (Pawn other in map.mapPawns.FreeColonistsSpawned.Where(p => p != worker))
            { drafts[other] = other.Drafted; other.drafter.Drafted = true; }
            worker.drafter.Drafted = false;
            worker.needs.food.CurLevel = worker.needs.food.MaxLevel; worker.needs.rest.CurLevel = 1f;
            worker.skills.GetSkill(SkillDefOf.Construction).Level = 20;
            worker.workSettings.EnableAndInitializeIfNotAlreadyInitialized(); worker.workSettings.SetPriority(WorkTypeDefOf.Construction, 1);
            worker.jobs.ClearQueuedJobs(true); worker.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
            foreach (string research in new[] { "ComplexFurniture", "Electricity", "AirConditioning" })
                Find.ResearchManager.FinishProject(DefDatabase<ResearchProjectDef>.GetNamed(research), false, null, false);
            component.PreviewBase();
            if (component.BaseProjects.Count == 0)
            {
                Check(component.BaseStatus.Contains("Sem terreno") && !map.listerThings.AllThings.OfType<Blueprint_Build>().Any(), "Blocked terrain should preserve the colony and emit no work.");
                PrepareBuildableSite();
                component.PreviewBase();
            }
            Check(component.BaseProjects.Count > 0, "No initial base modules planned: " + component.BaseStatus);
            int count = component.BaseProjects.Count; component.PreviewBase();
            Check(component.BaseProjects.Count == count, "Preview duplicated base modules.");
            Check(component.BaseProjects.Where(p => p.Kind == "Quarto").All(p => p.InteriorSize == 5 && p.Shell.Count == 24), "Bedroom must have 5x5 interior and 24 perimeter cells.");
            foreach (RoomProject bedroom in component.BaseProjects.Where(p => p.Kind == "Quarto"))
            {
                ConstructionTask bed = bedroom.Furniture.Single(t => t.Def == ThingDefOf.Bed);
                Check(bedroom.ReserveDoubleBed && GenAdj.OccupiedRect(bed.Position, bed.Rotation, ThingDefOf.Bed.size).All(bedroom.DoubleBedSpace.Contains), "Single bed is outside the reserved double-bed area.");
                ThingDef doubleBed = DefDatabase<ThingDef>.GetNamed("DoubleBed");
                Check(GenAdj.OccupiedRect(bedroom.Origin + new IntVec3(5, 0, 5), Rot4.South, doubleBed.size).All(bedroom.DoubleBedSpace.Contains), "Future double bed does not fit.");
                Check(component.BaseProjects.SelectMany(p => p.Furniture).Where(t => t.Def is ThingDef && t.Def != ThingDefOf.Bed)
                    .All(t => !GenAdj.OccupiedRect(t.Position, t.Rotation, ((ThingDef)t.Def).size).Overlaps(bedroom.DoubleBedSpace)), "Furniture blocks the double-bed reserve.");
            }
            corridor = component.BaseProjects.Single(p => p.Kind == "Corredor");
            Check(corridor.InteriorSize == 2 && corridor.Shell.Count(t => t.Def == ThingDefOf.Door && t.Rotation == Rot4.North) == 2, "Corridor requires width two and two exterior exits.");
            foreach (RoomProject room in component.BaseProjects.Where(p => p.RequiresRoof && p != corridor))
            {
                ConstructionTask door = room.Shell.Single(t => t.Def == ThingDefOf.Door);
                Check(corridor.Interior.Contains(door.Position + IntVec3.East) || corridor.Interior.Contains(door.Position + IntVec3.West), "A room doorway does not meet the corridor.");
                Check(!room.Interior.Overlaps(corridor.Interior), "Room/corridor interiors overlap.");
                if (room.Kind == "Freezer")
                {
                    Check(!room.Shell.Any(t => t.Def.defName == "Vent"), "Freezer must not exchange air with the heated corridor.");
                    continue;
                }
                ConstructionTask vent = room.Shell.Single(t => t.Def.defName == "Vent");
                Check((room.Interior.Contains(vent.Position + IntVec3.East) && corridor.Interior.Contains(vent.Position + IntVec3.West)) ||
                    (room.Interior.Contains(vent.Position + IntVec3.West) && corridor.Interior.Contains(vent.Position + IntVec3.East)), "Vent blocked by a double wall.");
            }
            Check(component.BaseProjects.All(p => p.Shell.Concat(p.Furniture).All(t => t.Pending == null)), "Preview unexpectedly emitted blueprints.");
            var flags = map.listerThings.AllThings.Where(t => t.def == ThingDefOf.WoodLog).ToDictionary(t => t, t => t.IsForbidden(Faction.OfPlayer));
            foreach (Thing item in flags.Keys) item.SetForbidden(true, false);
            string blocked = BaseConstructionManager.Apply(map, component.BaseProjects.ToList());
            Check(!component.BaseProjects.SelectMany(p => p.Shell.Concat(p.Furniture)).Any(t =>
                t.Pending?.Spawned == true && CostListCalculator.CostListAdjusted(t.Def, t.Stuff).Any(c => c.thingDef == ThingDefOf.WoodLog)),
                "Forbidden wood financed a new blueprint: " + blocked);
            foreach (var entry in flags) entry.Key.SetForbidden(entry.Value, false);
            Resource(ThingDefOf.WoodLog, 800); Resource(ThingDefOf.Steel, 300); Resource(ThingDefOf.ComponentIndustrial, 20);
            playerBlueprint = GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Wall, EmptyCell(), map, Rot4.North, Faction.OfPlayer, ThingDefOf.WoodLog);
            int beforeCount = component.BaseProjects.SelectMany(p => p.Shell.Concat(p.Furniture))
                .Where(t => t.Pending?.Spawned == true).Select(t => t.Pending).Distinct().Count();
            component.SetBaseAutomation(true);
            var tasks = component.BaseProjects.SelectMany(p => p.Shell.Concat(p.Furniture)).ToList();
            int afterCount = tasks.Where(t => t.Pending?.Spawned == true).Select(t => t.Pending).Distinct().Count();
            Check(afterCount > 0 && afterCount - beforeCount <= 6, "Initial blueprint batch invalid: " + component.BaseStatus);
            BaseConstructionManager.Apply(map, component.BaseProjects.ToList());
            BaseConstructionManager.Apply(map, component.BaseProjects.ToList());
            Check(tasks.Where(t => t.Pending?.Spawned == true).Select(t => t.Pending).Distinct().Count() <= BaseConstructionManager.MaxPending, "Construction queue exceeded its limit.");
            Check(tasks.Where(t => t.Pending?.Spawned == true).GroupBy(t => t.Position).All(g => g.Select(t => t.Pending).Distinct().Count() == 1), "Duplicated blueprint cells.");
            wall = tasks.First(t => t.Def == ThingDefOf.Wall && t.Pending is Blueprint_Build);
            Frame frame = PrepareFrame(wall);
            worker.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
            Check(worker.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.FinishFrame, frame), JobTag.Misc, false), "Native pawn construction job refused.");
        }

        private void VerifyModules()
        {
            RoomProject bedroom = component.BaseProjects.First(p => p.Kind == "Quarto");
            CompleteFixture(bedroom);
            BaseConstructionManager.Apply(map, new List<RoomProject> { bedroom });
            Check(bedroom.Completed && bedroom.Interior.Count() == 25 && bedroom.Furniture.All(t => t.Complete(map)), "Completed bedroom not recognized.");
            foreach (var kind in new[] { "Estoque", "Cozinha" })
            {
                RoomProject project = component.BaseProjects.FirstOrDefault(p => p.Kind == kind);
                if (project == null)
                {
                    project = null;
                    foreach (IntVec3 candidate in new CellRect(0, 0, map.Size.x, map.Size.z).Cells.Where(c => c.x % 4 == 0 && c.z % 4 == 0))
                            if (BasePlanner.ClearSite(map, new CellRect(candidate.x, candidate.z, kind == "Estoque" ? 8 : 6, kind == "Estoque" ? 8 : 6), component.BaseProjects))
                            {
                                project = BasePlanner.CreateRoom(map, candidate, kind == "Estoque" ? 6 : 4, kind, ThingDefOf.WoodLog);
                                if (project != null) break;
                            }
                }
                Check(project != null, "No valid " + kind + " fixture.");
                CompleteFixture(project);
                BaseConstructionManager.Apply(map, new List<RoomProject> { project });
                Check(project.Completed, "Module completion failed for " + kind);
                if (kind == "Estoque")
                {
                    Check(project.Stockpile != null && project.Stockpile.CellCount == 35, "Stockpile must reserve the interior doorway cell.");
                    Check(!project.Stockpile.GetStoreSettings().filter.Allows(DefDatabase<ThingDef>.GetNamed("Chemfuel")), "General stockpile must exclude chemfuel.");
                }
                else Check(project.Interior.Count() == 16 && project.Furniture.All(t => t.Complete(map) && t.Pending?.Spawned != true), "Kitchen benches not placed with valid footprints.");
            }
            // A player's cancellation must pause rather than silently recreate a blueprint.
            RoomProject cancelled = component.BaseProjects.First(p => !p.Completed && p.Kind == "Quarto");
            BaseConstructionManager.Apply(map, new List<RoomProject> { cancelled });
            ConstructionTask task = cancelled.Shell.First(t => t.Pending is Blueprint_Build bp && bp.Spawned);
            task.Pending.Destroy(DestroyMode.Cancel);
            BaseConstructionManager.Apply(map, new List<RoomProject> { cancelled });
            Check(cancelled.State == ConstructionState.Paused && cancelled.BlockReason.Contains("cancelamento"), "Cancelled player project was recreated.");
            component.SetEquipmentAutomation(true);
            component.SetAutomation(true, true);
            component.DisableAll();
            Check(!component.BaseAutomation && !component.EquipmentAutomation && !component.FoodAutomation && !component.WorkAutomation, "Disable-all did not switch every function off.");
            Check(playerBlueprint.Spawned && wall.Complete(map), "Disable-all affected player blueprint/completed construction.");
        }

        private void VerifyCompact()
        {
            foreach (RoomProject project in component.BaseProjects.Where(p => p.RequiresRoof && p != corridor)) CompleteFixture(project);
            CompleteFixture(corridor);
            RoomProject power = component.BaseProjects.Single(p => p.Kind == "Energia e climatização");
            CompleteFixture(power);
            foreach (ConstructionTask task in power.Furniture.Where(t => t.Def.defName == "WoodFiredGenerator"))
                task.Position.GetThingList(map).OfType<ThingWithComps>().Single(t => t.def == task.Def).TryGetComp<CompRefuelable>().Refuel(75f);
            RoomProject comfort = component.BaseProjects.Single(p => p.Kind == "Conforto dos quartos");
            CompleteFixture(comfort);
            RoomProject floors = component.BaseProjects.Single(p => p.Kind == "Pisos e acabamento");
            CompleteFixture(floors);
            Check(floors.Furniture.All(t => t.Complete(map)), "Native floor frame completion did not install the planned flooring.");
            BaseConstructionManager.Apply(map, new List<RoomProject> { corridor, power, comfort });
            Check(!power.Interior.Any(c => map.areaManager.BuildRoof[c]), "Outdoor generator was accidentally roofed.");
            foreach (ConstructionTask cooler in corridor.Shell.Where(t => t.Def.defName == "Cooler"))
            {
                IntVec3 hot = cooler.Position + IntVec3.North.RotatedBy(cooler.Rotation);
                IntVec3 cold = cooler.Position + IntVec3.South.RotatedBy(cooler.Rotation);
                Check(!hot.Roofed(map) && !hot.Impassable(map) && corridor.Interior.Contains(cold), "Cooler exhaust or cold outlet points into a wall/roof.");
            }
            foreach (RoomProject room in component.BaseProjects.Where(p => p.RequiresRoof))
                room.Interior.CenterCell.GetRoom(map).Temperature = 5f;
            thermalStart = corridor.Interior.CenterCell.GetTemperature(map);
        }

        private void VerifyHeat()
        {
            var powerProject = component.BaseProjects.Single(p => p.Kind == "Energia e climatização");
            foreach (ConstructionTask task in powerProject.Furniture.Where(t => (t.Def.defName == "PowerConduit" || t.Def.defName == "HiddenConduit")))
                Check(task.Complete(map), "Conduit disappeared during furnishing/flooring: " + task.Position);
            Check(!corridor.Interior.CenterCell.GetRoom(map).UsesOutdoorTemperature, "Corridor is not an enclosed temperature-controlled room.");
            Check(corridor.Interior.CenterCell.GetTemperature(map) > thermalStart, "Powered heaters did not increase corridor temperature.");
            foreach (ConstructionTask task in component.BaseProjects.SelectMany(p => p.Shell.Concat(p.Furniture)).Where(t => t.TargetTemperature > -999f))
            {
                ThingWithComps building = task.Position.GetThingList(map).OfType<ThingWithComps>().Single(t => t.def == task.Def);
                Check(building.TryGetComp<CompPowerTrader>().PowerOn, "Thermostat is disconnected/unpowered: " + task.Def.defName);
                Check(building.TryGetComp<CompTempControl>().TargetTemperature == task.TargetTemperature, "Thermostat setpoint incorrect.");
            }
            foreach (ConstructionTask task in component.BaseProjects.SelectMany(p => p.Furniture).Where(t => t.Def.defName == "StandingLamp"))
            {
                ThingWithComps lamp = task.Position.GetThingList(map).OfType<ThingWithComps>().Single(t => t.def == task.Def);
                CompPowerTrader electricity = lamp.TryGetComp<CompPowerTrader>();
                Check(electricity.PowerOn, $"A room lamp is off at {task.Position}: net={electricity.PowerNet != null}, connection={electricity.connectParent?.parent.Position}, flick={lamp.TryGetComp<CompFlickable>()?.SwitchIsOn}; powered generators=" +
                    string.Join(",", powerProject.Furniture.Where(t => t.Def.defName == "WoodFiredGenerator").Select(t => {
                        var generator = t.Position.GetThingList(map).OfType<ThingWithComps>().Single(b => b.def == t.Def);
                        var trader = generator.TryGetComp<CompPowerTrader>();
                        return t.Position + ":" + trader.PowerOn + "/sameNet=" + (trader.PowerNet == electricity.PowerNet);
                    })));
            }
            foreach (RoomProject room in component.BaseProjects.Where(p => p.Kind == "Quarto"))
            {
                var bed = room.Furniture.Single(t => t.Def == ThingDefOf.Bed).Position.GetThingList(map).OfType<Building_Bed>().Single();
                Check(bed.GetSleepingSlotPos(0).z == room.Interior.maxZ, "Bed head is not against the north wall.");
                Check(bed.TryGetComp<CompAffectedByFacilities>().LinkedFacilitiesListForReading.Any(t => t.def.defName == "EndTable") &&
                    bed.TryGetComp<CompAffectedByFacilities>().LinkedFacilitiesListForReading.Any(t => t.def.defName == "Dresser"), "Comfort furniture failed to link to a bed.");
                var pots = room.Interior.SelectMany(c => c.GetThingList(map)).OfType<Building_PlantGrower>().ToList();
                Check(pots.Count == 1 && pots[0].GetPlantDefToGrow().defName == "Plant_Daylily", "Bedroom pot is missing or unconfigured.");
            }
            foreach (RoomProject room in component.BaseProjects.Where(p => p.RequiresRoof)) room.Interior.CenterCell.GetRoom(map).Temperature = 40f;
            thermalStart = corridor.Interior.CenterCell.GetTemperature(map);
        }
    }
}
