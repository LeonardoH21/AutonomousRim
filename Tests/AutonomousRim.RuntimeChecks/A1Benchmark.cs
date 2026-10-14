using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;
namespace AutonomousRim.RuntimeChecks
{
    [StaticConstructorOnStartup]
    public static class A1BenchmarkTiming
    {
        static A1BenchmarkTiming()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimaonebenchmark"))return;
            var harmony=new Harmony("autonomousrim.a1.benchmark");
            foreach(var type in new[]{typeof(BaseConstructionManager),typeof(ConstructionWorkManager),typeof(WorkGiver_AutonomousConstruction),typeof(ConstructionResourceManager),typeof(StoragePolicy),typeof(LootAccessManager),typeof(ExistingFacilities),typeof(ExistingFacilityImprovements)})
                foreach(var method in type.GetMethods(BindingFlags.DeclaredOnly|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance)
                    .Where(m=>!m.IsAbstract && m.GetMethodBody()!=null && new[]{"Apply","CanDispatch","ConstructionDebrisJob","InteractionClearance","JobOnThing","GetPriority","PendingReason","ManageFoodStorage","Refresh","Reconcile","Plan"}.Contains(m.Name)))
                    harmony.Patch(method,prefix:new HarmonyMethod(typeof(ModuleTimingPatch),nameof(ModuleTimingPatch.Prefix)),finalizer:new HarmonyMethod(typeof(ModuleTimingPatch),nameof(ModuleTimingPatch.Finalizer)));
        }
    }
    public sealed class A1Benchmark:MapComponent
    {
        private static bool requested;private bool started,done;private int first,firstFrame;private bool measuring;private Stopwatch clock;private System.Collections.Generic.Dictionary<int,IntVec3> bedrooms;
        public A1Benchmark(Map map):base(map){}
        public override void MapComponentUpdate()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimaonebenchmark") || Current.ProgramState!=ProgramState.Playing)return;
            if(!requested){requested=true;GameDataSaveLoader.LoadGame("ModularResume");return;}
            foreach(var window in Find.WindowStack.Windows.Where(w=>w.forcePause).ToList())window.Close(false);
            Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
        }
        private void VerifyEquipment()
        {
            var core=typeof(EquipmentPlanner).GetMethod("PlanCore",BindingFlags.NonPublic|BindingFlags.Static);
            if(core==null)return; // Original binary used as the controlled baseline.
            Action compare=()=>
            {
                foreach(bool forbidden in new[]{false,true})
                {
                    var uncached=(System.Collections.Generic.List<EquipmentDecision>)core.Invoke(null,new object[]{map,new System.Collections.Generic.List<ManagedApparel>(),forbidden});
                    var cached=EquipmentPlanner.Plan(map,new System.Collections.Generic.List<ManagedApparel>(),forbidden);
                    if(uncached.Count!=cached.Count || uncached.Where((d,i)=>d.Pawn!=cached[i].Pawn || d.Item!=cached[i].Item || d.Wear!=cached[i].Wear || Math.Abs(d.CandidateScore-cached[i].CandidateScore)>0.0001 || Math.Abs(d.CurrentScore-cached[i].CurrentScore)>0.0001).Any())
                        throw new Exception("[A1Benchmark] FAIL: equipment decisions differ from uncached evaluation");
                }
            };
            compare();
            var item=map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel).OfType<Apparel>().FirstOrDefault(a=>a.def.useHitPoints);
            if(item!=null){int hp=item.HitPoints;bool forbidden=item.IsForbidden(Faction.OfPlayer);try{item.HitPoints=1;item.SetForbidden(!forbidden,false);compare();}finally{item.HitPoints=hp;item.SetForbidden(forbidden,false);}}
            compare();
            Log.Message("[A1Benchmark] PASS: cached and uncached equipment decisions identical, including changed condition and allow state; evaluation restored");
        }
        public override void MapComponentTick()
        {
            if(done || !requested || !GenCommandLine.CommandLineArgPassed("autonomousrimaonebenchmark") || Find.TickManager.TicksGame<977147)return;
            var ai=map.GetComponent<AutonomousRimMapComponent>();
            if(!started)
            {
                if(map.mapPawns.FreeColonistsSpawnedCount!=7){done=true;Log.Error("[A1Benchmark] FAIL: not original A1 population");return;}
                started=true;first=Find.TickManager.TicksGame;bedrooms=map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Where(b=>!b.ForPrisoners&&!b.Medical).ToDictionary(b=>b.thingIDNumber,b=>b.Position);
                if(GenCommandLine.CommandLineArgPassed("autonomousrimaoneoff"))ai.DisableAll();
                foreach(var bed in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Where(b=>!b.ForPrisoners&&!b.Medical))
                    Log.Message("[A1Benchmark] BED "+bed.thingIDNumber+" at "+bed.Position+" role="+bed.GetRoom()?.Role+" outdoors="+bed.GetRoom()?.PsychologicallyOutdoors);
                foreach(var project in ai.BaseProjects.Where(p=>p.Kind=="Quarto" && !p.Completed))
                    foreach(var task in BaseConstructionManager.Tasks(project).Where(t=>t.WasCompleted && !t.Complete(map)))Log.Message("[A1Benchmark] MISSING "+project.LayoutSlot+" "+task.Def+" at "+task.Position);
                VerifyEquipment();
                Log.Message("[A1Benchmark] START tick="+first+" things="+map.listerThings.AllThings.Count+" projects="+ai.BaseProjects.Count+" off="+GenCommandLine.CommandLineArgPassed("autonomousrimaoneoff"));return;
            }
            if(!measuring)
            {
                if(Find.TickManager.TicksGame-first<3000)return;
                measuring=true;first=Find.TickManager.TicksGame;firstFrame=UnityEngine.Time.frameCount;
                PerformanceTelemetry.Reset();PerformanceTelemetry.Enabled=true;clock=Stopwatch.StartNew();
                Log.Message("[A1Benchmark] SAMPLE focus="+UnityEngine.Application.isFocused+" vsync="+UnityEngine.QualitySettings.vSyncCount+" target="+UnityEngine.Application.targetFrameRate);return;
            }
            if(Find.TickManager.TicksGame-first<8000)return;
            Log.Message("[A1Benchmark] FPS "+((UnityEngine.Time.frameCount-firstFrame)/clock.Elapsed.TotalSeconds).ToString("0.0")+" resolution="+UnityEngine.Screen.width+"x"+UnityEngine.Screen.height+" batch="+UnityEngine.Application.isBatchMode);
            Log.Message("[A1Benchmark] TPS "+((Find.TickManager.TicksGame-first)/clock.Elapsed.TotalSeconds).ToString("0.0")+" elapsed="+clock.Elapsed.TotalSeconds.ToString("0.00"));
            foreach(var row in PerformanceTelemetry.Snapshot())Log.Message("[A1Benchmark] COST "+row.Module+" calls="+row.Calls+" totalMs="+row.TotalMs.ToString("0.0")+" meanMs="+row.MeanMs.ToString("0.00")+" p95="+row.P95Ms.ToString("0.00"));
            foreach(var project in ai.BaseProjects.Where(p=>p.Kind=="Freezer" || p.Kind=="Energia e climatização"))Log.Message("[A1Benchmark] SERVICE "+project.Kind+" "+project.State+" "+project.BlockReason);
            PerformanceTelemetry.Enabled=false;
            if(map.mapPawns.FreeColonistsSpawnedCount!=7 || bedrooms.Any(pair=>!map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Any(b=>b.thingIDNumber==pair.Key && b.Position==pair.Value)))
                throw new Exception("[A1Benchmark] FAIL: original colonists or bedroom positions changed");
            done=true;Log.Message("[A1Benchmark] PASS: seven original colonists and all original bedroom positions preserved");Log.Message("[A1Benchmark] DONE");
        }
    }
}
