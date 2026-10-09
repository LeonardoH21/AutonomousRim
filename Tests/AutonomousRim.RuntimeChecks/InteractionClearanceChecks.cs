using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
namespace AutonomousRim.RuntimeChecks
{
    // Explicit disposable setup. Clearance and construction after setup are native jobs.
    public sealed class InteractionClearanceChecks:MapComponent
    {
        private bool started,done,moved;private int start;private Thing chunk;private IntVec3 original;
        private List<RoomProject> projects;private List<ConstructionOrder> orders=new List<ConstructionOrder>();
        private ConstructionTask stove;
        public InteractionClearanceChecks(Map map):base(map){}
        private void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);}
        private void Setup()
        {
            map.GetComponent<AutonomousRimMapComponent>().DisableAll();
            foreach(var pending in map.listerThings.AllThings.Where(t=>t is Blueprint_Build || t is Frame).ToList())pending.Destroy(DestroyMode.Cancel);
            var difficulty=DefDatabase<DifficultyDef>.GetNamed("Peaceful");Find.Storyteller.difficultyDef=difficulty;Find.Storyteller.difficulty.CopyFrom(difficulty);
            foreach(var p in map.mapPawns.AllPawnsSpawned.Where(p=>p.Faction!=Faction.OfPlayer).ToList()){p.DeSpawn();Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);}
            foreach(var hive in map.listerThings.AllThings.OfType<Hive>().ToList())hive.Destroy();
            foreach(var c in new CellRect(map.Center.x-12,map.Center.z-12,25,25))
            {foreach(var t in c.GetThingList(map).Where(t=>!(t is Pawn)).ToList()){if(t.def.destroyable)t.Destroy();else t.DeSpawn();}map.terrainGrid.SetTerrain(c,TerrainDefOf.Soil);map.fogGrid.Unfog(c);}
            foreach(var old in map.mapPawns.FreeColonistsSpawned.ToList()){old.DeSpawn();Find.WorldPawns.PassToWorld(old,PawnDiscardDecideMode.KeepForever);}
            for(int i=0;i<5;i++)
            {
                Pawn pawn;do{pawn=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);}while(pawn.health.hediffSet.hediffs.Count>0 || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction));
                foreach(var trait in pawn.story.traits.allTraits.ToList())pawn.story.traits.RemoveTrait(trait);
                GenSpawn.Spawn(pawn,map.Center+new IntVec3(-6,0,i-2),map);
                for(int hour=0;hour<24;hour++)pawn.timetable.SetAssignment(hour,TimeAssignmentDefOf.Anything);
            }
            foreach(var pawn in map.mapPawns.FreeColonistsSpawned.ToList())
            {if(pawn.needs.food!=null)pawn.needs.food.CurLevel=1;if(pawn.needs.rest!=null)pawn.needs.rest.CurLevel=1;if(pawn.needs.joy!=null)pawn.needs.joy.CurLevel=1;foreach(var s in pawn.skills.skills)s.Level=20;pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized();foreach(var w in DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w=>!pawn.WorkTypeIsDisabled(w)))pawn.workSettings.SetPriority(w,3);}
            foreach(string name in new[]{"Steel","WoodLog"})for(int i=0;i<2;i++){var t=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name));t.stackCount=75;GenSpawn.Spawn(t,map.Center+new IntVec3(i*2,0,name=="Steel"?-5:-7),map);t.SetForbidden(false,false);}
            stove=new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed("FueledStove"),Position=map.Center,Rotation=Rot4.North};
            original=ThingUtility.InteractionCellsWhenAt((ThingDef)stove.Def,stove.Position,stove.Rotation,map).First();
            chunk=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ChunkGranite"));GenSpawn.Spawn(chunk,original,map);chunk.SetForbidden(false,false);
            projects=new List<RoomProject>{new RoomProject{Kind="Cozinha",Started=true,RequiresRoof=false,Origin=map.Center-new IntVec3(4,0,4),InteriorSize=9,Furniture=new List<ConstructionTask>{stove}}};
            Check(!GenConstruct.CanPlaceBlueprintAt(stove.Def,stove.Position,stove.Rotation,map),"Chunk failed to block the actual interaction cell.");
            Check(map.mapPawns.FreeColonistsSpawned.Any(p=>ConstructionWorkManager.CanDispatch(p,WorkTypeDefOf.Hauling)),"Controlled fixture lacks an eligible hauler before protection guards.");
            foreach(var pawn in map.mapPawns.FreeColonistsSpawned.ToList())pawn.drafter.Drafted=true;
            ConstructionWorkManager.Apply(map,projects,orders);Check(orders.Count==0,"Manual draft overwritten by clearance.");
            foreach(var pawn in map.mapPawns.FreeColonistsSpawned.ToList())pawn.drafter.Drafted=false;
            // Native granite chunks have no forbiddable component. Use a real
            // human corpse for this separate controlled forbidden-obstacle guard.
            chunk.DeSpawn();var fixturePawn=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfAncientsHostile);
            GenSpawn.Spawn(fixturePawn,original,map);fixturePawn.Kill(null);var forbiddenCorpse=fixturePawn.Corpse;
            Check(forbiddenCorpse?.Spawned==true,"Corpse fixture did not spawn.");forbiddenCorpse.SetForbidden(true,false);
            Check(forbiddenCorpse.IsForbidden(Faction.OfPlayer),"Forbidden guard fixture must actually be forbiddable.");
            ConstructionWorkManager.Apply(map,projects,orders);Check(orders.Count==0 && forbiddenCorpse.IsForbidden(Faction.OfPlayer),"Forbidden obstacle moved or allowed by clearance.");
            forbiddenCorpse.Destroy();GenSpawn.Spawn(chunk,original,map);
            Log.Message("[InteractionClearanceTests] PASS: actual blocked interaction; manual draft and forbidden item preserved.");
            start=Find.TickManager.TicksGame;started=true;
        }
        public override void MapComponentTick()
        {
            if(done || !GenCommandLine.CommandLineArgPassed("autonomousriminteractionclearancetest") || Find.TickManager.TicksGame<300)return;
            try
            {
                if(!started){Setup();return;}
                if(Find.TickManager.TicksGame%30!=0)return;
                if((Find.TickManager.TicksGame-start)%3000==0){Log.Message("[InteractionClearanceTests] PROGRESS chunk="+chunk.PositionHeld+"; state="+projects[0].State+"; reason="+projects[0].BlockReason+"; orders="+orders.Count+"; jobs="+string.Join(";",map.mapPawns.FreeColonistsSpawned.Select(p=>p.LabelShort+":"+p.Position+":"+p.CurJob?.def.defName+":haul="+ConstructionWorkManager.CanDispatch(p,WorkTypeDefOf.Hauling)+":canAct="+EquipmentManager.CanAct(p)+":canWork="+WorkPriorityManager.CanWork(p)+":initialized="+p.workSettings.Initialized+":prio="+p.workSettings.GetPriority(WorkTypeDefOf.Hauling)+":food="+p.needs.food.CurCategory+":schedule="+p.timetable.CurrentAssignment+":joyKind="+p.CurJob?.def.joyKind+":interrupt="+p.CurJob?.def.playerInterruptible)));GameDataSaveLoader.SaveGame("InteractionClearanceCheckpoint");}
                BaseConstructionManager.Apply(map,projects);ConstructionWorkManager.Apply(map,projects,orders);
                Check(!chunk.Destroyed,"Clearance destroyed the obstacle instead of hauling it.");
                if(!moved && chunk.Spawned && chunk.Position!=original && !ThingUtility.InteractionCellsWhenAt((ThingDef)stove.Def,stove.Position,stove.Rotation,map).Contains(chunk.Position))
                {Check(orders.Any(o=>o.Target==chunk),"No native owned clearance job observed.");moved=true;Log.Message("[InteractionClearanceTests] PASS: same granite chunk moved by native haul job outside the workstation footprint/interaction.");}
                if(moved && stove.Complete(map)){GameDataSaveLoader.SaveGame("InteractionClearanceComplete");done=true;Log.Message("[InteractionClearanceTests] DONE: native clearance unblocked blueprint, delivery and completed stove construction.");return;}
                Check(Find.TickManager.TicksGame-start<30000,"Clearance/construction failed to complete within twelve hours.");
            }
            catch(Exception ex){done=true;GameDataSaveLoader.SaveGame("InteractionClearanceFailure");Log.Error("[InteractionClearanceTests] FAIL: "+ex);}
        }
    }
}
