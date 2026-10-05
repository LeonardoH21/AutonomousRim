using RimWorld;
using Verse;

namespace AutonomousRim.Perception
{
    public static class PawnAnalyzer
    {
        public static float EstimateCombatValue(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
            {
                return 0f;
            }

            float shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
            float melee = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            float healthFactor = pawn.health?.summaryHealth?.SummaryHealthPercent ?? 0f;
            float consciousness = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 0f;
            float movement = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Moving) ?? 0f;

            float skillScore = (shooting * 0.6f) + (melee * 0.4f);
            float capabilityScore = ((healthFactor + consciousness + movement) / 3f) * 20f;

            if (pawn.Downed)
            {
                capabilityScore *= 0.1f;
            }

            return skillScore + capabilityScore;
        }
    }
}
