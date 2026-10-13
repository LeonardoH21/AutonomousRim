using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;
namespace AutonomousRim.Planning
{
    public sealed class DlcReadiness
    {
        public List<string> Rows=new List<string>();
        public void Update(Map map)
        {
            Rows.Clear();
            if(ModsConfig.IdeologyActive)
                foreach(var ideo in map.mapPawns.FreeColonistsSpawned.Select(p=>p.Ideo).Where(i=>i!=null).Distinct())
                {
                    foreach(var role in ideo.PreceptsListForReading.OfType<Precept_Role>())
                    {
                        var chosen=role.ChosenPawns().ToList();
                        Rows.Add("Papel: "+role.LabelCap+" • "+(chosen.Count==0?"não atribuído":string.Join(", ",chosen.Select(p=>p.LabelShort))));
                        foreach(var pawn in chosen)foreach(var text in role.AllApparelRequirementLabels(pawn.gender,pawn))Rows.Add(pawn.LabelShort+" • requisito de roupa: "+text);
                    }
                    Rows.Add("Preceitos ativos: "+string.Join(", ",ideo.PreceptsListForReading.Where(p=>!(p is Precept_Role)).Take(8).Select(p=>p.LabelCap.ToString()))+". Alterar ideologia/papéis não é automático.");
                }
            if(ModsConfig.RoyaltyActive)
                foreach(var pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    int powers=pawn.abilities?.abilities.Count(a=>a is Psycast)??0;
                    int permits=pawn.royalty?.AllFactionPermits.Count??0;
                    if(powers+permits>0)Rows.Add(pawn.LabelShort+" • "+powers+" poderes, "+permits+" permissões reais; atordoar ameaça próxima quando o job nativo permitir.");
                }
            if(ModsConfig.BiotechActive)Rows.Add("Mechs: reconhecer infraestrutura antes de automatizar gestação/manutenção; execução ainda pendente.");
            if(ModsConfig.AnomalyActive)Rows.Add("Contenção: automação de captura de entidades pendente; sem manipular contenção no escuro.");
        }
    }
}
