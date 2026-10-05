namespace AutonomousRim.Core
{
    public sealed class PawnProfile
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string Weapon { get; set; }
        public string Apparel { get; set; }
        public string Traits { get; set; }
        public float Health { get; set; }
        public float CombatValue { get; set; }
        public float WeaponRange { get; set; }
        public int Shooting { get; set; }
        public int Melee { get; set; }
        public int Medical { get; set; }
        public bool CombatReady { get; set; }
        public EquipmentProfile Equipment { get; set; }
        public string ArmorDetails { get; set; }
        public string RecommendedWeapon { get; set; } = "No improvement found among available weapons.";
    }
}
