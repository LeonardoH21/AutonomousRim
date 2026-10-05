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
        public float FoodNutrition { get; set; }
        public float DailyFoodNutrition { get; set; }
        public List<LootProfile> Loot { get; set; } = new List<LootProfile>();
        public float EstimatedFoodDays { get; set; }
        public List<PawnProfile> Pawns { get; } = new List<PawnProfile>();
        public ThreatState Threat { get; set; }

        public float OutdoorTemperature { get; set; }

        public Dictionary<string, int> Resources { get; } = new Dictionary<string, int>();

        public override string ToString()
        {
            return $"Tick={Tick}, Colonists={ColonistCount}, CombatCapable={CombatCapableColonists}, " +
                   $"Hostiles={HostilePawnCount}, Downed={DownedColonists}, Temp={OutdoorTemperature:0.0}C, Loot={Loot.Count}, FoodDays={EstimatedFoodDays:0.0}";
        }
    }
}
