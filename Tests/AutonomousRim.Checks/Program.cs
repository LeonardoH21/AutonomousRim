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
        Equal(true, ColonyPolicy.SafePrey(false, 0f, false, false, false), "Passive wild prey is eligible");
        Equal(false, ColonyPolicy.SafePrey(true, 0f, false, false, false), "Predators are excluded even without a revenge roll");
        Equal(false, ColonyPolicy.SafePrey(false, 0.01f, false, false, false), "Even a small retaliation chance is excluded");
        Equal(false, ColonyPolicy.SafePrey(false, 0f, true, false, false), "Owned animals are not automatic prey");
        Equal(false, ColonyPolicy.SafePrey(false, 0f, false, true, false), "Downed animals are not designated for hunting");
        Equal(false, ColonyPolicy.SafePrey(false, 0f, false, false, true), "Hostile animals are handled as threats");
        Equal(6, ColonyPolicy.MealTarget(1.6f, 0.9f), "Three-day meal reserve rounds up");
        Equal(0, ColonyPolicy.MealTarget(0f, 0.9f), "No eaters require no meals");
        Equal(400, ColonyPolicy.MealTarget(1000f, 0.9f), "Production reserve has a bounded target");
        Equal(true, ColonyPolicy.WorkScore(10, 2, 0) > ColonyPolicy.WorkScore(10, 0, 0), "Passion distinguishes equal skill levels");
        Equal(true, ColonyPolicy.WorkScore(10, 0, 0) > ColonyPolicy.WorkScore(10, 0, 2), "Specialist load spreads professions");
        Console.WriteLine($"Passed {checks} combat and colony policy checks.");
    }
}
