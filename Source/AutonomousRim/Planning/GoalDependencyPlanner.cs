using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    public sealed class DependencyGoal
    {
        public string Id, Parent, Kind, Detail;
        public int Required, Available;
    }
    public sealed class GoalDependencyPlanner
    {
        public List<DependencyGoal> Nodes=new List<DependencyGoal>();
        public Dictionary<ThingDef,int> Supplies=new Dictionary<ThingDef,int>();
        public HashSet<RecipeDef> Recipes=new HashSet<RecipeDef>();
        public HashSet<ResearchProjectDef> Research=new HashSet<ResearchProjectDef>();
        public bool HasDemand=>Nodes.Count>0;
        private Dictionary<ThingDef,int> stock;
        private HashSet<RecipeDef> neededRoutes=new HashSet<RecipeDef>();
        private Dictionary<RecipeDef,int> lastNeeded=new Dictionary<RecipeDef,int>();
        private Map map;
        private AutonomousRimMapComponent ai;
        public void Update(Map value,AutonomousRimMapComponent component,List<ManagedFoodBill> own)
        {
            map=value;ai=component;Nodes.Clear();Supplies.Clear();Recipes.Clear();Research.Clear();neededRoutes.Clear();
            own.RemoveAll(m=>m?.Bill==null || m.Bill.DeletedOrDereferenced);
            if(!ai.BaseAutomation && !ai.FoodAutomation && !ai.EquipmentAutomation && !ai.CommerceAutomation)
            {foreach(var managed in own.Where(m=>m.Matches)){managed.Bill.suspended=true;managed.Signature=ManagedFoodBill.Describe(managed.Bill);}return;}
            stock=new Dictionary<ThingDef,int>();
            foreach(var thing in map.listerThings.AllThings.Where(t=>t.Spawned && t.def.category==ThingCategory.Item && !t.Fogged() && !t.IsForbidden(Faction.OfPlayer))
                .Concat(map.mapPawns.FreeColonistsSpawned.SelectMany(p=>p.inventory.innerContainer.Concat(p.carryTracker.innerContainer))).Where(t=>t.TryGetComp<CompRottable>()?.Stage!=RotStage.Rotting && t.TryGetComp<CompRottable>()?.Stage!=RotStage.Dessicated).Distinct())
                stock[thing.def]=(stock.TryGetValue(thing.def,out int count)?count:0)+thing.stackCount;
            var roots=ai.ManagedProduction.Where(m=>m?.Matches==true && !own.Contains(m) && BillDiagnostics.Requested(m.Bill)).ToList();
            int tick=Find.TickManager.TicksGame;
            int offset=roots.Count==0?0:((tick/1200)*12)%roots.Count;
            foreach(var root in roots.Skip(offset).Concat(roots.Take(offset)).Take(12)) {
                if(Nodes.Count>=64)break;
                string id="bill:"+root.Bill.GetUniqueLoadID();
                Nodes.Add(new DependencyGoal{Id=id,Kind="Resultado",Detail=root.Bill.LabelCap+"; confirmar no contador nativo, não pela ordem emitida."});
                ExpandRecipe(root.Bill.recipe,root.Bill,id,0,new HashSet<RecipeDef>(),own);
            }
            foreach(var obsolete in own.Where(m=>m.Matches && !neededRoutes.Contains(m.Bill.recipe) && (!lastNeeded.TryGetValue(m.Bill.recipe,out int last) || tick-last>1200*(2+(roots.Count+11)/12)))) {
                obsolete.Bill.suspended=true;obsolete.Signature=ManagedFoodBill.Describe(obsolete.Bill);
            }
        }
        private void ExpandRecipe(RecipeDef recipe,Bill_Production bill,string parent,int depth,HashSet<RecipeDef> ancestors,List<ManagedFoodBill> own)
        {
            if(depth>=6 || Nodes.Count>=64 || !ancestors.Add(recipe)) {
                if(Nodes.Count<64)Nodes.Add(new DependencyGoal{Parent=parent,Kind="Limite",Detail="Ciclo ou limite de dependências; não emitir Bills recursivas."});return;
            }
            foreach(var project in (recipe.researchPrerequisites??new List<ResearchProjectDef>())
                .Concat(recipe.researchPrerequisite==null?Enumerable.Empty<ResearchProjectDef>():new[]{recipe.researchPrerequisite}).Where(p=>!p.IsFinished)) {
                if(Nodes.Count>=62)break;
                Research.Add(project);Nodes.Add(new DependencyGoal{Parent=parent,Kind="Pesquisa",Detail=project.LabelCap});
            }
            if(Nodes.Count>=64)return;
            Recipes.Add(recipe);
            var tables=map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Where(t=>t.def.AllRecipes.Contains(recipe)).ToList();
            Nodes.Add(new DependencyGoal{Parent=parent,Kind="Bancada",Available=tables.Count,Required=1,Detail=tables.Count==0?"Sem bancada para "+recipe.LabelCap:"Bancada existente; energia, combustível e acesso validados pelo job nativo."});
            foreach(var ingredient in recipe.ingredients) {
                if(Nodes.Count>=64)break;
                var allowed=ingredient.filter.AllowedThingDefs.Where(d=>d.defName!="Leather_Human" &&
                    (bill==null || ingredient.IsFixedIngredient || bill.ingredientFilter.Allows(d))).ToList();
                // Food mixtures and special ingredients stay with the native allocator.
                if(allowed.Count==0 || allowed.Any(d=>d.IsNutritionGivingIngestible || d.IsMedicine)) {
                    Nodes.Add(new DependencyGoal{Parent=parent,Kind="Ingrediente nativo",Detail=ingredient.filter.Summary+"; cultivo/comida/medicina mantêm seus planejadores."});continue;
                }
                var def=allowed.OrderByDescending(d=>stock.TryGetValue(d,out int n)?n:0).ThenBy(d=>d.defName,StringComparer.Ordinal).First();
                int required=ingredient.CountRequiredOfFor(def,recipe,bill);
                int available=(stock.TryGetValue(def,out int count)?count:0)+CommercePlanner.InTransit(ai.Commerce,def);
                string id=parent+"/"+def.defName;
                Nodes.Add(new DependencyGoal{Id=id,Parent=parent,Kind="Recurso",Required=required,Available=available,Detail=def.LabelCap+"; entregas pendentes não são estoque utilizável."});
                if(available>=required)continue;
                Supplies[def]=Math.Max(required,Supplies.TryGetValue(def,out int target)?target:0);
                if(!ItemCatalog.Entries.TryGetValue(def,out var entry))continue;
                var route=entry.Recipes.Where(r=>r!=recipe && r.products?.Count==1 && r.products[0].thingDef==def && !ancestors.Contains(r))
                    .OrderByDescending(r=>r.AvailableNow).ThenBy(r=>r.defName,StringComparer.Ordinal).FirstOrDefault();
                if(route==null)continue;
                // Known native component chains already have one owner; never duplicate them.
                if(IndustrySupplyPlan.Component(def))continue;
                neededRoutes.Add(route);lastNeeded[route]=Find.TickManager.TicksGame;
                var branch=new HashSet<RecipeDef>(ancestors);
                ExpandRecipe(route,null,id,depth+1,branch,own);
                bool safe=!ai.SecondarySuspended && ai.CurrentState?.EstimatedFoodDays>=3 && ai.CurrentState.HostilePawnCount==0;
                var existing=own.FirstOrDefault(m=>m.Matches && m.Bill.recipe==route);
                if(existing!=null && safe) {existing.Bill.suspended=false;existing.Signature=ManagedFoodBill.Describe(existing.Bill);}
                bool delegated=map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Any(t=>t.BillStack.Bills.Any(b=>b.recipe==route));
                if(!safe || !route.AvailableNow || delegated || own.Count>=8)continue;
                FoodManager.EnsureBill(map,route.defName,ai.CurrentState,own,Math.Min(100,required));
            }
        }
    }
}
