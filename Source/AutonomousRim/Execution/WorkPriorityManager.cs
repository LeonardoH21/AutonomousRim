using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    public static class WorkPriorityManager
    {
        public static bool CanWork(Pawn pawn)
        {
            return !pawn.Dead && !pawn.Downed && !pawn.Drafted && !pawn.InMentalState && pawn.workSettings != null;
        }

        public static bool CanHunt(Pawn pawn)
        {
            EquipmentProfile weapon = Perception.EquipmentAnalyzer.Analyze(pawn.equipment?.Primary);
            return CanWork(pawn) && Perception.PawnAnalyzer.IsCombatReady(pawn) &&
                   !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hunting) && weapon?.Ranged == true &&
                   !weapon.SpecialAttack && weapon.Range >= 12f;
        }

        public static void Apply(Map map, ColonyState state, List<WorkPriorityChange> changes)
        {
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(CanWork).ToList();
            var load = pawns.ToDictionary(p => p, p => 0);
            var workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            foreach (Pawn pawn in pawns) pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized();

            // Food/medical roles are assigned first; load penalties spread subsequent specialties.
            var ordered = workTypes.OrderBy(w => w == WorkTypeDefOf.Doctor ? 0 : w.defName == "Cooking" ? 1 :
                w == WorkTypeDefOf.Growing ? 2 : w == WorkTypeDefOf.Hunting ? 3 : 4).ThenByDescending(w => w.naturalPriority);
            foreach (WorkTypeDef work in ordered)
            {
                var eligible = pawns.Where(p => !p.WorkTypeIsDisabled(work) && (work != WorkTypeDefOf.Hunting || CanHunt(p))).ToList();
                Pawn specialist = work.relevantSkills?.Count > 0 || work == WorkTypeDefOf.Hunting
                    ? eligible.OrderByDescending(p => Score(p, work, load[p])).FirstOrDefault() : null;
                if (specialist != null) load[specialist]++;
                foreach (Pawn pawn in pawns)
                {
                    if (pawn.WorkTypeIsDisabled(work)) continue;
                    int priority = work == WorkTypeDefOf.Hunting ? (pawn == specialist ? 2 : 0) : 3;
                    if (pawn == specialist)
                        priority = work == WorkTypeDefOf.Doctor ||
                            (state.DailyFoodNutrition > 0f && state.EstimatedFoodDays < ColonyPolicy.TargetFoodDays && (work.defName == "Cooking" || work == WorkTypeDefOf.Growing)) ? 1 : 2;
                    if (work.defName == "Firefighter" || work.defName == "Patient" || work.defName == "BedRest" || work.defName == "Basic") priority = 1;
                    SetManagedPriority(pawn, work, priority, changes);
                }
            }
        }

        private static float Score(Pawn pawn, WorkTypeDef work, int load)
        {
            if (work.relevantSkills == null || work.relevantSkills.Count == 0) return -load * 6f;
            return work.relevantSkills.Average(skill =>
            {
                SkillRecord record = pawn.skills?.GetSkill(skill);
                return ColonyPolicy.WorkScore(record?.Level ?? 0, (int)(record?.passion ?? Passion.None), 0);
            }) * pawn.health.summaryHealth.SummaryHealthPercent * UnityEngine.Mathf.Clamp(pawn.GetStatValue(StatDefOf.WorkSpeedGlobal), 0.1f, 3f) - load * 6f;
        }

        private static void SetManagedPriority(Pawn pawn, WorkTypeDef work, int desired, List<WorkPriorityChange> changes)
        {
            int current = pawn.workSettings.GetPriority(work);
            WorkPriorityChange change = changes.FirstOrDefault(c => c.Pawn == pawn && c.Work == work);
            if (change != null)
            {
                if (current != change.Applied) change.UserOverride = true;
                if (change.UserOverride) return;
            }
            else
            {
                if (current == desired) return;
                change = new WorkPriorityChange { Pawn = pawn, Work = work, Original = current };
                changes.Add(change);
            }
            if (current == desired) return;
            pawn.workSettings.SetPriority(work, desired);
            change.Applied = desired;
        }

        public static void Restore(List<WorkPriorityChange> changes)
        {
            foreach (WorkPriorityChange change in changes)
                if (change.Pawn?.workSettings != null && change.Work != null && !change.UserOverride &&
                    !change.Pawn.WorkTypeIsDisabled(change.Work) && change.Pawn.workSettings.GetPriority(change.Work) == change.Applied)
                    change.Pawn.workSettings.SetPriority(change.Work, change.Original);
            changes.Clear();
        }
    }
}
