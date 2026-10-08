using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using AutonomousRim.Core;
using AutonomousRim.Perception;
using Verse;

namespace AutonomousRim.Planning
{
    public static class CombatTacticalPlanner
    {
        public static bool ParallelEnabled=true;
        public static int MinimumPairs=256;
        private sealed class State
        {
            public int Tick=-9999,Version,ResultTick=-9999;
            public FighterSnapshot[] Allies=new FighterSnapshot[0],Enemies=new FighterSnapshot[0];
            public TargetAssessment[] Results=new TargetAssessment[0];
            public TargetAssessment[] Ready;
            public int ReadyVersion;
            public bool Pending;
            public readonly object Gate=new object();
        }
        private static readonly ConditionalWeakTable<Map,State> states=new ConditionalWeakTable<Map,State>();
        public static TargetAssessment[] Analyze(Map map,List<Pawn> allies,List<Pawn> enemies)
        {
            var state=states.GetValue(map,_=>new State()); int tick=Find.TickManager.TicksGame;
            bool changed=state.Allies.Length!=allies.Count || state.Enemies.Length!=enemies.Count ||
                state.Allies.Any(a=>!allies.Any(p=>p.thingIDNumber==a.Id)) || state.Enemies.Any(a=>!enemies.Any(p=>p.thingIDNumber==a.Id));
            lock(state.Gate)
            {
                if(state.Ready!=null && state.ReadyVersion==state.Version && tick-state.Tick<=120)
                { state.Results=state.Ready; state.ResultTick=state.Tick; state.Ready=null; }
            }
            if(!changed && tick-state.Tick<60)return state.Results;
            state.Tick=tick; state.Version++;
            state.Allies=allies.Select(CombatEquipmentScanner.Copy).ToArray();
            state.Enemies=enemies.Select(CombatEquipmentScanner.Copy).ToArray();
            // Initial/roster-changing decisions are synchronous. Never delay a response to a threat.
            if(changed || !ParallelEnabled || allies.Count*enemies.Count<MinimumPairs || state.Results.Length==0 || tick-state.ResultTick>=120)
            { state.Results=TacticalPolicy.Assess(state.Allies,state.Enemies); state.ResultTick=tick; return state.Results; }
            var a=state.Allies; var e=state.Enemies; int version=state.Version;
            lock(state.Gate)
            {
                if(state.Pending)return state.Results;
                state.Pending=true;
                if(!AnalysisWorker.Submit(()=>
                {
                    try
                    {
                        var result=TacticalPolicy.Assess(a,e);
                        lock(state.Gate){state.Ready=result;state.ReadyVersion=version;}
                    }
                    finally { lock(state.Gate)state.Pending=false; }
                }))state.Pending=false;
            }
            return state.Results;
        }
        public static void Clear(Map map)=>states.Remove(map);
    }
}
