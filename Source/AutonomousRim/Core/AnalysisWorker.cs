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
        private static long completed, duration;
        public static long Completed=>Interlocked.Read(ref completed);
        public static double Milliseconds=>Interlocked.Read(ref duration)*1000.0/Stopwatch.Frequency;
        public static int ThreadId=>worker.ManagedThreadId;
        static AnalysisWorker()
        {
            worker=new Thread(Run){IsBackground=true,Name="AutonomousRim analysis"}; worker.Start();
        }
        public static bool Submit(Action valueOnlyCalculation)
        {
            lock(gate) { if(queue.Count>=4)return false; queue.Enqueue(valueOnlyCalculation); }
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
                try { work(); } catch { /* Consumer retains its synchronous fallback. */ }
                Interlocked.Add(ref duration,Stopwatch.GetTimestamp()-start); Interlocked.Increment(ref completed);
            }
        }
    }
}
