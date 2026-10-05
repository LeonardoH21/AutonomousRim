using System;

namespace AutonomousRim.Core
{
    public static class EquipmentPolicy
    {
        public static bool PreferMelee(bool brawler, int shooting, int melee)
        {
            return brawler || (melee >= 8 && melee >= shooting + 6);
        }

        public static float RoleMultiplier(bool rangedWeapon, bool meleeRole, bool brawler)
        {
            if (rangedWeapon) return brawler ? 0.1f : meleeRole ? 0.3f : 1f;
            return meleeRole ? 1f : 0.35f;
        }

        public static bool WorthUpgrade(float current, float candidate)
        {
            return !float.IsNaN(candidate) && !float.IsInfinity(candidate) && candidate > Math.Max(0f, current) * 1.2f + 0.25f;
        }

        public static float EffectiveBurstDps(float burstDamage, float warmup, float aimingFactor, float cooldown, float spacing)
        {
            return CombatMath.BurstDps(burstDamage, 1, Math.Max(0f, warmup) * Math.Max(0f, aimingFactor) +
                Math.Max(0f, cooldown) + Math.Max(0f, spacing));
        }
    }
}
