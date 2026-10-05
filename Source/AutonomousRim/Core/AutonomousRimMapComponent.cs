using AutonomousRim.Perception;
using Verse;

namespace AutonomousRim.Core
{
    public sealed class AutonomousRimMapComponent : MapComponent
    {
        private const int ScanIntervalTicks = 600;

        public ColonyState CurrentState { get; private set; }

        public AutonomousRimMapComponent(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            int ticks = Find.TickManager?.TicksGame ?? 0;
            if (ticks <= 0 || ticks % ScanIntervalTicks != 0)
            {
                return;
            }

            CurrentState = ColonyStateScanner.Scan(map);

            if (Prefs.DevMode)
            {
                Log.Message($"[AutonomousRim] Scan: {CurrentState}");
            }
        }
    }
}
