using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using RimWorld;
using Verse;
using HarmonyLib;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class FailureChecks : MapComponent
    {
        private static int stage;
        public FailureChecks(Map map):base(map){}
        private static void Check(bool ok,string msg){if(!ok)throw new InvalidOperationException(msg);}
        public override void MapComponentTick()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimfailuretest") || stage==99 || Find.TickManager.TicksGame%60!=0 || map.mapPawns.FreeColonistsSpawnedCount==0)return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(stage==1)
                {
                    Check(ai.FailureMemory.Reports.Count==1,"Failure report was not persisted in native save.");
                    Check(ai.FailureMemory.Reports[0].CausalChain.Count>=2,"Causal chain was lost on load.");
                    Check(ai.FailureMemory.Learnings.Count>0,"Learned adjustment was not persisted.");
                    Check(ai.FailureMemory.Raids.Count==1 && ai.FailureMemory.Raids[0].SaveName.StartsWith("AutonomousRim_Raid_"),"Raid checkpoint was not persisted.");
                    stage=99;Log.Message("[AutonomousRim.FailureTests] PASS: recoverable-risk distinction, repeated critical evidence, categorized causal report, expected/actual/improvement fields, learned adjustments, raid checkpoint save and native save/load.");return;
                }
                var memory=new FailureMemory{HadColony=true,PeakColonists=map.mapPawns.FreeColonistsSpawnedCount};
                var recoverable=new ColonyState{ColonistCount=map.mapPawns.FreeColonistsSpawnedCount,CombatCapableColonists=map.mapPawns.FreeColonistsSpawnedCount,DailyFoodNutrition=1f,EstimatedFoodDays=0.8f,FoodNutrition=1f,DownedColonists=0,HostilePawnCount=0,Threat=new ThreatState{FriendlyStrength=20f}};
                string recovery=FailureAnalyzer.Evaluate(map,recoverable,memory);
                Check(recovery.Contains("recuperação plausível"),"Bad but recoverable colony was declared lost.");
                var collapse=new ColonyState{ColonistCount=recoverable.ColonistCount,CombatCapableColonists=0,DailyFoodNutrition=1f,EstimatedFoodDays=0f,FoodNutrition=0f,DownedColonists=0,HostilePawnCount=0,Threat=new ThreatState{FriendlyStrength=0f}};
                string reportStatus=FailureAnalyzer.Evaluate(map,collapse,memory);FailureAnalyzer.Evaluate(map,collapse,memory);reportStatus=FailureAnalyzer.Evaluate(map,collapse,memory);
                Check(memory.FailureDeclared && memory.Reports.Count==1,"Repeated critical evidence did not create one failure report.");
                var report=memory.Reports[0];
                Check(report.Findings.Any(f=>f.Category==FailureCategory.Alimentacao && f.Severity==FailureSeverity.Critico),"Food collapse was not classified as critical.");
                Check(report.Findings.All(f=>!string.IsNullOrEmpty(f.Expected) && !string.IsNullOrEmpty(f.Actual) && !string.IsNullOrEmpty(f.Improvement)),"Report lacks expected/actual/improvement comparison.");
                Check(report.CausalChain.Count>=2 && memory.Learnings.Count>0,"Cause chain or learning missing.");
                var raidState=new ColonyState{EstimatedFoodDays=2f,DownedColonists=0,Threat=new ThreatState{ActiveCount=2,Ranged=1,Melee=1,EnemyStrength=30f,FriendlyStrength=20f,Risk="High"}};
                memory.LastRaidCheckpointTick=-600;FailureAnalyzer.RecordRaidStart(map,raidState,memory);Check(memory.Raids.Count==1,"Raid checkpoint was not created.");FailureAnalyzer.RecordRaidEnd(raidState,memory);
                AccessTools.Field(typeof(AutonomousRimMapComponent),"failureMemory").SetValue(ai,memory);
                GameDataSaveLoader.SaveGame("FailureAnalysisRoundtrip");stage=1;GameDataSaveLoader.LoadGame("FailureAnalysisRoundtrip");
            }
            catch(Exception e){stage=99;Log.Error("[AutonomousRim.FailureTests] FAIL: "+e);}
        }
    }
}
