using AutonomousRim.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace AutonomousRim.Perception
{
    public static class EquipmentAnalyzer
    {
        public static EquipmentProfile Analyze(Thing weapon)
        {
            if (weapon == null) return null;
            var profile = new EquipmentProfile
            {
                Name = weapon.LabelCap.ToString(),
                Ranged = weapon.def.IsRangedWeapon,
                Condition = weapon.def.useHitPoints ? (float)weapon.HitPoints / Mathf.Max(1, weapon.MaxHitPoints) : 1f
            };
            if (!profile.Ranged)
            {
                profile.DamagePerSecond = weapon.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
                profile.Notes = "Melee weapon stat; pawn accuracy and target armor excluded.";
                return profile;
            }

            var verb = weapon.TryGetComp<CompEquippable>()?.PrimaryVerb;
            var properties = verb?.verbProps;
            var projectile = properties?.defaultProjectile?.projectile;
            profile.Range = properties?.range ?? 0f;
            profile.SpecialAttack = projectile == null || projectile.explosionRadius > 0f ||
                                    properties.verbClass != typeof(Verb_Shoot) || properties.consumeFuelPerShot > 0f;
            if (projectile == null)
            {
                profile.Notes = "Special attack: direct projectile damage unavailable.";
                return profile;
            }
            int shots = Mathf.Max(1, properties.burstShotCount);
            float cycle = properties.warmupTime + weapon.GetStatValue(StatDefOf.RangedWeapon_Cooldown) +
                          (shots - 1) * properties.ticksBetweenBurstShots / 60f;
            profile.DamagePerSecond = CombatMath.BurstDps(projectile.GetDamageAmount(weapon), shots, cycle);
            profile.ArmorPenetration = projectile.GetArmorPenetration(weapon);
            profile.Accuracy = weapon.GetStatValue(StatDefOf.AccuracyShort);
            profile.Notes = profile.SpecialAttack
                ? "Explosive/special attack; excluded from automatic weapon recommendations."
                : "Theoretical burst DPS; cover, distance, pawn accuracy and target armor excluded.";
            return profile;
        }

        // Advisory score at a fixed reference distance, not a prediction of real damage.
        public static float Suitability(Pawn pawn, EquipmentProfile weapon)
        {
            if (weapon == null || weapon.SpecialAttack || (weapon.Ranged && weapon.Range < 12f)) return 0f;
            float skill = pawn.skills?.GetSkill(weapon.Ranged ? SkillDefOf.Shooting : SkillDefOf.Melee)?.Level ?? 0;
            float accuracy = weapon.Ranged ? weapon.Accuracy * Mathf.Pow(Mathf.Clamp01(pawn.GetStatValue(StatDefOf.ShootingAccuracyPawn)), 12f) : 1f;
            return weapon.DamagePerSecond * accuracy * (0.5f + skill / 20f) *
                   (1f + Mathf.Clamp(weapon.Range, 0f, 40f) / 80f) * (1f + Mathf.Clamp01(weapon.ArmorPenetration));
        }
    }
}
