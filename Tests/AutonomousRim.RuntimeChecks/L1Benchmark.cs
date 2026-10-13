using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using HarmonyLib;
using RimWorld;
using Verse;
namespace AutonomousRim.RuntimeChecks
{
    [StaticConstructorOnStartup]
    public static class L1BenchmarkTiming
    {
        static L1BenchmarkTiming()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimlonebenchmark"))return;
            var harmony=new Harmony("autonomousrim.l1.benchmark");
            foreach(var type in new[]{typeof(BaseConstructionManager),typeof(ConstructionWorkManager),typeof(WorkGiver_AutonomousConstruction),typeof(ConstructionResourceManager),typeof(StoragePolicy),typeof(LootAccessManager)})
                foreach(var method in type.GetMethods(BindingFlags.DeclaredOnly|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance)
                    .Where(m=>!m.IsAbstract && m.GetMethodBody()!=null && new[]{"Apply","CanDispatch","ConstructionDebrisJob","InteractionClearance","JobOnThing","GetPriority","PendingReason","ManageFoodStorage"}.Contains(m.Name)))
                    harmony.Patch(method,prefix:new HarmonyMethod(typeof(ModuleTimingPatch),nameof(ModuleTimingPatch.Prefix)),finalizer:new HarmonyMethod(typeof(ModuleTimingPatch),nameof(ModuleTimingPatch.Finalizer)));
        }
    }
    public sealed class L1Benchmark:MapComponent
    {
        private static bool requested;private bool started,done;private int first,firstFrame;private Stopwatch clock;private System.Collections.Generic.Dictionary<int,IntVec3> bedrooms;
        public L1Benchmark(Map map):base(map){}
        public override void MapComponentUpdate()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimlonebenchmark") || Current.ProgramState!=ProgramState.Playing)return;
            if(!requested){requested=true;GameDataSaveLoader.LoadGame("ModularResume");return;}
            foreach(var window in Find.WindowStack.Windows.Where(w=>w.forcePause).ToList())window.Close(false);
            Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
        }
        public override void MapComponentTick()
        {
            if(done || !requested || !GenCommandLine.CommandLineArgPassed("autonomousrimlonebenchmark") || Find.TickManager.TicksGame<373671)return;
            var ai=map.GetComponent<AutonomousRimMapComponent>();
            if(!started)
            {
                if(map.mapPawns.FreeColonistsSpawnedCount!=8){done=true;Log.Error("[L1Benchmark] FAIL: not original L1 population");return;}
                started=true;first=Find.TickManager.TicksGame;bedrooms=map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Where(b=>!b.ForPrisoners&&!b.Medical).ToDictionary(b=>b.thingIDNumber,b=>b.Position);
                if(GenCommandLine.CommandLineArgPassed("autonomousrimloneoff"))ai.DisableAll();
                foreach(var bed in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Where(b=>!b.ForPrisoners&&!b.Medical))
                    Log.Message("[L1Benchmark] BED "+bed.thingIDNumber+" at "+bed.Position+" role="+bed.GetRoom()?.Role+" outdoors="+bed.GetRoom()?.PsychologicallyOutdoors);
                foreach(var project in ai.BaseProjects.Where(p=>p.Kind=="Quarto" && !p.Completed))
                    foreach(var task in BaseConstructionManager.Tasks(project).Where(t=>t.WasCompleted && !t.Complete(map)))Log.Message("[L1Benchmark] MISSING "+project.LayoutSlot+" "+task.Def+" at "+task.Position);
                PerformanceTelemetry.Reset();PerformanceTelemetry.Enabled=true;clock=Stopwatch.StartNew();firstFrame=UnityEngine.Time.frameCount;
                Log.Message("[L1Benchmark] START tick="+first+" things="+map.listerThings.AllThings.Count+" projects="+ai.BaseProjects.Count+" off="+GenCommandLine.CommandLineArgPassed("autonomousrimloneoff"));return;
            }
            if(Find.TickManager.TicksGame-first<2000)return;
            Log.Message("[L1Benchmark] FPS "+((UnityEngine.Time.frameCount-firstFrame)/clock.Elapsed.TotalSeconds).ToString("0.0")+" resolution="+UnityEngine.Screen.width+"x"+UnityEngine.Screen.height+" batch="+UnityEngine.Application.isBatchMode);
            Log.Message("[L1Benchmark] TPS "+(2000/clock.Elapsed.TotalSeconds).ToString("0.0")+" elapsed="+clock.Elapsed.TotalSeconds.ToString("0.00"));
            foreach(var row in PerformanceTelemetry.Snapshot())Log.Message("[L1Benchmark] COST "+row.Module+" calls="+row.Calls+" totalMs="+row.TotalMs.ToString("0.0")+" meanMs="+row.MeanMs.ToString("0.00")+" p95="+row.P95Ms.ToString("0.00"));
            foreach(var project in ai.BaseProjects.Where(p=>p.Kind=="Freezer" || p.Kind=="Energia e climatização"))Log.Message("[L1Benchmark] SERVICE "+project.Kind+" "+project.State+" "+project.BlockReason);
            if(GenCommandLine.CommandLineArgPassed("autonomousrimloneverify"))
            {
                try
                {
                    if(ai.Housing.Count(map)<8)throw new Exception("Existing rooms not counted");
                    foreach(string slot in new[]{"mod:1:-1:1","mod:1:-1:4","mod:1:-1:8"})
                        if(!ai.BaseProjects.Any(p=>p.LayoutSlot==slot && p.HousingRecognized && p.Completed))throw new Exception("Modified bedroom not adopted: "+slot);
                    if(bedrooms.Any(pair=>!map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Any(b=>b.thingIDNumber==pair.Key && b.Position==pair.Value)))throw new Exception("Player bedroom furniture moved or removed");
                    foreach(var project in ai.BaseProjects.Where(p=>p.Kind=="Freezer" || p.Kind=="Energia e climatização"))
                        if(project.BlockReason?.Contains("quartos")==true)throw new Exception("Essential infrastructure still waiting for bedroom template");
                    Log.Message("[L1Benchmark] PASS: 8 existing bedrooms; three changed layouts adopted; original beds preserved; freezer/power no longer wait for bedrooms.");
                }
                catch(Exception error){done=true;Log.Error("[L1Benchmark] FAIL: "+error);return;}
            }
            PerformanceTelemetry.Enabled=false;done=true;Log.Message("[L1Benchmark] DONE");
        }
    }
}
