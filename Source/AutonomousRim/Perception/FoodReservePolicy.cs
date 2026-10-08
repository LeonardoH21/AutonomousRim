using System;
using RimWorld;
using Verse;
using AutonomousRim.Core;

namespace AutonomousRim.Perception
{
    public static class FoodReservePolicy
    {
        public static bool PreparingForWinter(Map map) => map != null && map.Tile >= 0 &&
            (GenDate.Season(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile)) == Season.Fall ||
             GenDate.Season(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile)) == Season.Winter || map.mapTemperature.OutdoorTemp < 0);
        public static float TargetDays(Map map, int colonists)
        {
            if (PreparingForWinter(map)) return 15f;
            float target = ColonyPolicy.TargetFoodDays;
            int ticks = Find.TickManager?.TicksAbs ?? 0;
            if (map != null && map.Tile >= 0)
            {
                Season season = GenDate.Season(ticks, Find.WorldGrid.LongLatOf(map.Tile));
                if (season == Season.Winter) target += 2f;
                else if (season == Season.Summer) target += 0.5f;
            }
            if (map != null && (map.mapTemperature.OutdoorTemp < 0f || map.mapTemperature.OutdoorTemp > 35f)) target += 1f;
            if (colonists >= 8) target += 1f;
            return Math.Min(8f, Math.Max(ColonyPolicy.TargetFoodDays, target));
        }
    }
}
