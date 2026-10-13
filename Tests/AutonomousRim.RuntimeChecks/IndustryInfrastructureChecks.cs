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
    public sealed class IndustryInfrastructureChecks : MapComponent
    {
        private static int phase,start;
        private Bill_Production order;
        private Building_WorkTable table;
        private string manualKey;
        private static bool hauled,crafted;
        public IndustryInfrastructureChecks(Map map):base(map){}
        public override void ExposeData()
        {Scribe_References.Look(ref order,"bufferOrder");Scribe_References.Look(ref table,"bufferTable");Scribe_Values.Look(ref manualKey,"manualBufferKey");}
        private void Check(bool pass,string text){if(!pass)throw new Exception(text);Log.Message("[IndustryInfrastructure] PASS: "+text);}
        private List<RoomProject> Projects(AutonomousRimMapComponent ai)=>(List<RoomProject>)ai.BaseProjects;
        private void Refresh(AutonomousRimMapComponent ai)=>IndustryInfrastructure.Update(map,ai,Projects(ai));
        private Building Spawn(string name,IntVec3 c,ThingDef stuff=null)
        {var b=(Building)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name),stuff);b.SetFaction(Faction.OfPlayer);return (Building)GenSpawn.Spawn(b,c,map);}
        public override void MapComponentTick()
        {
            if(phase==99 || !EliteFixtureBaseline.Ready || !GenCommandLine.CommandLineArgPassed("autonomousrimindustryinfrastructuretest"))return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();var projects=Projects(ai);
                if(phase==2)
                {
                    Check(projects.Where(p=>p.LayoutSlot?.StartsWith("industry:buffer:")==true).All(p=>p.Furniture[0].SupplyBench?.Spawned==true && p.LayoutSlot.Contains(":"+p.Furniture[0].SupplyBench.thingIDNumber+":")),"native bench reference survives save/load");
                    var manual=projects.Single(p=>p.LayoutSlot==manualKey).Furniture[0];
                    var shelf=manual.Position.GetThingList(map).OfType<Building_Storage>().Single();
                    Check(manual.SupplyManual && shelf.GetStoreSettings().Priority==StoragePriority.Low && shelf.GetStoreSettings().filter.Allows(ThingDefOf.Gold),"manual filter and relinquished ownership survive save/load");
                    var controlled=projects.Where(p=>p.LayoutSlot?.StartsWith("industry:buffer:")==true).SelectMany(p=>p.Furniture).First(t=>!t.SupplyManual);
                    var other=controlled.Position.GetThingList(map).OfType<Building_Storage>().Single();
                    Check(controlled.SupplySignature==IndustryInfrastructure.Describe(other.GetStoreSettings()),"controlled signature survives native save/load");
                    ai.SetEquipmentAutomation(false);
                    Check(other.GetStoreSettings().Priority==StoragePriority.Preferred && shelf.GetStoreSettings().Priority==StoragePriority.Low,"shutdown releases buffer emphasis while preserving player settings");
                    EliteFixtureBaseline.VerifyOriginalsPresent(map);phase=99;Log.Message("[IndustryInfrastructure] DONE");return;
                }
                if(phase==1)
                {
                    if(Find.TickManager.TicksGame%60!=0)return;
                    var buffers=projects.Where(p=>p.LayoutSlot?.StartsWith("industry:buffer:")==true && p.Furniture[0].SupplyBench==table).ToList();
                    foreach(var t in buffers.SelectMany(p=>p.Furniture).Where(t=>t.Complete(map) && t.SettingsConfigured))
                        hauled|=t.Position.GetThingList(map).Any(x=>x.def==ThingDefOf.Cloth && x.stackCount>0);
                    crafted|=order.repeatCount<30;
                    bool bench=projects.Any(p=>p.LayoutSlot=="industry:bench:FueledSmithy" && p.Furniture[0].Complete(map));
                    int elapsed=Find.TickManager.TicksGame-start;
                    if(elapsed/3000!=(elapsed-60)/3000)Log.Message("[IndustryInfrastructure] SNAPSHOT: elapsed="+elapsed+" shelves="+buffers.Count(p=>p.Furniture[0].Complete(map))+" hauled="+hauled+" crafted="+crafted+" smithy="+bench+" food="+ai.CurrentState.EstimatedFoodDays+" jobs="+string.Join(",",map.mapPawns.FreeColonistsSpawned.Select(p=>p.LabelShort+":"+p.CurJob?.def)));
                    if(buffers.Count==2 && buffers.All(p=>p.Furniture[0].SettingsConfigured && p.Furniture[0].Complete(map)) && hauled && crafted && bench)
                    {
                        Check(true,"missing smithy and two ingredient shelves built by native construction; cloth hauled into buffer and pants crafted by native jobs");
                        // The storage-policy contract needs a stable map. Natural fires
                        // can suspend buffers correctly even with emergency automation off.
                        if(EmergencyManager.LocalFire(map))
                        {
                            Refresh(ai);Check(buffers.SelectMany(p=>p.Furniture).Where(t=>!t.SupplyManual).All(t=>t.Position.GetThingList(map).OfType<Building_Storage>().Single().GetStoreSettings().Priority==StoragePriority.Preferred),"local fire correctly suspends buffers before stable policy fixture");
                            foreach(var fire in map.listerThings.AllThings.OfType<Fire>().ToList())fire.Destroy(DestroyMode.Vanish);
                            Log.Message("[IndustryInfrastructure] POLICY FIXTURE: cleared natural fires after native construction/hauling/crafting were verified.");
                        }
                        Check(!EmergencyManager.LocalFire(map),"stable storage-policy fixture has no local fire");Refresh(ai);
                        Check(buffers.All(p=>p.Furniture[0].SupplyInputs.Contains(ThingDefOf.Cloth) && !p.Furniture[0].SupplyInputs.Any(d=>d.IsMedicine || StoragePolicy.IsFood(d))),"tailoring buffers accept recipe inputs and exclude medicine/food");
                        int count=projects.Count;Refresh(ai);Refresh(ai);Check(projects.Count==count,"repeated reviews do not duplicate benches or buffers");
                        var owned=buffers[0].Furniture[0];var shelf=owned.Position.GetThingList(map).OfType<Building_Storage>().Single();
                        shelf.GetStoreSettings().Priority=StoragePriority.Low;shelf.GetStoreSettings().filter.SetAllow(ThingDefOf.Gold,true);
                        Refresh(ai);Check(owned.SupplyManual && shelf.GetStoreSettings().Priority==StoragePriority.Low && shelf.GetStoreSettings().filter.Allows(ThingDefOf.Gold),"manual priority/filter edits release control without overwriting");
                        var second=buffers[1].Furniture[0];var other=second.Position.GetThingList(map).OfType<Building_Storage>().Single();
                        float food=ai.CurrentState.EstimatedFoodDays;ai.CurrentState.EstimatedFoodDays=2;Refresh(ai);
                        Check(other.GetStoreSettings().Priority==StoragePriority.Preferred,"low food releases industrial storage emphasis");
                        ai.CurrentState.EstimatedFoodDays=food;Refresh(ai);
                        Check(other.GetStoreSettings().Priority==StoragePriority.Important,"stable demand restores only intact buffer priority; fire="+EmergencyManager.LocalFire(map)+" suspended="+ai.SecondarySuspended+" manual="+second.SupplyManual+" requested="+BillDiagnostics.Requested(order)+" matches="+ai.ManagedProduction.Any(m=>m.Bill==order&&m.Matches)+" billSuspended="+order.suspended+" mode="+order.repeatMode+" remaining="+order.repeatCount+" target="+order.targetCount);
                        ai.CurrentState.Threat.ActiveCount=1;Refresh(ai);
                        Check(other.GetStoreSettings().Priority==StoragePriority.Preferred,"immediate threat releases buffer emphasis without emergency toggle");
                        ai.CurrentState.Threat.ActiveCount=0;Refresh(ai);
                        // A native quality-range edit is also a manual edit even for raw cloth.
                        other.GetStoreSettings().filter.AllowedQualityLevels=new QualityRange(QualityCategory.Good,QualityCategory.Legendary);
                        Refresh(ai);Check(second.SupplyManual,"manual quality range is detected independently of item list");
                        // Restore fixture ownership explicitly for independent save/load/shutdown contract.
                        second.SupplyManual=false;second.SupplySignature=IndustryInfrastructure.Describe(other.GetStoreSettings());
                        manualKey=buffers[0].LayoutSlot;phase=2;
                        GameDataSaveLoader.SaveGame("IndustryInfrastructureEvidence");GameDataSaveLoader.LoadGame("IndustryInfrastructureEvidence");return;
                    }
                    if(elapsed>90000){GameDataSaveLoader.SaveGame("IndustryInfrastructureFailure");throw new Exception("native infrastructure stalled");}
                    return;
                }
                var roster=EliteFixtureBaseline.Roster(map);ai.DisableAll();
                var peaceful=DefDatabase<DifficultyDef>.GetNamed("Peaceful");Find.Storyteller.difficultyDef=peaceful;Find.Storyteller.difficulty.CopyFrom(peaceful);
                foreach(var r in DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r=>!r.IsFinished).ToList())Find.ResearchManager.FinishProject(r,false,null,false);
                foreach(var t in map.listerThings.AllThings.Where(t=>t is Building_WorkTable || t is Blueprint_Build || t is Frame || t.def.IsWeapon || t.def.defName=="Apparel_Pants").ToList())t.Destroy(DestroyMode.Vanish);
                foreach(var p in roster.Where(p=>DefenseProductionPlan.Melee(p)))p.equipment.DestroyAllEquipment();
                foreach(var p in roster)
                    foreach(var pants in p.apparel.WornApparel.Where(a=>a.def.defName=="Apparel_Pants" || a.def.defName=="Apparel_FlakPants").ToList())pants.Destroy(DestroyMode.Vanish);
                var c=GenRadial.RadialCellsAround(map.Center+new IntVec3(60,0,25),35,true).First(n=>new CellRect(n.x-1,n.z-1,17,17).Cells.All(x=>x.InBounds(map) && !x.GetThingList(map).OfType<Pawn>().Any()));
                foreach(var x in new CellRect(c.x-1,c.z-1,17,17).Cells)
                {foreach(var t in x.GetThingList(map).ToList())t.Destroy(DestroyMode.Vanish);map.terrainGrid.SetTerrain(x,TerrainDefOf.Soil);map.roofGrid.SetRoof(x,null);map.fogGrid.Unfog(x);}
                var host=new RoomProject{Kind="Estoque",Origin=c,InteriorSize=13,InteriorHeight=13,Completed=true,FunctionalStorage=true,RequiresRoof=false,State=ConstructionState.Completed};
                projects.Clear();projects.Add(host);
                foreach(var x in host.Footprint.EdgeCells)
                {
                    bool door=x==c+new IntVec3(7,0,0);string name=door?"Door":"Wall";Spawn(name,x,ThingDefOf.WoodLog);
                    host.Shell.Add(new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed(name),Position=x,Stuff=ThingDefOf.WoodLog,WasCompleted=true});
                }
                foreach(var x in host.Interior.Cells)map.roofGrid.SetRoof(x,RoofDefOf.RoofConstructed);
                table=(Building_WorkTable)Spawn("HandTailoringBench",c+new IntVec3(3,0,3),ThingDefOf.WoodLog);
                for(int i=0;i<roster.Count;i++)
                {
                    var p=roster[i];p.Position=c+new IntVec3(7,0,2+i);p.jobs.EndCurrentJob(JobCondition.InterruptForced);p.drafter.Drafted=false;
                    p.needs.food.CurLevel=1;p.needs.rest.CurLevel=1;p.needs.joy.CurLevel=1;
                    foreach(var work in DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w=>!p.WorkTypeIsDisabled(w)))p.workSettings.SetPriority(work,3);
                    p.workSettings.SetPriority(i<2?WorkTypeDefOf.Construction:i<4?WorkTypeDefOf.Hauling:WorkTypeDefOf.Crafting,1);
                    p.workSettings.SetPriority(DefDatabase<WorkTypeDef>.GetNamed("Tailoring"),i>=4?1:3);
                    for(int h=0;h<24;h++)p.timetable.SetAssignment(h,TimeAssignmentDefOf.Anything);
                }
                foreach(var pair in new[]{Tuple.Create(ThingDefOf.WoodLog,1000),Tuple.Create(ThingDefOf.Steel,500),Tuple.Create(ThingDefOf.Cloth,1500)})
                {
                    int left=pair.Item2;while(left>0){var t=ThingMaker.MakeThing(pair.Item1);t.stackCount=Math.Min(left,t.def.stackLimit);left-=t.stackCount;GenPlace.TryPlaceThing(t,c+new IntVec3(10,0,10),map,ThingPlaceMode.Near);}
                }
                order=(Bill_Production)DefDatabase<RecipeDef>.GetNamed("Make_Apparel_Pants").MakeNewBill();order.repeatMode=BillRepeatModeDefOf.RepeatCount;order.repeatCount=30;
                order.ingredientFilter.SetDisallowAll();order.ingredientFilter.SetAllow(ThingDefOf.Cloth,true);table.BillStack.AddBill(order);
                ((List<ManagedFoodBill>)ai.DefenseBills).Add(new ManagedFoodBill{Bill=order,Signature=ManagedFoodBill.Describe(order)});
                map.resourceCounter.UpdateResourceCounts();ai.SetEquipmentAutomation(true);
                AccessTools.Field(typeof(AutonomousRimMapComponent),"baseAutomation").SetValue(ai,true);
                AccessTools.Field(typeof(AutonomousRimMapComponent),"equipmentAutomation").SetValue(ai,true);
                AccessTools.Field(typeof(AutonomousRimMapComponent),"plannedPawnCount").SetValue(ai,6);
                AccessTools.Field(typeof(AutonomousRimMapComponent),"plannedClimateResearch").SetValue(ai,true);
                float baselineFood=ai.CurrentState.EstimatedFoodDays;ai.CurrentState.EstimatedFoodDays=2;Refresh(ai);
                Check(projects.Count==1,"low food does not plan benches/buffers");ai.CurrentState.EstimatedFoodDays=baselineFood;
                host.State=ConstructionState.Paused;Refresh(ai);Check(projects.Count==1,"paused host is not extended");host.State=ConstructionState.Completed;
                AccessTools.Field(typeof(AutonomousRimMapComponent),"baseAutomation").SetValue(ai,false);Refresh(ai);Check(projects.Count==1,"base toggle gates new infrastructure");
                AccessTools.Field(typeof(AutonomousRimMapComponent),"baseAutomation").SetValue(ai,true);
                AccessTools.Field(typeof(AutonomousRimMapComponent),"equipmentAutomation").SetValue(ai,false);Refresh(ai);Check(projects.Count==1,"equipment toggle gates new infrastructure");
                AccessTools.Field(typeof(AutonomousRimMapComponent),"equipmentAutomation").SetValue(ai,true);
                var playerZone=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager);map.zoneManager.RegisterZone(playerZone);
                foreach(var x in host.Interior.Cells.Where(x=>x.GetThingList(map).All(t=>t.def.CanOverlapZones)))playerZone.AddCell(x);
                Refresh(ai);Check(projects.Count==1,"unowned player zone blocks additions instead of losing cells");playerZone.Delete();
                foreach(var x in host.Interior.Cells.Where(x=>x.GetEdifice(map)==null))map.terrainGrid.SetTerrain(x,DefDatabase<TerrainDef>.GetNamed("WaterShallow"));
                Refresh(ai);Check(projects.Count==1,"unsupported terrain blocks native benches and shelves");
                foreach(var x in host.Interior.Cells)map.terrainGrid.SetTerrain(x,TerrainDefOf.Soil);
                Refresh(ai);
                Check(projects.Any(p=>p.LayoutSlot=="industry:bench:FueledSmithy"),"native melee loadout demand plans missing researched smithy inside existing storage room");
                var buffersPlanned=projects.Where(p=>p.LayoutSlot?.StartsWith("industry:buffer:")==true).ToList();
                Check(buffersPlanned.Count==2,"bounded two one-cell shelves planned for active tailoring order");
                Check(buffersPlanned.SelectMany(p=>p.Furniture).All(t=>host.Interior.Contains(t.Position) && t.Position.DistanceToSquared(table.Position)<=64 && t.Position!=table.InteractionCell && t.Position.x!=host.Interior.CenterCell.x && t.Position.z!=host.Interior.CenterCell.z),"buffer placement preserves interaction cell, central corridors and eight-cell radius");
                int before=projects.Count;Refresh(ai);Check(projects.Count==before,"planned tasks prevent duplicate additions");
                var smithy=projects.First(p=>p.LayoutSlot=="industry:bench:FueledSmithy").Furniture[0];smithy.CancelledByPlayer=true;Refresh(ai);
                Check(projects.Count(p=>p.LayoutSlot=="industry:bench:FueledSmithy")==1 && smithy.CancelledByPlayer,"player cancellation is not replaced");smithy.CancelledByPlayer=false;
                start=Find.TickManager.TicksGame;phase=1;
                Log.Message("[IndustryInfrastructure] START: original six peaceful; room/resources/research setup only, native construction/hauling/crafting after start");
            }
            catch(Exception e){phase=99;Log.Error("[IndustryInfrastructure] FAIL: "+e);}
        }
    }
}
