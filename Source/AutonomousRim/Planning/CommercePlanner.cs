using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    public static class CommercePlanner
    {
        public static readonly string[] Products={"SmokeleafJoint"};
        private static Thing jointReference;
        public static int Stock(Map map,ThingDef def)=>def==null?0:map.listerThings.ThingsOfDef(def).Where(t=>t.Spawned &&
            !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer) && !t.IsNotFresh()).Sum(t=>t.stackCount)+
            map.mapPawns.FreeColonistsSpawned.Sum(p=>p.inventory.innerContainer.Where(t=>t.def==def && !t.IsNotFresh()).Sum(t=>t.stackCount)+
                (p.carryTracker.CarriedThing?.def==def && !p.carryTracker.CarriedThing.IsNotFresh()?p.carryTracker.CarriedThing.stackCount:0));
        public static int InTransit(CommerceState commerce,ThingDef def)=>commerce.Deliveries.Where(d=>d.Def==def).Sum(d=>d.Count);
        public static int InProduction(Map map,ThingDef def)=>map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>()
            .SelectMany(t=>t.BillStack.Bills.OfType<Bill_ProductionWithUft>().Select(b=>b.BoundUft)).Where(u=>u!=null).Distinct()
            .Where(u=>u.MapHeld==map && u.BoundBill!=null && u.Creator?.Faction==Faction.OfPlayer &&
                !u.Creator.Dead && u.workLeft>=0).Sum(u=>u.Recipe?.products?.Where(p=>p.thingDef==def).Sum(p=>p.count)??0);
        public static void Evaluate(Map map,ColonyState colony,IReadOnlyList<RoomProject> rooms,CommerceState commerce)
        {
            int tick=Find.TickManager.TicksGame;
            // Orbital stock is only counted after the real delivery becomes accessible.
            // Confirm each accumulated batch against accessible stock; a timeout is not delivery.
            foreach(var group in commerce.Deliveries.Where(d=>d.Def!=null).GroupBy(d=>d.Def).ToList())
            {
                int available=Stock(map,group.Key);
                foreach(var delivery in group.OrderBy(d=>d.Tick).ToList())
                {
                    // Identified orbital goods are confirmed by their real pod opening,
                    // never by unrelated local production of the same material.
                    if(delivery.ShipmentIds?.Count>0)continue;
                    if(available<delivery.Baseline+delivery.Count)break;
                    commerce.Deliveries.Remove(delivery);
                    commerce.Record("Entrega acessível: "+delivery.Def.LabelCap+" ×"+delivery.Count);
                }
            }
            commerce.Deliveries.RemoveAll(d=>d.Def==null);
            commerce.Needs.Clear();
            void need(ThingDef def,int target,int priority,string reason)
            {
                if(def==null || target<=0)return;
                int deficit=target-Stock(map,def)-InTransit(commerce,def)-InProduction(map,def);
                if(deficit<=0)return;
                var old=commerce.Needs.FirstOrDefault(n=>n.Def==def);
                if(old==null)commerce.Needs.Add(new CommerceNeed{Def=def,Count=deficit,Priority=priority,Reason=reason});
                else {old.Count=Math.Max(old.Count,deficit);old.Priority=Math.Max(old.Priority,priority);old.Reason+="; "+reason;}
            }
            var checkpoint=LoadoutProgression.Analyze(map,DefenseProductionPlan.Stage(map));
            // Only a bounded batch of essential infrastructure feeds the purchase queue.
            var construction=new Dictionary<ThingDef,int>();
            foreach(var pending in map.listerThings.AllThings.Where(t=>t.Faction==Faction.OfPlayer && (t is Blueprint_Build || t is Frame)))
                foreach(var cost in pending is Frame frame?frame.TotalMaterialCost():((Blueprint_Build)pending).TotalMaterialCost())
                {
                    int missing=pending is Frame f?f.ThingCountNeeded(cost.thingDef):cost.count;
                    construction[cost.thingDef]=(construction.TryGetValue(cost.thingDef,out int v)?v:0)+missing;
                }
            foreach(var task in rooms.Where(r=>!r.Completed && r.Crop==null && r.Priority<=ConstructionPriority.High)
                .OrderBy(r=>r.Priority).SelectMany(BaseConstructionManager.Tasks).Distinct()
                .Where(t=>!t.CancelledByPlayer && !t.Complete(map) && !t.Issued && t.Def is ThingDef).Take(12))
                foreach(var cost in CostListCalculator.CostListAdjusted(task.Def,task.Stuff))
                    construction[cost.thingDef]=(construction.TryGetValue(cost.thingDef,out int v)?v:0)+cost.count;
            foreach(var pair in construction)need(pair.Key,pair.Value,85,"Infraestrutura essencial");
            var equipmentMaterials=new Dictionary<string,int>(checkpoint.Materials);
            foreach(var demand in commerce.ExpeditionMaterials.Where(n=>n.Def!=null))
                equipmentMaterials[demand.Def.defName]=Math.Max(equipmentMaterials.TryGetValue(demand.Def.defName,out int existing)?existing:0,demand.Count);
            foreach(var pair in equipmentMaterials)
            {
                var def=DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key);
                int extra=def!=null && construction.TryGetValue(def,out int c)?c:0;
                need(def,pair.Value+extra,70,"Conjunto militar da etapa "+(checkpoint.Stage+1));
            }
            var ai=map.GetComponent<AutonomousRimMapComponent>();
            if(ai.EquipmentAutomation)
                foreach(var request in ai.Industry.Requirements.Where(r=>r.Route=="commerce" && r.Missing>0))
                    need(request.Def,request.Target,75,"Insumo industrial sem fonte local viável");
            if(colony.EstimatedFoodDays<2)
                need(ThingDefOf.MealSimple,Math.Max(8,(colony.ColonistCount+colony.PrisonerCount)*6),100,"Reserva de alimentação urgente");
            int meds=map.listerThings.AllThings.Where(t=>t.Spawned && t.def.IsMedicine && !t.IsForbidden(Faction.OfPlayer) && !t.Position.Fogged(map)).Sum(t=>t.stackCount);
            if(meds<(colony.ColonistCount+colony.PrisonerCount)*2)need(ThingDefOf.MedicineIndustrial,Stock(map,ThingDefOf.MedicineIndustrial)+(colony.ColonistCount+colony.PrisonerCount)*2-meds,95,"Reserva médica");
            commerce.Needs=commerce.Needs.OrderByDescending(n=>n.Priority).ThenBy(n=>n.Def.defName,StringComparer.Ordinal).ToList();
            commerce.Silver=Stock(map,ThingDefOf.Silver);
            commerce.Reserve=Math.Max(150,colony.ColonistCount*50);
            commerce.Budget=Math.Max(0,commerce.Silver-commerce.Reserve);
            commerce.FundingGap=Math.Max(0,commerce.Needs.Sum(n=>n.Count*n.Def.GetStatValueAbstract(StatDefOf.MarketValue)*1.5f)-commerce.Budget);
            commerce.Product="SmokeleafJoint";
            if(commerce.Needs.Count==0)commerce.LastDemandTick=tick;
            else if(commerce.LastDemandTick==0)commerce.LastDemandTick=tick;
        }

        public static RecipeDef ProductRecipe(string name)=>DefDatabase<RecipeDef>.AllDefsListForReading
            .Where(r=>r.products?.Any(p=>p.thingDef.defName==name)==true).OrderBy(r=>r.workAmount).FirstOrDefault();
        public static void Production(Map map,ColonyState colony,List<RoomProject> rooms,CommerceState commerce,bool constructionEnabled)
        {
            var safe=colony.HostilePawnCount==0 && colony.EstimatedFoodDays>=3 &&
                !EmergencyManager.LocalFire(map) && map.mapPawns.FreeColonistsSpawned.Any(p=>WorkPriorityManager.CanWork(p) && !WorkReadiness.NeedsRecovery(p));
            commerce.Bills.RemoveAll(b=>b.Bill==null || b.Bill.DeletedOrDereferenced);
            var recipe=ProductRecipe(commerce.Product);
            if(recipe==null)return;
            var product=DefDatabase<ThingDef>.GetNamedSilentFail(commerce.Product);
            float funding=commerce.FundingGap+Math.Max(0,commerce.SilverTarget-commerce.Silver);
            int desired=Math.Min(500,Math.Max(commerce.JointReserve,(int)Math.Ceiling(funding/Math.Max(1,product.GetStatValueAbstract(StatDefOf.MarketValue)*.5f))));
            // A small first batch can await a buyer. Further growth needs observed demand.
            bool recentBuyer=commerce.LastTrade>0 && Find.TickManager.TicksGame-commerce.LastTrade<GenDate.TicksPerDay*10;
            if(!recentBuyer)desired=Math.Min(100,desired);
            var storage=map.haulDestinationManager.AllGroupsListForReading.Where(g=>g.Settings.filter.Allows(product) &&
                g.CellsList.Any(c=>!c.Fogged(map) && c.GetSlotGroup(map)==g)).ToList();
            if(jointReference==null)jointReference=ThingMaker.MakeThing(product);
            int free=0;
            foreach(var cell in storage.SelectMany(g=>g.CellsList).Distinct().Where(c=>!c.Fogged(map) &&
                StoreUtility.IsGoodStoreCell(c,map,jointReference,null,Faction.OfPlayer)))
            {
                var items=cell.GetThingList(map).Where(t=>t.def.category==ThingCategory.Item).ToList();
                int slots=cell.GetMaxItemsAllowedInCell(map);
                free+=Math.Max(0,slots-items.Count)*product.stackLimit+items.Where(t=>t.def==product).Sum(t=>Math.Max(0,product.stackLimit-t.stackCount));
            }
            bool storageFull=storage.Count>0 && free==0;
            desired=Math.Min(desired,storage.Count>0?Math.Max(commerce.JointReserve,Stock(map,product)+free):100);
            commerce.Target=desired;
            commerce.ProductionStatus=storageFull?"Pausada: armazenamento permitido cheio; aguardar venda ou novas prateleiras.":!safe?"Pausada: alimento, defesa ou recuperação exigem os trabalhadores.":
                !recipe.AvailableNow?"Aguardar pesquisa da receita.":Stock(map,product)>=desired?"Reserva comercial completa.":"Produzir até "+desired+" baseados; preservar atividades essenciais.";
            if(safe && !storageFull && desired>0 && recipe.AvailableNow)FoodManager.EnsureBill(map,recipe.defName,colony,commerce.Bills,desired);
            foreach(var bill in commerce.Bills.Where(b=>b.Matches))
            {
                bool active=bill.Bill.recipe==recipe && safe && !storageFull && desired>0 && recipe.AvailableNow;
                bill.Bill.suspended=!active;
                if(active){bill.Bill.targetCount=desired;bill.Bill.unpauseWhenYouHave=desired;}
                var covered=Building_OrbitalTradeBeacon.AllPowered(map).SelectMany(b=>b.TradeableCells).ToHashSet();
                var destination=storage.Where(g=>g.CellsList.Count>0 && g.CellsList.All(covered.Contains) &&
                    g.CellsList.Any(c=>StoreUtility.IsGoodStoreCell(c,map,jointReference,null,Faction.OfPlayer)))
                    .OrderBy(g=>g.CellsList[0].DistanceToSquared(((Thing)bill.Bill.billStack.billGiver).Position)).FirstOrDefault();
                if(destination!=null && (bill.StorageSignature!=null || bill.Bill.GetStoreMode()==BillStoreModeDefOf.BestStockpile))
                    bill.Bill.SetStoreMode(BillStoreModeDefOf.SpecificStockpile,destination);
                bill.StorageSignature=ManagedFoodBill.DescribeStorage(bill.Bill);
                bill.Signature=ManagedFoodBill.Describe(bill.Bill);
            }
            if(!constructionEnabled || !safe || desired<=0)return;
            CommerceInfrastructure.Add(map,rooms,recipe);
            if(desired<=Stock(map,product))return;
            var plantName="Plant_Smokeleaf";
            var plant=DefDatabase<ThingDef>.GetNamedSilentFail(plantName);
            if(plant?.plant==null || !map.mapPawns.FreeColonistsSpawned.Any(p=>WorkPriorityManager.CanWork(p) &&
                !p.WorkTypeIsDisabled(WorkTypeDefOf.Growing) && p.skills.GetSkill(SkillDefOf.Plants).Level>=plant.plant.sowMinSkill))return;
            var raw=plant.plant.harvestedThingDef;
            int rawNeed=recipe.ingredients.Where(i=>i.filter.Allows(raw)).Sum(i=>i.CountRequiredOfFor(raw,recipe)) * Math.Max(0,desired-Stock(map,product));
            int cells=Math.Min(recentBuyer?144:36,Math.Max(16,(int)Math.Ceiling((rawNeed-Stock(map,raw))/Math.Max(1,plant.plant.harvestYield))));
            AgriculturePlanner.AddCommercial(map,rooms,plant,cells);
        }
        public static IEnumerable<ResearchProjectDef> Research(Map map)
        {
            var state=map.GetComponent<AutonomousRimMapComponent>();
            if(!state.CommerceAutomation)yield break;
            var recipe=ProductRecipe(state.Commerce.Product);
            if(recipe?.researchPrerequisite!=null)yield return recipe.researchPrerequisite;
            foreach(var def in recipe?.researchPrerequisites??new List<ResearchProjectDef>())yield return def;
            foreach(var name in new[]{"DrugLab","CommsConsole","OrbitalTradeBeacon"})
                foreach(var r in DefDatabase<ThingDef>.GetNamedSilentFail(name)?.researchPrerequisites??new List<ResearchProjectDef>())yield return r;
        }
    }
}
