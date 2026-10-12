using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld.Planet;

namespace AutonomousRim.Execution
{
    public static class CommerceManager
    {
        private static JobDef TradeJob=>DefDatabase<JobDef>.GetNamed("AutonomousRim_Trade");
        public static bool Safe(Map map)=>map.GetComponent<AutonomousRimMapComponent>().CurrentState?.Threat?.Immediate!=true &&
            !map.GetComponent<AutonomousRimMapComponent>().SecondarySuspended && !EmergencyManager.LocalFire(map);
        public static bool Available(Pawn p)=>WorkPriorityManager.CanWork(p) && !WorkReadiness.NeedsRecovery(p) &&
            p.CurJob?.bill?.recipe.products?.Any(product=>CommercePlanner.Products.Contains(product.thingDef.defName))!=true &&
            !(p.CurJob?.bill!=null && p.Map?.GetComponent<AutonomousRimMapComponent>().Coordination.Equipment.Any(d=>d.Bill==p.CurJob.bill && d.Ready)==true) &&
            p.CurJob?.playerForced!=true && p.CurJob?.workGiverDef?.workType!=WorkTypeDefOf.Warden && p.jobs.jobQueue.Count==0 && p.GetLord()==null &&
            p.health.capacities.CapableOf(PawnCapacityDefOf.Talking) && p.skills?.GetSkill(SkillDefOf.Social).TotallyDisabled==false &&
            !HealthAIUtility.ShouldSeekMedicalRest(p) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Warden);
        public static void Apply(Map map,ColonyState colony,List<RoomProject> projects,CommerceState commerce,bool build)
        {
            CommercePlanner.Evaluate(map,colony,projects,commerce);
            if(!Safe(map)) {Suspend(commerce);commerce.Status="Comércio suspenso: ameaça="+colony.Threat?.Immediate+", recuperação="+map.GetComponent<AutonomousRimMapComponent>().SecondarySuspended+", incêndio="+EmergencyManager.LocalFire(map)+"; reservar colonos para defesa e cuidados.";return;}
            ProductionPolicies(map,commerce);
            CommercePlanner.Production(map,colony,projects,commerce,build);
            string demand=string.Join(", ",commerce.Needs.Take(5).Select(n=>n.Def.LabelCap+" ×"+n.Count));
            commerce.Status=$"Prata {commerce.Silver}; reserva {commerce.Reserve}; verba {commerce.Budget}. Comprar: {demand}. {commerce.Product}: alvo {commerce.Target}.";
            if(commerce.JobId!=null && commerce.Negotiator!=null &&
                (commerce.Negotiator.CurJob?.GetUniqueLoadID()==commerce.JobId ||
                 commerce.Negotiator.jobs.jobQueue.Any(q=>q.job.GetUniqueLoadID()==commerce.JobId)))return;
            commerce.Negotiator=null; commerce.Visitor=null; commerce.Ship=null; commerce.JobId=null;
            if(TradeSession.Active || Find.WindowStack.WindowOfType<Dialog_Trade>()!=null){commerce.Status+=" Aguardar negociação manual.";return;}
            int tick=Find.TickManager.TicksGame;
            var negotiators=map.mapPawns.FreeColonistsSpawned.Where(Available).Where(p=>!CommerceCaravanManager.Reserved(commerce,p)).Where(p=>
                !(colony.DownedColonists>0 && p.workSettings.GetPriority(WorkTypeDefOf.Doctor)==1) &&
                !(map.mapPawns.FreeColonistsSpawned.Any(q=>q.health.hediffSet.BleedRateTotal>0) &&
                    p.skills.GetSkill(SkillDefOf.Medicine).Level==map.mapPawns.FreeColonistsSpawned.Max(q=>q.skills?.GetSkill(SkillDefOf.Medicine).Level??0)))
                .OrderByDescending(p=>p.GetStatValue(StatDefOf.TradePriceImprovement)).ThenBy(p=>p.thingIDNumber).ToList();
            if(negotiators.Count==0){commerce.Status+=" Sem negociador disponível sem prejudicar cuidados.";return;}
            var visitors=map.mapPawns.AllPawnsSpawned.Where(p=>p.Faction!=Faction.OfPlayer && !p.HostileTo(Faction.OfPlayer) &&
                !p.Downed && p.CanTradeNow && ((ITrader)p).TradeCurrency==TradeCurrency.Silver).Cast<ITrader>().ToList();
            var consoles=map.listerBuildings.AllBuildingsColonistOfClass<Building_CommsConsole>().Where(c=>c.CanUseCommsNow && !c.IsForbidden(Faction.OfPlayer)).ToList();
            var ships=consoles.Count>0?map.passingShipManager.passingShips.OfType<TradeShip>().Where(s=>s.CanTradeNow && s.TradeCurrency==TradeCurrency.Silver).Cast<ITrader>():Enumerable.Empty<ITrader>();
            var traders=visitors.Concat(ships).Where(t=>t.CanTradeNow).OrderByDescending(t=>
                commerce.Needs.Sum(n=>Math.Min(n.Count,t.Goods.Where(g=>g.def==n.Def).Sum(g=>g.stackCount))*n.Priority)).ToList();
            commerce.BuyerStatus=traders.Count==0?"Sem visitante ou nave com console energizado.":string.Join("; ",traders.Select(t=>t.TraderName+" • prata "+t.Goods.Where(g=>g.def==ThingDefOf.Silver).Sum(g=>g.stackCount)+
                (t is TradeShip ship?" • partida em "+(ship.ticksUntilDeparture/2500f).ToString("0.0")+" h":"")));
            // Inspect changes on every AI review; unchanged opportunities do not dispatch another walk.
            string signature=commerce.Silver+":"+CommercePlanner.Stock(map,DefDatabase<ThingDef>.GetNamed("SmokeleafJoint"))+":"+
                string.Join(",",commerce.Needs.Select(n=>n.Def.defName+"="+n.Count))+":"+
                string.Join(";",traders.Select(t=>t.TraderName+"="+string.Join(",",t.Goods.Select(g=>g.ThingID+"/"+g.stackCount))+"/oferta="+
                    string.Join(",",t.ColonyThingsWillingToBuy(negotiators[0]).Where(g=>g.def==ThingDefOf.Silver || CommercePlanner.Products.Contains(g.def.defName))
                        .Select(g=>g.ThingID+"/"+g.stackCount+"/"+g.Position+"/"+g.IsForbidden(Faction.OfPlayer)+"/"+map.reservationManager.IsReservedByAnyoneOf(g,Faction.OfPlayer)))))+":"+
                string.Join(",",negotiators.Select(p=>p.ThingID));
            if(tick<commerce.NextAttempt && CommercePlanner.Stock(map,DefDatabase<ThingDef>.GetNamed("SmokeleafJoint"))<20 && !commerce.Needs.Any(n=>n.Priority>=95))return;
            if(signature==commerce.OpportunitySignature && tick<commerce.NextAttempt)return;
            commerce.NextAttempt=tick+Math.Max(600,commerce.RefreshTicks);
            if(signature==commerce.OpportunitySignature){commerce.Status+=" Aguardar mudança de estoque, demanda ou comprador.";return;}
            commerce.OpportunitySignature=signature;
            commerce.Blocker=null;
            foreach(var trader in traders)
            {
                bool buyerCash=trader.Goods.Any(g=>g.def==ThingDefOf.Silver && g.stackCount>0);
                bool usefulGoods=trader.Goods.Any(g=>commerce.Needs.Any(n=>n.Def==g.def));
                if(!buyerCash && !usefulGoods){commerce.Blocker="Comprador sem prata e sem recursos solicitados.";continue;}
                if(!trader.Goods.Any(g=>commerce.Needs.Any(n=>n.Def==g.def)) &&
                    !CommercePlanner.Products.Any(name=>trader.TraderKind.WillTrade(DefDatabase<ThingDef>.GetNamedSilentFail(name)) &&
                        CommercePlanner.Stock(map,DefDatabase<ThingDef>.GetNamed(name))>0))continue;
                foreach(var p in negotiators)
                {
                    if(!p.CanTradeWith(trader.Faction,trader.TraderKind).Accepted || trader is Pawn dismissed && dismissed.mindState.traderDismissed)continue;
                    Thing target=trader as Pawn ?? (Thing)consoles.FirstOrDefault(c=>p.CanReach(c,PathEndMode.InteractionCell,Danger.None) && p.CanReserve(c));
                    if(target==null || target.IsForbidden(Faction.OfPlayer) || !p.CanReach(target,target is Pawn?PathEndMode.Touch:PathEndMode.InteractionCell,Danger.None) || !p.CanReserve(target))continue;
                    var job=JobMaker.MakeJob(TradeJob,target);
                    commerce.Negotiator=p; commerce.Visitor=trader as Pawn; commerce.Ship=trader as TradeShip;commerce.JobId=job.GetUniqueLoadID();
                    p.jobs.TryTakeOrderedJob(job,JobTag.Misc);
                    commerce.Status+=" Negociador "+p.LabelShort+" → "+trader.TraderName;
                    return;
                }
            }
            commerce.Blocker=commerce.Blocker??"Sem oferta aceita e acessível; verificar prata, cobertura orbital, acesso e estoque.";
            commerce.Status+=" "+commerce.Blocker;
        }

        // Build and commit the native trade synchronously, only after walking to the trader/console.
        public static void Execute(Map map,Pawn negotiator,CommerceState commerce)
        {
            if(!map.GetComponent<AutonomousRimMapComponent>().CommerceAutomation || !Safe(map) ||
                commerce.Negotiator!=negotiator || negotiator.CurJob?.GetUniqueLoadID()!=commerce.JobId ||
                TradeSession.Active || Find.WindowStack.WindowOfType<Dialog_Trade>()!=null)return;
            ITrader trader=(ITrader)commerce.Visitor??commerce.Ship;
            if(trader?.CanTradeNow!=true || trader.TradeCurrency!=TradeCurrency.Silver ||
                trader.Faction?.HostileTo(Faction.OfPlayer)==true || WorkReadiness.NeedsRecovery(negotiator) ||
                !negotiator.CanTradeWith(trader.Faction,trader.TraderKind).Accepted)return;
            if(trader is Pawn visitor && (!visitor.Spawned || visitor.Map!=map || !negotiator.Position.AdjacentTo8WayOrInside(visitor.Position)))return;
            if(trader is TradeShip && (!(negotiator.CurJob.targetA.Thing is Building_CommsConsole console) || !console.CanUseCommsNow || negotiator.Position!=console.InteractionCell))return;
            CommercePlanner.Evaluate(map,map.GetComponent<AutonomousRimMapComponent>().CurrentState,map.GetComponent<AutonomousRimMapComponent>().BaseProjects,commerce);
            ExecuteTransaction(map,negotiator,commerce,trader);
        }
        public static void ExecuteTransaction(Map map,Pawn negotiator,CommerceState commerce,ITrader trader,Caravan caravan=null)
        {
            if(TradeSession.Active || Find.WindowStack.WindowOfType<Dialog_Trade>()!=null || trader?.CanTradeNow!=true || trader.TradeCurrency!=TradeCurrency.Silver ||
                !negotiator.CanTradeWith(trader.Faction,trader.TraderKind).Accepted)return;
            try
            {
                TradeSession.SetupWith(trader,negotiator,false);
                var deal=TradeSession.deal;
                var currency=deal.CurrencyTradeable;
                if(currency==null)return;
                bool allowed(Thing t)=>caravan!=null ? caravan.AllThings.Contains(t) : t.Spawned && t.Map==map && !t.IsForbidden(Faction.OfPlayer) &&
                    !t.Position.Fogged(map) && !map.reservationManager.IsReservedByAnyoneOf(t,Faction.OfPlayer);
                // Filter only this private native deal, never the map stacks or a manual session.
                // A prohibited/reserved pile must not block sales from other accessible piles.
                currency.thingsColony.RemoveAll(t=>!allowed(t));
                foreach(var group in deal.AllTradeables.Where(t=>CommercePlanner.Products.Contains(t.ThingDef?.defName)))
                    group.thingsColony.RemoveAll(t=>!allowed(t));
                int silver=currency.CountHeldBy(Transactor.Colony);
                var sales=deal.AllTradeables.Where(t=>CommercePlanner.Products.Contains(t.ThingDef?.defName) && t.TraderWillTrade && t.Interactive &&
                    t.thingsColony.Count>0 && t.thingsColony.All(allowed)).ToList();
                float income=sales.Sum(t=>t.CountHeldBy(Transactor.Colony)*t.GetPriceFor(TradeAction.PlayerSells));
                bool critical=map.GetComponent<AutonomousRimMapComponent>().CurrentState.EstimatedFoodDays<1 ||
                    map.mapPawns.FreeColonistsSpawned.Any(p=>p.Downed || p.health.hediffSet.BleedRateTotal>0);
                bool urgentOffer=critical && commerce.Needs.Any(n=>n.Priority>=95 && deal.AllTradeables.Any(t=>t.ThingDef==n.Def && t.FirstThingTrader!=null && t.TraderWillTrade && t.Interactive));
                int reserve=urgentOffer || caravan!=null?0:commerce.Reserve;
                int protectedOutside=caravan==null?Math.Max(0,CommercePlanner.Stock(map,ThingDefOf.Silver)-silver):0;
                int ordinaryReserve=Math.Max(0,commerce.Reserve-protectedOutside);
                int effectiveReserve=Math.Max(0,reserve-protectedOutside);
                float budget=Math.Max(0,silver-effectiveReserve)+income;
                float spent=0;
                var purchases=new List<Tuple<Tradeable,int>>();
                var quantitiesNeeded=commerce.Needs.ToDictionary(n=>n.Def,n=>n.Count);
                void deductInputs(RecipeDef recipe,int count)
                {
                    if(recipe==null || count<=0)return;
                    foreach(var ingredient in recipe.ingredients)
                    {
                        var def=LoadoutProgression.Material(ingredient);
                        if(def==null || !quantitiesNeeded.ContainsKey(def))continue;
                        int reduced=Math.Min(quantitiesNeeded[def],ingredient.CountRequiredOfFor(def,recipe)*count);
                        quantitiesNeeded[def]-=reduced;
                        if(reduced>0 && (def==ThingDefOf.ComponentIndustrial || def.defName=="ComponentSpacer"))
                            deductInputs(DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r=>r.products?.Any(p=>p.thingDef==def)==true),reduced);
                    }
                }
                void buyNeed(CommerceNeed need)
                {
                    int left=quantitiesNeeded[need.Def];
                    foreach(var item in deal.AllTradeables.Where(t=>t.ThingDef==need.Def && t.Interactive && t.TraderWillTrade && t.FirstThingTrader!=null &&
                        !t.FirstThingTrader.IsNotFresh()).OrderBy(t=>t.GetPriceFor(TradeAction.PlayerBuys)))
                    {
                        float price=item.GetPriceFor(TradeAction.PlayerBuys);
                        int count=Math.Min(left,Math.Min(item.CountHeldBy(Transactor.Trader),(int)Math.Floor((budget-spent)/Math.Max(.5f,price))));
                        if(count<=0)continue;
                        purchases.Add(Tuple.Create(item,count));spent+=price*count;left-=count;
                        if(need.Def==ThingDefOf.ComponentIndustrial || need.Def.defName=="ComponentSpacer")
                        {
                            var componentRecipe=DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r=>r.products?.Any(p=>p.thingDef==need.Def)==true);
                            deductInputs(componentRecipe,count);
                        }
                        if(left<=0)break;
                    }
                }
                var ordered=commerce.Needs.OrderByDescending(n=>n.Priority).ThenBy(n=>ResourceOrder(n.Def)).ToList();
                // Health/food and essential infrastructure receive budget before military upgrades.
                foreach(var need in ordered.Where(n=>n.Priority>=95))buyNeed(need);
                // Emergency funds are only spent on survival goods, never on later upgrades.
                if(caravan==null)budget=Math.Max(0,silver-ordinaryReserve)+income;
                if(!critical)foreach(var need in ordered.Where(n=>n.Priority>=85 && n.Priority<95))buyNeed(need);
                // Compare ready equipment with estimated ingredient expense. Defer military ingredients
                // to the next scan when buying a finished piece, avoiding double financing its recipe.
                var checkpoint=LoadoutProgression.Analyze(map,DefenseProductionPlan.Stage(map));
                var equipmentCraft=caravan==null?checkpoint.Craft:commerce.ExpeditionGear.ToDictionary(n=>n.Def.defName,n=>n.Count);
                foreach(var pair in equipmentCraft)
                {
                    if(critical)break;
                    var def=DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key);
                    var recipe=DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r=>r.products?.Any(p=>p.thingDef==def)==true);
                    float craftValue=recipe?.ingredients.Sum(i=>LoadoutProgression.Material(i) is ThingDef material?
                        material.GetStatValueAbstract(StatDefOf.MarketValue)*i.CountRequiredOfFor(material,recipe):0)??0;
                    bool canCraft=recipe?.AvailableNow==true && map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Any(b=>b.def.AllRecipes.Contains(recipe) && b.CurrentlyUsableForBills() && !b.IsForbidden(Faction.OfPlayer)) &&
                        map.mapPawns.FreeColonistsSpawned.Any(p=>WorkPriorityManager.CanWork(p) && !WorkReadiness.NeedsRecovery(p) &&
                            (recipe.workSkill==null || p.skills?.GetSkill(recipe.workSkill).TotallyDisabled==false) &&
                            (recipe.requiredGiverWorkType==null || !p.WorkTypeIsDisabled(recipe.requiredGiverWorkType)) &&
                            (recipe.skillRequirements==null || recipe.skillRequirements.All(s=>p.skills.GetSkill(s.skill).Level>=s.minLevel)));
                    int remaining=pair.Value-CommercePlanner.InTransit(commerce,def)-CommercePlanner.InProduction(map,def);
                    foreach(var item in deal.AllTradeables.Where(t=>t.ThingDef==def && t.Interactive && t.TraderWillTrade && t.FirstThingTrader!=null &&
                        DefenseProductionPlan.Usable(t.FirstThingTrader) && !CompBiocodable.IsBiocoded(t.FirstThingTrader) &&
                        DefenseProductionPlan.Fighters(map).Concat(commerce.ExpeditionPawns.Where(p=>p!=null && !p.Dead))
                            .Any(p=>LoadoutProgression.Meets(p,t.FirstThingTrader,pair.Key))).OrderBy(t=>t.GetPriceFor(TradeAction.PlayerBuys)))
                    {
                        if(remaining<=0)break;
                        float price=item.GetPriceFor(TradeAction.PlayerBuys);
                        if(canCraft && price>craftValue*2f)continue;
                        int count=Math.Min(remaining,Math.Min(item.CountHeldBy(Transactor.Trader),(int)Math.Floor((budget-spent)/Math.Max(.5f,price))));
                        if(count<=0)continue;
                        purchases.Add(Tuple.Create(item,count));spent+=price*count;remaining-=count;
                        deductInputs(recipe,count);
                    }
                }
                if(!critical)foreach(var need in ordered.Where(n=>n.Priority<85)
                    .OrderBy(n=>n.Def.defName=="ComponentSpacer"?0:n.Def.defName=="ComponentIndustrial"?1:2)
                    .ThenByDescending(n=>n.Priority).ThenBy(n=>ResourceOrder(n.Def)))buyNeed(need);
                foreach(var p in purchases)p.Item1.ForceToSource(p.Item2);
                float sellLimit=spent+currency.CountHeldBy(Transactor.Trader);
                float revenue=0;
                foreach(var sale in sales.OrderByDescending(t=>t.GetPriceFor(TradeAction.PlayerSells)))
                {
                    float price=sale.GetPriceFor(TradeAction.PlayerSells);
                    int count=Math.Min(sale.CountHeldBy(Transactor.Colony),Math.Max(0,(int)Math.Floor((sellLimit-revenue)/Math.Max(.01f,price))));
                    if(count<=0)continue;
                    sale.ForceToSource(-count);revenue+=count*price;
                }
                deal.UpdateCurrencyCount();
                // Native rounding and unsold goods can make the optimistic basket unaffordable.
                // Trim lowest-priority purchases instead of aborting every useful transaction.
                int protectedFloor=(int)Math.Min(effectiveReserve,silver+Math.Min(income,currency.CountHeldBy(Transactor.Trader)));
                while(currency.CountPostDealFor(Transactor.Colony)<protectedFloor)
                {
                    var purchase=purchases.AsEnumerable().Reverse().FirstOrDefault(p=>p.Item1.CountToTransferToSource>0);
                    if(purchase==null){commerce.Blocker="Verba insuficiente para preservar a reserva de prata.";return;}
                    int reduce=Math.Max(1,(int)Math.Ceiling((protectedFloor-currency.CountPostDealFor(Transactor.Colony))/Math.Max(.01f,purchase.Item1.GetPriceFor(TradeAction.PlayerBuys))));
                    purchase.Item1.ForceToSource(Math.Max(0,purchase.Item1.CountToTransferToSource-reduce));
                    deal.UpdateCurrencyCount();
                }
                if(!deal.DoesTraderHaveEnoughSilver()){commerce.Blocker="Prata do comprador insuficiente após arredondamento.";return;}
                if(caravan!=null)
                {
                    var items=caravan.AllThings.ToList();
                    int iterations=0;
                    while(CollectionsMassCalculator.MassUsageLeftAfterTradeableTransfer(items,deal.AllTradeables,IgnorePawnsInventoryMode.DontIgnore)>
                        CollectionsMassCalculator.CapacityLeftAfterTradeableTransfer(items,deal.AllTradeables)*.9f)
                    {
                        var largest=deal.AllTradeables.Where(t=>!t.IsCurrency && t.CountToTransferToSource>0)
                            .OrderByDescending(t=>t.AnyThing.GetStatValue(StatDefOf.Mass)*t.CountToTransferToSource).FirstOrDefault();
                        if(largest==null || ++iterations>2000)return;
                        largest.ForceToSource(largest.CountToTransferToSource-1);
                        deal.UpdateCurrencyCount();
                    }
                    // Trimming purchases also reduces how much stock the trader can afford.
                    while(!deal.DoesTraderHaveEnoughSilver())
                    {
                        var sale=sales.Where(t=>t.CountToTransferToDestination>0).OrderByDescending(t=>t.GetPriceFor(TradeAction.PlayerSells)).FirstOrDefault();
                        if(sale==null)return;
                        int reduce=Math.Max(1,(int)Math.Ceiling(-currency.CountPostDealFor(Transactor.Trader)/Math.Max(.01f,sale.GetPriceFor(TradeAction.PlayerSells))));
                        sale.ForceToSource(-Math.Max(0,sale.CountToTransferToDestination-reduce));deal.UpdateCurrencyCount();
                    }
                }
                var summary=string.Join(", ",deal.AllTradeables.Where(t=>!t.IsCurrency && t.CountToTransfer!=0)
                    .Select(t=>t.ActionToDo+" "+t.ThingDef.LabelCap+" ×"+Math.Abs(t.CountToTransfer)));
                if(string.IsNullOrEmpty(summary)){commerce.Blocker="Nenhuma transferência viável: estoque aceito, verba ou recursos ausentes.";return;}
                var pending=deal.AllTradeables.Where(t=>!t.IsCurrency && t.ActionToDo==TradeAction.PlayerBuys).GroupBy(t=>t.ThingDef)
                    .Select(g=>new CommerceDelivery{Def=g.Key,Count=g.Sum(t=>t.CountToTransferToSource),Baseline=CommercePlanner.Stock(map,g.Key)+CommercePlanner.InTransit(commerce,g.Key),Tick=Find.TickManager.TicksGame}).ToList();
                CommerceDeliveryObserver.Pending=trader is TradeShip?pending:null;
                int jointsSold=sales.Sum(s=>s.CountToTransferToDestination);
                string buyer=(trader as ILoadReferenceable)?.GetUniqueLoadID()??trader.TraderName;
                if(deal.TryExecute(out bool traded) && traded)
                {
                    commerce.Transactions++;commerce.LastBuyer=buyer;
                    commerce.JointSales[buyer]=(commerce.JointSales.TryGetValue(buyer,out int previous)?previous:0)+jointsSold;
                    commerce.LastTrade=Find.TickManager.TicksGame;
                    if(trader is TradeShip || caravan!=null)commerce.Deliveries.AddRange(pending);
                    commerce.Record(trader.TraderName+": "+summary);
                    commerce.Product="SmokeleafJoint";
                    commerce.Blocker=null;
                }
                else commerce.Blocker="O jogo recusou a transação nativa; reavaliar acesso, preço e estoque.";
            }
            catch(Exception e)
            {
                commerce.Status="Negociação interrompida: "+e.GetType().Name+". Conferir log antes de repetir.";
                commerce.NextAttempt=Find.TickManager.TicksGame+GenDate.TicksPerDay;
                Log.Error("[AutonomousRim.Commerce] "+e);
            }
            finally {CommerceDeliveryObserver.Pending=null;TradeSession.Close();TradeSession.deal=null;TradeSession.playerNegotiator=null;commerce.JobId=null;}
        }
        private static int ResourceOrder(ThingDef def)
        {
            switch(def.defName)
            {
                // Buy available components before their precursors so deductInputs
                // can remove the fabrication costs before any raw-material purchase.
                case "ComponentSpacer":return 0;
                case "Plasteel":return 1;
                case "Uranium":return 2;
                case "Gold":return 3;
                case "ComponentIndustrial":return 4;
                case "Steel":return 5;
                default:return 6;
            }
        }
        private static string Signature(DrugPolicy policy)=>string.Join(";",Enumerable.Range(0,policy.Count).Select(i=>policy[i])
            .Select(e=>$"{e.drug.defName}|{e.allowedForAddiction}|{e.allowedForJoy}|{e.allowScheduled}|{e.daysFrequency:R}|{e.onlyIfMoodBelow:R}|{e.onlyIfJoyBelow:R}|{e.takeToInventory}"));
        private static void ProductionPolicies(Map map,CommerceState commerce)
        {
            foreach(var p in map.mapPawns.FreeColonistsSpawned.Where(p=>p.drugs!=null))
            {
                var entry=commerce.Policies.FirstOrDefault(e=>e.Pawn==p);
                if(entry!=null)
                {
                    if(entry.Manual)continue;
                    if(p.drugs.CurrentPolicy!=entry.Applied || Signature(entry.Applied)!=entry.Signature)entry.Manual=true;
                    continue;
                }
                // Custom policies remain manual. Copy the default rather than editing a shared policy.
                if(p.drugs.CurrentPolicy!=Current.Game.drugPolicyDatabase.DefaultDrugPolicy())continue;
                var policy=Current.Game.drugPolicyDatabase.MakeNewDrugPolicy();
                policy.CopyFrom(p.drugs.CurrentPolicy);policy.label="AutonomousRim comércio: "+p.LabelShort;
                for(int i=0;i<policy.Count;i++)if(CommercePlanner.Products.Contains(policy[i].drug.defName))
                {policy[i].allowedForJoy=false;policy[i].allowScheduled=false;policy[i].takeToInventory=0;}
                // Addiction treatment remains allowed; economy must not force withdrawal on a patient.
                entry=new CommerceDrugPolicy{Pawn=p,Previous=p.drugs.CurrentPolicy,Applied=policy,Signature=Signature(policy)};
                commerce.Policies.Add(entry);p.drugs.CurrentPolicy=policy;
            }
        }
        public static void Suspend(CommerceState commerce)
        {
            commerce.OpportunitySignature=null;commerce.NextAttempt=0;
            if(commerce.JobId!=null && commerce.Negotiator?.CurJob?.GetUniqueLoadID()==commerce.JobId)commerce.Negotiator.jobs.EndCurrentJob(JobCondition.InterruptForced);
            commerce.JobId=null;
            foreach(var bill in commerce.Bills.Where(b=>b.Matches)){bill.Bill.suspended=true;bill.Signature=ManagedFoodBill.Describe(bill.Bill);}
        }
        public static void Stop(CommerceState commerce)
        {
            Suspend(commerce);FoodManager.RemoveOwnedBills(commerce.Bills);
            foreach(var entry in commerce.Policies)
                if(!entry.Manual && entry.Pawn?.drugs?.CurrentPolicy==entry.Applied && entry.Applied!=null && Signature(entry.Applied)==entry.Signature)
                {
                    entry.Pawn.drugs.CurrentPolicy=entry.Previous;
                    Current.Game.drugPolicyDatabase.TryDelete(entry.Applied);
                }
            commerce.Policies.Clear();commerce.Status="Comércio desligado; ordens e políticas próprias liberadas.";
        }
    }
}
