using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    internal static class ConstructionAccess
    {
        public static bool IsInAllowedArea(this IntVec3 cell, Pawn pawn)
        {
            Area area = pawn.playerSettings?.EffectiveAreaRestrictionInPawnCurrentMap;
            return area == null || area[cell];
        }
    }
    public sealed class ConstructionOrder : IExposable
    {
        public Pawn Pawn;
        public string JobId;
        public Thing Target;
        public int IssuedTick;
        public bool Pending;
        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn"); Scribe_References.Look(ref Target, "target");
            Scribe_Values.Look(ref JobId, "jobId"); Scribe_Values.Look(ref IssuedTick, "issuedTick"); Scribe_Values.Look(ref Pending, "pending");
        }
    }

    public static class ConstructionWorkManager
    {
        public static bool CanDispatch(Pawn p, WorkTypeDef work)
        {
            if (!EquipmentManager.CanAct(p) || !p.workSettings.Initialized || p.WorkTypeIsDisabled(work) || p.workSettings.GetPriority(work) == 0 ||
                p.needs.food?.CurCategory >= HungerCategory.Hungry || p.needs.rest?.CurLevel < 0.35f || p.needs.joy?.CurLevel < 0.25f ||
                p.timetable?.CurrentAssignment == TimeAssignmentDefOf.Sleep || p.timetable?.CurrentAssignment == TimeAssignmentDefOf.Joy) return false;
            Job current = p.CurJob;
            if (current?.def.joyKind != null || current?.def.joyGainRate > 0 || current?.workGiverDef?.workType == WorkTypeDefOf.Construction ||
                current?.targetA.Thing is Frame || current?.targetB.Thing is Frame || current?.targetA.Thing is Blueprint_Build || current?.targetB.Thing is Blueprint_Build) return false;
            if (current?.workGiverDef?.workType != null)
            {
                WorkTypeDef other = current.workGiverDef.workType;
                float foodDays = p.Map.GetComponent<AutonomousRimMapComponent>().CurrentState?.EstimatedFoodDays ?? 0;
                if (other == WorkTypeDefOf.Doctor || other == WorkTypeDefOf.Growing || other == WorkTypeDefOf.Mining || other == WorkTypeDefOf.PlantCutting ||
                    other.defName == "Firefighter" || other.defName == "Patient" || other.defName == "BedRest" ||
                    other == WorkTypeDefOf.Hunting && foodDays < 1f ||
                    other.defName == "Cooking" && (foodDays < 1f || p.workSettings.GetPriority(other) <= p.workSettings.GetPriority(work))) return false;
                if (p.workSettings.GetPriority(other) < p.workSettings.GetPriority(work)) return false;
            }
            return true;
        }
        public static void Apply(Map map, IReadOnlyList<RoomProject> projects, List<ConstructionOrder> orders)
        {
            if (map.mapPawns.AllPawnsSpawned.Any(p => !p.Dead && !p.Downed && p.HostileTo(Faction.OfPlayer))) return;
            foreach (var o in orders) if (o.Pending && o.Pawn?.CurJob?.GetUniqueLoadID() != o.JobId) o.Pending = false;
            var targets = projects.Where(BaseConstructionManager.CanContinueExistingWork).OrderBy(p => p.Priority).SelectMany(p => BaseConstructionManager.CurrentStage(map, p)
                .Where(t => t.Pending?.Spawned == true && t.RetryAfter <= Find.TickManager.TicksGame).Select(t => new { Project = p, Task = t })).ToList();
            var workers = map.mapPawns.FreeColonistsSpawned.Where(WorkPriorityManager.CanWork).OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Construction).Level).ToList();
            int issued = 0;
            foreach (Pawn pawn in workers)
            {
                if (issued >= 3 || orders.Any(o => o.Pending && o.Pawn == pawn) || orders.Any(o => o.Pawn == pawn && Find.TickManager.TicksGame - o.IssuedTick < 180)) continue;
                bool assigned = false;
                foreach (string name in new[] { "ConstructFinishFrames", "DeliverResourcesToFrames", "DeliverResourcesToBlueprints", "ConstructDeliverResourcesToFrames", "ConstructDeliverResourcesToBlueprints" })
                {
                    WorkGiverDef def = DefDatabase<WorkGiverDef>.GetNamed(name);
                    var scanner = def.Worker as WorkGiver_Scanner;
                    if (!CanDispatch(pawn, def.workType) || scanner == null || scanner.ShouldSkip(pawn, false)) continue;
                    foreach (var target in targets.OrderBy(t => t.Project.Priority).ThenBy(t => pawn.Position.DistanceToSquared(t.Task.Position)))
                    {
                        var task = target.Task;
                        if (name == "ConstructFinishFrames" && pawn.skills.GetSkill(SkillDefOf.Construction).Level < task.Def.constructionSkillPrerequisite || !task.Position.IsInAllowedArea(pawn)) continue;
                        if (name == "ConstructFinishFrames" && task.Def is ThingDef thing && thing.comps?.Any(c => c.compClass == typeof(CompQuality)) == true)
                        {
                            Pawn best = workers.FirstOrDefault(b => CanDispatch(b, WorkTypeDefOf.Construction) && !b.WorkTypeIsDisabled(WorkTypeDefOf.Construction) &&
                                b.CanReach(task.Pending, PathEndMode.Touch, Danger.None));
                            if (best != null && best != pawn && !target.Project.Stalled) continue;
                        }
                        Job job = scanner.JobOnThing(pawn, task.Pending, false);
                        Area area = pawn.playerSettings?.EffectiveAreaRestrictionInPawnCurrentMap;
                        if (job == null || area != null && job.AnyTargetOutsideArea(area)) continue;
                        if (Start(pawn, job, def, task.Pending, orders)) { assigned = true; issued++; break; }
                    }
                    if (assigned) break;
                }
                if (assigned || !CanDispatch(pawn, WorkTypeDefOf.Construction)) continue;
                var roofDef = DefDatabase<WorkGiverDef>.GetNamed("BuildRoofs");
                var roofer = (WorkGiver_Scanner)roofDef.Worker;
                foreach (var cell in projects.Where(p => p.State == ConstructionState.Active).SelectMany(p => p.RoofOrders).Where(c => !c.Roofed(map)))
                {
                    if (!cell.IsInAllowedArea(pawn) || !roofer.HasJobOnCell(pawn, cell, false)) continue;
                    Job job = roofer.JobOnCell(pawn, cell, false);
                    if (job != null && Start(pawn, job, roofDef, null, orders)) { issued++; break; }
                }
            }
            orders.RemoveAll(o => !o.Pending && Find.TickManager.TicksGame - o.IssuedTick > 1800);
        }
        private static bool Start(Pawn p, Job job, WorkGiverDef def, Thing target, List<ConstructionOrder> orders)
        {
            // Use an ordinary work job: no forced flag, ignored restrictions or modified work speed.
            job.workGiverDef = def; job.playerForced = false; job.ignoreForbidden = false;
            string id = job.GetUniqueLoadID();
            p.jobs.StartJob(job, JobCondition.InterruptOptional, tag: JobTag.Misc, preToilReservationsCanFail: true);
            if (p.CurJob?.GetUniqueLoadID() != id) return false;
            orders.Add(new ConstructionOrder { Pawn = p, JobId = id, Target = target, IssuedTick = Find.TickManager.TicksGame, Pending = true });
            return true;
        }
        public static void NotifyEnding(Pawn pawn, Job job, JobCondition condition, IReadOnlyList<RoomProject> projects, List<ConstructionOrder> orders)
        {
            var order = orders.FirstOrDefault(o => o.Pending && o.Pawn == pawn && o.JobId == job?.GetUniqueLoadID());
            if (order != null) order.Pending = false;
            // Native scheduler jobs from our WorkGivers need the same failure tracking
            // as executor requests. Never apply backoff to a manually forced job.
            var task = order != null ? projects.SelectMany(BaseConstructionManager.Tasks).FirstOrDefault(t => t.Pending == order.Target) :
                job?.playerForced == false && job.workGiverDef?.defName.StartsWith("AutonomousRim", StringComparison.Ordinal) == true ?
                projects.SelectMany(BaseConstructionManager.Tasks).FirstOrDefault(t => t.Pending != null && (job.targetA.Thing == t.Pending || job.targetB.Thing == t.Pending)) : null;
            if (task == null) return;
            if (condition == JobCondition.Succeeded) { task.FailedJobs = 0; task.RetryAfter = 0; task.LastFailure = null; return; }
            if (condition != JobCondition.Incompletable && condition != JobCondition.Errored && condition != JobCondition.ErroredPather) return;
            task.FailedJobs++; task.RetryAfter = Find.TickManager.TicksGame + Math.Min(1800, task.FailedJobs * 180);
            task.LastFailure = $"Job {job.def.defName} falhou ({condition}), tentativa {task.FailedJobs}; reavaliar acesso/material/reservas.";
            Log.Message("[AutonomousRim] " + task.LastFailure);
        }
        public static void CleanInvalidReservations(Map map, IEnumerable<RoomProject> projects)
        {
            var targets = new HashSet<Thing>(projects.SelectMany(BaseConstructionManager.Tasks).Select(t => t.Pending).Where(t => t != null));
            foreach (var r in map.reservationManager.ReservationsReadOnly.ToList())
                if (r.Target.HasThing && (targets.Contains(r.Target.Thing) || r.Job != null &&
                    (targets.Contains(r.Job.targetA.Thing) || targets.Contains(r.Job.targetB.Thing))) && (r.Claimant == null || r.Claimant.Destroyed || r.Claimant.Dead ||
                    r.Claimant.jobs == null || !r.Claimant.jobs.AllJobs().Any(j => j == r.Job)))
                    map.reservationManager.Release(r.Target, r.Claimant, r.Job);
        }
        public static void Stop(List<ConstructionOrder> orders)
        {
            foreach (var o in orders.Where(o => o.Pending && o.Pawn?.CurJob?.GetUniqueLoadID() == o.JobId))
                if (!o.Pawn.CurJob.playerForced) o.Pawn.jobs.EndCurrentJob(JobCondition.InterruptOptional);
            orders.Clear();
        }
    }
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
    public static class ConstructionJobObserver
    {
        public static void Prefix(Pawn_JobTracker __instance, JobCondition condition, Pawn ___pawn)
        {
            if (___pawn.Map != null && __instance.curJob != null)
                ___pawn.Map.GetComponent<Core.AutonomousRimMapComponent>()?.NotifyConstructionJobEnding(___pawn, __instance.curJob, condition);
        }
    }
}
