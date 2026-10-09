using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class ProgressionChecks : MapComponent
    {
        private bool started,done;
        private static bool reloading;
        private List<RoomProject> planned;
        private ConstructionTask wallUpgrade,shelfUpgrade;
        private List<ManagedFoodBill> nativeBills;
        private int start;
        private ThingDef craftedDef;
        private Pawn crafter;
        private HashSet<int> oldHelmets=new HashSet<int>();
        public ProgressionChecks(Map map):base(map){}
        private void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        private void Pass(string message)=>Log.Message("[ProgressionTests] PASS: "+message);
        private Thing Item(string name)
        {
            var def=DefDatabase<ThingDef>.GetNamed(name);var t=ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.Steel:null);
            t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Colony);return t;
        }
        private Thing Spawn(string name,IntVec3 pos,int count=1)
        {var t=Item(name);t.stackCount=count;if(t.def.CanHaveFaction)t.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(t,pos,map);return t;}
        public override void MapComponentUpdate()
        {
            if(done || !GenCommandLine.CommandLineArgPassed("autonomousrimprogressiontest"))return;
            foreach(var w in Find.WindowStack.Windows.Where(w=>w.forcePause).ToList())w.Close(false);
            Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
        }
        public override void MapComponentTick()
        {
            if(done || !GenCommandLine.CommandLineArgPassed("autonomousrimprogressiontest") || Find.TickManager.TicksGame<300)return;
            try
            {
                if(reloading)
                {
                    var component=map.GetComponent<AutonomousRimMapComponent>();
                    Check(component.Strategy.EquipmentStatus.Contains("Etapa"),"Equipment metrics lost during save/load.");
                    Check(component.BaseProjects.Any(p=>p.Crop?.defName=="Plant_Cotton"),"Agriculture plan lost during save/load.");
                    Check(component.BaseProjects.SelectMany(p=>p.Shell).Any(t=>t.UpgradeMaterial!=null && t.Complete(map)),"Stone wall upgrade lost during save/load.");
                    var restored=(List<ManagedFoodBill>)AccessTools.Field(typeof(AutonomousRimMapComponent),"defenseBills").GetValue(component);
                    Check(restored.Count==1 && restored.All(b=>b.Matches),"Bill ownership/HP/quality signature lost during save/load.");
                    Pass("native save/load preserves metrics, crop plans and wall upgrade state");
                    Log.Message("[ProgressionTests] DONE");done=true;Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;return;
                }
                if(started)
                {
                    if((!wallUpgrade.Complete(map) || !shelfUpgrade.Complete(map)) && Find.TickManager.TicksGame%120==0)BaseConstructionManager.Apply(map,planned);
                    if(map.listerThings.ThingsOfDef(craftedDef).Any(t=>t.Spawned && !oldHelmets.Contains(t.thingIDNumber)) ||
                        map.mapPawns.FreeColonistsSpawned.SelectMany(p=>p.apparel.WornApparel).Any(a=>a.def==craftedDef && !oldHelmets.Contains(a.thingIDNumber)))
                    {
                        if(!wallUpgrade.Complete(map) || !shelfUpgrade.Complete(map)){Check(Find.TickManager.TicksGame-start<24000,"Native wall/shelf construction timeout.");return;}
                        BaseConstructionManager.Apply(map,planned); // Final review configures newly built storage.
                        var builtShelf=shelfUpgrade.Position.GetThingList(map).OfType<Building_Storage>().First();
                        Check(builtShelf.def.building.maxItemsInCell==3 && builtShelf.GetStoreSettings().filter.Allows(ThingDefOf.Steel) &&
                            !builtShelf.GetStoreSettings().filter.Allows(DefDatabase<ThingDef>.GetNamed("MealSimple")),"Built shelf capacity/filter incorrect.");
                        Pass("native crafting produced a steel helmet; native wall replacement and shelf construction/capacity/filter passed");
                        var component=map.GetComponent<AutonomousRimMapComponent>();component.Strategy.EquipmentStatus=LoadoutProgression.Analyze(map,DefenseProductionPlan.Stage(map)).Status;
                        AccessTools.Field(typeof(AutonomousRimMapComponent),"baseProjects").SetValue(component,planned);
                        var oldSignature=nativeBills[0].Signature.Split('|');
                        nativeBills[0].Signature=string.Join("|",oldSignature.Take(9).Concat(oldSignature.Skip(11))); // Simulate the previous on-disk schema.
                        AccessTools.Field(typeof(AutonomousRimMapComponent),"defenseBills").SetValue(component,nativeBills);
                        GameDataSaveLoader.SaveGame("ProgressionRoundtrip");reloading=true;GameDataSaveLoader.LoadGame("ProgressionRoundtrip");return;
                    }
                    if(Find.TickManager.TicksGame%600==0)Log.Message("[ProgressionTests] crafting: job="+crafter.CurJob?.def.defName+"; target="+crafter.CurJob?.targetA+"; work="+(crafter.CurJob?.targetB.Thing as UnfinishedThing)?.workLeft);
                    Check(Find.TickManager.TicksGame-start<24000,"Native crafting timeout: "+crafter.CurJob?.def.defName);return;
                }
                var ai=map.GetComponent<AutonomousRimMapComponent>();ai.DisableAll();
                Find.PlaySettings.useWorkPriorities=true;
                var peaceful=DefDatabase<DifficultyDef>.GetNamed("Peaceful");Find.Storyteller.difficultyDef=peaceful;Find.Storyteller.difficulty.CopyFrom(peaceful);
                foreach(var p in map.mapPawns.AllPawnsSpawned.ToList()){p.DeSpawn();Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);}
                var center=map.Center;
                foreach(var cell in new CellRect(center.x-40,center.z-40,81,81))
                {
                    foreach(var t in cell.GetThingList(map).ToList()){if(t.def.destroyable)t.Destroy(DestroyMode.Vanish);else t.DeSpawn();}
                    map.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);map.roofGrid.SetRoof(cell,null);map.fogGrid.Unfog(cell);
                }
                foreach(var z in map.zoneManager.AllZones.ToList())map.zoneManager.DeregisterZone(z);
                var pawns=new List<Pawn>();
                for(int i=0;i<5;i++)
                {
                    Pawn p;
                    do{p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,forceGenerateNewPawn:true,canGeneratePawnRelations:false));}
                    while(p.ageTracker.AgeBiologicalYears<18 || p.health.hediffSet.hediffs.Count>0 || p.skills.skills.Any(s=>s.TotallyDisabled) ||
                        DefDatabase<WorkTypeDef>.AllDefsListForReading.Any(p.WorkTypeIsDisabled));
                    foreach(var trait in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(trait);
                    foreach(var skill in p.skills.skills)skill.Level=20;
                    foreach(var a in p.apparel.WornApparel.ToList())p.apparel.Remove(a);
                    foreach(var e in p.equipment.AllEquipmentListForReading.ToList())p.equipment.Remove(e);
                    GenSpawn.Spawn(p,center+new IntVec3(i,0,0),map);p.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
                    p.needs.food.CurLevel=1;p.needs.rest.CurLevel=1;p.needs.joy.CurLevel=1;p.needs.mood.CurLevel=1;pawns.Add(p);
                }
                foreach(string name in new[]{"ComplexFurniture","ComplexClothing","Smithing","LongBlades","PlateArmor","Machining","FlakArmor","GasOperation","PrecisionRifling","Stonecutting"})
                    Find.ResearchManager.FinishProject(DefDatabase<ResearchProjectDef>.GetNamed(name),false,null,false);
                Check(DefenseProductionPlan.Stage(map)==0,"Research alone completed equipment checkpoint.");
                Check(!DefenseProductionPlan.Research(map).Contains("PrecisionRifling"),"Offensive research skipped the equipped checkpoint.");
                var empty=LoadoutProgression.Analyze(map,0);Check(empty.Craft.Values.Sum()>0 && empty.Materials.Count>0 && empty.Ready==0,"No measurable equipment/material demand.");
                foreach(var p in pawns)foreach(string name in LoadoutProgression.Set(p,0))
                {var item=Item(name);if(item is Apparel a)p.apparel.Wear(a);else p.equipment.AddEquipment((ThingWithComps)item);}
                Check(DefenseProductionPlan.Stage(map)==1,"Full starter sets did not release the industrial checkpoint.");
                Check(DefenseProductionPlan.Research(map).Contains("GasOperation"),"Heavy SMG stage absent.");
                var ranged=pawns.First(p=>!DefenseProductionPlan.Melee(p));ranged.equipment.Remove(ranged.equipment.Primary);
                ranged.equipment.AddEquipment((ThingWithComps)Item("Gun_ChargeRifle"));
                Check(LoadoutProgression.Meets(ranged,ranged.equipment.Primary,"Gun_HeavySMG") && LoadoutProgression.Meets(ranged,ranged.equipment.Primary,"Gun_AssaultRifle"),"Better ranged weapon would be downgraded.");
                foreach(var p in pawns)foreach(string name in LoadoutProgression.Set(p,2))
                {
                    if(p.apparel.WornApparel.Any(a=>LoadoutProgression.Meets(p,a,name)) || LoadoutProgression.Meets(p,p.equipment.Primary,name))continue;
                    var item=Item(name);if(item is Apparel a)p.apparel.Wear(a);else{p.equipment.Remove(p.equipment.Primary);p.equipment.AddEquipment((ThingWithComps)item);}
                }
                Check(DefenseProductionPlan.Stage(map)==3,"Industrial equipment did not release the spacer checkpoint.");
                var late=LoadoutProgression.Analyze(map,3);
                Check(late.Materials.ContainsKey("ComponentSpacer") && late.Materials.ContainsKey("Plasteel") && late.Materials.ContainsKey("Gold") && late.Craft.ContainsKey("Apparel_PowerArmor"),"Spacer checkpoint lacks advanced component precursors/plasteel/gold/armor demand.");
                Check(DefenseProductionPlan.Research(map).Contains("ChargedShot"),"Charge rifle research absent.");
                var unstable=ColonyStateScanner.Scan(map);unstable.EstimatedFoodDays=.5f;
                var strategy=new StrategicPlan();StrategicPlanner.Evaluate(map,unstable,new List<RoomProject>(),strategy,false);
                Check(!strategy.Route.Any(r=>r.defName=="PoweredArmor" || r.defName=="ChargedShot"),"Spacer research was rushed during shortage/unstable base.");
                var protectedArmor=pawns[0].apparel.WornApparel.First(a=>a.def.defName=="Apparel_AdvancedHelmet");
                pawns[0].outfits.forcedHandler.SetForced(protectedArmor,true);
                var blocked=LoadoutProgression.Analyze(map,3);
                Check(blocked.Craft["Apparel_PowerArmorHelmet"]<late.Craft["Apparel_PowerArmorHelmet"] && blocked.Blockers.Any(b=>b.Contains("roupa manual")),"Forced clothing caused wasteful repeated crafting.");
                pawns[0].outfits.forcedHandler.SetForced(protectedArmor,false);
                Pass("research alone cannot finish checkpoint; worn sets release next tier; materials counted; superior gun retained");
                var tailor=(Building_WorkTable)Spawn("HandTailoringBench",center+new IntVec3(-10,0,0));
                var state=ColonyStateScanner.Scan(map);state.HostilePawnCount=0;state.EstimatedFoodDays=8;state.OutdoorTemperature=-5;
                var clothes=new List<ManagedFoodBill>();FoodManager.Apply(map,state,new List<Pawn>(),clothes);
                foreach(string name in new[]{"Apparel_Parka","Apparel_Tuque"})
                    Check(clothes.Any(b=>b.Bill.recipe.products.Any(p=>p.thingDef.defName==name) && b.Bill.targetCount==5 && b.Bill.includeEquipped),"Missing population-based winter reserve: "+name);
                Pass("winter reserve: 1 parka and 1 tuque per colonist, tainted/worn-out clothing excluded");
                var protectedBill=clothes.First();var originalRange=protectedBill.Bill.hpRange;
                protectedBill.Bill.hpRange=new FloatRange(.75f,1);
                Check(!protectedBill.Matches,"Manual HP filter edit was not recognized as bill ownership override.");
                protectedBill.Bill.hpRange=originalRange;
                FoodManager.RemoveOwnedBills(clothes);
                var storage=new RoomProject{Kind="Estoque",Origin=center+new IntVec3(-20,0,-20),InteriorSize=7,Completed=true,RequiresRoof=false};storage.StorageCells=storage.Interior.Cells.ToList();
                var spotDef=DefDatabase<ThingDef>.GetNamed("CraftingSpot");
                Check(spotDef.hasInteractionCell,"Crafting spot interaction fixture invalid.");
                var interaction=storage.Interior.Cells.First();
                var spot=(Building)Spawn("CraftingSpot",interaction-spotDef.interactionCellOffset);
                foreach(var cell in storage.Interior.Cells)Spawn("PowerConduit",cell);
                Check(spot.Spawned,"Conduit fixture removed the workstation.");
                var plannedSpot=new ConstructionTask{Def=spotDef,Position=storage.Interior.CenterCell+new IntVec3(1,0,1)};
                storage.Furniture.Add(plannedSpot);
                var projects=new List<RoomProject>{storage};
                for(int i=0;i<70;i++)Spawn("Steel",center+new IntVec3(-35+i%10,0,20+i/10));
                StoragePolicy.ManageFoodStorage(map,state,projects);
                Check(storage.Furniture.Count(t=>t.Def.defName=="ShelfSmall")==2,"Storage upgrade must queue only two shelves at a time under capacity pressure.");
                Check(storage.Furniture.Where(t=>t.Def.defName=="ShelfSmall").All(t=>t.Position!=spot.InteractionCell &&
                    t.Position!=plannedSpot.Position+spotDef.interactionCellOffset),"Shelf plan blocks native or planned workstation interaction.");
                StoragePolicy.ManageFoodStorage(map,state,projects);
                Check(storage.Furniture.Count(t=>t.Def.defName=="ShelfSmall")==2,"Unfinished storage upgrade duplicated shelves.");
                var blockedShelf=storage.Furniture.First(t=>t.Def.defName=="ShelfSmall");blockedShelf.Position=spot.InteractionCell;
                var cancelledShelf=new ConstructionTask{Def=blockedShelf.Def,Position=spot.InteractionCell,CancelledByPlayer=true};storage.Furniture.Add(cancelledShelf);
                StoragePolicy.ManageFoodStorage(map,state,projects);
                Check(blockedShelf.Position!=spot.InteractionCell && cancelledShelf.Position==spot.InteractionCell && cancelledShelf.CancelledByPlayer,
                    "Unfinished room did not repair old unissued shelf plan or moved a player-cancelled plan.");
                storage.Furniture.Remove(cancelledShelf);
                storage.Completed=true;AgriculturePlanner.Add(map,projects);
                Check(new[]{"Plant_Rice","Plant_Cotton","Plant_Healroot"}.All(name=>projects.Any(p=>p.Crop?.defName==name)),"Food/cotton/medicine crop plan missing.");
                Check(projects.Where(p=>p.Crop!=null).SelectMany(p=>p.StorageCells).Distinct().Count()==projects.Where(p=>p.Crop!=null).Sum(p=>p.StorageCells.Count),"Agriculture plots overlap.");
                Pass("capacity-driven shelves, bounded batches/no duplication; rice/cotton/medicine planned without overlapping plots");
                var zone=new Zone_Growing(map.zoneManager);map.zoneManager.RegisterZone(zone);zone.SetPlantDefToGrow(DefDatabase<ThingDef>.GetNamed("Plant_Rice"));
                var growCell=center+new IntVec3(15,0,0);zone.AddCell(growCell);
                var plant=(Plant)Spawn("Plant_Rice",growCell);plant.Growth=1;
                state.EstimatedFoodDays=state.TargetFoodDays+5;
                var work=new List<WorkPriorityChange>();WorkPriorityManager.Apply(map,state,work);
                var farmer=pawns.FirstOrDefault(p=>WorkPriorityManager.RawPriority(p,WorkTypeDefOf.Growing)==1);
                Check(farmer!=null,"Sufficient food disabled the continuous farmer.");
                plant.Destroy(DestroyMode.Vanish);zone.allowSow=false;WorkPriorityManager.Apply(map,state,work);
                Check(WorkPriorityManager.RawPriority(farmer,WorkTypeDefOf.Hauling)==1,"Idle farmer did not switch to hauling.");
                WorkPriorityManager.Restore(work);Pass("farmer continues with surplus food and switches to hauling when fields have no work");
                var cutter=(Building_WorkTable)Spawn("TableStonecutter",center+new IntVec3(-10,0,8));
                var stoneBills=new List<ManagedFoodBill>();StoneProductionManager.Apply(map,state,projects,stoneBills);
                Check(stoneBills.Any(b=>!b.Bill.suspended && b.Bill.targetCount>=100),"Stone block reserve bill absent.");
                state.EstimatedFoodDays=.5f;StoneProductionManager.Apply(map,state,projects,stoneBills);
                Check(stoneBills.All(b=>b.Bill.suspended),"Stone production displaced survival during shortage.");
                FoodManager.RemoveOwnedBills(stoneBills);state.EstimatedFoodDays=8;
                var social=new RoomProject{Kind="Refeitório e recreação",LayoutSlot="mod:0:2:3",Origin=center+new IntVec3(15,0,10),InteriorSize=11,InteriorHeight=5,Completed=true};
                projects.Add(social);ModularInteriorPlanner.Plan(map,projects);
                var finish=projects.Single(p=>p.LayoutSlot=="interior:"+social.LayoutSlot);
                Check(finish.Furniture.Any(t=>t.Def.defName=="Table3x3c") && finish.Furniture.Count(t=>t.Def.defName=="DiningChair")>=12,"Combined dining/recreation lacks useful seating.");
                Pass("stone reserve respects shortage; combined dining/recreation has large table and useful chairs");
                foreach(var p in pawns){p.jobs.EndCurrentJob(JobCondition.InterruptForced);foreach(var type in DefDatabase<WorkTypeDef>.AllDefsListForReading)if(!p.WorkTypeIsDisabled(type))p.workSettings.SetPriority(type,0);}
                var stoneDef=DefDatabase<ThingDef>.GetNamed("BlocksGranite");
                Spawn(stoneDef.defName,center+new IntVec3(10,0,5),50);
                var wall=(Building)Spawn("Wall",center+new IntVec3(12,0,8));
                // The fixture starts with an owned wooden wall; production changes it through native labor only.
                wall.Destroy(DestroyMode.Vanish);wall=(Building)ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.WoodLog);wall.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(wall,center+new IntVec3(12,0,8),map);
                wallUpgrade=new ConstructionTask{Def=ThingDefOf.Wall,Stuff=ThingDefOf.WoodLog,Position=wall.Position,Owned=true,WasCompleted=true};
                var wallRoom=new RoomProject{Kind="Melhoria de parede",Origin=wall.Position,InteriorSize=1,Completed=true,RequiresRoof=false};wallRoom.Shell.Add(wallUpgrade);
                planned=projects.Where(p=>p.Crop!=null).Concat(new[]{wallRoom}).ToList();
                pawns[1].workSettings.SetPriority(WorkTypeDefOf.Construction,1);
                StoneProductionManager.Apply(map,state,planned,new List<ManagedFoodBill>());
                Check(wallUpgrade.UpgradeMaterial==stoneDef,"Owned wall replacement was not queued.");
                Spawn("WoodLog",center+new IntVec3(10,0,4),75);
                shelfUpgrade=new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed("ShelfSmall"),Stuff=ThingDefOf.WoodLog,Position=center+new IntVec3(15,0,8),StorageKind="Estoque"};
                var shelfRoom=new RoomProject{Kind="Prateleiras",Origin=shelfUpgrade.Position,InteriorSize=1,RequiresRoof=false};shelfRoom.Furniture.Add(shelfUpgrade);planned.Add(shelfRoom);
                var smithy=(Building_WorkTable)Spawn("FueledSmithy",center+new IntVec3(0,0,8));smithy.TryGetComp<CompRefuelable>().Refuel(100);
                Spawn("Steel",center+new IntVec3(0,0,5),75);
                var recipe=smithy.def.AllRecipes.First(r=>r.products.Any(p=>p.thingDef.defName=="Apparel_SimpleHelmet"));craftedDef=recipe.products[0].thingDef;
                oldHelmets=pawns.SelectMany(p=>p.apparel.WornApparel).Cast<Thing>().Concat(map.listerThings.ThingsOfDef(craftedDef))
                    .Where(a=>a.def==craftedDef).Select(a=>a.thingIDNumber).ToHashSet();
                crafter=pawns[0];crafter.workSettings.SetPriority(DefDatabase<WorkTypeDef>.GetNamed("Smithing"),1);
                int existingHelmets=map.listerThings.ThingsOfDef(craftedDef).Where(t=>t.Spawned).Sum(t=>t.stackCount);
                nativeBills=new List<ManagedFoodBill>();FoodManager.EnsureBill(map,recipe.defName,state,nativeBills,existingHelmets+1);
                var giver=new WorkGiver_DoBill{def=DefDatabase<WorkGiverDef>.GetNamed("DoBillsMakeWeapons")};
                var job=giver.JobOnThing(pawns[0],smithy,true);Check(job!=null,"Native helmet production job unavailable.");
                Check(pawns[0].jobs.TryTakeOrderedJob(job,JobTag.Misc,false),"Native crafting job rejected.");started=true;start=Find.TickManager.TicksGame;
            }
            catch(Exception ex){done=true;Log.Error("[ProgressionTests] FAIL: "+ex);Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;}
        }
    }
}
