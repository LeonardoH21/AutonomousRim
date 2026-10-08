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
        private IntVec3 center;
        public CommerceChecks(Map map):base(map){}
        private void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);}
        private void Pass(string text)=>Log.Message("[CommerceTests] PASS: "+text);
        private int Stock(string name)=>CommercePlanner.Stock(map,DefDatabase<ThingDef>.GetNamed(name));
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
            while(count>0){int batch=Math.Min(count,def.stackLimit);Check(trader.inventory.innerContainer.TryAdd(Item(name,batch)),"Trader stock insertion failed.");count-=batch;}
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
                while(p.health.hediffSet.hediffs.Count>0 || p.skills.skills.Any(s=>s.TotallyDisabled));
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
            Goods("Silver",2000);Goods("Steel",200);Goods("ComponentIndustrial",20);Goods("Cloth",100);
            Ground("Silver",500);Ground("SmokeleafJoint",100);Ground("MealSimple",60);Ground("MedicineHerbal",30);Ground("Steel",10);Ground("MeleeWeapon_Knife",1);
            var crafting=(Building_WorkTable)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("CraftingSpot"));crafting.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(crafting,center+new IntVec3(-10,0,10),map);
            var projects=(List<RoomProject>)HarmonyLib.AccessTools.Field(typeof(AutonomousRimMapComponent),"baseProjects").GetValue(ai);
            var essential=new RoomProject{Kind="Energia e climatização",Origin=center+new IntVec3(-16,0,0),InteriorSize=4,RequiresRoof=false,Priority=ConstructionPriority.High};
            essential.Furniture.Add(new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed("Battery"),Position=essential.Origin});projects.Add(essential);
            beforeSteel=Stock("Steel");beforeDrug=Stock("SmokeleafJoint");beforeMedicine=Stock("MedicineHerbal");beforeWeapon=Stock("MeleeWeapon_Knife");
            ai.SetAutomation(false,false);ai.SetCommerceAutomation(true);
            Check(ai.Commerce.Needs.Count>0 && ai.Commerce.JobId!=null,"Idle commerce did not issue a native trader job: "+ai.Commerce.Status);
            Check(ai.Commerce.Bills.Count>0 && ai.Commerce.Target>0 && ai.Commerce.Target<=100,"First commercial production batch missing or unbounded.");
            Pass("deficit plan, bounded production bill and native negotiator dispatch from idle state");
            start=Find.TickManager.TicksGame;stage=1;
        }
        public override void MapComponentTick()
        {
            if(stage==99 || !GenCommandLine.CommandLineArgPassed("autonomousrimcommercetest") || Find.TickManager.TicksGame<300)return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(stage==0){Setup();return;}
                if(stage==2)
                {
                    Check(ai.CommerceAutomation && ai.Commerce.LastTrade>0 && ai.Commerce.History.Count>0,"Trade state lost after native save/load.");
                    Pass("native save/load preserves completed transaction, history and toggle");
                    ai.SetCommerceAutomation(false);Check(ai.Commerce.Bills.Count==0 && ai.Commerce.JobId==null,"Commerce disable retained owned jobs/bills.");
                    Pass("disable removes only managed trade production and job ownership");stage=99;Log.Message("[CommerceTests] DONE");return;
                }
                if(ai.Commerce.LastTrade>0)
                {
                    Check(Stock("SmokeleafJoint")<beforeDrug,"Commercial products not actually sold.");
                    Check(Stock("Steel")>beforeSteel || Stock("ComponentIndustrial")>0,"Needed materials were not actually delivered.");
                    Check(Stock("Silver")>=250,"Cash reserve spent below ordinary survival threshold.");
                    Check(Stock("MedicineHerbal")==beforeMedicine && Stock("MeleeWeapon_Knife")==beforeWeapon,"Protected medicine/weapon sold.");
                    Check(!TradeSession.Active,"Automatic trade session was left active.");
                    Pass("native trade transfers drugs and needed materials, preserves silver reserve and protected goods, closes session");
                    stage=2;GameDataSaveLoader.SaveGame("CommerceRoundtrip");GameDataSaveLoader.LoadGame("CommerceRoundtrip");return;
                }
                if(Find.TickManager.TicksGame%3000==0)Log.Message("[CommerceTests] progress: "+ai.Commerce.Status+" job="+ai.Commerce.Negotiator?.CurJob?.def.defName);
                Check(Find.TickManager.TicksGame-start<20000,"No transaction completed: "+ai.Commerce.Status);
            }
            catch(Exception ex){stage=99;Log.Error("[CommerceTests] FAIL: "+ex);}
        }
    }
}
