using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    // Disposable initial facilities test native opportunistic hauling. The six
    // original enhanced profiles and world factors remain unchanged.
    public sealed class QueuedConstructionChecks:MapComponent
    {
        private static bool loaded;
        private bool started,done;
        private int start;
        private Pawn worker;
        private Frame frame;
        private Thing hauled;
        private IntVec3 destination;
        private List<ConstructionOrder> orders=new List<ConstructionOrder>();
        public QueuedConstructionChecks(Map map):base(map){}
        private void Check(bool valid,string message){if(!valid)throw new Exception(message);}
        public override void MapComponentUpdate()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimqueuedconstructiontest"))return;
            if(!loaded){loaded=true;GameDataSaveLoader.LoadGame("ModularResume");return;}
            if(started && !done)Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
        }
        private void Setup()
        {
            Check(map.mapPawns.FreeColonistsSpawnedCount==6 && Current.Game.Scenario.name=="AutonomousRim Enhanced World","Six original enhanced colonists required");
            map.GetComponent<AutonomousRimMapComponent>().DisableAll();
            foreach(var cell in new CellRect(map.Center.x-15,map.Center.z-5,31,11))
            {
                foreach(var thing in cell.GetThingList(map).Where(t=>!(t is Pawn)).ToList())if(thing.def.destroyable)thing.Destroy();
                map.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);map.fogGrid.Unfog(cell);
            }
            worker=map.mapPawns.FreeColonistsSpawned.Single(p=>p.Name.ToStringShort=="Alpha");
            foreach(var p in map.mapPawns.FreeColonistsSpawned)
            {
                p.jobs.StopAll();p.drafter.Drafted=true;
                if(p.Spawned)p.DeSpawn();GenSpawn.Spawn(p,map.Center+new IntVec3(-12,0,p==worker?0:3),map);
            }
            worker.drafter.Drafted=false;
            worker.workSettings.SetPriority(WorkTypeDefOf.Hauling,1);worker.workSettings.SetPriority(WorkTypeDefOf.Construction,1);
            for(int h=0;h<24;h++)worker.timetable.SetAssignment(h,TimeAssignmentDefOf.Work);
            var zone=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager);zone.settings.filter.SetDisallowAll();zone.settings.filter.SetAllow(ThingDefOf.WoodLog,true);
            zone.settings.Priority=StoragePriority.Critical;map.zoneManager.RegisterZone(zone);destination=map.Center+new IntVec3(7,0,0);zone.AddCell(destination);
            hauled=ThingMaker.MakeThing(ThingDefOf.WoodLog);hauled.stackCount=10;GenSpawn.Spawn(hauled,map.Center+new IntVec3(-10,0,0),map);hauled.SetForbidden(false,false);
            frame=(Frame)ThingMaker.MakeThing(ThingDefOf.Wall.frameDef,ThingDefOf.WoodLog);frame.SetFaction(Faction.OfPlayer);
            var materials=ThingMaker.MakeThing(ThingDefOf.WoodLog);materials.stackCount=5;frame.resourceContainer.TryAdd(materials);
            GenSpawn.Spawn(frame,map.Center+new IntVec3(10,0,0),map);
            var job=JobMaker.MakeJob(JobDefOf.FinishFrame,frame);
            bool accepted=(bool)AccessTools.Method(typeof(ConstructionWorkManager),"Start").Invoke(null,new object[]{worker,job,DefDatabase<WorkGiverDef>.GetNamed("ConstructFinishFrames"),frame,orders});
            Check(accepted && worker.CurJob?.def==JobDefOf.HaulToCell && worker.jobs.jobQueue.Contains(job),"Native prefix must be accepted with construction queued");
            Check(orders.Count==1 && orders[0].Pending,"Queued ownership not recorded");
            var original=worker.CurJob.GetUniqueLoadID();
            ConstructionWorkManager.Apply(map,new List<AutonomousRim.Planning.RoomProject>(),orders);
            Check(orders[0].Pending && worker.CurJob.GetUniqueLoadID()==original,"Apply interrupted prefix or cleared queued ownership");
            Log.Message("[QueuedConstructionTests] PASS: real native opportunistic hauling accepted; queued construction ownership survives Apply without another order.");
            start=Find.TickManager.TicksGame;started=true;
        }
        public override void MapComponentTick()
        {
            if(!loaded || done || !GenCommandLine.CommandLineArgPassed("autonomousrimqueuedconstructiontest") || Find.TickManager.TicksGame<300)return;
            try
            {
                if(!started){Setup();return;}
                if(frame.Destroyed && frame.Position.GetEdifice(map)?.def==ThingDefOf.Wall)
                {
                    Check(destination.GetThingList(map).Any(t=>t.def==ThingDefOf.WoodLog && t.stackCount>=10),"Opportunistic hauling did not actually store its wood");
                    var owned=JobMaker.MakeJob(JobDefOf.Wait,3000);var manual=JobMaker.MakeJob(JobDefOf.Wait,3000);manual.playerForced=true;
                    worker.jobs.jobQueue.EnqueueLast(owned);worker.jobs.jobQueue.EnqueueLast(manual);
                    orders.Add(new ConstructionOrder{Pawn=worker,JobId=owned.GetUniqueLoadID(),Pending=true});
                    ConstructionWorkManager.Stop(orders);
                    Check(!worker.jobs.jobQueue.Contains(owned) && worker.jobs.jobQueue.Contains(manual),"Stop must remove only queued owned jobs and preserve manual queue");
                    GameDataSaveLoader.SaveGame("QueuedConstructionComplete");done=true;Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
                    Log.Message("[QueuedConstructionTests] DONE: wood stored and wall built by native jobs; disable removes queued owned order and preserves manual order; original six profiles retained.");
                }
                Check(Find.TickManager.TicksGame-start<15000,"Native prefix/construction did not finish within six hours");
            }
            catch(Exception e){done=true;GameDataSaveLoader.SaveGame("QueuedConstructionFailure");Log.Error("[QueuedConstructionTests] FAIL: "+e);}
        }
    }
}
