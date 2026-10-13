using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
using AutonomousRim.Execution;
using AutonomousRim.Perception;

namespace AutonomousRim.Core
{
    public sealed class ModuleTiming
    {
        public string Module;
        public long Calls, Failures;
        public double TotalMs, MeanMs, P95Ms, MaxMs;
    }
    public static class PerformanceTelemetry
    {
        private sealed class Counter
        {
            public long Calls, Failures, Total, Max;
            public readonly Queue<long> Samples=new Queue<long>();
        }
        private static readonly Dictionary<string,Counter> counters=new Dictionary<string,Counter>();
        private static readonly object gate=new object();
        public static bool Enabled;
        public static long Start()=>Enabled?Stopwatch.GetTimestamp():0;
        public static void End(string module,long start,bool failed=false)
        {
            if(start==0)return;
            long elapsed=Math.Max(0,Stopwatch.GetTimestamp()-start);
            lock(gate) {
                if(!counters.TryGetValue(module,out var counter))counters[module]=counter=new Counter();
                counter.Calls++;if(failed)counter.Failures++;counter.Total+=elapsed;counter.Max=Math.Max(counter.Max,elapsed);
                counter.Samples.Enqueue(elapsed);if(counter.Samples.Count>512)counter.Samples.Dequeue();
            }
        }
        public static List<ModuleTiming> Snapshot()
        {
            lock(gate)return counters.Select(pair=> {
                var c=pair.Value;var sorted=c.Samples.OrderBy(x=>x).ToArray();double scale=1000.0/Stopwatch.Frequency;
                return new ModuleTiming {Module=pair.Key,Calls=c.Calls,Failures=c.Failures,TotalMs=c.Total*scale,
                    MeanMs=c.Total*scale/c.Calls,MaxMs=c.Max*scale,P95Ms=sorted[(int)Math.Ceiling(sorted.Length*.95)-1]*scale};
            }).OrderByDescending(x=>x.TotalMs).ToList();
        }
        public static void Reset(){lock(gate)counters.Clear();}
    }
    // Inclusive timings: parents contain child time, so their costs must not be summed.
    // One prefix/finalizer branch when disabled; exceptions retain their native behavior.
    [HarmonyPatch]
    public static class ModuleTimingPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var targets=new Dictionary<Type,string[]> {
                {typeof(AutonomousRimMapComponent),new[]{"MapComponentTick","ObserveNow","ManageColony","ExecuteConstruction","EvaluateStrategy","EvaluateFailure","RefreshThreat"}},
                {typeof(CombatManager),new[]{"Apply"}}, {typeof(CommerceManager),new[]{"Apply"}},
                {typeof(CommerceCaravanManager),new[]{"Apply"}}, {typeof(FoodManager),new[]{"Apply"}},
                {typeof(WorkPriorityManager),new[]{"Apply"}}, {typeof(ScheduleManager),new[]{"Apply"}},
                {typeof(EquipmentManager),new[]{"Apply"}}, {typeof(PrisonManager),new[]{"Apply"}},
                {typeof(ColonyProgressMonitor),new[]{"Sample"}}, {typeof(RoomObserver),new[]{"Read"}},
                {typeof(ColonyStateScanner),new[]{"Scan"}}
            };
            foreach(var target in targets)
                foreach(var method in target.Key.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static)
                    .Where(m=>target.Value.Contains(m.Name)))yield return method;
        }
        public static void Prefix(out long __state){__state=PerformanceTelemetry.Start();}
        public static Exception Finalizer(MethodBase __originalMethod,long __state,Exception __exception)
        {if(__state!=0)PerformanceTelemetry.End(__originalMethod.DeclaringType.Name+"."+__originalMethod.Name,__state,__exception!=null);return __exception;}
    }
    public sealed class TickRateMeter
    {
        private int lastTick=-1, speed;
        private long lastTime;
        public double Tps {get;private set;}
        public int ObservedTicks {get;private set;}
        public double Seconds {get;private set;}
        public void Reset(){lastTick=-1;Tps=0;ObservedTicks=0;Seconds=0;}
        public void Update(int tick,int currentSpeed,bool paused)
        {
            long now=Stopwatch.GetTimestamp();
            if(paused || !PerformanceTelemetry.Enabled){Reset();return;}
            if(lastTick<0 || currentSpeed!=speed || tick<lastTick) {Reset();lastTick=tick;lastTime=now;speed=currentSpeed;return;}
            double elapsed=(now-lastTime)/(double)Stopwatch.Frequency;
            if(elapsed<1)return;
            // Long events/loading/background stalls are not comparable play intervals.
            if(elapsed<5 && tick>lastTick) {ObservedTicks+=tick-lastTick;Seconds+=elapsed;Tps=ObservedTicks/Seconds;}
            lastTick=tick;lastTime=now;
        }
    }
}
