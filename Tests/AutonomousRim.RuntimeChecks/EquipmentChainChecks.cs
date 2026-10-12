using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class EquipmentChainChecks : MapComponent
    {
        private static int phase,start,created,finished;
        private static string goalKey;
        private static bool mined,marked;
        private Pawn recipient;
        private Bill_Production bill;
        public EquipmentChainChecks(Map map):base(map){}
        public override void ExposeData()
        {Scribe_References.Look(ref recipient,"chainRecipient");Scribe_References.Look(ref bill,"chainBill");}
        private void Check(bool pass,string text)
        {if(!pass)throw new Exception(text);Log.Message("[EquipmentChain] PASS: "+text);}
        private EquipmentGoal Goal(AutonomousRimMapComponent ai)=>ai.EquipmentDemands.Goals.First(g=>g.Current && g.Recipient==recipient && g.Target=="MeleeWeapon_LongSword");
        public override void MapComponentTick()
        {
            if(phase==99 || !EliteFixtureBaseline.Ready || !GenCommandLine.CommandLineArgPassed("autonomousrimequipmentchaintest"))return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(phase==2)
                {
                    var saved=ai.EquipmentDemands.Goals.First(g=>g.Key==goalKey);
                    Check(saved.Status=="equipped" && saved.Created==created && saved.Completed==finished && saved.Recipient==recipient,"recipient, creation and native delivery survive save/load");
                    ai.EquipmentDemands.Reconcile(map,ai);
                    Check(Goal(ai).Status=="equipped","loaded goals reconcile against actually equipped gear");
                    EliteFixtureBaseline.VerifyOriginalsPresent(map);
                    var manualCell=GenRadial.RadialCellsAround(recipient.Position,10,true).First(c=>c.InBounds(map) && c.GetThingList(map).Count==0);
                    GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MineableSteel")),manualCell,map);
                    var manual=new Designation(manualCell,DesignationDefOf.Mine);map.designationManager.AddDesignation(manual);
                    ai.SetEquipmentAutomation(false);
                    Check(map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Mine).SequenceEqual(new[]{manual}),"turning off standalone equipment clears owned mining marks and preserves manual orders");
                    phase=99;Log.Message("[EquipmentChain] DONE");return;
                }
                if(phase==1)
                {
                    marked|=map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Mine).Any();
                    mined|=map.mapPawns.FreeColonistsSpawned.Any(p=>p.CurJob?.def==JobDefOf.Mine);
                    if(Find.TickManager.TicksGame%60!=0)return;
                    ai.EquipmentDemands.Reconcile(map,ai);
                    if((Find.TickManager.TicksGame-start)/3000!=(Find.TickManager.TicksGame-start-60)/3000)
                        Log.Message("[EquipmentChain] SNAPSHOT: elapsed="+(Find.TickManager.TicksGame-start)+" goal="+Goal(ai).Status+" mined="+mined+" jobs="+string.Join(",",map.mapPawns.FreeColonistsSpawned.Select(p=>p.LabelShort+":"+p.CurJob?.def)));
                    if(Goal(ai).Status=="equipped")
                    {
                        Check(!ai.BaseAutomation && marked,"equipment requests mining with base automation disabled");
                        Check(mined,"a colonist executes native mining, without a forced job");
                        Check(recipient.equipment.Primary?.def.defName=="MeleeWeapon_LongSword","mined ingredients become a native crafted and equipped sword");
                        var goal=Goal(ai);goalKey=goal.Key;created=goal.Created;finished=goal.Completed;
                        Check(finished>=start,"completion is recorded only after actual equipment delivery");
                        phase=2;GameDataSaveLoader.SaveGame("EquipmentChainEvidence");GameDataSaveLoader.LoadGame("EquipmentChainEvidence");
                    }
                    else if(Find.TickManager.TicksGame-start>60000)throw new Exception("Chain stalled: "+Goal(ai).Status+" "+Goal(ai).Detail+" miningObserved="+mined+" marks="+marked+" jobs="+string.Join(",",map.mapPawns.FreeColonistsSpawned.Select(p=>p.LabelShort+":"+p.CurJob?.def)));
                    return;
                }
                var roster=EliteFixtureBaseline.Roster(map);
                var peaceful=DefDatabase<DifficultyDef>.GetNamed("Peaceful");
                Find.Storyteller.difficultyDef=peaceful;Find.Storyteller.difficulty.CopyFrom(peaceful);
                ai.DisableAll();
                recipient=roster.First(p=>DefenseProductionPlan.Melee(p));
                foreach(var designation in map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Mine).ToList())designation.Delete();
                foreach(var t in map.listerThings.AllThings.Where(t=>t is Building_WorkTable || t is Blueprint_Build || t is Frame || t.def==ThingDefOf.Steel || t.def.IsWeapon).ToList())t.Destroy(DestroyMode.Vanish);
                foreach(var p in roster)
                {
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced);p.drafter.Drafted=false;
                    p.equipment.DestroyAllEquipment();
                    p.needs.food.CurLevel=1;p.needs.rest.CurLevel=1;p.needs.joy.CurLevel=1;
                    foreach(var work in DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w=>!p.WorkTypeIsDisabled(w)))p.workSettings.SetPriority(work,3);
                    for(int hour=0;hour<24;hour++)p.timetable.SetAssignment(hour,TimeAssignmentDefOf.Anything);
                }
                var cell=map.Center+new IntVec3(20,0,20);
                foreach(var c in new CellRect(cell.x-7,cell.z-7,15,15).Cells)
                {
                    foreach(var t in c.GetThingList(map).Where(t=>!(t is Pawn)).ToList())t.Destroy(DestroyMode.Vanish);
                    map.terrainGrid.SetTerrain(c,TerrainDefOf.Soil);map.roofGrid.SetRoof(c,null);map.fogGrid.Unfog(c);
                }
                for(int i=0;i<roster.Count;i++)roster[i].Position=cell+new IntVec3(-3,0,i-3);
                var bench=(Building_WorkTable)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("FueledSmithy"));
                bench.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(bench,cell,map);bench.TryGetComp<CompRefuelable>().Refuel(80);
                var recipe=DefDatabase<RecipeDef>.GetNamed("Make_MeleeWeapon_LongSword");
                Check(recipe.AvailableNow,"fixture uses already researched V4 sword recipe");
                bill=(Bill_Production)recipe.MakeNewBill();bill.repeatMode=BillRepeatModeDefOf.RepeatCount;bill.repeatCount=1;
                bill.ingredientFilter.SetDisallowAll();bill.ingredientFilter.SetAllow(ThingDefOf.Steel,true);bench.BillStack.AddBill(bill);
                ((List<ManagedFoodBill>)ai.DefenseBills).Add(new ManagedFoodBill{Bill=bill,Signature=ManagedFoodBill.Describe(bill)});
                ai.SetEquipmentAutomation(true);ai.Coordination.Refresh(map,ai.WorkChanges,true);ai.EquipmentDemands.Reconcile(map,ai);
                Check(Goal(ai).Status=="blocked" && Goal(ai).Completed<0,"missing inputs do not falsely complete an equipment goal");
                ai.SetEquipmentAllowedFor(recipient,false);ai.EquipmentDemands.Reconcile(map,ai);
                Check(Goal(ai).Status=="manual","recipient exclusion blocks automatic delivery");ai.SetEquipmentAllowedFor(recipient,true);
                bill.suspended=true;ai.EquipmentDemands.Reconcile(map,ai);
                Check(Goal(ai).Status=="manual" && bill.suspended,"manual suspension remains intact");
                bill.suspended=false;float radius=bill.ingredientSearchRadius;bill.ingredientSearchRadius=5;ai.EquipmentDemands.Reconcile(map,ai);
                Check(Goal(ai).Status=="manual","changed bill signature is not claimed by the demand planner");
                bill.ingredientSearchRadius=radius;
                ai.EquipmentDemands.Reconcile(map,ai);created=Goal(ai).Created;
                ai.EquipmentDemands.Reconcile(map,ai);
                Check(Goal(ai).Created==created && ai.EquipmentDemands.Goals.Count(g=>g.Key==Goal(ai).Key)==1,"reconciliation preserves goal identity and creation time");
                foreach(var ore in map.listerThings.AllThings.Where(t=>t.def.building?.mineableThing==ThingDefOf.Steel).ToList())ore.Destroy(DestroyMode.Vanish);
                for(int i=0;i<6;i++)GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MineableSteel")),cell+new IntVec3(4,0,i-3),map);
                ai.SetAutomation(false,true);start=Find.TickManager.TicksGame;phase=1;
                Log.Message("[EquipmentChain] START: original six, peaceful isolated fixture, fueled bench/ore setup; no steel grant, forced jobs or post-start changes; base disabled.");
            }
            catch(Exception e){phase=99;Log.Error("[EquipmentChain] FAIL: "+e);}
        }
    }
}
