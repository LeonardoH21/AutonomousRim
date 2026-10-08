using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AutonomousRim.Core
{
    public enum PrisonerDestination { Recruit, TreatAndRelease, Manual }
    public sealed class PrisonerRecord : IExposable
    {
        public Pawn Pawn;
        public Faction OriginalFaction;
        public PrisonerDestination Destination;
        public PrisonerInteractionModeDef OriginalMode,AppliedMode;
        public Ideo OriginalConversion,AppliedConversion;
        public bool Owned,Manual,Completed;
        public int CapturedTick,LastProgressTick,ReleaseGoodwill;
        public float LastResistance,LastCertainty;
        public string Status;
        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn,"pawn"); Scribe_References.Look(ref OriginalFaction,"originalFaction");
            Scribe_Values.Look(ref Destination,"destination"); Scribe_Defs.Look(ref OriginalMode,"originalMode");
            Scribe_Defs.Look(ref AppliedMode,"appliedMode"); Scribe_References.Look(ref OriginalConversion,"originalConversion");
            Scribe_References.Look(ref AppliedConversion,"appliedConversion"); Scribe_Values.Look(ref Owned,"owned");
            Scribe_Values.Look(ref Manual,"manual"); Scribe_Values.Look(ref Completed,"completed");
            Scribe_Values.Look(ref CapturedTick,"capturedTick"); Scribe_Values.Look(ref LastProgressTick,"lastProgressTick");
            Scribe_Values.Look(ref ReleaseGoodwill,"releaseGoodwill"); Scribe_Values.Look(ref LastResistance,"lastResistance");
            Scribe_Values.Look(ref LastCertainty,"lastCertainty"); Scribe_Values.Look(ref Status,"status");
        }
    }
    public sealed class PrisonOrder : IExposable
    {
        public Pawn Helper,Patient;
        public Building_Bed Bed;
        public string JobId,PreviousJobId,Stage;
        public void ExposeData()
        {
            Scribe_References.Look(ref Helper,"helper"); Scribe_References.Look(ref Patient,"patient");
            Scribe_References.Look(ref Bed,"bed"); Scribe_Values.Look(ref JobId,"jobId");
            Scribe_Values.Look(ref PreviousJobId,"previousJobId"); Scribe_Values.Look(ref Stage,"stage");
        }
    }
    public sealed class PrisonState : IExposable
    {
        public List<PrisonerRecord> Prisoners=new List<PrisonerRecord>();
        public List<PrisonOrder> Orders=new List<PrisonOrder>();
        public List<Pawn> Excluded=new List<Pawn>();
        public List<string> History=new List<string>();
        public int PopulationTarget=8,Capacity,LastPlan,NextAction;
        public string Status="Prisão automática desligada.";
        public void Record(string text)
        {
            History.Add($"Dia {Find.TickManager.TicksGame/GenDate.TicksPerDay}: {text}");
            if(History.Count>12)History.RemoveAt(0);
            Log.Message("[AutonomousRim.Prison] "+text);
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref Prisoners,"prisoners",LookMode.Deep); Scribe_Collections.Look(ref Orders,"orders",LookMode.Deep);
            Scribe_Collections.Look(ref Excluded,"excluded",LookMode.Reference); Scribe_Collections.Look(ref History,"history",LookMode.Value);
            Scribe_Values.Look(ref PopulationTarget,"populationTarget",8); Scribe_Values.Look(ref LastPlan,"lastPlan");
            Scribe_Values.Look(ref NextAction,"nextAction"); Scribe_Values.Look(ref Status,"status");
            if(Scribe.mode==LoadSaveMode.PostLoadInit)
            {
                Prisoners=Prisoners??new List<PrisonerRecord>(); Orders=Orders??new List<PrisonOrder>();
                Excluded=Excluded??new List<Pawn>(); History=History??new List<string>();
            }
        }
    }
}
