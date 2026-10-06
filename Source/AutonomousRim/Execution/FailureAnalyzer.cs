using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Perception;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    public static class FailureAnalyzer
    {
        private static string S(FailureSeverity s)=>s.ToString().ToLowerInvariant();
        private static FailureFinding Finding(FailureCategory category,FailureSeverity severity,string signal,string evidence,string expected,string actual,string improvement,string cause,int impact)
            =>new FailureFinding{Category=category,Severity=severity,Signal=signal,Evidence=evidence,Expected=expected,Actual=actual,Improvement=improvement,Cause=cause,Impact=impact};

        public static string Evaluate(Map map,ColonyState state,FailureMemory memory)
        {
            if(memory==null)return "Análise de falha indisponível.";
            int colonists=map.mapPawns.FreeColonistsSpawnedCount;
            int buildings=map.listerBuildings.allBuildingsColonist.Count;
            memory.HadColony |= colonists>0; memory.PeakColonists=Math.Max(memory.PeakColonists,colonists); memory.PeakBuildings=Math.Max(memory.PeakBuildings,buildings);
            var pawns=map.mapPawns.FreeColonistsSpawned.ToList(); var findings=new List<FailureFinding>();
            int medicine=state.Resources.TryGetValue(ThingDefOf.MedicineIndustrial.defName,out int med)?med:0;
            bool hasGrowing=map.zoneManager.AllZones.OfType<Zone_Growing>().Any(z=>z.Cells.Any(c=>c.InBounds(map)));
            bool hasFoodSource=state.FoodNutrition>0.1f || hasGrowing;
            bool hasPower=map.listerThings.AllThings.OfType<ThingWithComps>().Any(t=>t.TryGetComp<CompPowerTrader>()!=null);
            bool hasProduction=map.listerBuildings.allBuildingsColonist.Any(b=>b.def.building?.workTableRoomRole!=null);
            bool lowMood=pawns.Count>0 && pawns.Count(p=>(p.needs?.mood?.CurLevelPercentage??1f)<0.25f)>=Math.Max(1,(int)Math.Ceiling(pawns.Count*0.5f));
            bool allDowned=colonists>0 && state.DownedColonists>=colonists;
            if(colonists==0 && memory.PeakColonists>0)
                findings.Add(Finding(FailureCategory.Colonos,FailureSeverity.Critico,"Todos os colonos foram perdidos",$"Pico {memory.PeakColonists}; vivos no mapa 0","Ao menos um colono recuperável","Nenhum colono livre no mapa","Encerrar a execução somente depois do relatório; revisar defesa, medicina e aceitação de riscos.","A cadeia de perdas terminou a capacidade de recuperação.",100));
            else if(allDowned && (medicine==0 || !hasProduction || state.EstimatedFoodDays<0.5f))
                findings.Add(Finding(FailureCategory.Medicina,FailureSeverity.Critico,"Incapacitação coletiva sem reserva",$"{state.DownedColonists}/{colonists} incapacitados; medicina {medicine}","Ao menos um colono capaz de tratar e recursos médicos","Todos incapacitados e recuperação sem suporte evidente","Priorizar Doctor, BedRest, medicina e abrigo antes de aceitar novos riscos.","Ferimentos acumulados impediram a recuperação básica.",95));
            if(state.EstimatedFoodDays<0.5f && !hasFoodSource)
                findings.Add(Finding(FailureCategory.Alimentacao,FailureSeverity.Critico,"Fome sem fonte de recuperação",$"Reserva estimada {state.EstimatedFoodDays:0.0} dias; cultivo/fonte útil ausente","Reserva e fonte de alimento ativa","Sem comida suficiente nem fonte detectável","Reduzir consumo, mudar para refeições simples, plantar/capturar e reservar comida antes de expandir.","A reserva alimentar acabou antes de uma fonte alternativa estar pronta.",92));
            else if(state.EstimatedFoodDays<1f)
                findings.Add(Finding(FailureCategory.Alimentacao,FailureSeverity.Grave,"Reserva alimentar crítica",$"Reserva estimada {state.EstimatedFoodDays:0.0} dias","Pelo menos 1 dia e produção em andamento","Menos de 1 dia de comida","Elevar Growing/Cooking, liberar comida próxima e interromper produção cara até recuperar a reserva.","Consumo e produção ficaram desalinhados.",70));
            if(state.HostilePawnCount>0 && state.Threat.EnemyStrength>Math.Max(1f,state.Threat.FriendlyStrength*1.35f))
                findings.Add(Finding(FailureCategory.Defesa,FailureSeverity.Grave,"Ameaça supera a defesa atual",$"Inimigos {state.Threat.EnemyStrength:0.0}; aliados {state.Threat.FriendlyStrength:0.0}; risco {state.Threat.Risk}","Força defensiva compatível ou retirada segura","Resposta atual inferior à ameaça","Usar cover e chokepoints, equipar armas/armaduras e evitar iniciar confronto com colonos feridos.","Equipamentos, posição e preparação não acompanharam a ameaça.",88));
            if(state.HostilePawnCount>0 && state.CombatCapableColonists==0)
                findings.Add(Finding(FailureCategory.Combate,FailureSeverity.Critico,"Nenhum combatente capaz durante ameaça",$"Hostis {state.HostilePawnCount}; combatentes capazes 0","Combatentes equipados ou retirada protegida","Colônia sem resposta de combate","Preservar pelo menos uma reserva de combatentes e priorizar equipamento/medicina durante raids.","A colônia perdeu capacidade de resposta antes da ameaça terminar.",96));
            if(!hasPower && buildings>0)
                findings.Add(Finding(FailureCategory.Economia,FailureSeverity.Grave,"Capacidade de energia ausente",$"Edifícios {buildings}; geradores/power traders detectados 0","Fonte de energia ou alternativa térmica adequada","Nenhuma rede de energia detectável","Pesquisar geração cedo, manter combustível e instalar energia antes de depender de freezer/oficinas.","A infraestrutura dependente foi construída antes da redundância de energia.",66));
            if(!hasProduction && memory.PeakColonists>=2)
                findings.Add(Finding(FailureCategory.Producao,FailureSeverity.Grave,"Capacidade de produção perdida",$"Colonos de pico {memory.PeakColonists}; mesas de trabalho 0","Ao menos cozinha/oficina pesquisada ou plano de emergência","Nenhuma bancada de produção detectável","Preservar uma cozinha e bancada mínima; reduzir expansão quando a produção essencial estiver bloqueada.","A expansão consumiu trabalhadores/recursos sem manter produção básica.",75));
            if(medicine<Math.Max(1,colonists) && (state.DownedColonists>0 || state.HostilePawnCount>0))
                findings.Add(Finding(FailureCategory.Medicina,FailureSeverity.Grave,"Reserva médica insuficiente para o risco",$"Medicina {medicine}; feridos {state.DownedColonists}; hostis {state.HostilePawnCount}","Reserva mínima proporcional a colonos e ameaça","Medicina abaixo do mínimo","Manter depósito médico de alta prioridade e pesquisar/produzir medicina quando a reserva cair.","O risco de combate foi aceito sem reserva médica.",62));
            if(lowMood)
                findings.Add(Finding(FailureCategory.Colonos,FailureSeverity.Moderado,"Humor baixo generalizado",$"Pelo menos metade dos colonos abaixo de 25% de humor","Rotina com recreação, descanso e necessidades básicas atendidas","Humor médio da colônia em queda","Ativar recuperação de agenda, Recreation e reduzir trabalho urgente quando possível.","A rotina manteve trabalho enquanto necessidades básicas se deterioravam.",48));
            var baseComp=map.GetComponent<AutonomousRimMapComponent>();
            if(baseComp!=null && baseComp.BaseProjects.Any(p=>p.State==AutonomousRim.Planning.ConstructionState.Blocked))
                findings.Add(Finding(FailureCategory.Construcao,FailureSeverity.Moderado,"Projetos essenciais bloqueados",$"{baseComp.BaseProjects.Count(p=>p.State==Planning.ConstructionState.Blocked)} módulos bloqueados","Essenciais desbloqueados ou pausados com diagnóstico","Obras não avançam","Abandonar módulos secundários, minerar obstáculos e manter estoque/cozinha/quartos antes de expandir.","O plano acumulou dependências que não foram resolvidas.",44));
            if(baseComp!=null && baseComp.Strategy?.ResearchStatus?.IndexOf("bloqueada",StringComparison.OrdinalIgnoreCase)>=0)
                findings.Add(Finding(FailureCategory.Pesquisa,FailureSeverity.Moderado,"Pesquisa bloqueada por requisito",baseComp.Strategy.ResearchStatus,"Pesquisa com bancada e pré-requisitos disponíveis","Rota tecnológica parada","Construir a bancada exigida e replanejar a rota conforme a urgência atual.","A rota foi escolhida sem concluir a infraestrutura necessária.",52));
            bool critical=findings.Any(f=>f.Severity==FailureSeverity.Critico); bool severe=findings.Count(f=>f.Severity>=FailureSeverity.Grave)>=2;
            if(critical||severe)memory.ConsecutiveCritical++;else memory.ConsecutiveCritical=Math.Max(0,memory.ConsecutiveCritical-1);
            bool terminal=findings.Any(f=>f.Category==FailureCategory.Colonos && f.Severity==FailureSeverity.Critico && f.Signal.StartsWith("Todos"));
            bool declare=terminal || memory.ConsecutiveCritical>=3;
            memory.RecoveryPlanActive=findings.Count>0 && !declare;
            memory.LastEvaluationTick=Find.TickManager.TicksGame;
            if(declare && !memory.FailureDeclared)
            {
                var report=BuildReport(map,state,findings);memory.Reports.Add(report);memory.FailureDeclared=true;memory.Status="Falha completa registrada: "+report.PrimaryCause;Learn(memory,findings);
                return memory.Status;
            }
            memory.Status=findings.Count==0?"Colônia estável; nenhum sinal de colapso detectado.":memory.RecoveryPlanActive?"Risco alto, mas há recuperação plausível; plano de emergência ativo.":"Colapso provável; acumulando evidência para relatório.";
            return memory.Status;
        }

        private static FailureReport BuildReport(Map map,ColonyState state,List<FailureFinding> findings)
        {
            var ordered=findings.OrderByDescending(f=>f.Impact).ToList();var report=new FailureReport{Tick=Find.TickManager.TicksGame,Title="Relatório de falha da colônia",PrimaryCause=ordered[0].Signal};
            report.Findings=ordered;report.Summary="Causa principal: "+ordered[0].Signal+". Foram encontrados "+ordered.Count+" sinais; o último evento não é tratado como causa única.";
            report.CausalChain.Add("Evento final observado: "+ordered[0].Actual);
            foreach(var f in ordered.Take(5)){report.CausalChain.Add(f.Signal+" porque "+f.Cause);report.Improvements.Add(f.Improvement);}
            report.WorkingSystems.Add("O relatório foi preservado no save e pode ser comparado com futuras execuções.");
            if(!findings.Any(f=>f.Category==FailureCategory.Alimentacao))report.WorkingSystems.Add("Alimentação não apresentou sinal crítico no último diagnóstico.");
            if(!findings.Any(f=>f.Category==FailureCategory.Defesa || f.Category==FailureCategory.Combate))report.WorkingSystems.Add("Defesa não apresentou sinal crítico no último diagnóstico.");
            return report;
        }
        private static void Learn(FailureMemory memory,List<FailureFinding> findings)
        {
            foreach(var f in findings.Where(f=>f.Severity>=FailureSeverity.Grave))
            {
                var existing=memory.Learnings.FirstOrDefault(l=>l.Category==f.Category && l.Adjustment==f.Improvement);
                if(existing==null){existing=new FailureLearning{Category=f.Category,Adjustment=f.Improvement};memory.Learnings.Add(existing);} existing.EvidenceCount++;existing.Confidence=Math.Min(0.85f,0.25f+existing.EvidenceCount*0.15f);
            }
        }
        public static void RecordRaidStart(Map map,ColonyState state,FailureMemory memory)
        {
            if(memory==null || Find.TickManager.TicksGame-memory.LastRaidCheckpointTick<600)return;
            var snapshot=new RaidSnapshot{Tick=Find.TickManager.TicksGame,EnemyCount=state.Threat.ActiveCount,Ranged=state.Threat.Ranged,Melee=state.Threat.Melee,EnemyStrength=state.Threat.EnemyStrength,FriendlyStrength=state.Threat.FriendlyStrength,FoodDays=state.EstimatedFoodDays,Downed=state.DownedColonists,Risk=state.Threat.Risk,Outcome="em andamento"};
            string saveName="AutonomousRim_Raid_"+Find.TickManager.TicksGame; snapshot.SaveName=saveName;memory.Raids.Add(snapshot);if(memory.Raids.Count>12)memory.Raids.RemoveAt(0);memory.LastRaidCheckpointTick=snapshot.Tick;
            try{GameDataSaveLoader.SaveGame(saveName);Log.Message("[AutonomousRim] Checkpoint de combate salvo: "+saveName);}catch(Exception e){Log.Warning("[AutonomousRim] Não foi possível salvar checkpoint de combate: "+e.Message);}
        }
        public static void RecordRaidEnd(ColonyState state,FailureMemory memory)
        {
            var last=memory?.Raids?.LastOrDefault();if(last==null || last.Outcome!="em andamento")return;last.Outcome=state.DownedColonists>0?"terminou com feridos":"sobreviveu sem feridos detectados";
        }
    }
}
