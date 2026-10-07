using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Planning;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    public static class BaseConstructionManager
    {
        public const int MaxActiveProjects = 9, MaxPending = 90, MaxPerCycle = 24, ExecutionInterval = 30, ReviewInterval = 120, StallTicks = 1800;
        public static IEnumerable<ConstructionTask> Tasks(RoomProject p) => p.Shell.Concat(p.Furniture);
        public static bool CanContinueExistingWork(RoomProject p) => p.State == ConstructionState.Active || p.State == ConstructionState.WaitingMaterials;
        public static bool Matches(ConstructionTask task, Thing thing)
        {
            ThingDef material = thing is Blueprint bp ? bp.EntityToBuildStuff() : thing is Frame f ? f.EntityToBuildStuff() : thing.Stuff;
            return thing.def.entityDefToBuild == task.Def && thing.Position == task.Position && thing.Rotation == task.Rotation && material == task.Stuff && thing.Faction == Faction.OfPlayer;
        }
        private static void Track(Map map, ConstructionTask task)
        {
            if (task.Def is TerrainDef && task.OriginalTerrain != null && !task.Complete(map) && task.Position.GetTerrain(map) != task.OriginalTerrain)
            {
                var current=task.Position.GetTerrain(map);
                // Mining can replace natural rough rock before a floor has
                // even been ordered. Refresh that baseline; preserve changes
                // to already-issued work and newly built player flooring.
                if(!task.Issued&&!task.WasCompleted&&current.designationCategory==null)
                {task.OriginalTerrain=current;task.CancelledByPlayer=false;}
                else task.CancelledByPlayer=true;
            }
            if (task.Complete(map)) { task.WasCompleted = true; return; }
            if (task.WasCompleted || !task.Issued) return;
            if (task.Pending?.Spawned == true) { if (!Matches(task, task.Pending)) task.CancelledByPlayer = true; return; }
            // Failure of native construction can replace a frame with a blueprint. Reuse it.
            Thing replacement = task.Position.GetThingList(map).FirstOrDefault(t => (t is Frame || t is Blueprint_Build) && Matches(task, t));
            if (replacement != null) task.Pending = replacement;
            else { task.Pending = null; task.CancelledByPlayer = true; }
        }
        public static Dictionary<ThingDef, int> Available(Map map)
        {
            var budget = map.listerThings.AllThings.Where(t => t.Spawned && t.def.category == ThingCategory.Item && !t.IsForbidden(Faction.OfPlayer) && !t.Position.Fogged(map))
                .GroupBy(t => t.def).ToDictionary(g => g.Key, g => g.Sum(t => t.stackCount));
            foreach (Thing pending in map.listerThings.AllThings.Where(t => t is Blueprint_Build || t is Frame))
                foreach (var cost in pending is Frame f ? f.TotalMaterialCost() : ((Blueprint_Build)pending).TotalMaterialCost())
                { budget.TryGetValue(cost.thingDef, out int held); budget[cost.thingDef] = held - (pending is Frame frame ? frame.ThingCountNeeded(cost.thingDef) : cost.count); }
            return budget;
        }
        public static List<Pawn> Builders(Map map) => map.mapPawns.FreeColonistsSpawned.Where(p => WorkPriorityManager.CanWork(p) &&
            !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction) && p.workSettings.Initialized && p.workSettings.GetPriority(WorkTypeDefOf.Construction) > 0).ToList();
        private static bool CourtyardSupport(Map map, RoomProject p) => p.Kind == "Apoio inicial" &&
            map.GetComponent<Core.AutonomousRimMapComponent>().BaseProjects.Any(r => r.Kind == CourtyardBasePlanner.ReservationKind);
        private static IEnumerable<ConstructionTask> FurnitureStage(Map map, RoomProject p)
        {
            if(p.LayoutSlot=="courtyard-power")
            {
                var projects=map.GetComponent<Core.AutonomousRimMapComponent>().BaseProjects;
                var reservation=projects.FirstOrDefault(r=>r.Kind==CourtyardBasePlanner.ReservationKind);
                if(reservation!=null)
                {
                    var trunk=CourtyardBasePlanner.At(reservation.LayoutAnchor,50,50);
                    var essential=new HashSet<IntVec3>();
                    var targets=p.Furniture.Where(t=>t.Def.defName=="WoodFiredGenerator" ||
                        t.Def.defName=="Heater" && map.mapTemperature.OutdoorTemp<10)
                        .Concat(projects.Where(r=>r.Kind=="Freezer").SelectMany(Tasks).Where(t=>t.Def.defName=="Cooler"));
                    foreach(var t in targets)
                    {
                        for(int x=Math.Min(t.Position.x,trunk.x);x<=Math.Max(t.Position.x,trunk.x);x++)essential.Add(new IntVec3(x,0,trunk.z));
                        for(int z=Math.Min(t.Position.z,trunk.z);z<=Math.Max(t.Position.z,trunk.z);z++)essential.Add(new IntVec3(t.Position.x,0,z));
                    }
                    return p.Furniture.OrderBy(t=>t.Def.defName=="WoodFiredGenerator"?0:
                        t.Def.defName=="PowerConduit" && essential.Contains(t.Position)?1:2);
                }
            }
            if (!CourtyardSupport(map,p)) return p.Furniture;
            // Furnish the occupied colony first. Extra bedrooms are still built
            // by their own projects; their beds must not delay essential shelter.
            var beds = new HashSet<ConstructionTask>(p.Furniture.Where(t => t.Def == ThingDefOf.Bed)
                .Take(map.mapPawns.FreeColonistsSpawnedCount));
            return p.Furniture.Where(t => t.Def != ThingDefOf.Bed || beds.Contains(t))
                .OrderBy(t => t.Def == ThingDefOf.Bed ? 0 : t.Def.defName == "FueledStove" ? 1 : t.Def.defName == "TableButcher" ? 2 : 3);
        }
        public static IEnumerable<ConstructionTask> CurrentStage(Map map, RoomProject p)
        {
            if (p.Shell.Any(t => !t.Complete(map))) return p.Shell;
            var floors = p.Furniture.Where(t => t.Def is TerrainDef).ToList();
            return floors.Any(t => !t.Complete(map)) ? floors : FurnitureStage(map, p);
        }
        private static int ExecutionRank(Map map, RoomProject p) => ModularBasePlanner.IsModular(p) ? ModularBasePlanner.Rank(p.Kind) : CourtyardSupport(map,p) &&
            FurnitureStage(map,p).Any(t => t.Def == ThingDefOf.Bed && !t.Complete(map)) ? -1 : RingBasePlanner.Rank(p.Kind);
        private static IEnumerable<RoomProject> NeededBedrooms(Map map, List<RoomProject> all)
        {
            var bedrooms=all.Where(p=>p.Kind=="Quarto");
            return all.Any(p=>p.Kind==CourtyardBasePlanner.ReservationKind)?bedrooms.Take(map.mapPawns.FreeColonistsSpawnedCount):bedrooms;
        }
        private static void CompleteDoorFloorPlan(Map map, List<RoomProject> projects)
        {
            if (!projects.Any(p => p.Kind == CourtyardBasePlanner.ReservationKind)) return;
            var floor = projects.FirstOrDefault(p => p.Kind == "Pisos e acabamento");
            if (floor == null) return;
            var planned = new HashSet<IntVec3>(floor.Furniture.Where(t => t.Def is TerrainDef).Select(t => t.Position));
            var wood = DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor");
            foreach (var cell in projects.Where(p => p.RequiresRoof).SelectMany(p => p.Shell)
                .Where(t => t.Def == ThingDefOf.Door).Select(t => t.Position).Distinct())
                if (planned.Add(cell))
                {
                    // Doorways are part of the interior path too. These are
                    // ordinary floor tasks, including native costs and labor.
                    floor.Furniture.Add(new ConstructionTask { Def = wood, Position = cell, OriginalTerrain = cell.GetTerrain(map) });
                    floor.Completed = false;
                }
        }
        private static bool DependsOn(Map map, RoomProject p, List<RoomProject> all)
        {
            if (all.Any(r => r.Kind == ModularBasePlanner.ReservationKind))
            {
                // Finish habitable bedrooms before spending their materials on secondary shells.
                // Sibling bedrooms still progress independently when one lacks materials.
                if (p.Kind != "Preparação do terreno" && p.Kind != ModularBasePlanner.ReservationKind && p.Kind != "Quarto" &&
                    all.Any(r => r.Kind == "Quarto" && !r.Completed && r.State != ConstructionState.Paused)) return true;
                if (p.Kind == "Corredor" && all.Any(r => ModularBasePlanner.IsModular(r) && r.Crop == null && !r.Completed)) return true;
                // Validate obstacles per task below. A rock under one wall or
                // workbench must not hold every other funded wall in its room.
                // Service rooms share a ranked queue, so a blocked butcher
                // room cannot prevent a clear stockpile from progressing.
                return false;
            }
            if (p.Kind == "Plantação inicial") return false;
            if (p.Kind != "Preparação do terreno" && p.Kind != RingBasePlanner.ReservationKind && p.Kind != CourtyardBasePlanner.ReservationKind &&
                all.Any(r => r.Kind == "Preparação do terreno" && !r.Completed) &&
                (!all.Any(r => r.Kind == CourtyardBasePlanner.ReservationKind) || Tasks(p).Any(t =>
                    GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size).Any(c => c.GetEdifice(map) is Mineable)))) return true;
            if (p.Crop != null || p.Kind == "Muro externo" || p.Kind == "Roupas" || p.Kind == "Baterias" || p.Kind == "Pesquisa" || p.Kind == "Fabricação" || p.Kind == "Multiuso")
                return all.Any(r => !r.Completed && r.Priority == ConstructionPriority.Critical && r.Kind != "Preparação do terreno") ||
                    NeededBedrooms(map,all).Any(r=>!r.Completed);
            // Population growth can add a second compact block. Its corridors
            // must wait for rooms, never for one another (a dependency cycle).
            if (p.Kind == "Corredor") return all.Where(r => r.RequiresRoof && r.Kind != "Corredor").Any(r => !r.Shell.All(t => t.Complete(map)));
            // Power is independent of enclosing every room; valid reserved cells can be built early.
            if (p.Kind == "Energia e climatização") return false;
            if (p.Kind == "Conforto dos quartos") return all.Where(r => r.Kind == "Quarto").Any(r => !r.Completed);
            if (p.Kind == "Prateleiras") return all.Where(r => r.Kind == "Estoque" || r.Kind == "Freezer" || r.Kind == "Armas").Any(r => !r.Completed);
            if (p.Kind == "Hospital" || p.Kind == "Oficina" || p.Kind == "Armas")
                return NeededBedrooms(map,all).Concat(all.Where(r => r.Kind == "Cozinha" || r.Kind == "Estoque" || r.Kind == "Freezer")).Any(r => !r.Completed);
            return p.Kind == "Pisos e acabamento" && all.Where(r => r.RequiresRoof).Any(r => !r.Completed);
        }
        private static void Storage(Map map, RoomProject p)
        {
            if (p.Kind != "Estoque" && p.Kind != "Freezer" && p.Kind != "Despejo" && p.Kind != "Medicamentos" && p.Kind != "Armas" && p.Kind != "Roupas") return;
            if (p.Stockpile != null) { p.FunctionalStorage = true; return; }
            // Storage works immediately. The shell/roof is still required for weather protection.
            var cells = p.StorageCells.Count > 0 ? p.StorageCells : p.Interior.ToList();
            if (cells.Any(c => c.GetZone(map) != null)) { p.BlockReason = "Zona do jogador ocupa a área de armazenamento."; return; }
            p.Stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(p.Stockpile);
            var doors = p.Shell.Where(t => t.Def == ThingDefOf.Door).Select(t => t.Position).ToList();
            foreach (IntVec3 c in cells.Where(c => !doors.Any(d => c.AdjacentToCardinal(d)))) p.Stockpile.AddCell(c);
            StoragePolicy.Configure(p.Stockpile.GetStoreSettings(), p.Kind);
            p.FunctionalStorage = true;
        }
        private static float Progress(Map map, RoomProject p)
        {
            float result = 0;
            foreach (var t in Tasks(p))
                if (t.Complete(map)) result += 10000;
                else if (t.Pending is Frame f && f.Spawned) result += 100 + f.workDone + f.resourceContainer.Sum(i => i.stackCount);
                else if (t.Pending?.Spawned == true) result += 1;
            return result + (p.RequiresRoof ? p.RoofArea.Count(c => c.Roofed(map)) * 100 : 0);
        }
        private static string PendingReason(Map map, RoomProject p, List<Pawn> builders)
        {
            var pending = Tasks(p).Where(t => t.Pending?.Spawned == true && !t.Complete(map)).ToList();
            if (pending.Count == 0) return "Aguardando etapa/cobertura.";
            if (pending.Any(t => GenAdj.OccupiedRect(t.Position, t.Rotation, t.Def.Size).Any(c => c.GetThingList(map).OfType<Plant>().Any())))
                return "Vegetação na obra: corte nativo antes de entregar/construir.";
            if (!pending.Any(t => builders.Any(b => b.skills.GetSkill(SkillDefOf.Construction).Level >= t.Def.constructionSkillPrerequisite &&
                !t.Pending.IsForbidden(b) && t.Position.IsInAllowedArea(b) && b.CanReach(t.Pending, PathEndMode.Touch, Danger.None))))
                return "Obras sem acesso/área permitida, proibidas ou sem habilidade mínima.";
            foreach (var group in pending.SelectMany(t => t.Pending is Frame f ? f.TotalMaterialCost().Select(c => new ThingDefCountClass(c.thingDef, f.ThingCountNeeded(c.thingDef))) : ((Blueprint_Build)t.Pending).TotalMaterialCost()).Where(c => c.count > 0).GroupBy(c => c.thingDef))
            {
                var items = map.listerThings.ThingsOfDef(group.Key).Where(t => t.Spawned && !t.Position.Fogged(map)).ToList();
                int allowed = items.Where(t => !t.IsForbidden(Faction.OfPlayer) && builders.Any(b => t.Position.IsInAllowedArea(b) && b.CanReach(t, PathEndMode.Touch, Danger.None))).Sum(t => t.stackCount);
                if (allowed < group.Sum(c => c.count)) return items.Any(t => t.IsForbidden(Faction.OfPlayer)) ? "Material proibido: " + group.Key.label : "Material insuficiente/inacessível: " + group.Key.label;
            }
            var reservation = map.reservationManager.ReservationsReadOnly.FirstOrDefault(r => r.Target.HasThing && pending.Any(t => t.Pending == r.Target.Thing));
            if (reservation != null) return "Reserva de " + reservation.Claimant?.LabelShort + ": " + reservation.Claimant?.CurJob?.def.defName + ". Mantida enquanto o trabalho é válido.";
            return "Materiais existem: aguardando entrega/construção; conferir ocupação e prioridades de Construction/Hauling.";
        }
        public static string Apply(Map map, List<RoomProject> projects)
        {
            int ticks = Find.TickManager.TicksGame;
            bool danger = map.mapPawns.AllPawnsSpawned.Any(p => !p.Dead && !p.Downed && p.HostileTo(Faction.OfPlayer));
            CompleteDoorFloorPlan(map, projects);
            if(projects.Any(p=>p.Kind==CourtyardBasePlanner.ReservationKind))
            {
                var needed=new HashSet<RoomProject>(NeededBedrooms(map,projects));
                foreach(var bedroom in projects.Where(p=>p.Kind=="Quarto"))
                    bedroom.Priority=needed.Contains(bedroom)?ConstructionPriority.High:ConstructionPriority.Low;
            }
            if (!danger) BasePlanner.RepairUnissuedKitchenConflicts(map, projects);
            var builders = Builders(map);
            foreach (var p in projects)
            {
                p.BlockReason = null;
                if(!danger)foreach(var c in p.NoRoofCells)map.areaManager.NoRoof[c]=true;
                if (p.Kind == "Energia e climatização") p.Priority = (map.mapTemperature.OutdoorTemp < 0 || map.mapTemperature.OutdoorTemp > 35) &&
                    projects.Any(r => r.RequiresRoof && r.Shell.All(t => t.Complete(map))) ? ConstructionPriority.Critical : ConstructionPriority.High;
                foreach (var t in Tasks(p))
                {
                    // Pending coolers in older plans follow the new default too;
                    // already configured/player-adjusted thermostats remain intact.
                    if (p.Kind == "Freezer" && t.Def.defName == "Cooler" && !t.TemperatureConfigured) t.TargetTemperature = -2f;
                    Track(map, t);
                    if (!t.SettingsConfigured && t.Complete(map) && (t.MedicalBed || t.StorageKind != null))
                    {
                        var building = t.Position.GetThingList(map).First(b => b.def == t.Def && b.Position == t.Position);
                        if (t.MedicalBed && building is Building_Bed bed) bed.Medical = true;
                        if (t.StorageKind != null && building is Building_Storage storage) StoragePolicy.Configure(storage.GetStoreSettings(), t.StorageKind, shelf: true);
                        t.SettingsConfigured = true;
                    }
                    if (!t.TemperatureConfigured && t.TargetTemperature > -999 && t.Complete(map))
                    {
                        var thermostat = t.Position.GetThingList(map).OfType<ThingWithComps>().First(b => b.def == t.Def).TryGetComp<CompTempControl>();
                        if (thermostat != null) thermostat.TargetTemperature = t.TargetTemperature;
                        t.TemperatureConfigured = true;
                    }
                }
                float progress = Progress(map, p);
                if (Math.Abs(progress - p.LastProgress) > 0.01f || p.LastProgressTick == 0) { p.LastProgress = progress; p.LastProgressTick = ticks; }
                p.Stalled = p.Started && ticks - p.LastProgressTick >= StallTicks;
                if (Tasks(p).Any(t => t.WasCompleted && !t.Complete(map)))
                { p.Completed = false; p.State = ConstructionState.Blocked; p.BlockReason = "Estrutura concluída removida ou destruída; revisão necessária. Outras obras continuam."; continue; }
                if (Tasks(p).Any(t => t.CancelledByPlayer)) { p.State = ConstructionState.Paused; p.BlockReason = "Obra removida ou alterada (possível cancelamento); outras obras continuam."; continue; }
                if (p.Completed) { p.State = ConstructionState.Completed; continue; }
                if (danger) { p.State = ConstructionState.Paused; p.BlockReason = "Hostis no mapa."; continue; }
                if (p.Kind == "Preparação do terreno")
                {
                    if (p.LayoutSlot?.StartsWith("prep:mod:") == true)
                    {
                        var owner = projects.FirstOrDefault(r => "prep:" + r.LayoutSlot == p.LayoutSlot);
                        if (owner != null && owner.Kind != "Quarto" && projects.Any(r => r.Kind == "Quarto" && !r.Completed))
                        { p.State = ConstructionState.Blocked; p.BlockReason = "Limpeza aguarda os quartos essenciais."; continue; }
                    }
                    var rocks = p.MineCells.Select(c => c.GetEdifice(map)).Where(t => t is Mineable).ToList();
                    var miners = map.mapPawns.FreeColonistsSpawned.Where(b => WorkPriorityManager.CanWork(b) && b.workSettings.Initialized &&
                        !b.WorkTypeIsDisabled(WorkTypeDefOf.Mining) && b.workSettings.GetPriority(WorkTypeDefOf.Mining) > 0).ToList();
                    int miningPending = rocks.Count(r => map.designationManager.DesignationAt(r.Position, DesignationDefOf.Mine) != null);
                    foreach (var rock in rocks.Where(r => map.designationManager.DesignationAt(r.Position, DesignationDefOf.Mine) == null &&
                        miners.Any(b => r.Position.IsInAllowedArea(b) && b.CanReach(r, PathEndMode.Touch, Danger.None)))
                        .OrderBy(r => miners.Min(b => b.Position.DistanceToSquared(r.Position))).Take(Math.Max(0, 8 - miningPending)))
                    { map.designationManager.AddDesignation(new Designation(rock.Position, DesignationDefOf.Mine)); p.OwnedMineCells.Add(rock.Position); }
                    // A later planned wall can occupy a former ruin cell. It
                    // belongs to the finished build, not to the old demolition.
                    p.ClearCells.RemoveAll(c=>c.GetEdifice(map)==null ||
                        projects.SelectMany(Tasks).Any(t=>t.Position==c && t.Complete(map) && t.Def==c.GetEdifice(map).def));
                    p.OwnedClearCells.RemoveAll(c=>!p.ClearCells.Contains(c));
                    p.OwnedPlants.RemoveAll(t=>t==null || t.Destroyed || !t.Spawned);
                    var debris=p.ClearCells.Select(c=>c.GetEdifice(map)).Where(b=>b!=null).Distinct().ToList();
                    // Clearing is a one-time preparation task. Regrowing grass
                    // must not turn the whole plan into an endless clearing loop.
                    var cultivated=p.PlantCells.Where(c=>c.GetZone(map) is Zone_Growing zone &&
                        c.GetThingList(map).OfType<Plant>().Any(plant=>plant.def==zone.GetPlantDefToGrow())).ToHashSet();
                    // Growing can replace cleared grass between execution ticks.
                    // The new crop is not the original obstacle to remove.
                    foreach(var plant in p.OwnedPlants.OfType<Plant>().Where(plant=>cultivated.Contains(plant.Position)).ToList())
                    {
                        var cut=map.designationManager.DesignationOn(plant,DesignationDefOf.CutPlant);
                        if(cut!=null)map.designationManager.RemoveDesignation(cut);
                        p.OwnedPlants.Remove(plant);
                    }
                    p.PlantCells.RemoveAll(c=>cultivated.Contains(c)||!c.GetThingList(map).OfType<Plant>().Any());
                    var plants=p.PlantCells.SelectMany(c=>c.GetThingList(map).OfType<Plant>()).Distinct().ToList();
                    int plantPending=plants.Count(t=>map.designationManager.AllDesignationsOn(t).Any());
                    foreach(var plant in plants.Where(t=>!map.designationManager.AllDesignationsOn(t).Any()).Take(Math.Max(0,8-plantPending)))
                    {
                        var designation=plant.HarvestableNow&&plant.def.plant.harvestedThingDef==ThingDefOf.WoodLog?DesignationDefOf.HarvestPlant:DesignationDefOf.CutPlant;
                        map.designationManager.AddDesignation(new Designation(plant,designation));p.OwnedPlants.Add(plant);
                    }
                    foreach(var building in debris)
                    {
                        if(building.Faction!=null||!building.DeconstructibleBy(Faction.OfPlayer))
                        {p.BlockReason="Obstáculo não removível ou pertencente a outra facção; preservado.";continue;}
                        if(map.designationManager.DesignationOn(building,DesignationDefOf.Deconstruct)==null)
                        {map.designationManager.AddDesignation(new Designation(building,DesignationDefOf.Deconstruct));p.OwnedClearCells.Add(building.Position);}
                    }
                    p.Completed = rocks.Count == 0 && debris.Count==0 && plants.Count==0; p.State = p.Completed ? ConstructionState.Completed : ConstructionState.Active;
                    p.BlockReason = p.Completed ? null : $"Limpeza por trabalho normal: {rocks.Count} rochas, {debris.Count} estruturas removíveis, {plants.Count} plantas restantes. Mining, Construction e Plant Cut precisam estar habilitados."; continue;
                }
                foreach (var c in p.ClearCells)
                {
                    if (p.Shell.Any(t => t.Position == c && t.Complete(map))) continue;
                    var building = c.GetEdifice(map);
                    if (building == null) continue;
                    if (building.def != ThingDefOf.Wall || building.Faction != Faction.OfPlayer)
                    { p.BlockReason = "Obstáculo alterado pelo jogador; abertura da porta suspensa."; break; }
                    if (map.designationManager.DesignationOn(building, DesignationDefOf.Deconstruct) == null)
                    { map.designationManager.AddDesignation(new Designation(building, DesignationDefOf.Deconstruct)); p.OwnedClearCells.Add(c); }
                }
                if (p.BlockReason != null) { p.State = ConstructionState.Blocked; continue; }
                if (p.Kind == "Energia e climatização" && p.LayoutSlot == "power")
                    foreach (var c in p.Footprint) map.areaManager.NoRoof[c] = true;
                if (p.Crop != null)
                {
                    if (DependsOn(map, p, projects)) { p.State = ConstructionState.Blocked; p.BlockReason = "Aguardando estoque, alimentação e quartos."; continue; }
                    if (p.GrowingZone == null)
                    {
                        if (p.StorageCells.Any(c => c.GetZone(map) != null)) { p.State = ConstructionState.Blocked; p.BlockReason = "Zona do jogador ocupa a plantação."; continue; }
                        var cells = p.StorageCells.Where(c => c.GetTerrain(map).fertility >= p.Crop.plant.fertilityMin && c.GetEdifice(map) == null && !c.Roofed(map)).ToList();
                        if (cells.Count == 0) { p.State = ConstructionState.Blocked; p.BlockReason = "Sem solo fértil descoberto para " + p.Crop.label; continue; }
                        p.GrowingZone = new Zone_Growing(map.zoneManager); map.zoneManager.RegisterZone(p.GrowingZone);
                        foreach (var c in cells) { p.GrowingZone.AddCell(c); map.areaManager.NoRoof[c] = true; }
                        p.GrowingZone.SetPlantDefToGrow(p.Crop);
                    }
                    p.Completed = true; p.State = ConstructionState.Completed; continue;
                }
                if (builders.Count == 0) { p.State = ConstructionState.Blocked; p.BlockReason = "Nenhum construtor com Construction habilitado."; continue; }
                if (projects.Any(r => r.Kind == ModularBasePlanner.ReservationKind) && DependsOn(map, p, projects))
                { p.State = ConstructionState.Blocked; p.BlockReason = "Aguardando quartos habitáveis, alimentação ou limpeza local."; continue; }
                Storage(map, p);
                if (p.BlockReason != null) { p.State = ConstructionState.Blocked; continue; }
                bool shell = p.Shell.All(t => t.Complete(map));
                if (shell && p.RequiresRoof)
                    foreach (var c in p.RoofArea)
                    {
                        if (map.areaManager.NoRoof[c]) { p.BlockReason = "Área Sem teto do jogador impede cobertura."; break; }
                        if (!c.Roofed(map) && !map.areaManager.BuildRoof[c]) { map.areaManager.BuildRoof[c] = true; p.RoofOrders.Add(c); }
                    }
                if (p.BlockReason != null) { p.State = ConstructionState.Blocked; continue; }
                if (shell && FurnitureStage(map,p).All(t => t.Complete(map)) && (!p.RequiresRoof || p.RoofArea.All(c => c.Roofed(map)))) { p.Completed = true; p.State = ConstructionState.Completed; continue; }
                if (DependsOn(map, p, projects)) { p.State = ConstructionState.Blocked; p.BlockReason = "Aguardando estrutura de outros módulos."; continue; }
                p.State = Tasks(p).Any(t => t.Pending?.Spawned == true) || shell && p.RequiresRoof && p.RoofArea.Any(c => !c.Roofed(map)) ? ConstructionState.Active : ConstructionState.Planned;
                if (p.Stalled)
                {
                    p.BlockReason = PendingReason(map, p, builders);
                    if (p.BlockReason.StartsWith("Material ")) p.State = ConstructionState.WaitingMaterials;
                    else if (p.BlockReason.StartsWith("Obras")) p.State = ConstructionState.Blocked;
                    if (ticks - p.LastRecoveryTick >= 600) { p.LastRecoveryTick = ticks; ConstructionWorkManager.CleanInvalidReservations(map, projects); Log.Message($"[AutonomousRim] Recovery: {p.Kind}: {p.BlockReason}"); }
                }
            }
            if (danger) return "Construção suspensa: hostis no mapa.";
            if (builders.Count == 0) return "Construção bloqueada: nenhum construtor habilitado/disponível.";
            var budget = Available(map);
            var neededMaterials = new HashSet<ThingDef>(projects.Where(p => !p.Completed).SelectMany(Tasks).Where(t => !t.Complete(map))
                .SelectMany(t => CostListCalculator.CostListAdjusted(t.Def, t.Stuff)).Select(c => c.thingDef));
            // Only construction materials require path checks here. Hundreds of stone
            // chunks, meals or apparel cannot fund these blueprints.
            foreach (var group in map.listerThings.AllThings.Where(t => t.Spawned && t.def.category == ThingCategory.Item && neededMaterials.Contains(t.def) && !t.IsForbidden(Faction.OfPlayer) && !t.Position.Fogged(map))
                .Where(t => !builders.Any(b => t.Position.IsInAllowedArea(b) && b.CanReach(t, PathEndMode.Touch, Danger.None))).GroupBy(t => t.def))
                if (budget.ContainsKey(group.Key)) budget[group.Key] -= group.Sum(t => t.stackCount);
            int pendingCount = projects.SelectMany(Tasks).Where(t => t.Pending?.Spawned == true).Select(t => t.Pending).Distinct().Count(), issued = 0, slots = 0;
            var priorityHold = new Dictionary<ThingDef, int>();
            foreach (var p in projects.Where(p => !p.Completed && Tasks(p).Any() && p.State != ConstructionState.Paused && p.State != ConstructionState.Blocked).OrderBy(p => p.Priority)
                .ThenBy(p => ExecutionRank(map,p)))
            {
                if (slots >= Math.Min(MaxActiveProjects, Math.Max(1, builders.Count * 2)))
                { continue; } // Limit new commitments, never suspend already funded native work.
                bool active = p.State == ConstructionState.Active; string missing = null; int roomIssued = 0;
                int roomPending = Tasks(p).Where(t => t.Pending?.Spawned == true).Select(t => t.Pending).Distinct().Count();
                foreach (var t in CurrentStage(map, p).Where(t => !t.Complete(map) && t.Pending?.Spawned != true))
                {
                    if (t.RetryAfter > ticks) { missing = t.LastFailure ?? "Cooldown após falha."; continue; }
                    if (!t.Def.IsResearchFinished) { missing = "Pesquisa necessária: " + t.Def.label; continue; }
                    if (!builders.Any(b => b.skills.GetSkill(SkillDefOf.Construction).Level >= t.Def.constructionSkillPrerequisite && t.Position.IsInAllowedArea(b) && b.CanReach(t.Position, PathEndMode.Touch, Danger.None))) { missing = "Sem acesso ou habilidade: " + t.Def.label; continue; }
                    Thing shared = t.Position.GetThingList(map).FirstOrDefault(b => (b is Blueprint_Build || b is Frame) && Matches(t, b));
                    if (shared != null) { t.Pending = shared; t.Issued = true; t.Owned = projects.SelectMany(Tasks).Any(o => o.Pending == shared && o.Owned); active = true; continue; }
                    if (issued >= MaxPerCycle || roomIssued >= 12 || roomPending >= 24 || pendingCount >= MaxPending) break;
                    // Stuff and explicit cost may name the same resource (butcher
                    // tables need wood both ways). Fund their combined native cost.
                    var costs = CostListCalculator.CostListAdjusted(t.Def, t.Stuff)
                        .GroupBy(c => c.thingDef).Select(g => new ThingDefCountClass(g.Key, g.Sum(c => c.count))).ToList();
                    var deficit = costs.FirstOrDefault(c => !budget.TryGetValue(c.thingDef, out int held) || held - SafetyReserve(map, p, c.thingDef) -
                        (priorityHold.TryGetValue(c.thingDef, out int reserved) ? reserved : 0) < c.count);
                    if (deficit != null)
                    {
                        missing = "Aguardando material/reserva: " + deficit.thingDef.label;
                        // A stream of small walls must not consume every incoming log
                        // before an earlier essential table/bed/generator can be funded.
                        if (p.Priority <= ConstructionPriority.High)
                        { priorityHold.TryGetValue(deficit.thingDef, out int held); priorityHold[deficit.thingDef] = Math.Max(held, deficit.count); }
                        continue;
                    }
                    AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(t.Def, t.Position, t.Rotation, map, stuffDef: t.Stuff);
                    if (!report) { missing = t.Def.label + ": " + report.Reason; continue; }
                    t.Pending = GenConstruct.PlaceBlueprintForBuild(t.Def, t.Position, map, t.Rotation, Faction.OfPlayer, t.Stuff);
                    t.Issued = true; t.Owned = true; p.Started = true; active = true; issued++; roomIssued++; pendingCount++; roomPending++;
                    foreach (var c in costs) budget[c.thingDef] -= c.count;
                }
                if (active) { slots++; p.State = ConstructionState.Active; }
                else if (missing != null) p.State = missing.StartsWith("Aguardando material") ? ConstructionState.WaitingMaterials : ConstructionState.Blocked;
                p.BlockReason = missing ?? p.BlockReason;
            }
            return $"Construção: {slots}/{MaxActiveProjects} projetos ativos; {issued} blueprints novos, {pendingCount}/{MaxPending} pendentes. Etapas por lote; bloqueios não param a fila.";
        }
        private static int SafetyReserve(Map map, RoomProject p, ThingDef d) => p.Priority <= ConstructionPriority.High || p.RequiresRoof && p.Shell.Any(t => !t.Complete(map)) ? 0 :
            d == ThingDefOf.WoodLog ? 60 : d == ThingDefOf.Steel ? 50 : d == ThingDefOf.ComponentIndustrial ? 2 : 0;
        public static void Stop(Map map, List<RoomProject> projects)
        {
            var tasks = projects.SelectMany(Tasks).ToList();
            foreach (var t in tasks) Track(map, t);
            var cancelled = new HashSet<Thing>(tasks.Where(t => t.Owned && t.Pending is Blueprint_Build bp && bp.Spawned && Matches(t, bp) && !t.CancelledByPlayer).Select(t => t.Pending));
            foreach (var bp in cancelled) bp.Destroy(DestroyMode.Cancel);
            foreach (var t in tasks.Where(t => cancelled.Contains(t.Pending))) { t.Pending = null; t.Issued = false; t.Owned = false; }
            foreach (var p in projects)
            {
                foreach(var plant in p.OwnedPlants.Where(t=>t?.Spawned==true))
                    foreach(var d in map.designationManager.AllDesignationsOn(plant).Where(d=>d.def==DesignationDefOf.HarvestPlant||d.def==DesignationDefOf.CutPlant).ToList())d.Delete();
                p.OwnedPlants.Clear();
                foreach (var c in p.OwnedMineCells)
                { var rock = c.GetEdifice(map); if (rock != null) map.designationManager.DesignationAt(c, DesignationDefOf.Mine)?.Delete(); }
                foreach (var c in p.OwnedClearCells)
                { var wall = c.GetEdifice(map); if (wall != null) map.designationManager.DesignationOn(wall, DesignationDefOf.Deconstruct)?.Delete(); }
                p.OwnedMineCells.Clear(); p.OwnedClearCells.Clear();
                foreach (var c in p.RoofOrders) if (!c.Roofed(map)) map.areaManager.BuildRoof[c] = false;
                p.RoofOrders.Clear(); if (!p.Completed) p.State = ConstructionState.Paused;
            }
        }
    }
}
