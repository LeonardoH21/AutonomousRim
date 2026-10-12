using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class IndustrySupplyChecks : MapComponent
    {
        private static int phase,start,components,products;
        private static bool refueled;
        private static bool pausedItemVerified;
        private Bill_Production root,precursor;
        private Building_WorkTable bench,fabricator;
        private UnfinishedThing contractUnfinished;
        private bool Fuel=>GenCommandLine.CommandLineArgPassed("autonomousrimindustryfuel");
        private bool Contracts=>GenCommandLine.CommandLineArgPassed("autonomousrimindustrycontracts");
        public IndustrySupplyChecks(Map map):base(map){}
        public override void ExposeData()
        {Scribe_References.Look(ref root,"supplyRoot");Scribe_References.Look(ref precursor,"supplyPrecursor");Scribe_References.Look(ref bench,"supplyBench");Scribe_References.Look(ref fabricator,"supplyFabricator");}
        public static void Completed(Bill_Production __instance)
        {
            if(phase!=1 || !GenCommandLine.CommandLineArgPassed("autonomousrimindustrytest"))return;
            if(__instance.recipe.defName=="Make_ComponentIndustrial")components++;
            if(__instance.recipe.defName=="Make_Gun_HeavySMG" || __instance.recipe.defName=="Make_MeleeWeapon_LongSword")products++;
        }
        private void Check(bool pass,string text) {if(!pass)throw new Exception(text);Log.Message("[IndustrySupply] PASS: "+text);}
        private Building Spawn(string name,IntVec3 cell)
        {var b=(Building)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name));b.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(b,cell,map);return b;}
        private void Refresh(AutonomousRimMapComponent ai)=>ai.Industry.Update(map,ai,(List<ManagedFoodBill>)ai.IndustryBills);
        public override void MapComponentTick()
        {
            if(phase==99 || !EliteFixtureBaseline.Ready || !GenCommandLine.CommandLineArgPassed("autonomousrimindustrytest"))return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(phase==3)
                {
                    // Electrical networks initialize in normal ticks after the fixture setup.
                    if(bench.TryGetComp<CompPowerTrader>()?.PowerOn!=true && Find.TickManager.TicksGame-start<600)return;
                    Check(bench.TryGetComp<CompPowerTrader>()?.PowerOn==true,"native electrical network is ready for unfinished-work coordination");
                    var creator=contractUnfinished.Creator;
                    creator.jobs.EndCurrentJob(JobCondition.InterruptForced,startNewJob:false);
                    var diagnostic=BillDiagnostics.Inspect(map,bench,root);
                    Check(diagnostic.Code=="ready","paused creator outside the first three workers receives a ready diagnostic: "+diagnostic.Code+" "+diagnostic.Detail);
                    ai.Coordination.Refresh(map,ai.WorkChanges,true);
                    Check(ai.Coordination.Equipment.Any(d=>d.Bill==root && d.ReadyPawns.Contains(creator) && d.ReadyPawns.All(p=>p==creator) && d.Materials.Count==0),"coordination retains creator of unfinished equipment without reserving a second batch");
                    ((Bill_ProductionWithUft)root).SetBoundUft(null);contractUnfinished.Destroy(DestroyMode.Vanish);
                    ai.SetEquipmentAutomation(false);Check(ai.Industry.Targets.Count==0,"disabled equipment clears derived supply plan");
                    phase=99;Log.Message("[IndustrySupply] DONE");return;
                }
                if(phase==2)
                {
                    Check(Fuel || ai.IndustryBills.Any(m=>m.Bill==precursor && m.Matches),"native industry ownership survives save/load");
                    EliteFixtureBaseline.VerifyOriginalsPresent(map);
                    if(!Fuel)
                    {
                        precursor.targetCount=77;
                        ai.SetEquipmentAutomation(false);
                        Check(!precursor.DeletedOrDereferenced && precursor.targetCount==77,"shutdown preserves a manually edited precursor bill");
                    }
                    phase=99;Log.Message("[IndustrySupply] DONE");return;
                }
                if(phase==1)
                {
                    refueled|=bench.TryGetComp<CompRefuelable>()?.Fuel>0;
                    if(Find.TickManager.TicksGame%60!=0)return;
                    int elapsed=Find.TickManager.TicksGame-start;
                    if(!pausedItemVerified && GenCommandLine.CommandLineArgPassed("autonomousrimindustryresume") &&
                        root is Bill_ProductionWithUft progress && progress.BoundUft?.Creator is Pawn creator && creator.CurJob?.bill!=root)
                    {
                        Check(BillDiagnostics.TryAllocate(root,creator,bench,out var nextBatch) && nextBatch.Count==0,
                            "naturally paused native equipment remains resumable without fresh ingredients");
                        pausedItemVerified=true;
                    }
                    if(elapsed/3000!=(elapsed-60)/3000)Log.Message("[IndustrySupply] SNAPSHOT: elapsed="+elapsed+" components="+components+" products="+products+" refueled="+refueled+" stage="+DefenseProductionPlan.Stage(map)+" foodDays="+ai.CurrentState.EstimatedFoodDays+" rootRequested="+BillDiagnostics.Requested(root)+" rootSuspended="+root.suspended+" diagnostic="+BillDiagnostics.Inspect(map,bench,root).Code+" power="+bench.TryGetComp<CompPowerTrader>()?.PowerOn+" jobs="+string.Join(",",map.mapPawns.FreeColonistsSpawned.Select(p=>p.LabelShort+":"+p.CurJob?.def+":"+p.CurJob?.bill?.recipe?.defName)));
                    bool equipped=map.mapPawns.FreeColonistsSpawned.Any(p=>p.equipment.Primary?.def.defName==(Fuel?"MeleeWeapon_LongSword":"Gun_HeavySMG"));
                    if(products>0 && equipped && (Fuel?refueled:components>0))
                    {
                        if(GenCommandLine.CommandLineArgPassed("autonomousrimindustryresume"))
                            Check(pausedItemVerified,"requested native pause/resume observation occurred before final delivery");
                        Check(!ai.BaseAutomation,"industry chain runs with base automation disabled");
                        Check(Fuel?refueled:components>0,Fuel?"empty fueled bench receives wood through native work":"native component recipe completes before final equipment");
                        Check(products>0 && equipped,"native final product is made and equipped");
                        precursor=ai.IndustryBills.FirstOrDefault()?.Bill;phase=2;
                        GameDataSaveLoader.SaveGame("IndustrySupplyEvidence");GameDataSaveLoader.LoadGame("IndustrySupplyEvidence");return;
                    }
                    // Allow two native day/night cycles: precursor batches, armor competition,
                    // walking to the original stockpile and recovery are part of this fixture.
                    if(elapsed>120000){GameDataSaveLoader.SaveGame("IndustrySupplyFailure");throw new Exception("Industry stalled after two days: components="+components+" products="+products+" fuel="+bench.TryGetComp<CompRefuelable>()?.Fuel+" requirements="+string.Join(",",ai.Industry.Requirements.Select(r=>r.Def+":"+r.Route)));}
                    return;
                }
                var roster=EliteFixtureBaseline.Roster(map);ai.DisableAll();
                var peaceful=DefDatabase<DifficultyDef>.GetNamed("Peaceful");Find.Storyteller.difficultyDef=peaceful;Find.Storyteller.difficulty.CopyFrom(peaceful);
                foreach(var r in DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r=>!r.IsFinished).ToList())Find.ResearchManager.FinishProject(r,false,null,false);
                foreach(var t in map.listerThings.AllThings.Where(t=>t is Building_WorkTable || t is Blueprint_Build || t is Frame ||
                    t.def==ThingDefOf.Steel || t.def==ThingDefOf.ComponentIndustrial || t.def.defName=="ComponentSpacer" || Fuel && t.def==ThingDefOf.WoodLog).ToList())t.Destroy(DestroyMode.Vanish);
                foreach(var p in roster)
                {
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced);p.drafter.Drafted=false;
                    p.needs.food.CurLevel=1;p.needs.rest.CurLevel=1;p.needs.joy.CurLevel=1;
                    foreach(var work in DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w=>!p.WorkTypeIsDisabled(w)))p.workSettings.SetPriority(work,3);
                    for(int h=0;h<24;h++)p.timetable.SetAssignment(h,TimeAssignmentDefOf.Anything);
                }
                if(Fuel)roster.First(p=>DefenseProductionPlan.Melee(p)).equipment.DestroyAllEquipment();
                else
                {
                    // Ensure the requested weapon is genuinely absent; defense retires surplus orders.
                    foreach(var p in roster.Where(p=>!DefenseProductionPlan.Melee(p)))
                    {
                        p.equipment.DestroyAllEquipment();
                        var bow=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Bow_Short"));
                        bow.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Colony);
                        p.equipment.AddEquipment(bow);
                    }
                    foreach(var t in map.listerThings.AllThings.Where(t=>t.def.IsRangedWeapon).ToList())t.Destroy(DestroyMode.Vanish);
                }
                var cell=GenRadial.RadialCellsAround(map.Center+new IntVec3(60,0,25),35,true).First(candidate=>
                    new CellRect(candidate.x-9,candidate.z-9,19,19).Cells.All(c=>c.InBounds(map) &&
                        !c.GetThingList(map).OfType<ThingWithComps>().Any(t=>t.TryGetComp<CompPower>()!=null)));
                foreach(var c in new CellRect(cell.x-9,cell.z-9,19,19).Cells)
                {foreach(var t in c.GetThingList(map).Where(t=>!(t is Pawn)).ToList())t.Destroy(DestroyMode.Vanish);map.terrainGrid.SetTerrain(c,TerrainDefOf.Soil);map.roofGrid.SetRoof(c,null);map.fogGrid.Unfog(c);}
                for(int i=0;i<roster.Count;i++)roster[i].Position=cell+new IntVec3(-4,0,i-3);
                bench=(Building_WorkTable)Spawn(Fuel?"FueledSmithy":"TableMachining",cell);
                if(!Fuel)
                {
                    fabricator=(Building_WorkTable)Spawn("FabricationBench",cell+new IntVec3(0,0,4));
                    Spawn("WoodFiredGenerator",cell+new IntVec3(-5,0,5)).TryGetComp<CompRefuelable>().Refuel(80);
                    for(int x=-5;x<=1;x++)Spawn("PowerConduit",cell+new IntVec3(x,0,3));
                    for(int z=0;z<3;z++)Spawn("PowerConduit",cell+new IntVec3(-5,0,z));
                    Check(DefenseProductionPlan.Stage(map)==1,"V4 military checkpoint requests heavy SMG tier");
                }
                int steelLeft=500;
                while(steelLeft>0){var steel=ThingMaker.MakeThing(ThingDefOf.Steel);steel.stackCount=Math.Min(steelLeft,steel.def.stackLimit);steelLeft-=steel.stackCount;GenPlace.TryPlaceThing(steel,cell+new IntVec3(-2,0,0),map,ThingPlaceMode.Near);}
                root=(Bill_Production)DefDatabase<RecipeDef>.GetNamed(Fuel?"Make_MeleeWeapon_LongSword":"Make_Gun_HeavySMG").MakeNewBill();
                root.repeatMode=BillRepeatModeDefOf.RepeatCount;root.repeatCount=1;bench.BillStack.AddBill(root);
                ((List<ManagedFoodBill>)ai.DefenseBills).Add(new ManagedFoodBill{Bill=root,Signature=ManagedFoodBill.Describe(root)});
                map.resourceCounter.UpdateResourceCounts(); // Refresh the native cache after fixture stock deletion.
                ai.SetEquipmentAutomation(true);
                if(Fuel)
                {
                    for(int i=0;i<4;i++) {var tree=(Plant)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Plant_TreeOak"));tree.Growth=1;GenSpawn.Spawn(tree,cell+new IntVec3(5,0,i-2),map);}
                    var comp=bench.TryGetComp<CompRefuelable>();comp.allowAutoRefuel=false;Refresh(ai);
                    Check(!ai.Industry.Targets.ContainsKey(ThingDefOf.WoodLog),"disabled native auto-refuel does not request wood");
                    comp.allowAutoRefuel=true;Refresh(ai);
                    Check(ai.Industry.Targets.TryGetValue(ThingDefOf.WoodLog,out int fuel) && fuel==comp.GetFuelCountToFullyRefuel(),"fuel demand follows native target and fuel count");
                }
                else
                {
                    foreach(var ore in map.listerThings.AllThings.Where(t=>t.def.building?.mineableThing==ThingDefOf.ComponentIndustrial).ToList())ore.Destroy(DestroyMode.Vanish);
                    Refresh(ai);
                    precursor=ai.IndustryBills.First().Bill;
                    Check(precursor.recipe.defName=="Make_ComponentIndustrial" && precursor.targetCount<=24,"missing components create a bounded native precursor bill");
                    Refresh(ai);Check(ai.IndustryBills.Count==1,"repeated planning does not duplicate precursor bills");
                    Check(ai.Coordination.Equipment.Any(d=>d.Bill==precursor),"component production participates in worker coordination");
                    if(Contracts)
                    {
                        float food=ai.CurrentState.EstimatedFoodDays;ai.CurrentState.EstimatedFoodDays=2;Refresh(ai);
                        Check(ai.Industry.Targets.Count==0 && precursor.suspended,"low food suspends only owned industry and clears supply forecast");
                        ai.CurrentState.EstimatedFoodDays=food;Refresh(ai);
                        Check(!precursor.suspended,"stable food resumes intact precursor without replacing the bill");
                        int threats=ai.CurrentState.Threat.ActiveCount;ai.CurrentState.Threat.ActiveCount=1;Refresh(ai);
                        Check(!ai.SecondarySuspended && ai.Industry.Targets.Count==0 && precursor.suspended,"immediate threat pauses industry even with global emergency toggle off");
                        ai.CurrentState.Threat.ActiveCount=threats;Refresh(ai);
                        ai.SetEmergencyAutomation(true);ai.Emergency.Phase=EmergencyPhase.Danger;
                        AccessTools.Method(typeof(AutonomousRimMapComponent),"ManageColony").Invoke(ai,new object[]{true});
                        Check(ai.Industry.Targets.Count==0 && precursor.suspended,"global emergency early return still clears industrial demands and suspends owned bills");
                        ai.SetEmergencyAutomation(false);ai.Emergency.Phase=EmergencyPhase.Normal;Refresh(ai);
                        var uftBill=(Bill_ProductionWithUft)root;
                        var unfinished=(UnfinishedThing)ThingMaker.MakeThing(root.recipe.unfinishedThingDef);
                        var contractCreator=map.mapPawns.FreeColonistsSpawned.Last();
                        var otherCreator=roster.First(p=>p!=contractCreator);
                        unfinished.Creator=contractCreator;unfinished.workLeft=2000;uftBill.SetBoundUft(unfinished);
                        GenSpawn.Spawn(unfinished,cell+new IntVec3(3,0,-3),map);
                        Check(BillDiagnostics.TryAllocate(root,contractCreator,bench,out var reserved) && reserved.Count==0,"unfinished item is viable for its creator without requesting fresh ingredients");
                        Check(!BillDiagnostics.TryAllocate(root,otherCreator,bench,out _) && uftBill.BoundUft==unfinished,"unfinished creator ownership is preserved by read-only analysis");
                        unfinished.SetForbidden(true,false);
                        Check(!BillDiagnostics.TryAllocate(root,contractCreator,bench,out _),"forbidden unfinished item does not become a ready manufacturing job");
                        unfinished.SetForbidden(false,false);contractUnfinished=unfinished;
                        var shared=((List<ManagedFoodBill>)ai.IndustryBills).First(m=>m.Bill==precursor);
                        ((List<ManagedFoodBill>)ai.IndustryBills).Remove(shared);((List<ManagedFoodBill>)ai.DefenseBills).Add(shared);
                        int sharedTarget=precursor.targetCount;Refresh(ai);
                        Check(ai.IndustryBills.Count==0 && precursor.targetCount==sharedTarget && ai.Industry.Requirements.Any(r=>r.Def==ThingDefOf.ComponentIndustrial && r.Route=="manufacture"),"defense-owned precursor is shared read-only without duplicate bill");
                        precursor.suspended=true;shared.Signature=ManagedFoodBill.Describe(precursor);Refresh(ai);
                        Check(precursor.suspended && ai.Industry.Requirements.Any(r=>r.Def==ThingDefOf.ComponentIndustrial && r.Route=="paused"),"defense checkpoint suspension is not overridden");
                        precursor.suspended=false;shared.Signature=ManagedFoodBill.Describe(precursor);
                        ((List<ManagedFoodBill>)ai.DefenseBills).Remove(shared);((List<ManagedFoodBill>)ai.IndustryBills).Add(shared);
                        var armorRecipe=fabricator.def.AllRecipes.First(r=>r.products?.Any(p=>p.thingDef.defName=="Apparel_PowerArmor")==true);
                        var armor=(Bill_Production)armorRecipe.MakeNewBill();armor.repeatMode=BillRepeatModeDefOf.RepeatCount;armor.repeatCount=1;fabricator.BillStack.AddBill(armor);
                        var armorOwner=new ManagedFoodBill{Bill=armor,Signature=ManagedFoodBill.Describe(armor)};((List<ManagedFoodBill>)ai.DefenseBills).Add(armorOwner);
                        Refresh(ai);
                        Check(ai.Industry.Targets.ContainsKey(DefDatabase<ThingDef>.GetNamed("ComponentSpacer")) && ai.Industry.Targets.ContainsKey(ThingDefOf.Gold) && ai.Industry.Targets.ContainsKey(DefDatabase<ThingDef>.GetNamed("Plasteel")),"advanced components expand into native industrial, gold and plasteel inputs");
                        armor.billStack.Delete(armor);((List<ManagedFoodBill>)ai.DefenseBills).Remove(armorOwner);Refresh(ai);
                        precursor.targetCount=77;Refresh(ai);
                        Check(precursor.targetCount==77 && ai.Industry.Requirements.Any(r=>r.Def==ThingDefOf.ComponentIndustrial && r.Route=="manual"),"edited precursor stays manual and blocks automatic replacement");
                        precursor.billStack.Delete(precursor);((List<ManagedFoodBill>)ai.IndustryBills).Clear();fabricator.Destroy(DestroyMode.Vanish);Refresh(ai);
                        Check(ai.Industry.Requirements.Any(r=>r.Def==ThingDefOf.ComponentIndustrial && r.Route=="commerce"),"missing producer routes input to acquisition");
                        CommercePlanner.Evaluate(map,ai.CurrentState,ai.BaseProjects,ai.Commerce);CommercePlanner.Evaluate(map,ai.CurrentState,ai.BaseProjects,ai.Commerce);
                        Check(ai.Commerce.Needs.Count(n=>n.Def==ThingDefOf.ComponentIndustrial)==1,"commercial requirement merges with existing military need without duplication");
                        start=Find.TickManager.TicksGame;phase=3;return;
                    }
                }
                new Harmony("autonomousrim.industry.proof").Patch(AccessTools.Method(typeof(Bill_Production),nameof(Bill_Production.Notify_IterationCompleted)),postfix:new HarmonyMethod(typeof(IndustrySupplyChecks),nameof(Completed)));
                ai.SetAutomation(false,true);start=Find.TickManager.TicksGame;phase=1;
                Log.Message("[IndustrySupply] START: peaceful original six, researched/bench/power/steel setup; no ingredients/products/jobs granted after start. fuelCase="+Fuel);
            }
            catch(Exception e){phase=99;Log.Error("[IndustrySupply] FAIL: "+e);}
        }
    }
}
