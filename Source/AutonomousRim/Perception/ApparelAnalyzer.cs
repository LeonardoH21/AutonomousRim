using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AutonomousRim.Perception
{
    public static class ApparelAnalyzer
    {
        public static bool BlocksRanged(Apparel apparel)
        {
            return apparel.def.comps?.OfType<CompProperties_Shield>().Any(c => c.blocksRangedWeapons) == true;
        }

        public static float Score(Pawn pawn, Apparel apparel, float outdoorTemperature)
        {
            float coverage = Mathf.Clamp01(pawn.health.hediffSet.GetNotMissingParts()
                .Where(p => p.depth == BodyPartDepth.Outside && apparel.def.apparel.CoversBodyPart(p)).Sum(p => p.coverageAbs));
            float sharp = apparel.GetStatValue(StatDefOf.ArmorRating_Sharp);
            float blunt = apparel.GetStatValue(StatDefOf.ArmorRating_Blunt);
            float protection = coverage * (sharp * 0.7f + blunt * 0.3f) * (AutonomousRim.Planning.DefenseProductionPlan.Melee(pawn) ? 40f : 25f);
            float coldWeight = outdoorTemperature < 0f ? 1.2f : outdoorTemperature < 10f ? 0.3f : 0.02f;
            float heatWeight = outdoorTemperature > 35f ? 1.2f : outdoorTemperature > 25f ? 0.3f : 0.02f;
            float thermal = apparel.GetStatValue(StatDefOf.Insulation_Cold) * coldWeight + apparel.GetStatValue(StatDefOf.Insulation_Heat) * heatWeight;
            float movement = apparel.def.equippedStatOffsets?.Where(s => s.stat == StatDefOf.MoveSpeed).Sum(s => s.value) ?? 0f;
            float condition = apparel.def.useHitPoints ? (float)apparel.HitPoints / Mathf.Max(1, apparel.MaxHitPoints) : 1f;
            bool coversTorso = apparel.def.apparel.countsAsClothingForNudity;
            float nudistPenalty = TraitAnalyzer.HasActiveTrait(pawn, "Nudist") && coversTorso && outdoorTemperature >= 10f && outdoorTemperature <= 30f ? 30f : 0f;
            float shield = BlocksRanged(apparel) && AutonomousRim.Planning.DefenseProductionPlan.Melee(pawn) ? apparel.GetStatValue(StatDefOf.EnergyShieldEnergyMax) * 10f : 0f;
            float movementWeight = AutonomousRim.Planning.DefenseProductionPlan.Melee(pawn) ? 4f :
                pawn.workSettings?.GetPriority(WorkTypeDefOf.Doctor) == 1 ? 20f : 12f;
            return (protection + thermal + shield) * condition + movement * movementWeight - nudistPenalty;
        }
    }
}
