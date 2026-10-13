using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using RimWorld;
using Verse;
namespace AutonomousRim.RuntimeChecks
{
    public sealed class L1ConstructionProgress:MapComponent
    {
        private static bool requested;private bool started,done;private int first;
        private Dictionary<string,int> initial;private Dictionary<int,IntVec3> beds;private List<int> colonists;
        public L1ConstructionProgress(Map map):base(map){}
        public override void MapComponentUpdate()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimloneprogress") || Current.ProgramState!=ProgramState.Playing)return;
            if(!requested){requested=true;GameDataSaveLoader.LoadGame("ModularResume");return;}
            foreach(var window in Find.WindowStack.Windows.Where(w=>w.forcePause).ToList())window.Close(false);Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
        }
        public override void MapComponentTick()
        {
            if(done || !requested || !GenCommandLine.CommandLineArgPassed("autonomousrimloneprogress") || Find.TickManager.TicksGame<373671)return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                var services=ai.BaseProjects.Where(p=>p.Kind=="Freezer" || p.Kind=="Energia e climatização").ToList();
                if(!started)
                {
                    started=true;first=Find.TickManager.TicksGame;
                    if(map.mapPawns.FreeColonistsSpawnedCount!=8)throw new Exception("Not L1 population");
                    colonists=map.mapPawns.FreeColonistsSpawned.Select(p=>p.thingIDNumber).ToList();
                    beds=map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Where(b=>!b.ForPrisoners&&!b.Medical).ToDictionary(b=>b.thingIDNumber,b=>b.Position);
                    initial=services.ToDictionary(p=>p.Kind,p=>BaseConstructionManager.Tasks(p).Count(t=>t.Complete(map)));
                    var peaceful=DefDatabase<DifficultyDef>.GetNamed("Peaceful");Find.Storyteller.difficultyDef=peaceful;Find.Storyteller.difficulty.CopyFrom(peaceful);
                    Log.Message("[L1Construction] START: original eight pawns, normal jobs/resources, 3x, peaceful isolated profile");return;
                }
                int elapsed=Find.TickManager.TicksGame-first;
                if(elapsed%6000==0)foreach(var service in services)Log.Message("[L1Construction] PROGRESS "+elapsed+" "+service.Kind+" "+service.State+" tasks="+BaseConstructionManager.Tasks(service).Count(t=>t.Complete(map))+" initial="+initial[service.Kind]+" "+service.BlockReason);
                if(elapsed<30000)return;
                if(colonists.Any(id=>!map.mapPawns.FreeColonistsSpawned.Any(p=>p.thingIDNumber==id&&!p.Dead)))throw new Exception("Original colony lost a member");
                if(beds.Any(pair=>!map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Any(b=>b.thingIDNumber==pair.Key&&b.Position==pair.Value)))throw new Exception("Player bedrooms changed");
                foreach(var service in services)
                {
                    int complete=BaseConstructionManager.Tasks(service).Count(t=>t.Complete(map));
                    if(complete<=initial[service.Kind])throw new Exception("No actual construction progress: "+service.Kind+" "+service.State+" "+service.BlockReason);
                    Log.Message("[L1Construction] PASS: "+service.Kind+" actual native tasks "+initial[service.Kind]+" -> "+complete+" / "+BaseConstructionManager.Tasks(service).Count()+"; state="+service.State+"; "+service.BlockReason);
                }
                Log.Message("[L1Construction] PASS: original eight colonists alive and manual bedrooms preserved for 12 game hours.");
                GameDataSaveLoader.SaveGame("AutonomousRim_L1_12h_Corrigido");done=true;Log.Message("[L1Construction] DONE");
            }
            catch(Exception error){done=true;GameDataSaveLoader.SaveGame("L1ConstructionFailure");Log.Error("[L1Construction] FAIL: "+error);}
        }
    }
}
