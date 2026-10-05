using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;

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
                    .Count(p => !p.Downed && !p.Dead && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)),
                HostilePawnCount = map.mapPawns.AllPawnsSpawned
                    .Count(p => p.HostileTo(Faction.OfPlayer) && !p.Dead),
                DownedColonists = map.mapPawns.FreeColonistsSpawned.Count(p => p.Downed),
                OutdoorTemperature = map.mapTemperature.OutdoorTemp
            };

            foreach (ThingDef resource in TrackedResources)
            {
                state.Resources[resource.defName] = map.resourceCounter.GetCount(resource);
            }

            return state;
        }
    }
}
