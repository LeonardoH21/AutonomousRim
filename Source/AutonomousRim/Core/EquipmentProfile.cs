namespace AutonomousRim.Core
{
    public sealed class EquipmentProfile
    {
        public string Name { get; set; }
        public bool Ranged { get; set; }
        public bool SpecialAttack { get; set; }
        public float Range { get; set; }
        public float DamagePerSecond { get; set; }
        public float ArmorPenetration { get; set; }
        public float Accuracy { get; set; }
        public float Condition { get; set; }
        public string Notes { get; set; }
        public float BurstDamage { get; set; }
        public float WarmupSeconds { get; set; }
        public float CooldownSeconds { get; set; }
        public float BurstSpacingSeconds { get; set; }
        public float MinimumRange { get; set; }
    }
}
