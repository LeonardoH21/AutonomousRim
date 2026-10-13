using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace AutonomousRim.Core
{
    // Paired trials are separate from descriptive day windows. A baseline identifies the
    // exact source save; replica identifies an independently repeated run, never a window.
    public sealed class PolicyTrial : IExposable
    {
        public string Baseline, Context, Colony, Replica, Policy, Configuration, Scenario;
        public int Horizon, Observed, Deaths, HungerTicks, DownedTicks, Products;
        public float MinimumFood, Research;
        public string Interruption;
        public bool Completed, Interrupted;
        public bool Valid => Completed && !Interrupted && Horizon >= 60000 && Observed == Horizon &&
            Baseline != null && Baseline.Length == 64 && Baseline.All(Uri.IsHexDigit) &&
            !string.IsNullOrEmpty(Context) && !Context.Contains("unverified") &&
            !string.IsNullOrEmpty(Colony) && !string.IsNullOrEmpty(Replica) && !string.IsNullOrEmpty(Configuration) &&
            !string.IsNullOrEmpty(Scenario) &&
            (Policy == "observation-v1" || Policy == "food-buffer-v1") &&
            Deaths >= 0 && HungerTicks >= 0 && HungerTicks <= Horizon && DownedTicks >= 0 && DownedTicks <= Horizon &&
            Products >= 0 && Research >= 0 && !float.IsNaN(Research) && !float.IsInfinity(Research) &&
            MinimumFood >= 0 && !float.IsNaN(MinimumFood) && !float.IsInfinity(MinimumFood);
        public string PairKey => Baseline?.ToUpperInvariant() + "|" + Context + "|" + Configuration + "|" + Scenario + "|" + Colony + "|" + Replica + "|" + Horizon;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Baseline,"baseline"); Scribe_Values.Look(ref Context,"context");
            Scribe_Values.Look(ref Colony,"colony"); Scribe_Values.Look(ref Replica,"replica");
            Scribe_Values.Look(ref Policy,"policy"); Scribe_Values.Look(ref Configuration,"configuration");
            Scribe_Values.Look(ref Scenario,"scenario");
            Scribe_Values.Look(ref Horizon,"horizon"); Scribe_Values.Look(ref Observed,"observed");
            Scribe_Values.Look(ref Deaths,"deaths"); Scribe_Values.Look(ref HungerTicks,"hungerTicks");
            Scribe_Values.Look(ref DownedTicks,"downedTicks"); Scribe_Values.Look(ref Products,"products");
            Scribe_Values.Look(ref MinimumFood,"minimumFood"); Scribe_Values.Look(ref Research,"research");
            Scribe_Values.Look(ref Completed,"completed"); Scribe_Values.Look(ref Interrupted,"interrupted");
            Scribe_Values.Look(ref Interruption,"interruption");
        }
    }
    public sealed class PolicyVerdict
    {
        public bool Approved;
        public int Pairs, Colonies;
        public string Detail;
    }
    public static class PolicyComparison
    {
        public static PolicyVerdict Evaluate(IEnumerable<PolicyTrial> trials,string context,string configuration,string scenario)
        {
            var paired=trials.Where(t=>t!=null && t.Valid && t.Context==context && t.Configuration==configuration && t.Scenario==scenario)
                .GroupBy(t=>t.PairKey).Where(g=>g.Count()==2 && g.Select(t=>t.Policy).Distinct().Count()==2)
                .Select(g=>new { A=g.Single(t=>t.Policy=="observation-v1"), B=g.Single(t=>t.Policy=="food-buffer-v1") }).ToList();
            var replicated=paired.GroupBy(p=>p.A.Colony).Where(g=>g.Select(p=>p.A.Replica).Distinct().Count()>=2).ToList();
            var verdict=new PolicyVerdict {Pairs=paired.Count,Colonies=replicated.Count};
            if(replicated.Count<3) {verdict.Detail="Evidência insuficiente: "+paired.Count+" pares; requer 3 colônias independentes, 2 repetições pareadas por colônia.";return verdict;}
            // Any eligible regression vetoes promotion, including pairs outside the replicated set.
            bool regression=paired.Any(p=>p.B.Deaths>p.A.Deaths || p.B.HungerTicks>p.A.HungerTicks ||
                p.B.DownedTicks>p.A.DownedTicks || p.B.Research+1<p.A.Research*.9f || p.B.Products+1<p.A.Products*.9f ||
                p.B.MinimumFood+.05f<p.A.MinimumFood);
            bool benefit=replicated.All(g=>g.Average(p=>p.B.MinimumFood-p.A.MinimumFood)>=.25f ||
                g.Sum(p=>p.B.HungerTicks)<g.Sum(p=>p.A.HungerTicks));
            verdict.Approved=!regression && benefit;
            verdict.Detail=verdict.Approved ? "Reserva +1 dia validada neste contexto: "+verdict.Pairs+" pares, "+verdict.Colonies+" colônias; sem regressão nos critérios de segurança e produção." :
                regression?"Mudança rejeitada: regressão de sobrevivência, comida, pesquisa ou produção.":"Sem benefício consistente entre colônias; manter política atual.";
            return verdict;
        }
    }
    public sealed class PolicyTrialSession
    {
        public PolicyTrial Result {get;private set;}
        private int start, previous, products;
        private string research;
        private float points;
        private List<Pawn> roster;
        private string pendingBaseline,pendingReplica;
        private int pendingHorizon;
        public bool Running=>Result!=null && !Result.Completed && !Result.Interrupted;
        public void Request(string baseline,string replica,int horizon)
        {pendingBaseline=baseline;pendingReplica=replica;pendingHorizon=horizon;}
        public void Begin(Map map,AutonomousRimMapComponent ai,string baseline,string replica,int horizon)
        {
            if(Running)throw new InvalidOperationException("Uma comparação já está em andamento.");
            if(baseline==null || baseline.Length!=64 || !baseline.All(Uri.IsHexDigit) || string.IsNullOrEmpty(replica) || horizon<60000)
                throw new ArgumentException("Comparação exige SHA-256 do save inicial, repetição e pelo menos um dia.");
            ai.ObserveNow();start=previous=Find.TickManager.TicksGame;products=ai.Progress.TotalCompletedProduction;
            roster=map.mapPawns.FreeColonistsSpawned.ToList();var frame=ai.Observations.Latest;
            research=frame.ResearchDef;points=frame.Research;
            Result=new PolicyTrial {Baseline=baseline.ToUpperInvariant(),Replica=replica,Horizon=horizon,
                Context=frame.Context,Configuration=ai.PolicyConfiguration,Colony=ai.Observations.ColonyId,
                Policy=ai.Observations.Policy,MinimumFood=frame.FoodDays,Scenario=ai.PolicyScenario};
        }
        public void Tick(Map map,AutonomousRimMapComponent ai)
        {
            // Loading can finish inside an old tick loop. Anchor the trial on the
            // first tick of the new map, not inside a long-event callback.
            if(pendingBaseline!=null) {
                string baseline=pendingBaseline,replica=pendingReplica;int horizon=pendingHorizon;
                pendingBaseline=null;Begin(map,ai,baseline,replica,horizon);return;
            }
            if(!Running)return;
            int tick=Find.TickManager.TicksGame,delta=tick-previous;
            if(delta<=0)return;
            if(delta!=1 || ai.Observations.Policy!=Result.Policy || ai.PolicyConfiguration!=Result.Configuration ||
                roster.Any(p=>!p.Dead && p.Map!=map) || map.mapPawns.FreeColonistsSpawned.Any(p=>!roster.Contains(p)))
            {
                Result.Interrupted=true;
                Result.Interruption=delta!=1?"salto temporal de "+delta+" ticks":ai.Observations.Policy!=Result.Policy?"política alterada":
                    ai.PolicyConfiguration!=Result.Configuration?"configuração alterada":"elenco mudou ou saiu do mapa";
                Log.Message("[AutonomousRim.Study] Ensaio interrompido: "+Result.Interruption);return;
            }
            previous=tick;Result.Observed++;
            Result.Deaths=roster.Count(p=>p.Dead);
            if(roster.Any(p=>!p.Dead && p.needs?.food?.CurLevelPercentage<.1f))Result.HungerTicks++;
            if(roster.Any(p=>!p.Dead && p.Downed))Result.DownedTicks++;
            if(tick%60==0 || Result.Observed==Result.Horizon) {
                ai.ObserveNow(false);var frame=ai.Observations.Latest;
                if(frame.Context!=Result.Context){Result.Interrupted=true;Result.Interruption="conteúdo/dificuldade alterados";return;}
                Result.MinimumFood=Math.Min(Result.MinimumFood,frame.FoodDays);
                if(research==frame.ResearchDef)Result.Research+=Math.Max(0,frame.Research-points);
                research=frame.ResearchDef;points=frame.Research;
                Result.Products=Math.Max(0,ai.Progress.TotalCompletedProduction-products);
            }
            if(Result.Observed==Result.Horizon) {
                Result.Completed=true;
                var archive=AutonomousRimMod.Instance?.Evidence;
                if(archive?.AddTrial(Result)==true)AutonomousRimMod.Instance.WriteSettings();
            }
        }
    }
}
