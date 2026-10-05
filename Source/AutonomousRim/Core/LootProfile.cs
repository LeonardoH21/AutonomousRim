namespace AutonomousRim.Core
{
    public sealed class LootProfile
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Location { get; set; }
        public bool Forbidden { get; set; }
        public bool Tainted { get; set; }
        public float Condition { get; set; }
        public float SharpArmor { get; set; }
        public float BluntArmor { get; set; }
        public EquipmentProfile Weapon { get; set; }
    }
}
