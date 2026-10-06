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
        public static void Evaluate(Map map,ColonyState state, IReadOnlyList<RoomProject> rooms,StrategicPlan plan,bool executeResearch)
        {
            var colonists=map.mapPawns.FreeColonistsSpawned;
            if(colonists.Count==0)return;
            int tick=Find.TickManager.TicksGame;
            plan.LastEvaluation=tick;
            if(!plan.Landing.IsValid)plan.Landing=colonists.First().Position;
            var buildings=map.listerBuildings.allBuildingsColonist.ToList();
            var items=map.listerThings.AllThings.Where(t=>t.Spawned && t.def.category==ThingCategory.Item && !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer)).ToList();
            bool has(string name)=>buildings.Any(b=>b.def.defName==name);
            bool researched(string name)=>DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name)?.IsFinished==true;
            int beds=buildings.OfType<Building_Bed>().Count(b=>!b.Medical && !b.ForPrisoners);
            bool medical=buildings.OfType<Building_Bed>().Any(b=>b.Medical);
            int meds=items.Where(t=>t.def.IsMedicine).Sum(t=>t.stackCount);
            bool foodLow=state.DailyFoodNutrition>0 && state.EstimatedFoodDays<ColonyPolicy.TargetFoodDays;
            bool energy=buildings.OfType<ThingWithComps>().Any(b=>b.TryGetComp<CompPowerTrader>()?.PowerOn==true);
            bool poorWeapons=colonists.Count(p=>p.equipment?.Primary==null)>0;
            var priorities=new List<Tuple<string,int>>();
            void target(string id,int score)=>priorities.Add(Tuple.Create(id,score));
            target("Electricity",energy?55:100); target("AirConditioning",foodLow?98:65); target("Batteries",energy?55:90);
            target("ComplexFurniture",beds<colonists.Count?85:45); target("Stonecutting",75); target("ComplexClothing",70);
            target("Smithing",poorWeapons?85:50); target("Machining",poorWeapons?88:60); target("Gunsmithing",poorWeapons?86:60);
            target("DrugProduction",meds<colonists.Count*2?92:55); target("MedicineProduction",meds<colonists.Count*2?89:55);
            target("HospitalBed",state.DownedColonists>0?95:65); target("SolarPanels",energy?55:85); target("GeothermalPower",energy?62:82);
            target("MicroelectronicsBasics",72); target("MultiAnalyzer",58); target("Fabrication",68);
            target("FlakArmor",70); target("PrecisionRifling",65); target("Hydroponics",foodLow && map.mapTemperature.OutdoorTemp<0?87:35);
            foreach(var name in new[]{"ShipBasics","ShipCryptosleep","ShipReactor","ShipEngine","ShipComputerCore","ShipSensorCluster"})target(name,25);
            var targets=priorities.OrderByDescending(p=>p.Item2).ThenBy(p=>p.Item1,StringComparer.Ordinal).Select(p=>DefDatabase<ResearchProjectDef>.GetNamedSilentFail(p.Item1)).Where(r=>r!=null).ToList();
            plan.Route=ResearchRoute(targets);
            // Never stop progressing just because the curated route is exhausted.
            if(plan.Route.Count==0)plan.Route=ResearchRoute(DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r=>!r.IsFinished && r.CanStartNow).OrderBy(r=>r.baseCost).ThenBy(r=>r.defName));
            plan.Goals.Clear();
            void goal(string id,string horizon,int priority,string description,bool done,string blocker)
                =>plan.Goals.Add(new StrategicGoal{Id=id,Horizon=horizon,Priority=priority,Description=description,Status=done?"Atendido":blocker});
            goal("food","Curto",100,"Comida, cozinha e abate separados",!foodLow && has("TableButcher") && (has("FueledStove")||has("ElectricStove")),"Reserva/comida ou bancadas insuficientes; depende da automação de comida e base.");
            goal("shelter","Curto",99,"Estoque coberto e cama por colono",beds>=colonists.Count && rooms.Any(r=>r.Kind=="Estoque" && r.Completed),"Construir essenciais antes de ampliar.");
            goal("energy","Curto",95,"Geração, rede e conservação de comida",energy && has("Cooler"),"Pesquisar energia/climatização e executar módulos da base.");
            goal("defense","Curto",poorWeapons?94:70,"Armas por colono e perímetro",!poorWeapons && rooms.Any(r=>r.Kind=="Muro externo" && r.Completed),"Equipar e fechar perímetro; killbox e combate coordenado ainda sem executor.");
            goal("medicine","Médio",meds<colonists.Count*2?93:65,"Hospital e reserva de medicina",medical && meds>=colonists.Count*2,"Camas médicas, cultivo/allow e produção médica.");
            goal("industry","Médio",68,"Produção e pesquisa tecnológica",has("FabricationBench") && has("HiTechResearchBench"),"Instalar bancadas desbloqueadas; materiais e trabalhadores reais necessários.");
            goal("agriculture","Médio",foodLow?90:60,"Culturas e armazenagem por finalidade",rooms.Count(r=>r.GrowingZone!=null)>=5,"Essenciais/quartos e solo fértil; semeadura segue habilidade nativa.");
            bool shipTech=new[]{"ShipBasics","ShipCryptosleep","ShipReactor","ShipEngine","ShipComputerCore","ShipSensorCluster"}.All(researched);
            goal("ship-tech","Longo",30,"Tecnologias da nave e seus pré-requisitos",shipTech,"Seguir a árvore de pesquisa nativa, incluindo requisitos ocultos.");
            goal("ship-build","Longo",20,"Nave funcional e cápsula para cada colono",false,shipTech?"Pendente: executor de montagem/conectividade e orçamento da nave.":"Aguardar tecnologias; não considera pesquisa como vitória.");
            goal("ship-launch","Longo",10,"Preparação, defesa do reator e decolagem",false,"Pendente: executor de defesa, reator e embarque; não inicia o evento automaticamente.");
            plan.Goals=plan.Goals.OrderByDescending(g=>g.Priority).ToList();
            AuditUnlocks(plan,rooms,buildings);
            if(tick-plan.LastTerrainEvaluation>=60000 || plan.TerrainStatus==null)
            {plan.TerrainStatus=AssessTerrain(map,plan.Landing); plan.LastTerrainEvaluation=tick;}
            if(executeResearch)ManageResearch(map,plan,state.HostilePawnCount>0);
            else plan.ResearchStatus="Planejamento disponível; pesquisa automática desligada.";
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
        private static void ManageResearch(Map map,StrategicPlan plan,bool danger)
        {
            if(map!=Find.Maps.FirstOrDefault(m=>m.IsPlayerHome)) {plan.ResearchStatus="Pesquisa global gerenciada pelo primeiro mapa da colônia.";return;}
            var current=Find.ResearchManager.GetProject();
            if(current!=null && current!=plan.OwnedResearch)plan.ResearchOverride=true;
            if(current==null && plan.OwnedResearch!=null && !plan.OwnedResearch.IsFinished)plan.ResearchOverride=true;
            if(plan.ResearchOverride){plan.ResearchStatus="Escolha manual de pesquisa preservada. Desligue/religue para retomar a IA.";return;}
            if(danger){plan.ResearchStatus="Seleção suspensa por hostis; progresso atual preservado.";return;}
            if(current!=null && !current.IsFinished){plan.ResearchStatus="Pesquisando "+current.LabelCap+"; próxima rota recalculada sem perder progresso.";return;}
            var next=plan.Route.FirstOrDefault(r=>r.CanStartNow);
            if(next==null){plan.ResearchStatus=plan.Route.Count==0?"Rota tecnológica concluída; metas de execução continuam.":"Pesquisa bloqueada por bancada, instalação ou requisito especial: "+plan.Route[0].LabelCap;return;}
            plan.OwnedResearch=next;
            Find.ResearchManager.SetCurrentProject(next);
            plan.ResearchStatus="Pesquisa selecionada: "+next.LabelCap+". Progresso por trabalho nativo.";
        }
        public static string AssessTerrain(Map map,IntVec3 landing)
        {
            var nearby=GenRadial.RadialCellsAround(landing,45,true).Where(c=>c.InBounds(map) && !c.Fogged(map)).ToList();
            int mountain=nearby.Count(c=>c.GetRoof(map)?.isThickRoof==true);
            int open=nearby.Count(c=>c.GetEdifice(map)==null && c.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy));
            if(mountain<100)return "Preferência: base externa modular. Montanha próxima pequena; preservar a planta em anel.";
            return "Candidata a base parcialmente subterrânea: "+mountain+" células conhecidas de teto espesso; "+open+" células firmes livres próximas. Escavação estratégica bloqueada até validar duas fugas, setores isoláveis, chokepoints/melee block e risco de infestação. O anel externo permanece aprovado.";
        }
    }
}
