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
    // Controlled budget/dependency contracts. Actual construction is verified
    // separately by resuming the unmodified native colony checkpoint.
    public sealed class ConstructionDependencyChecks : MapComponent
    {
        private bool done;
        public ConstructionDependencyChecks(Map map):base(map){}
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        private IntVec3 Empty()=>GenRadial.RadialCellsAround(map.Center,45,true).First(c=>c.InBounds(map) &&
            c.Standable(map) && !c.Fogged(map) && !c.GetThingList(map).Any());
        private Dictionary<ThingDef,int> Review(ColonyState state,List<RoomProject> rooms)=>(Dictionary<ThingDef,int>)
            AccessTools.Method(typeof(StoneProductionManager),"ReserveFutureWalls").Invoke(null,new object[]{map,state,rooms});
        private bool Blocked(RoomProject room,List<RoomProject> rooms)=>(bool)
            AccessTools.Method(typeof(BaseConstructionManager),"DependsOn").Invoke(null,new object[]{map,room,rooms});
        public override void MapComponentTick()
        {
            if(done || !GenCommandLine.CommandLineArgPassed("autonomousrimconstructiondependencytest") || Find.TickManager.TicksGame<300)return;
            done=true;
            try
            {
                map.GetComponent<AutonomousRimMapComponent>().DisableAll();
                var category=DefDatabase<ThingCategoryDef>.GetNamed("StoneBlocks");
                foreach(var t in map.listerThings.AllThings.Where(t=>t.def.category==ThingCategory.Item && t.def.IsWithinCategory(category)).ToList())t.Destroy();
                var lime=DefDatabase<ThingDef>.GetNamed("BlocksLimestone");
                var stock=ThingMaker.MakeThing(lime);stock.stackCount=40;GenSpawn.Spawn(stock,Empty(),map);stock.SetForbidden(false,false);
                var state=new ColonyState{EstimatedFoodDays=5};
                var rooms=new List<RoomProject>{new RoomProject{Kind="Quarto"},new RoomProject{Kind="Quarto"}};
                var places=new HashSet<IntVec3>();
                for(int i=0;i<16;i++)
                {
                    var c=GenRadial.RadialCellsAround(map.Center,45,true).First(p=>p.InBounds(map) && p.Standable(map) && !p.Fogged(map) &&
                        !p.GetThingList(map).Any() && places.Add(p));
                    rooms[i/8].Shell.Add(new ConstructionTask{Def=ThingDefOf.Wall,Stuff=ThingDefOf.WoodLog,Position=c});
                }
                rooms[1].Shell.Add(rooms[0].Shell[0]); // Shared wall reference must reserve once.
                int Cost(ConstructionTask task)=>CostListCalculator.CostListAdjusted(task.Def,task.Stuff).Where(c=>c.thingDef==lime).Sum(c=>c.count);
                Review(state,rooms);
                var walls=rooms.SelectMany(r=>r.Shell).Distinct().ToList();
                Check(walls.Sum(Cost)==40,"Future stone plans exceeded/dropped the available 40-block budget.");
                var selected=walls.Select(t=>t.Stuff).ToList();Review(state,rooms);
                Check(selected.SequenceEqual(walls.Select(t=>t.Stuff)),"Repeated review reused the same blocks for more walls.");
                Log.Message("[ConstructionDependencyTests] PASS: shared tasks reserve once; repeated planning cannot reuse blocks.");

                var manualCell=GenRadial.RadialCellsAround(map.Center,45,true).First(c=>c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && !places.Contains(c) && !c.GetThingList(map).Any());
                var manual=GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Wall,manualCell,map,Rot4.North,Faction.OfPlayer,lime);
                var manualTask=new ConstructionTask{Def=ThingDefOf.Wall,Stuff=lime,Position=manualCell};rooms[0].Shell.Add(manualTask);
                Review(state,rooms);
                int available=BaseConstructionManager.Available(map).TryGetValue(lime,out int amount)?Math.Max(0,amount):0;
                Check(walls.Where(t=>t.Stuff==lime).Sum(Cost)<=available && manual.Spawned && manual.EntityToBuildStuff()==lime && manualTask.Stuff==lime,
                    "Native blueprint reservation ignored or player's material changed: future="+walls.Where(t=>t.Stuff==lime).Sum(Cost)+", available="+available+", spawned="+manual.Spawned+", stuff="+manualTask.Stuff);
                Log.Message("[ConstructionDependencyTests] PASS: native pending costs deducted; existing blueprint preserved.");

                var protectedTask=walls.First(t=>t.Stuff==lime);protectedTask.Issued=true;protectedTask.Pending=manual;
                var cancelled=walls.Last();cancelled.Stuff=lime;cancelled.CancelledByPlayer=true;
                stock.Destroy();Review(state,rooms);
                Check(protectedTask.Stuff==lime && cancelled.Stuff==lime && manualTask.Stuff==lime,
                    "Issued/cancelled/player wall plan modified.");
                Check(walls.Where(t=>t!=protectedTask && t!=cancelled).All(t=>t.Stuff==ThingDefOf.WoodLog),
                    "Unissued automatic walls remained stranded on unavailable stone.");
                Log.Message("[ConstructionDependencyTests] PASS: unavailable automatic stone falls back to wood; issued/cancelled work remains protected.");

                var dependencies=new List<RoomProject>{new RoomProject{Kind=ModularBasePlanner.ReservationKind}};
                for(int i=0;i<map.mapPawns.FreeColonistsSpawnedCount;i++)dependencies.Add(new RoomProject{Kind="Quarto",Completed=true});
                var spare=new RoomProject{Kind="Quarto",State=ConstructionState.WaitingMaterials};dependencies.Insert(1,spare);
                var kitchen=new RoomProject{Kind="Cozinha"};var butcher=new RoomProject{Kind="Abate"};var storage=new RoomProject{Kind="Estoque"};
                var research=new RoomProject{Kind="Pesquisa"};var freezer=new RoomProject{Kind="Freezer"};
                dependencies.AddRange(new[]{kitchen,butcher,storage,research,freezer});
                Check(new[]{kitchen,butcher,storage,research,freezer}.All(r=>!Blocked(r,dependencies)),
                    "Unfinished spare bedroom blocked essential services despite enough completed bedrooms.");
                dependencies.First(r=>r.Kind=="Quarto" && r.Completed).Completed=false;
                Check(!Blocked(kitchen,dependencies) && !Blocked(butcher,dependencies) && !Blocked(storage,dependencies) && Blocked(research,dependencies),
                    "Food/storage dependency or occupied-colony shelter gate incorrect.");
                Log.Message("[ConstructionDependencyTests] PASS: spare bedrooms cannot block production; food/storage proceed while shelter is built.");
                var lampCell=GenRadial.RadialCellsAround(map.Center,45,true).First(c=>c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && !c.GetThingList(map).Any() && (c+IntVec3.North).InBounds(map) && !(c+IntVec3.North).GetThingList(map).Any());
                var lampRoom=new RoomProject();lampRoom.Furniture.Add(new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed("WallLamp"),Position=lampCell,Rotation=Rot4.North});
                Check(!RingBasePlanner.Validate(map,new List<RoomProject>{lampRoom}),"Unsupported lamp accepted.");
                lampRoom.Shell.Add(new ConstructionTask{Def=ThingDefOf.Wall,Stuff=ThingDefOf.WoodLog,Position=lampCell+IntVec3.North});
                Check(RingBasePlanner.Validate(map,new List<RoomProject>{lampRoom}),"Future supporting wall rejected.");
                lampRoom.Shell[0].CancelledByPlayer=true;Check(!RingBasePlanner.Validate(map,new List<RoomProject>{lampRoom}),"Cancelled support accepted.");
                Log.Message("[ConstructionDependencyTests] PASS: planned lamp support accepted; missing/cancelled support rejected.");
                var bedroom=new RoomProject{Kind="Quarto",LayoutSlot="mod:12:12:1",Origin=lampCell-new IntVec3(3,0,5),InteriorSize=5,Completed=true};
                foreach(var edge in bedroom.Footprint.EdgeCells)bedroom.Shell.Add(new ConstructionTask{Def=ThingDefOf.Wall,Stuff=ThingDefOf.WoodLog,Position=edge});
                var door=(Building)ThingMaker.MakeThing(ThingDefOf.Door,ThingDefOf.WoodLog);door.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(door,lampCell+IntVec3.North,map);
                var finish=new RoomProject{Kind="Acabamento",LayoutSlot="interior:"+bedroom.LayoutSlot};
                var oldLamp=new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed("WallLamp"),Position=lampCell,Rotation=Rot4.North};finish.Furniture.Add(oldLamp);
                var interiors=new List<RoomProject>{bedroom,finish};ModularInteriorPlanner.Plan(map,interiors);
                Check(oldLamp.Position!=lampCell && bedroom.Shell.Any(w=>w.Position==oldLamp.Position+IntVec3.North) && (oldLamp.Position+IntVec3.North).GetEdifice(map)==null,"Unissued lamp kept unsupported door as support.");
                oldLamp.Position=lampCell;oldLamp.CancelledByPlayer=true;ModularInteriorPlanner.Plan(map,interiors);Check(oldLamp.Position==lampCell,"Cancelled lamp plan relocated.");
                Log.Message("[ConstructionDependencyTests] PASS: unissued door-backed lamp relocated to a planned wall; cancelled lamp preserved.");
                bedroom.LayoutSlot="prison:cell:0";finish.LayoutSlot="prison:finish:0";oldLamp.CancelledByPlayer=false;ModularInteriorPlanner.ReconcileFurniture(map,interiors);
                Check(oldLamp.Position!=lampCell,"Saved prison lamp remained attached to an unsupported door.");
                Log.Message("[ConstructionDependencyTests] PASS: prison finishing also migrates unissued lamps away from unsupported doors.");
                Log.Message("[ConstructionDependencyTests] DONE: controlled contracts only; native checkpoint continuation required.");
            }
            catch(Exception ex){Log.Error("[ConstructionDependencyTests] FAIL: "+ex);}
        }
    }
}


