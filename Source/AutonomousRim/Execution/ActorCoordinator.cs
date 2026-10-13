using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    // Read actual module ownership; no second collection of pawn reservations can
    // outlive its native job. Rescue may release owned combat through its existing gate.
    public static class ActorCoordinator
    {
        public static string Owner(Pawn pawn)
        {
            var ai=pawn?.Map?.GetComponent<AutonomousRimMapComponent>();
            if(ai==null)return null;
            if(MedicalRescueManager.Reserved(pawn))return "medicina";
            if(CommerceCaravanManager.Reserved(ai.Commerce,pawn))return "caravana";
            if(PrisonManager.Reserved(pawn))return "prisao";
            if(ai.Commerce.Negotiator==pawn && ai.Commerce.JobId!=null &&
                (pawn.CurJob?.GetUniqueLoadID()==ai.Commerce.JobId || pawn.jobs.jobQueue.Any(q=>q.job.GetUniqueLoadID()==ai.Commerce.JobId)))return "comercio";
            if(ai.CombatOrders.Any(o=>o.Pawn==pawn) && pawn.Drafted)return "combate";
            if(ai.Recovery.Attempts.Any(a=>a.Worker==pawn && a.JobId!=null && pawn.CurJob?.GetUniqueLoadID()==a.JobId))return "producao";
            if(ai.EquipmentOrders.Any(o=>o.Pawn==pawn && o.Pending && pawn.CurJob?.GetUniqueLoadID()==o.JobId))return "equipamento";
            if(ai.ConstructionOrders.Any(o=>o.Pawn==pawn && o.Pending && pawn.CurJob?.GetUniqueLoadID()==o.JobId))return "construcao";
            if(pawn.CurJob?.playerForced!=true && pawn.CurJob?.bill is Bill_Production bill && ai.ManagedProduction.Any(m=>m.Matches && m.Bill==bill))return "producao";
            if(pawn.CurJob?.playerForced!=true && pawn.CurJob?.def==JobDefOf.Research && ai.StrategyAutomation)return "pesquisa";
            if(pawn.CurJob?.playerForced==true)return "manual";
            return null;
        }
        public static bool Blocked(Pawn pawn,string requester)
        {
            string owner=Owner(pawn);
            if(requester=="medicina" && (owner=="producao" || owner=="construcao" || owner=="pesquisa" || owner=="equipamento"))return false;
            if(requester=="combate" && (owner=="producao" || owner=="construcao" || owner=="pesquisa" || owner=="equipamento"))return false;
            return owner!=null && owner!=requester && !(requester=="medicina" && owner=="combate");
        }
    }
}
