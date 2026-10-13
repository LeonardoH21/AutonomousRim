using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace AutonomousRim.Core
{
    // One background worker shared by maps, bounded queue; callers never wait for it.
    public static class AnalysisWorker
    {
        private static readonly object gate=new object();
        private static readonly Queue<Action> queue=new Queue<Action>();
        private static readonly AutoResetEvent signal=new AutoResetEvent(false);
        private static readonly Thread worker;
        private static long completed, duration, rejected, failed;
        public static long Rejected=>Interlocked.Read(ref rejected);
        public static long Failed=>Interlocked.Read(ref failed);
        public static int Queued {get {lock(gate)return queue.Count;}}
        public static long Completed=>Interlocked.Read(ref completed);
        public static double Milliseconds=>Interlocked.Read(ref duration)*1000.0/Stopwatch.Frequency;
        public static int ThreadId=>worker.ManagedThreadId;
        static AnalysisWorker()
        {
            worker=new Thread(Run){IsBackground=true,Name="AutonomousRim analysis"}; worker.Start();
        }
        public static bool Submit(Action valueOnlyCalculation)
        {
            if(valueOnlyCalculation==null)return false;
            lock(gate) { if(queue.Count>=4){Interlocked.Increment(ref rejected);return false;} queue.Enqueue(valueOnlyCalculation); }
            signal.Set(); return true;
        }
        private static void Run()
        {
            while(true)
            {
                Action work=null;
                lock(gate) { if(queue.Count>0)work=queue.Dequeue(); }
                if(work==null){signal.WaitOne();continue;}
                long start=Stopwatch.GetTimestamp();
                long sample=PerformanceTelemetry.Start();bool error=false;
                try { work(); } catch { error=true;Interlocked.Increment(ref failed); /* Consumer retains its synchronous fallback. */ }
                PerformanceTelemetry.End("AnalysisWorker.Calculate",sample,error);
                Interlocked.Add(ref duration,Stopwatch.GetTimestamp()-start); Interlocked.Increment(ref completed);
            }
        }
    }
}
