using System.Collections.Generic;
using Verse;
namespace AutonomousRim.Core
{
    public sealed class ThreatState
    {
        public int ActiveCount { get; set; }
        public int Humanlikes { get; set; }
        public int Mechanoids { get; set; }
        public int Animals { get; set; }
        public int Other { get; set; }
        public int Insects, Manhunters, Structures, Dormant;
        public List<Thing> Sources = new List<Thing>();
        public List<Pawn> Colonists = new List<Pawn>();
        public List<Pawn> Vulnerable = new List<Pawn>();
        public List<Pawn> Fighters = new List<Pawn>();
        public bool Immediate => ActiveCount > 0;
        public string Classification => $"humanos {Humanlikes}, manhunters {Manhunters}, insetos {Insects}, mechs {Mechanoids}, estruturas {Structures}, outros {Other}";
        public int Ranged { get; set; }
        public int Melee { get; set; }
        public float EnemyStrength { get; set; }
        public float FriendlyStrength { get; set; }
        public string Risk { get; set; } = "Clear";
        public string LastTransition { get; set; } = "No transitions observed this session.";
    }
}
