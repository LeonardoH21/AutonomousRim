using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    // Read-only presentation data: no research selection, bills or equipment orders.
    public sealed class ProgressDashboard
    {
        public List<ResearchProjectDef> Queue;
        public List<StrategicGoal> Goals;
        public LoadoutCheckpoint Gear;
        public Dictionary<string,int> Stock;
        public int CurrentStage;
        public Dictionary<string,List<ResearchProjectDef>> Research=new Dictionary<string,List<ResearchProjectDef>>();
        public Dictionary<string,string> Benches=new Dictionary<string,string>();
        public static List<ResearchProjectDef> ResearchQueue(ResearchProjectDef current,IEnumerable<ResearchProjectDef> route) =>
            new[]{current}.Concat(route ?? Enumerable.Empty<ResearchProjectDef>())
                .Where(r=>r!=null && !r.IsFinished).Distinct().Take(6).ToList();
        public static ProgressDashboard Read(Map map,AutonomousRimMapComponent ai,int requestedStage=-1)
        {
            var plan=ai.Strategy;
            if(plan.Route.Count==0 && plan.Goals.Count==0)
            {
                plan=new StrategicPlan{Landing=map.mapPawns.FreeColonistsSpawned.FirstOrDefault()?.Position??IntVec3.Invalid,
                    LastTerrainEvaluation=Find.TickManager.TicksGame,TerrainStatus="Prévia do painel"};
                StrategicPlanner.Evaluate(map,ai.CurrentState??Perception.ColonyStateScanner.Scan(map),ai.BaseProjects,plan,false);
            }
            int stage=DefenseProductionPlan.Stage(map);
            var result=new ProgressDashboard{
                Queue=ResearchQueue(Find.ResearchManager.GetProject(),plan.Route),Goals=plan.Goals.ToList(),CurrentStage=stage,
                Gear=LoadoutProgression.Analyze(map,requestedStage<0?stage:Math.Max(0,Math.Min(4,requestedStage))),
                Stock=map.listerThings.AllThings.Where(t=>t.Spawned && t.def.category==ThingCategory.Item &&
                    !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer)).GroupBy(t=>t.def.defName)
                    .ToDictionary(g=>g.Key,g=>g.Sum(t=>t.stackCount))};
            foreach(var item in result.Gear.Items.Select(r=>r.Target).Distinct())
            {
                var recipe=Recipe(item);
                var tables=recipe==null?new List<ThingDef>():DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.thingClass!=null && typeof(Building_WorkTable).IsAssignableFrom(d.thingClass) && d.AllRecipes?.Contains(recipe)==true).ToList();
                result.Research[item]=RequiredResearch(item);
                if(tables.Count>0 && !tables.Any(t=>t.IsResearchFinished))
                    result.Research[item]=StrategicPlanner.ResearchRoute(result.Research[item].Concat(tables.OrderBy(t=>StrategicPlanner.ResearchRoute(t.researchPrerequisites??new List<ResearchProjectDef>()).Count).First().researchPrerequisites??new List<ResearchProjectDef>()));
                result.Benches[item]=recipe==null?"sem receita; adquirir por comércio ou loot":tables.Count==0?"nenhuma bancada compatível":string.Join(" / ",tables.Select(d=>d.LabelCap));
            }
            return result;
        }
        public static RecipeDef Recipe(string item) => DefDatabase<RecipeDef>.AllDefsListForReading
            .FirstOrDefault(r=>r.products?.Any(p=>p.thingDef.defName==item)==true);
        public static List<ResearchProjectDef> RequiredResearch(string item)
        {
            var recipe=Recipe(item);var def=DefDatabase<ThingDef>.GetNamedSilentFail(item);
            var required=(def?.researchPrerequisites??new List<ResearchProjectDef>()).AsEnumerable();
            if(recipe?.researchPrerequisite!=null)required=required.Concat(new[]{recipe.researchPrerequisite});
            if(recipe?.researchPrerequisites!=null)required=required.Concat(recipe.researchPrerequisites);
            return StrategicPlanner.ResearchRoute(required);
        }
        public static string Label(string name) => DefDatabase<ThingDef>.GetNamedSilentFail(name)?.LabelCap.ToString()??name;
        public static readonly string[] Stages={"Aço e armas iniciais","Flak e Heavy SMG","Assault Rifle","Marine, Charge Rifle e escudos","Cataphract"};
    }
}
