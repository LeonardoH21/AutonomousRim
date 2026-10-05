using RimWorld;
using Verse;

namespace AutonomousRim.Core
{
    public sealed class EquipmentDecision
    {
        public Pawn Pawn;
        public Thing Item;
        public bool Wear;
        public float CurrentScore;
        public float CandidateScore;
        public float Gain => CandidateScore - CurrentScore;
        public string Reason;
    }
}
