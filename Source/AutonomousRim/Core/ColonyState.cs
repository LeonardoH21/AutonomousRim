using System.Collections.Generic;

namespace AutonomousRim.Core
{
    public sealed class ColonyState
    {
        public int Tick { get; set; }
        public int ColonistCount { get; set; }
        public int CombatCapableColonists { get; set; }
        public int HostilePawnCount { get; set; }
        public int DownedColonists { get; set; }

        public float OutdoorTemperature { get; set; }

        public Dictionary<string, int> Resources { get; } = new Dictionary<string, int>();

        public override string ToString()
        {
            return $"Tick={Tick}, Colonists={ColonistCount}, CombatCapable={CombatCapableColonists}, " +
                   $"Hostiles={HostilePawnCount}, Downed={DownedColonists}, Temp={OutdoorTemperature:0.0}C";
        }
    }
}
