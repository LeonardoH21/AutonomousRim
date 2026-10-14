using System;
using System.Collections.Generic;
using Verse;
namespace AutonomousRim.Planning
{
    // Reuse pure answers within one synchronous evaluation only. Never retain them
    // across jobs, ticks, health/equipment changes, loads or player policy edits.
    public static class EquipmentEvaluationScope
    {
        private sealed class State
        {
            public Map Map;
            public int? Stage;
            public readonly Dictionary<Pawn,bool> Melee=new Dictionary<Pawn,bool>();
            public readonly Dictionary<Pawn,Dictionary<Thing,Dictionary<string,bool>>> Meets=new Dictionary<Pawn,Dictionary<Thing,Dictionary<string,bool>>>();
        }
        [ThreadStatic] private static State current;
        private sealed class Scope:IDisposable
        {
            private readonly State previous;
            public Scope(Map map){previous=current;current=new State{Map=map};}
            public void Dispose(){current=previous;}
        }
        public static IDisposable Begin(Map map)=>new Scope(map);
        public static int Stage(Map map,Func<int> calculate)
        {
            if(current?.Map!=map)return calculate();
            if(!current.Stage.HasValue)current.Stage=calculate();return current.Stage.Value;
        }
        public static bool Melee(Pawn pawn,Func<bool> calculate)
        {
            if(current==null || current.Map!=pawn.Map)return calculate();
            if(!current.Melee.TryGetValue(pawn,out bool result)){result=calculate();current.Melee[pawn]=result;}return result;
        }
        public static bool Meets(Pawn pawn,Thing item,string target,Func<bool> calculate)
        {
            if(current==null || current.Map!=pawn.Map || item==null)return calculate();
            if(!current.Meets.TryGetValue(pawn,out var items))current.Meets[pawn]=items=new Dictionary<Thing,Dictionary<string,bool>>();
            if(!items.TryGetValue(item,out var targets))items[item]=targets=new Dictionary<string,bool>();
            if(!targets.TryGetValue(target,out bool result)){result=calculate();targets[target]=result;}return result;
        }
    }
}
