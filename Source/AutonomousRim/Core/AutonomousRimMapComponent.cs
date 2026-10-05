using AutonomousRim.Perception;
using Verse;

namespace AutonomousRim.Core
{
    public sealed class AutonomousRimMapComponent : MapComponent
    {
        private const int ScanIntervalTicks = 600;
        private const int ThreatIntervalTicks = 120;

        public ColonyState CurrentState { get; private set; }

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

            if (Prefs.DevMode)
            {
                Log.Message($"[AutonomousRim] Scan: {CurrentState}");
            }
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
        }
    }
}
