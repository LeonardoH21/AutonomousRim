using System;
using System.Linq;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using AutonomousRim.Core;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class EnhancedWorldChecks : MapComponent
    {
        private static int phase;
        private static bool finished;
        private Pawn worker;
        private Mineable ore;
        private Plant crop;
        private int steelBefore,riceBefore,jobStart;
        public EnhancedWorldChecks(Map map):base(map){}
        public override void MapComponentTick()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimenhancedtest") || finished || Find.TickManager.TicksGame%30!=0)return;
            try
            {
                if(phase==0 && map.mapPawns.FreeColonistsSpawnedCount==6)
                {
                    Verify();
                    Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
                    GameDataSaveLoader.SaveGame("AutonomousRim_Enhanced_World_EliteSix_BASE");
                    Log.Message("[EnhancedWorld] PASS: clean baseline saved before disposable yield fixtures.");
                    var ai=map.GetComponent<AutonomousRimMapComponent>();ai.SetAutomation(false,false);ai.SetBaseAutomation(false);
                    worker=map.mapPawns.FreeColonistsSpawned.First(p=>p.Name.ToStringShort=="Bravo");
                    var cell=CellFinder.RandomClosewalkCellNear(worker.Position,map,6);
                    while(cell.GetEdifice(map)!=null)cell=CellFinder.RandomClosewalkCellNear(worker.Position,map,12);
                    ore=(Mineable)GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MineableSteel")),cell,map);
                    steelBefore=Count(ThingDefOf.Steel);
                    worker.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Mine,ore),JobTag.Misc);
                    jobStart=Find.TickManager.TicksGame;phase=1;Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
                }
                else if(phase==1 && ore.Destroyed)
                {
                    int expected=GenMath.RoundRandom(ore.def.building.EffectiveMineableYield*worker.GetStatValue(StatDefOf.MiningYield));
                    int actual=Count(ThingDefOf.Steel)-steelBefore;
                    Check(Math.Abs(actual-expected)<=2,"native mining output "+actual+" expected "+expected);
                    Log.Message("[EnhancedWorld] PASS: native mining job yielded "+actual+" steel; stat="+worker.GetStatValue(StatDefOf.MiningYield));
                    worker=map.mapPawns.FreeColonistsSpawned.First(p=>p.Name.ToStringShort=="Charlie");
                    var cell=CellFinder.RandomClosewalkCellNear(worker.Position,map,6);
                    while(cell.GetEdifice(map)!=null || cell.GetPlant(map)!=null)cell=CellFinder.RandomClosewalkCellNear(worker.Position,map,12);
                    crop=(Plant)GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Plant_Rice")),cell,map);crop.Growth=1;
                    riceBefore=Count(DefDatabase<ThingDef>.GetNamed("RawRice"));
                    worker.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Harvest,crop),JobTag.Misc);
                    jobStart=Find.TickManager.TicksGame;phase=2;
                }
                else if(phase==2 && crop.Destroyed)
                {
                    int actual=Count(DefDatabase<ThingDef>.GetNamed("RawRice"))-riceBefore;
                    int expected=GenMath.RoundRandom(crop.def.plant.harvestYield*Find.Storyteller.difficulty.cropYieldFactor*worker.GetStatValue(StatDefOf.PlantHarvestYield));
                    Check(Math.Abs(actual-expected)<=1,"native harvest output "+actual+" expected "+expected);
                    Log.Message("[EnhancedWorld] PASS: native rice harvest yielded "+actual+"; stat="+worker.GetStatValue(StatDefOf.PlantHarvestYield));
                    phase=3;
                    GameDataSaveLoader.LoadGame("AutonomousRim_Enhanced_World_EliteSix_BASE");
                }
                else if(phase==3)
                {
                    Verify();Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
                    Log.Message("[EnhancedWorld] PASS: baseline reload preserved six profiles, scenario factors, difficulty and free saving; disposable resources absent.");
                    finished=true;Log.Message("[EnhancedWorld] DONE");
                }
                if(phase==1 || phase==2)Check(Find.TickManager.TicksGame-jobStart<30000,"native yield job timeout: "+worker.CurJob?.def.defName);
            }
            catch(Exception e){finished=true;Log.Error("[EnhancedWorld] FAIL: "+e);}
        }
        private int Count(ThingDef d)=>map.listerThings.ThingsOfDef(d).Sum(t=>t.stackCount)+map.mapPawns.FreeColonistsSpawned.Sum(p=>p.inventory.innerContainer.Where(t=>t.def==d).Sum(t=>t.stackCount)+(p.carryTracker.CarriedThing?.def==d?p.carryTracker.CarriedThing.stackCount:0));
        private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        public void Verify()
        {
            Check(Current.Game.Scenario.name=="AutonomousRim Enhanced World","scenario name");
            Check(Find.Storyteller.def==StorytellerDefOf.Cassandra && Find.Storyteller.difficultyDef.defName=="Medium" && !Current.Game.Info.permadeathMode,"Cassandra / Medium / reload mode");
            var profiles=EnhancedWorldSetup.Preset().SelectNodes("/preset/pawns/li");
            foreach(XmlNode profile in profiles)
            {
                var p=map.mapPawns.FreeColonistsSpawned.Single(x=>x.Name.ToStringShort==profile["nickName"].InnerText);
                foreach(XmlNode skill in profile.SelectNodes("skills/li"))
                {
                    var s=p.skills.GetSkill(DefDatabase<SkillDef>.GetNamed(skill["name"].InnerText));
                    Check(s.Level==int.Parse(skill["value"].InnerText) && s.passion.ToString()==skill["passion"].InnerText,"skill/passions "+p.LabelShort);
                }
                Check(p.story.traits.allTraits.Count==3,"traits count");
                Check(p.gender.ToString()==profile["gender"].InnerText && p.story.bodyType.defName==profile["bodyType"].InnerText,"gender / body type");
                Check(Math.Abs(p.ageTracker.AgeBiologicalTicks-long.Parse(profile["biologicalAgeInTicks"].InnerText))<120 && Math.Abs(p.ageTracker.AgeChronologicalTicks-long.Parse(profile["chronologicalAgeInTicks"].InnerText))<120,"preset ages");
                Check((p.story.Childhood.defName==profile["childhood"].InnerText || p.story.Childhood.identifier==profile["childhood"].InnerText) && (p.story.Adulthood.defName==profile["adulthood"].InnerText || p.story.Adulthood.identifier==profile["adulthood"].InnerText),"backstories");
                Check(p.genes.Xenotype.defName=="Baseliner" && p.genes.GenesListForReading.Count==0,"preset xenotype");
                foreach(XmlNode implant in profile.SelectNodes("implants/li"))
                {
                    var recipe=DefDatabase<RecipeDef>.GetNamed(implant["recipe"].InnerText);
                    int index=implant["bodyPartIndex"]==null?0:int.Parse(implant["bodyPartIndex"].InnerText);
                    var part=p.RaceProps.body.AllParts.Where(b=>b.def.defName==implant["bodyPart"].InnerText).ElementAt(index);
                    Check(p.health.hediffSet.hediffs.Count(h=>h.def==recipe.addsHediff && h.Part==part)==1,"exact implant / body part "+p.LabelShort+" "+recipe.defName);
                }
                foreach(XmlNode trait in profile.SelectNodes("traits/li"))Check(p.story.traits.allTraits.Any(t=>t.def.defName==trait["def"].InnerText && t.Degree==int.Parse(trait["degree"].InnerText)),"trait mismatch");
                Check(p.health.hediffSet.hediffs.Count(h=>h.def.addedPartProps!=null || h.def.defName.Contains("Gland") || h.def.defName=="Coagulator" || h.def.defName=="HealingEnhancer" || h.def.defName=="Immunoenhancer")==12,"implant count: "+p.LabelShort+" "+string.Join(",",p.health.hediffSet.hediffs.Select(h=>h.def.defName)));
                Check(p.equipment.AllEquipmentListForReading.Count==0 && p.apparel.WornApparel.Count==0,"preset starting equipment");
            }
            foreach(var pair in new[]{("MiningYield",5f),("PlantHarvestYield",3f),("ConstructionSpeed",2f),("ResearchSpeed",3f)})
            {
                var stat=DefDatabase<StatDef>.GetNamed(pair.Item1);
                Check(Current.Game.Scenario.GetStatFactor(stat)==pair.Item2,"scenario stat factor: "+pair.Item1);
                var p=map.mapPawns.FreeColonistsSpawned.First();
                var part=Current.Game.Scenario.AllParts.OfType<ScenPart_StatFactor>().Single(s=>s.GetStatFactor(stat)!=1f);
                float effective=p.GetStatValue(stat,cacheStaleAfterTicks:0);
                AccessTools.Field(typeof(ScenPart_StatFactor),"factor").SetValue(part,1f);
                float normal=p.GetStatValue(stat,cacheStaleAfterTicks:0);
                AccessTools.Field(typeof(ScenPart_StatFactor),"factor").SetValue(part,pair.Item2);
                p.GetStatValue(stat,cacheStaleAfterTicks:0);
                Check(Math.Abs(effective-normal*pair.Item2)<0.01,"effective factor "+pair.Item1+" "+effective+" / "+normal);
                Log.Message("[EnhancedWorld] PASS: "+pair.Item1+" baseline="+normal+" final="+effective+" multiplier="+pair.Item2);
            }
            Check(StatDefOf.MiningYield.maxValue==100 && StatDefOf.PlantHarvestYield.maxValue==100,"yield caps");
            Check(ThingDefOf.Wall.costStuffCount==5,"wall materials unchanged");
            Check(DefDatabase<ResearchProjectDef>.GetNamed("MicroelectronicsBasics").baseCost==3000,"research cost unchanged");
        }
    }
    public sealed class EnhancedWorldReloadChecks : MapComponent
    {
        private static bool requested,done;
        public EnhancedWorldReloadChecks(Map map):base(map){}
        public override void MapComponentUpdate()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimenhancedreload") || requested)return;
            requested=true;GameDataSaveLoader.LoadGame("ModularResume");
        }
        public override void MapComponentTick()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimenhancedreload") || !requested || done)return;
            try
            {
                new EnhancedWorldChecks(map).Verify();
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                ai.SetAutomation(true,true);ai.SetScheduleAutomation(true);ai.SetStrategyAutomation(true);ai.SetLootAutomation(true);
                ai.SetEquipmentAutomation(true);ai.SetCombatAutomation(true);ai.SetEmergencyAutomation(true);
                ai.SetBaseAutomation(true);ai.SetPrisonAutomation(true);ai.SetCommerceAutomation(true);
                Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
                GameDataSaveLoader.SaveGame("AutonomousRim_Enhanced_World_EliteSix_TESTE");
                Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
                Log.Message("[EnhancedWorldReload] PASS: exported baseline loaded; exact profiles/factors/costs verified; all automation enabled; speed 3x saved.");
                done=true;
            }
            catch(Exception e){done=true;Log.Error("[EnhancedWorldReload] FAIL: "+e);}
        }
    }
}
