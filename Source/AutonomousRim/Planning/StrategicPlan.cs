using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    public sealed class StrategicGoal : IExposable
    {
        public string Id, Horizon, Description, Status;
        public string Blocker, ResourceNeed, Risk;
        public int Priority;
        public bool Completed;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Id,"id"); Scribe_Values.Look(ref Horizon,"horizon");
            Scribe_Values.Look(ref Description,"description"); Scribe_Values.Look(ref Status,"status");
            Scribe_Values.Look(ref Blocker,"blocker"); Scribe_Values.Look(ref ResourceNeed,"resourceNeed");
            Scribe_Values.Look(ref Risk,"risk"); Scribe_Values.Look(ref Priority,"priority");
            Scribe_Values.Look(ref Completed,"completed");
        }
    }
    public sealed class StrategicPlan : IExposable
    {
        public const int DefaultHorizonDays = 500;
        public string VictoryRoute = "Nave construída na colônia";
        public string ResearchStatus = "Planejador aguardando avaliação.";
        public string TerrainStatus;
        public string MountainPlanStatus = "Terreno ainda não avaliado.";
        public string StabilityStatus = "Estabilidade ainda não avaliada.";
        public string CurrentFocus = "Aguardando avaliação.";
        public string NextFocus = "Aguardando avaliação.";
        public IntVec3 Landing = IntVec3.Invalid;
        public int HorizonDays = DefaultHorizonDays;
        public int EstimatedDaysRemaining = DefaultHorizonDays;
        public int StabilityScore;
        public int LastKnownDay;
        public int LastEvaluation = -600, LastTerrainEvaluation = -60000;
        public bool ProgressionAllowed;
        public bool MountainCandidate;
        public bool MountainExcavationApproved;
        public int MountainCells;
        public int MountainOpenCells;
        public int MountainEntrances;
        public bool ResearchOverride;
        public ResearchProjectDef OwnedResearch, PreviousResearch;
        public List<ResearchProjectDef> Route = new List<ResearchProjectDef>();
        public List<ResearchProjectDef> SeenResearch = new List<ResearchProjectDef>();
        public List<StrategicGoal> Goals = new List<StrategicGoal>();
        public List<string> Unlocks = new List<string>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref VictoryRoute,"victoryRoute","Nave construída na colônia");
            Scribe_Values.Look(ref ResearchStatus,"researchStatus"); Scribe_Values.Look(ref TerrainStatus,"terrainStatus");
            Scribe_Values.Look(ref MountainPlanStatus,"mountainPlanStatus","Terreno ainda não avaliado.");
            Scribe_Values.Look(ref StabilityStatus,"stabilityStatus","Estabilidade ainda não avaliada.");
            Scribe_Values.Look(ref CurrentFocus,"currentFocus","Aguardando avaliação.");
            Scribe_Values.Look(ref NextFocus,"nextFocus","Aguardando avaliação.");
            Scribe_Values.Look(ref Landing,"landing",IntVec3.Invalid);
            Scribe_Values.Look(ref HorizonDays,"horizonDays",DefaultHorizonDays);
            Scribe_Values.Look(ref EstimatedDaysRemaining,"estimatedDaysRemaining",DefaultHorizonDays);
            Scribe_Values.Look(ref StabilityScore,"stabilityScore");
            Scribe_Values.Look(ref LastKnownDay,"lastKnownDay");
            Scribe_Values.Look(ref LastEvaluation,"lastEvaluation",-600);
            Scribe_Values.Look(ref LastTerrainEvaluation,"lastTerrainEvaluation",-60000);
            Scribe_Values.Look(ref ProgressionAllowed,"progressionAllowed");
            Scribe_Values.Look(ref MountainCandidate,"mountainCandidate");
            Scribe_Values.Look(ref MountainExcavationApproved,"mountainExcavationApproved");
            Scribe_Values.Look(ref MountainCells,"mountainCells");
            Scribe_Values.Look(ref MountainOpenCells,"mountainOpenCells");
            Scribe_Values.Look(ref MountainEntrances,"mountainEntrances");
            Scribe_Values.Look(ref ResearchOverride,"researchOverride");
            Scribe_Defs.Look(ref OwnedResearch,"ownedResearch"); Scribe_Defs.Look(ref PreviousResearch,"previousResearch");
            Scribe_Collections.Look(ref Route,"route",LookMode.Def); Scribe_Collections.Look(ref SeenResearch,"seenResearch",LookMode.Def);
            Scribe_Collections.Look(ref Goals,"goals",LookMode.Deep); Scribe_Collections.Look(ref Unlocks,"unlocks",LookMode.Value);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)
            {
                if (HorizonDays <= 0) HorizonDays = DefaultHorizonDays;
                if (EstimatedDaysRemaining < 0) EstimatedDaysRemaining = HorizonDays;
                Route=Route??new List<ResearchProjectDef>(); SeenResearch=SeenResearch??new List<ResearchProjectDef>(); Goals=Goals??new List<StrategicGoal>(); Unlocks=Unlocks??new List<string>(); Route.RemoveAll(r=>r==null); SeenResearch.RemoveAll(r=>r==null);
            }
        }
    }
}
