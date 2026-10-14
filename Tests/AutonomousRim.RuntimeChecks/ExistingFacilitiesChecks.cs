using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;
namespace AutonomousRim.RuntimeChecks
{
    // Spawned facilities are disposable fixtures in an isolated copy of L1.
    // They exercise native rooms, access, storage, power, blueprint ownership and save/load.
    public sealed class ExistingFacilitiesChecks : MapComponent
    {
        private static bool requested,done;
        private static int phase,ready,checks;
        private static IntVec3 root;
        private static Building research,generator,cooler,hospitalBed;
        private static Zone_Stockpile coldStore,medicine;
        private static Blueprint_Build manualBlueprint;
        private static Frame frame;
        private static Dictionary<int,IntVec3> originalBeds;
        private static List<int> originalPawns;
        public ExistingFacilitiesChecks(Map map):base(map){}
        public override void MapComponentUpdate()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimexistingfacilities") || Current.ProgramState!=ProgramState.Playing)return;
            if(!requested){requested=true;GameDataSaveLoader.LoadGame("ModularResume");return;}
            foreach(var w in Find.WindowStack.Windows.Where(w=>w.forcePause).ToList())w.Close(false);
            Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
        }
        private static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Log.Message("[ExistingFacilities] PASS: "+message);}
        public override void MapComponentTick()
        {
            if(done || !requested || !GenCommandLine.CommandLineArgPassed("autonomousrimexistingfacilities") || Find.TickManager.TicksGame<373671)return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();var projects=(List<RoomProject>)ai.BaseProjects;
                if(phase==0)
                {
                    ai.DisableAll();originalPawns=map.mapPawns.FreeColonistsSpawned.Select(p=>p.thingIDNumber).ToList();
                    originalBeds=map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().ToDictionary(b=>b.thingIDNumber,b=>b.Position);
                    root=GenRadial.RadialCellsAround(map.Center,70,true).First(c=>new CellRect(c.x,c.z,39,29).ExpandedBy(2).InBounds(map) &&
                        new CellRect(c.x,c.z,39,29).All(x=>!x.GetThingList(map).Any(t=>t.Faction==Faction.OfPlayer || t is Pawn)));
                    foreach(var c in new CellRect(root.x,root.z,39,29))
                    {
                        foreach(var t in c.GetThingList(map).ToList())if(t.def.destroyable)t.Destroy(DestroyMode.Vanish);
                        map.fogGrid.Unfog(c);map.roofGrid.SetRoof(c,null);map.terrainGrid.SetTerrain(c,TerrainDefOf.Soil);
                    }
                    Room(0,0);Room(10,0);Room(20,0,true);Room(30,0);Room(0,10);Room(10,10);Room(20,10);Room(30,10);
                    Put("FueledStove",At(2,3));Put("TableButcher",At(12,3));
                    research=Put("SimpleResearchBench",At(22,3));
                    projects.Add(new RoomProject{Kind="Fixture rotation",Origin=research.Position,InteriorSize=1,Furniture=new List<ConstructionTask>{
                        new ConstructionTask{Def=research.def,Position=research.Position,Rotation=Rot4.East,Owned=true,WasCompleted=true}}});
                    cooler=Put("Cooler",At(34,8),Rot4.North);cooler.TryGetComp<CompTempControl>().TargetTemperature=-5;
                    coldStore=Stock(new[]{At(32,2),At(33,2),At(34,2),At(35,2),At(32,3),At(33,3)},ThingDefOf.MealSimple);
                    hospitalBed=Put("Bed",At(2,15));((Building_Bed)hospitalBed).Medical=true;
                    medicine=Stock(new[]{At(5,12),At(6,12)},ThingDefOf.MedicineHerbal);
                    generator=Put("WoodFiredGenerator",At(12,13));Put("PowerConduit",At(12,12));Put("Battery",At(15,13));
                    Put("Table2x2c",At(23,13));Put("Stool",At(22,13));Put("HorseshoesPin",At(26,15));
                    Put("HandTailoringBench",At(32,13));Put("TableStonecutter",At(35,15));
                    Stock(new[]{At(32,11),At(33,11),At(34,11),At(35,11)},ThingDefOf.WoodLog,ThingDefOf.Steel);
                    map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
                    var duplicate=GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Wall,At(3,24),map,Rot4.North,Faction.OfPlayer,ThingDefOf.WoodLog);
                    manualBlueprint=GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Wall,At(4,24),map,Rot4.North,Faction.OfPlayer,ThingDefOf.WoodLog);
                    frame=(Frame)ThingMaker.MakeThing(ThingDefOf.Wall.frameDef,ThingDefOf.WoodLog);GenSpawn.Spawn(frame,At(5,24),map);
                    projects.RemoveAll(p=>p.Kind=="Energia e climatização"); // Isolate one queued energy goal for ownership assertions.
                    projects.Add(new RoomProject{Kind="Energia e climatização",LayoutSlot="fixture:duplicate",Origin=At(1,22),InteriorSize=5,
                        Shell=new List<ConstructionTask>{new ConstructionTask{Def=ThingDefOf.Wall,Stuff=ThingDefOf.WoodLog,Position=duplicate.Position,Pending=duplicate,Issued=true,Owned=true},
                            new ConstructionTask{Def=ThingDefOf.Wall,Stuff=ThingDefOf.WoodLog,Position=manualBlueprint.Position,Pending=manualBlueprint,Issued=true},
                            new ConstructionTask{Def=ThingDefOf.Wall,Stuff=ThingDefOf.WoodLog,Position=frame.Position,Pending=frame,Issued=true,Owned=true}}});
                    ready=Find.TickManager.TicksGame+120;phase=1;return;
                }
                if(Find.TickManager.TicksGame<ready)return;
                if(phase==1)
                {
                    ai.Facilities.Refresh(map,projects,true);ai.Facilities.Reconcile(map,projects);
                    foreach(string kind in new[]{"Cozinha","Abate","Pesquisa","Hospital","Freezer","Estoque","Oficina","Refeitório e recreação","Energia e climatização","Medicamentos"})
                        Check(ai.Facilities.Services.Any(s=>s.Kind==kind && new CellRect(root.x,root.z,38,29).Contains(s.Anchor)),"native manual service recognized: "+kind);
                    Check(ai.Facilities.Services.Any(s=>s.Kind=="Pesquisa" && s.Anchor==research.Position),"manual rotation differs from old owned template and is still recognized");
                    var energy=projects.Single(p=>p.LayoutSlot=="fixture:duplicate");
                    Check(energy.ExistingFacility && energy.Completed,"queued energy room fulfilled by manual installation");
                    Check(energy.Shell[0].Pending==null && manualBlueprint.Spawned && frame.Spawned,"only AI blueprint withdrawn; player blueprint and invested frame preserved");
                    Check(!BaseConstructionManager.Tasks(energy).Any(),"superseded template excluded from resource/work queue");
                    var irregular=new RoomProject{Kind="Oficina",Origin=At(20,0),InteriorSize=7,InteriorHeight=7,ExistingFacility=true,ExistingFacilityAnchor=research.Position};
                    var placed=(ConstructionTask)AccessTools.Method(typeof(IndustryInfrastructure),"Place").Invoke(null,new object[]{map,projects,irregular,DefDatabase<ThingDef>.GetNamed("Shelf"),At(27,7)});
                    Check(placed!=null && GenAdj.OccupiedRect(placed.Position,placed.Rotation,placed.Def.Size).All(c=>c.GetRoom(map)==research.GetRoom()),
                        "industrial placement uses real cells of irregular manual room");
                    ModularBasePlanner.Plan(map,projects);
                    foreach(string kind in new[]{"Cozinha","Abate","Pesquisa","Hospital","Freezer","Estoque","Oficina","Refeitório e recreação"})
                        Check(!projects.Any(p=>p.Kind==kind && !p.ExistingFacility && !p.Completed && ModularBasePlanner.IsModular(p)),"no duplicate queued walled module: "+kind);
                    Check(projects.SelectMany(BaseConstructionManager.Tasks).Any(t=>t.MedicalBed && t.Position.GetRoom(map)==hospitalBed.GetRoom()),"hospital capacity complemented inside existing room");
                    Check(coldStore.GetStoreSettings().Priority==StoragePriority.Low && coldStore.GetStoreSettings().filter.AllowedThingDefs.Count()==1 &&
                        cooler.TryGetComp<CompTempControl>().TargetTemperature==-5 && medicine.GetStoreSettings().Priority==StoragePriority.Low,"manual filters, priorities and temperature preserved");
                    int count=projects.Count;ModularBasePlanner.Plan(map,projects);Check(projects.Count==count,"second planning pass creates no duplicate supplements");
                    var supplement=projects.First(p=>p.Kind=="Complemento de Hospital" && p.Furniture.Any(t=>t.MedicalBed));
                    foreach(var cost in CostListCalculator.CostListAdjusted(supplement.Furniture[0].Def,supplement.Furniture[0].Stuff))
                        for(int i=0;i<60;i++){var item=ThingMaker.MakeThing(cost.thingDef);item.stackCount=item.def.stackLimit;GenPlace.TryPlaceThing(item,At(1,22),map,ThingPlaceMode.Near);}
                    BaseConstructionManager.Apply(map,new List<RoomProject>{supplement});
                    Check(supplement.Furniture[0].Pending is Blueprint_Build && supplement.Furniture[0].Owned && supplement.Furniture[0].Pending.Spawned,
                        "missing medical bed emitted as native blueprint inside manual hospital");
                    phase=2;GameDataSaveLoader.SaveGame("ExistingFacilitiesEvidence");GameDataSaveLoader.LoadGame("ExistingFacilitiesEvidence");return;
                }
                if(phase==2)
                {
                    ai.DisableAll();ai.Facilities.Refresh(map,projects,true);ai.Facilities.Reconcile(map,projects);
                    Check(projects.Any(p=>p.ExistingFacility && p.Kind=="Hospital") && projects.Single(p=>p.LayoutSlot=="fixture:duplicate").ExistingFacility,"bindings survive native save/load and revalidation");
                    Check(originalPawns.All(id=>map.mapPawns.FreeColonistsSpawned.Any(p=>p.thingIDNumber==id)) && originalBeds.All(pair=>map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Any(b=>b.thingIDNumber==pair.Key && b.Position==pair.Value)),"original colonists and bedrooms preserved");
                    research=At(22,3).GetThingList(map).OfType<Building_ResearchBench>().Single();research.Destroy(DestroyMode.Vanish);
                    ai.Facilities.Refresh(map,projects,true);ai.Facilities.Reconcile(map,projects);
                    Check(!projects.Any(p=>p.Kind=="Pesquisa" && p.ExistingFacility),"removed research bench no longer satisfies goal");
                    Put("SimpleResearchBench",At(23,3));map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
                    ai.Facilities.Refresh(map,projects,true);ai.Facilities.Reconcile(map,projects);
                    Check(projects.Any(p=>p.Kind=="Pesquisa" && p.ExistingFacility && p.ExistingFacilityAnchor==At(23,3)),"relocated manual bench recognized without exact template position");
                    var bed=(Building_Bed)At(2,15).GetThingList(map).First(b=>b is Building_Bed);bed.ForPrisoners=true;
                    ai.Facilities.Refresh(map,projects,true);Check(!ai.Facilities.Services.Any(s=>s.Kind=="Hospital" && s.Room==bed.GetRoom()),"prisoner bed rejected as colonist hospital");
                    bed.ForOwnerType=BedOwnerType.Colonist;bed.Medical=true;
                    var stove=At(2,3).GetThingList(map).OfType<Building_WorkTable>().Single();Put("Wall",stove.InteractionCell);
                    ai.Facilities.Refresh(map,projects,true);Check(!ai.Facilities.Services.Any(s=>s.Kind=="Cozinha" && s.Anchor==At(2,3)),"blocked interaction cell rejected as usable kitchen");
                    done=true;Log.Message("[ExistingFacilities] DONE checks="+checks);return;
                }
            }
            catch(Exception error){done=true;Log.Error("[ExistingFacilities] FAIL: "+error);}
        }
        private static IntVec3 At(int x,int z)=>root+new IntVec3(x,0,z);
        private Building Put(string name,IntVec3 cell,Rot4? rot=null)
        {
            var def=DefDatabase<ThingDef>.GetNamed(name);var b=(Building)ThingMaker.MakeThing(def,def.MadeFromStuff?GenStuff.DefaultStuffFor(def):null);
            b.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(b,cell,map,rot??Rot4.North);return b;
        }
        private void Room(int x,int z,bool irregular=false)
        {
            var cells=new CellRect(At(x,z).x,At(x,z).z,9,9).Where(c=>!irregular || c.x<At(x+6,z).x || c.z<At(x,z+6).z).ToHashSet();
            foreach(var c in cells)
            {
                bool edge=GenAdj.CardinalDirections.Any(d=>!cells.Contains(c+d));
                if(edge)Put(c==At(x+4,z)?"Door":"Wall",c);
                else{map.terrainGrid.SetTerrain(c,DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor"));map.roofGrid.SetRoof(c,RoofDefOf.RoofConstructed);}
            }
        }
        private Zone_Stockpile Stock(IEnumerable<IntVec3> cells,params ThingDef[] defs)
        {
            var zone=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager);map.zoneManager.RegisterZone(zone);
            foreach(var c in cells)zone.AddCell(c);zone.GetStoreSettings().filter.SetDisallowAll();foreach(var def in defs)zone.GetStoreSettings().filter.SetAllow(def,true);
            zone.GetStoreSettings().Priority=StoragePriority.Low;return zone;
        }
    }
}
