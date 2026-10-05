using RimWorld;
using Verse;
using System.Linq;
using AutonomousRim.Core;

namespace AutonomousRim.Perception
{
    public static class PawnAnalyzer
    {
        public static bool IsCombatReady(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Downed &&
                   !pawn.InMentalState && !pawn.WorkTagIsDisabled(WorkTags.Violent) &&
                   pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) &&
                   pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
        }

        public static PawnProfile Analyze(Pawn pawn)
        {
            var weapon = pawn.equipment?.Primary;
            bool ranged = weapon != null && weapon.def.IsRangedWeapon;
            bool ready = IsCombatReady(pawn);
            EquipmentProfile equipment = EquipmentAnalyzer.Analyze(weapon);
            return new PawnProfile
            {
                Name = pawn.LabelShortCap,
                PawnId = pawn.thingIDNumber,
                TraitEffects = TraitAnalyzer.Describe(pawn),
                DerivedStats = TraitAnalyzer.DerivedStats(pawn),
                PreferredCombatRole = !ready ? "Não combatente" : TraitAnalyzer.PreferMelee(pawn) ? "Corpo a corpo" : "Combate à distância",
                CombatReady = ready,
                Role = !ready ? "Unavailable" : weapon == null ? "Unarmed" : ranged ? "Ranged" : "Melee",
                Weapon = weapon?.LabelCap.ToString() ?? "None",
                WeaponRange = ranged ? weapon.TryGetComp<CompEquippable>()?.PrimaryVerb?.verbProps.range ?? 0f : 0f,
                Apparel = pawn.apparel == null ? "None" : string.Join(", ", pawn.apparel.WornApparel.Select(a => a.LabelCap.ToString())),
                Traits = pawn.story == null ? "None" : string.Join(", ", pawn.story.traits.allTraits.Select(t => t.LabelCap.ToString())),
                Health = pawn.health.summaryHealth.SummaryHealthPercent,
                Shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0,
                Melee = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0,
                Medical = pawn.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0,
                CombatValue = EstimateCombatValue(pawn),
                Equipment = equipment,
                ArmorDetails = pawn.apparel == null ? "None" : string.Join("; ", pawn.apparel.WornApparel.Select(a =>
                    $"{a.LabelCap}: sharp {a.GetStatValue(StatDefOf.ArmorRating_Sharp):P0}, blunt {a.GetStatValue(StatDefOf.ArmorRating_Blunt):P0}"))
            };
        }

        public static float EstimateCombatValue(Pawn pawn)
        {
            if (!IsCombatReady(pawn))
            {
                return 0f;
            }

            float shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
            float melee = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            float healthFactor = pawn.health?.summaryHealth?.SummaryHealthPercent ?? 0f;
            float consciousness = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 0f;
            float movement = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Moving) ?? 0f;

            bool ranged = pawn.equipment?.Primary?.def.IsRangedWeapon ?? false;
            float skillScore = ranged ? shooting : melee;
            float capabilityScore = ((healthFactor + consciousness + movement) / 3f) * 20f;

            var equipment = EquipmentAnalyzer.Analyze(pawn.equipment?.Primary);
            float equipmentValue = EquipmentAnalyzer.Suitability(pawn, equipment);
            return (skillScore + capabilityScore + equipmentValue) * healthFactor;
        }
    }
}
