using AutonomousRim.Perception;
using Verse;
using System.Collections.Generic;
using AutonomousRim.Execution;
using RimWorld;

namespace AutonomousRim.Core
{
    public sealed class AutonomousRimMapComponent : MapComponent
    {
        private const int ScanIntervalTicks = 600;
        private const int ThreatIntervalTicks = 120;

        public ColonyState CurrentState { get; private set; }
        private bool foodAutomation;
        private bool workAutomation;
        private List<Pawn> ownedHunts = new List<Pawn>();
        private List<ManagedFoodBill> ownedBills = new List<ManagedFoodBill>();
        private List<WorkPriorityChange> workChanges = new List<WorkPriorityChange>();
        public bool FoodAutomation => foodAutomation;
        public bool WorkAutomation => workAutomation;
        public string ManagementStatus { get; private set; } = "Automação desativada neste mapa.";

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref foodAutomation, "foodAutomation");
            Scribe_Values.Look(ref workAutomation, "workAutomation");
            Scribe_Collections.Look(ref ownedHunts, "ownedHunts", LookMode.Reference);
            Scribe_Collections.Look(ref ownedBills, "ownedBills", LookMode.Deep);
            Scribe_Collections.Look(ref workChanges, "workChanges", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ownedHunts = ownedHunts ?? new List<Pawn>();
                ownedBills = ownedBills ?? new List<ManagedFoodBill>();
                workChanges = workChanges ?? new List<WorkPriorityChange>();
                workChanges.RemoveAll(c => c.Pawn == null || c.Work == null);
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
            CurrentState = ColonyStateScanner.Scan(map);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            int ticks = Find.TickManager?.TicksGame ?? 0;
            if (CurrentState != null && ticks > 0 && ticks % ThreatIntervalTicks == 0)
            {
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
            if (workAutomation && Find.PlaySettings.useWorkPriorities) WorkPriorityManager.Apply(map, CurrentState, workChanges);
            ManagementStatus = foodAutomation ? FoodManager.Apply(map, CurrentState, ownedHunts, ownedBills) : "Alimentação automática desativada.";
            if (workAutomation) ManagementStatus += Find.PlaySettings.useWorkPriorities
                ? " Prioridades ativas; alterações manuais do jogador são preservadas."
                : " Prioridades pausadas: o modo numérico de trabalho foi desligado pelo jogador.";
        }

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
            if (foodAutomation && next.ActiveCount > 0) FoodManager.CancelHunts(map, ownedHunts);
        }
    }
}
