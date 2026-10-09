using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    [StaticConstructorOnStartup]
    public static class HaulingLoopDiagnostics
    {
        private static int count;
        private static readonly Dictionary<int,int> last=new Dictionary<int,int>();
        static HaulingLoopDiagnostics()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimhauldiagnostic"))return;
            var harmony=new Harmony("autonomousrim.haul.diagnostic");
            harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker),"CleanupCurrentJob"),prefix:new HarmonyMethod(typeof(HaulingLoopDiagnostics),nameof(Ending)));
        }
        public static void Ending(Pawn_JobTracker __instance,Pawn ___pawn,JobCondition condition)
        {
            var job=__instance.curJob;
            if(count>=120 || ___pawn.Faction!=Faction.OfPlayer || ___pawn.Map==null || job==null ||
                (job.def!=JobDefOf.HaulToCell && job.def!=JobDefOf.HaulToContainer && job.def!=JobDefOf.FinishFrame))return;
            int now=Find.TickManager.TicksGame;
            bool repeated=last.TryGetValue(___pawn.thingIDNumber,out int previous)&&now==previous;
            last[___pawn.thingIDNumber]=now;
            if(!repeated && condition==JobCondition.Succeeded)return;
            count++;
            var cell=job.targetB.Cell;
            var carried=___pawn.carryTracker.CarriedThing;
            Log.Message("[HaulDiagnostic] tick="+now+" pawn="+___pawn.LabelShort+" condition="+condition+" repeated="+repeated+
                " job="+job+" giver="+job.workGiverDef?.defName+" mode="+job.haulMode+" toil="+__instance.curDriver?.CurToilIndex+
                " count="+job.count+" carried="+carried+" position="+___pawn.Position+" Bcontents="+
                (cell.InBounds(___pawn.Map)?string.Join(",",cell.GetThingList(___pawn.Map)):"outside")+
                " caller="+string.Join(" > ",new StackTrace().GetFrames().Skip(2).Take(5).Select(f=>f.GetMethod().DeclaringType?.Name+"."+f.GetMethod().Name)));
        }
    }
}
