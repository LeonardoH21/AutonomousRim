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
        public static readonly string[] Products={"SmokeleafJoint","Flake","Yayo"};
        public static int Stock(Map map,ThingDef def)=>map.listerThings.ThingsOfDef(def).Where(t=>t.Spawned &&
            !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer) && !t.IsNotFresh()).Sum(t=>t.stackCount);
        public static int InTransit(CommerceState commerce,ThingDef def)=>commerce.Deliveries.Where(d=>d.Def==def).Sum(d=>d.Count);
        public static void Evaluate(Map map,ColonyState colony,IReadOnlyList<RoomProject> rooms,CommerceState commerce)
        {
            int tick=Find.TickManager.TicksGame;
            // Orbital stock is only counted after the real delivery becomes accessible.
            commerce.Deliveries.RemoveAll(d=>d.Def==null || Stock(map,d.Def)>=d.Baseline+d.Count ||
                commerce.ExpeditionPawns.Count==0 && tick-d.Tick>GenDate.TicksPerDay);
            commerce.Needs.Clear();
            void need(ThingDef def,int target,int priority,string reason)
            {
                if(def==null || target<=0)return;
                int deficit=target-Stock(map,def)-InTransit(commerce,def);
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
            if(colony.EstimatedFoodDays<2)
                need(ThingDefOf.MealSimple,Math.Max(8,(colony.ColonistCount+colony.PrisonerCount)*6),100,"Reserva de alimentação urgente");
            int meds=map.listerThings.AllThings.Where(t=>t.Spawned && t.def.IsMedicine && !t.IsForbidden(Faction.OfPlayer) && !t.Position.Fogged(map)).Sum(t=>t.stackCount);
            if(meds<(colony.ColonistCount+colony.PrisonerCount)*2)need(ThingDefOf.MedicineIndustrial,Stock(map,ThingDefOf.MedicineIndustrial)+(colony.ColonistCount+colony.PrisonerCount)*2-meds,95,"Reserva médica");
            commerce.Needs=commerce.Needs.OrderByDescending(n=>n.Priority).ThenBy(n=>n.Def.defName,StringComparer.Ordinal).ToList();
            commerce.Silver=Stock(map,ThingDefOf.Silver);
            commerce.Reserve=Math.Max(150,colony.ColonistCount*50);
            commerce.Budget=Math.Max(0,commerce.Silver-commerce.Reserve);
            commerce.FundingGap=Math.Max(0,commerce.Needs.Sum(n=>n.Count*n.Def.GetStatValueAbstract(StatDefOf.MarketValue)*1.5f)-commerce.Budget);
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
            int desired=commerce.FundingGap>0?Math.Min(500,Math.Max(40,(int)Math.Ceiling(commerce.FundingGap/Math.Max(1,product.GetStatValueAbstract(StatDefOf.MarketValue)*.5f)))):0;
            // A small first batch can await a buyer. Further growth needs observed demand.
            bool recentBuyer=commerce.LastTrade>0 && Find.TickManager.TicksGame-commerce.LastTrade<GenDate.TicksPerDay*10;
            if(!recentBuyer)desired=Math.Min(100,desired);
            commerce.Target=desired;
            if(safe && desired>0 && recipe.AvailableNow)FoodManager.EnsureBill(map,recipe.defName,colony,commerce.Bills,desired);
            foreach(var bill in commerce.Bills.Where(b=>b.Matches))
            {
                bool active=bill.Bill.recipe==recipe && safe && desired>0 && recipe.AvailableNow;
                bill.Bill.suspended=!active;
                if(active){bill.Bill.targetCount=desired;bill.Bill.unpauseWhenYouHave=desired;}
                bill.Signature=ManagedFoodBill.Describe(bill.Bill);
            }
            if(!constructionEnabled || !safe || desired<=0)return;
            CommerceInfrastructure.Add(map,rooms,recipe);
            if(desired<=Stock(map,product))return;
            var plantName=commerce.Product=="SmokeleafJoint"?"Plant_Smokeleaf":"Plant_Psychoid";
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
            if(!state.CommerceAutomation || state.Commerce.Needs.Count==0)yield break;
            var recipe=ProductRecipe(state.Commerce.Product);
            if(recipe?.researchPrerequisite!=null)yield return recipe.researchPrerequisite;
            foreach(var def in recipe?.researchPrerequisites??new List<ResearchProjectDef>())yield return def;
            foreach(var name in new[]{"DrugLab","CommsConsole","OrbitalTradeBeacon"})
                foreach(var r in DefDatabase<ThingDef>.GetNamedSilentFail(name)?.researchPrerequisites??new List<ResearchProjectDef>())yield return r;
        }
    }
}
