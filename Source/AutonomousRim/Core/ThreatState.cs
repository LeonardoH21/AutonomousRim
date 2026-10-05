namespace AutonomousRim.Core
{
    public sealed class ThreatState
    {
        public int ActiveCount { get; set; }
        public int Humanlikes { get; set; }
        public int Mechanoids { get; set; }
        public int Animals { get; set; }
        public int Other { get; set; }
        public int Ranged { get; set; }
        public int Melee { get; set; }
        public float EnemyStrength { get; set; }
        public float FriendlyStrength { get; set; }
        public string Risk { get; set; } = "Clear";
        public string LastTransition { get; set; } = "No transitions observed this session.";
    }
}
