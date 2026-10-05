using System;
using AutonomousRim.Core;

internal static class Program
{
    private static int checks;

    private static void Equal<T>(T expected, T actual, string scenario)
    {
        if (!Equals(expected, actual)) throw new Exception($"{scenario}: expected {expected}, got {actual}");
        checks++;
    }

    private static void Main()
    {
        Equal(12f, CombatMath.BurstDps(8f, 3, 2f), "Three-shot burst amortizes damage over the complete firing cycle");
        Equal(6f, CombatMath.BurstDps(8f, 3, 4f), "Doubling the cycle halves sustained damage");
        Equal(0f, CombatMath.BurstDps(8f, 3, 0f), "Zero-duration attacks do not produce infinite scores");
        Equal(0f, CombatMath.BurstDps(8f, 0, 2f), "Attacks without shots have no projectile DPS");
        Equal(0f, CombatMath.BurstDps(8f, 3, float.NaN), "Invalid cycle cannot enter a ranking");
        Equal(0f, CombatMath.BurstDps(float.NaN, 3, 2f), "Invalid damage cannot enter a ranking");
        Equal(0f, CombatMath.BurstDps(float.PositiveInfinity, 3, 2f), "Unbounded damage cannot enter a ranking");
        Equal("Clear", CombatMath.AssessRisk(0, 0, 0f, 0f), "Empty map is clear even without defenders");
        Equal("Critical", CombatMath.AssessRisk(1, 0, 1f, 100f), "Presence of enemies without capable defenders is critical");
        Equal("Critical", CombatMath.AssessRisk(1, 3, 20f, 0f), "Zero allied strength cannot hide a threat");
        Equal("High", CombatMath.AssessRisk(2, 3, 150f, 100f), "Strength ratio at the high-risk boundary");
        Equal("High", CombatMath.AssessRisk(6, 3, 10f, 100f), "Numerical disadvantage is considered independently of strength");
        Equal("Threat present", CombatMath.AssessRisk(1, 3, 10f, 100f), "Weak enemies remain a threat rather than being called safe");
        Console.WriteLine($"Passed {checks} combat calculation and risk checks.");
    }
}
