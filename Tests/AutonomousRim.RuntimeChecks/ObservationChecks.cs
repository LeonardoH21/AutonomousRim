using System;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Perception;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class ObservationChecks : MapComponent
    {
        private static int stage;
        private static int checkpoint;
        private static string colony;
        public ObservationChecks(Map map):base(map){}
        private static void Check(bool pass,string text) {if(!pass)throw new Exception(text);Log.Message("[Observation] PASS: "+text);}
        public override void MapComponentTick()
        {
            if(stage==99 || !EliteFixtureBaseline.Ready || !GenCommandLine.CommandLineArgPassed("autonomousrimobservationtest") || Find.TickManager.TicksGame<300)return;
            try {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(stage==1) {
                    Check(ai.Observations.Frames.Any(f=>f.Tick==checkpoint && f.MapId==map.uniqueID),"native perception checkpoint survives save/load");
                    Check(ai.Observations.ColonyId==colony,"colony identity survives load and does not multiply evidence");
                    var history=ai.Progress.Events.Single(e=>e.Text=="Observation roundtrip marker");
                    Check(history.MapId==map.uniqueID && history.Module=="Fixture" && history.Objective=="fixture:memory" && history.ObservationTick==checkpoint && history.Policy=="food-buffer-v1" && !string.IsNullOrEmpty(history.Context),"structured decision provenance survives native save/load");
                    Check(ai.Observations.Policy=="food-buffer-v1" && ai.Observations.PreviousPolicy=="observation-v1","policy and rollback version survive load");
                    Check(StrategyEvidenceBuilder.From(ai.Observations,colony)==null,"loading does not manufacture a day of cross-colony evidence");
                    EliteFixtureBaseline.VerifyOriginalsPresent(map);stage=99;Log.Message("[Observation] DONE");return;
                }
                EliteFixtureBaseline.Roster(map);ai.DisableAll();
                ai.ObserveNow();checkpoint=ai.Observations.Latest.Tick;colony=ai.Observations.ColonyId;
                Check(ai.Observations.Latest.Tick==ai.CurrentState.Tick && ai.Progress.ObservationTick==ai.CurrentState.Tick,"shared diagnostic observation has one tick and map");
                var frame=ai.Observations.Latest;float savedNutrition=frame.FoodNutrition;
                ai.CurrentState.FoodNutrition+=100;
                Check(frame.FoodNutrition==savedNutrition && !string.IsNullOrEmpty(frame.Resources) && !string.IsNullOrEmpty(frame.Workers),"checkpoint stores copied scalar resources and worker context");
                var ledger=new ProgressState();ledger.Bind(9,42,"known stock","v1");
                ledger.Observe("research:fixture","Fixture",1,true,"ready","worker and bench",50);
                var evt=ledger.Events.Single();Check(evt.Module=="Pesquisa" && evt.MapId==9 && evt.ObservationTick==42 && evt.Objective=="research:fixture","events identify responsible module and contemporaneous observation");
                for(int i=0;i<100;i++)ledger.Record(i,"item");Check(ledger.Events.Count==64,"event ledger remains bounded");
                var memory=new ObservationMemory();memory.ResetSession(0);
                memory.Add(new ObservationFrame{Tick=0,MapId=1,Context="same",Policy="observation-v1",FoodNutrition=100,FoodDays=5,Colonists=6});
                memory.Add(new ObservationFrame{Tick=3000,MapId=1,Context="same",Policy="observation-v1",FoodNutrition=95,FoodDays=4.75f,Colonists=6});
                Check(memory.Trends().Contains("estimativa") && memory.Trends().Contains("saldo líquido"),"falling stocks produce net-rate exhaustion estimate without claiming consumption");
                memory.Add(new ObservationFrame{Tick=6000,MapId=1,Context="different",Policy="observation-v1"});
                memory.Add(new ObservationFrame{Tick=9000,MapId=1,Context="same",Policy="observation-v1"});
                Check(memory.CompatibleWindow().Count==1,"returning to an old context never bridges incompatible windows");
                memory.ResetSession(9000);Check(memory.Trends().Contains("Aguardando"),"load/session reset excludes historical time from trends");
                var stable=new ObservationMemory();stable.ResetSession(0);
                for(int tick=0;tick<=60000;tick+=600)stable.Add(new ObservationFrame{Tick=tick,MapId=1,Context="same",Policy="observation-v1",FoodDays=3,Colonists=6});
                var evidence=StrategyEvidenceBuilder.From(stable,"colony-a");Check(evidence!=null,"one full uncontested observed day creates evidence");
                var archive=new EvidenceSettings();Check(archive.Add(evidence) && !archive.Add(evidence),"same colony and window are not counted twice");
                Check(archive.Describe("same","observation-v1").Contains("insuficiente"),"one colony cannot claim inter-colony confirmation");
                var second=StrategyEvidenceBuilder.From(stable,"colony-b");archive.Add(second);
                Check(archive.Describe("same","observation-v1").Contains("2 colônias") && archive.Describe("other","observation-v1").Contains("0 janelas"),"compatible independent colonies count but other contexts are excluded");
                stable.Latest.Enemies=1;Check(StrategyEvidenceBuilder.From(stable,"colony-c")==null,"threat invalidates peaceful strategy evidence");
                stable.Latest.Enemies=0;stable.Latest.Downed=1;Check(StrategyEvidenceBuilder.From(stable,"colony-c")==null,"downed colonist invalidates stable-window export");
                stable.Latest.Downed=0;
                stable.ChangePolicy("food-buffer-v1");Check(stable.ExtraFoodDays==1 && stable.RestorePolicy() && stable.ExtraFoodDays==0,"strategy versions apply and restore a real food reserve adjustment");
                Check(!stable.ChangePolicy("unknown"),"unknown strategy cannot be activated");
                for(int tick=60600;tick<200000;tick+=600)stable.Add(new ObservationFrame{Tick=tick,MapId=1,Context="same",Policy="observation-v1"});
                Check(stable.Frames.Count==128,"perception history stays bounded while covering a day");
                var modItem=new ThingDef{defName="AutonomousRimFixtureItem",label="fixture mod item",category=ThingCategory.Item,thingClass=typeof(ThingWithComps)};
                var modRecipe=new RecipeDef{defName="AutonomousRimFixtureRecipe",label="fixture mod recipe",products=new System.Collections.Generic.List<ThingDefCountClass>{new ThingDefCountClass(modItem,1)},researchPrerequisite=DefDatabase<ResearchProjectDef>.GetNamed("ComplexFurniture")};
                var modBench=new ThingDef{defName="AutonomousRimFixtureBench",label="fixture mod bench",category=ThingCategory.Building,thingClass=typeof(Building_WorkTable),building=new BuildingProperties(),recipes=new System.Collections.Generic.List<RecipeDef>{modRecipe}};
                DefDatabase<ThingDef>.Add(modItem);DefDatabase<ThingDef>.Add(modBench);DefDatabase<RecipeDef>.Add(modRecipe);
                AssignHash(modItem);AssignHash(modBench);AssignHash(modRecipe);
                Check(ItemCatalog.Entries.TryGetValue(modItem,out var custom) && custom.Recipes.Contains(modRecipe) && custom.Benches.Contains(modBench) && custom.Route.IndexOf("fixture mod recipe",StringComparison.OrdinalIgnoreCase)>=0 && custom.Route.Contains(modRecipe.researchPrerequisite.LabelCap.ToString()),"catalog discovers added native mod definitions with recipe research and bench links");
                var pants=DefDatabase<ThingDef>.GetNamed("Apparel_Pants");
                Check(ItemCatalog.Entries.TryGetValue(pants,out var catalog) && catalog.Recipes.Any(r=>r.products.Any(p=>p.thingDef==pants)) && catalog.Benches.Any(b=>b.AllRecipes.Any(r=>catalog.Recipes.Contains(r))),"item catalog follows native product recipes and compatible worktables");
                BuildRooms();
                var rooms=RoomObserver.Read(map);
                var left=(map.Center+new IntVec3(41,0,41)).GetRoom(map);
                var right=(map.Center+new IntVec3(45,0,41)).GetRoom(map);
                Check(left!=null && right!=null && left!=right,"fixture creates two native rooms sharing one partition wall");
                var observed=rooms.FirstOrDefault(r=>r.Id==left.ID);
                Check(observed!=null && observed.Connections.Any(c=>c.Contains("sala #"+right.ID)),"room graph identifies shared door destination");
                Check(observed.Unroofed==0 && observed.Doors>0 && observed.Vents==1,"room perception reports roof doors and thermal vent");
                float normal=ColonyStateScanner.Scan(map).TargetFoodDays;
                ai.SetObservationPolicy("food-buffer-v1");
                Check(Math.Abs(ColonyStateScanner.Scan(map).TargetFoodDays-normal-1)<.001f,"versioned reserve policy reaches native food planning inputs");
                ai.Progress.Record(Find.TickManager.TicksGame,"Observation roundtrip marker","Fixture","fixture:memory");
                checkpoint=ai.Progress.ObservationTick;stage=1;
                GameDataSaveLoader.SaveGame("ObservationEvidence");GameDataSaveLoader.LoadGame("ObservationEvidence");
            } catch(Exception e) {stage=99;Log.Error("[Observation] FAIL: "+e);}
        }
        private void BuildRooms()
        {
            var origin=map.Center+new IntVec3(40,0,40);var rect=new CellRect(origin.x,origin.z,9,5);
            foreach(var c in rect.Cells) {
                foreach(var t in c.GetThingList(map).Where(t=>!(t is Pawn)).ToList())t.Destroy(DestroyMode.Vanish);
                map.terrainGrid.SetTerrain(c,TerrainDefOf.Soil);map.fogGrid.Unfog(c);map.roofGrid.SetRoof(c,RoofDefOf.RoofConstructed);
            }
            foreach(var c in rect.Cells.Where(c=>c.x==rect.minX || c.x==rect.maxX || c.z==rect.minZ || c.z==rect.maxZ || c.x==origin.x+4)) {
                string name=c.x==origin.x+4 && c.z==origin.z+2?"Door":c.x==origin.x+4 && c.z==origin.z+1?"Vent":"Wall";
                var def=DefDatabase<ThingDef>.GetNamed(name);var building=ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.WoodLog:null);
                building.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(building,c,map);
            }
            foreach(int x in new[]{1,5}) {
                var bed=ThingMaker.MakeThing(ThingDefOf.Bed,ThingDefOf.WoodLog);bed.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(bed,origin+new IntVec3(x,0,1),map);
            }
            map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
        }
        private static void AssignHash<T>(T def) where T:Def
        {
            var used=new System.Collections.Generic.HashSet<ushort>(DefDatabase<T>.AllDefsListForReading.Where(d=>d!=def).Select(d=>d.shortHash));
            HarmonyLib.AccessTools.Method(typeof(ShortHashGiver),"GiveShortHash").Invoke(null,new object[]{def,typeof(T),used});
        }
    }
}
