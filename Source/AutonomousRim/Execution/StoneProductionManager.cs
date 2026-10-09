using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    public static class StoneProductionManager
    {
        private static Dictionary<ThingDef,int> ReserveFutureWalls(Map map,ColonyState state,List<RoomProject> projects)
        {
            var category=DefDatabase<ThingCategoryDef>.GetNamed("StoneBlocks");
            // Native blueprints/frames already reserve their material. Reserve
            // unissued plans too, once per shared task, on every review.
            var budget=BaseConstructionManager.Available(map).Where(p=>p.Key.IsWithinCategory(category))
                .ToDictionary(p=>p.Key,p=>Math.Max(0,p.Value));
            var future=projects.OrderBy(p=>p.Priority).SelectMany(p=>p.Shell).Distinct()
                .Where(t=>t.Def==ThingDefOf.Wall && !t.Issued && !t.WasCompleted && !t.CancelledByPlayer &&
                    t.UpgradeMaterial==null && t.Pending?.Spawned!=true && !t.Complete(map) &&
                    !t.Position.GetThingList(map).Any(b=>b is Blueprint || b is Frame)).ToList();
            int cost(ConstructionTask task,ThingDef stuff)=>CostListCalculator.CostListAdjusted(task.Def,stuff)
                .Where(c=>c.thingDef==stuff).Sum(c=>c.count);
            foreach(var task in future.Where(t=>t.Stuff?.IsWithinCategory(category)==true))
            {
                budget.TryGetValue(task.Stuff,out int remaining);int needed=cost(task,task.Stuff);
                if(remaining>=needed)budget[task.Stuff]=remaining-needed;
                else task.Stuff=ThingDefOf.WoodLog; // Only an unissued automatic plan changes.
            }
            if(state.EstimatedFoodDays<2)return budget;
            var eligible=budget.Where(p=>p.Value>=40).Select(p=>p.Key).ToHashSet();
            foreach(var task in future.Where(t=>t.Stuff==ThingDefOf.WoodLog))
            {
                var stone=budget.Where(p=>eligible.Contains(p.Key)).OrderByDescending(p=>p.Value).FirstOrDefault();
                if(stone.Key==null)break;
                int needed=cost(task,stone.Key);if(stone.Value<needed)break;
                task.Stuff=stone.Key;budget[stone.Key]-=needed;
            }
            return budget;
        }
        public static void CancelDemolition(Map map,Thing wall)
        {
            foreach(var pawn in map.mapPawns.FreeColonistsSpawned.Where(p=>p.CurJob?.def==JobDefOf.Deconstruct &&
                p.CurJob.targetA.Thing==wall && !p.CurJob.playerForced).ToList())
                pawn.jobs.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
        }
        public static void Apply(Map map,ColonyState state,List<RoomProject> projects,List<ManagedFoodBill> bills)
        {
            bills.RemoveAll(b=>b?.Bill==null || b.Bill.DeletedOrDereferenced || !b.Matches);
            foreach(var task in projects.SelectMany(p=>p.Shell).Where(t=>t.UpgradeFrom?.Spawned==true && t.UpgradeMaterial!=null))
                if(map.designationManager.DesignationOn(task.UpgradeFrom,DesignationDefOf.Deconstruct)==null)
                {
                    // A removed demolition mark is a player's veto. Keep the original wall intact.
                    CancelDemolition(map,task.UpgradeFrom);
                    task.Stuff=task.UpgradeFrom.Stuff;task.UpgradeMaterial=null;task.UpgradeFrom=null;
                    task.UpgradeDeclined=true;task.WasCompleted=true;
                }
            var wallBudget=ReserveFutureWalls(map,state,projects);
            var recipe=DefDatabase<RecipeDef>.GetNamedSilentFail("Make_StoneBlocksAny");
            if(recipe==null || !recipe.AvailableNow)return;
            var category=DefDatabase<ThingCategoryDef>.GetNamed("StoneBlocks");
            int blocks=map.listerThings.AllThings.Where(t=>t.Spawned && t.def.IsWithinCategory(category) && !t.IsForbidden(Faction.OfPlayer)).Sum(t=>t.stackCount);
            int demand=projects.Where(p=>!p.Completed).SelectMany(BaseConstructionManager.Tasks).Where(t=>t.Stuff?.IsWithinCategory(category)==true && !t.Complete(map))
                .Sum(t=>CostListCalculator.CostListAdjusted(t.Def,t.Stuff).Where(c=>c.thingDef==t.Stuff).Sum(c=>c.count));
            int target=Math.Min(600,Math.Max(100,state.ColonistCount*30+demand));
            FoodManager.EnsureBill(map,recipe.defName,state,bills,target);
            foreach(var owned in bills.Where(b=>b.Matches && b.Bill.recipe==recipe))
            {
                owned.Bill.suspended=state.HostilePawnCount>0 || state.EstimatedFoodDays<2 || state.DownedColonists>0 || blocks>=target;
                owned.Signature=ManagedFoodBill.Describe(owned.Bill);
            }
            // Prefer available blocks for future walls; do not replace built/issued walls or wooden floors.
            var stone=map.listerThings.AllThings.Where(t=>t.Spawned && t.def.IsWithinCategory(category) && !t.IsForbidden(Faction.OfPlayer))
                .GroupBy(t=>t.def).OrderByDescending(g=>g.Sum(t=>t.stackCount)).FirstOrDefault();
            if(stone==null || blocks<40 || state.EstimatedFoodDays<2)return;
            wallBudget.TryGetValue(stone.Key,out int budget);
            // Replace one AI-owned wooden wall at a time, only after basic construction is stable.
            if(state.EstimatedFoodDays<4 || state.HostilePawnCount>0 || state.DownedColonists>0 ||
                projects.Any(p=>p.Crop==null && !p.Completed && p.Priority<=ConstructionPriority.High) ||
                projects.SelectMany(p=>p.Shell).Any(t=>t.UpgradeMaterial!=null && !t.Complete(map)) ||
                !BaseConstructionManager.Builders(map).Any())return;
            var candidate=projects.SelectMany(p=>p.Shell).FirstOrDefault(t=>t.Def==ThingDefOf.Wall && t.Owned && t.WasCompleted &&
                !t.CancelledByPlayer && !t.UpgradeDeclined && t.UpgradeMaterial==null && t.Position.GetEdifice(map)?.Stuff==ThingDefOf.WoodLog);
            if(candidate==null || budget<20)return;
            var wall=candidate.Position.GetEdifice(map);
            if(map.designationManager.AllDesignationsOn(wall).Any())return;
            candidate.UpgradeFrom=wall;candidate.UpgradeMaterial=stone.Key;candidate.Stuff=stone.Key;
            candidate.WasCompleted=false;candidate.Issued=false;candidate.Pending=null;
            foreach(var project in projects.Where(p=>p.Shell.Contains(candidate))){project.Completed=false;project.State=ConstructionState.Planned;}
            map.designationManager.AddDesignation(new Designation(wall,DesignationDefOf.Deconstruct));
        }
    }
}
