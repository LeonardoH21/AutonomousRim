using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;
using Verse.AI;
using AutonomousRim.Planning;
using AutonomousRim.Execution;
using System.Collections.Generic;

namespace AutonomousRim.Perception
{
    public static class ColonyStateScanner
    {
        private static readonly ThingDef[] TrackedResources =
        {
            ThingDefOf.Silver,
            ThingDefOf.Steel,
            ThingDefOf.ComponentIndustrial,
            ThingDefOf.MedicineIndustrial,
            ThingDefOf.WoodLog
        };

        public static ColonyState Scan(Map map)
        {
            var state = new ColonyState
            {
                Tick = Find.TickManager?.TicksGame ?? 0,
                ColonistCount = map.mapPawns.FreeColonistsSpawnedCount,
                CombatCapableColonists = map.mapPawns.FreeColonistsSpawned
                    .Count(PawnAnalyzer.IsCombatReady),
                HostilePawnCount = map.mapPawns.AllPawnsSpawned
                    .Count(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed),
                DownedColonists = map.mapPawns.FreeColonistsSpawned.Count(p => p.Downed),
                OutdoorTemperature = map.mapTemperature.OutdoorTemp
            };

            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                PawnProfile profile = PawnAnalyzer.Analyze(pawn);
                state.Pawns.Add(profile);
            }
            state.Threat = ThreatScanner.Scan(map, state);
            state.HostilePawnCount = state.Threat.ActiveCount;
            state.Loot = LootScanner.Scan(map);
            FoodScanner.Scan(map, state);
            state.EquipmentDecisions = EquipmentPlanner.Plan(map, map.GetComponent<AutonomousRimMapComponent>()?.ManagedApparel ?? new List<ManagedApparel>());
            foreach (PawnProfile profile in state.Pawns)
            {
                var weapon = state.EquipmentDecisions.FirstOrDefault(d => d.Pawn.thingIDNumber == profile.PawnId && !d.Wear);
                var apparel = state.EquipmentDecisions.FirstOrDefault(d => d.Pawn.thingIDNumber == profile.PawnId && d.Wear);
                if (weapon != null) profile.RecommendedWeapon = weapon.Item.LabelCap.ToString();
                if (apparel != null) profile.RecommendedApparel = apparel.Item.LabelCap.ToString();
                profile.EquipmentReason = (weapon?.Reason ?? "") + " " + (apparel?.Reason ?? "");
            }

            foreach (ThingDef resource in TrackedResources)
            {
                state.Resources[resource.defName] = map.resourceCounter.GetCount(resource);
            }

            return state;
        }
    }
}
