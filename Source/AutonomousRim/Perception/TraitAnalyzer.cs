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
            return $"Precisão: {StatText(pawn, StatDefOf.ShootingAccuracyPawn, "P0")}; tempo de mira: {StatText(pawn, StatDefOf.AimingDelayFactor, "0.00", "×")}; " +
                $"acerto corpo a corpo: {StatText(pawn, StatDefOf.MeleeHitChance, "P0")}; esquiva: {StatText(pawn, StatDefOf.MeleeDodgeChance, "P0")}; " +
                $"dano recebido: {StatText(pawn, StatDefOf.IncomingDamageFactor, "0.00", "×")}; movimento: {StatText(pawn, StatDefOf.MoveSpeed, "0.00")}; " +
                $"trabalho: {StatText(pawn, StatDefOf.WorkSpeedGlobal, "0.00", "×")}. Valores do jogo já incluem modificadores ativos.";
        }

        private static string StatText(Pawn pawn, StatDef stat, string format, string prefix = "")
        {
            return stat.Worker.IsDisabledFor(pawn) ? "indisponível" : prefix + pawn.GetStatValue(stat).ToString(format);
        }
    }
}
