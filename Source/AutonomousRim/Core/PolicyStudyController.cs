using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RimWorld;
using Verse;

namespace AutonomousRim.Core
{
    // Explicit HUD workflow also works without the runtime test add-on. Both arms
    // reload the exact same native save; only the declared food policy is changed.
    public static class PolicyStudyController
    {
        public static bool Replaying {get;private set;}
        private static Game previousGame;
        private static string pendingBaseline,pendingPolicy;
        private static int pendingMap,pendingReplica;
        private static string Hash(string path)
        {using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
        public static string SaveBaseline(Map map)
        {
            var settings=AutonomousRimMod.Instance.Evidence;
            string name="AutonomousRim-PolicyBaseline-"+Guid.NewGuid().ToString("N");
            GameDataSaveLoader.SaveGame(name);
            settings.StudySave=name;settings.StudyHash=Hash(GenFilePaths.FilePathForSavedGame(name));settings.StudyMap=map.uniqueID;
            AutonomousRimMod.Instance.WriteSettings();return name;
        }
        public static void Replay(string policy,int replica)
        {
            if(Replaying)throw new InvalidOperationException("Carregamento do ensaio já em andamento.");
            if(policy!="observation-v1" && policy!="food-buffer-v1" || replica<1 || replica>8)throw new ArgumentException("Política ou repetição inválida.");
            var settings=AutonomousRimMod.Instance.Evidence;
            if(settings.StudySave==null || !settings.StudySave.StartsWith("AutonomousRim-PolicyBaseline-",StringComparison.Ordinal) ||
                settings.StudySave.Any(c=>!char.IsLetterOrDigit(c) && c!='-'))throw new InvalidOperationException("Crie um baseline de comparação primeiro.");
            string path=GenFilePaths.FilePathForSavedGame(settings.StudySave);
            if(Hash(path)!=settings.StudyHash)throw new InvalidOperationException("O baseline foi alterado; crie outro checkpoint.");
            pendingBaseline=settings.StudyHash;pendingMap=settings.StudyMap;pendingPolicy=policy;pendingReplica=replica;
            previousGame=Current.Game;
            Replaying=true;
            try {
                GameDataSaveLoader.LoadGame(settings.StudySave);
            }catch {Replaying=false;throw;}
        }
        public static void MapLoaded(Map map,AutonomousRimMapComponent ai)
        {
            // LoadGame only queues its first event. ExecuteWhenFinished may run
            // immediately against the disposed old game, before that event starts.
            if(!Replaying || Current.Game==previousGame || map.uniqueID!=pendingMap)return;
            try {
                ai.SetObservationPolicy(pendingPolicy);
                ai.Trial.Request(pendingBaseline,"hud-"+pendingReplica,60000);
                Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
                Log.Message("[AutonomousRim.Study] Baseline carregado; ensaio agendado para o primeiro tick: "+pendingPolicy);
            }catch(Exception error){Log.Warning("[AutonomousRim.Study] Ensaio indisponível: "+error);}
            finally {Replaying=false;previousGame=null;}
        }
    }
}
