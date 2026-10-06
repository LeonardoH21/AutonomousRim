using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    // Changes selection order inside the enabled work type; the native work giver
    // still checks skill, reachability, forbidden status, obstacles and reservations.
    public sealed class WorkGiver_AutonomousConstruction : WorkGiver_Scanner
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            var ai = pawn.Map?.GetComponent<AutonomousRimMapComponent>();
            return ai?.BaseAutomation != true || !WorkPriorityManager.CanWork(pawn) || !pawn.workSettings.Initialized ||
                pawn.WorkTypeIsDisabled(def.workType) || pawn.workSettings.GetPriority(def.workType) == 0 ||
                pawn.Map.mapPawns.AllPawnsSpawned.Any(p => !p.Dead && !p.Downed && p.HostileTo(Faction.OfPlayer));
        }
        private static bool FoodDeliveryNeeded(Pawn pawn)
        {
            var ai = pawn.Map?.GetComponent<AutonomousRimMapComponent>();
            var state = ai?.CurrentState;
            return ai?.FoodAutomation == true && state != null && state.DailyFoodNutrition > 0f && state.EstimatedFoodDays < state.TargetFoodDays;
        }
        private static bool FoodTarget(Pawn pawn, Thing t) => t?.Spawned == true && t.def.ingestible?.HumanEdible == true &&
            t.def.ingestible.drugCategory == DrugCategory.None && !(t is Corpse) && !t.IsInAnyStorage() && !t.IsForbidden(pawn) && !t.Position.Fogged(pawn.Map) &&
            (t.TryGetComp<CompRottable>() == null || t.TryGetComp<CompRottable>().Stage == RotStage.Fresh) &&
            FoodUtility.WillEat(pawn, t, pawn, true, false) && (pawn.foodRestriction?.CurrentFoodPolicy == null || pawn.foodRestriction.CurrentFoodPolicy.filter.Allows(t));
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            var targets = pawn.Map.GetComponent<AutonomousRimMapComponent>().BaseProjects
                .Where(BaseConstructionManager.CanContinueExistingWork).OrderBy(p => p.Priority).SelectMany(BaseConstructionManager.Tasks)
                .Where(t => t.Pending?.Spawned == true && !t.Complete(pawn.Map) && t.RetryAfter <= Find.TickManager.TicksGame).Select(t => t.Pending);
            if (def.workType == WorkTypeDefOf.Construction)
                targets = targets.Concat(pawn.Map.GetComponent<AutonomousRimMapComponent>().BaseProjects.Where(p => p.State != ConstructionState.Paused)
                    .SelectMany(p => p.OwnedClearCells).Select(c => c.GetEdifice(pawn.Map)).Where(t => t != null &&
                        pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null));
            if (def.workType == WorkTypeDefOf.Hauling && FoodDeliveryNeeded(pawn))
                targets = targets.Concat(pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Where(t => FoodTarget(pawn, t)));
            return targets.Distinct();
        }
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override bool Prioritized => true;
        public override float GetPriority(Pawn pawn, TargetInfo target)
        {
            if (def.workType == WorkTypeDefOf.Hauling && FoodDeliveryNeeded(pawn) && FoodTarget(pawn, target.Thing)) return 5000;
            var p = pawn.Map.GetComponent<AutonomousRimMapComponent>().BaseProjects.FirstOrDefault(r => BaseConstructionManager.Tasks(r).Any(t => t.Pending == target.Thing) || r.OwnedClearCells.Contains(target.Cell));
            if (p == null) return 0;
            return 4000 - (int)p.Priority * 1000 + 400 - RingBasePlanner.Rank(p.Kind) * 60;
        }
        public override Danger MaxPathDanger(Pawn pawn) => Danger.None;
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) => JobOnThing(pawn, t, false) != null;
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (ShouldSkip(pawn, false) || t == null || !t.Spawned || !t.Position.IsInAllowedArea(pawn)) return null;
            if (def.workType == WorkTypeDefOf.Construction && pawn.Map.GetComponent<AutonomousRimMapComponent>().BaseProjects
                .Any(p => p.State != ConstructionState.Paused && p.OwnedClearCells.Contains(t.Position)) &&
                pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null)
                return ((WorkGiver_Scanner)DefDatabase<WorkGiverDef>.GetNamed("Deconstruct").Worker).JobOnThing(pawn, t, false);
            if (def.workType == WorkTypeDefOf.Hauling && FoodDeliveryNeeded(pawn) && FoodTarget(pawn, t))
                return ((WorkGiver_Scanner)DefDatabase<WorkGiverDef>.GetNamed("HaulGeneral").Worker).JobOnThing(pawn, t, false);
            var ai = pawn.Map.GetComponent<AutonomousRimMapComponent>();
            var project = ai.BaseProjects.FirstOrDefault(p => BaseConstructionManager.CanContinueExistingWork(p) && BaseConstructionManager.Tasks(p).Any(task => task.Pending == t && task.RetryAfter <= Find.TickManager.TicksGame));
            if (project == null) return null;
            bool hauling = def.workType == WorkTypeDefOf.Hauling;
            if (!hauling && t is Frame f && f.def.entityDefToBuild is ThingDef quality && quality.comps?.Any(c => c.compClass == typeof(CompQuality)) == true && !project.Stalled)
            {
                Pawn better = BaseConstructionManager.Builders(pawn.Map).FirstOrDefault(p => p != pawn && p.skills.GetSkill(SkillDefOf.Construction).Level > pawn.skills.GetSkill(SkillDefOf.Construction).Level &&
                    ConstructionWorkManager.CanDispatch(p, WorkTypeDefOf.Construction) && p.CanReach(t, PathEndMode.Touch, Danger.None));
                if (better != null) return null;
            }
            // Keep native finish-before-delivery ordering for constructors. Combining both
            // in one scanner would select a nearby blueprint before a funded frame.
            string[] names = hauling ? new[] { "DeliverResourcesToFrames", "DeliverResourcesToBlueprints" } : new[] { "ConstructFinishFrames" };
            foreach (string name in names)
            {
                var giver = (WorkGiver_Scanner)DefDatabase<WorkGiverDef>.GetNamed(name).Worker;
                Job job = giver.JobOnThing(pawn, t, false);
                if (job != null) return job;
            }
            return null;
        }
    }
}
