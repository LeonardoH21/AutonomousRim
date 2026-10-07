using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    // Observer only after the explicitly permitted five-colonist/skill-20 setup.
    // Never changes resources, health, needs, research, terrain, frame work or buildings.
    public sealed class CourtyardConstructionTrial : MapComponent
    {
        private bool started, finished, loadRequested;
        private int startTick, lastSave, lastReport;
        private float lastPause;
        private List<Pawn> startingColonists = new List<Pawn>();
        public CourtyardConstructionTrial(Map map):base(map){}
        public override void ExposeData()
        {
            Scribe_Values.Look(ref started,"courtyardStarted");Scribe_Values.Look(ref finished,"courtyardFinished");
            Scribe_Values.Look(ref startTick,"courtyardStartTick");Scribe_Values.Look(ref lastSave,"courtyardLastSave");
            Scribe_Collections.Look(ref startingColonists,"courtyardStartingColonists",LookMode.Reference);
            if(Scribe.mode==LoadSaveMode.PostLoadInit){loadRequested=true;if(!started)finished=false;}
        }
        public override void MapComponentUpdate()
        {
            if(!started||finished||!GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardtrial")||
                !Find.TickManager.Paused&&Find.TickManager.CurTimeSpeed==TimeSpeed.Superfast||
                UnityEngine.Time.realtimeSinceStartup-lastPause<5f)return;
            lastPause=UnityEngine.Time.realtimeSinceStartup;
            Log.Message("[AutonomousRim.CourtyardTrial] RESUME: tick="+Find.TickManager.TicksGame+"; paused="+Find.TickManager.Paused+
                "; speed="+Find.TickManager.CurTimeSpeed+"; windows="+string.Join(",",Find.WindowStack.Windows.Where(w=>w.forcePause).Select(w=>w.GetType().Name)));
            foreach(var w in Find.WindowStack.Windows.Where(w=>w.forcePause).ToList())
            {
                if(w is Dialog_NamePlayerFactionAndSettlement naming)
                {
                    AccessTools.Method(typeof(Dialog_NamePlayerFactionAndSettlement),"Named").Invoke(naming,new object[]{AccessTools.Field(typeof(Dialog_GiveName),"curName").GetValue(naming)});
                    AccessTools.Method(typeof(Dialog_NamePlayerFactionAndSettlement),"NamedSecond").Invoke(naming,new object[]{AccessTools.Field(typeof(Dialog_GiveName),"curSecondName").GetValue(naming)});
                    naming.Close(false);
                }
                else if(w is Dialog_NodeTree dialogue)
                {
                    var node=(DiaNode)AccessTools.Field(typeof(Dialog_NodeTree),"curNode").GetValue(dialogue);
                    var exit=node.options.FirstOrDefault(o=>!o.disabled&&o.resolveTree&&o.link==null&&o.linkLateBind==null&&o.action==null);
                    if(exit!=null)AccessTools.Method(typeof(DiaOption),"Activate").Invoke(exit,null);
                    else {Log.Warning("[AutonomousRim.CourtyardTrial] Needs player decision: "+w.GetType().Name);return;}
                }
                else w.Close(false);
            }
            Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
        }
        public override void MapComponentTick()
        {
            if(finished||!GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardtrial")||Find.TickManager.TicksGame%120!=0)return;
            if(!started&&!loadRequested&&(GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardresume")||GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardfromfailure")||GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardfromstart")))
            {loadRequested=true;GameDataSaveLoader.LoadGame(GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardfromfailure")?"CourtyardFailure":GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardfromstart")?"CourtyardStart":"CourtyardCheckpoint");return;}
            loadRequested=true;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                int tick=Find.TickManager.TicksGame;
                if(startingColonists==null)startingColonists=new List<Pawn>();
                if(started&&startingColonists.Count==0)startingColonists.AddRange(map.mapPawns.FreeColonistsSpawned);
                var checkpointRequest=Path.Combine(GenFilePaths.SaveDataFolderPath,"checkpoint-request.txt");
                if(started&&File.Exists(checkpointRequest))
                {
                    lastSave=tick;Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
                    GameDataSaveLoader.SaveGame("CourtyardCheckpoint");File.Delete(checkpointRequest);
                    finished=true;Log.Message("[AutonomousRim.CourtyardTrial] CHECKPOINT: saved and paused for a normal restart.");return;
                }
                if(started&&!ai.BaseAutomation)
                {
                    var projects=(List<RoomProject>)AccessTools.Field(typeof(AutonomousRimMapComponent),"baseProjects").GetValue(ai);
                    if(projects.SelectMany(BaseConstructionManager.Tasks).All(t=>!t.Issued))
                    {
                        var anchor=projects.First(p=>p.Kind==CourtyardBasePlanner.ReservationKind).LayoutAnchor;
                        var revised=CourtyardBasePlanner.Create(map,anchor,14);
                        if(!RingBasePlanner.Validate(map,revised))throw new InvalidOperationException("Refined initial drawing placement failed");
                        CheckGeometry(revised);projects.Clear();projects.AddRange(revised);
                    }
                    if(GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardgeometry"))
                    {finished=true;Log.Message("[AutonomousRim.CourtyardTrial] GEOMETRY PASS: revised full drawing, terrain, shared partitions, furniture and floor checks.");return;}
                    ai.SetBaseAutomation(true);
                    Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
                    Log.Message("[AutonomousRim.CourtyardTrial] CONSTRUCTION: saved five-colonist start resumed with native blueprints and normal jobs.");
                }
                if(!started)
                {
                    if(tick<120||map.mapPawns.FreeColonistsSpawnedCount==0)return;
                    ai.SetBaseAutomation(false);
                    while(map.mapPawns.FreeColonistsSpawnedCount<5)
                    {
                        var pawn=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);
                        var cell=CellFinder.RandomClosewalkCellNear(map.mapPawns.FreeColonistsSpawned.First().Position,map,6);
                        GenSpawn.Spawn(pawn,cell,map);
                    }
                    if(map.mapPawns.FreeColonistsSpawnedCount!=5)throw new InvalidOperationException("Expected five starting colonists");
                    foreach(var pawn in map.mapPawns.FreeColonistsSpawned)
                    {
                        foreach(var skill in pawn.skills.skills){skill.Level=20;skill.xpSinceLastLevel=0;}
                        pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
                    }
                    var preset=DefDatabase<DifficultyDef>.GetNamed("Peaceful");
                    Find.Storyteller.difficultyDef=preset;Find.Storyteller.difficulty.CopyFrom(preset);
                    var projects=(List<RoomProject>)AccessTools.Field(typeof(AutonomousRimMapComponent),"baseProjects").GetValue(ai);
                    projects.Clear();ai.PreviewBase();
                    var reservation=projects.FirstOrDefault(p=>p.Kind==CourtyardBasePlanner.ReservationKind);
                    if(reservation==null)throw new InvalidOperationException("No valid firm-ground site: "+ai.BaseStatus);
                    var full=CourtyardBasePlanner.Create(map,reservation.LayoutAnchor,14);
                    if(!RingBasePlanner.Validate(map,full))throw new InvalidOperationException("Full drawing cannot be placed");
                    CheckGeometry(full);
                    projects.Clear();projects.AddRange(full);
                    ai.SetAutomation(true,true);ai.SetLootAutomation(true);ai.SetEquipmentAutomation(true);
                    ai.SetScheduleAutomation(true);ai.SetStrategyAutomation(true);ai.SetEmergencyAutomation(true);ai.SetCombatAutomation(true);
                    started=true;startTick=tick;lastSave=tick;
                    startingColonists.AddRange(map.mapPawns.FreeColonistsSpawned);
                    Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
                    Log.Message("[AutonomousRim.CourtyardTrial] START: five skill-20 colonists; native Crashlanded resources/research/needs/work; Peaceful; third speed; full 14-bedroom drawing. No building/material cheats.");
                    GameDataSaveLoader.SaveGame("CourtyardStart");
                    if(GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardgeometry"))
                    {finished=true;Log.Message("[AutonomousRim.CourtyardTrial] GEOMETRY PASS: exact dimensions, unique shared partitions, native furniture placement and interactions.");return;}
                    ai.SetBaseAutomation(true);
                }
                if(tick-lastReport>=6000)
                {
                    lastReport=tick;
                    var unique=ai.BaseProjects.SelectMany(BaseConstructionManager.Tasks).GroupBy(t=>new{t.Def,t.Position,t.Rotation,t.Stuff}).Select(g=>g.First()).ToList();
                    Log.Message("[AutonomousRim.CourtyardTrial] PROGRESS: day="+((tick-startTick)/60000f).ToString("0.00")+
                        "; tasks="+unique.Count(t=>t.Complete(map))+"/"+unique.Count+"; projects="+ai.BaseProjects.Count(p=>p.Completed)+"/"+ai.BaseProjects.Count+
                        "; speed="+Find.TickManager.CurTimeSpeed+"; foodDays="+ai.CurrentState?.EstimatedFoodDays.ToString("0.0")+"; "+ai.BaseStatus+"; "+ai.GatheringStatus+
                        "; pawns="+string.Join(" | ",map.mapPawns.FreeColonistsSpawned.Select(p=>p.LabelShort+":"+p.CurJobDef?.defName+"/rest="+(p.needs?.rest?.CurLevelPercentage??0).ToString("0.00"))));
                    foreach(var p in ai.BaseProjects.Where(p=>!p.Completed&&p.BlockReason!=null))Log.Message("[AutonomousRim.CourtyardTrial] WAIT: "+p.LayoutSlot+"/"+p.Kind+" "+p.BlockReason);
                }
                if(tick-lastSave>=6000)
                {lastSave=tick;GameDataSaveLoader.SaveGame("CourtyardCheckpoint");}
                var all=ai.BaseProjects.SelectMany(BaseConstructionManager.Tasks).ToList();
                bool done=all.Count>0&&all.All(t=>t.Complete(map))&&ai.BaseProjects.Where(p=>p.RequiresRoof).All(p=>p.RoofArea.All(c=>c.Roofed(map)))&&
                    ai.BaseProjects.Where(p=>p.Crop!=null).All(p=>p.GrowingZone!=null)&&ai.BaseProjects.Where(p=>p.Kind=="Estoque"||p.Kind=="Freezer"||p.Kind=="Medicamentos"||p.Kind=="Despejo"||p.Kind=="Roupas"||p.Kind=="Armas").All(p=>p.Stockpile!=null);
                if(done)
                {
                    if(startingColonists.Count!=5||startingColonists.Any(p=>p==null||p.Dead||p.Downed||!p.Spawned||p.Map!=map))throw new InvalidOperationException("Construction complete but starting colony not intact");
                    finished=true;Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;GameDataSaveLoader.SaveGame("CourtyardFinished");
                    File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"courtyard-result.txt"),"All "+all.Count+" planned task references verified against real objects/terrain; roofs and zones verified. Elapsed ticks="+(tick-startTick));
                    Log.Message("[AutonomousRim.CourtyardTrial] PASS: every planned wall, furniture and floor exists; native construction; final save CourtyardFinished.");
                }
            }
            catch(Exception e)
            {finished=true;Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;GameDataSaveLoader.SaveGame("CourtyardFailure");Log.Error("[AutonomousRim.CourtyardTrial] FAIL: "+e);}
        }
        private static void CheckGeometry(List<RoomProject> p)
        {
            if(p.Count(r=>r.Kind=="Quarto"&&r.InteriorSize==6&&r.Height==6)!=14)throw new InvalidOperationException("Bedroom sizes");
            if(p.Any(r=>(r.Kind=="Cozinha"||r.Kind=="Abate")&&(r.InteriorSize!=4||r.Height!=4)))throw new InvalidOperationException("Kitchen/butcher sizes");
            var battery=p.First(r=>r.Kind=="Baterias");if(battery.InteriorSize!=4||battery.Height!=8)throw new InvalidOperationException("Battery room size");
            var shell=p.SelectMany(r=>r.Shell).GroupBy(t=>t.Position).ToList();
            if(shell.Any(g=>g.Select(t=>new{t.Def,t.Stuff,t.Rotation}).Distinct().Count()!=1))throw new InvalidOperationException("Conflicting shared boundary");
            var walls=new HashSet<IntVec3>(shell.Select(g=>g.Key));
            // The antecâmara partitions are deliberate interior walls in the freezer.
            if(p.Where(r=>r.RequiresRoof&&r.Kind!="Freezer"&&r.Kind!="Corredor").SelectMany(r=>r.Interior).Any(walls.Contains))throw new InvalidOperationException("Wall/interior collision");
            if(!shell.Any(g=>g.Count()>1))throw new InvalidOperationException("No shared boundaries");
        }
    }
}
