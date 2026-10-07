using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;
using HarmonyLib;

namespace AutonomousRim.Execution
{
    public static class WorkPriorityManager
    {
        // GetPriority masks every enabled priority as 3 in checkbox mode.
        // Ownership must compare the stored value, preserving future numeric settings.
        private static readonly AccessTools.FieldRef<Pawn_WorkSettings,DefMap<WorkTypeDef,int>> StoredPriorities =
            AccessTools.FieldRefAccess<Pawn_WorkSettings,DefMap<WorkTypeDef,int>>("priorities");
        public static int RawPriority(Pawn pawn,WorkTypeDef work) => StoredPriorities(pawn.workSettings)?[work] ?? pawn.workSettings.GetPriority(work);
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
            foreach (var pawn in pawns) pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
            var types = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            bool foodUrgent = state.DailyFoodNutrition > 0 && state.EstimatedFoodDays < 1;
            bool foodLow = state.DailyFoodNutrition > 0 && state.EstimatedFoodDays < state.TargetFoodDays;
            var available = pawns.Where(p => !WorkReadiness.NeedsRecovery(p) || foodUrgent && WorkReadiness.CanProduceEmergencyFood(p)).ToList();
            var load = available.ToDictionary(p => p, p => 0);
            int patients = Math.Max(state.DownedColonists, map.mapPawns.FreeColonistsSpawned.Count(p => p.Downed || HealthAIUtility.ShouldSeekMedicalRest(p)));
            bool fire = EmergencyManager.LocalFire(map);
            int builds = map.listerThings.AllThings.Count(t => t is Frame || t is Blueprint_Build);
            int mines = map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Mine).Count();
            int cuts = map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.CutPlant).Count() +
                map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.HarvestPlant).Count();
            int hunts = map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt).Count();
            int crops = map.zoneManager.AllZones.OfType<Zone_Growing>().Sum(z => z.Cells.Count(c =>
                c.GetPlant(map)?.HarvestableNow == true || z.allowSow && c.GetPlant(map) == null &&
                c.GetTerrain(map).fertility >= z.GetPlantDefToGrow().plant.fertilityMin &&
                PlantUtility.GrowthSeasonNow(c, map, z.GetPlantDefToGrow())));
            int bills = map.listerThings.AllThings.OfType<Building_WorkTable>().Where(t => t.Faction == Faction.OfPlayer)
                .SelectMany(t => t.BillStack.Bills).Count(b => b.recipe.workSkill == SkillDefOf.Cooking && b.ShouldDoNow());
            bool mealsLow = bills > 0 && state.StoredMealCount < Math.Max(4, state.ColonistCount * 2);
            var tables = map.listerThings.AllThings.OfType<Building_WorkTable>().Where(t => t.Faction == Faction.OfPlayer).ToList();
            var currentResearch=Find.ResearchManager.GetProject();
            bool researchNeeded=strategicResearch && !foodUrgent && !fire && currentResearch!=null &&
                map.GetComponent<AutonomousRimMapComponent>().BaseProjects.Where(p=>!p.Completed)
                .SelectMany(BaseConstructionManager.Tasks).Any(t=>t.Def is ThingDef d && d.researchPrerequisites?.Contains(currentResearch)==true && !d.IsResearchFinished);
            var protectedBuilders = new HashSet<Pawn>();
            if (construction)
                foreach (var frame in map.listerThings.AllThings.OfType<Frame>().Where(f => f.IsCompleted()))
                    if (frame.def.entityDefToBuild is ThingDef building && building.constructionSkillPrerequisite > 0)
                    {
                        var capable = available.Where(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction) &&
                            p.skills.GetSkill(SkillDefOf.Construction).Level >= building.constructionSkillPrerequisite).ToList();
                        if (capable.Count == 1) protectedBuilders.Add(capable[0]);
                    }
            var demand = new Dictionary<WorkTypeDef, int>();
            var urgent = new HashSet<WorkTypeDef>();
            foreach (var work in types)
            {
                int count = 0;
                bool high = false;
                if (work == WorkTypeDefOf.Doctor) { count = patients; high = patients > 0; }
                else if (work.defName == "Cooking") { count = Math.Max(bills, foodLow ? 1 : 0); high = foodLow || mealsLow; }
                else if (work == WorkTypeDefOf.Growing) { count = Math.Max(crops, foodLow ? 1 : 0); high = foodLow; }
                else if (work == WorkTypeDefOf.Construction) { count = builds; high = construction && builds > 0; }
                else if (work == WorkTypeDefOf.Mining) { count = mines; high = gathering && mines > 0; }
                else if (work == WorkTypeDefOf.PlantCutting) { count = cuts; high = gathering && cuts > 0; }
                else if (work == WorkTypeDefOf.Hunting) { count = hunts; high = foodUrgent && hunts > 0; }
                else if (work == WorkTypeDefOf.Research) { count = strategicResearch && !foodUrgent && (patients == 0 || researchNeeded) ? 1 : 0; high=researchNeeded; }
                else
                {
                    var stations = DefDatabase<WorkGiverDef>.AllDefsListForReading.Where(g => g.workType == work && g.fixedBillGiverDefs != null)
                        .SelectMany(g => g.fixedBillGiverDefs).ToHashSet();
                    count = tables.Where(t => stations.Contains(t.def)).Sum(t => t.BillStack.Bills.Count(b => b.ShouldDoNow()));
                }
                demand[work] = count;
                if (high) urgent.Add(work);
            }
            // Assign high-demand roles first; assigned work limits subsequent specialists.
            // Preserve an existing suitable specialist through a small tie-breaking bonus.
            var roles = new Dictionary<WorkTypeDef, List<Pawn>>();
            foreach (var work in types.Where(w => demand[w] > 0)
                .OrderBy(w => w == WorkTypeDefOf.Doctor && patients > 0 ? 0 :
                    foodUrgent && (w.defName == "Cooking" || w == WorkTypeDefOf.Growing) ? 1 : urgent.Contains(w) ? 2 : 3)
                .ThenByDescending(w => demand[w]).ThenByDescending(w => w.naturalPriority))
            {
                var candidates = available.Where(p => !p.WorkTypeIsDisabled(work) &&
                    (work != WorkTypeDefOf.Hunting || CanHunt(p)) &&
                    (!WorkReadiness.NeedsRecovery(p) || foodUrgent && (work.defName == "Cooking" || work == WorkTypeDefOf.Growing) && WorkReadiness.CanProduceEmergencyFood(p))).ToList();
                if(work==WorkTypeDefOf.Research && researchNeeded)
                {
                    var helpers=candidates.Where(p=>!roles.Any(r=>(r.Key==WorkTypeDefOf.Doctor && patients>0 ||
                        r.Key.defName=="Cooking" && (foodLow || mealsLow) || r.Key==WorkTypeDefOf.Growing && foodLow) && r.Value.Contains(p))).ToList();
                    if(helpers.Count>0)candidates=helpers;
                }
                if (!foodUrgent && (work.defName == "Cooking" || work == WorkTypeDefOf.Mining || work == WorkTypeDefOf.PlantCutting))
                {
                    var helpers = candidates.Where(p => !protectedBuilders.Contains(p)).ToList();
                    if (helpers.Count > 0) candidates = helpers;
                }
                int unit = work == WorkTypeDefOf.Growing ? 48 : work == WorkTypeDefOf.Construction ? 12 :
                    work == WorkTypeDefOf.Mining || work == WorkTypeDefOf.PlantCutting ? 20 : 1;
                int desired = Math.Min(candidates.Count, Math.Max(1, Math.Min((available.Count + 1) / 2,
                    (demand[work] + unit - 1) / unit)));
                var selected = new List<Pawn>();
                while (selected.Count < desired)
                {
                    var next = candidates.Where(p => !selected.Contains(p)).OrderByDescending(p => Score(p, work, load[p]) +
                        (changes.Any(c => c.Pawn == p && c.Work == work && c.Applied <= 2 && c.Applied > 0 && !c.UserOverride) ? 1f : 0f)).First();
                    selected.Add(next); load[next]++;
                }
                roles[work] = selected;
            }
            foreach (var pawn in pawns)
            foreach (var work in types.Where(w => !pawn.WorkTypeIsDisabled(w)))
            {
                bool recovering = WorkReadiness.NeedsRecovery(pawn);
                bool assigned = roles.TryGetValue(work, out var owners) && owners.Contains(pawn);
                int priority = WorkReadiness.SelfCare(work) ? 1 : work == WorkTypeDefOf.Hunting ? 0 : 3;
                if (assigned) priority = urgent.Contains(work) ? 1 : 2;
                if ((work.defName == "Patient" || work.defName == "PatientBedRest") &&
                    roles.TryGetValue(WorkTypeDefOf.Doctor, out var doctors) && doctors.Contains(pawn) && !WorkReadiness.SeriousMedicalNeed(pawn)) priority = 2;
                if (work.defName == "Firefighter") priority = fire ? 1 : 2;
                if (work == WorkTypeDefOf.Hunting && assigned) priority = foodUrgent ? 1 : 2;
                // A primary food/medical role must beat construction's native tie order.
                bool survivalRole = roles.Any(r => (r.Key == WorkTypeDefOf.Doctor && patients > 0 ||
                    (r.Key.defName == "Cooking" || r.Key == WorkTypeDefOf.Growing) && foodLow || r.Key.defName == "Cooking" && mealsLow) && r.Value.Contains(pawn));
                if (survivalRole && !WorkReadiness.SelfCare(work) && work != WorkTypeDefOf.Doctor &&
                    work.defName != "Cooking" && work != WorkTypeDefOf.Growing && work.defName != "Firefighter")
                    priority = Math.Max(priority, 2);
                if (construction && builds > 0 && work == WorkTypeDefOf.Hauling && !survivalRole) priority = 2;
                if(researchNeeded && roles.TryGetValue(WorkTypeDefOf.Research,out var researchers) && researchers.Contains(pawn) &&
                    !survivalRole && !WorkReadiness.SelfCare(work) && work!=WorkTypeDefOf.Research && work!=WorkTypeDefOf.Doctor && work.defName!="Firefighter")
                    priority=Math.Max(priority,2);
                if (recovering && !WorkReadiness.SelfCare(work) && !(assigned && foodUrgent &&
                    (work.defName == "Cooking" || work == WorkTypeDefOf.Growing) && WorkReadiness.CanProduceEmergencyFood(pawn))) priority = 0;
                SetManagedPriority(pawn, work, priority, changes);
            }
            changes.RemoveAll(c => c.Pawn == null || c.Pawn.Dead);
        }

        internal static float Score(Pawn pawn, WorkTypeDef work, int load)
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
            int current = RawPriority(pawn,work);
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
                    !change.Pawn.WorkTypeIsDisabled(change.Work) && RawPriority(change.Pawn,change.Work) == change.Applied)
                    change.Pawn.workSettings.SetPriority(change.Work, change.Original);
            changes.Clear();
        }
    }
}
