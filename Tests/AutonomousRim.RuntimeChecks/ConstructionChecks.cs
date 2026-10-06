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
        public ConstructionChecks(Map map) : base(map) { }
        public static void InspectorDrawn() { draws++; }
        private void Finish()
        {
            foreach (var entry in drafts) entry.Key.drafter.Drafted = entry.Value;
            stage = 99;
            Log.Message("[AutonomousRim.ConstructionTests] PASS: preview, budget gate, valid room sizes/footprints/interactions, bounded native blueprints, real pawn wall and full bedroom construction including bed/roof, module completion/stockpile, duplicate prevention, player cancellation and toggle cleanup.");
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
            foreach (IntVec3 cell in GenAdj.OccupiedRect(task.Position, task.Rotation, task.Def.size))
                foreach (Plant plant in cell.GetThingList(map).OfType<Plant>().ToList()) plant.Destroy();
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
        private void CompleteFixture(RoomProject project)
        {
            // Accelerates prerequisites only; the first wall is separately completed by a real pawn job.
            foreach (ConstructionTask task in project.Shell.Concat(project.Furniture))
                if (!task.Complete(map))
                {
                    PrepareFrame(task).CompleteConstruction(worker);
                }
            foreach (IntVec3 cell in project.Interior) map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
        }

        public override void MapComponentTick()
        {
            int ticks = Find.TickManager.TicksGame;
            if (stage == 99 || !GenCommandLine.CommandLineArgPassed("autonomousrimtest") || ticks < 1400 || ticks % 20 != 0) return;
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
                        Log.Message("[AutonomousRim.ConstructionTests] Native room: " + progress);
                    }
                    if (trialRoom.Completed)
                    {
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
            Check(!component.BaseAutomation, "Base automation must default to off.");
            worker = map.mapPawns.FreeColonistsSpawned.First(p => WorkPriorityManager.CanWork(p) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction));
            foreach (Pawn other in map.mapPawns.FreeColonistsSpawned.Where(p => p != worker))
            { drafts[other] = other.Drafted; other.drafter.Drafted = true; }
            worker.drafter.Drafted = false;
            worker.needs.food.CurLevel = worker.needs.food.MaxLevel; worker.needs.rest.CurLevel = 1f;
            worker.skills.GetSkill(SkillDefOf.Construction).Level = 20;
            worker.workSettings.EnableAndInitializeIfNotAlreadyInitialized(); worker.workSettings.SetPriority(WorkTypeDefOf.Construction, 1);
            worker.jobs.ClearQueuedJobs(true); worker.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
            component.PreviewBase();
            Check(component.BaseProjects.Count > 0, "No initial base modules planned: " + component.BaseStatus);
            int count = component.BaseProjects.Count; component.PreviewBase();
            Check(component.BaseProjects.Count == count, "Preview duplicated base modules.");
            Check(component.BaseProjects.Where(p => p.Kind == "Quarto").All(p => p.InteriorSize == 5 && p.Shell.Count == 24), "Bedroom must have 5x5 interior and 24 perimeter cells.");
            Check(component.BaseProjects.All(p => p.Shell.Concat(p.Furniture).All(t => t.Pending == null)), "Preview unexpectedly emitted blueprints.");
            var flags = map.listerThings.AllThings.Where(t => t.def == ThingDefOf.WoodLog).ToDictionary(t => t, t => t.IsForbidden(Faction.OfPlayer));
            foreach (Thing item in flags.Keys) item.SetForbidden(true, false);
            string blocked = BaseConstructionManager.Apply(map, component.BaseProjects.ToList());
            Check(blocked.Contains("faltam") && component.BaseProjects.All(p => !p.Started), "Material budget did not block unfunded construction: " + blocked);
            foreach (var entry in flags) entry.Key.SetForbidden(entry.Value, false);
            Resource(ThingDefOf.WoodLog, 800); Resource(ThingDefOf.Steel, 300); Resource(ThingDefOf.ComponentIndustrial, 20);
            playerBlueprint = GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Wall, EmptyCell(), map, Rot4.North, Faction.OfPlayer, ThingDefOf.WoodLog);
            component.SetBaseAutomation(true);
            var tasks = component.BaseProjects.SelectMany(p => p.Shell.Concat(p.Furniture)).ToList();
            Check(tasks.Count(t => t.Pending?.Spawned == true) > 0 && tasks.Count(t => t.Pending?.Spawned == true) <= 6, "Initial blueprint batch invalid: " + component.BaseStatus);
            BaseConstructionManager.Apply(map, component.BaseProjects.ToList());
            BaseConstructionManager.Apply(map, component.BaseProjects.ToList());
            Check(tasks.Count(t => t.Pending?.Spawned == true) <= BaseConstructionManager.MaxPending, "Construction queue exceeded its limit.");
            Check(tasks.Where(t => t.Pending?.Spawned == true).Select(t => t.Position).Distinct().Count() == tasks.Count(t => t.Pending?.Spawned == true), "Duplicated blueprint cells.");
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
                if (kind == "Estoque") Check(project.Stockpile != null && project.Stockpile.CellCount == 36, "Stockpile zone not created.");
                else Check(project.Interior.Count() == 16 && project.Furniture.All(t => t.Complete(map) && t.Pending?.Spawned != true), "Kitchen benches not placed with valid footprints.");
            }
            // A player's cancellation must pause rather than silently recreate a blueprint.
            RoomProject cancelled = component.BaseProjects.First(p => !p.Completed && p.Kind == "Quarto");
            BaseConstructionManager.Apply(map, new List<RoomProject> { cancelled });
            ConstructionTask task = cancelled.Shell.First(t => t.Pending is Blueprint_Build bp && bp.Spawned);
            task.Pending.Destroy(DestroyMode.Cancel);
            Check(BaseConstructionManager.Apply(map, new List<RoomProject> { cancelled }).Contains("cancelamento"), "Cancelled player project was recreated.");
            component.SetEquipmentAutomation(true);
            component.SetAutomation(true, true);
            component.DisableAll();
            Check(!component.BaseAutomation && !component.EquipmentAutomation && !component.FoodAutomation && !component.WorkAutomation, "Disable-all did not switch every function off.");
            Check(playerBlueprint.Spawned && wall.Complete(map), "Disable-all affected player blueprint/completed construction.");
        }
    }
}
