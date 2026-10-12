using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    public static class ResearchDiagnostics
    {
        public static BillDiagnostic Inspect(Map map,AutonomousRimMapComponent ai)
        {
            var project=Find.ResearchManager.GetProject();
            if(project==null)return new BillDiagnostic("project","Nenhuma pesquisa selecionada.");
            if(ai.SecondarySuspended)return new BillDiagnostic("emergency","Pesquisa pausada pela emergência local ativa.");
            var benches=map.listerBuildings.AllBuildingsColonistOfClass<Building_ResearchBench>()
                .Where(b=>project.CanBeResearchedAt(b,false)).ToList();
            if(benches.Count==0)return new BillDiagnostic("bench","Sem bancada energizada compatível com a pesquisa e seus requisitos.");
            var assigned=map.mapPawns.FreeColonistsSpawned.Where(p=>p.workSettings!=null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Research) &&
                p.workSettings.GetPriority(WorkTypeDefOf.Research)>0).ToList();
            var working=assigned.FirstOrDefault(p=>p.CurJob?.def==JobDefOf.Research && benches.Contains(p.CurJob.targetA.Thing as Building_ResearchBench));
            if(working!=null)return new BillDiagnostic("working",working.LabelShort+" pesquisando; medir pontos reais.",true);
            if(assigned.Count==0)return new BillDiagnostic("worker","Nenhum pesquisador atribuído; verificar prioridade manual zero.");
            var shift=assigned.Where(p=>WorkPriorityManager.CanWork(p) && BillDiagnostics.OnShift(p)).ToList();
            if(shift.Count==0)return new BillDiagnostic("shift","Pesquisadores em descanso, recuperação ou outra indisponibilidade.");
            var routes=shift.SelectMany(p=>benches.Where(b=>!b.IsForbidden(p) && b.InteractionCell.IsInAllowedArea(p) && !b.IsBurning() && b.InteractionCell.Standable(map) &&
                p.CanReach(b.InteractionCell,PathEndMode.OnCell,Danger.Some)).Select(b=>new{Pawn=p,Bench=b})).ToList();
            if(routes.Count==0)return new BillDiagnostic("route","Bancada proibida, em chamas, obstruída ou fora da área/rota dos pesquisadores.");
            var ready=routes.FirstOrDefault(r=>r.Pawn.CanReserve(r.Bench) && r.Pawn.CanReserveSittableOrSpot(r.Bench.InteractionCell));
            if(ready==null)return new BillDiagnostic("reservation","Bancada ou posição de trabalho reservada; aguardar liberação nativa.");
            return new BillDiagnostic("waiting",ready.Pawn.LabelShort+" tem acesso e reserva viáveis. "+
                WorkCoordination.DescribeConflict(ready.Pawn,new[]{WorkTypeDefOf.Research}),true);
        }
    }
}
