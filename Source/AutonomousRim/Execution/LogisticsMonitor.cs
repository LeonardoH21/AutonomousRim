using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AutonomousRim.Core;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace AutonomousRim.Execution
{
    public sealed class HaulMetric : IExposable
    {
        public string Item,Destination; public int Trips,Ticks; public float Distance;
        public void ExposeData(){Scribe_Values.Look(ref Item,"item");Scribe_Values.Look(ref Destination,"destination");Scribe_Values.Look(ref Trips,"trips");Scribe_Values.Look(ref Ticks,"ticks");Scribe_Values.Look(ref Distance,"distance");}
    }
    public sealed class LogisticsState : IExposable
    {
        public List<HaulMetric> Rows=new List<HaulMetric>();
        public void Record(string item,string destination,int ticks,float displacement)
        {
            var row=Rows.FirstOrDefault(r=>r.Item==item&&r.Destination==destination);
            if(row==null){row=new HaulMetric{Item=item,Destination=destination};Rows.Add(row);}
            row.Trips++;row.Ticks+=Math.Max(0,ticks);row.Distance+=displacement;
            if(Rows.Count>32)Rows.RemoveAt(0);
        }
        public void ExposeData(){Scribe_Collections.Look(ref Rows,"rows",LookMode.Deep);if(Scribe.mode==LoadSaveMode.PostLoadInit){Rows=Rows??new List<HaulMetric>();Rows.RemoveAll(r=>r==null);}}
    }
    // Native completed hauling only. Time includes acquisition/waiting; displacement is not route length.
    [HarmonyPatch(typeof(Pawn_JobTracker),nameof(Pawn_JobTracker.EndCurrentJob))]
    public static class LogisticsMonitor
    {
        private sealed class Trace {public string Id,Item,Destination;public int Tick;public IntVec3 Origin;public Map Map;}
        private static readonly ConditionalWeakTable<Pawn,Trace> traces=new ConditionalWeakTable<Pawn,Trace>();
        public static void Observe(Map map)
        {
            foreach(var pawn in map.mapPawns.FreeColonistsSpawned)
            {
                var job=pawn.CurJob;
                if(job==null || job.def!=JobDefOf.HaulToCell || traces.TryGetValue(pawn,out var old)&&old.Id==job.GetUniqueLoadID())continue;
                traces.Remove(pawn);
                traces.Add(pawn,new Trace{Id=job.GetUniqueLoadID(),Item=job.targetA.Thing?.def.defName??pawn.carryTracker.CarriedThing?.def.defName??"item",
                    Destination=job.targetB.Cell.InBounds(map)?job.targetB.Cell.GetZone(map)?.label??"armazenamento":"armazenamento",Tick=Find.TickManager.TicksGame,Origin=pawn.Position,Map=map});
            }
        }
        public static void Prefix(Pawn_JobTracker __instance,JobCondition condition,Pawn ___pawn)
        {
            if(!traces.TryGetValue(___pawn,out var trace) || __instance.curJob?.GetUniqueLoadID()!=trace.Id)return;
            traces.Remove(___pawn);
            if(condition==JobCondition.Succeeded && ___pawn.Map==trace.Map)
                trace.Map.GetComponent<AutonomousRimMapComponent>().Logistics.Record(trace.Item,trace.Destination,Find.TickManager.TicksGame-trace.Tick,trace.Origin.DistanceTo(___pawn.Position));
        }
    }
}
