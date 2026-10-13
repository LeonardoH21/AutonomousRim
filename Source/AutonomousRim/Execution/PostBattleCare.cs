using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    public sealed class PostBattleCare : IExposable
    {
        public int UntilTick, LastTick, Deaths, Infections, Ready;
        public List<Pawn> Patients=new List<Pawn>();
        public string Status="Sem acompanhamento de combate recente.";
        public void Begin(Map map)
        {Patients=map.mapPawns.FreeColonistsSpawned.ToList();UntilTick=Find.TickManager.TicksGame+180000;Status="Acompanhar sobreviventes por 72 horas.";}
        public void Update(Map map,AutonomousRimMapComponent ai)
        {
            int tick=Find.TickManager.TicksGame;
            if(Patients.Count==0)return;
            if(tick>UntilTick){Status="Janela de 72 horas encerrada; último registro: "+Deaths+" mortos, "+Infections+" infectados, "+Ready+" aptos.";Patients.Clear();return;}
            LastTick=tick;Deaths=Patients.Count(p=>p==null||p.Dead);
            Infections=Patients.Count(p=>p!=null&&!p.Dead&&p.health.hediffSet.hediffs.Any(h=>h.def.defName=="WoundInfection"));
            Ready=Patients.Count(p=>p!=null&&p.Spawned&&!p.Dead&&!WorkReadiness.NeedsRecovery(p));
            if(ai.EmergencyAutomation && !ai.CurrentState.Threat.Immediate)
                MedicalRescueManager.Apply(map,ai.Emergency,ai.CurrentState.Threat);
            Status="Pós-combate: "+Deaths+" mortos, "+Infections+" infectados, "+Ready+" / "+Patients.Count+
                " aptos para retomar funções; observar por mais "+((UntilTick-tick)/2500f).ToString("0.0")+" h.";
        }
        public void ExposeData()
        {
            Scribe_Values.Look(ref UntilTick,"untilTick");Scribe_Values.Look(ref LastTick,"lastTick");Scribe_Values.Look(ref Deaths,"deaths");
            Scribe_Values.Look(ref Infections,"infections");Scribe_Values.Look(ref Ready,"ready");Scribe_Values.Look(ref Status,"status");
            Scribe_Collections.Look(ref Patients,"patients",LookMode.Reference);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)Patients=Patients??new List<Pawn>();
        }
    }
}
