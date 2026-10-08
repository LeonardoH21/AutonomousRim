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
            p.CurJob?.playerForced!=true && p.CurJob?.workGiverDef?.workType!=WorkTypeDefOf.Warden && p.jobs.jobQueue.Count==0 && p.GetLord()==null &&
            p.health.capacities.CapableOf(PawnCapacityDefOf.Talking) && p.skills?.GetSkill(SkillDefOf.Social).TotallyDisabled==false &&
            !HealthAIUtility.ShouldSeekMedicalRest(p) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Warden);
        public static void Apply(Map map,ColonyState colony,List<RoomProject> projects,CommerceState commerce,bool build)
        {
            CommercePlanner.Evaluate(map,colony,projects,commerce);
            if(!Safe(map)) {Suspend(commerce);commerce.Status="Comércio suspenso: emergência; reservar colonos para defesa e cuidados.";return;}
            ProductionPolicies(map,commerce);
            CommercePlanner.Production(map,colony,projects,commerce,build);
            string demand=string.Join(", ",commerce.Needs.Take(5).Select(n=>n.Def.LabelCap+" ×"+n.Count));
            commerce.Status=$"Prata {commerce.Silver}; reserva {commerce.Reserve}; verba {commerce.Budget}. Comprar: {demand}. {commerce.Product}: alvo {commerce.Target}.";
            if(commerce.JobId!=null && commerce.Negotiator?.CurJob?.GetUniqueLoadID()==commerce.JobId)return;
            commerce.Negotiator=null; commerce.Visitor=null; commerce.Ship=null; commerce.JobId=null;
            if(commerce.Needs.Count==0){commerce.Status+=" Checkpoint abastecido; produção comercial pausada.";return;}
            if(TradeSession.Active || Find.WindowStack.WindowOfType<Dialog_Trade>()!=null){commerce.Status+=" Aguardar negociação manual.";return;}
            int tick=Find.TickManager.TicksGame;
            if(tick<commerce.NextAttempt)return;
            commerce.NextAttempt=tick+2500;
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
            foreach(var trader in traders)
            {
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
            commerce.Status+=" Sem oferta acessível; manter lote comercial limitado.";
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
                // Never sell a mixed transfer group containing a player-prohibited/reserved stack.
                if(currency.thingsColony.Any(t=>!allowed(t)))return;
                int silver=currency.CountHeldBy(Transactor.Colony);
                var sales=deal.AllTradeables.Where(t=>CommercePlanner.Products.Contains(t.ThingDef?.defName) && t.TraderWillTrade && t.Interactive &&
                    t.thingsColony.Count>0 && t.thingsColony.All(allowed)).ToList();
                float income=sales.Sum(t=>t.CountHeldBy(Transactor.Colony)*t.GetPriceFor(TradeAction.PlayerSells));
                bool urgent=commerce.Needs.Any(n=>n.Priority>=95);
                bool critical=map.GetComponent<AutonomousRimMapComponent>().CurrentState.EstimatedFoodDays<1 ||
                    map.mapPawns.FreeColonistsSpawned.Any(p=>p.Downed || p.health.hediffSet.BleedRateTotal>0);
                bool urgentOffer=critical && commerce.Needs.Any(n=>n.Priority>=95 && deal.AllTradeables.Any(t=>t.ThingDef==n.Def && t.FirstThingTrader!=null && t.TraderWillTrade && t.Interactive));
                int reserve=urgentOffer || caravan!=null?0:commerce.Reserve;
                float budget=Math.Max(0,silver-reserve)+income;
                float spent=0;
                var purchases=new List<Tuple<Tradeable,int>>();
                var quantitiesNeeded=commerce.Needs.ToDictionary(n=>n.Def,n=>n.Count);
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
                            if(componentRecipe!=null)foreach(var ingredient in componentRecipe.ingredients)
                                foreach(var def in quantitiesNeeded.Keys.ToList().Where(d=>ingredient.filter.Allows(d)))
                                    quantitiesNeeded[def]=Math.Max(0,quantitiesNeeded[def]-ingredient.CountRequiredOfFor(def,componentRecipe)*count);
                        }
                        if(left<=0)break;
                    }
                }
                var ordered=commerce.Needs.OrderByDescending(n=>n.Priority).ThenBy(n=>n.Def.defName=="ComponentSpacer"?0:n.Def.defName=="ComponentIndustrial"?1:2).ToList();
                // Health/food and essential infrastructure receive budget before military upgrades.
                foreach(var need in ordered.Where(n=>n.Priority>=95))buyNeed(need);
                // Emergency funds are only spent on survival goods, never on later upgrades.
                if(caravan==null)budget=Math.Max(0,silver-commerce.Reserve)+income;
                foreach(var need in ordered.Where(n=>n.Priority>=85 && n.Priority<95))buyNeed(need);
                // Compare ready equipment with estimated ingredient expense. Defer military ingredients
                // to the next scan when buying a finished piece, avoiding double financing its recipe.
                bool boughtEquipment=false;
                var checkpoint=LoadoutProgression.Analyze(map,DefenseProductionPlan.Stage(map));
                var equipmentCraft=caravan==null?checkpoint.Craft:commerce.ExpeditionGear.ToDictionary(n=>n.Def.defName,n=>n.Count);
                foreach(var pair in equipmentCraft)
                {
                    if(urgent)break;
                    var def=DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key);
                    var recipe=DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r=>r.products?.Any(p=>p.thingDef==def)==true);
                    float craftValue=recipe?.ingredients.Sum(i=>i.filter.AllowedThingDefs.Where(d=>d!=null && d.defName!="Leather_Human")
                        .Select(d=>d.GetStatValueAbstract(StatDefOf.MarketValue)*i.CountRequiredOfFor(d,recipe)).DefaultIfEmpty(0).Min())??0;
                    bool canCraft=recipe?.AvailableNow==true && map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Any(b=>b.def.AllRecipes.Contains(recipe));
                    int remaining=pair.Value-CommercePlanner.InTransit(commerce,def);
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
                        purchases.Add(Tuple.Create(item,count));spent+=price*count;remaining-=count;boughtEquipment=true;
                    }
                }
                if(!boughtEquipment)foreach(var need in ordered.Where(n=>n.Priority<85))buyNeed(need);
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
                if(currency.CountPostDealFor(Transactor.Colony)<Math.Min(silver,reserve) || !deal.DoesTraderHaveEnoughSilver())return;
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
                if(string.IsNullOrEmpty(summary))return;
                var pending=deal.AllTradeables.Where(t=>!t.IsCurrency && t.ActionToDo==TradeAction.PlayerBuys).GroupBy(t=>t.ThingDef)
                    .Select(g=>new CommerceDelivery{Def=g.Key,Count=g.Sum(t=>t.CountToTransferToSource),Baseline=CommercePlanner.Stock(map,g.Key),Tick=Find.TickManager.TicksGame}).ToList();
                if(deal.TryExecute(out bool traded) && traded)
                {
                    commerce.LastTrade=Find.TickManager.TicksGame;
                    if(trader is TradeShip || caravan!=null)commerce.Deliveries.AddRange(pending);
                    commerce.Record(trader.TraderName+": "+summary);
                    ChooseProduct(map,trader,commerce,deal);
                }
            }
            catch(Exception e)
            {
                commerce.Status="Negociação interrompida: "+e.GetType().Name+". Conferir log antes de repetir.";
                commerce.NextAttempt=Find.TickManager.TicksGame+GenDate.TicksPerDay;
                Log.Error("[AutonomousRim.Commerce] "+e);
            }
            finally {TradeSession.Close();TradeSession.deal=null;TradeSession.playerNegotiator=null;commerce.JobId=null;}
        }
        private static void ChooseProduct(Map map,ITrader trader,CommerceState commerce,TradeDeal deal)
        {
            var candidates=CommercePlanner.Products.Select(name=>new{ Name=name,Recipe=CommercePlanner.ProductRecipe(name) })
                .Where(c=>c.Recipe?.AvailableNow==true && trader.TraderKind.WillTrade(DefDatabase<ThingDef>.GetNamed(c.Name)) &&
                    map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Any(t=>t.def.AllRecipes.Contains(c.Recipe)) &&
                    map.mapPawns.FreeColonistsSpawned.Any(p=>WorkPriorityManager.CanWork(p) && !WorkReadiness.NeedsRecovery(p) &&
                        (c.Recipe.skillRequirements==null || c.Recipe.skillRequirements.All(s=>p.skills.GetSkill(s.skill).Level>=s.minLevel))))
                .Select(c=>new {c.Name, Score=EconomicScore(c.Name,c.Recipe,deal)})
                .Where(c=>c.Score>0).OrderByDescending(c=>c.Score).FirstOrDefault();
            if(candidates!=null && CommercePlanner.Stock(map,DefDatabase<ThingDef>.GetNamed(commerce.Product))<10)commerce.Product=candidates.Name;
        }
        private static float EconomicScore(string name,RecipeDef recipe,TradeDeal deal)
        {
            var def=DefDatabase<ThingDef>.GetNamed(name);
            var known=deal.AllTradeables.FirstOrDefault(t=>t.ThingDef==def);
            // Unspawned objects are price references, never colony goods or free production.
            var reference=known??new Tradeable(ThingMaker.MakeThing(def),null);
            float price=reference.GetPriceFor(TradeAction.PlayerSells);
            var plant=DefDatabase<ThingDef>.GetNamedSilentFail(name=="SmokeleafJoint"?"Plant_Smokeleaf":"Plant_Psychoid");
            if(plant?.plant==null)return 0;
            var raw=plant.plant.harvestedThingDef;
            int count=recipe.ingredients.Where(i=>i.filter.Allows(raw)).Sum(i=>i.CountRequiredOfFor(raw,recipe));
            float fieldWork=(plant.plant.sowWork+plant.plant.harvestWork)*count/Math.Max(1,plant.plant.harvestYield);
            return price*recipe.products.Where(p=>p.thingDef==def).Sum(p=>p.count)/Math.Max(1,recipe.workAmount+fieldWork);
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
