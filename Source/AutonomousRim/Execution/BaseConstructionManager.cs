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
        public const int MaxActiveProjects = 3, MaxPending = 18, MaxPerCycle = 6, ExecutionInterval = 120, StallTicks = 1800;
        public static IEnumerable<ConstructionTask> Tasks(RoomProject p) => p.Shell.Concat(p.Furniture);
        public static bool CanContinueExistingWork(RoomProject p) => p.State == ConstructionState.Active || p.State == ConstructionState.WaitingMaterials;
        public static bool Matches(ConstructionTask task, Thing thing)
        {
            ThingDef material = thing is Blueprint bp ? bp.EntityToBuildStuff() : thing is Frame f ? f.EntityToBuildStuff() : thing.Stuff;
            return thing.def.entityDefToBuild == task.Def && thing.Position == task.Position && thing.Rotation == task.Rotation && material == task.Stuff && thing.Faction == Faction.OfPlayer;
        }
        private static void Track(Map map, ConstructionTask task)
        {
            if (task.Def is TerrainDef && task.OriginalTerrain != null && !task.Complete(map) && task.Position.GetTerrain(map) != task.OriginalTerrain) task.CancelledByPlayer = true;
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
        public static IEnumerable<ConstructionTask> CurrentStage(Map map, RoomProject p) => p.Shell.All(t => t.Complete(map)) ? p.Furniture : p.Shell;
        private static bool DependsOn(Map map, RoomProject p, List<RoomProject> all)
        {
            if (p.Kind == "Corredor") return all.Where(r => r.RequiresRoof && r != p).Any(r => !r.Shell.All(t => t.Complete(map)));
            // Power is independent of enclosing every room; valid reserved cells can be built early.
            if (p.Kind == "Energia e climatização") return false;
            if (p.Kind == "Conforto dos quartos") return all.Where(r => r.Kind == "Quarto").Any(r => !r.Completed);
            if (p.Kind == "Prateleiras") return all.Where(r => r.Kind == "Estoque" || r.Kind == "Freezer" || r.Kind == "Armas").Any(r => !r.Completed);
            if (p.Kind == "Hospital" || p.Kind == "Oficina" || p.Kind == "Armas")
                return all.Where(r => r.Kind == "Quarto" || r.Kind == "Cozinha" || r.Kind == "Estoque" || r.Kind == "Freezer").Any(r => !r.Completed);
            return p.Kind == "Pisos e acabamento" && all.Where(r => r.RequiresRoof).Any(r => !r.Completed);
        }
        private static void Storage(Map map, RoomProject p)
        {
            if (p.Kind != "Estoque" && p.Kind != "Freezer" && p.Kind != "Despejo" && p.Kind != "Medicamentos" && p.Kind != "Armas") return;
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
            return result + (p.RequiresRoof ? p.Interior.Count(c => c.Roofed(map)) * 100 : 0);
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
            if (!danger) BasePlanner.RepairUnissuedKitchenConflicts(map, projects);
            var builders = Builders(map);
            foreach (var p in projects)
            {
                p.BlockReason = null;
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
                if (builders.Count == 0) { p.State = ConstructionState.Blocked; p.BlockReason = "Nenhum construtor com Construction habilitado."; continue; }
                Storage(map, p);
                if (p.BlockReason != null) { p.State = ConstructionState.Blocked; continue; }
                bool shell = p.Shell.All(t => t.Complete(map));
                if (shell && p.RequiresRoof)
                    foreach (var c in p.Interior)
                    {
                        if (map.areaManager.NoRoof[c]) { p.BlockReason = "Área Sem teto do jogador impede cobertura."; break; }
                        if (!c.Roofed(map) && !map.areaManager.BuildRoof[c]) { map.areaManager.BuildRoof[c] = true; p.RoofOrders.Add(c); }
                    }
                if (p.BlockReason != null) { p.State = ConstructionState.Blocked; continue; }
                if (shell && p.Furniture.All(t => t.Complete(map)) && (!p.RequiresRoof || p.Interior.All(c => c.Roofed(map)))) { p.Completed = true; p.State = ConstructionState.Completed; continue; }
                if (DependsOn(map, p, projects)) { p.State = ConstructionState.Blocked; p.BlockReason = "Aguardando estrutura de outros módulos."; continue; }
                p.State = Tasks(p).Any(t => t.Pending?.Spawned == true) || shell && p.RequiresRoof && p.Interior.Any(c => !c.Roofed(map)) ? ConstructionState.Active : ConstructionState.Planned;
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
            foreach (var p in projects.Where(p => !p.Completed && p.State != ConstructionState.Paused && p.State != ConstructionState.Blocked).OrderBy(p => p.Priority)
                .ThenBy(p => p.Kind == "Estoque" ? 0 : p.Kind == "Quarto" ? 1 : p.Kind == "Cozinha" ? 2 : p.Kind == "Freezer" ? 3 : 4))
            {
                if (slots >= Math.Min(MaxActiveProjects, Math.Max(1, builders.Count + 1)))
                { if (p.State == ConstructionState.Active) { p.State = ConstructionState.Planned; p.BlockReason = "Aguardando um dos três slots; investimento preservado."; } continue; }
                bool active = p.State == ConstructionState.Active; string missing = null; int roomIssued = 0;
                int roomPending = Tasks(p).Where(t => t.Pending?.Spawned == true).Select(t => t.Pending).Distinct().Count();
                foreach (var t in CurrentStage(map, p).Where(t => !t.Complete(map) && t.Pending?.Spawned != true))
                {
                    if (t.RetryAfter > ticks) { missing = t.LastFailure ?? "Cooldown após falha."; continue; }
                    if (!t.Def.IsResearchFinished) { missing = "Pesquisa necessária: " + t.Def.label; continue; }
                    if (!builders.Any(b => b.skills.GetSkill(SkillDefOf.Construction).Level >= t.Def.constructionSkillPrerequisite && t.Position.IsInAllowedArea(b) && b.CanReach(t.Position, PathEndMode.Touch, Danger.None))) { missing = "Sem acesso ou habilidade: " + t.Def.label; continue; }
                    Thing shared = t.Position.GetThingList(map).FirstOrDefault(b => (b is Blueprint_Build || b is Frame) && Matches(t, b));
                    if (shared != null) { t.Pending = shared; t.Issued = true; t.Owned = projects.SelectMany(Tasks).Any(o => o.Pending == shared && o.Owned); active = true; continue; }
                    if (issued >= MaxPerCycle || roomIssued >= 2 || roomPending >= 6 || pendingCount >= MaxPending) break;
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
            return $"Construção: {slots}/3 projetos ativos; {issued} blueprints novos, {pendingCount}/{MaxPending} pendentes. Etapas por lote; bloqueios não param a fila.";
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
                foreach (var c in p.RoofOrders) if (!c.Roofed(map)) map.areaManager.BuildRoof[c] = false;
                p.RoofOrders.Clear(); if (!p.Completed) p.State = ConstructionState.Paused;
            }
        }
    }
}
