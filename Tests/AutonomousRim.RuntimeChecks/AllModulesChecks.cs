using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using AutonomousRim.Planning;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    // Admin construction audit. Instant research and buildings are fixture operations,
    // never evidence of construction labor, progression or survival from scratch.
    public sealed class AllModulesChecks : MapComponent
    {
        private bool started, done, captured;
        private int start, rounds, settle, lastTaskCount = -1, steady;
        private static bool reloadPending;
        private float captureAt;
        private List<RoomProject> projects;
        private AutonomousRimMapComponent ai;
        private Thing debrisChunk;
        private RoomProject debrisProject;
        public AllModulesChecks(Map map) : base(map) { }
        private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        private static string Key(ConstructionTask t) => t.Position + "/" + t.Def.defName + "/" + t.Rotation.AsInt;
        public override void MapComponentTick()
        {
            if (done || !GenCommandLine.CommandLineArgPassed("autonomousrimallmodulestest") || !EliteFixtureBaseline.Ready || Find.TickManager.TicksGame < 300) return;
            try
            {
                if (reloadPending)
                {
                    if (!started) { started = true; start = Find.TickManager.TicksGame; return; }
                    if (Find.TickManager.TicksGame - start < 600) return;
                    reloadPending = false;
                    ai = map.GetComponent<AutonomousRimMapComponent>(); projects = (List<RoomProject>)ai.BaseProjects;
                    BaseConstructionManager.Apply(map, projects); AuditGeometry(); AuditOperation();
                    done = true; Find.TickManager.CurTimeSpeed = TimeSpeed.Paused; captureAt = Time.realtimeSinceStartup + 2;
                    Log.Message("[AllModules] PASS: native physical geometry, access, power, settings and save verified after native save/load; admin fixture, not natural construction.");
                    return;
                }
                if (GenCommandLine.CommandLineArgPassed("autonomousrimallmodulesresume"))
                {
                    ai = map.GetComponent<AutonomousRimMapComponent>(); projects = (List<RoomProject>)ai.BaseProjects;
                    if (!projects.Any(p => p.Kind == ModularBasePlanner.ReservationKind)) return;
                    if (!started) { started = true; start = Find.TickManager.TicksGame; return; }
                    if (Find.TickManager.TicksGame - start < 600) return;
                    AuditGeometry(); AuditOperation();
                    GameDataSaveLoader.SaveGame("AutonomousRim-Modulos-Admin");
                    done = true; captureAt = Time.realtimeSinceStartup + 2;
                    Log.Message("[AllModules] PASS: native physical geometry, access, power, settings and save verified after native reload; admin fixture, not natural construction.");
                    return;
                }
                if (!started) Setup();
                // Some research completion signals/scenario events can spawn mechs
                // even with Peaceful. They are outside this construction-only fixture.
                foreach (var hostile in map.mapPawns.AllPawnsSpawned.Where(p => p.HostileTo(Faction.OfPlayer)).ToList())
                { hostile.Destroy(DestroyMode.Vanish); }
                if (Find.TickManager.TicksGame % 120 != 0) return;
                if (settle == 0)
                {
                    rounds++;
                    ModularBasePlanner.Plan(map, projects);
                    PrisonPlanner.Plan(map, projects, ai.Prison);
                    Materialize();
                    BaseConstructionManager.Apply(map, projects);
                    var state = ColonyStateScanner.Scan(map);
                    StoragePolicy.ManageFoodStorage(map, state, projects);
                    Materialize(); BaseConstructionManager.Apply(map, projects);
                    int count = projects.Sum(p => BaseConstructionManager.Tasks(p).Count());
                    steady = count == lastTaskCount ? steady + 1 : 0; lastTaskCount = count;
                    Log.Message("[AllModules] BUILD round=" + rounds + " projects=" + projects.Count + " tasks=" + count + " stable=" + steady);
                    if (rounds >= 20 && steady >= 3) { AuditGeometry(); CheckReferenceEntrances(); CheckUpgradeControls(); BeginDebrisCheck(); settle = Find.TickManager.TicksGame; }
                    Check(rounds < 40, "Plan keeps adding tasks instead of reaching a stable built layout.");
                }
                else if (Find.TickManager.TicksGame - settle >= 6000)
                {
                    Check(debrisChunk?.Spawned==true && projects.First(p=>p.Kind=="Despejo").Stockpile.Cells.Contains(debrisChunk.Position),"Native colonist failed to haul obstructing stone to the dump.");
                    projects.Remove(debrisProject);
                    Log.Message("[AllModules] PASS: obstructing stone physically hauled by a colonist to the filtered dump; no teleport.");
                    AuditOperation();
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    GameDataSaveLoader.SaveGame("AutonomousRim-Modulos-Admin");
                    reloadPending = true; GameDataSaveLoader.LoadGame("AutonomousRim-Modulos-Admin");
                    return;
                }
                Check(Find.TickManager.TicksGame - start < 24000, "Audit deadline exceeded.");
            }
            catch (Exception e)
            {
                done = true; Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                GameDataSaveLoader.SaveGame("AllModulesFailure");
                Log.Error("[AllModules] FAIL: " + e);
            }
        }
        public override void MapComponentUpdate()
        {
            if (!GenCommandLine.CommandLineArgPassed("autonomousrimallmodulestest") || !done) return;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            if (captureAt <= 0 || Time.realtimeSinceStartup < captureAt) return;
            string file = Path.Combine(GenFilePaths.SaveDataFolderPath, "AllModules.png");
            if (!captured) { ScreenCapture.CaptureScreenshot(file); captured = true; return; }
            if (File.Exists(file)) { captureAt = 0; Log.Message("[AllModules] DONE"); }
        }
        private void Setup()
        {
            EliteFixtureBaseline.Roster(map);
            ai = map.GetComponent<AutonomousRimMapComponent>(); ai.DisableAll();
            var peaceful = DefDatabase<DifficultyDef>.GetNamed("Peaceful");
            Find.Storyteller.difficultyDef = peaceful; Find.Storyteller.difficulty.CopyFrom(peaceful);
            var original = map.mapPawns.FreeColonistsSpawned.ToList();
            foreach (var p in map.mapPawns.AllPawnsSpawned.Where(p => !original.Contains(p)).ToList())
            { p.Destroy(DestroyMode.Vanish); }
            // Uniform land isolates layout defects. Irregular soil/mining must be tested separately.
            foreach (var cell in new CellRect(20, 20, map.Size.x - 40, map.Size.z - 40))
            {
                foreach (var t in cell.GetThingList(map).Where(t => !(t is Pawn)).ToList())
                    if (t.def.destroyable) t.Destroy(DestroyMode.Vanish); else t.DeSpawn();
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil); map.roofGrid.SetRoof(cell, null); map.fogGrid.Unfog(cell);
            }
            foreach (var z in map.zoneManager.AllZones.ToList()) z.Delete();
            foreach (var r in DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r => !r.IsFinished).ToList()) Find.ResearchManager.FinishProject(r, false, null, false);
            foreach (var p in original)
            {
                p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                p.Position = map.Center + new IntVec3(-10 + original.IndexOf(p), 0, -10);
                p.workSettings.SetPriority(WorkTypeDefOf.Construction, 1); p.workSettings.SetPriority(WorkTypeDefOf.Growing, 1);
                p.needs.food.CurLevelPercentage = 1; p.needs.rest.CurLevelPercentage = 1;
            }
            projects = (List<RoomProject>)ai.BaseProjects; projects.Clear();
            SpawnItem("MealSurvivalPack", map.Center + new IntVec3(-8, 0, -12), 100);
            SpawnItem("MedicineIndustrial", map.Center + new IntVec3(-7, 0, -12), 75);
            SpawnItem("ChunkSlagSteel", map.Center + new IntVec3(-6, 0, -12), 1);
            // Real stock pressure exercises the normal shelf-demand calculation.
            for (int i = 0; i < 150; i++) SpawnItem("Steel", new IntVec3(35 + i % 20, 0, 35 + i / 20), 75);
            for (int i = 0; i < 60; i++) SpawnItem("MealSurvivalPack", new IntVec3(60 + i % 15, 0, 35 + i / 15), 10);
            // Force refinery demand, without changing how the planner places the bench.
            var generator = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ChemfuelPoweredGenerator"));
            generator.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(generator, new IntVec3(25, 0, 25), map);
            start = Find.TickManager.TicksGame; started = true;
            Log.Message("[AllModules] SETUP: six original colonists, peaceful, flat disposable map, all research instant; automatic layout still supplied by production planners.");
        }
        private void SpawnItem(string name, IntVec3 cell, int count)
        {
            var thing = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name));
            thing.stackCount = Math.Min(count, thing.def.stackLimit); GenSpawn.Spawn(thing, cell, map);
            if (thing is ThingWithComps) thing.SetForbidden(false);
        }
        private void BeginDebrisCheck()
        {
            var source=new IntVec3(30,0,55);
            debrisChunk=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ChunkGranite"));GenSpawn.Spawn(debrisChunk,source,map);if(debrisChunk is ThingWithComps)debrisChunk.SetForbidden(true);
            debrisProject=new RoomProject{Kind="Teste de limpeza",LayoutSlot="test:stone",Origin=source,InteriorSize=1,RequiresRoof=false};
            debrisProject.Furniture.Add(new ConstructionTask{Def=DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor"),Position=source,OriginalTerrain=source.GetTerrain(map)});
            projects.Add(debrisProject);
            var worker=map.mapPawns.FreeColonistsSpawned.First();
            worker.jobs.ClearQueuedJobs(true);worker.jobs.EndCurrentJob(JobCondition.InterruptForced,false);worker.needs.food.CurLevelPercentage=1;worker.needs.rest.CurLevelPercentage=1;worker.needs.joy.CurLevelPercentage=1;
            worker.workSettings.SetPriority(WorkTypeDefOf.Hauling,1);worker.workSettings.SetPriority(WorkTypeDefOf.Growing,0);
            for(int h=0;h<24;h++)worker.timetable.SetAssignment(h,TimeAssignmentDefOf.Anything);
            var job=ConstructionWorkManager.ConstructionDebrisJob(worker,projects);
            Log.Message("[AllModules] STONE prerequisites: dispatch="+ConstructionWorkManager.CanDispatch(worker,WorkTypeDefOf.Hauling)+" floorSize="+debrisProject.Furniture[0].Def.Size+
                " sourceReach="+worker.CanReserveAndReach(debrisChunk,PathEndMode.Touch,Danger.None)+" dumpAccept="+projects.First(p=>p.Kind=="Despejo").Stockpile.GetStoreSettings().AllowedToAccept(debrisChunk)+" job="+job?.def.defName);
            Check(job?.targetA.Thing==debrisChunk && job.def==JobDefOf.HaulToCell,"No native hauling job for stone blocking a planned floor.");
            job.workGiverDef=DefDatabase<WorkGiverDef>.GetNamed("HaulGeneral");
            worker.jobs.StartJob(job,JobCondition.InterruptForced);
            Log.Message("[AllModules] STONE: native haul issued from "+source+" to "+job.targetB.Cell);
        }
        private void CheckUpgradeControls()
        {
            var cell=new IntVec3(30,0,60);
            var old=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SimpleResearchBench"),ThingDefOf.WoodLog);
            old.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(old,cell,map);
            var target=DefDatabase<ThingDef>.GetNamed("MultiAnalyzer");
            var task=new ConstructionTask{Def=old.def,Stuff=ThingDefOf.WoodLog,Position=cell,Owned=true,Issued=true,WasCompleted=true,UpgradeTo=target};
            var room=new RoomProject{Kind="Teste de upgrade",LayoutSlot="test:upgrade",Origin=cell,InteriorSize=1,RequiresRoof=false};room.Furniture.Add(task);projects.Add(room);
            BaseConstructionManager.Apply(map,projects);
            Check(old.Spawned && map.designationManager.DesignationOn(old,DesignationDefOf.Deconstruct)==null,"Usable research bench demolished without replacement resources.");
            int i=0;
            foreach(var cost in CostListCalculator.CostListAdjusted(target,null))
                for(int left=cost.count;left>0;left-=cost.thingDef.stackLimit)SpawnItem(cost.thingDef.defName,new IntVec3(34+i++,0,60),Math.Min(left,cost.thingDef.stackLimit));
            BaseConstructionManager.Apply(map,projects);
            Check(task.OwnsUpgradeDesignation && map.designationManager.DesignationOn(old,DesignationDefOf.Deconstruct)!=null,"Funded native upgrade did not designate old bench for deconstruction.");
            map.designationManager.DesignationOn(old,DesignationDefOf.Deconstruct).Delete();
            BaseConstructionManager.Apply(map,projects);
            Check(task.UpgradeTo==null && task.UpgradeDeclined && old.Spawned,"Manual cancellation of upgrade not respected.");
            task.UpgradeDeclined=false;task.UpgradeTo=target;room.Completed=false;
            BaseConstructionManager.Apply(map,projects);BaseConstructionManager.Stop(map,new List<RoomProject>{room});
            Check(old.Spawned && map.designationManager.DesignationOn(old,DesignationDefOf.Deconstruct)==null,"Stopping construction left automatic demolition active.");
            projects.Remove(room);old.Destroy(DestroyMode.Vanish);
            Log.Message("[AllModules] PASS: upgrade keeps old bench without resources, uses native demolition after funding, respects cancellation and stops owned demolition.");
        }
        private void CheckReferenceEntrances()
        {
            var origin=new IntVec3(30,0,70);
            var tree=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Plant_TreeOak"));
            var rock=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Granite"));
            GenSpawn.Spawn(tree,origin+new IntVec3(6,0,-1),map);GenSpawn.Spawn(rock,origin+new IntVec3(13,0,3),map);
            var scratch=new List<RoomProject>{new RoomProject{Kind=ModularBasePlanner.ReservationKind,LayoutSlot="modular-root",Origin=origin,LayoutAnchor=origin,InteriorSize=11,RequiresRoof=false,Completed=true}};
            ReferenceModulePlanner.AddEnergy(map,scratch,origin);
            Check(scratch.Any(p=>p.Kind=="Preparação do terreno" && p.PlantCells.Contains(tree.Position)),"Actual reference entrance tree was omitted from clearing plan.");
            Check(scratch.Any(p=>p.Kind=="Preparação do terreno" && p.MineCells.Contains(rock.Position)) &&
                scratch.Any(p=>p.ReferenceLayout=="energy_bank" && p.NoRoofCells.Contains(rock.Position)),"External energy vent blocked by rock or missing roof exclusion.");
            tree.Destroy(DestroyMode.Vanish);rock.Destroy(DestroyMode.Vanish);
            Log.Message("[AllModules] PASS: tree at the real reference entrance and rock at the external energy vent receive normal clearing/mining plans.");
        }
        private void Materialize()
        {
            var tasks = projects.SelectMany(BaseConstructionManager.Tasks).Where(t => t.Def != null).GroupBy(Key).Select(g => g.First()).ToList();
            // Native order: supporting walls, floors, equipment, then cables and wall attachments.
            foreach (var task in tasks.OrderBy(t => t.Def is TerrainDef ? 1 : t.Def.defName == "Wall" || t.Def is ThingDef d && d.IsDoor ? 0 : t.Def.defName == "WallLamp" ? 4 : t.Def.defName.Contains("Conduit") ? 3 : 2))
            {
                // Admin-only materialization of a production upgrade; normal gameplay
                // uses the executor's native deconstruction and funded blueprint.
                if(task.UpgradeTo!=null)
                {
                    foreach(var old in task.Position.GetThingList(map).Where(b=>b.def==task.Def && b.Position==task.Position).ToList())old.Destroy(DestroyMode.Vanish);
                    task.Def=task.UpgradeTo;task.UpgradeTo=null;
                    task.Stuff=((ThingDef)task.Def).MadeFromStuff?ThingDefOf.WoodLog:null;
                    task.Pending=null;task.WasCompleted=false;task.Issued=false;task.SettingsConfigured=false;
                }
                if (task.Complete(map)) continue;
                if (task.Pending?.Spawned == true) task.Pending.Destroy(DestroyMode.Vanish);
                if (task.Def is TerrainDef floor) { map.terrainGrid.SetTerrain(task.Position, floor); continue; }
                var def = (ThingDef)task.Def;
                var report = GenConstruct.CanPlaceBlueprintAt(def, task.Position, task.Rotation, map, stuffDef: task.Stuff);
                Check(report.Accepted, "Native placement rejected " + def.defName + " at " + task.Position + ": " + report.Reason);
                var thing = ThingMaker.MakeThing(def, task.UpgradeMaterial ?? task.Stuff);
                if (def.CanHaveFaction) thing.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(thing, task.Position, map, task.Rotation);
                task.Owned = true; task.Issued = true; task.WasCompleted = true;
                if (thing is ThingWithComps comp) comp.TryGetComp<CompRefuelable>()?.Refuel(1000);
            }
            foreach (var p in projects)
            {
                if (p.RequiresRoof && p.Shell.All(t => t.Complete(map))) foreach (var c in p.RoofArea) map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                // Completion belongs to the production executor: it must first
                // create zones and apply storage/medical/thermostat settings.
            }
        }
        private void AuditGeometry()
        {
            foreach (string kind in new[] { "Quarto", "Cozinha", "Abate", "Estoque", "Freezer", "Refeitório e recreação", "Energia e climatização", "Pesquisa", "Oficina", "Hospital", "Fabricação", "Laboratório" })
                Check(projects.Any(p => p.Kind == kind), "Missing module: " + kind);
            foreach (string name in ModularFacilities.WorkshopBenches)
                Check(projects.SelectMany(BaseConstructionManager.Tasks).Any(t => (t.Def.defName == name || name=="HandTailoringBench" && t.Def.defName=="ElectricTailoringBench" || name=="FueledSmithy" && t.Def.defName=="ElectricSmithy") && t.Complete(map)), "Missing unlocked workbench: " + name);
            var tables = map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().ToList();
            var worker = map.mapPawns.FreeColonistsSpawned.First();
            foreach (var table in tables)
            {
                Check(table.InteractionCell.Standable(map) && worker.CanReach(table.InteractionCell, PathEndMode.OnCell, Danger.Some), "Workbench inaccessible: " + table.def.defName + " at " + table.Position + " interaction=" + table.InteractionCell+" standable="+table.InteractionCell.Standable(map)+" physical="+worker.CanReach(table.InteractionCell,PathEndMode.OnCell,Danger.Deadly));
                if(!worker.CanReach(table.InteractionCell,PathEndMode.OnCell,Danger.None))
                    Log.Message("[AllModules] NOTE: workbench accessible at Danger.Some; cold transit excluded by Danger.None: "+table.def.defName);
            }
            foreach (var p in projects.Where(p => ModularBasePlanner.IsModular(p) && p.Crop == null))
            {
                Check(BaseConstructionManager.Tasks(p).All(t => t.Complete(map)), "Physical task missing: " + p.Kind);
                // Structural reachability must not classify a cold freezer as sealed.
                // Danger.None also excludes temperature danger for lightly dressed pawns.
                bool physical = p.Interior.Cells.Any(c => c.Standable(map) && worker.CanReach(c, PathEndMode.OnCell, Danger.Deadly));
                Check(physical, "Room sealed/inaccessible: " + p.Kind + " at " + p.Origin + " worker=" + worker.Position);
                if (!p.Interior.Cells.Any(c => c.Standable(map) && worker.CanReach(c, PathEndMode.OnCell, Danger.None)))
                    Log.Message("[AllModules] NOTE: room physically accessible but Danger.None excludes it: " + p.Kind + " temperature=" + p.Interior.CenterCell.GetTemperature(map));
            }
            var crops = projects.Where(p => p.Crop != null).ToList();
            Check(!projects.Where(p => p.Kind == "Gerador auxiliar").Any(p => projects.SelectMany(BaseConstructionManager.Tasks)
                .Any(t => t.Def.defName == "Heater" && p.Interior.Contains(t.Position))), "Auxiliary generator room was given an unnecessary heater.");
            Check(crops.Count >= 5 && crops.All(p => p.GrowingZone?.Cells.Count == p.StorageCells.Count && p.StorageCells.All(c => !c.Roofed(map))), "Growing zones missing, incomplete or roofed.");
            Check(projects.Any(p => p.Kind == "Medicamentos" && p.StorageCells.Count == 2 && p.StorageCells.All(c=>
                p.Stockpile?.Cells.Contains(c)==true || c.GetThingList(map).OfType<Building_Storage>().Any(b=>
                    b.GetStoreSettings().Priority==StoragePriority.Critical && b.GetStoreSettings().filter.Allows(ThingDefOf.MedicineHerbal) &&
                    !b.GetStoreSettings().filter.Allows(ThingDefOf.Steel)))), "Hospital medicine storage missing or incorrectly filtered.");
            Check(map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().Any(b => b.Medical), "No medical beds configured.");
            Check(map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().Any(b => b.ForPrisoners && b.Position.IsInPrisonCell(map)), "No valid prison bed.");
            AuditReference();
            Log.Message("[AllModules] PASS: all twelve room functions, eight workbenches, paths, farm zones, medical storage and prison.");
        }
        private void AuditReference()
        {
            var stores=projects.Where(p=>p.ReferenceLayout=="workshop_storage_a" || p.ReferenceLayout=="workshop_storage_b").ToList();
            Check(stores.Count==2 && Math.Abs(stores[0].Origin.x-stores[1].Origin.x)+Math.Abs(stores[0].Origin.z-stores[1].Origin.z)==ModularBasePlanner.Stride,"Two adjacent complete storage/workshop modules missing.");
            foreach(var store in stores)
            {
                Check(store.Furniture.Count(t=>t.Def.defName=="Shelf" && t.Complete(map))>=6,"Storage lacks interior shelving.");
                var benches=map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Where(b=>store.Interior.Contains(b.Position)).ToList();
                Check(benches.Count>=4 && benches.All(b=>b.OccupiedRect().Any(c=>c.x==store.Interior.minX || c.x==store.Interior.maxX || c.z==store.Interior.minZ || c.z==store.Interior.maxZ)),"Storage workbenches are not along walls.");
            }
            Log.Message("[AllModules] PASS: two adjacent storage/workshops, perimeter benches and interior shelves.");
            var food=projects.First(p=>p.ReferenceLayout=="kitchen_butcher_freezer");
            var kitchen=projects.First(p=>p.Kind=="Cozinha");var butcher=projects.First(p=>p.Kind=="Abate");
            Check(food.Footprint.Contains(kitchen.Interior.CenterCell) && food.Footprint.Contains(butcher.Interior.CenterCell),"Food services detached from freezer.");
            Check(kitchen.Interior.CenterCell.GetRoom(map)!=butcher.Interior.CenterCell.GetRoom(map) && kitchen.Interior.CenterCell.GetRoom(map)!=food.Interior.CenterCell.GetRoom(map),"Cooking, butchering and freezer lack separate rooms.");
            Check(new[]{8,12}.All(z=>food.Shell.Any(t=>t.Position==food.Origin+new IntVec3(1,0,z) && ((ThingDef)t.Def).IsDoor && t.Complete(map))),"Reference airlock doors missing.");
            var bank=projects.First(p=>p.ReferenceLayout=="energy_bank");
            Check(bank.InteriorSize==11 && bank.Height==5 && bank.Furniture.Count(t=>t.Def.defName=="WoodFiredGenerator")==6,"Reference generator bank not compact/full.");
            Check(bank.Furniture.Count(t=>t.Def.defName=="Battery")==7 && bank.Shell.Any(t=>t.Def.defName=="Vent" && t.Position==bank.Origin+new IntVec3(12,0,3)),"Reference batteries or external vent missing.");
            var research=projects.First(p=>p.ReferenceLayout=="research_two_bedrooms");
            Check(research.Furniture.Any(t=>t.Def.defName=="MultiAnalyzer" && t.Position==research.Origin+new IntVec3(10,0,8) && t.Complete(map)),"Analyzer did not replace the old bench in the reference position.");
            Check(!projects.Any(p=>p.Kind=="Laboratório" && p.ReferenceLayout==null),"Detached research lab was added.");
            var hospital=projects.First(p=>p.ReferenceLayout=="hospital");
            Check(hospital.Furniture.Count(t=>t.Def.defName=="HospitalBed" && t.Complete(map))==6 && hospital.Furniture.Count(t=>t.Def.defName=="VitalsMonitor" && t.Complete(map))==6,"Reference advanced hospital incomplete.");
            var leisure=projects.First(p=>p.ReferenceLayout=="dining_recreation");
            Check(leisure.Furniture.Count(t=>t.Def.defName=="Couch")==6 && leisure.Furniture.Any(t=>t.Def.defName=="FlatscreenTelevision"),"Reference advanced recreation incomplete.");
            Check(projects.First(p=>p.Kind=="Despejo").Stockpile.label.StartsWith("Despejo"),"Dump zone still has a generic stockpile name.");
            Log.Message("[AllModules] PASS: reference food block/airlock, compact six-generator/seven-battery bank, analyzer replacement, advanced hospital and recreation.");
        }
        private void AuditOperation()
        {
            var consumers = map.listerBuildings.allBuildingsColonist.OfType<ThingWithComps>().Where(b => b.def.defName != "ChemfuelPoweredGenerator")
                .Where(b => b.TryGetComp<CompPowerTrader>()?.Props.PowerConsumption > 0).ToList();
            Log.Message("[AllModules] DEMAND: rated=" + consumers.Sum(b => b.TryGetComp<CompPowerTrader>().Props.PowerConsumption));
            foreach (var b in consumers.Where(b => !b.TryGetComp<CompPowerTrader>().PowerOn))
            {
                var t = b.TryGetComp<CompPowerTrader>();
                Log.Message("[AllModules] POWER: " + b.def.defName + " " + b.Position + " net=" + (t.PowerNet != null) +
                    " gain=" + t.PowerNet?.CurrentEnergyGainRate() + " parent=" + t.connectParent?.parent.Position +
                    " switch=" + b.TryGetComp<CompFlickable>()?.SwitchIsOn);
            }
            foreach (var b in map.listerBuildings.allBuildingsColonist.Where(b => b.def.defName == "WoodFiredGenerator"))
                Log.Message("[AllModules] GENERATOR: " + b.Position + " output=" + b.TryGetComp<CompPowerPlant>()?.PowerOutput + " fuel=" + b.TryGetComp<CompRefuelable>()?.Fuel);
            Check(consumers.All(b => b.TryGetComp<CompPowerTrader>().PowerOn), "Unpowered: " + string.Join(", ", consumers.Where(b => !b.TryGetComp<CompPowerTrader>().PowerOn).Select(b => b.def.defName + " " + b.Position)));
            Check(map.listerBuildings.allBuildingsColonist.Any(b=>b.def.defName=="HiTechResearchBench" &&
                b.TryGetComp<CompAffectedByFacilities>()?.LinkedFacilitiesListForReading.Any(f=>f.def.defName=="MultiAnalyzer")==true),"Analyzer exists but is not linked to an advanced research bench.");
            var medical=projects.First(p=>p.ReferenceLayout=="hospital");
            Check(medical.Furniture.Where(t=>t.Def.defName=="HospitalBed").All(t=>t.Position.GetThingList(map)
                .OfType<Building_Bed>().Any(b=>b.TryGetComp<CompAffectedByFacilities>()?.LinkedFacilitiesListForReading.Any(f=>f.def.defName=="VitalsMonitor")==true)),"A hospital bed has no linked medical monitor.");
            Log.Message("[AllModules] PASS: native analyzer/research link and all six bedside medical monitor links.");
            var freezer = projects.First(p => p.Kind == "Freezer");
            var cooler = freezer.Shell.First(t => t.Def.defName == "Cooler");
            var actualCooler = cooler.Position.GetThingList(map).OfType<ThingWithComps>().First(b => b.def == cooler.Def);
            Check(Math.Abs(actualCooler.TryGetComp<CompTempControl>().TargetTemperature + 2) < .01f, "Freezer target is not -2 C.");
            Check(freezer.Interior.CenterCell.GetTemperature(map) < 0, "Freezer failed to cool: " + freezer.Interior.CenterCell.GetTemperature(map));
            Check(freezer.Stockpile != null && freezer.Stockpile.GetStoreSettings().filter.Allows(DefDatabase<ThingDef>.GetNamed("MealSimple")), "Freezer does not accept meals.");
            foreach(var stock in projects.Where(p=>p.Kind=="Estoque"))
                Check(stock.Stockpile != null && !stock.Stockpile.GetStoreSettings().filter.Allows(DefDatabase<ThingDef>.GetNamed("MealSimple")), "General storage accepts meals or lacks zone.");
            var shelves = map.listerBuildings.allBuildingsColonist.OfType<Building_Storage>().Where(b => (b.def.defName == "ShelfSmall" || b.def.defName == "Shelf")).ToList();
            Check(shelves.All(b=>b.OccupiedRect().All(c=>c.GetZone(map)==null)),"Floor stockpile overlaps shelf storage.");
            Check(shelves.Count > 0 && shelves.All(b => b.def.building.maxItemsInCell == 3), "Three-stack shelves missing.");
            Log.Message("[AllModules] PASS: " + consumers.Count + " powered consumers; freezer " + freezer.Interior.CenterCell.GetTemperature(map).ToString("0.0") + " C; shelf capacity and filters valid.");
        }
    }
}
