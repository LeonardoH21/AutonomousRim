using System;
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

        public static void Apply(Map map, ColonyState state, List<WorkPriorityChange> changes, bool construction = false, bool gathering = false, bool strategicResearch = false)
        {
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(CanWork).ToList();
            var load = pawns.ToDictionary(p => p, p => 0);
            var workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            foreach (Pawn pawn in pawns) pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
            Pawn mainBuilder = construction ? pawns.Where(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction))
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Construction).Level).FirstOrDefault() : null;
            bool skilledFrames = mainBuilder != null &&
                map.GetComponent<AutonomousRimMapComponent>().BaseProjects.Where(p => !p.Completed && BaseConstructionManager.CanContinueExistingWork(p) && p.Priority <= Planning.ConstructionPriority.High)
                    .SelectMany(BaseConstructionManager.Tasks).Any(t => t.Pending is Frame frame && frame.Spawned && frame.IsCompleted() &&
                        t.Def.constructionSkillPrerequisite > 0 && mainBuilder.skills.GetSkill(SkillDefOf.Construction).Level >= t.Def.constructionSkillPrerequisite &&
                        !pawns.Any(p => p != mainBuilder && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction) &&
                            p.skills.GetSkill(SkillDefOf.Construction).Level >= t.Def.constructionSkillPrerequisite));
            Pawn researcher = strategicResearch && pawns.Count >= 3 && state.HostilePawnCount == 0 && state.DownedColonists == 0 &&
                state.EstimatedFoodDays >= 1f ? pawns.Where(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Research) && (!skilledFrames || p != mainBuilder))
                .OrderByDescending(p => Score(p, WorkTypeDefOf.Research, 0)).FirstOrDefault() : null;
            bool foodUrgent = state.DailyFoodNutrition > 0f && state.EstimatedFoodDays < 1f;
            bool foodTight = state.DailyFoodNutrition > 0f && state.EstimatedFoodDays < ColonyPolicy.TargetFoodDays;
            WorkTypeDef cookingWork = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
            bool medicalUrgent = state.DownedColonists > 0 || pawns.Any(p => p.health?.summaryHealth?.SummaryHealthPercent < 0.75f);
            bool fireUrgent = ScheduleManager.HasFire(map);
            bool resourceUrgent = state.Resources.TryGetValue(ThingDefOf.Steel.defName, out int steel) && steel < pawns.Count * 20 ||
                state.Resources.TryGetValue(ThingDefOf.WoodLog.defName, out int wood) && wood < pawns.Count * 30 ||
                state.Resources.TryGetValue(ThingDefOf.ComponentIndustrial.defName, out int components) && components < Math.Max(2, pawns.Count / 2);
            int unfinished = map.listerThings.AllThings.Count(t => t is Frame || t is Blueprint_Build);

            // Food/medical roles are assigned first; load penalties spread subsequent specialties.
            var ordered = workTypes.OrderBy(w => w == WorkTypeDefOf.Doctor ? 0 : w.defName == "Cooking" ? 1 :
                w == WorkTypeDefOf.Growing ? 2 : w == WorkTypeDefOf.Hunting ? 3 : 4).ThenByDescending(w => w.naturalPriority);
            foreach (WorkTypeDef work in ordered)
            {
                var eligible = pawns.Where(p => !p.WorkTypeIsDisabled(work) && (work != WorkTypeDefOf.Hunting || CanHunt(p))).ToList();
                var specialists = eligible;
                if (skilledFrames && (work == WorkTypeDefOf.Mining || work == WorkTypeDefOf.PlantCutting || work.defName == "Cooking"))
                {
                    var helpers = eligible.Where(p => p != mainBuilder && (work.defName != "Cooking" || p.skills.GetSkill(SkillDefOf.Cooking).Level >= 3)).ToList();
                    if (helpers.Count > 0) specialists = helpers;
                }
                Pawn specialist = work.relevantSkills?.Count > 0 || work == WorkTypeDefOf.Hunting
                    ? specialists.OrderByDescending(p => Score(p, work, load[p])).FirstOrDefault() : null;
                if (specialist != null) load[specialist]++;
                foreach (Pawn pawn in pawns)
                {
                    if (pawn.WorkTypeIsDisabled(work)) continue;
                    int priority = work == WorkTypeDefOf.Hunting ? (pawn == specialist ?
                        (state.DailyFoodNutrition > 0f && state.EstimatedFoodDays < 1f ? 1 : 2) : 0) : 3;
                    if (pawn == specialist && work != WorkTypeDefOf.Hunting)
                        priority = work == WorkTypeDefOf.Doctor ||
                            (state.DailyFoodNutrition > 0f && state.EstimatedFoodDays < ColonyPolicy.TargetFoodDays && (work.defName == "Cooking" || work == WorkTypeDefOf.Growing)) ? 1 : 2;
                    if (work.defName == "Firefighter" || work.defName == "Patient" || work.defName == "BedRest" || work.defName == "Basic") priority = 1;
                    // Equal top priority lets native constructors help on funded simple
                    // frames before unrelated hauling; skill checks still apply.
                    if (construction && work == WorkTypeDefOf.Construction) priority = 1;
                    if (construction && work == WorkTypeDefOf.Hauling) priority = pawn == mainBuilder ? 2 : 1;
                    if (gathering && (work == WorkTypeDefOf.PlantCutting || work == WorkTypeDefOf.Mining)) priority = pawn == specialist ? 1 : 2;
                    if (skilledFrames && pawn == mainBuilder && work.defName == "Cooking" && (specialist != mainBuilder || state.EstimatedFoodDays >= 1f)) priority = 2;
                    if (researcher == pawn && work == WorkTypeDefOf.Research) priority = 1;
                    if (researcher == pawn && construction && (work == WorkTypeDefOf.Construction || work == WorkTypeDefOf.Hauling)) priority = 2;
                    if (fireUrgent && work.defName == "Firefighter") priority = 1;
                    if (medicalUrgent && work == WorkTypeDefOf.Doctor) priority = pawn == specialist ? 1 : Math.Min(priority, 2);
                    if (medicalUrgent && (work.defName == "Patient" || work.defName == "BedRest")) priority = 1;
                    if ((foodUrgent || foodTight) && (work == WorkTypeDefOf.Growing || work == cookingWork))
                        priority = pawn == specialist ? 1 : Math.Min(priority, foodUrgent ? 2 : 3);
                    if (resourceUrgent && (work == WorkTypeDefOf.Mining || work == WorkTypeDefOf.PlantCutting))
                        priority = pawn == specialist ? 1 : Math.Min(priority, 2);
                    if (construction && unfinished > 0 && work == WorkTypeDefOf.Construction) priority = Math.Min(priority, 1);
                    if ((pawn.needs?.mood?.CurLevelPercentage ?? 1f) < 0.30f && work.defName == "Basic") priority = 1;
                    SetManagedPriority(pawn, work, priority, changes);
                }
            }
            void Raise(WorkTypeDef work, bool urgent)
            {
                if (!urgent || work == null)return;
                var eligible=pawns.Where(p=>!p.WorkTypeIsDisabled(work)).OrderByDescending(p=>Score(p,work,0)).ToList();
                if(eligible.Count==0)return;
                foreach(var pawn in eligible)SetManagedPriority(pawn,work,pawn==eligible[0]?1:2,changes);
            }
            Raise(cookingWork,foodUrgent);Raise(WorkTypeDefOf.Growing,foodUrgent);
            Raise(WorkTypeDefOf.Doctor,medicalUrgent);Raise(DefDatabase<WorkTypeDef>.GetNamedSilentFail("Firefighter"),fireUrgent);
            Raise(DefDatabase<WorkTypeDef>.GetNamedSilentFail("Mining"),resourceUrgent);Raise(WorkTypeDefOf.PlantCutting,resourceUrgent);
        }

        private static float Score(Pawn pawn, WorkTypeDef work, int load)
        {
            if (work.relevantSkills == null || work.relevantSkills.Count == 0) return -load * 6f;
            float mood = pawn.needs?.mood?.CurLevelPercentage ?? 1f;
            float rest = pawn.needs?.rest?.CurLevelPercentage ?? 1f;
            return work.relevantSkills.Average(skill =>
            {
                SkillRecord record = pawn.skills?.GetSkill(skill);
                return ColonyPolicy.WorkScore(record?.Level ?? 0, (int)(record?.passion ?? Passion.None), 0);
            }) * pawn.health.summaryHealth.SummaryHealthPercent * UnityEngine.Mathf.Clamp(pawn.GetStatValue(StatDefOf.WorkSpeedGlobal), 0.1f, 3f) *
                UnityEngine.Mathf.Lerp(0.55f, 1f, mood) * UnityEngine.Mathf.Lerp(0.65f, 1f, rest) - load * 6f;
        }

        internal static void SetManagedPriority(Pawn pawn, WorkTypeDef work, int desired, List<WorkPriorityChange> changes)
        {
            int current = pawn.workSettings.GetPriority(work);
            WorkPriorityChange change = changes.FirstOrDefault(c => c.Pawn == pawn && c.Work == work);
            if (change != null)
            {
                if (current != change.Applied) change.UserOverride = true;
                if (change.UserOverride)return;
            }
            else
            {
                change = new WorkPriorityChange { Pawn = pawn, Work = work, Original = current, Applied = current };
                changes.Add(change);
            }
            if (current == desired)return;
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
