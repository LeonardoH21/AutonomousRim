using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;
using Verse.AI;

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

            var availableWeapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                .Where(t => !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer))
                .Select(t => new { Thing = t, Profile = EquipmentAnalyzer.Analyze(t) }).ToList();
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                PawnProfile profile = PawnAnalyzer.Analyze(pawn);
                if (profile.CombatReady && profile.Equipment?.SpecialAttack != true)
                {
                    float currentScore = EquipmentAnalyzer.Suitability(pawn, profile.Equipment);
                    // Check only the strongest five improvements to bound pathfinding work.
                    var improvements = availableWeapons
                        .Select(w => new { w.Thing, w.Profile, Score = EquipmentAnalyzer.Suitability(pawn, w.Profile) })
                        .Where(w => w.Score > currentScore * 1.15f && w.Score > 0f)
                        .OrderByDescending(w => w.Score).Take(5);
                    foreach (var candidate in improvements)
                    {
                        if (EquipmentUtility.CanEquip(candidate.Thing, pawn) &&
                            pawn.CanReserveAndReach(candidate.Thing, Verse.AI.PathEndMode.Touch, Danger.Some))
                        {
                            profile.RecommendedWeapon = candidate.Profile.Name + " (advisory; not assigned)";
                            break;
                        }
                    }
                }
                state.Pawns.Add(profile);
            }
            state.Threat = ThreatScanner.Scan(map, state);
            state.HostilePawnCount = state.Threat.ActiveCount;
            state.Loot = LootScanner.Scan(map);
            FoodScanner.Scan(map, state);

            foreach (ThingDef resource in TrackedResources)
            {
                state.Resources[resource.defName] = map.resourceCounter.GetCount(resource);
            }

            return state;
        }
    }
}
