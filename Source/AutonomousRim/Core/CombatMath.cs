using System;

namespace AutonomousRim.Core
{
    public static class CombatMath
    {
        public static float BurstDps(float damage, int shots, float cycleSeconds)
        {
            if (damage <= 0f || shots <= 0 || cycleSeconds <= 0f || float.IsNaN(cycleSeconds) ||
                float.IsInfinity(cycleSeconds) || float.IsNaN(damage) || float.IsInfinity(damage)) return 0f;
            return damage * shots / cycleSeconds;
        }

        public static string AssessRisk(int hostiles, int defenders, float enemyStrength, float friendlyStrength)
        {
            if (hostiles <= 0) return "Clear";
            if (defenders <= 0 || friendlyStrength <= 0f) return "Critical";
            float ratio = Math.Max(0f, enemyStrength) / friendlyStrength;
            if (ratio >= 1.5f || hostiles >= defenders * 2) return "High";
            return "Threat present";
        }
    }
}
