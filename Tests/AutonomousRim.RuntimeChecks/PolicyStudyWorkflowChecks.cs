using System;
using System.IO;
using System.Linq;
using AutonomousRim.Core;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class PolicyStudyWorkflowChecks : MapComponent
    {
        private static int stage;
        private static string baseline,hash;
        public PolicyStudyWorkflowChecks(Map map):base(map){}
        private static void Check(bool result,string text){if(!result)throw new Exception(text);Log.Message("[PolicyWorkflow] PASS: "+text);}
        public override void MapComponentTick()
        {
            if(stage==99 || !EliteFixtureBaseline.Ready || !GenCommandLine.CommandLineArgPassed("autonomousrimstudyworkflow"))return;
            try {
                var ai=map.GetComponent<AutonomousRimMapComponent>();var archive=AutonomousRimMod.Instance.Evidence;
                if(stage==0) {
                    EliteFixtureBaseline.Roster(map);ai.DisableAll();
                    baseline=PolicyStudyController.SaveBaseline(map);hash=archive.StudyHash;
                    Check(File.Exists(GenFilePaths.FilePathForSavedGame(baseline)) && hash.Length==64,"HUD controller saves a separate native baseline with exact SHA-256");
                    stage=1;PolicyStudyController.Replay("food-buffer-v1",1);return;
                }
                if(stage==1 && !PolicyStudyController.Replaying && ai.Trial.Result!=null) {
                    Check(ai.Trial.Running,"replay begins on the first native map tick without falsely interrupting continuity");
                    Check(ai.Trial.Result.Baseline==hash && ai.Trial.Result.Replica=="hud-1" && ai.Trial.Result.Policy=="food-buffer-v1","HUD replay starts declared arm from the exact baseline");
                    Check(ai.Trial.Result.Configuration==ai.PolicyConfiguration && ai.Trial.Result.Context==ai.Observations.Latest.Context,"HUD trial captures actual native configuration and content");
                    Check(!ai.ApplyProvenPolicy(),"unvalidated archive cannot automatically apply a strategy");
                    ai.SetObservationPolicy("observation-v1");stage=2;return;
                }
                if(stage==2 && ai.Trial.Result.Interrupted) {
                    Check(archive.Trials.Count==0,"manual intervention interrupts the trial without manufacturing completed evidence");
                    stage=3;GameDataSaveLoader.SaveGame("PolicyWorkflowRoundtrip");GameDataSaveLoader.LoadGame("PolicyWorkflowRoundtrip");return;
                }
                if(stage==3) {
                    Check(archive.StudySave==baseline && archive.StudyHash==hash && ai.Trial.Result==null,"baseline settings persist while active experimental time is discarded on load");
                    string path=GenFilePaths.FilePathForSavedGame(baseline);
                    using(var stream=File.Open(path,FileMode.Append))stream.WriteByte(32);
                    bool rejected=false;try {PolicyStudyController.Replay("observation-v1",1);}catch(InvalidOperationException){rejected=true;}
                    Check(rejected && !PolicyStudyController.Replaying,"altered disposable baseline is rejected before game load");
                    EliteFixtureBaseline.VerifyOriginalsPresent(map);stage=99;Log.Message("[PolicyWorkflow] DONE");
                }
            }catch(Exception error){stage=99;Log.Error("[PolicyWorkflow] FAIL: "+error);}
        }
    }
}
