using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace AutonomousRim.Core
{
    // Saveable scalar checkpoint. No Unity/game references escape into history.
    public sealed class ObservationFrame : IExposable
    {
        public int Tick, MapId, Colonists, Enemies, Downed, Meals, Completed;
        public float FoodDays, FoodNutrition, DailyNutrition, BatteryEnergy, Research;
        public string ResearchDef, Context, Policy, Resources, Workers;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick,"tick"); Scribe_Values.Look(ref MapId,"mapId");
            Scribe_Values.Look(ref Colonists,"colonists"); Scribe_Values.Look(ref Enemies,"enemies");
            Scribe_Values.Look(ref Downed,"downed"); Scribe_Values.Look(ref Meals,"meals");
            Scribe_Values.Look(ref Completed,"completed"); Scribe_Values.Look(ref FoodDays,"foodDays");
            Scribe_Values.Look(ref FoodNutrition,"nutrition"); Scribe_Values.Look(ref BatteryEnergy,"batteryEnergy");
            Scribe_Values.Look(ref DailyNutrition,"dailyNutrition");
            Scribe_Values.Look(ref Research,"research"); Scribe_Values.Look(ref ResearchDef,"researchDef");
            Scribe_Values.Look(ref Context,"context"); Scribe_Values.Look(ref Policy,"policy");
            Scribe_Values.Look(ref Resources,"resources"); Scribe_Values.Look(ref Workers,"workers");
        }
        public string Summary => "colonos "+Colonists+", ameaças "+Enemies+", caídos "+Downed+
            ", comida "+FoodDays.ToString("0.0")+" d, refeições "+Meals+", bateria "+BatteryEnergy.ToString("0.0")+
            ", pesquisa "+ResearchDef+" "+Research.ToString("0.0")+" • recursos "+Resources+" • trabalhadores "+Workers;
    }

    public sealed class ObservationMemory : IExposable
    {
        public List<ObservationFrame> Frames = new List<ObservationFrame>();
        public string Policy = "observation-v1";
        public string PreviousPolicy;
        public string ColonyId = System.Guid.NewGuid().ToString("N");
        public bool ChangePolicy(string policy)
        {
            if(policy!="observation-v1" && policy!="food-buffer-v1")return false;
            if(policy==Policy)return false;PreviousPolicy=Policy;Policy=policy;return true;
        }
        public bool RestorePolicy() => PreviousPolicy!=null && ChangePolicy(PreviousPolicy);
        public float ExtraFoodDays => Policy=="food-buffer-v1"?1:0;
        public int SessionStart = -1;
        public ObservationFrame Latest => Frames.LastOrDefault();
        public void ResetSession(int tick) { SessionStart=tick; }
        public void Add(ObservationFrame frame, bool force = false)
        {
            if(Latest!=null && frame.Tick<Latest.Tick) return;
            if(Latest!=null && frame.Tick==Latest.Tick) { Frames[Frames.Count-1]=frame; return; }
            // Keep one checkpoint per sample interval; frequent decision refreshes must not
            // erase the day-long window, but policy changes always start a new segment.
            if(!force && Latest!=null && frame.Policy==Latest.Policy && frame.Context==Latest.Context && frame.Tick-Latest.Tick<600)return;
            Frames.Add(frame); if(Frames.Count>128) Frames.RemoveRange(0,Frames.Count-128);
        }
        public List<ObservationFrame> CompatibleWindow()
        {
            var last=Latest;if(last==null)return new List<ObservationFrame>();
            return Frames.AsEnumerable().Reverse().TakeWhile(f=>f.Tick>=SessionStart && f.MapId==last.MapId && f.Context==last.Context && f.Policy==last.Policy)
                .Reverse().ToList();
        }
        public string Trends()
        {
            var last=Latest;
            if(last==null) return "Sem observações.";
            var first=CompatibleWindow().FirstOrDefault();
            if(first==null || last.Tick-first.Tick<2500) return "Aguardando pelo menos uma hora de observações compatíveis nesta sessão.";
            float days=(last.Tick-first.Tick)/60000f;
            float foodRate=(last.FoodNutrition-first.FoodNutrition)/days;
            string exhaustion=foodRate<-.01f ? " • Nesse ritmo, saldo alimentar se esgota em "+(last.FoodNutrition/-foodRate).ToString("0.0")+" d (estimativa)." : "";
            string research=first.ResearchDef==last.ResearchDef ? ((last.Research-first.Research)/days).ToString("0.0")+" pontos/d" : "Projeto mudou; taxas de pesquisa não são comparáveis";
            return "Janela "+days.ToString("0.00")+" d • saldo líquido de nutrição "+foodRate.ToString("+0.0;-0.0;0")+
                "/d • consumo previsto "+last.DailyNutrition.ToString("0.0")+" nutrição/d • refeições "+((last.Meals-first.Meals)/days).ToString("+0.0;-0.0;0")+
                "/d • produção nativa "+((last.Completed-first.Completed)/days).ToString("0.0")+" lotes/d (todas as receitas) • pesquisa "+research+
                " • bateria "+((last.BatteryEnergy-first.BatteryEnergy)/days).ToString("+0.0;-0.0;0")+" Wd/d."+exhaustion+
                " Saldos incluem consumo, produção, compras, perdas e transporte; não medem essas causas separadamente.";
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref Frames,"frames",LookMode.Deep); Scribe_Values.Look(ref Policy,"policy","observation-v1");
            Scribe_Values.Look(ref PreviousPolicy,"previousPolicy"); Scribe_Values.Look(ref ColonyId,"colonyId");
            if(Scribe.mode==LoadSaveMode.PostLoadInit && string.IsNullOrEmpty(ColonyId))ColonyId=System.Guid.NewGuid().ToString("N");
            if(Scribe.mode==LoadSaveMode.PostLoadInit) { Frames=Frames??new List<ObservationFrame>(); Frames.RemoveAll(f=>f==null); if(Frames.Count>128) Frames.RemoveRange(0,Frames.Count-128); SessionStart=-1; }
        }
    }
}
