using System.Linq;
using AutonomousRim.Core;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    // Restrict only untouched commercial AI bills. Native priorities, manual bills and
    // ongoing jobs remain authoritative; the equipment specialist can finish the set.
    [HarmonyPatch(typeof(Bill),nameof(Bill.PawnAllowedToStartAnew))]
    public static class CommerceWorkerProtection
    {
        public static void Postfix(Bill __instance,Pawn p,ref bool __result)
        {
            if(!__result || p?.Map==null)return;
            var ai=p.Map.GetComponent<AutonomousRimMapComponent>();
            if(!ai.CommerceAutomation || !ai.Commerce.Bills.Any(b=>b.Matches && b.Bill==__instance))return;
            var candidates=ai.Coordination.Equipment.Where(d=>d.Ready).SelectMany(d=>d.ReadyPawns).Distinct()
                .Where(q=>WorkPriorityManager.CanWork(q) && !WorkReadiness.NeedsRecovery(q))
                .OrderByDescending(q=>q.skills?.GetSkill(SkillDefOf.Crafting).Level??0).ThenBy(q=>q.thingIDNumber);
            if(candidates.FirstOrDefault()==p)__result=false;
        }
    }
}
