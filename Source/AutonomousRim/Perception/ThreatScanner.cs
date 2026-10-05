using AutonomousRim.Core;
using RimWorld;
using Verse;

namespace AutonomousRim.Perception
{
    public static class ThreatScanner
    {
        public static ThreatState Scan(Map map, ColonyState colony)
        {
            var state = new ThreatState();
            foreach (PawnProfile friendly in colony.Pawns)
                state.FriendlyStrength += friendly.CombatValue;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Dead || pawn.Downed || !pawn.HostileTo(Faction.OfPlayer)) continue;
                state.ActiveCount++;
                if (pawn.RaceProps.IsMechanoid) state.Mechanoids++;
                else if (pawn.RaceProps.Humanlike) state.Humanlikes++;
                else if (pawn.RaceProps.Animal) state.Animals++;
                else state.Other++;
                if (pawn.equipment?.Primary?.def.IsRangedWeapon == true) state.Ranged++;
                else state.Melee++;
                state.EnemyStrength += PawnAnalyzer.EstimateCombatValue(pawn);
            }
            state.Risk = CombatMath.AssessRisk(state.ActiveCount, colony.CombatCapableColonists,
                state.EnemyStrength, state.FriendlyStrength);
            return state;
        }
    }
}
