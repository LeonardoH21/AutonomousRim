using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    // Planning does not finish research, spawn resources, draft combatants or activate an ending.
    public static class StrategicPlanner
    {
        private static readonly HashSet<string> VictoryResearchNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "ShipBasics", "ShipCryptosleep", "ShipReactor", "ShipEngine", "ShipComputerCore", "ShipSensorCluster"
        };

        public static bool IsVictoryResearch(ResearchProjectDef project) => project != null && VictoryResearchNames.Contains(project.defName);
        public static IEnumerable<ResearchProjectDef> Dependencies(ResearchProjectDef def) =>
            (def.prerequisites??new List<ResearchProjectDef>()).Concat(def.hiddenPrerequisites??new List<ResearchProjectDef>()).Distinct();
        public static List<ResearchProjectDef> ResearchRoute(IEnumerable<ResearchProjectDef> targets)
        {
            var result=new List<ResearchProjectDef>(); var visited=new HashSet<ResearchProjectDef>();
            void visit(ResearchProjectDef r)
            {if(r==null || r.IsFinished || !visited.Add(r))return; foreach(var d in Dependencies(r))visit(d); result.Add(r);}
            foreach(var target in targets)visit(target);
            return result;
        }

        public static int CalculateStability(Map map, ColonyState state, IReadOnlyList<RoomProject> rooms)
        {
            if (map == null || state == null || state.ColonistCount <= 0) return 0;
            int score = 0;
            bool foodStable = state.DailyFoodNutrition <= 0f || state.EstimatedFoodDays >= state.TargetFoodDays;
            bool foodRecoverable = state.DailyFoodNutrition <= 0f || state.EstimatedFoodDays >= 1f;
            score += foodStable ? 25 : foodRecoverable ? 14 : 0;
            score += state.HostilePawnCount == 0 ? 15 : 0;
            score += state.DownedColonists == 0 ? 12 : 0;

            float averageHealth = state.Pawns.Count == 0 ? 0f : state.Pawns.Average(p => p.Health);
            score += averageHealth >= 0.9f ? 12 : averageHealth >= 0.7f ? 7 : 0;
            var pawns = map.mapPawns.FreeColonistsSpawned;
            float averageMood = pawns.Count == 0 ? 0f : pawns.Average(p => p.needs?.mood?.CurLevelPercentage ?? 0f);
            float averageRest = pawns.Count == 0 ? 0f : pawns.Average(p => p.needs?.rest?.CurLevelPercentage ?? 0f);
            score += averageMood >= 0.55f && averageRest >= 0.45f ? 14 : averageMood >= 0.35f && averageRest >= 0.25f ? 7 : 0;
            score += state.CombatCapableColonists >= Math.Max(1, (int)Math.Ceiling(state.ColonistCount * 0.5f)) ? 10 : 4;
            bool essentialBase = rooms != null && rooms.Any(p => p.Completed && (p.Kind == "Estoque" || p.Kind == "Cozinha" || p.Kind == "Abate"));
            score += essentialBase ? 6 : 0;
            int steel = state.Resources.TryGetValue("Steel", out int steelValue) ? steelValue : 0;
            int components = state.Resources.TryGetValue("ComponentIndustrial", out int componentValue) ? componentValue : 0;
            score += steel >= Math.Max(100, state.ColonistCount * 25) && components >= Math.Max(2, state.ColonistCount / 2) ? 6 : 0;
            return Math.Max(0, Math.Min(100, score));
        }

        public static void Evaluate(Map map,ColonyState state, IReadOnlyList<RoomProject> rooms,StrategicPlan plan,bool executeResearch)
        {
            var colonists=map.mapPawns.FreeColonistsSpawned;
            if(colonists.Count==0)return;
            int tick=Find.TickManager.TicksGame;
            plan.LastEvaluation=tick;
            if (plan.HorizonDays <= 0) plan.HorizonDays = StrategicPlan.DefaultHorizonDays;
            plan.LastKnownDay = tick / GenDate.TicksPerDay;
            plan.EstimatedDaysRemaining = Math.Max(0, plan.HorizonDays - plan.LastKnownDay);
            if(!plan.Landing.IsValid)plan.Landing=colonists.First().Position;
            var buildings=map.listerBuildings.allBuildingsColonist.ToList();
            var items=map.listerThings.AllThings.Where(t=>t.Spawned && t.def.category==ThingCategory.Item && !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer)).ToList();
            bool has(string name)=>buildings.Any(b=>b.def.defName==name);
            bool researched(string name)=>DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name)?.IsFinished==true;
            int beds=buildings.OfType<Building_Bed>().Count(b=>!b.Medical && !b.ForPrisoners);
            bool medical=buildings.OfType<Building_Bed>().Any(b=>b.Medical);
            int meds=items.Where(t=>t.def.IsMedicine).Sum(t=>t.stackCount);
            bool foodLow=state.DailyFoodNutrition>0 && state.EstimatedFoodDays<state.TargetFoodDays;
            bool energy=buildings.OfType<ThingWithComps>().Any(b=>b.TryGetComp<CompPowerTrader>()?.PowerOn==true);
            bool poorWeapons=colonists.Count(p=>p.equipment?.Primary==null)>0;
            int stability = CalculateStability(map, state, rooms);
            bool emergency = state.HostilePawnCount > 0 || state.DownedColonists > 0 ||
                (state.DailyFoodNutrition > 0f && state.EstimatedFoodDays < 1f);
            bool plannedStorage = rooms != null && rooms.Any(r => r.Kind == "Estoque" && (r.Completed || r.FunctionalStorage));
            bool existingStorage = map.zoneManager.AllZones.OfType<Zone_Stockpile>().Any(z => z.Cells.Count > 0);
            bool essentialBase = beds >= colonists.Count && (plannedStorage || existingStorage) && has("TableButcher") && (has("FueledStove") || has("ElectricStove"));
            bool stable = stability >= 65 && !emergency && essentialBase;
            plan.StabilityScore = stability;
            plan.ProgressionAllowed = stable;
            plan.StabilityStatus = stable
                ? $"Estável ({stability}/100): pode ampliar tecnologia e preparar a rota da nave sem pressa."
                : $"Estabilidade limitada ({stability}/100): manter comida, saúde, defesa e infraestrutura antes de acelerar tecnologia/vitória.";
            var priorities=new List<Tuple<string,int>>();
            void target(string id,int score)=>priorities.Add(Tuple.Create(id,score));
            var constructionResearch=new HashSet<ResearchProjectDef>();
            foreach(var room in rooms.Where(r=>!r.Completed))
                foreach(var task in BaseConstructionManager.Tasks(room).Where(t=>t.Def is ThingDef && !t.Def.IsResearchFinished))
                    foreach(var research in ((ThingDef)task.Def).researchPrerequisites.Where(r=>!r.IsFinished))
                    {
                        constructionResearch.Add(research);
                        bool ready=room.Shell.All(t=>t.Complete(map));
                        target(research.defName,room.Priority<=ConstructionPriority.High || ready?97:80);
                    }
            constructionResearch.UnionWith(ResearchRoute(constructionResearch.ToList()));
            target("Electricity",energy?55:100); target("AirConditioning",foodLow?98:65); target("Batteries",energy?55:90);
            target("ComplexFurniture",beds<colonists.Count?85:45); target("Stonecutting",state.Resources.TryGetValue("Steel",out int steel) && steel<colonists.Count*25?88:75); target("ComplexClothing",70);
            target("Smithing",poorWeapons?85:50); target("Machining",poorWeapons?88:60); target("Gunsmithing",poorWeapons?86:60);
            target("DrugProduction",meds<colonists.Count*2?92:55); target("MedicineProduction",meds<colonists.Count*2?89:55);
            target("HospitalBed",state.DownedColonists>0?95:65); target("SolarPanels",energy?55:85); target("GeothermalPower",energy?62:82);
            target("SolarPanels",74);target("Batteries",75);
            target("MicroelectronicsBasics",72); target("MultiAnalyzer",58); target("Fabrication",68);
            target("FlakArmor",70); target("PrecisionRifling",65); target("Hydroponics",foodLow && map.mapTemperature.OutdoorTemp<0?87:35);
            foreach(var name in VictoryResearchNames)target(name,stable?30:5);
            var targets=priorities.OrderByDescending(p=>p.Item2).ThenBy(p=>p.Item1,StringComparer.Ordinal).Select(p=>DefDatabase<ResearchProjectDef>.GetNamedSilentFail(p.Item1)).Where(r=>r!=null).ToList();
            plan.Route=ResearchRoute(targets);
            // Never stop progressing just because the curated route is exhausted.
            if(plan.Route.Count==0)plan.Route=ResearchRoute(DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r=>!r.IsFinished && r.CanStartNow).OrderBy(r=>r.baseCost).ThenBy(r=>r.defName));
            plan.Goals.Clear();
            void goal(string id,string horizon,int priority,string description,bool done,string blocker,string resource,string risk)
                =>plan.Goals.Add(new StrategicGoal{Id=id,Horizon=horizon,Priority=priority,Description=description,Completed=done,Blocker=done?null:blocker,ResourceNeed=resource,Risk=risk,Status=done?"Atendido":blocker});
            goal("stability","Curto",110,"Reserva, saúde, descanso e segurança",stable,"Recuperar estabilidade antes de ampliar a rota tecnológica.","Comida para 3 dias, medicina, energia e colonos descansados","Fome, feridos, hostis ou humor baixo interrompem o avanço.");
            goal("food","Curto",100,"Comida, cozinha e abate separados",!foodLow && has("TableButcher") && (has("FueledStove")||has("ElectricStove")),"Reserva/comida ou bancadas insuficientes; depende da automação de comida e base.","Comida, fogão, butcher e cultivo seguro","Fome e deterioração podem parar a colônia.");
            goal("shelter","Curto",99,"Estoque coberto e cama por colono",beds>=colonists.Count && rooms.Any(r=>r.Kind=="Estoque" && r.Completed),"Construir essenciais antes de ampliar.","Madeira/pedra e camas","Quartos improvisados reduzem descanso e humor.");
            goal("energy","Curto",95,"Geração, rede e conservação de comida",energy && has("Cooler"),"Pesquisar energia/climatização e executar módulos da base.","Gerador, conduítes, bateria e cooler","Temperatura e queda de energia ameaçam a comida.");
            goal("defense","Curto",poorWeapons?94:70,"Armas por colono e perímetro",!poorWeapons && rooms.Any(r=>r.Kind=="Muro externo" && r.Completed),"Equipar e fechar perímetro; killbox e combate coordenado ainda sem executor.","Armas, armadura, portas e muro","A rota de vitória nunca substitui defesa imediata.");
            goal("medicine","Médio",meds<colonists.Count*2?93:65,"Hospital e reserva de medicina",medical && meds>=colonists.Count*2,"Camas médicas, cultivo/allow e produção médica.","Medicina, camas e espaço reservado","Ferimentos e infecções podem bloquear trabalho e pesquisa.");
            goal("industry","Médio",68,"Produção e pesquisa tecnológica",has("FabricationBench") && has("HiTechResearchBench"),"Instalar bancadas desbloqueadas; materiais e trabalhadores reais necessários.","Componentes, aço e um crafter capaz","Bancadas sem recursos viram tarefas bloqueadas.");
            goal("agriculture","Médio",foodLow?90:60,"Culturas e armazenagem por finalidade",rooms.Count(r=>r.GrowingZone!=null)>=5,"Essenciais/quartos e solo fértil; semeadura segue habilidade nativa.","Arroz, linho, batata, medicina e hemp","Clima, pragas e distância podem reduzir a colheita.");
            bool reservePlanned=rooms.Any(r=>r.Kind==RingBasePlanner.ReservationKind);
            goal("expansion","Médio",stable?55:72,"Base modular, corredores, energia e perímetro",reservePlanned,"Reservar módulos e validar terreno antes de expandir.","Clareira, materiais e rotas de fuga","Expansão prematura cria distância e gargalos.");
            bool shipTech=new[]{"ShipBasics","ShipCryptosleep","ShipReactor","ShipEngine","ShipComputerCore","ShipSensorCluster"}.All(researched);
            goal("ship-tech","Longo",stable?30:12,"Tecnologias da nave e seus pré-requisitos",shipTech,"Seguir a árvore de pesquisa nativa, incluindo requisitos ocultos.","Pesquisa, bancada e energia estáveis","Só acelerar quando as necessidades imediatas estiverem atendidas.");
            goal("ship-build","Longo",stable?20:8,"Nave funcional e cápsula para cada colono",false,shipTech?"Pendente: executor de montagem/conectividade e orçamento da nave.":"Aguardar tecnologias; não considera pesquisa como vitória.","Recursos de nave e mão de obra especializada","Construir cedo demais sacrifica defesa, comida e medicina.");
            goal("ship-launch","Longo",stable?10:4,"Preparação, defesa do reator e decolagem",false,"Pendente: executor de defesa, reator e embarque; não inicia o evento automaticamente.","Defesa completa, reator, tripulação e plano de contingência","O lançamento continua uma meta de longo prazo, não uma ordem urgente.");
            plan.Goals=plan.Goals.OrderByDescending(g=>g.Priority).ToList();
            var pending=plan.Goals.Where(g=>!g.Completed).ToList();
            plan.CurrentFocus=pending.Count==0?"Todos os objetivos cadastrados estão atendidos.":FormatFocus(pending[0]);
            plan.NextFocus=pending.Count<2?"Nenhum próximo objetivo pendente.":FormatFocus(pending[1]);
            AuditUnlocks(plan,rooms,buildings);
            if(tick-plan.LastTerrainEvaluation>=60000 || plan.TerrainStatus==null)
            {
                var terrain=AssessTerrainDetails(map,plan.Landing);
                plan.TerrainStatus=terrain.Status; plan.MountainPlanStatus=terrain.MountainStatus;
                plan.MountainCandidate=terrain.MountainCandidate; plan.MountainExcavationApproved=false;
                plan.MountainCells=terrain.MountainCells; plan.MountainOpenCells=terrain.OpenCells; plan.MountainEntrances=terrain.Entrances;
                plan.LastTerrainEvaluation=tick;
            }
            if(executeResearch)ManageResearch(map,plan,state.HostilePawnCount>0,constructionResearch);
            else plan.ResearchStatus=plan.ProgressionAllowed
                ? "Planejamento disponível; pesquisa automática desligada."
                : "Planejamento disponível; pesquisa automática desligada e avanço de vitória aguardando estabilidade.";
        }

        private static string FormatFocus(StrategicGoal goal)
        {
            string blocker=string.IsNullOrEmpty(goal.Blocker)?"": " — "+goal.Blocker;
            string resources=string.IsNullOrEmpty(goal.ResourceNeed)?"":" Recursos: "+goal.ResourceNeed+".";
            return goal.Id+": "+goal.Description+blocker+resources;
        }
        private static void AuditUnlocks(StrategicPlan plan,IReadOnlyList<RoomProject> rooms,List<Building> buildings)
        {
            plan.SeenResearch=DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r=>r.IsFinished).ToList();
            plan.Unlocks=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.researchPrerequisites?.Count>0 && d.IsResearchFinished &&
                (d.building!=null || d.IsWeapon || d.IsApparel)).OrderBy(d=>d.defName).Select(d=>
                d.LabelCap.ToString()+": "+(buildings.Any(b=>b.def==d)?"instalado":rooms.SelectMany(BaseConstructionManager.Tasks).Any(t=>t.Def==d)?"no plano":d.IsWeapon||d.IsApparel?"avaliado pelo autoequipamento quando disponível; fabricação avançada pendente":"disponível; incorporação/posicionamento ainda pendente")).ToList();
            foreach(var recipe in DefDatabase<RecipeDef>.AllDefsListForReading.Where(r=>r.researchPrerequisite!=null && r.researchPrerequisite.IsFinished))
                plan.Unlocks.Add("Receita: "+recipe.LabelCap+" — disponível; execução depende de bancada, ingredientes, habilidades e automação de produção.");
        }
        private static void ManageResearch(Map map,StrategicPlan plan,bool danger,HashSet<ResearchProjectDef> constructionResearch)
        {
            if(map!=Find.Maps.FirstOrDefault(m=>m.IsPlayerHome)) {plan.ResearchStatus="Pesquisa global gerenciada pelo primeiro mapa da colônia.";return;}
            var current=Find.ResearchManager.GetProject();
            if(current!=null && current!=plan.OwnedResearch)plan.ResearchOverride=true;
            if(current==null && plan.OwnedResearch!=null && !plan.OwnedResearch.IsFinished)plan.ResearchOverride=true;
            if(plan.ResearchOverride){plan.ResearchStatus="Escolha manual de pesquisa preservada. Desligue/religue para retomar a IA.";return;}
            if(danger){plan.ResearchStatus="Seleção suspensa por hostis; progresso atual preservado.";return;}
            if(current!=null && !current.IsFinished)
            {
                var required=plan.Route.FirstOrDefault(r=>(plan.ProgressionAllowed || !IsVictoryResearch(r)) && r.CanStartNow);
                if(current==plan.OwnedResearch && required!=null && required!=current && constructionResearch.Contains(required))
                {
                    plan.PreviousResearch=current;plan.OwnedResearch=required;Find.ResearchManager.SetCurrentProject(required);
                    plan.ResearchStatus="Obra planejada aguardando tecnologia: pesquisando "+required.LabelCap+"; progresso anterior preservado.";return;
                }
                if(!plan.ProgressionAllowed && IsVictoryResearch(current) && current==plan.OwnedResearch)
                {
                    var recovery=plan.Route.FirstOrDefault(r=>!IsVictoryResearch(r) && r.CanStartNow);
                    if(recovery!=null)
                    {
                        plan.PreviousResearch=current; plan.OwnedResearch=recovery; Find.ResearchManager.SetCurrentProject(recovery);
                        plan.ResearchStatus="Estabilidade baixa: pesquisa da nave pausada sem perder progresso; retomando "+recovery.LabelCap+"."; return;
                    }
                }
                plan.ResearchStatus="Pesquisando "+current.LabelCap+"; próxima rota recalculada sem perder progresso.";return;
            }
            var next=plan.Route.FirstOrDefault(r=>(plan.ProgressionAllowed || !IsVictoryResearch(r)) && r.CanStartNow);
            if(next==null)
            {
                plan.ResearchStatus=plan.Route.Count==0?"Rota tecnológica concluída; metas de execução continuam.":!plan.ProgressionAllowed
                    ?"Pesquisa de longo prazo aguardando estabilidade; necessidades imediatas continuam como foco."
                    :"Pesquisa bloqueada por bancada, instalação ou requisito especial: "+plan.Route[0].LabelCap;return;
            }
            plan.OwnedResearch=next;
            Find.ResearchManager.SetCurrentProject(next);
            plan.ResearchStatus="Pesquisa selecionada: "+next.LabelCap+". Progresso por trabalho nativo.";
        }
        private sealed class TerrainAssessment
        {
            public string Status, MountainStatus;
            public bool MountainCandidate;
            public int MountainCells, OpenCells, Entrances;
        }

        private static TerrainAssessment AssessTerrainDetails(Map map, IntVec3 landing)
        {
            var nearby=GenRadial.RadialCellsAround(landing,45,true).Where(c=>c.InBounds(map) && !c.Fogged(map)).ToList();
            int mountain=nearby.Count(c=>c.GetRoof(map)?.isThickRoof==true);
            int open=nearby.Count(c=>c.GetEdifice(map)==null && c.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy));
            var adjacentOffsets = new[] { new IntVec3(-1,0,-1), new IntVec3(0,0,-1), new IntVec3(1,0,-1), new IntVec3(-1,0,0), new IntVec3(1,0,0), new IntVec3(-1,0,1), new IntVec3(0,0,1), new IntVec3(1,0,1) };
            int entrances=nearby.Count(c=>c.GetRoof(map)?.isThickRoof==true && adjacentOffsets.Any(a=>
                (c+a).InBounds(map) && (c+a).GetRoof(map)?.isThickRoof!=true && (c+a).GetEdifice(map)==null));
            bool candidate=mountain>=100;
            if(!candidate)
                return new TerrainAssessment
                {
                    MountainCandidate=false, MountainCells=mountain, OpenCells=open, Entrances=entrances,
                    Status="Preferência: base externa modular. Montanha próxima pequena; preservar a planta em anel.",
                    MountainStatus="Montanha insuficiente para um núcleo subterrâneo prioritário; manter expansão externa progressiva."
                };
            return new TerrainAssessment
            {
                MountainCandidate=true, MountainCells=mountain, OpenCells=open, Entrances=entrances,
                Status="Candidata a base parcialmente subterrânea: "+mountain+" células conhecidas de teto espesso; "+open+" células firmes livres próximas. Escavação estratégica bloqueada até validar duas fugas, setores isoláveis, chokepoints/melee block e risco de infestação. O anel externo permanece aprovado.",
                MountainStatus="Plano de montanha: avaliar entradas ("+entrances+" bordas conectadas), espaço livre, rotas de fuga, temperatura, infestação e expansão em etapas; nenhuma escavação automática foi autorizada."
            };
        }

        public static string AssessTerrain(Map map,IntVec3 landing) => AssessTerrainDetails(map,landing).Status;
    }
}
