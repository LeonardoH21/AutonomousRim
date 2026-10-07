using AutonomousRim.Core;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using AutonomousRim.Execution;

namespace AutonomousRim.Perception
{
    public static class ThreatScanner
    {
        public static bool Active(Pawn pawn) => pawn.Spawned && !pawn.Dead && !pawn.Downed &&
            pawn.HostileTo(Faction.OfPlayer) &&
            (pawn.canBeDormant==null || pawn.canBeDormant.Awake) &&
            (pawn.MentalStateDef==MentalStateDefOf.Manhunter || pawn.MentalStateDef==MentalStateDefOf.ManhunterPermanent ||
             GenHostility.IsActiveThreatTo(pawn,Faction.OfPlayer,false,true));
        public static ThreatState Scan(Map map, ColonyState colony)
        {
            var state = new ThreatState();
            state.Colonists=map.mapPawns.FreeColonistsSpawned.Where(p=>!p.Dead).ToList();
            state.Fighters=state.Colonists.Where(CombatManager.Eligible).ToList();
            state.Vulnerable=state.Colonists.Where(p=>p.Downed || !CombatManager.Eligible(p) ||
                p.health.summaryHealth.SummaryHealthPercent<0.75f || p.health.hediffSet.BleedRateTotal>0.35f).ToList();
            state.FriendlyStrength=state.Fighters.Sum(PawnAnalyzer.EstimateCombatValue);
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if(!Active(pawn)) { if(!pawn.Dead && !pawn.Downed && pawn.HostileTo(Faction.OfPlayer)) state.Dormant++; continue; }
                state.Sources.Add(pawn);
                state.ActiveCount++;
                if (pawn.RaceProps.IsMechanoid) state.Mechanoids++;
                else if (pawn.RaceProps.Humanlike) state.Humanlikes++;
                else if (pawn.RaceProps.Animal) { state.Animals++; if(pawn.RaceProps.Insect)state.Insects++;
                    if(pawn.MentalStateDef==MentalStateDefOf.Manhunter || pawn.MentalStateDef==MentalStateDefOf.ManhunterPermanent)state.Manhunters++; }
                else state.Other++;
                if (pawn.equipment?.Primary?.def.IsRangedWeapon == true) state.Ranged++;
                else state.Melee++;
                state.EnemyStrength += PawnAnalyzer.EstimateCombatValue(pawn);
            }
            foreach(var thing in map.listerThings.AllThings.Where(t=>t.Spawned && !(t is Pawn) && !t.Fogged()))
            {
                bool hive=thing is Hive && thing.Faction!=Faction.OfPlayer;
                bool active=thing is IAttackTarget target && GenHostility.IsActiveThreatTo(target,Faction.OfPlayer,false,false);
                bool condition=thing.HostileTo(Faction.OfPlayer) && thing.TryGetComp<CompCauseGameCondition>()!=null;
                if(!hive && !active && !condition)continue;
                state.Sources.Add(thing); state.Structures++; state.ActiveCount++; state.EnemyStrength+=20;
            }
            state.Risk = CombatMath.AssessRisk(state.ActiveCount, state.Fighters.Count,
                state.EnemyStrength, state.FriendlyStrength);
            return state;
        }
    }
}
