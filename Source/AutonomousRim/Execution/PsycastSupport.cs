using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
namespace AutonomousRim.Execution
{
    public static class PsycastSupport
    {
        public static bool TryStun(CombatOrder order,Pawn target)
        {
            var pawn=order.Pawn;
            if(!ModsConfig.RoyaltyActive || !pawn.Drafted || target==null || !Perception.ThreatScanner.Active(target) || target.stances.stunner.Stunned || target.Position.DistanceTo(pawn.Position)>8 ||
                Find.TickManager.TicksGame-order.LastOrderTick<180 || pawn.stances.FullBodyBusy ||
                !(pawn.CurJob==null || pawn.CurJob.def==JobDefOf.Wait_Combat) ||
                !pawn.Map.mapPawns.FreeColonistsSpawned.Any(p=>target.Position.DistanceTo(p.Position)<3))return false;
            var ability=pawn.abilities?.abilities.FirstOrDefault(a=>a is Psycast && a.def.defName=="Stun" && !a.GizmoDisabled(out var reason) && a.CanCast && a.CanQueueCast && a.CanApplyOn((LocalTargetInfo)target) && a.verb.CanHitTarget(target) && a.EffectComps.All(c=>c.Valid((LocalTargetInfo)target,false)));
            if(ability==null)return false;
            var job=ability.GetJob(target,LocalTargetInfo.Invalid);
            if(job==null || !OrderedJobDispatcher.Issue(pawn,job,JobTag.Misc,false))return false;
            order.JobId=job.GetUniqueLoadID();order.LastOrderTick=Find.TickManager.TicksGame;order.Role="Apoio psíquico: atordoar ameaça próxima";return true;
        }
    }
}
