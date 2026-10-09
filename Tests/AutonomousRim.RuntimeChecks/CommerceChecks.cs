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
        private IntVec3 center;
        public CommerceChecks(Map map):base(map){}
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
            var peaceful=DefDatabase<DifficultyDef>.GetNamed("Peaceful");Find.Storyteller.difficultyDef=peaceful;Find.Storyteller.difficulty.CopyFrom(peaceful);
            map.Biome.constantOutdoorTemperature=21;
            foreach(var pawn in map.mapPawns.AllPawnsSpawned.ToList()){pawn.DeSpawn();Find.WorldPawns.PassToWorld(pawn,PawnDiscardDecideMode.KeepForever);}
            foreach(var t in map.listerThings.AllThings.Where(t=>t.def.category==ThingCategory.Item || t is Hive || !(t is Pawn) && t is IAttackTarget && t.HostileTo(Faction.OfPlayer)).ToList())t.Destroy(DestroyMode.Vanish);
            foreach(var cell in new CellRect(center.x-20,center.z-20,41,41))
            {
                foreach(var t in cell.GetThingList(map).ToList()){if(t.def.destroyable)t.Destroy(DestroyMode.Vanish);else t.DeSpawn();}
                map.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);map.roofGrid.SetRoof(cell,null);map.fogGrid.Unfog(cell);map.areaManager.Home[cell]=true;
            }
            for(int i=0;i<5;i++)
            {
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
                Building PowerBuilding(string name,IntVec3 cell)
                {
                    var b=(Building)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name));b.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(b,cell,map);return b;
                }
                console=(Building_CommsConsole)PowerBuilding("CommsConsole",center+new IntVec3(-8,0,4));
                PowerBuilding("OrbitalTradeBeacon",center+new IntVec3(0,0,5));
                PowerBuilding("WoodFiredGenerator",center+new IntVec3(-4,0,8)).TryGetComp<CompRefuelable>().Refuel(75);
                for(int x=-8;x<=0;x++)PowerBuilding("PowerConduit",center+new IntVec3(x,0,5));
                for(int z=6;z<=9;z++)PowerBuilding("PowerConduit",center+new IntVec3(-4,0,z));
            }
            Goods("Silver",2000);Goods("Steel",200);Goods("ComponentIndustrial",20);Goods("Cloth",100);
            Ground("Silver",500);Ground("SmokeleafJoint",100);Ground("MealSimple",60);Ground("MedicineHerbal",30);Ground("Steel",10);Ground("MeleeWeapon_Knife",1);
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
            var projects=(List<RoomProject>)HarmonyLib.AccessTools.Field(typeof(AutonomousRimMapComponent),"baseProjects").GetValue(ai);
            var essential=new RoomProject{Kind="Energia e climatização",Origin=center+new IntVec3(-16,0,0),InteriorSize=4,RequiresRoof=false,Priority=ConstructionPriority.High};
            essential.Furniture.Add(new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed("Battery"),Position=essential.Origin});projects.Add(essential);
            beforeSteel=Stock("Steel");beforeDrug=Stock("SmokeleafJoint");beforeMedicine=OwnedStock("MedicineHerbal");beforeWeapon=OwnedStock("MeleeWeapon_Knife");
            ai.SetAutomation(false,false);
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
            Check(ai.Commerce.Needs.Count>0 && ai.Commerce.JobId!=null,"Idle commerce did not issue a native trader job: "+ai.Commerce.Status);
            Check(ai.Commerce.Bills.Count>0 && ai.Commerce.Target>0 && ai.Commerce.Target<=100,"First commercial production batch missing or unbounded.");
            Pass("deficit plan, bounded production bill and native negotiator dispatch from idle state");
            start=Find.TickManager.TicksGame;stage=1;
        }
        private void SafetySetup(AutonomousRimMapComponent ai)
        {
            var manualNegotiator=map.mapPawns.FreeColonistsSpawned.First();
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
            if(stage==99 || !(GenCommandLine.CommandLineArgPassed("autonomousrimcommercetest") || Orbital || CaravanTest || SafetyTest) || Find.TickManager.TicksGame<300)return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(stage==0){Setup();return;}
                if(stage==4)
                {
                    Check(Find.TickManager.TicksGame-start<3000,"Native console power network did not start.");
                    if(!console.CanUseCommsNow)return;
                    ai.SetCommerceAutomation(true);
                    Check(ai.Commerce.JobId!=null,"Powered orbital setup did not dispatch negotiator: "+ai.Commerce.Status);
                    Pass("native powered console/beacon and orbital negotiator dispatch");stage=1;return;
                }
                if(stage==2)
                {
                    Check(ai.CommerceAutomation && ai.Commerce.LastTrade>0 && ai.Commerce.History.Count>0,"Trade state lost after native save/load.");
                    Pass("native save/load preserves completed transaction, history and toggle");
                    ai.SetCommerceAutomation(false);Check(ai.Commerce.Bills.Count==0 && ai.Commerce.JobId==null,"Commerce disable retained owned jobs/bills.");
                    Pass("disable removes only managed trade production and job ownership");stage=99;Log.Message("[CommerceTests] DONE");return;
                }
                if(CaravanTest && !caravanDeparted && ai.Commerce.Expedition?.Spawned==true)
                {caravanDeparted=true;Pass("couriers packed real goods, left map and formed native world caravan");}
                bool caravanUnloaded=!CaravanTest || ai.Commerce.ExpeditionPawns.Count==0 &&
                    (Stock("Steel")>beforeSteel || Stock("ComponentIndustrial")>0) && OwnedStock("MedicineHerbal")==beforeMedicine;
                if(ai.Commerce.LastTrade>0 && caravanUnloaded)
                {
                    if(Orbital && (ai.Commerce.Deliveries.Count>0 || Stock("Silver")<250))
                    {Check(Find.TickManager.TicksGame-start<20000,"Orbital delivery never became accessible.");return;}
                    Check(Stock("SmokeleafJoint")<beforeDrug,"Commercial products not actually sold.");
                    Check(Stock("Steel")>beforeSteel || Stock("ComponentIndustrial")>0,"Needed materials were not actually delivered.");
                    Check(Stock("Silver")>=250,"Cash reserve spent below ordinary survival threshold.");
                    Check(OwnedStock("MedicineHerbal")==beforeMedicine && OwnedStock("MeleeWeapon_Knife")==beforeWeapon,"Protected medicine/weapon ownership changed: medicine="+OwnedStock("MedicineHerbal")+"/"+beforeMedicine+"; weapon="+OwnedStock("MeleeWeapon_Knife")+"/"+beforeWeapon+"; carried="+
                        string.Join(";",map.mapPawns.FreeColonistsSpawned.Select(p=>p.LabelShort+":equipment="+p.equipment?.Primary?.def.defName+":inventory="+string.Join(",",p.inventory.innerContainer.Select(t=>t.def.defName+"x"+t.stackCount))+":carry="+p.carryTracker.CarriedThing?.def.defName)));
                    Check(!TradeSession.Active,"Automatic trade session was left active.");
                    Pass("native trade transfers drugs and needed materials, preserves silver reserve and protected goods, closes session");
                    if(Orbital)Pass("orbital drop pods landed; in-transit purchases cleared against real accessible stock");
                    if(CaravanTest){Check(caravanDeparted && map.mapPawns.FreeColonistsSpawnedCount==5,"Couriers did not return alive to original map.");Pass("native world travel, settlement trade and return deliver actual purchases to the colony");}
                    stage=2;GameDataSaveLoader.SaveGame("CommerceRoundtrip");GameDataSaveLoader.LoadGame("CommerceRoundtrip");return;
                }
                if(Find.TickManager.TicksGame%3000==0)Log.Message("[CommerceTests] progress: "+ai.Commerce.Status+" job="+ai.Commerce.Negotiator?.CurJob?.def.defName);
                Check(Find.TickManager.TicksGame-start<(CaravanTest?300000:20000),"No transaction completed: "+ai.Commerce.Status+" / "+ai.Commerce.CaravanStatus);
            }
            catch(Exception ex){stage=99;GameDataSaveLoader.SaveGame("CommerceFailure");Log.Error("[CommerceTests] FAIL: "+ex);}
        }
    }
}
