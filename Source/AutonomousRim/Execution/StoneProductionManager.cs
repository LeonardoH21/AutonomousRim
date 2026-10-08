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
            int budget=stone.Sum(t=>t.stackCount);
            foreach(var task in projects.SelectMany(p=>p.Shell).Where(t=>t.Def==ThingDefOf.Wall && !t.Issued && !t.WasCompleted && !t.CancelledByPlayer))
            {
                int cost=CostListCalculator.CostListAdjusted(ThingDefOf.Wall,stone.Key).Sum(c=>c.count);
                if(budget<cost)break;
                task.Stuff=stone.Key;budget-=cost;
            }
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
