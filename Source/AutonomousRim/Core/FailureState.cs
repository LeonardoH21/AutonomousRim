using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AutonomousRim.Core
{
    public enum FailureSeverity { Pequeno, Moderado, Grave, Critico }
    public enum FailureCategory { Planejamento, Economia, Alimentacao, Producao, Pesquisa, Combate, Defesa, Medicina, Construcao, Colonos }

    public sealed class FailureFinding : IExposable
    {
        public FailureCategory Category;
        public FailureSeverity Severity;
        public string Signal, Evidence, Expected, Actual, Improvement, Cause;
        public int Impact;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Category,"category"); Scribe_Values.Look(ref Severity,"severity"); Scribe_Values.Look(ref Signal,"signal");
            Scribe_Values.Look(ref Evidence,"evidence"); Scribe_Values.Look(ref Expected,"expected"); Scribe_Values.Look(ref Actual,"actual");
            Scribe_Values.Look(ref Improvement,"improvement"); Scribe_Values.Look(ref Cause,"cause"); Scribe_Values.Look(ref Impact,"impact");
        }
    }

    public sealed class FailureReport : IExposable
    {
        public int Tick;
        public string Title, Summary, PrimaryCause;
        public List<FailureFinding> Findings = new List<FailureFinding>();
        public List<string> CausalChain = new List<string>();
        public List<string> Improvements = new List<string>();
        public List<string> WorkingSystems = new List<string>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick,"tick"); Scribe_Values.Look(ref Title,"title"); Scribe_Values.Look(ref Summary,"summary"); Scribe_Values.Look(ref PrimaryCause,"primaryCause");
            Scribe_Collections.Look(ref Findings,"findings",LookMode.Deep); Scribe_Collections.Look(ref CausalChain,"causalChain",LookMode.Value);
            Scribe_Collections.Look(ref Improvements,"improvements",LookMode.Value); Scribe_Collections.Look(ref WorkingSystems,"workingSystems",LookMode.Value);
            if(Scribe.mode==LoadSaveMode.PostLoadInit){Findings=Findings??new List<FailureFinding>();CausalChain=CausalChain??new List<string>();Improvements=Improvements??new List<string>();WorkingSystems=WorkingSystems??new List<string>();}
        }
    }

    public sealed class FailureLearning : IExposable
    {
        public FailureCategory Category;
        public string Adjustment;
        public int EvidenceCount;
        public float Confidence;
        public void ExposeData(){Scribe_Values.Look(ref Category,"category");Scribe_Values.Look(ref Adjustment,"adjustment");Scribe_Values.Look(ref EvidenceCount,"evidenceCount");Scribe_Values.Look(ref Confidence,"confidence");}
    }

    public sealed class RaidSnapshot : IExposable
    {
        public int Tick, EnemyCount, Ranged, Melee, Downed;
        public float EnemyStrength, FriendlyStrength, FoodDays;
        public string Risk, SaveName, Outcome;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick,"tick");Scribe_Values.Look(ref EnemyCount,"enemyCount");Scribe_Values.Look(ref Ranged,"ranged");Scribe_Values.Look(ref Melee,"melee");Scribe_Values.Look(ref Downed,"downed");
            Scribe_Values.Look(ref EnemyStrength,"enemyStrength");Scribe_Values.Look(ref FriendlyStrength,"friendlyStrength");Scribe_Values.Look(ref FoodDays,"foodDays");
            Scribe_Values.Look(ref Risk,"risk");Scribe_Values.Look(ref SaveName,"saveName");Scribe_Values.Look(ref Outcome,"outcome");
        }
    }

    public sealed class FailureMemory : IExposable
    {
        public bool HadColony, FailureDeclared, RecoveryPlanActive;
        public int PeakColonists, PeakBuildings, ConsecutiveCritical, LastEvaluationTick, LastRaidCheckpointTick;
        public string Status = "Análise de risco aguardando histórico suficiente.";
        public List<FailureReport> Reports = new List<FailureReport>();
        public List<FailureLearning> Learnings = new List<FailureLearning>();
        public List<RaidSnapshot> Raids = new List<RaidSnapshot>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref HadColony,"hadColony");Scribe_Values.Look(ref FailureDeclared,"failureDeclared");Scribe_Values.Look(ref RecoveryPlanActive,"recoveryPlanActive");
            Scribe_Values.Look(ref PeakColonists,"peakColonists");Scribe_Values.Look(ref PeakBuildings,"peakBuildings");Scribe_Values.Look(ref ConsecutiveCritical,"consecutiveCritical");
            Scribe_Values.Look(ref LastEvaluationTick,"lastEvaluationTick",-600);Scribe_Values.Look(ref LastRaidCheckpointTick,"lastRaidCheckpointTick",-600);Scribe_Values.Look(ref Status,"status");
            Scribe_Collections.Look(ref Reports,"reports",LookMode.Deep);Scribe_Collections.Look(ref Learnings,"learnings",LookMode.Deep);Scribe_Collections.Look(ref Raids,"raids",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit){Reports=Reports??new List<FailureReport>();Learnings=Learnings??new List<FailureLearning>();Raids=Raids??new List<RaidSnapshot>();Reports.RemoveAll(r=>r==null);Learnings.RemoveAll(l=>l==null);Raids.RemoveAll(r=>r==null);}
        }
        public FailureReport Latest=>Reports.Count==0?null:Reports[Reports.Count-1];
    }
}
