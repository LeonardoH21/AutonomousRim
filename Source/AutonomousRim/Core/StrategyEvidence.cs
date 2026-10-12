using System.Collections.Generic;
using System.Linq;
using Verse;

namespace AutonomousRim.Core
{
    public sealed class ObservationIdentity : GameComponent
    {
        public string ColonyId=System.Guid.NewGuid().ToString("N");
        public ObservationIdentity(Game game){}
        public override void ExposeData() { Scribe_Values.Look(ref ColonyId,"observationColonyId"); if(Scribe.mode==LoadSaveMode.PostLoadInit && string.IsNullOrEmpty(ColonyId))ColonyId=System.Guid.NewGuid().ToString("N"); }
    }
    public sealed class StrategyEvidence : IExposable
    {
        public string Context, Colony, Policy;
        public int Start, End;
        public float FoodStart, FoodEnd, ResearchGain, EnergyDelta;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Context,"context"); Scribe_Values.Look(ref Colony,"colony");
            Scribe_Values.Look(ref Policy,"policy"); Scribe_Values.Look(ref Start,"start"); Scribe_Values.Look(ref End,"end");
            Scribe_Values.Look(ref FoodStart,"foodStart"); Scribe_Values.Look(ref FoodEnd,"foodEnd");
            Scribe_Values.Look(ref ResearchGain,"researchGain"); Scribe_Values.Look(ref EnergyDelta,"energyDelta");
        }
    }
    public sealed class EvidenceSettings : ModSettings
    {
        public List<StrategyEvidence> Evidence=new List<StrategyEvidence>();
        public bool Add(StrategyEvidence evidence)
        {
            if(Evidence.Any(e=>e.Colony==evidence.Colony && e.Policy==evidence.Policy && e.End>=evidence.End))return false;
            Evidence.Add(evidence);if(Evidence.Count>128)Evidence.RemoveAt(0);return true;
        }
        public string Describe(string context,string policy)
        {
            var rows=Evidence.Where(e=>e.Context==context && e.Policy==policy).ToList();
            int colonies=rows.Select(e=>e.Colony).Distinct().Count();
            return rows.Count+" janelas / "+colonies+" colônias compatíveis"+
                (rows.Count==0?"":" • saldo de reserva médio "+rows.Average(e=>e.FoodEnd-e.FoodStart).ToString("+0.0;-0.0;0")+" d/janela")+" • "+
                (colonies>=2?"Observação repetida; comparação descritiva, sem causalidade comprovada.":"Evidência insuficiente entre colônias.");
        }
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref Evidence,"evidence",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit) {Evidence=Evidence??new List<StrategyEvidence>();Evidence.RemoveAll(e=>e==null);if(Evidence.Count>128)Evidence.RemoveRange(0,Evidence.Count-128);}
        }
    }
    public static class StrategyEvidenceBuilder
    {
        // Explicit export of an uncontested day, not repeat-count "confidence" from the same failure.
        public static StrategyEvidence From(ObservationMemory memory,string colony)
        {
            var last=memory.Latest;if(last==null)return null;
            if(last.Context?.Contains("unverified")==true)return null;
            var frames=memory.CompatibleWindow();
            var first=frames.FirstOrDefault(f=>last.Tick-f.Tick>=60000);
            if(first==null)return null;
            var window=frames.Where(f=>f.Tick>=first.Tick).ToList();
            if(window.Any(f=>f.Enemies>0 || f.Downed>0 || f.Colonists!=last.Colonists || f.FoodDays<1))return null;
            for(int i=1;i<window.Count;i++)if(window[i].Tick-window[i-1].Tick>1200)return null;
            return new StrategyEvidence {Colony=colony,Context=last.Context,Policy=last.Policy,Start=first.Tick,End=last.Tick,
                FoodStart=first.FoodDays,FoodEnd=last.FoodDays,EnergyDelta=last.BatteryEnergy-first.BatteryEnergy,
                ResearchGain=first.ResearchDef==last.ResearchDef?last.Research-first.Research:-1};
        }
    }
}
