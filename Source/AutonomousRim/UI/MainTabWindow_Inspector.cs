using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Perception;
using AutonomousRim.Planning;
using RimWorld;
using UnityEngine;
using Verse;

namespace AutonomousRim.UI
{
    // Reads existing planners. Opening or changing pages never starts automation.
    public sealed class MainTabWindow_Inspector : MainTabWindow
    {
        private static readonly Color Accent = new Color(.34f, .78f, .74f);
        private static readonly Color Muted = new Color(.65f, .70f, .75f);
        private static readonly Color Warning = new Color(1f, .72f, .32f);
        private static readonly Color Danger = new Color(1f, .39f, .39f);
        private static readonly Color Panel = new Color(.11f, .14f, .17f, .95f);
        private static readonly string[] Pages = { "Visão geral", "Pesquisa e metas", "Construção", "Colonos", "Comida e estoque", "Defesa e resgate", "Comércio e prisão", "Automações", "Diagnóstico", "Equipamentos" };
        private readonly Vector2[] scroll = new Vector2[Pages.Length];
        private readonly float[] heights = Enumerable.Repeat(1600f, Pages.Length).ToArray();
        private readonly Dictionary<RoomProject, int[]> projectProgress = new Dictionary<RoomProject, int[]>();
        private readonly Dictionary<RoomProject, string> functionality = new Dictionary<RoomProject, string>();
        private ProgressDashboard dashboard;
        private Map dashboardMap;
        private float nextDashboard;
        private int gearStage=-1;
        private int page, lootPage, built, planned;
        private bool onlyBlocked;
        private bool showObservationDetails;
        private int studyReplica=1;
        private string search = "";
        private Map metricMap;
        private float nextMetrics;
        private float y, width;

        public override Vector2 RequestedTabSize => new Vector2(Mathf.Min(1120f, Verse.UI.screenWidth - 40f), Mathf.Min(760f, Verse.UI.screenHeight - 90f));

        public override void DoWindowContents(Rect inRect)
        {
            Color oldColor = GUI.color;
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            try
            {
                GUI.color = Color.white; Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                Map map = Find.CurrentMap;
                var ai = map?.GetComponent<AutonomousRimMapComponent>();
                if (ai == null) { Widgets.Label(inRect, "AutonomousRim • Abra uma colônia para acompanhar a IA."); return; }
                RefreshMetrics(map, ai);
                Header(new Rect(inRect.x, inRect.y, inRect.width, 72f), ai);
                float sidebar = inRect.width < 850f ? 148f : 174f;
                Rect nav = new Rect(inRect.x, inRect.y + 82f, sidebar, inRect.height - 82f);
                Widgets.DrawBoxSolid(nav, Panel);
                for (int i = 0; i < Pages.Length; i++)
                {
                    Rect button = new Rect(nav.x + 8f, nav.y + 10f + i * 37f, nav.width - 16f, 32f);
                    GUI.color = i == page ? Accent : Color.white;
                    if (Widgets.ButtonText(button, Pages[i])) page = i;
                }
                GUI.color = Color.white;
                Rect viewport = new Rect(nav.xMax + 14f, nav.y, inRect.xMax - nav.xMax - 14f, nav.height);
                width = viewport.width - 22f;
                Rect content = new Rect(0, 0, width, Mathf.Max(viewport.height, heights[page]));
                scroll[page].y = Mathf.Clamp(scroll[page].y, 0f, Mathf.Max(0f, content.height - viewport.height));
                Widgets.BeginScrollView(viewport, ref scroll[page], content);
                int renderedPage = page;
                try
                {
                    y = 0;
                    Title(Pages[page]);
                    ColonyState state = ai.CurrentState;
                    if (state == null) Line("Aguardando a primeira leitura da colônia. Os controles de automação já estão disponíveis.", Warning);
                    if (page == 7) Automations(ai);
                    else if (state != null)
                    {
                        switch (page)
                        {
                            case 0: Overview(map, ai, state); break;
                            case 1: Research(map,ai); break;
                            case 2: Construction(map, ai); break;
                            case 3: Colonists(map, ai, state); break;
                            case 4: Food(ai, state); break;
                            case 5: Defense(ai, state); break;
                            case 6: CommercePrison(ai); break;
                            case 8: Diagnostics(ai, state); break;
                            case 9: Gear(map,ai); break;
                        }
                    }
                    // A navigation action may select another page during this draw.
                    // Only update the height of the content actually rendered.
                    heights[renderedPage] = y + 20f;
                }
                finally { Widgets.EndScrollView(); }
            }
            finally { GUI.color = oldColor; Text.Font = oldFont; Text.Anchor = oldAnchor; Text.WordWrap = oldWrap; }
        }

        private void Header(Rect rect, AutonomousRimMapComponent ai)
        {
            Widgets.DrawBoxSolid(rect, Panel);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 4f, rect.height), Accent);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x + 16f, rect.y + 7f, rect.width - 320f, 32f), "AUTONOMOUS RIM");
            Text.Font = GameFont.Small;
            GUI.color = ai.Emergency.Phase == EmergencyPhase.Normal ? Accent : Warning;
            Widgets.Label(new Rect(rect.x + 16f, rect.y + 42f, rect.width - 310f, 26f), Phase(ai.Emergency.Phase) + "  •  " + EnabledCount(ai) + "/11 automações ligadas");
            GUI.color = Color.white;
            if (Widgets.ButtonText(new Rect(rect.xMax - 284f, rect.y + 20f, 130f, 32f), "Ligar tudo")) ai.EnableAll();
            if (Widgets.ButtonText(new Rect(rect.xMax - 146f, rect.y + 20f, 130f, 32f), "Desligar tudo")) ai.DisableAll();
        }

        private void Overview(Map map, AutonomousRimMapComponent ai, ColonyState s)
        {
            Metrics(new[] { "COLONOS", "COMIDA • DIAS", "AMEAÇAS", "OBRAS • ITENS" }, new[] { s.ColonistCount.ToString(), s.EstimatedFoodDays.ToString("0.0"), (s.Threat?.ActiveCount ?? 0).ToString(), built + "/" + planned });
            Section("Situação da colônia");
            Line(ai.EmergencyStatus, ai.Emergency.Phase == EmergencyPhase.Normal ? Accent : Warning);
            if (s.DownedColonists > 0) Line(s.DownedColonists + " colono(s) incapacitado(s).", Danger);
            if (s.EstimatedFoodDays < s.TargetFoodDays) Line("Reserva abaixo da meta de " + s.TargetFoodDays.ToString("0.0") + " dias. " + s.FoodReserveStatus, Warning);
            if (s.FreezerNearFull) Line("Freezer próximo da capacidade: " + s.FreezerFillRatio.ToString("P0") + ".", Warning);
            int blocked = ai.BaseProjects.Count(p => !p.Completed && (p.State == ConstructionState.Blocked || p.Stalled));
            if (blocked > 0) Line(blocked + " projeto(s) bloqueado(s) ou sem progresso. Consulte Construção.", Warning);
            Section("O que a IA está fazendo");
            Summary("Agora: " + ai.Strategy.CurrentFocus);
            Summary("Em seguida: " + ai.Strategy.NextFocus, Muted);
            Line("Construção automática: " + (ai.BaseAutomation ? "ligada" : "desligada") + " • detalhes na página Construção.");
            Line("Trabalho: " + (ai.WorkAutomation ? "automático" : "manual") + " • Agenda: " + (ai.ScheduleAutomation ? "automática" : "manual"), Muted);
            Section("Pesquisa atual"); ResearchBar();
            Section("Base e clima");
            Progress("Paredes, pisos e móveis construídos", planned > 0 ? (float)built / planned : 0f, planned > 0 ? built + " de " + planned : "Sem tarefas de construção", Accent);
            Line("Progresso dos itens físicos do plano; telhados, plantio e funcionamento aparecem por projeto.", Muted);
            Line(CompactBasePlanner.ClimateSummary(map));
            if (Button("Ver controles de automação")) page = 7;
        }

        private ProgressDashboard Dashboard(Map map,AutonomousRimMapComponent ai)
        {
            if(dashboard==null || dashboardMap!=map || Time.realtimeSinceStartup>=nextDashboard)
            {
                dashboardMap=map;dashboard=ProgressDashboard.Read(map,ai,gearStage);nextDashboard=Time.realtimeSinceStartup+5;
            }
            return dashboard;
        }
        private void Research(Map map,AutonomousRimMapComponent ai)
        {
            var view=Dashboard(map,ai);
            ResearchBar();Line(ai.Strategy.ResearchStatus);
            if(Button("Atualizar informações")){nextDashboard=0;return;}
            Section("Ordem atual • até 6 pesquisas");
            Line("A pesquisa selecionada aparece primeiro. A sequência acompanha o plano da IA e pode mudar com as necessidades da colônia.",Muted);
            if(view.Queue.Count==0)Line("Sem pesquisas pendentes na rota atual. Confira as metas de produção e equipamentos.",Accent);
            for(int i=0;i<view.Queue.Count;i++)
            {
                var r=view.Queue[i];bool current=Find.ResearchManager.GetProject()==r;
                Line((i+1)+". "+r.LabelCap+" • "+(current?"Em andamento":r.CanStartNow?"Disponível":"Aguardando requisitos"),current?Accent:Color.white);
                var deps=StrategicPlanner.Dependencies(r).Where(d=>!d.IsFinished).ToList();
                if(deps.Count>0)Line("Antes: "+string.Join(", ",deps.Select(d=>d.LabelCap)),Warning);
                else if(!r.CanStartNow)Line("Verifique a bancada de pesquisa, os equipamentos e os requisitos na árvore de pesquisa.",Warning);
            }
            Section("Metas e checkpoints");
            foreach(var goal in view.Goals.OrderByDescending(g=>g.Priority))
            {
                Line((goal.Completed?"✓ ":"• ")+goal.Description,goal.Completed?Accent:Color.white);
                Line(goal.Horizon+" • "+goal.Status,Muted);
                if(!goal.Completed && !string.IsNullOrEmpty(goal.Blocker))Line("Impedimento: "+goal.Blocker,Warning);
                if(!goal.Completed && !string.IsNullOrEmpty(goal.ResourceNeed))Line("Recursos: "+goal.ResourceNeed,Muted);
            }
        }
        private void Gear(Map map,AutonomousRimMapComponent ai)
        {
            var view=Dashboard(map,ai);var gear=view.Gear;
            Line("Meta exibida: etapa "+(gear.Stage+1)+" • "+ProgressDashboard.Stages[gear.Stage],Accent);
            Line(gearStage<0?"Acompanhando a etapa atual da colônia.":"Prévia de outra etapa; a meta da IA permanece na etapa "+(view.CurrentStage+1)+".",Muted);
            if(Button("Ver próxima etapa")){gearStage=(gear.Stage+1)%5;nextDashboard=0;return;}
            if(gearStage>=0 && Button("Voltar à meta atual")){gearStage=-1;nextDashboard=0;return;}
            if(Button("Atualizar equipamentos e recursos")){nextDashboard=0;return;}
            Metrics(new[]{"CONJUNTOS PRONTOS","MELEE / RANGED","PEÇAS DISPONÍVEIS","PEÇAS A FABRICAR"},
                new[]{gear.Ready+"/"+gear.Fighters,gear.Melee+" / "+gear.Ranged,gear.PendingEquip.ToString(),gear.Craft.Values.Sum().ToString()});
            Section("Recursos para completar os loadouts");
            Line("Necessário / disponível permitido no mapa / falta. Estimativa com tecido ou aço padrão; inclui insumos para fabricar componentes ausentes. Comprar os componentes reduz esses insumos.",Muted);
            if(gear.Materials.Count==0)Line("Sem demanda de materiais para fabricação nesta etapa.",Accent);
            foreach(var pair in gear.Materials.OrderBy(p=>p.Key))
            {
                var def=DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key);
                int stock=def==null?0:CommercePlanner.Stock(ai.map,def);int missing=Math.Max(0,pair.Value-stock);
                int transit=def==null?0:CommercePlanner.InTransit(ai.Commerce,def);
                int reserved=def!=null && ai.Coordination.Materials.TryGetValue(def,out int held)?held:0;
                Line(ProgressDashboard.Label(pair.Key)+": "+pair.Value+" / "+stock+" / faltam "+Math.Max(0,missing-transit)+" • reservados para produção "+reserved+" • em transporte "+transit,missing>transit?Warning:Accent);
            }
            Section("Equipamento de cada colono");
            foreach(var group in gear.Items.GroupBy(row=>row.Pawn))
            {
                var pawn=group.Key;
                Line(pawn.LabelShortCap+" • "+(DefenseProductionPlan.Melee(pawn)?"Melee":"Ranged"),Accent);
                foreach(var row in group)
                {
                    Line("• "+ProgressDashboard.Label(row.Target)+" — "+row.State+(row.Existing!=null?" ("+row.Existing.LabelCap+")":""),row.State=="Equipado"?Muted:row.State=="Disponível para equipar"?Accent:Warning);
                    if(row.State=="Equipado")continue;
                    var research=view.Research[row.Target];
                    Line("Pesquisa: "+(research.Count==0?"requisitos tecnológicos atendidos":string.Join(" → ",research.Select(r=>r.LabelCap))),Muted);
                    Line("Fabricação: "+view.Benches[row.Target],Muted);
                }
            }
            foreach(var pawn in map.mapPawns.FreeColonistsSpawned.Where(p=>!gear.Items.Any(row=>row.Pawn==p)))
                Line(pawn.LabelShortCap+": fora do plano de combate (idade ou incapacidade de violência).",Muted);
            Section("Impedimentos da etapa");
            if(gear.Blockers.Count==0)Line("Nenhum impedimento adicional detectado.",Accent);
            foreach(var blocker in gear.Blockers.Distinct())Line(blocker,Warning);
            Section("Fabricação em andamento e responsáveis");
            foreach(var demand in ai.Coordination.Equipment.Where(d=>d.Bill.recipe.products.Any(p=>p.thingDef.IsApparel || p.thingDef.IsWeapon)))
            {
                var workers=demand.ReadyPawns.OrderByDescending(p=>p.CurJob?.bill==demand.Bill)
                    .ThenByDescending(p=>p.skills?.GetSkill(SkillDefOf.Crafting).Level??0).ToList();
                Line(demand.Bill.LabelCap+" • "+demand.Table.LabelCap+" • "+demand.Blocker, demand.Ready?Muted:Warning);
                Line("Responsável apto: "+(workers.FirstOrDefault()?.LabelShort??"nenhum")+
                    " • substitutos aptos: "+(workers.Count>1?string.Join(", ",workers.Skip(1).Select(p=>p.LabelShort)):"nenhum"),Muted);
            }
        }

        private void ResearchBar()
        {
            var project = Find.ResearchManager.GetProject();
            if (project == null) { Line("Nenhuma pesquisa selecionada.", Muted); return; }
            Progress(project.LabelCap.ToString(), project.ProgressPercent, project.ProgressPercent.ToString("P0"), Accent);
            Line("Progresso da pesquisa nativa do jogo; a velocidade depende dos pesquisadores e das bancadas.", Muted);
        }

        private void Construction(Map map, AutonomousRimMapComponent ai)
        {
            Line("Construção automática: " + (ai.BaseAutomation ? "ligada" : "desligada"), Accent);
            Line("Última avaliação: " + ai.BaseStatus); Line("Última avaliação da coleta: " + ai.GatheringStatus, Muted);
            if (Button("Visualizar / planejar base")) { ai.PreviewBase(); nextMetrics = 0; }
            Widgets.CheckboxLabeled(new Rect(0, y, width, 28f), "Mostrar apenas projetos com impedimentos", ref onlyBlocked); y += 34;
            Progress("Execução física do plano", planned > 0 ? (float)built / planned : 0, built + "/" + planned, Accent);
            foreach (var p in ai.BaseProjects.Where(p => !onlyBlocked || (!p.Completed && (!string.IsNullOrEmpty(p.BlockReason) || p.Stalled || p.State == ConstructionState.WaitingMaterials || p.State == ConstructionState.Blocked))))
            {
                Section(p.Kind + " • " + State(p.State));
                Line("Área " + p.InteriorSize + "×" + p.Height + " • " + Priority(p.Priority) + " • posição " + p.Origin, Muted);
                if (projectProgress.TryGetValue(p, out var n) && n[1] > 0) Progress("Estrutura, pisos e móveis", (float)n[0] / n[1], n[0] + "/" + n[1], Accent);
                if (functionality.TryGetValue(p, out string functional)) Line(functional);
                if (!string.IsNullOrEmpty(p.BlockReason)) Line(p.BlockReason, Warning);
                if (p.Stalled) Line("Sem progresso recente; aguardando recuperação do planejador.", Warning);
                if (p.FunctionalStorage && !p.Completed) Line("Estoque utilizável; proteção climática ainda em obra.", Muted);
            }
        }

        private void Colonists(Map map, AutonomousRimMapComponent ai, ColonyState s)
        {
            Line("Última avaliação da agenda: " + ai.ScheduleStatus); Line("Última avaliação de equipamentos: " + ai.EquipmentStatus, Muted);
            search = Widgets.TextField(new Rect(0, y, width, 30f), search); y += 36;
            Line("Filtrar colonos por nome ou função", Muted);
            foreach (var profile in s.Pawns.Where(p => Matches(p.Name + " " + p.Role, search)))
            {
                Pawn pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.thingIDNumber == profile.PawnId);
                Section(profile.Name + " • " + Role(profile.Role));
                Progress("Saúde", profile.Health, profile.Health.ToString("P0"), profile.Health < .6f ? Warning : Accent);
                if (pawn != null)
                {
                    Need("Humor", pawn.needs?.mood?.CurLevelPercentage);
                    Need("Descanso", pawn.needs?.rest?.CurLevelPercentage);
                    Need("Comida", pawn.needs?.food?.CurLevelPercentage);
                    Need("Recreação", pawn.needs?.joy?.CurLevelPercentage);
                    Line("Atividade: " + (pawn.CurJob?.def.label ?? "Aguardando tarefa") + (pawn.Drafted ? " • convocado" : ""), Muted);
                    if (pawn.workSettings != null)
                    {
                        var jobs = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w => !pawn.WorkTypeIsDisabled(w) && pawn.workSettings.GetPriority(w) > 0)
                            .OrderBy(w => pawn.workSettings.GetPriority(w)).ThenByDescending(w => w.naturalPriority).Take(6);
                        Line("Trabalhos prioritários: " + string.Join(" • ", jobs.Select(w => (w.labelShort ?? w.label ?? w.defName) + " [" + pawn.workSettings.GetPriority(w) + "]")), Muted);
                    }
                    bool enabled = ai.EquipmentAllowedFor(pawn), previous = enabled;
                    Widgets.CheckboxLabeled(new Rect(0, y, width, 28), "Permitir autoequipamento", ref enabled); y += 34;
                    if (enabled != previous) ai.SetEquipmentAllowedFor(pawn, enabled);
                }
                Line("Tiro " + profile.Shooting + " • Corpo a corpo " + profile.Melee + " • Medicina " + profile.Medical);
                Line("Arma: " + profile.Weapon + " • Alcance " + profile.WeaponRange.ToString("0.0"));
                Line("Roupas: " + profile.Apparel);
                Line("Traços: " + profile.Traits); Line(profile.TraitEffects, Muted);
                Line("Arma sugerida: " + profile.RecommendedWeapon, Muted);
                Line("Próximo equipamento: " + profile.RecommendedApparel); Line(profile.EquipmentReason, Muted);
                Line("Proteção por peça: " + profile.ArmorDetails, Muted);
            }
        }

        private void Food(AutonomousRimMapComponent ai, ColonyState s)
        {
            Metrics(new[] { "RESERVA • DIAS", "REFEIÇÕES", "ALVO REFEIÇÕES", "FREEZER" }, new[] { s.EstimatedFoodDays.ToString("0.0"), s.StoredMealCount.ToString(), s.CookingTargetCount.ToString(), s.FreezerFillRatio.ToString("P0") });
            Section("Produção e sobrevivência");
            Progress("Reserva / meta " + s.TargetFoodDays.ToString("0.0") + " dias", s.TargetFoodDays > 0 ? s.EstimatedFoodDays / s.TargetFoodDays : 0, s.FoodReserveLevel, s.EstimatedFoodDays < s.TargetFoodDays ? Warning : Accent);
            Line(s.FoodReserveStatus); Line("Refeição escolhida: " + s.PreferredMeal);
            Line("Nutrição armazenada " + s.FoodNutrition.ToString("0.0") + " • Consumo " + s.DailyFoodNutrition.ToString("0.0") + "/dia");
            Line("Reserva estratégica: alvo " + s.StrategicMealTargetCount + " refeições duráveis");
            Line("Perecíveis " + s.PerishableFoodNutrition.ToString("0.0") + " • Duráveis " + s.LongLifeFoodNutrition.ToString("0.0") + " • Próximos de estragar " + s.NearSpoilingNutrition.ToString("0.0") + " (nutrição)", Muted);
            Section("Armazenamento");
            Progress("Ocupação do freezer", s.FreezerFillRatio, s.FreezerUsedCells + "/" + s.FreezerCapacityCells + " células", s.FreezerNearFull ? Warning : Accent);
            Line(s.FoodStorageStatus); Line("Última avaliação do estoque: " + ai.StorageStatus); Line("Última avaliação do allow: " + ai.LootStatus, Muted);
            Line("Reserva considera alimentos permitidos, dietas e deterioração. Produção futura, visitantes e animais não entram na estimativa.", Muted);
            Section("Recursos observados");
            foreach (var item in s.Resources.OrderBy(r => r.Key)) Line((DefDatabase<ThingDef>.GetNamedSilentFail(item.Key)?.LabelCap.ToString() ?? item.Key) + "  •  " + item.Value);
        }

        private void Defense(AutonomousRimMapComponent ai, ColonyState s)
        {
            var t = s.Threat;
            Metrics(new[] { "HOSTIS ATIVOS", "COMBATENTES", "INCAPACITADOS", "RESGATES ADIADOS" }, new[] { (t?.ActiveCount ?? 0).ToString(), s.CombatCapableColonists.ToString(), s.DownedColonists.ToString(), ai.Emergency.DeferredRescues.ToString() });
            Line(ai.EmergencyStatus); Line(ai.Emergency.Decision); Line("Última avaliação do combate: " + ai.CombatStatus, Muted);
            if (ai.SecondarySuspended) Line("Atividades secundárias temporariamente suspensas pela emergência.", Warning);
            Section("Leitura da ameaça");
            if (t != null)
            {
                Line(t.Classification);
                Line("Atiradores " + t.Ranged + " • Corpo a corpo " + t.Melee + " • Dormentes " + t.Dormant);
                Line("Força estimada: aliados " + t.FriendlyStrength.ToString("0.0") + " / inimigos " + t.EnemyStrength.ToString("0.0"), Muted);
                Line("Estimativa heurística; não representa uma probabilidade de vitória.", Muted);
                foreach (var source in t.Sources.Where(v => v != null).Take(20)) Line(source.LabelShort + " • " + source.Position);
            }
            Section("Ordens dos combatentes");
            if (ai.CombatOrders.Count == 0) Line("Nenhuma ordem de combate ativa.", Muted);
            foreach (var order in ai.CombatOrders) Line(order.Pawn?.LabelShort + " • " + order.Role);
            Section("Resgate e recuperação");
            Line("Abrigos ausentes: " + ai.Emergency.MissingShelters + " • Ordens médicas: " + ai.Emergency.Medical.Count);
            Line(ai.ScheduleStatus);
        }

        private void CommercePrison(AutonomousRimMapComponent ai)
        {
            Section("Comércio"); Line(ai.Commerce.Status); Line(ai.Commerce.CaravanStatus);
            var commerce=ai.Commerce;
            Line("Prata: "+commerce.Silver+" • reserva protegida: "+commerce.Reserve+" • meta: "+commerce.SilverTarget+" • financiamento estimado faltante: "+commerce.FundingGap.ToString("0"),Accent);
            Line("Baseados: "+CommercePlanner.Stock(ai.map,DefDatabase<ThingDef>.GetNamed("SmokeleafJoint"))+" / "+commerce.Target+" • reserva comercial: "+commerce.JointReserve);
            Line(commerce.ProductionStatus??"Produção ainda não avaliada.");
            Line("Transações concluídas: "+commerce.Transactions+" • baseados vendidos: "+commerce.JointSales.Values.Sum());
            Line(commerce.BuyerStatus??"Compradores ainda não avaliados.");
            Line("Última negociação: "+(commerce.LastTrade==0?"nenhuma":"dia "+(commerce.LastTrade/(float)GenDate.TicksPerDay).ToString("0.00"))+" • próxima revisão em "+Math.Max(0,commerce.NextAttempt-Find.TickManager.TicksGame)+" ticks.",Muted);
            Line("Intervalo: "+(commerce.RefreshTicks/2500f).ToString("0.0")+" horas; mudanças de estoque antecipam a revisão.",Muted);
            if(Button("Alternar revisão: 1 / 2 / 4 horas")){commerce.RefreshTicks=commerce.RefreshTicks==2500?5000:commerce.RefreshTicks==5000?10000:2500;commerce.NextAttempt=0;}
            if(!string.IsNullOrEmpty(commerce.Blocker))Line(commerce.Blocker,Warning);
            Toggle("Expedições comerciais", "Permitir planejamento de caravanas comerciais.", ai.Commerce.ExpeditionsEnabled, () => ai.Commerce.ExpeditionsEnabled = !ai.Commerce.ExpeditionsEnabled);
            foreach (var need in ai.Commerce.Needs.Where(n => n.Def != null)) Line("Comprar: " + need.Def.LabelCap + " ×" + need.Count + " • estoque "+CommercePlanner.Stock(ai.map,need.Def)+" • em transporte "+CommercePlanner.InTransit(commerce,need.Def)+" • em fabricação "+CommercePlanner.InProduction(ai.map,need.Def)+" • " + need.Reason);
            foreach (var delivery in ai.Commerce.Deliveries.Where(d => d.Def != null)) Line("Aguardando entrega: " + delivery.Def.LabelCap + " ×" + delivery.Count, Accent);
            foreach (var record in ai.Commerce.History.AsEnumerable().Reverse().Take(6)) Line(record, Muted);
            Section("Prisão e novos colonos"); Line(ai.Prison.Status);
            Line("Capacidade registrada: " + ai.Prison.Capacity + " • Meta de população: " + ai.Prison.PopulationTarget);
            if (Widgets.ButtonText(new Rect(0, y, 110, 30), "− Reduzir")) ai.Prison.PopulationTarget = Math.Max(3, ai.Prison.PopulationTarget - 1);
            if (Widgets.ButtonText(new Rect(118, y, 110, 30), "+ Aumentar")) ai.Prison.PopulationTarget = Math.Min(20, ai.Prison.PopulationTarget + 1); y += 38;
            foreach (var p in ai.Prison.Prisoners.Where(p => !p.Completed)) Line(p.Pawn?.LabelShort + " • " + p.Status);
            foreach (var record in ai.Prison.History.AsEnumerable().Reverse().Take(6)) Line(record, Muted);
        }

        private void Automations(AutonomousRimMapComponent a)
        {
            Line("Cada controle mostra seu estado próprio. A emergência pode suspender tarefas e também acionar o combate.", Muted);
            Section("Base e abastecimento");
            Toggle("Construção da base", "Planejar e executar estruturas, pisos e móveis.", a.BaseAutomation, () => a.SetBaseAutomation(!a.BaseAutomation));
            Toggle("Comida e roupas", "Gerenciar produção e metas das receitas.", a.FoodAutomation, () => a.SetAutomation(!a.FoodAutomation, a.WorkAutomation));
            Toggle("Liberar itens do chão", "Allow gradual conforme necessidades e acesso.", a.LootAutomation, () => a.SetLootAutomation(!a.LootAutomation));
            Section("Colonos e tecnologia");
            Toggle("Autoequipamento", "Selecionar armas e roupas; permissões individuais na página Colonos.", a.EquipmentAutomation, () => a.SetEquipmentAutomation(!a.EquipmentAutomation));
            Toggle("Prioridades de trabalho", "Ajustar profissões de acordo com demanda e capacidade.", a.WorkAutomation, () => a.SetAutomation(a.FoodAutomation, !a.WorkAutomation));
            Toggle("Agenda dinâmica", "Gerenciar trabalho, sono, recreação e recuperação.", a.ScheduleAutomation, () => a.SetScheduleAutomation(!a.ScheduleAutomation));
            Toggle("Planejamento e pesquisa", "Gerenciar etapas tecnológicas e checkpoints.", a.StrategyAutomation, () => a.SetStrategyAutomation(!a.StrategyAutomation));
            Section("Segurança e expansão");
            Toggle("Combate cooperativo", "Posicionar combatentes. Desligar também desliga a emergência automática.", a.CombatAutomation, () => a.SetCombatAutomation(!a.CombatAutomation));
            Toggle("Emergência automática", "Detectar perigo, proteger colonos e coordenar recuperação.", a.EmergencyAutomation, () => a.SetEmergencyAutomation(!a.EmergencyAutomation));
            Toggle("Comércio", "Planejar compras e financiar recursos faltantes.", a.CommerceAutomation, () => a.SetCommerceAutomation(!a.CommerceAutomation));
            Toggle("Prisão", "Gerenciar captura, tratamento, conversão e recrutamento.", a.PrisonAutomation, () => a.SetPrisonAutomation(!a.PrisonAutomation));
        }

        private void Diagnostics(AutonomousRimMapComponent ai, ColonyState s)
        {
            Section("Observação e tendências");
            var observation=ai.Observations.Latest;
            Line(observation==null?"Aguardando checkpoint de percepção.":"Mapa "+observation.MapId+" • checkpoint "+observation.Tick+" • idade "+Math.Max(0,Find.TickManager.TicksGame-observation.Tick)+" ticks • colonos "+observation.Colonists+" • ameaças "+observation.Enemies+" • comida "+observation.FoodDays.ToString("0.0")+" d • bateria "+observation.BatteryEnergy.ToString("0.0")+" Wd",Muted);
            if(Button(showObservationDetails?"Ocultar contexto completo dos checkpoints":"Mostrar contexto completo dos checkpoints"))showObservationDetails=!showObservationDetails;
            if(showObservationDetails && observation!=null)Line(observation.Summary,Muted);
            Line(ai.Observations.Trends(),Muted);
            Line("Política: "+ai.Observations.Policy+" • adicional alimentar: "+ai.Observations.ExtraFoodDays+" dia. Mantém a reserva sazonal nativa.");
            if(Button("Comparar: ativar reserva com +1 dia"))ai.SetObservationPolicy("food-buffer-v1");
            if(ai.Observations.PreviousPolicy!=null && Button("Restaurar política anterior: "+ai.Observations.PreviousPolicy))ai.SetObservationPolicy(ai.Observations.PreviousPolicy);
            if(observation!=null) {
                Line("Política padrão: "+(AutonomousRimMod.Instance?.Evidence?.Describe(observation.Context,"observation-v1")??"Sem arquivo compartilhado."),Muted);
                Line("Reserva ampliada: "+(AutonomousRimMod.Instance?.Evidence?.Describe(observation.Context,"food-buffer-v1")??"Sem arquivo compartilhado."),Muted);
            }
            if(Button("Registrar um dia estável no histórico entre colônias"))Messages.Message(ai.ArchiveObservation(),MessageTypeDefOf.NeutralEvent,false);
            Section("Aprendizado por comparação pareada");
            Line(ai.PolicyComparison.Detail, ai.PolicyComparison.Approved ? Muted : Warning);
            Line("Janelas descritivas não validam políticas. Ensaios exigem o mesmo save inicial, configuração e duração por par; mortes e fome também entram no resultado.",Muted);
            if(ai.PolicyComparison.Approved && Button("Aplicar reserva +1 dia validada neste contexto"))ai.ApplyProvenPolicy();
            var archive=AutonomousRimMod.Instance?.Evidence;
            Line("Ensaios recarregam um checkpoint próprio. Salve sua partida atual antes de executar outro braço; o teste usa os colonos e recursos reais do checkpoint.",Warning);
            if(archive!=null && Button("Criar novo checkpoint de comparação A/B")) {
                try {Messages.Message("Baseline criado: "+PolicyStudyController.SaveBaseline(Find.CurrentMap),MessageTypeDefOf.NeutralEvent,false);}
                catch(Exception error){Messages.Message("Baseline indisponível: "+error.Message,MessageTypeDefOf.NeutralEvent,false);}
            }
            if(archive?.StudySave!=null) {
                Line("Baseline: "+archive.StudySave+" • repetição "+studyReplica,Muted);
                if(Button("Escolher próxima repetição (1–8)"))studyReplica=studyReplica%8+1;
                foreach(string policy in new[]{"observation-v1","food-buffer-v1"})
                    if(Button("Recarregar baseline e testar: "+policy)) {
                        try {PolicyStudyController.Replay(policy,studyReplica);}
                        catch(Exception error){Messages.Message("Ensaio indisponível: "+error.Message,MessageTypeDefOf.NeutralEvent,false);}
                    }
            }
            if(ai.Trial.Result!=null)Line("Ensaio "+ai.Trial.Result.Policy+": "+ai.Trial.Result.Observed+" / "+ai.Trial.Result.Horizon+" ticks • "+
                (ai.Trial.Result.Interrupted?"interrompido; não conta como evidência":ai.Trial.Result.Completed?"concluído":"em andamento"),Muted);
            if(archive!=null && Button("Importar arquivo AutonomousRimPolicyTrials.xml das configurações")) {
                try {
                    int count=TrialExchange.Import(System.IO.Path.Combine(GenFilePaths.ConfigFolderPath,"AutonomousRimPolicyTrials.xml"),archive);
                    AutonomousRimMod.Instance.WriteSettings();Messages.Message(count+" ensaios importados; contextos incompatíveis não recomendam mudanças.",MessageTypeDefOf.NeutralEvent,false);
                }catch(Exception error){Messages.Message("Importação indisponível: "+error.Message,MessageTypeDefOf.NeutralEvent,false);}
            }
            if(archive!=null && Button("Aplicação validada ao carregar: "+(archive.ApplyValidatedPolicies?"ligada":"desligada")))
            {archive.ApplyValidatedPolicies=!archive.ApplyValidatedPolicies;AutonomousRimMod.Instance.WriteSettings();}
            Section("Desempenho medido • todos os mapas desta sessão");
            if(Button("Medição: "+(PerformanceTelemetry.Enabled?"ligada":"desligada")))
            {PerformanceTelemetry.Enabled=!PerformanceTelemetry.Enabled;PerformanceTelemetry.Reset();ai.Performance.Reset();}
            if(Button("Zerar janela de desempenho")){PerformanceTelemetry.Reset();ai.Performance.Reset();}
            Line("TPS observado neste mapa: "+ai.Performance.Tps.ToString("0.0")+" • "+ai.Performance.ObservedTicks+" ticks / "+ai.Performance.Seconds.ToString("0.0")+" s. Pausas, loads e mudanças de velocidade reiniciam a amostragem.",Muted);
            foreach(var timing in PerformanceTelemetry.Snapshot().Take(20))
                Line(timing.Module+" • "+timing.Calls+" chamadas • média "+timing.MeanMs.ToString("0.000")+" ms • p95 "+timing.P95Ms.ToString("0.000")+" ms • máximo "+timing.MaxMs.ToString("0.000")+" ms • falhas "+timing.Failures,Muted);
            Line("Tempos inclusivos: não somar pai e filhos. P95 usa as últimas 512 chamadas; média e máximo cobrem a janela inteira. TPS inclui o restante do jogo.",Muted);
            Line("Thread auxiliar: "+AnalysisWorker.Completed+" tarefas concluídas • "+AnalysisWorker.Milliseconds.ToString("0.0")+" ms acumulados • paralelismo de combate "+(CombatTacticalPlanner.ParallelEnabled?"ligado":"desligado")+". Medição não amplia o uso de threads.",Muted);
            Line("Fila auxiliar: "+AnalysisWorker.Queued+" • recusadas por lotação: "+AnalysisWorker.Rejected+" • falhas: "+AnalysisWorker.Failed,Muted);
            if(Button("Comparar execução: "+(CombatTacticalPlanner.ParallelEnabled?"usar modo síncrono":"usar thread auxiliar existente"))) {
                CombatTacticalPlanner.ParallelEnabled=!CombatTacticalPlanner.ParallelEnabled;
                foreach(var loadedMap in Find.Maps)CombatTacticalPlanner.Clear(loadedMap);
                PerformanceTelemetry.Reset();ai.Performance.Reset();
            }
            Section("Salas existentes • conexões e temperatura");
            foreach(var room in ai.ObservedRooms.Take(16)) {
                Line("#"+room.Id+" • "+room.Role+" • "+room.Cells+" células • "+room.Temperature.ToString("0.0")+" °C • "+room.Vents+" saídas de ar"+(room.Problems.Length==0?"":" • "+room.Problems),room.Problems.Length==0?Muted:Warning);
                Line(string.Join("; ",room.Connections.Take(6)),Muted);
            }
            Line("Conexões físicas não garantem acesso: áreas permitidas, reservas e perigo são verificados pelo trabalho nativo.",Muted);
            Section("Catálogo de receitas do jogo e mods");
            Line(ItemCatalog.Entries.Count+" tipos de itens registrados; produtos variáveis podem exigir análise específica do mod.",Muted);
            foreach(var target in ai.EquipmentDemands.Goals.Where(g=>g.Current && g.Status!="equipped").Select(g=>g.Target).Distinct().Take(8)) {
                var def=DefDatabase<ThingDef>.GetNamedSilentFail(target);
                if(def!=null && ItemCatalog.Entries.TryGetValue(def,out var catalog))Line(def.LabelCap+" • "+catalog.Route,Muted);
            }
            Section("Coordenação de trabalho e materiais");
            foreach(var input in ai.Industry.Requirements.Where(r=>r.Missing>0).Take(10))
                Line("Insumo: " + input.Def.LabelCap + " • faltam " + input.Missing + " / meta " + input.Target + " • " + input.Detail, Muted);
            Line("Demandas por colono: " + ai.EquipmentDemands.Goals.Count(g => g.Current && g.Status == "equipped") + " / " + ai.EquipmentDemands.Goals.Count(g => g.Current) + " peças equipadas.");
            foreach (var goal in ai.EquipmentDemands.Goals.Where(g => g.Current && g.Status != "equipped").Take(12))
                Line((goal.Recipient?.LabelShort ?? "Colono") + " • " + (DefDatabase<ThingDef>.GetNamedSilentFail(goal.Target)?.LabelCap.ToString() ?? goal.Target) +
                    " • " + goal.Detail + (goal.Responsible == null ? "" : " Responsável previsto/atual: " + goal.Responsible) +
                    (Find.TickManager.TicksGame >= goal.ReviewDue ? " Revisão da meta pendente." : ""), Muted);
            Line("Equipamentos: " + ai.Coordination.Equipment.Count(d => d.Ready) + " ordens viáveis / " + ai.Coordination.Equipment.Count + " pendentes.");
            foreach (var role in ai.Coordination.Roles) Line(role, Muted);
            foreach (var material in ai.Coordination.Materials) Line("Reserva para próximo lote: " + material.Key.LabelCap + " × " + material.Value, Muted);
            foreach (var demand in ai.Coordination.Equipment.Where(d => !d.Ready).Take(8)) Line(demand.Bill.LabelCap + " • " + demand.Blocker, Muted);
            Section("Progresso real e bloqueios");
            Line("Atualização a cada 600 ticks; até 6 Bills por leitura. Alerta após 4 horas de trabalho disponível sem avanço. Sono e recuperação não contam.", Muted);
            foreach (var entry in ai.Progress.Entries.OrderByDescending(e => e.Stalled).ThenBy(e => e.Key).Take(30))
            {
                Line((entry.Stalled ? "⚠ " : "") + entry.Label + " • " + entry.Detail, entry.Stalled ? Warning : (Color?)null);
                Line("Leitura tick "+entry.LastSample+" • próxima ação: "+NextDiagnosticAction(entry.Status),Muted);
                if (entry.Key.StartsWith("bill:")) Line("Lotes concluídos observados: " + entry.Completions, Muted);
                if (entry.Key.StartsWith("research:")) Line("Pontos reais: " + entry.Value.ToString("0.0") + " • Sem avanço em trabalho disponível: " + (entry.EligibleTicks / 2500f).ToString("0.1") + " h", Muted);
            }
            Section("Recuperação automática e responsáveis");
            foreach(var attempt in ai.Recovery.Attempts.Take(8))Line(attempt.Key+" • tentativa "+attempt.Attempts+"/3 • "+attempt.Result,Muted);
            foreach(var pawn in Find.CurrentMap.mapPawns.FreeColonistsSpawned)Line(pawn.LabelShort+" • "+(Execution.ActorCoordinator.Owner(pawn)??"disponível para rotina"),Muted);
            Section("Dependências universais • próximo lote");
            foreach(var node in ai.GoalGraph.Nodes.Take(16))Line(node.Kind+" • "+node.Detail+(node.Required>0?" • "+node.Available+"/"+node.Required:""),Muted);
            Section("Inverno e recuperação da batalha");Line(ai.Winter.Status);Line(ai.Aftercare.Status);Line(ai.Doctrine.Status);
            Section("Logística observada");
            foreach(var row in ai.Logistics.Rows.Where(r=>r.Trips>0).OrderByDescending(r=>r.Ticks/r.Trips).Take(6))
                Line(row.Item+" → "+row.Destination+" • "+row.Trips+" transportes • média "+(row.Ticks/(float)row.Trips).ToString("0")+" ticks; tempo inclui espera/coleta, não apenas caminhada.",Muted);
            if(ai.Logistics.Rows.Any(r=>r.Trips>=3 && r.Ticks/r.Trips>600))Line("Sugestão: aproximar insumos em prateleiras do estoque/oficina aprovados. Nenhuma sala será movida automaticamente.",Warning);
            Section("Oportunidades e DLCs • avaliação");
            foreach(var row in ai.Opportunities.Rows.Take(12))Line(row,Muted);
            foreach(var row in ai.Dlc.Rows.Take(12))Line(row,Muted);
            Section("Histórico recente • preservado no save");
            foreach (var item in ai.Progress.Events.AsEnumerable().Reverse().Take(10))
            {
                Line("Tick " + item.Tick + " • mapa "+item.MapId+" • "+(item.Module??"Histórico legado")+" • " + item.Text, Muted);
                if(item.ObservationTick>=0) {
                    Line("Checkpoint "+item.ObservationTick+" • política "+item.Policy,Muted);
                    if(showObservationDetails)Line(item.Context??"",Muted);
                }
            }
            Line("Leitura da colônia: " + Math.Max(0, Find.TickManager.TicksGame - s.Tick) + " ticks atrás. Pausar o jogo também pausa as leituras.", Muted);
            Line(ai.FailureStatus); Line(ai.Strategy.StabilityStatus); Line(ai.Strategy.TerrainStatus); Line(ai.Strategy.MountainPlanStatus);
            if (Button("Analisar riscos agora")) ai.EvaluateFailure();
            if (ai.FailureMemory.Latest != null)
            {
                Section("Último diagnóstico"); var r = ai.FailureMemory.Latest;
                Line(r.PrimaryCause); Line(string.Join(" → ", r.CausalChain.Take(6)));
                foreach (string improvement in r.Improvements) Line(improvement);
            }
            Line("Checkpoints de combate: " + ai.FailureMemory.Raids.Count + " • Relatórios: " + ai.FailureMemory.Reports.Count, Muted);
            Section("Equipamentos identificados • " + s.Loot.Count);
            int pages = Math.Max(1, (s.Loot.Count + 19) / 20); lootPage = Mathf.Clamp(lootPage, 0, pages - 1);
            if (Button("Página " + (lootPage + 1) + "/" + pages + " • próxima")) lootPage = (lootPage + 1) % pages;
            foreach (var item in s.Loot.Skip(lootPage * 20).Take(20))
            {
                Line(item.Name + " • Condição " + item.Condition.ToString("P0") + (item.Forbidden ? " • Proibido" : " • Liberado") + (item.Tainted ? " • Usado por cadáver" : ""));
                Line(item.Location, Muted);
                if (item.Weapon != null) Line("Dano/s " + item.Weapon.DamagePerSecond.ToString("0.0") + " • Alcance " + item.Weapon.Range.ToString("0.0"), Muted);
            }
        }

        private static string NextDiagnosticAction(string status)
        {
            switch(status) {
                case "working": case "crafting": return "Acompanhar avanço real; aguardar conclusão.";
                case "ingredients": return "Conferir ingrediente, filtro, raio e reservas; atender demanda de insumos.";
                case "research": return "Concluir os pré-requisitos indicados.";
                case "bench": return "Conferir bancada, energia, combustível e instalações exigidas.";
                case "worker": return "Atribuir especialista ou substituto capaz; respeitar prioridade manual zero.";
                case "shift": return "Aguardar descanso/recuperação e próximo horário disponível.";
                case "route": return "Conferir porta, obstrução, área permitida e perigo.";
                case "reservation": return "Aguardar reserva nativa; verificar responsável atual.";
                case "emergency": return "Resolver perigo local antes de retomar esta atividade.";
                case "manual": return "Aguardar decisão do jogador; não substituir sua configuração.";
                default: return "Revalidar na próxima leitura; estado da ordem não prova conclusão.";
            }
        }

        private void RefreshMetrics(Map map, AutonomousRimMapComponent ai)
        {
            if (metricMap == map && Time.realtimeSinceStartup < nextMetrics) return;
            metricMap = map; nextMetrics = Time.realtimeSinceStartup + 2f;
            projectProgress.Clear(); functionality.Clear(); built = planned = 0;
            var unique = new HashSet<string>();
            foreach (var p in ai.BaseProjects)
            {
                int count = 0, complete = 0;
                foreach (var task in p.Shell.Concat(p.Furniture).Where(t => t.Def != null && !t.CancelledByPlayer))
                {
                    bool ready = task.Complete(map); count++; if (ready) complete++;
                    string key = task.Position + "/" + task.Def.defName + "/" + task.Rotation.AsInt;
                    if (!unique.Add(key)) continue;
                    planned++; if (ready) built++;
                }
                projectProgress[p] = new[] { complete, count };
                functionality[p] = ModularFacilities.FunctionalStatus(map, ai.BaseProjects, p);
            }
        }

        private void Title(string label) { Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, y, width, 36f), label); Text.Font = GameFont.Small; y += 44; }
        private void Section(string label) { y += 10; GUI.color = Accent; Widgets.Label(new Rect(0, y, width, 28), label); GUI.color = Color.white; y += 30; Widgets.DrawBoxSolid(new Rect(0, y, width, 1), new Color(.3f, .4f, .45f)); y += 10; }
        private void Line(string text, Color? color = null) { if (string.IsNullOrWhiteSpace(text)) return; GUI.color = color ?? Color.white; float h = Text.CalcHeight(text, width); Widgets.Label(new Rect(0, y, width, h), text); GUI.color = Color.white; y += h + 7; }
        private void Summary(string text, Color? color = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            float start = y;
            Line(text.Length > 240 ? text.Substring(0, 237) + "…" : text, color);
            if (text.Length > 240) TooltipHandler.TipRegion(new Rect(0, start, width, y - start), text);
        }
        private bool Button(string text) { bool clicked = Widgets.ButtonText(new Rect(0, y, width, 32), text); y += 40; return clicked; }
        private void Need(string label, float? value) { if (value.HasValue) Progress(label, value.Value, value.Value.ToString("P0"), value.Value < .25f ? Warning : Accent); }
        private void Progress(string label, float value, string detail, Color color)
        {
            Line(label + "  •  " + detail);
            Widgets.DrawBoxSolid(new Rect(0, y, width, 9), Panel);
            Widgets.DrawBoxSolid(new Rect(0, y, width * Mathf.Clamp01(value), 9), color); y += 19;
        }
        private void Metrics(string[] labels, string[] values)
        {
            // Two columns at narrow resolutions, four when room permits.
            int columns = width < 680 ? 2 : 4; float cardWidth = (width - (columns - 1) * 8) / columns;
            float start = y;
            for (int i = 0; i < labels.Length; i++)
            {
                Rect card = new Rect(i % columns * (cardWidth + 8), start + i / columns * 86, cardWidth, 78);
                Widgets.DrawBoxSolid(card, Panel); GUI.color = Muted;
                Text.Font = GameFont.Tiny; Widgets.Label(new Rect(card.x + 10, card.y + 8, card.width - 20, 22), labels[i]);
                Text.Font = GameFont.Medium; GUI.color = Accent;
                Widgets.Label(new Rect(card.x + 10, card.y + 33, card.width - 20, 34), values[i]);
            }
            Text.Font = GameFont.Small; GUI.color = Color.white; y = start + ((labels.Length + columns - 1) / columns) * 86;
        }
        private void Toggle(string label, string description, bool enabled, Action action)
        {
            float textWidth = Mathf.Max(100, width - 126), height = Mathf.Max(74, Text.CalcHeight(description, textWidth - 20) + 40);
            Widgets.DrawBoxSolid(new Rect(0, y, width, height), Panel);
            Widgets.Label(new Rect(12, y + 8, textWidth - 20, 25), label);
            GUI.color = Muted; Widgets.Label(new Rect(12, y + 35, textWidth - 20, height - 35), description);
            GUI.color = enabled ? Accent : Muted;
            if (Widgets.ButtonText(new Rect(width - 108, y + (height - 32) / 2, 96, 32), enabled ? "LIGADO" : "DESLIGADO")) action();
            GUI.color = Color.white; y += height + 8;
        }
        private static bool Matches(string text, string filter) => string.IsNullOrWhiteSpace(filter) || (text ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        private static string Role(string role) => role == "Ranged" ? "Atirador" : role == "Melee" ? "Corpo a corpo" : role;
        private static string Phase(EmergencyPhase phase) => phase == EmergencyPhase.Normal ? "ROTINA NORMAL" : phase == EmergencyPhase.Danger ? "EMERGÊNCIA" : phase == EmergencyPhase.Securing ? "PROTEGENDO A COLÔNIA" : "RECUPERAÇÃO";
        private static string State(ConstructionState state) => new[] { "Planejado", "Em construção", "Aguardando materiais", "Bloqueado", "Pausado", "Concluído" }[(int)state];
        private static string Priority(ConstructionPriority priority) => new[] { "Prioridade crítica", "Prioridade alta", "Prioridade normal", "Prioridade baixa" }[(int)priority];
        private static int EnabledCount(AutonomousRimMapComponent a) => new[] { a.BaseAutomation, a.FoodAutomation, a.LootAutomation, a.EquipmentAutomation, a.WorkAutomation, a.ScheduleAutomation, a.StrategyAutomation, a.CombatAutomation, a.EmergencyAutomation, a.CommerceAutomation, a.PrisonAutomation }.Count(v => v);
    }
}
