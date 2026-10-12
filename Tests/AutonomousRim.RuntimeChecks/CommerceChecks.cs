using System;
using System.Linq;
using System.Collections.Generic;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using AutonomousRim.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class CommerceChecks : MapComponent
    {
        private static int stage;
        private int start,beforeSteel,beforeDrug,beforeMedicine,beforeWeapon;
        private Pawn trader;
        private TradeShip ship;
        private Building_CommsConsole console;
        private bool Orbital=>GenCommandLine.CommandLineArgPassed("autonomousrimorbitaltest");
        private bool CaravanTest=>GenCommandLine.CommandLineArgPassed("autonomousrimcaravantest");
        private bool SafetyTest=>GenCommandLine.CommandLineArgPassed("autonomousrimcommercesafetytest");
        private bool caravanDeparted;
        private bool Elite=>GenCommandLine.CommandLineArgPassed("autonomousrimelitesix");
        private bool Continuous=>GenCommandLine.CommandLineArgPassed("autonomousrimcontinuoustrade");
        private bool GearTest=>GenCommandLine.CommandLineArgPassed("autonomousrimcommercegear");
        private bool CropTest=>GenCommandLine.CommandLineArgPassed("autonomousrimcommercecrop");
        private Zone_Growing commercialPlot;
        private bool harvestObserved,sowObserved;
        private bool rosterKnown;
        private bool storagePrepared;
        private bool deliveryRoundtripDone;
        private bool peacefulPrepared;
        private List<string> approvedOriginals=new List<string>();
        private static int firstTrade,firstRemaining,firstSilver;
        private static string firstBuyer;
        private static int firstSales;
        private int ExpectedResidents=>Elite?6:5;
        private IntVec3 center;
        public CommerceChecks(Map map):base(map){}
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref peacefulPrepared,"commercePeacefulPrepared");
            Scribe_Values.Look(ref deliveryRoundtripDone,"commerceDeliveryRoundtripDone");
            Scribe_Values.Look(ref stage,"commerceTestStage");Scribe_Values.Look(ref start,"commerceTestStart");
            Scribe_Values.Look(ref firstTrade,"commerceFirstTrade");Scribe_Values.Look(ref firstRemaining,"commerceFirstRemaining");Scribe_Values.Look(ref firstSilver,"commerceFirstSilver");
            Scribe_Values.Look(ref firstBuyer,"commerceFirstBuyer");Scribe_Values.Look(ref firstSales,"commerceFirstSales");
            Scribe_Values.Look(ref beforeSteel,"commerceBeforeSteel");Scribe_Values.Look(ref beforeDrug,"commerceBeforeDrug");
            Scribe_Values.Look(ref beforeMedicine,"commerceBeforeMedicine");Scribe_Values.Look(ref beforeWeapon,"commerceBeforeWeapon");
            Scribe_Values.Look(ref harvestObserved,"commerceHarvestObserved");Scribe_Values.Look(ref sowObserved,"commerceSowObserved");
            Scribe_Values.Look(ref center,"commerceTestCenter");
            Scribe_References.Look(ref trader,"commerceTestTrader");Scribe_References.Look(ref ship,"commerceTestShip");
            Scribe_References.Look(ref console,"commerceTestConsole");Scribe_References.Look(ref commercialPlot,"commerceTestPlot");
            Scribe_Collections.Look(ref approvedOriginals,"commerceOriginalIds",LookMode.Value);
            Scribe_Values.Look(ref storagePrepared,"commerceStoragePrepared");
        }
        private void PrepareCommercialStorage()
        {
            int built=0;
            foreach(var cell in new CellRect(center.x+8,center.z-3,8,6))
            {
                if(built>=12)break;
                if(!cell.InBounds(map) || !cell.Standable(map) || cell.GetEdifice(map)!=null || cell.GetZone(map) is Zone_Growing ||
                    cell.GetThingList(map).Any(t=>t.def.category==ThingCategory.Item || t is Blueprint || t is Frame))continue;
                var shelf=(Building_Storage)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ShelfSmall"),ThingDefOf.WoodLog);
                shelf.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(shelf,cell,map);
                StoragePolicy.Configure(shelf.GetStoreSettings(),"Mercadorias",true);built++;
            }
            Check(built==12,"Could not prepare controlled commercial shelves without wiping items.");
            storagePrepared=true;
            Log.Message("[CommerceTests] SETUP: twelve dedicated shelves; only furniture supplied, no production/trade/equipment results simulated.");
        }
        private void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);}
        private void Pass(string text)=>Log.Message("[CommerceTests] PASS: "+text);
        private int Stock(string name)=>CommercePlanner.Stock(map,DefDatabase<ThingDef>.GetNamed(name));
        private int OwnedStock(string name)
        {
            var def=DefDatabase<ThingDef>.GetNamed(name);
            return map.listerThings.ThingsOfDef(def).Where(t=>t.Spawned).Sum(t=>t.stackCount)+
                map.mapPawns.FreeColonistsSpawned.Sum(p=>p.inventory.innerContainer.Where(t=>t.def==def).Sum(t=>t.stackCount)+
                    (p.equipment?.Primary?.def==def?p.equipment.Primary.stackCount:0)+
                    (p.carryTracker.CarriedThing?.def==def?p.carryTracker.CarriedThing.stackCount:0));
        }
        private Thing Item(string name,int count=1)
        {
            var def=DefDatabase<ThingDef>.GetNamed(name);var thing=ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.Steel:null);
            thing.stackCount=count;thing.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Colony);return thing;
        }
        private void Ground(string name,int count)
        {
            var def=DefDatabase<ThingDef>.GetNamed(name);
            while(count>0)
            {
                var cell=GenRadial.RadialCellsAround(center,12,true).First(c=>c.InBounds(map) && c.Standable(map) && !c.GetThingList(map).Any());
                int batch=Math.Min(count,def.stackLimit);var item=Item(name,batch);GenSpawn.Spawn(item,cell,map);item.SetForbidden(false,false);
                map.areaManager.Home[cell]=true;count-=batch;
            }
        }
        private void Goods(string name,int count)
        {
            var def=DefDatabase<ThingDef>.GetNamed(name);
            while(count>0){int batch=Math.Min(count,def.stackLimit);Check((Orbital?ship.GetDirectlyHeldThings():trader.inventory.innerContainer).TryAdd(Item(name,batch)),"Trader stock insertion failed.");count-=batch;}
        }
        private void Setup()
        {
            var ai=map.GetComponent<AutonomousRimMapComponent>();ai.DisableAll();center=map.Center;
            var originals=Elite?EliteFixtureBaseline.Roster(map):new List<Pawn>();
            approvedOriginals=originals.Select(p=>p.GetUniqueLoadID()).ToList();rosterKnown=Elite;
            foreach(var original in originals){original.jobs.StopAll();original.carryTracker.CarriedThing?.Destroy(DestroyMode.Vanish);original.inventory.innerContainer.ClearAndDestroyContents();}
            foreach(var building in map.listerBuildings.allBuildingsColonist.Where(b=>b is Building_WorkTable || b.TryGetComp<CompPower>()!=null).ToList())building.Destroy(DestroyMode.Vanish);
            foreach(var zone in map.zoneManager.AllZones.ToList())zone.Delete();
            if(!Elite){var peaceful=DefDatabase<DifficultyDef>.GetNamed("Peaceful");Find.Storyteller.difficultyDef=peaceful;Find.Storyteller.difficulty.CopyFrom(peaceful);map.Biome.constantOutdoorTemperature=21;}
            foreach(var pawn in map.mapPawns.AllPawnsSpawned.ToList()){pawn.DeSpawn();if(!originals.Contains(pawn))Find.WorldPawns.PassToWorld(pawn,PawnDiscardDecideMode.KeepForever);}
            foreach(var t in map.listerThings.AllThings.Where(t=>t.def.category==ThingCategory.Item || t is Fire || t is Hive || !(t is Pawn) && t is IAttackTarget && t.HostileTo(Faction.OfPlayer)).ToList())t.Destroy(DestroyMode.Vanish);
            foreach(var cell in new CellRect(center.x-20,center.z-20,41,41))
            {
                foreach(var t in cell.GetThingList(map).ToList()){if(t.def.destroyable)t.Destroy(DestroyMode.Vanish);else t.DeSpawn();}
                map.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);map.roofGrid.SetRoof(cell,null);map.fogGrid.Unfog(cell);map.areaManager.Home[cell]=true;
            }
            for(int i=0;i<ExpectedResidents;i++)
            {
                if(Elite){var original=originals[i];GenSpawn.Spawn(original,center+new IntVec3(i*2,0,-8),map);original.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
                    original.needs.food.CurLevel=1;original.needs.rest.CurLevel=1;original.needs.joy.CurLevel=1;original.needs.mood.CurLevel=1;
                    original.playerSettings.AreaRestrictionInPawnCurrentMap=null;continue;}
                Pawn p;do{p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,forceGenerateNewPawn:true,canGeneratePawnRelations:false));}
                while(p.health.hediffSet.hediffs.Count>0 || p.skills.skills.Any(s=>s.TotallyDisabled) || DefDatabase<WorkTypeDef>.AllDefsListForReading.Any(p.WorkTypeIsDisabled));
                foreach(var trait in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(trait);
                foreach(var skill in p.skills.skills)skill.Level=20;
                GenSpawn.Spawn(p,center+new IntVec3(i*2,0,-8),map);
                p.needs.food.CurLevel=1;p.needs.rest.CurLevel=1;p.needs.joy.CurLevel=1;p.needs.mood.CurLevel=1;
                p.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
            }
            var faction=Find.FactionManager.AllFactions.First(f=>!f.IsPlayer && !f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction);
            trader=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,faction,forceGenerateNewPawn:true,canGeneratePawnRelations:false));
            trader.trader=new Pawn_TraderTracker(trader);
            trader.trader.traderKind=DefDatabase<TraderKindDef>.AllDefsListForReading.First(t=>t.tradeCurrency==TradeCurrency.Silver && t.WillTrade(ThingDefOf.Steel) && t.WillTrade(DefDatabase<ThingDef>.GetNamed("SmokeleafJoint")));
            foreach(var item in trader.inventory.innerContainer.ToList())trader.inventory.innerContainer.Remove(item);
            trader.mindState.wantsToTradeWithColony=true;GenSpawn.Spawn(trader,center+new IntVec3(8,0,8),map);
            trader.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Wait,90000),JobTag.Misc);
            if(Orbital)
            {
                trader.DeSpawn();Find.WorldPawns.PassToWorld(trader,PawnDiscardDecideMode.KeepForever);
                ship=new TradeShip(DefDatabase<TraderKindDef>.AllDefsListForReading.First(t=>t.orbital && t.tradeCurrency==TradeCurrency.Silver && t.WillTrade(ThingDefOf.Steel) && t.WillTrade(DefDatabase<ThingDef>.GetNamed("SmokeleafJoint"))),faction);
                map.passingShipManager.AddShip(ship);
                ship.ticksUntilDeparture=120000;
                Building PowerBuilding(string name,IntVec3 cell)
                {
                    var b=(Building)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name));b.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(b,cell,map);return b;
                }
                console=(Building_CommsConsole)PowerBuilding("CommsConsole",center+new IntVec3(-8,0,4));
                PowerBuilding("OrbitalTradeBeacon",center+new IntVec3(0,0,5));
                PowerBuilding("WoodFiredGenerator",center+new IntVec3(-4,0,8)).TryGetComp<CompRefuelable>().Refuel(75);
                for(int x=-8;x<=0;x++)PowerBuilding("PowerConduit",center+new IntVec3(x,0,5));
                // A plant already transmits power: spawning a conduit inside it can wipe it.
                for(int z=6;z<=7;z++)PowerBuilding("PowerConduit",center+new IntVec3(-4,0,z));
                var stockpile=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager);
                map.zoneManager.RegisterZone(stockpile);stockpile.GetStoreSettings().filter.SetDisallowAll();
                foreach(var name in new[]{"Silver","SmokeleafJoint","SmokeleafLeaves"})stockpile.GetStoreSettings().filter.SetAllow(DefDatabase<ThingDef>.GetNamed(name),true);
                stockpile.GetStoreSettings().Priority=StoragePriority.Critical;
                foreach(var cell in new CellRect(center.x-2,center.z+1,5,3).Where(c=>c.GetZone(map)==null && c.GetEdifice(map)==null))stockpile.AddCell(cell);
                var blockedShelf=(Building_Storage)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ShelfSmall"),ThingDefOf.WoodLog);
                blockedShelf.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(blockedShelf,center+new IntVec3(-6,0,3),map);
                blockedShelf.GetStoreSettings().filter.SetDisallowAll();
                blockedShelf.GetStoreSettings().filter.SetAllow(DefDatabase<ThingDef>.GetNamed("SmokeleafJoint"),true);
                blockedShelf.GetStoreSettings().Priority=StoragePriority.Critical;
                foreach(var blocks in new[]{"BlocksGranite","BlocksLimestone","BlocksSandstone"})
                {
                    // Keep preparation material valid here so native haulers do not
                    // empty the full-shelf fixture before the console powers up.
                    blockedShelf.GetStoreSettings().filter.SetAllow(DefDatabase<ThingDef>.GetNamed(blocks),true);
                    Check(GenPlace.TryPlaceThing(Item(blocks,75),blockedShelf.Position,map,ThingPlaceMode.Direct),"Could not fill the controlled shelf through native placement.");
                }
                Check(blockedShelf.GetSlotGroup().CellsList.All(c=>Building_OrbitalTradeBeacon.AllPowered(map).Any(b=>b.TradeableCells.Contains(c))) || !console.CanUseCommsNow,
                    "Full-shelf fixture must sit inside the future powered beacon coverage.");
                Log.Message("[CommerceTests] SETUP: closest orbital shelf filled with noncommercial fixture material; another covered destination remains available.");
                foreach(var pawn in map.mapPawns.FreeColonistsSpawned)pawn.workSettings.SetPriority(WorkTypeDefOf.Hauling,2);
            }
            Goods("Silver",2000);Goods("Steel",200);Goods("ComponentIndustrial",20);Goods("Cloth",100);
            Ground("Silver",500);Ground("SmokeleafJoint",Continuous?40:100);Ground("MealSimple",60);Ground("MedicineHerbal",30);Ground("Steel",10);Ground("MeleeWeapon_Knife",1);
            if(Continuous)Ground("MealSurvivalPack",200);
            if(Continuous)
            {
                if(!CropTest)Ground("SmokeleafLeaves",80);
                Find.PlaySettings.useWorkPriorities=true;
                foreach(var p in map.mapPawns.FreeColonistsSpawned)
                {
                    foreach(var work in DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w=>!p.WorkTypeIsDisabled(w)))p.workSettings.SetPriority(work,0);
                    p.workSettings.SetPriority(WorkTypeDefOf.Crafting,1);
                    if(Orbital)p.workSettings.SetPriority(WorkTypeDefOf.Hauling,2);
                    for(int h=0;h<24;h++)p.timetable.SetAssignment(h,TimeAssignmentDefOf.Anything);
                }
            }
            if(GearTest)
            {
                foreach(var research in DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r=>!r.IsFinished).ToList())Find.ResearchManager.FinishProject(research,false,null,false);
                Ground("Cloth",1800);Ground("Steel",CropTest?50:1500);Ground("WoodLog",1000);
                if(CropTest)Goods("Steel",800);
                if(CropTest)Goods("MealSurvivalPack",100);
                foreach(var name in new[]{"HandTailoringBench","FueledSmithy"})
                {
                    var def=DefDatabase<ThingDef>.GetNamed(name);var bench=(Building_WorkTable)ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.WoodLog:null);
                    bench.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(bench,center+new IntVec3(name=="HandTailoringBench"?-12:12,0,-14),map);
                    bench.TryGetComp<CompRefuelable>()?.Refuel(75);
                }
                foreach(var p in map.mapPawns.FreeColonistsSpawned)
                    foreach(var work in DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w=>!p.WorkTypeIsDisabled(w)))p.workSettings.SetPriority(work,4);
            }
            if(CaravanTest)
            {
                Ground("MealSurvivalPack",200);
                var zone=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager);map.zoneManager.RegisterZone(zone);
                zone.GetStoreSettings().filter.SetAllowAll(null);zone.GetStoreSettings().Priority=StoragePriority.Critical;
                foreach(var cell in new CellRect(center.x+2,center.z-4,10,10).Where(c=>c.GetZone(map)==null && c.GetEdifice(map)==null))zone.AddCell(cell);
                Find.PlaySettings.useWorkPriorities=true;
                foreach(var p in map.mapPawns.FreeColonistsSpawned)p.workSettings.SetPriority(WorkTypeDefOf.Hauling,1);
                var neighbors=new List<PlanetTile>();Find.WorldGrid.GetTileNeighbors(map.Tile,neighbors);
                var tile=neighbors.First(t=>!Find.WorldObjects.AllWorldObjects.Any(o=>o.Tile==t) && Find.WorldReachability.CanReach(map.Tile,t) &&
                    CaravanArrivalTimeEstimator.EstimatedTicksToArrive(map.Tile,t,null)>0 && CaravanArrivalTimeEstimator.EstimatedTicksToArrive(map.Tile,t,null)<60000);
                var settlement=(Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                settlement.SetFaction(faction);settlement.Tile=tile;Find.WorldObjects.Add(settlement);
                Check(settlement.TraderKind?.WillTrade(ThingDefOf.Steel)==true && settlement.TraderKind.WillTrade(DefDatabase<ThingDef>.GetNamed("SmokeleafJoint")),"Controlled settlement must accept steel and commercial goods.");
                _=settlement.trader.StockListForReading;
                var stock=settlement.trader.GetDirectlyHeldThings();foreach(var item in stock.ToList()){stock.Remove(item);item.Destroy(DestroyMode.Vanish);}
                trader.inventory.innerContainer.TryTransferAllToContainer(stock);
                trader.DeSpawn();Find.WorldPawns.PassToWorld(trader,PawnDiscardDecideMode.KeepForever);
            }
            var crafting=(Building_WorkTable)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("CraftingSpot"));crafting.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(crafting,center+new IntVec3(-10,0,10),map);
            if(CropTest)
            {
                // Initial mature crops shorten biological waiting only. Leaves, recipe products,
                // trades and equipped gear must all come from real native jobs afterwards.
                commercialPlot=new Zone_Growing(map.zoneManager);map.zoneManager.RegisterZone(commercialPlot);
                commercialPlot.SetPlantDefToGrow(DefDatabase<ThingDef>.GetNamed("Plant_Smokeleaf"));
                foreach(var cell in new CellRect(center.x-6,center.z-7,5,5))
                {
                    foreach(var t in cell.GetThingList(map).ToList())t.Destroy(DestroyMode.Vanish);
                    commercialPlot.AddCell(cell);var plant=(Plant)ThingMaker.MakeThing(commercialPlot.GetPlantDefToGrow());plant.Growth=1;GenSpawn.Spawn(plant,cell,map);
                }
                foreach(var p in map.mapPawns.FreeColonistsSpawned)p.workSettings.SetPriority(WorkTypeDefOf.Growing,1);
            }
            var projects=(List<RoomProject>)HarmonyLib.AccessTools.Field(typeof(AutonomousRimMapComponent),"baseProjects").GetValue(ai);
            projects.Clear();
            if(GearTest)PrepareCommercialStorage();
            var essential=new RoomProject{Kind="Energia e climatização",Origin=center+new IntVec3(-16,0,0),InteriorSize=4,RequiresRoof=false,Priority=ConstructionPriority.High};
            essential.Furniture.Add(new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed("Battery"),Position=essential.Origin});projects.Add(essential);
            beforeSteel=Stock("Steel");beforeDrug=Stock("SmokeleafJoint");beforeMedicine=OwnedStock("MedicineHerbal");beforeWeapon=OwnedStock("MeleeWeapon_Knife");
            ai.SetAutomation(false,GearTest);
            if(GearTest)ai.SetEquipmentAutomation(true);
            if(Orbital){start=Find.TickManager.TicksGame;stage=4;return;}
            if(SafetyTest)SafetySetup(ai);
            ai.SetCommerceAutomation(true);
            if(CaravanTest)
            {
                ai.Commerce.ExpeditionsEnabled=true;ai.Commerce.LastDemandTick=-120000;ai.Commerce.NextExpedition=0;
                CommerceCaravanManager.Apply(map,ai.CurrentState,ai.Commerce,true);
                Check(ai.Commerce.ExpeditionPawns.Count==2 && ai.Commerce.FormationLord?.LordJob is LordJob_FormAndSendCaravan,
                    "Native caravan formation did not start: "+ai.Commerce.CaravanStatus);
                Check(map.mapPawns.FreeColonistsSpawned.Count(p=>!ai.Commerce.ExpeditionPawns.Contains(p))>=3,"Too few defenders retained.");
                Pass("native formation reserves two couriers and retains three defenders and medicine/food reserves");
                start=Find.TickManager.TicksGame;stage=1;return;
            }
            Check((GearTest || ai.Commerce.Needs.Count>0) && ai.Commerce.JobId!=null,"Idle commerce did not issue a native trader job: "+ai.Commerce.Status);
            Check(ai.Commerce.Bills.Count>0 && ai.Commerce.Target>0 && ai.Commerce.Target<=100,"First commercial production batch missing or unbounded.");
            Pass("deficit plan, bounded production bill and native negotiator dispatch from idle state");
            start=Find.TickManager.TicksGame;stage=1;
        }
        private void SafetySetup(AutonomousRimMapComponent ai)
        {
            var manualNegotiator=map.mapPawns.FreeColonistsSpawned.First();
            var needs=ai.Commerce.Needs.ToList();ai.Commerce.Needs.Clear();
            int joints=Stock("SmokeleafJoint"),silver=OwnedStock("Silver");
            var cash=trader.inventory.innerContainer.Where(t=>t.def==ThingDefOf.Silver).Select(t=>Tuple.Create(t,t.stackCount)).ToList();
            try
            {
                foreach(var stack in cash)stack.Item1.stackCount=0;
                CommerceManager.ExecuteTransaction(map,manualNegotiator,ai.Commerce,trader);
                Check(Stock("SmokeleafJoint")==joints && OwnedStock("Silver")==silver && ai.Commerce.LastTrade==0,"Buyer without silver consumed sale-only goods.");
                Pass("native sale-only buyer without cash preserves stock and records no transaction");
            }
            finally{foreach(var stack in cash)stack.Item1.stackCount=stack.Item2;}
            var kind=trader.trader.traderKind;
            var reject=DefDatabase<TraderKindDef>.AllDefsListForReading.FirstOrDefault(k=>!k.orbital && k.tradeCurrency==TradeCurrency.Silver && !k.WillTrade(DefDatabase<ThingDef>.GetNamed("SmokeleafJoint")));
            Check(reject!=null,"No native trader kind rejecting smokeleaf available.");
            try
            {
                trader.trader.traderKind=reject;
                CommerceManager.ExecuteTransaction(map,manualNegotiator,ai.Commerce,trader);
                Check(Stock("SmokeleafJoint")==joints && OwnedStock("Silver")==silver && ai.Commerce.LastTrade==0,"Trader rejecting smokeleaf changed commercial goods.");
                Pass("native incompatible trader accepts no smokeleaf sale and preserves silver");
            }
            finally{trader.trader.traderKind=kind;ai.Commerce.Needs=needs;}
            TradeSession.SetupWith(trader,manualNegotiator,false);
            var manualDeal=TradeSession.deal;
            try
            {
                ai.SetCommerceAutomation(true);
                Check(TradeSession.Active && TradeSession.deal==manualDeal && TradeSession.playerNegotiator==manualNegotiator &&
                    ai.Commerce.JobId==null && ai.Commerce.LastTrade==0 && Stock("SmokeleafJoint")==beforeDrug && Stock("Steel")==beforeSteel,
                    "Automatic commerce replaced/executed an active manual trade session.");
                ai.SetCommerceAutomation(false);
                Check(TradeSession.Active && TradeSession.deal==manualDeal,"Disable closed the player's trade session.");
                Pass("native manual TradeSession survives enable/disable with unchanged goods and no automatic transaction");
            }
            finally {TradeSession.Close();TradeSession.deal=null;TradeSession.playerNegotiator=null;}
            ai.SetCommerceAutomation(true);
            float foodDays=ai.CurrentState.EstimatedFoodDays;
            int cashGoal=ai.Commerce.SilverTarget,jointGoal=ai.Commerce.JointReserve;
            try
            {
                ai.CurrentState.EstimatedFoodDays=.5f;
                CommercePlanner.Production(map,ai.CurrentState,new List<RoomProject>(),ai.Commerce,false);
                Check(ai.Commerce.Bills.Any(b=>b.Matches) && ai.Commerce.Bills.Where(b=>b.Matches).All(b=>b.Bill.suspended),
                    "Food shortage did not pause owned commercial bills.");
                Check(ai.Commerce.SilverTarget==cashGoal && ai.Commerce.JointReserve==jointGoal && ai.Commerce.Target>=jointGoal,
                    "Food shortage erased commercial goals.");
                Pass("food shortage pauses owned native bills while preserving cash/product goals");
            }
            finally {ai.CurrentState.EstimatedFoodDays=foodDays;}
            ai.SetCommerceAutomation(false);
            ai.Commerce.NextAttempt=0;ai.SetCommerceAutomation(true);
            Check(ai.Commerce.JobId!=null,"Owned negotiation was not dispatched before fire suspension.");
            // This fire is an explicit setup fixture, removed before the ordinary roundtrip.
            var fire=ThingMaker.MakeThing(ThingDefOf.Fire);
            GenSpawn.Spawn(fire,center+new IntVec3(0,0,-7),map);
            try
            {
                Check(EmergencyManager.LocalFire(map),"Safety fire fixture was not a nearby threat.");
                ai.SetCommerceAutomation(true);
                Check(ai.Commerce.JobId==null && ai.Commerce.LastTrade==0 && Stock("SmokeleafJoint")==beforeDrug && Stock("Steel")==beforeSteel,
                    "Commerce dispatched/executed during a nearby fire.");
                ai.SetCommerceAutomation(false);
                Pass("native nearby fire suspends an owned negotiator and prevents transactions without changing stock");
            }
            finally {fire.Destroy(DestroyMode.Vanish);}
            ai.Commerce.NextAttempt=0;ai.SetCommerceAutomation(true);
            Check(ai.Commerce.JobId!=null && ai.Commerce.Negotiator!=null,"Owned negotiator setup missing for manual takeover.");
            var pawn=ai.Commerce.Negotiator;
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Wait,3000),JobTag.Misc);
            var manualJob=pawn.CurJob;
            Check(manualJob?.playerForced==true && manualJob.GetUniqueLoadID()!=ai.Commerce.JobId,"Native manual order did not replace the owned trade job.");
            ai.SetCommerceAutomation(false);
            Check(pawn.CurJob==manualJob && pawn.CurJob.playerForced && ai.Commerce.JobId==null,"Disable interrupted the replacement manual job.");
            Pass("native ordered job replaces automatic negotiator; disable preserves player control");
            ai.Commerce.NextAttempt=0;
        }
        public override void MapComponentTick()
        {
            if(!EliteFixtureBaseline.Ready)return;
            if(stage==99 || !(GenCommandLine.CommandLineArgPassed("autonomousrimcommercetest") || Orbital || CaravanTest || SafetyTest) || Find.TickManager.TicksGame<300)return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(!peacefulPrepared && GenCommandLine.CommandLineArgPassed("autonomousrimcommercepeaceful"))
                {
                    foreach(var threat in map.mapPawns.AllPawnsSpawned.Where(p=>p.HostileTo(Faction.OfPlayer)).ToList())threat.DeSpawn();
                    peacefulPrepared=true;
                    Log.Message("[CommerceTests] SETUP: economic fixture without random hostile incidents; native manufacture/trade/equip unchanged.");
                }
                if(Elite && stage>0 && !rosterKnown)
                {
                    var roster=EliteFixtureBaseline.Roster(map);
                    Check(approvedOriginals!=null && approvedOriginals.Count==6 && approvedOriginals.All(id=>roster.Any(p=>p.GetUniqueLoadID()==id)),"Original fixture IDs changed after load.");
                    rosterKnown=true;
                }
                if(stage>0 && !storagePrepared && GenCommandLine.CommandLineArgPassed("autonomousrimcommerceaddstorage"))PrepareCommercialStorage();
                if(stage==0){Setup();return;}
                if(stage==4)
                {
                    Check(Find.TickManager.TicksGame-start<3000,"Native console power network did not start.");
                    if(Find.TickManager.TicksGame-start<600)return;
                    if(!console.CanUseCommsNow)return;
                    var fullShelf=(Building_Storage)(center+new IntVec3(-6,0,3)).GetEdifice(map);
                    Check(fullShelf!=null && fullShelf.Position.GetThingList(map).Count(t=>t.def.category==ThingCategory.Item)==3 &&
                        Building_OrbitalTradeBeacon.AllPowered(map).Any(b=>b.TradeableCells.Contains(fullShelf.Position)),"Covered full-shelf fixture changed before trade: shelf="+fullShelf?.ThingID+
                        " items="+fullShelf?.Position.GetThingList(map).Count(t=>t.def.category==ThingCategory.Item)+" capacity="+fullShelf?.Position.GetMaxItemsAllowedInCell(map)+
                        " poweredBeacons="+Building_OrbitalTradeBeacon.AllPowered(map).Count()+" covered="+(fullShelf!=null && Building_OrbitalTradeBeacon.AllPowered(map).Any(b=>b.TradeableCells.Contains(fullShelf.Position))));
                    ai.SetCommerceAutomation(true);
                    Check(ai.Commerce.Bills.All(b=>b.Bill.GetSlotGroup()!=fullShelf.GetSlotGroup()),"Native commercial bill selected the full orbital shelf.");
                    Pass("full nearest orbital shelf skipped in favor of a covered destination with capacity");
                    Check(ai.Commerce.JobId!=null,"Powered orbital setup did not dispatch negotiator: "+ai.Commerce.Status);
                    Pass("native powered console/beacon and orbital negotiator dispatch");stage=1;return;
                }
                if(stage==2)
                {
                    EliteFixtureBaseline.VerifyOriginalsPresent(map);
                    Check(ai.CommerceAutomation && ai.Commerce.LastTrade>0 && ai.Commerce.History.Count>0,"Trade state lost after native save/load.");
                    Pass("native save/load preserves completed transaction, history and toggle");
                    if(Continuous)
                    {
                        Check(ai.Commerce.RefreshTicks==5000 && ai.Commerce.Product=="SmokeleafJoint","Commercial settings lost after load.");
                        Check(ai.Commerce.Target>=ai.Commerce.JointReserve,"No commercial reserve target.");
                        start=Find.TickManager.TicksGame;stage=5;return;
                    }
                    ai.SetCommerceAutomation(false);Check(ai.Commerce.Bills.Count==0 && ai.Commerce.JobId==null,"Commerce disable retained owned jobs/bills.");
                    Pass("disable removes only managed trade production and job ownership");stage=99;Log.Message("[CommerceTests] DONE");return;
                }
                if(stage==5)
                {
                    if(CropTest)
                    {
                        if(!harvestObserved && (Stock("SmokeleafLeaves")>0 || ai.Commerce.Bills.Sum(b=>ai.Progress.Get("bill:"+b.Bill.GetUniqueLoadID()).Completions)>0))
                        {harvestObserved=true;Pass("mature setup crops harvested through native work; no leaves spawned by the fixture");}
                        if(!sowObserved && map.zoneManager.AllZones.OfType<Zone_Growing>().Where(z=>z.GetPlantDefToGrow().defName=="Plant_Smokeleaf")
                            .SelectMany(z=>z.Cells).Any(c=>c.GetPlant(map)?.Growth<.2f))
                        {sowObserved=true;Pass("commercial crop plot resown through native work after harvesting");}
                    }
                    int crafted=ai.Commerce.Bills.Where(b=>b.Bill!=null).Sum(b=>ai.Progress.Get("bill:"+b.Bill.GetUniqueLoadID()).Completions);
                    int repeatSold=firstBuyer!=null && ai.Commerce.JointSales.TryGetValue(firstBuyer,out int total)?total-firstSales:0;
                    if(ai.Commerce.LastTrade>firstTrade && crafted>=20 && repeatSold>=20 && ai.Commerce.Deliveries.Count==0)
                    {
                        if(GearTest && !DefenseProductionPlan.Fighters(map).All(p=>LoadoutProgression.Equipped(p,0)))
                        {
                            if(Find.TickManager.TicksGame%3000==0)
                            {
                                var gear=LoadoutProgression.Analyze(map,0);
                                Log.Message("[CommerceTests] gear: "+gear.Status+" activeStage="+DefenseProductionPlan.Stage(map)+" missing="+
                                    string.Join(";",gear.Items.Where(i=>i.State!="Equipado").Select(i=>i.Pawn.LabelShort+":"+i.Target+":"+i.State))+" bills="+
                                    string.Join(";",ai.DefenseBills.Select(b=>b.Bill.LabelCap+":"+b.Bill.targetCount+":suspended="+b.Bill.suspended+":owned="+b.Matches)));
                            }
                            if(CropTest && Find.TickManager.TicksGame%60000==0)GameDataSaveLoader.SaveGame("CommerceCycleCheckpoint");
                            Check(Find.TickManager.TicksGame-start<(CropTest?600000:120000),"Native gear checkpoint stalled: "+LoadoutProgression.Analyze(map,0).Status);return;
                        }
                        if(CropTest)Check(harvestObserved && sowObserved,"Native harvest/replant evidence missing.");
                        Check(ai.Commerce.History.Count(h=>h.Contains("PlayerSells"))>=2,"No second native sale recorded.");
                        Check(!ai.Commerce.Bills.Any(b=>b.Bill.recipe.products.Any(p=>p.thingDef.defName=="Flake" || p.thingDef.defName=="Yayo")),"Other drugs were scheduled.");
                        EliteFixtureBaseline.VerifyOriginalsPresent(map);
                        Pass("same buyer accepts newly crafted joints after save/load; native leaves consumed, second sale completed, exclusive smokeleaf reserve retained");
                        if(GearTest)Pass("all six initial sets crafted/equipped through native work while commercial production and repeated sales continue");
                        Log.Message("[CommerceTests] METRICS firstTrade="+firstTrade+" lastTrade="+ai.Commerce.LastTrade+" firstSilver="+firstSilver+" silver="+Stock("Silver")+" crafted="+crafted+" repeatSold="+repeatSold+" remainingJoints="+Stock("SmokeleafJoint"));
                        ai.SetCommerceAutomation(false);stage=99;Log.Message("[CommerceTests] DONE");return;
                    }
                    if(Find.TickManager.TicksGame%3000==0)Log.Message("[CommerceTests] repeat: "+ai.Commerce.Status+" leaves="+Stock("SmokeleafLeaves")+" joints="+Stock("SmokeleafJoint")+" lastTrade="+ai.Commerce.LastTrade+" buyers="+ai.Commerce.BuyerStatus+" blocker="+ai.Commerce.Blocker+" production="+ai.Commerce.ProductionStatus+(GearTest?" gear="+LoadoutProgression.Analyze(map,0).Ready+"/6":""));
                    if(CropTest && Find.TickManager.TicksGame%60000==0)GameDataSaveLoader.SaveGame("CommerceCycleCheckpoint");
                    Check(Find.TickManager.TicksGame-start<(CropTest?600000:GearTest?120000:60000),"Second production/sale failed: "+ai.Commerce.Status+" / "+ai.Commerce.Blocker);
                    return;
                }
                if(CaravanTest && !caravanDeparted && ai.Commerce.Expedition?.Spawned==true)
                {caravanDeparted=true;Pass("couriers packed real goods, left map and formed native world caravan");}
                bool caravanUnloaded=!CaravanTest || ai.Commerce.ExpeditionPawns.Count==0 &&
                    (Stock("Steel")>beforeSteel || Stock("ComponentIndustrial")>0) && OwnedStock("MedicineHerbal")==beforeMedicine;
                if(ai.Commerce.LastTrade>0 && caravanUnloaded)
                {
                    if(Orbital && ai.Commerce.Deliveries.Count>0 && !deliveryRoundtripDone)
                    {
                        var pending=ai.Commerce.Deliveries.ToList();
                        CommercePlanner.Evaluate(map,ai.CurrentState,ai.BaseProjects,ai.Commerce);
                        var deficits=ai.Commerce.Needs.ToDictionary(n=>n.Def,n=>n.Count);
                        Check(pending.All(d=>d.ShipmentIds.Count>0 && ai.Commerce.Deliveries.Contains(d)),"Shipment was confirmed before its native pod landed.");
                        ai.Commerce.Deliveries.Clear();
                        try
                        {
                            CommercePlanner.Evaluate(map,ai.CurrentState,ai.BaseProjects,ai.Commerce);
                            Check(pending.Any(d=>ai.Commerce.Needs.FirstOrDefault(n=>n.Def==d.Def)?.Count>(deficits.TryGetValue(d.Def,out int count)?count:0)),
                                "Pending orbital purchase did not reduce the next purchase deficit.");
                        }
                        finally {ai.Commerce.Deliveries.AddRange(pending);CommercePlanner.Evaluate(map,ai.CurrentState,ai.BaseProjects,ai.Commerce);}
                        deliveryRoundtripDone=true;
                        Pass("identified pending pods reduce purchase deficits before arrival; save/load resumes real delivery");
                        GameDataSaveLoader.SaveGame("CommerceInTransit");GameDataSaveLoader.LoadGame("CommerceInTransit");return;
                    }
                    if(Orbital && (ai.Commerce.Deliveries.Count>0 || Stock("Silver")<250))
                    {Check(Find.TickManager.TicksGame-start<20000,"Orbital delivery never became accessible.");return;}
                    Check(Stock("SmokeleafJoint")<beforeDrug,"Commercial products not actually sold.");
                    Check(GearTest || Stock("Steel")>beforeSteel || Stock("ComponentIndustrial")>0,"Needed materials were not actually delivered.");
                    Check(OwnedStock("Silver")>=250,"Cash reserve spent below ordinary survival threshold: "+OwnedStock("Silver"));
                    Check(OwnedStock("MedicineHerbal")==beforeMedicine && OwnedStock("MeleeWeapon_Knife")==beforeWeapon,"Protected medicine/weapon ownership changed: medicine="+OwnedStock("MedicineHerbal")+"/"+beforeMedicine+"; weapon="+OwnedStock("MeleeWeapon_Knife")+"/"+beforeWeapon+"; carried="+
                        string.Join(";",map.mapPawns.FreeColonistsSpawned.Select(p=>p.LabelShort+":equipment="+p.equipment?.Primary?.def.defName+":inventory="+string.Join(",",p.inventory.innerContainer.Select(t=>t.def.defName+"x"+t.stackCount))+":carry="+p.carryTracker.CarriedThing?.def.defName)));
                    Check(!TradeSession.Active,"Automatic trade session was left active.");
                    Pass("native trade transfers drugs and needed materials, preserves silver reserve and protected goods, closes session");
                    if(Orbital)Pass("orbital drop pods landed; in-transit purchases cleared against real accessible stock");
                    if(CaravanTest){Check(caravanDeparted && map.mapPawns.FreeColonistsSpawnedCount==ExpectedResidents,"Couriers did not return alive to original map.");Pass("native world travel, settlement trade and return deliver actual purchases to the colony");}
                    EliteFixtureBaseline.VerifyOriginalsPresent(map);
                    firstTrade=ai.Commerce.LastTrade;firstRemaining=Stock("SmokeleafJoint");firstSilver=Stock("Silver");
                    firstBuyer=ai.Commerce.LastBuyer;firstSales=firstBuyer!=null && ai.Commerce.JointSales.TryGetValue(firstBuyer,out int sold)?sold:0;
                    stage=2;GameDataSaveLoader.SaveGame("CommerceRoundtrip");GameDataSaveLoader.LoadGame("CommerceRoundtrip");return;
                }
                if(Find.TickManager.TicksGame%3000==0)Log.Message("[CommerceTests] progress: "+ai.Commerce.Status+" job="+ai.Commerce.Negotiator?.CurJob?.def.defName);
                Check(Find.TickManager.TicksGame-start<(CaravanTest?300000:20000),"No transaction completed: "+ai.Commerce.Status+" / "+ai.Commerce.CaravanStatus);
            }
            catch(Exception ex){stage=99;GameDataSaveLoader.SaveGame("CommerceFailure");Log.Error("[CommerceTests] FAIL: "+ex);}
        }
    }
}
