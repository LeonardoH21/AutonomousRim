using System;

namespace AutonomousRim.Core
{
    public static class ColonyPolicy
    {
        public const float TargetFoodDays = 3f;

        public static bool SafePrey(bool predator, float revengeChance, bool owned, bool downed, bool hostile)
        {
            return !predator && revengeChance <= 0f && !owned && !downed && !hostile;
        }

        public static int MealTarget(float dailyNutrition, float mealNutrition)
        {
            return MealTarget(dailyNutrition, mealNutrition, TargetFoodDays);
        }

        public static int MealTarget(float dailyNutrition, float mealNutrition, float targetDays)
        {
            if (dailyNutrition <= 0f || mealNutrition <= 0f) return 0;
            return Math.Min(400, Math.Max(1, (int)Math.Ceiling(dailyNutrition * Math.Max(1f, targetDays) / mealNutrition)));
        }


        public static float WorkScore(int level, int passion, int specialistLoad)
        {
            return level + passion * 3f - specialistLoad * 6f;
        }
    }
}
