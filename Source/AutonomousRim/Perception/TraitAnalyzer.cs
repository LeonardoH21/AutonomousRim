using System.Linq;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace AutonomousRim.Perception
{
    public static class TraitAnalyzer
    {
        public static bool HasActiveTrait(Pawn pawn, string defName)
        {
            return pawn.story?.traits?.allTraits.Any(t => t.def.defName == defName && !t.Suppressed) == true;
        }

        public static bool PreferMelee(Pawn pawn)
        {
            return Core.EquipmentPolicy.PreferMelee(HasActiveTrait(pawn, "Brawler"),
                pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0, pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0);
        }

        public static string Describe(Pawn pawn)
        {
            if (pawn.story?.traits == null || pawn.story.traits.allTraits.Count == 0) return "Sem traços humanos.";
            return string.Join("\n", pawn.story.traits.allTraits.Select(trait =>
            {
                if (trait.Suppressed) return $"{trait.LabelCap}: suprimido; não usado pela IA.";
                string offsets = trait.CurrentData.statOffsets == null ? "" : string.Join(", ", trait.CurrentData.statOffsets.Select(s => $"{s.stat.LabelCap} {s.value:+0.##;-0.##;0}"));
                string factors = trait.CurrentData.statFactors == null ? "" : string.Join(", ", trait.CurrentData.statFactors.Select(s => $"{s.stat.LabelCap} ×{s.value:0.##}"));
                string disabled = string.Join(", ", (trait.GetDisabledWorkTypes() ?? new List<WorkTypeDef>()).Select(w => w.LabelCap.ToString()));
                string preference = trait.def.defName == "Brawler" ? "Prefere armas corpo a corpo." :
                    trait.def.defName == "Nudist" ? "Evita cobrir o torso quando a temperatura permite." : "";
                return $"{trait.LabelCap}: {offsets} {factors} {preference}" +
                    (disabled.Length > 0 ? $" Trabalhos impedidos: {disabled}." : "");
            }));
        }

        public static string DerivedStats(Pawn pawn)
        {
            return $"Precisão: {pawn.GetStatValue(StatDefOf.ShootingAccuracyPawn):P0}; tempo de mira: ×{pawn.GetStatValue(StatDefOf.AimingDelayFactor):0.00}; " +
                $"acerto corpo a corpo: {pawn.GetStatValue(StatDefOf.MeleeHitChance):P0}; esquiva: {pawn.GetStatValue(StatDefOf.MeleeDodgeChance):P0}; " +
                $"dano recebido: ×{pawn.GetStatValue(StatDefOf.IncomingDamageFactor):0.00}; movimento: {pawn.GetStatValue(StatDefOf.MoveSpeed):0.00}; " +
                $"trabalho: ×{pawn.GetStatValue(StatDefOf.WorkSpeedGlobal):0.00}. Valores do jogo já incluem modificadores ativos.";
        }
    }
}
