using System;
using System.Linq;
using AutonomousRim.Core;
using HarmonyLib;
using Verse;
using Verse.AI;
namespace AutonomousRim.Execution
{
    public static class OrderedJobDispatcher
    {
        [ThreadStatic] internal static int AutomaticDepth;
        public static bool Issue(Pawn pawn,Job job,JobTag? tag=JobTag.Misc,bool queue=false)
        {AutomaticDepth++;try{return pawn.jobs.TryTakeOrderedJob(job,tag,queue);}finally{AutomaticDepth--;}}
    }
    // RimWorld accepts an identical manual order without changing current job ID.
    // Capture human ownership at the API boundary, rather than comparing IDs alone.
    [HarmonyPatch(typeof(Pawn_JobTracker),nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    public static class ManualCombatOwnershipObserver
    {
        public static void Postfix(Job job,bool __result,Pawn ___pawn)
        {
            if(!__result || OrderedJobDispatcher.AutomaticDepth>0 || ___pawn.Map==null)return;
            var ai=___pawn.Map.GetComponent<AutonomousRimMapComponent>();
            if(ai==null || ai.CombatOrders.Any(o=>o.Pawn==___pawn && o.JobId==job.GetUniqueLoadID()))return;
            ai.NotifyManualCombatOrder(___pawn);
        }
    }
}
