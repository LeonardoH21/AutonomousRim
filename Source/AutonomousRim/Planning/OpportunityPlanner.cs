using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using RimWorld;
using Verse;
namespace AutonomousRim.Planning
{
    public sealed class OpportunityPlanner
    {
        public List<string> Rows=new List<string>();
        public void Update(Map map,AutonomousRimMapComponent ai)
        {
            Rows.Clear();
            int available=map.mapPawns.FreeColonistsSpawned.Count(p=>WorkPriorityManager.CanWork(p) && !WorkReadiness.NeedsRecovery(p) && ActorCoordinator.Owner(p)==null);
            foreach(var quest in Find.QuestManager.QuestsListForReading.Where(q=>q.State==QuestState.NotYetAccepted).Take(12))
            {
                var parts=quest.PartsListForReading;
                string[] types=parts.Select(p=>p.GetType().Name).ToArray();
                bool danger=types.Any(t=>t.IndexOf("Raid",StringComparison.OrdinalIgnoreCase)>=0 || t.IndexOf("Assault",StringComparison.OrdinalIgnoreCase)>=0 || t.IndexOf("Threat",StringComparison.OrdinalIgnoreCase)>=0 || t.IndexOf("Condition",StringComparison.OrdinalIgnoreCase)>=0);
                bool travel=types.Any(t=>t.IndexOf("Caravan",StringComparison.OrdinalIgnoreCase)>=0 || t.IndexOf("Site",StringComparison.OrdinalIgnoreCase)>=0);
                var choices=parts.OfType<QuestPart_Choice>().SelectMany(c=>c.choices).ToList();
                var rewardRows=choices.Select(c=>c.rewards.OfType<Reward_Items>().SelectMany(r=>r.ItemsListForReading).Where(t=>
                    ai.GoalGraph.Supplies.ContainsKey(t.def) || ai.Industry.Targets.ContainsKey(t.def) || t.def==ThingDefOf.Silver).Select(t=>t.LabelCap.ToString())).ToList();
                string useful=string.Join(" / ",rewardRows.Select(r=>string.Join(", ",r.Take(5))).Where(r=>r.Length>0));
                string verdict=ai.SecondarySuspended || available<2 || ai.CurrentState.EstimatedFoodDays<3?"Adiar: equipe/reservas indisponíveis":danger||travel?"Revisão necessária: combate/viagem":"Revisão necessária: partes especiais não garantem ausência de risco";
                Rows.Add(quest.name+" • prazo "+(quest.TicksUntilExpiry<0?"sem expiração":(quest.TicksUntilExpiry/60000f).ToString("0.0")+" d")+" • equipe livre "+available+
                    " • "+(useful.Length==0?"recompensa útil não identificada":useful)+" • "+verdict);
            }
            foreach(var faction in Find.FactionManager.AllFactionsListForReading.Where(f=>!f.IsPlayer && !f.def.hidden && !f.def.permanentEnemy).Take(8))
                Rows.Add("Diplomacia: "+faction.Name+" • relação "+faction.PlayerGoodwill+" • "+(faction.HostileTo(Faction.OfPlayer)?"hostil; tratamento/liberação continua sob política de prisão":"comércio possível quando houver trader"));
        }
    }
}
