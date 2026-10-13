using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class LearningPerformanceChecks : MapComponent
    {
        private static int stage;
        private static bool failed;
        private static long started;
        private static PolicyTrial native;
        public LearningPerformanceChecks(Map map):base(map){}
        private static void Check(bool condition,string text)
        {if(!condition)throw new Exception(text);Log.Message("[LearningPerformance] PASS: "+text);}
        public override void MapComponentTick()
        {
            if(failed || stage==99 || !EliteFixtureBaseline.Ready || !GenCommandLine.CommandLineArgPassed("autonomousrimlearningtest"))return;
            try {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(stage==1 && ai.Trial.Result?.Interrupted==true)
                    throw new Exception("native trial interrupted: "+ai.Trial.Result.Interruption+" after "+ai.Trial.Result.Observed+" ticks");
                if(stage==0) {
                    EliteFixtureBaseline.Roster(map);Contracts();
                    ai.DisableAll();ai.SetAutomation(true,true);ai.SetScheduleAutomation(true);
                    ai.SetObservationPolicy(GenCommandLine.CommandLineArgPassed("autonomousrimcandidate")?"food-buffer-v1":"observation-v1");
                    CombatTacticalPlanner.ParallelEnabled=!GenCommandLine.CommandLineArgPassed("autonomousrimsynchronous");
                    string path=GenFilePaths.FilePathForSavedGame("ModularResume");string hash;
                    using(var stream=File.OpenRead(path))using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
                    PerformanceTelemetry.Reset();ai.Performance.Reset();PerformanceTelemetry.Enabled=true;
                    var replica=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-autonomousrimreplica",StringComparison.Ordinal))??"-autonomousrimreplica1";
                    ai.Trial.Begin(map,ai,hash,replica,60000);
                    started=Stopwatch.GetTimestamp();stage=1;return;
                }
                if(stage==1 && ai.Trial.Result?.Completed==true) {
                    native=ai.Trial.Result;
                    Check(native.Valid && native.Observed==60000,"native policy trial observes one whole day without changing configuration");
                    Check(AutonomousRimMod.Instance.Evidence.Trials.Any(t=>t.PairKey==native.PairKey && t.Policy==native.Policy),"real native result is archived independently from descriptive windows");
                    Check(!ai.PolicyComparison.Approved,"one real paired scenario cannot trigger cross-colony learning");
                    var timings=PerformanceTelemetry.Snapshot();
                    Check(timings.Any(t=>t.Module=="AutonomousRimMapComponent.MapComponentTick" && t.Calls>59000),"real native ticks are timed per module");
                    Check(timings.Any(t=>t.Module=="WorkPriorityManager.Apply") && timings.Any(t=>t.Module=="FoodManager.Apply"),"native work and food costs are measured separately");
                    Check(timings.All(t=>t.P95Ms>=0 && t.MaxMs>=t.P95Ms && t.Calls>0),"native timing percentiles have bounded valid samples");
                    TrialExchange.Export(Path.Combine(GenFilePaths.SaveDataFolderPath,"PolicyTrials.xml"),new[]{native});
                    var imported=new EvidenceSettings();Check(TrialExchange.Import(Path.Combine(GenFilePaths.SaveDataFolderPath,"PolicyTrials.xml"),imported)==1 &&
                        TrialExchange.Import(Path.Combine(GenFilePaths.SaveDataFolderPath,"PolicyTrials.xml"),imported)==0,"real trial XML roundtrip deduplicates copied evidence");
                    File.WriteAllLines(Path.Combine(GenFilePaths.SaveDataFolderPath,"Performance.csv"),new[]{"module,calls,failures,total_ms,mean_ms,p95_ms,max_ms"}.Concat(timings.Select(t=>
                        t.Module+","+t.Calls+","+t.Failures+","+t.TotalMs.ToString("R",CultureInfo.InvariantCulture)+","+t.MeanMs.ToString("R",CultureInfo.InvariantCulture)+","+t.P95Ms.ToString("R",CultureInfo.InvariantCulture)+","+t.MaxMs.ToString("R",CultureInfo.InvariantCulture))));
                    double seconds=(Stopwatch.GetTimestamp()-started)/(double)Stopwatch.Frequency;
                    Log.Message("[LearningPerformance] RESULT policy="+native.Policy+" ticks="+native.Observed+" seconds="+seconds.ToString("0.000",CultureInfo.InvariantCulture)+
                        " tps="+(60000/seconds).ToString("0.0",CultureInfo.InvariantCulture)+" food_min="+native.MinimumFood+" hunger="+native.HungerTicks+" deaths="+native.Deaths+
                        " products="+native.Products+" research="+native.Research+" worker_tasks="+AnalysisWorker.Completed+" parallel="+CombatTacticalPlanner.ParallelEnabled);
                    PerformanceTelemetry.Enabled=false;
                    GameDataSaveLoader.SaveGame("LearningRoundtrip");stage=2;GameDataSaveLoader.LoadGame("LearningRoundtrip");return;
                }
                if(stage==2) {
                    Check(ai.Observations.Policy==native.Policy,"native save/load preserves chosen version and rollback history");
                    Check(ai.Trial.Result==null,"loading discards an active session instead of fabricating continuous experimental time");
                    // A death is a valid experimental outcome, never grounds to hide a trial.
                    if(native.Deaths==0)EliteFixtureBaseline.VerifyOriginalsPresent(map);
                    stage=99;Log.Message("[LearningPerformance] DONE");
                }
            }catch(Exception error){failed=true;Log.Error("[LearningPerformance] FAIL: "+error);}
        }
        private static List<PolicyTrial> SuccessfulCases()
        {
            var rows=new List<PolicyTrial>();
            for(int colony=0;colony<3;colony++)for(int replica=0;replica<2;replica++)foreach(string policy in new[]{"observation-v1","food-buffer-v1"})
                rows.Add(new PolicyTrial {Baseline=new string((char)('A'+colony),64),Context="fixture",Configuration="config",Scenario="cohort",Colony="c"+colony,Replica="r"+replica,
                    Policy=policy,Horizon=60000,Observed=60000,MinimumFood=policy=="observation-v1"?3:3.5f,Research=100,Products=10,Completed=true});
            return rows;
        }
        private static void Contracts()
        {
            // Synthetic records exercise gates only; they never enter the global evidence archive.
            var rows=SuccessfulCases();Check(PolicyComparison.Evaluate(rows,"fixture","config","cohort").Approved,"three independent replicated synthetic pairs pass predefined safety and utility criteria");
            Check(!PolicyComparison.Evaluate(rows.Take(4),"fixture","config","cohort").Approved,"one colony and repeated windows are insufficient");
            Check(!PolicyComparison.Evaluate(rows,"different","config","cohort").Approved && !PolicyComparison.Evaluate(rows,"fixture","other","cohort").Approved,"content and automation contexts cannot be mixed");
            Check(!PolicyComparison.Evaluate(rows,"fixture","config","other-population-season").Approved,"population and season outside the tested cohort cannot inherit a recommendation");
            var settings=new EvidenceSettings();foreach(var row in rows)Check(settings.AddTrial(row),"unique synthetic trial accepted");
            Check(!settings.AddTrial(rows[0]),"duplicate trial cannot amplify evidence");
            var alias=SuccessfulCases()[0];alias.Baseline=alias.Baseline.ToLowerInvariant();
            Check(!settings.AddTrial(alias),"SHA-256 letter case cannot duplicate a baseline");
            rows[1].Deaths=1;Check(!PolicyComparison.Evaluate(rows,"fixture","config","cohort").Approved,"additional death vetoes a food improvement");rows[1].Deaths=0;
            rows[1].HungerTicks=10;Check(!PolicyComparison.Evaluate(rows,"fixture","config","cohort").Approved,"additional hunger vetoes promotion");rows[1].HungerTicks=0;
            rows[1].Research=50;Check(!PolicyComparison.Evaluate(rows,"fixture","config","cohort").Approved,"large research regression vetoes promotion");rows[1].Research=100;
            rows[1].Products=3;Check(!PolicyComparison.Evaluate(rows,"fixture","config","cohort").Approved,"production regression vetoes promotion");rows[1].Products=10;
            foreach(var row in rows)row.MinimumFood=3;Check(!PolicyComparison.Evaluate(rows,"fixture","config","cohort").Approved,"equal outcomes do not invent a useful adjustment");
            rows=SuccessfulCases();rows[1].Baseline=new string('F',64);Check(!PolicyComparison.Evaluate(rows,"fixture","config","cohort").Approved,"different source checkpoint cannot form a pair");
            rows=SuccessfulCases();rows[1].Interrupted=true;Check(!PolicyComparison.Evaluate(rows,"fixture","config","cohort").Approved,"interrupted or shortened trial cannot count as a full pair");
            rows[1].Interrupted=false;rows[1].MinimumFood=float.NaN;Check(!rows[1].Valid,"nonfinite measurements are rejected");
            PerformanceTelemetry.Reset();PerformanceTelemetry.Enabled=false;
            PerformanceTelemetry.End("disabled",PerformanceTelemetry.Start());Check(PerformanceTelemetry.Snapshot().Count==0,"disabled telemetry accumulates no samples");
            PerformanceTelemetry.Enabled=true;
            for(int i=0;i<700;i++)PerformanceTelemetry.End("bounded",PerformanceTelemetry.Start());
            PerformanceTelemetry.End("failure",PerformanceTelemetry.Start(),true);
            Check(PerformanceTelemetry.Snapshot().Single(t=>t.Module=="bounded").Calls==700,"bounded percentile ring retains total call count");
            Check(PerformanceTelemetry.Snapshot().Single(t=>t.Module=="failure").Failures==1,"failure remains visible without swallowing an exception");
            PureWorkerComparison();PerformanceTelemetry.Enabled=false;
        }
        private static void PureWorkerComparison()
        {
            var allies=Enumerable.Range(0,6).Select(i=>new FighterSnapshot(i,i*3,10,-1,i>1,false,1,10,5,.2f,.5f,.3f,24,4)).ToArray();
            var enemies=Enumerable.Range(10,16).Select(i=>new FighterSnapshot(i,i*2,30,-1,i%2==0,false,.8f,10,5,.1f,.4f,.2f,20,4)).ToArray();
            var expected=TacticalPolicy.Assess(allies,enemies);TargetAssessment[] actual=null;
            var timer=Stopwatch.StartNew();for(int i=0;i<40;i++)TacticalPolicy.Assess(allies,enemies);timer.Stop();double sync=timer.Elapsed.TotalMilliseconds/40;
            // Callback publishes a result; main game thread is never blocked waiting on it.
            long start=Stopwatch.GetTimestamp();
            Check(AnalysisWorker.Submit(()=> {actual=TacticalPolicy.Assess(allies,enemies);
                double ms=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
                bool equal=actual.Length==expected.Length && actual.Zip(expected,(a,b)=>a.PawnId==b.PawnId && a.TargetId==b.TargetId && a.Score==b.Score && a.Advantage==b.Advantage).All(x=>x);
                // No logging/game API on worker. Only scalar/result publication.
                workerEqual=equal;workerLatency=ms;Interlocked.Exchange(ref workerReady,1);
            }),"value-only comparison submitted without accessing mutable game objects on worker");
            syncMs=sync;
        }
        private static int workerReady;
        private static bool workerEqual;
        private static double workerLatency,syncMs;
        public override void MapComponentUpdate()
        {
            if(Interlocked.Exchange(ref workerReady,0)==1) {
                try {Check(workerEqual,"sync and asynchronous value-only target assessments are identical");
                    Log.Message("[LearningPerformance] MICRO sync_ms="+syncMs.ToString("0.000",CultureInfo.InvariantCulture)+" worker_latency_ms="+workerLatency.ToString("0.000",CultureInfo.InvariantCulture)+" pairs=96; microbenchmark is not a combat TPS result");
                }catch(Exception error){failed=true;Log.Error("[LearningPerformance] FAIL: "+error);}
            }
        }
    }
}
