using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Planning
{
    public sealed class SupplyRequirement
    {
        public ThingDef Def;
        public int Target, Stock, Missing;
        public string Route, Detail;
    }
    // Bounded next-batch forecast, not a native reservation or universal recipe solver.
    public sealed class IndustrySupplyPlan
    {
        public List<SupplyRequirement> Requirements=new List<SupplyRequirement>();
        public Dictionary<ThingDef,int> Targets=new Dictionary<ThingDef,int>();
        public void Clear() {Requirements.Clear();Targets.Clear();}
        public static bool Component(ThingDef def)=>def==ThingDefOf.ComponentIndustrial || def?.defName=="ComponentSpacer";
        public static bool LocalSource(Map map,ThingDef def)
        {
            var work=def==ThingDefOf.WoodLog?WorkTypeDefOf.PlantCutting:WorkTypeDefOf.Mining;
            var workers=map.mapPawns.FreeColonistsSpawned.Where(p=>WorkPriorityManager.CanWork(p) && !WorkReadiness.NeedsRecovery(p) &&
                !p.WorkTypeIsDisabled(work) && p.workSettings.Initialized && p.workSettings.GetPriority(work)>0).ToList();
            return workers.Count>0 && map.listerThings.AllThings.Any(t=>t.Spawned && !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer) &&
                (def==ThingDefOf.WoodLog?t is Plant plant && plant.HarvestableNow && plant.def.plant.harvestedThingDef==def:t.def.building?.mineableThing==def) &&
                workers.Any(p=>p.Position.DistanceTo(t.Position)<=60 && t.Position.IsInAllowedArea(p) && p.CanReach(t,PathEndMode.Touch,Danger.None)));
        }
        public static ThingDef PreferredInput(Map map,RecipeDef recipe,Bill_Production bill,IngredientCount ingredient)
        {
            return ingredient.filter.AllowedThingDefs.Where(d=>d.defName!="Leather_Human" && !d.IsNutritionGivingIngestible && !d.IsMedicine &&
                    (bill==null || ingredient.IsFixedIngredient || bill.ingredientFilter.Allows(d)))
                    .OrderByDescending(d=>CommercePlanner.Stock(map,d)>=ingredient.CountRequiredOfFor(d,recipe,bill))
                    .ThenByDescending(d=>d==ThingDefOf.Steel || d==ThingDefOf.Cloth).ThenBy(d=>d.defName,StringComparer.Ordinal).FirstOrDefault();
        }
        private void Add(Map map,RecipeDef recipe,Bill_Production bill,int iterations=1)
        {
            foreach(var ingredient in recipe.ingredients)
            {
                var def=PreferredInput(map,recipe,bill,ingredient);
                if(def==null)continue;
                int cost=ingredient.CountRequiredOfFor(def,recipe,bill)*iterations;
                Targets[def]=Math.Min(2000,(Targets.TryGetValue(def,out int n)?n:0)+cost);
            }
        }
        public void Update(Map map,AutonomousRimMapComponent ai,List<ManagedFoodBill> bills)
        {
            Clear();
            bills.RemoveAll(b=>b?.Bill==null || b.Bill.DeletedOrDereferenced);
            bool safe=ai.EquipmentAutomation && !ai.SecondarySuspended && ai.CurrentState?.HostilePawnCount==0 &&
                ai.CurrentState.Threat?.Immediate!=true && ai.CurrentState.EstimatedFoodDays>=3 && !EmergencyManager.LocalFire(map);
            if(!safe)
            {
                foreach(var own in bills.Where(b=>b.Matches)) {own.Bill.suspended=true;own.Signature=ManagedFoodBill.Describe(own.Bill);}
                return;
            }
            var orders=ai.DefenseBills.Where(m=>m.Matches && BillDiagnostics.Requested(m.Bill) &&
                m.Bill.recipe.products?.Any(p=>p.thingDef.IsWeapon || p.thingDef.IsApparel)==true).Take(8).ToList();
            foreach(var own in orders)Add(map,own.Bill.recipe,own.Bill);
            var routes=new Dictionary<ThingDef,string>();
            // Expand advanced first: industrial components are an input of that recipe.
            foreach(var name in new[]{"ComponentSpacer","ComponentIndustrial"})
            {
                var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if(def==null || !Targets.TryGetValue(def,out int desired))continue;
                desired=Math.Min(name=="ComponentSpacer"?8:24,desired);Targets[def]=desired;
                int deficit=desired-CommercePlanner.Stock(map,def)-CommercePlanner.InTransit(ai.Commerce,def);
                if(deficit<=0)continue;
                if(LocalSource(map,def)) {routes[def]="local";continue;}
                var recipe=DefDatabase<RecipeDef>.GetNamedSilentFail("Make_"+name);
                var tables=recipe==null?new List<Building_WorkTable>():map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>()
                    .Where(t=>t.def.AllRecipes.Contains(recipe) && !t.IsForbidden(Faction.OfPlayer)).ToList();
                if(recipe==null || !recipe.AvailableNow || tables.Count==0)
                {routes[def]="commerce";continue;}
                var delegated=ai.DefenseBills.FirstOrDefault(m=>m.Matches && m.Bill.recipe==recipe);
                if(delegated!=null)
                {
                    // Existing defense owns its target and stability suspension. Read only.
                    bool activeOrder=BillDiagnostics.Requested(delegated.Bill);
                    routes[def]=activeOrder?"manufacture":"paused";
                    if(activeOrder)
                        Add(map,recipe,delegated.Bill,(int)Math.Ceiling(deficit/(float)Math.Max(1,recipe.products.Where(p=>p.thingDef==def).Sum(p=>p.count))));
                    continue;
                }
                FoodManager.EnsureBill(map,recipe.defName,ai.CurrentState,bills,desired);
                var own=bills.FirstOrDefault(b=>b.Matches && b.Bill.recipe==recipe);
                if(own==null) {routes[def]="manual";continue;}
                own.Bill.suspended=false;own.Signature=ManagedFoodBill.Describe(own.Bill);
                routes[def]="manufacture";
                int output=recipe.products.Where(p=>p.thingDef==def).Sum(p=>p.count);
                Add(map,recipe,own.Bill,(int)Math.Ceiling(deficit/(float)Math.Max(1,output)));
            }
            foreach(var own in bills.Where(b=>b.Matches))
                if(!own.Bill.recipe.products.Any(p=>Targets.ContainsKey(p.thingDef) && routes.TryGetValue(p.thingDef,out string r) && r=="manufacture"))
                {own.Bill.suspended=true;own.Signature=ManagedFoodBill.Describe(own.Bill);}
            var active=orders.Concat(bills.Where(b=>b.Matches && BillDiagnostics.Requested(b.Bill))).Select(m=>m.Bill).ToList();
            foreach(var table in map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Where(t=>t.BillStack.Bills.Any(active.Contains)))
            {
                var fuel=table.TryGetComp<CompRefuelable>();
                if(fuel?.allowAutoRefuel!=true || !fuel.ShouldAutoRefuelNow || !fuel.Props.fuelFilter.Allows(ThingDefOf.WoodLog))continue;
                Targets[ThingDefOf.WoodLog]=(Targets.TryGetValue(ThingDefOf.WoodLog,out int wood)?wood:0)+fuel.GetFuelCountToFullyRefuel();
            }
            foreach(var pair in Targets)
            {
                int stock=CommercePlanner.Stock(map,pair.Key),missing=Math.Max(0,pair.Value-stock-CommercePlanner.InTransit(ai.Commerce,pair.Key));
                string route=missing==0?"stock":routes.TryGetValue(pair.Key,out string r)?r:LocalSource(map,pair.Key)?"local":"commerce";
                string detail=route=="manufacture"?"Produzir em bancada; ingredientes e profissão continuam sujeitos aos jobs nativos.":
                    route=="local"?"Coleta local segura por corte/mineração.":route=="paused"?"Produção aguardando a política do checkpoint de defesa.":route=="manual"?"Receita sob controle manual; pedido automático adiado.":
                    route=="stock"?"Estoque ou entrega já registrada cobre a previsão.":ai.CommerceAutomation?"Pedido integrado à fila comercial; aguarda oferta e orçamento.":"Sem fonte local viável; habilitar comércio ou obter manualmente.";
                Requirements.Add(new SupplyRequirement{Def=pair.Key,Target=pair.Value,Stock=stock,Missing=missing,Route=route,Detail=detail});
            }
            ai.Coordination.Refresh(map,ai.WorkChanges,true);
        }
    }
}
