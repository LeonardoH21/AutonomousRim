using AutonomousRim.Perception;
using Verse;
using System.Collections.Generic;
using AutonomousRim.Execution;
using RimWorld;
using AutonomousRim.Planning;

namespace AutonomousRim.Core
{
    public sealed class AutonomousRimMapComponent : MapComponent
    {
        private const int ScanIntervalTicks = 360;
        private const int LootIntervalTicks = 600;
        private const int ThreatIntervalTicks = 120;

        public ColonyState CurrentState { get; private set; }
        private bool foodAutomation;
        private bool workAutomation;
        private bool scheduleAutomation = true;
        private bool equipmentAutomation;
        private bool baseAutomation = true;
        private bool strategyAutomation = true;
        private StrategicPlan strategicPlan = new StrategicPlan();
        private FailureMemory failureMemory = new FailureMemory();
        public bool StrategyAutomation => strategyAutomation;
        public StrategicPlan Strategy => strategicPlan;
        public FailureMemory FailureMemory => failureMemory;
        public string FailureStatus => failureMemory?.Status ?? "Análise de falha indisponível.";
        private int lastPlanTick = -3600;
        private int lastConstructionTick = -120;
        private int plannedPawnCount;
        private bool plannedClimateResearch;
        private List<ConstructionOrder> constructionOrders = new List<ConstructionOrder>();
        private List<Thing> constructionGathering = new List<Thing>();
        private List<IntVec3> gatheringMineCells = new List<IntVec3>();
        public string GatheringStatus { get; private set; } = "Coleta desativada.";
        private bool lootAutomation = true;
        private int lastLootTick = -600;
        private List<Thing> releasedLoot = new List<Thing>();
        public bool LootAutomation => lootAutomation;
        public string LootStatus { get; private set; } = "Allow gradual desativado.";
        private List<RoomProject> baseProjects = new List<RoomProject>();
        public bool BaseAutomation => baseAutomation;
        public IReadOnlyList<RoomProject> BaseProjects => baseProjects;
        public string BaseStatus { get; private set; } = "Base automática desativada. Gere um plano para ver os módulos iniciais.";
        private List<EquipmentOrder> equipmentOrders = new List<EquipmentOrder>();
        private List<ManagedApparel> managedApparel = new List<ManagedApparel>();
        private List<Pawn> equipmentExcluded = new List<Pawn>();
        internal List<ManagedApparel> ManagedApparel => managedApparel;
        public bool EquipmentAutomation => equipmentAutomation;
        public string EquipmentStatus { get; private set; } = "Autoequipamento desativado.";
        private List<Pawn> ownedHunts = new List<Pawn>();
        private List<ManagedFoodBill> ownedBills = new List<ManagedFoodBill>();
        private List<WorkPriorityChange> workChanges = new List<WorkPriorityChange>();
        private List<ScheduleChange> scheduleChanges = new List<ScheduleChange>();
        public bool FoodAutomation => foodAutomation;
        public bool WorkAutomation => workAutomation;
        public bool ScheduleAutomation => scheduleAutomation;
        public string ScheduleStatus { get; private set; } = "Agenda automática aguardando a primeira avaliação.";
        public string ManagementStatus { get; private set; } = "Automação desativada neste mapa.";

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref foodAutomation, "foodAutomation");
            Scribe_Values.Look(ref workAutomation, "workAutomation");
            Scribe_Values.Look(ref scheduleAutomation, "scheduleAutomation", true);
            Scribe_Values.Look(ref equipmentAutomation, "equipmentAutomation");
            Scribe_Values.Look(ref baseAutomation, "baseAutomation");
            Scribe_Values.Look(ref strategyAutomation, "strategyAutomation");
            Scribe_Deep.Look(ref strategicPlan, "strategicPlan");
            Scribe_Deep.Look(ref failureMemory, "failureMemory");
            Scribe_Values.Look(ref lastPlanTick, "lastPlanTick", -3600);
            Scribe_Values.Look(ref plannedPawnCount, "plannedPawnCount");
            Scribe_Values.Look(ref plannedClimateResearch, "plannedClimateResearch");
            Scribe_Collections.Look(ref constructionOrders, "constructionOrders", LookMode.Deep);
            // Native map compression does not preserve individual mineable IDs.
            // Save their cells; keep ordinary plant references for wood gathering.
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                gatheringMineCells = constructionGathering.FindAll(t => t is Mineable && t.Spawned).ConvertAll(t => t.Position);
                var plants = constructionGathering.FindAll(t => !(t is Mineable));
                Scribe_Collections.Look(ref plants, "constructionGathering", LookMode.Reference);
            }
            else Scribe_Collections.Look(ref constructionGathering, "constructionGathering", LookMode.Reference);
            Scribe_Collections.Look(ref gatheringMineCells, "gatheringMineCells", LookMode.Value);
            Scribe_Values.Look(ref lootAutomation, "lootAutomation");
            Scribe_Values.Look(ref lastLootTick, "lastLootTick", -600);
            Scribe_Collections.Look(ref releasedLoot, "releasedLoot", LookMode.Reference);
            Scribe_Collections.Look(ref baseProjects, "baseProjects", LookMode.Deep);
            Scribe_Collections.Look(ref equipmentOrders, "equipmentOrders", LookMode.Deep);
            Scribe_Collections.Look(ref managedApparel, "managedApparel", LookMode.Deep);
            Scribe_Collections.Look(ref equipmentExcluded, "equipmentExcluded", LookMode.Reference);
            Scribe_Collections.Look(ref ownedHunts, "ownedHunts", LookMode.Reference);
            Scribe_Collections.Look(ref ownedBills, "ownedBills", LookMode.Deep);
            Scribe_Collections.Look(ref workChanges, "workChanges", LookMode.Deep);
            Scribe_Collections.Look(ref scheduleChanges, "scheduleChanges", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                strategicPlan = strategicPlan ?? new StrategicPlan();
                failureMemory = failureMemory ?? new FailureMemory();
                ownedHunts = ownedHunts ?? new List<Pawn>();
                ownedBills = ownedBills ?? new List<ManagedFoodBill>();
                workChanges = workChanges ?? new List<WorkPriorityChange>();
                scheduleChanges = scheduleChanges ?? new List<ScheduleChange>();
                workChanges.RemoveAll(c => c.Pawn == null || c.Work == null);
                equipmentOrders = equipmentOrders ?? new List<EquipmentOrder>();
                managedApparel = managedApparel ?? new List<ManagedApparel>();
                equipmentExcluded = equipmentExcluded ?? new List<Pawn>();
                equipmentOrders.RemoveAll(o => o.Pawn == null);
                managedApparel.RemoveAll(o => o.Pawn == null || o.Apparel == null);
                equipmentExcluded.RemoveAll(p => p == null);
                baseProjects = baseProjects ?? new List<RoomProject>();
                releasedLoot = releasedLoot ?? new List<Thing>();
                releasedLoot.RemoveAll(t => t == null || t.Destroyed);
                constructionOrders = constructionOrders ?? new List<ConstructionOrder>();
                constructionOrders.RemoveAll(o => o.Pawn == null);
                constructionGathering = constructionGathering ?? new List<Thing>();
                constructionGathering.RemoveAll(t => t == null || t.Destroyed);
                scheduleChanges.RemoveAll(c => c.Pawn == null || c.Pawn.Dead);
            }
        }

        public void PreviewBase()
        {
            BaseStatus = BasePlanner.Plan(map, baseProjects);
            lastPlanTick = Find.TickManager.TicksGame;
            plannedPawnCount = map.mapPawns.FreeColonistsSpawnedCount;
            plannedClimateResearch = DefDatabase<ResearchProjectDef>.GetNamed("AirConditioning").IsFinished;
        }

        public void SetBaseAutomation(bool enabled)
        {
            if (!enabled)
            {
                ConstructionWorkManager.Stop(constructionOrders);
                ConstructionResourceManager.Stop(map, constructionGathering);
                BaseConstructionManager.Stop(map, baseProjects);
            }
            baseAutomation = enabled;
            if (enabled)
            {
                PreviewBase();
                ExecuteConstruction(Find.TickManager.TicksGame);
            }
            else BaseStatus = "Base automática desligada. Projetos pendentes da IA cancelados; estruturas e obras com materiais mantidas.";
        }

        public void DisableAll()
        {
            SetStrategyAutomation(false);
            SetLootAutomation(false);
            SetBaseAutomation(false);
            SetEquipmentAutomation(false);
            SetAutomation(false, false);
            SetScheduleAutomation(false);
        }

        public void SetScheduleAutomation(bool enabled)
        {
            if (!enabled) ScheduleManager.Restore(scheduleChanges);
            scheduleAutomation = enabled;
            ScheduleStatus = enabled ? ScheduleManager.Apply(map, CurrentState ?? ColonyStateScanner.Scan(map), scheduleChanges, true) : "Agenda automática desligada; agenda restaurada quando possível.";
        }

        public void SetStrategyAutomation(bool enabled)
        {
            if (!enabled && strategicPlan.OwnedResearch != null && Find.ResearchManager.GetProject() == strategicPlan.OwnedResearch)
            {
                if (strategicPlan.PreviousResearch?.IsFinished == false) Find.ResearchManager.SetCurrentProject(strategicPlan.PreviousResearch);
                else Find.ResearchManager.StopProject(strategicPlan.OwnedResearch);
            }
            if (enabled && !strategyAutomation)
            {
                strategicPlan.ResearchOverride = false;
                strategicPlan.PreviousResearch = Find.ResearchManager.GetProject();
                // Explicit re-enable adopts the current selection without replacing it.
                strategicPlan.OwnedResearch = strategicPlan.PreviousResearch;
            }
            strategyAutomation = enabled;
            EvaluateStrategy();
        }

        public void EvaluateStrategy()
        {
            CurrentState = ColonyStateScanner.Scan(map);
            StrategicPlanner.Evaluate(map, CurrentState, baseProjects, strategicPlan, strategyAutomation);
            if (strategyAutomation && baseAutomation && StrategicInfrastructure.Add(map, baseProjects)) PreviewBase();
        }

        public void EvaluateFailure()
        {
            CurrentState = CurrentState ?? ColonyStateScanner.Scan(map);
            FailureAnalyzer.Evaluate(map, CurrentState, failureMemory);
        }

        public void SetLootAutomation(bool enabled)
        {
            lootAutomation = enabled;
            if (enabled) ManageColony();
            else LootStatus = "Allow desligado; itens já liberados permanecem disponíveis.";
        }

        public void SetEquipmentAutomation(bool enabled)
        {
            if (!enabled) EquipmentManager.Stop(equipmentOrders, managedApparel);
            equipmentAutomation = enabled;
            CurrentState = ColonyStateScanner.Scan(map);
            EquipmentStatus = enabled ? EquipmentManager.Apply(map, CurrentState, equipmentOrders, managedApparel, equipmentExcluded) : "Autoequipamento desativado; equipamentos atuais mantidos.";
        }

        public bool EquipmentAllowedFor(Pawn pawn) => !equipmentExcluded.Contains(pawn);

        public void SetEquipmentAllowedFor(Pawn pawn, bool allowed)
        {
            if (allowed) equipmentExcluded.Remove(pawn);
            else if (!equipmentExcluded.Contains(pawn))
            {
                equipmentExcluded.Add(pawn);
                var orders = equipmentOrders.FindAll(o => o.Pawn == pawn);
                var apparel = managedApparel.FindAll(o => o.Pawn == pawn);
                EquipmentManager.Stop(orders, apparel);
                equipmentOrders.RemoveAll(o => o.Pawn == pawn);
                managedApparel.RemoveAll(o => o.Pawn == pawn);
            }
        }

        public void SetAutomation(bool food, bool work)
        {
            if (foodAutomation && !food) { FoodManager.CancelHunts(map, ownedHunts); FoodManager.RemoveOwnedBills(ownedBills); }
            if (workAutomation && !work) WorkPriorityManager.Restore(workChanges);
            if (!workAutomation && work) Find.PlaySettings.useWorkPriorities = true;
            foodAutomation = food;
            workAutomation = work;
            string transition = CurrentState?.Threat?.LastTransition;
            CurrentState = ColonyStateScanner.Scan(map);
            if (transition != null) CurrentState.Threat.LastTransition = transition;
            ManageColony();
        }

        public AutonomousRimMapComponent(Map map) : base(map)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (gatheringMineCells != null)
                foreach (var cell in gatheringMineCells)
                    if (cell.InBounds(map) && cell.GetEdifice(map) is Mineable rock && !constructionGathering.Contains(rock)) constructionGathering.Add(rock);
            CurrentState = ColonyStateScanner.Scan(map);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            int ticks = Find.TickManager?.TicksGame ?? 0;
            if (ticks > 0 && ticks - strategicPlan.LastEvaluation >= 600) EvaluateStrategy();
            if (ticks > 0 && ticks - failureMemory.LastEvaluationTick >= 600) EvaluateFailure();
            if (baseAutomation && ticks > 0 && ticks % BaseConstructionManager.ExecutionInterval == 0) ExecuteConstruction(ticks);
            if (CurrentState != null && ticks > 0 && ticks % ThreatIntervalTicks == 0)
            {
                if (equipmentAutomation) EquipmentManager.TrackOrders(equipmentOrders, managedApparel);
                RefreshThreat(ticks);
            }
            if (ticks <= 0 || ticks % ScanIntervalTicks != 0)
            {
                return;
            }

            string lastTransition = CurrentState?.Threat?.LastTransition;
            CurrentState = ColonyStateScanner.Scan(map);
            if (lastTransition != null) CurrentState.Threat.LastTransition = lastTransition;
            ManageColony();

            if (Prefs.DevMode)
            {
                Log.Message($"[AutonomousRim] Scan: {CurrentState}");
            }
        }

        private void ManageColony()
        {
            if (lootAutomation && CurrentState != null && Find.TickManager.TicksGame - lastLootTick >= LootIntervalTicks)
            {
                lastLootTick = Find.TickManager.TicksGame;
                LootStatus = LootAccessManager.Apply(map, CurrentState, baseAutomation, baseProjects, managedApparel, EquipmentAllowedFor, releasedLoot, out int released, foodAutomation ? ownedBills : null);
                if (released > 0)
                {
                    string transition = CurrentState.Threat.LastTransition;
                    CurrentState = ColonyStateScanner.Scan(map);
                    CurrentState.Threat.LastTransition = transition;
                }
            }
            if (workAutomation && Find.PlaySettings.useWorkPriorities) WorkPriorityManager.Apply(map, CurrentState, workChanges,
                baseAutomation && baseProjects.Exists(p => !p.Completed && p.Priority <= ConstructionPriority.High), constructionGathering.Count > 0, strategyAutomation && Find.ResearchManager.GetProject() != null);
            if (scheduleAutomation) ScheduleStatus = ScheduleManager.Apply(map, CurrentState, scheduleChanges, true);
            ManagementStatus = foodAutomation ? FoodManager.Apply(map, CurrentState, ownedHunts, ownedBills, baseProjects) : "Comida / roupas automáticas desativadas.";
            if (workAutomation) ManagementStatus += Find.PlaySettings.useWorkPriorities
                ? " Prioridades ativas; alterações manuais do jogador são preservadas."
                : " Prioridades pausadas: o modo numérico de trabalho foi desligado pelo jogador.";
            if (equipmentAutomation) EquipmentStatus = EquipmentManager.Apply(map, CurrentState, equipmentOrders, managedApparel, equipmentExcluded);
            if (baseAutomation)
            {
                ExecuteConstruction(Find.TickManager.TicksGame);
            }
        }

        private void ExecuteConstruction(int ticks)
        {
            if (lastConstructionTick == ticks) return;
            lastConstructionTick = ticks;
            bool climate = DefDatabase<ResearchProjectDef>.GetNamed("AirConditioning").IsFinished;
            if (baseProjects.Count == 0 && ticks - lastPlanTick >= 1200 || plannedPawnCount != map.mapPawns.FreeColonistsSpawnedCount || plannedClimateResearch != climate ||
                baseProjects.Exists(p => p.Kind == RingBasePlanner.ReservationKind) && ticks - lastPlanTick >= 3600)
                PreviewBase();
            if (baseProjects.Count == 0) return;
            BaseStatus = BaseConstructionManager.Apply(map, baseProjects);
            if (ticks % 600 == 0) GatheringStatus = ConstructionResourceManager.Apply(map, baseProjects, constructionGathering);
            ConstructionWorkManager.Apply(map, baseProjects, constructionOrders);
        }

        public void NotifyConstructionJobEnding(Pawn pawn, Verse.AI.Job job, Verse.AI.JobCondition condition) =>
            ConstructionWorkManager.NotifyEnding(pawn, job, condition, baseProjects, constructionOrders);

        private void RefreshThreat(int ticks)
        {
            ThreatState previous = CurrentState.Threat;
            ThreatState next = ThreatScanner.Scan(map, CurrentState);
            next.LastTransition = previous.LastTransition;
            if ((previous.ActiveCount == 0) != (next.ActiveCount == 0))
            {
                next.LastTransition = next.ActiveCount > 0
                    ? $"Tick {ticks}: hostile presence detected."
                    : $"Tick {ticks}: no active hostile pawns remain.";
                Log.Message($"[AutonomousRim] Map {map.uniqueID}: {next.LastTransition}");
            }
            CurrentState.Threat = next;
            CurrentState.HostilePawnCount = next.ActiveCount;
            if (previous.ActiveCount == 0 && next.ActiveCount > 0) FailureAnalyzer.RecordRaidStart(map, CurrentState, failureMemory);
            if (previous.ActiveCount > 0 && next.ActiveCount == 0) FailureAnalyzer.RecordRaidEnd(CurrentState, failureMemory);
            if (foodAutomation && next.ActiveCount > 0) FoodManager.CancelHunts(map, ownedHunts);
            if (equipmentAutomation && next.ActiveCount > 0) EquipmentManager.CancelPending(equipmentOrders);
        }
    }
}
