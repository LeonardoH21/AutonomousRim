using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using AutonomousRim.Perception;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AutonomousRim.Execution
{
    public static class CommerceCaravanManager
    {
        public static bool Reserved(CommerceState state,Pawn pawn)=>!state.ExpeditionManual && state.ExpeditionPawns.Contains(pawn);
        public static void Apply(Map map,ColonyState colony,CommerceState state,bool commerceEnabled)
        {
            int tick=Find.TickManager.TicksGame;
            if(state.ExpeditionPawns.Count>0)
            {
                Follow(map,state,commerceEnabled && state.ExpeditionsEnabled);
                return;
            }
            state.CaravanStatus=state.ExpeditionsEnabled?"Expedições aguardam déficit persistente, equipe, reservas e rota segura.":"Expedições automáticas desligadas; comércio local/orbital disponível.";
            if(!commerceEnabled || !state.ExpeditionsEnabled || tick<state.NextExpedition || state.Needs.Count==0 ||
                tick-state.LastDemandTick<GenDate.TicksPerDay*2 || !CommerceManager.Safe(map) || colony.EstimatedFoodDays<7 ||
                colony.DownedColonists>0 || map.mapPawns.FreeColonistsSpawned.Any(p=>p.health.hediffSet.BleedRateTotal>0))return;
            state.NextExpedition=tick+GenDate.TicksPerDay;
            var healthy=map.mapPawns.FreeColonistsSpawned.Where(CommerceManager.Available).Where(PawnAnalyzer.IsCombatReady).ToList();
            if(healthy.Count<5){state.CaravanStatus="Caravana bloqueada: manter ao menos três defensores aptos em casa.";return;}
            var doctors=map.mapPawns.FreeColonistsSpawned.Where(p=>!p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)).OrderByDescending(p=>p.skills.GetSkill(SkillDefOf.Medicine).Level).ToList();
            var protectedPawn=doctors.FirstOrDefault();
            var candidates=healthy.Where(p=>p!=protectedPawn && !p.IsQuestLodger() && !p.IsQuestHelper()).OrderByDescending(p=>p.GetStatValue(StatDefOf.TradePriceImprovement)).ToList();
            if(candidates.Count<2)return;
            var team=new List<Pawn>{candidates[0],candidates.Skip(1).OrderByDescending(p=>p.skills.GetSkill(SkillDefOf.Shooting).Level+p.skills.GetSkill(SkillDefOf.Melee).Level).First()};
            var remaining=healthy.Except(team).ToList();
            if(!remaining.Any(p=>!p.WorkTypeIsDisabled(WorkTypeDefOf.Growing)) || !remaining.Any(p=>!p.WorkTypeIsDisabled(DefDatabase<WorkTypeDef>.GetNamed("Cooking"))))return;
            if(map.mapTemperature.OutdoorTemp<0 || map.mapTemperature.OutdoorTemp>32)
            {state.CaravanStatus="Caravana aguarda clima ameno; viagem não deve consumir preparo de inverno.";return;}
            var destinations=Find.WorldObjects.Settlements.Where(s=>s.Spawned && s.Tile.Layer==map.Tile.Layer && !s.HasMap &&
                s.Faction!=null && s.Faction!=Faction.OfPlayer && !s.Faction.HostileTo(Faction.OfPlayer) && !s.Faction.def.permanentEnemy &&
                Find.WorldReachability.CanReach(map.Tile,s.Tile)).Select(s=>new{Settlement=s,Ticks=CaravanArrivalTimeEstimator.EstimatedTicksToArrive(map.Tile,s.Tile,null)})
                .Where(s=>s.Ticks>0 && s.Ticks<=GenDate.TicksPerDay*2).OrderBy(s=>s.Ticks).ToList();
            foreach(var destination in destinations)
            {
                int travelTicks=destination.Ticks*2+GenDate.TicksPerDay*2;
                var reachable=CaravanFormingUtility.AllReachableColonyItems(map).Where(t=>t.Spawned && !t.IsForbidden(Faction.OfPlayer) &&
                    !t.Position.Fogged(map) && team.All(p=>p.CanReach(t,PathEndMode.Touch,Danger.None)) &&
                    !map.reservationManager.IsReservedByAnyoneOf(t,Faction.OfPlayer)).ToList();
                var quantities=new Dictionary<Thing,int>();
                float daily=team.Sum(p=>p.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed,true)*GenDate.TicksPerDay);
                float foodNeeded=daily*travelTicks/GenDate.TicksPerDay;
                float foodPacked=0;
                foreach(var food in reachable.Where(t=>t.def.IsIngestible && !t.def.IsDrug && !t.def.IsCorpse &&
                    t.def.ingestible.foodType.HasFlag(FoodTypeFlags.Meal) && !t.IsNotFresh() && t.GetStatValue(StatDefOf.Nutrition)>0 &&
                    team.All(p=>p.foodRestriction?.CurrentFoodPolicy?.filter.Allows(t)!=false) &&
                    (t.TryGetComp<CompRottable>()==null || t.TryGetComp<CompRottable>().TicksUntilRotAtCurrentTemp>travelTicks))
                    .OrderByDescending(t=>t.TryGetComp<CompRottable>()==null))
                {
                    int count=Math.Min(food.stackCount,(int)Math.Ceiling((foodNeeded-foodPacked)/food.GetStatValue(StatDefOf.Nutrition)));
                    if(count<=0)break;
                    quantities[food]=count;foodPacked+=count*food.GetStatValue(StatDefOf.Nutrition);
                }
                if(foodPacked<foodNeeded || colony.FoodNutrition-foodPacked<colony.DailyFoodNutrition*3)
                {state.CaravanStatus="Caravana bloqueada: comida durável para ida, volta e reserva da base.";continue;}
                float capacity=CollectionsMassCalculator.Capacity(team);
                float mass=CollectionsMassCalculator.MassUsage(team,IgnorePawnsInventoryMode.DontIgnore)+quantities.Sum(p=>p.Key.GetStatValue(StatDefOf.Mass)*p.Value);
                foreach(var medicine in reachable.Where(t=>t.def.IsMedicine).Take(2))
                {
                    int count=Math.Min(2,medicine.stackCount);
                    if(reachable.Where(t=>t.def.IsMedicine).Sum(t=>t.stackCount)-count-quantities.Where(q=>q.Key.def.IsMedicine).Sum(q=>q.Value)<colony.ColonistCount*2)continue;
                    quantities[medicine]=count;mass+=medicine.GetStatValue(StatDefOf.Mass)*count;
                }
                int cash=state.Budget;
                foreach(var silver in reachable.Where(t=>t.def==ThingDefOf.Silver))
                {
                    int count=Math.Min(cash,silver.stackCount);if(count<=0)break;
                    quantities[silver]=count;cash-=count;mass+=silver.GetStatValue(StatDefOf.Mass)*count;
                }
                int merchandise=0;
                foreach(var goods in reachable.Where(t=>CommercePlanner.Products.Contains(t.def.defName)))
                {
                    if(!destination.Settlement.TraderKind.WillTrade(goods.def))continue;
                    int count=Math.Min(goods.stackCount,Math.Max(0,(int)((capacity*.5f-mass)/Math.Max(.001f,goods.GetStatValue(StatDefOf.Mass)))));
                    if(count<=0)continue;
                    quantities[goods]=count;mass+=goods.GetStatValue(StatDefOf.Mass)*count;merchandise+=count;
                }
                if(mass>capacity*.6f || merchandise<20 && state.Budget<250)continue;
                // Reserve half the load for purchases; packing and departure consume actual native work.
                var transferables=new List<TransferableOneWay>();
                foreach(var pair in quantities)
                {
                    var transfer=new TransferableOneWay();transfer.things.Add(pair.Key);transfer.AdjustTo(pair.Value);transferables.Add(transfer);
                }
                var meeting=map.GetComponent<AutonomousRimMapComponent>().BaseProjects.FirstOrDefault(p=>p.Kind=="Estoque")?.Interior.CenterCell??team[0].Position;
                if(!RCellFinder.TryFindClosestEdgeCellTo(meeting,map,out var exit) || !team.All(p=>p.CanReach(exit,PathEndMode.OnCell,Danger.None)))continue;
                state.ExpeditionPawns=team;state.Destination=destination.Settlement;state.ExpeditionStarted=tick;
                state.Returning=false;state.ExpeditionManual=false;state.ExpeditionStage="Formando";
                state.ExpeditionEquipmentStage=DefenseProductionPlan.Stage(map);
                var checkpoint=LoadoutProgression.Analyze(map,state.ExpeditionEquipmentStage);
                state.ExpeditionMaterials=checkpoint.Materials.Select(p=>new CommerceNeed{Def=DefDatabase<ThingDef>.GetNamedSilentFail(p.Key),Count=p.Value}).Where(n=>n.Def!=null).ToList();
                state.ExpeditionGear=checkpoint.Craft.Select(p=>new CommerceNeed{Def=DefDatabase<ThingDef>.GetNamedSilentFail(p.Key),Count=p.Value}).Where(n=>n.Def!=null).ToList();
                // An invalid initial destination prevents the native exit routine from opening
                // a manual trade dialog. Assign the expedition route after actual departure.
                CaravanFormingUtility.StartFormingCaravan(team,new List<Pawn>(),Faction.OfPlayer,transferables,meeting,exit,map.Tile,PlanetTile.Invalid);
                state.FormationLord=team[0].GetLord();
                state.Record("Expedição para "+destination.Settlement.Label+"; "+merchandise+" mercadorias. Estoque do destino ainda desconhecido.");
                state.CaravanStatus="Caravana em formação: "+destination.Settlement.Label;
                return;
            }
        }
        private static void Follow(Map map,CommerceState state,bool enabled)
        {
            if(state.ExpeditionManual)
            {
                if(state.ExpeditionPawns.All(p=>p==null || p.Dead || p.Spawned && p.Map==map))Clear(state,"Equipe retornou após controle manual.");
                else state.CaravanStatus="Caravana sob controle manual; IA não altera a rota.";
                return;
            }
            if(state.Expedition==null || !state.Expedition.Spawned)
                state.Expedition=Find.WorldObjects.Caravans.FirstOrDefault(c=>c.IsPlayerControlled && c.PawnsListForReading.Any(state.ExpeditionPawns.Contains));
            var caravan=state.Expedition;
            if(caravan==null)
            {
                var lord=state.ExpeditionPawns.Where(p=>p?.Spawned==true).Select(p=>p.GetLord()).FirstOrDefault(l=>l?.LordJob is LordJob_FormAndSendCaravan);
                if(lord!=null)
                {
                    if(lord!=state.FormationLord){state.ExpeditionManual=true;state.CaravanStatus="Formação manual detectada; IA libera controle.";return;}
                    if(!enabled || !CommerceManager.Safe(map) || Find.TickManager.TicksGame-state.ExpeditionStarted>GenDate.TicksPerDay)
                    {CaravanFormingUtility.StopFormingCaravan(lord);Clear(state,"Formação cancelada por emergência, desligamento ou demora.");}
                    return;
                }
                if(state.ExpeditionPawns.All(p=>p==null || p.Dead || p.Spawned && p.Map==map))Clear(state,"Expedição encerrada; colonos e compras disponíveis conforme desembarque real.");
                return;
            }
            if(state.ExpeditionStage=="Formando")
            {
                state.ExpeditionStage="Viajando";
                if(enabled && state.Destination?.Spawned==true && CommerceManager.Safe(map))
                    caravan.pather.StartPath(state.Destination.Tile,null);
                else
                {
                    state.Returning=true;state.ExpeditionStage="Retornando";
                    caravan.pather.StartPath(map.Tile,new CaravanArrivalAction_Enter(map.Parent));
                }
                return;
            }
            if(caravan.pather.Paused || caravan.pather.Moving && caravan.pather.Destination!=(state.Returning?map.Tile:state.Destination?.Tile??PlanetTile.Invalid))
            {state.ExpeditionManual=true;state.CaravanStatus="Rota/pausa manual detectada; controle da caravana liberado.";return;}
            bool needReturn=!enabled || !CommerceManager.Safe(map) || caravan.PawnsListForReading.Any(p=>p.Downed || p.health.hediffSet.BleedRateTotal>0) ||
                state.Destination?.Faction?.HostileTo(Faction.OfPlayer)==true || state.Destination?.Spawned!=true;
            int backTicks=CaravanArrivalTimeEstimator.EstimatedTicksToArrive(caravan.Tile,map.Tile,caravan);
            if(caravan.DaysWorthOfFood.days<(float)backTicks/GenDate.TicksPerDay+1)needReturn=true;
            if(!state.Returning && state.Destination?.Spawned==true)
            {
                int forward=CaravanArrivalTimeEstimator.EstimatedTicksToArrive(caravan.Tile,state.Destination.Tile,caravan);
                int returnFromDestination=CaravanArrivalTimeEstimator.EstimatedTicksToArrive(state.Destination.Tile,map.Tile,caravan);
                if(caravan.DaysWorthOfFood.days<(float)(forward+returnFromDestination)/GenDate.TicksPerDay+1)needReturn=true;
            }
            if(!state.Returning && !needReturn && caravan.Tile==state.Destination.Tile && !caravan.pather.Moving)
            {
                // Trade stock is read only on arrival. No settlement goods are generated in advance.
                var negotiator=BestCaravanPawnUtility.FindBestNegotiator(caravan,state.Destination.Faction,state.Destination.TraderKind);
                if(negotiator!=null && state.Destination.CanTradeNow && !TradeSession.Active)
                {
                    CommercePlanner.Evaluate(map,map.GetComponent<AutonomousRimMapComponent>().CurrentState,map.GetComponent<AutonomousRimMapComponent>().BaseProjects,state);
                    CommerceManager.ExecuteTransaction(map,negotiator,state,state.Destination,caravan);
                }
                else {state.CaravanStatus="Destino indisponível; retornar com mercadorias.";}
                needReturn=true;
            }
            if(!state.Returning && needReturn)
            {
                state.Returning=true;state.ExpeditionStage="Retornando";
                if(!caravan.pather.StartPath(map.Tile,new CaravanArrivalAction_Enter(map.Parent)))
                    state.CaravanStatus="Sem rota de retorno; intervenção necessária.";
                else state.CaravanStatus="Expedição retornando à base.";
            }
            else if(!state.Returning)state.CaravanStatus="A caminho de "+state.Destination.Label+"; reserva "+caravan.DaysWorthOfFood.days.ToString("0.0")+" dias.";
        }
        private static void Clear(CommerceState state,string message)
        {
            state.Record(message);state.Expedition=null;state.ExpeditionPawns.Clear();state.Destination=null;state.FormationLord=null;
            state.Returning=false;state.ExpeditionStage=null;state.NextExpedition=Find.TickManager.TicksGame+GenDate.TicksPerDay*3;
            state.ExpeditionEquipmentStage=-1;state.ExpeditionMaterials.Clear();state.ExpeditionGear.Clear();
            state.CaravanStatus=message;
        }
    }
}
