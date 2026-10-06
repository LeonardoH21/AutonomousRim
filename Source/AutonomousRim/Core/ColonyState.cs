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
        public float TargetFoodDays { get; set; } = ColonyPolicy.TargetFoodDays;
        public float StrategicReserveDays { get; set; } = 1.5f;
        public string FoodReserveLevel { get; set; } = "Indisponível";
        public string FoodReserveStatus { get; set; } = "Reserva ainda não avaliada.";
        public float PerishableFoodNutrition { get; set; }
        public float LongLifeFoodNutrition { get; set; }
        public float NearSpoilingNutrition { get; set; }
        public int StoredMealCount { get; set; }
        public int StoredFoodItemCount { get; set; }
        public int FreezerCapacityCells { get; set; }
        public int FreezerUsedCells { get; set; }
        public float FreezerFillRatio { get; set; }
        public bool FreezerNearFull { get; set; }
        public int CookingTargetCount { get; set; }
        public int StrategicMealTargetCount { get; set; }
        public string PreferredMeal { get; set; } = "Nenhuma receita disponível.";
        public string FoodStorageStatus { get; set; } = "Armazenamento ainda não avaliado.";
        public List<PawnProfile> Pawns { get; } = new List<PawnProfile>();
        public List<EquipmentDecision> EquipmentDecisions { get; set; } = new List<EquipmentDecision>();
        public ThreatState Threat { get; set; }

        public float OutdoorTemperature { get; set; }

        public Dictionary<string, int> Resources { get; } = new Dictionary<string, int>();

        public override string ToString()
        {
            return $"Tick={Tick}, Colonists={ColonistCount}, CombatCapable={CombatCapableColonists}, " +
                   $"Hostiles={HostilePawnCount}, Downed={DownedColonists}, Temp={OutdoorTemperature:0.0}C, Loot={Loot.Count}, FoodDays={EstimatedFoodDays:0.0}/{TargetFoodDays:0.0}, Reserve={FoodReserveLevel}";
        }
    }
}
