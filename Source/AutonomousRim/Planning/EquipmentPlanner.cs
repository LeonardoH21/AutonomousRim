using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Perception;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AutonomousRim.Planning
{
    public static class EquipmentPlanner
    {
        public static bool AllowedWeapon(Pawn pawn, Thing item, bool considerForbidden = false)
        {
            if (item.def.IsStuff || !PawnAnalyzer.IsCombatReady(pawn) || !item.Spawned || item.Map != pawn.Map || item.Position.Fogged(pawn.Map) || (!considerForbidden && item.IsForbidden(pawn)) ||
                item.TryGetComp<CompBladelinkWeapon>() != null) return false;
            if (!EquipmentUtility.CanEquip(item, pawn)) return false;
            var biocode = item.TryGetComp<CompBiocodable>();
            if (biocode?.Biocoded == true && !CompBiocodable.IsBiocodedFor(item, pawn)) return false;
            if (item.def.IsRangedWeapon && pawn.apparel?.WornApparel.Any(ApparelAnalyzer.BlocksRanged) == true) return false;
            return true;
        }

        public static List<Apparel> Conflicts(Pawn pawn, Apparel candidate)
        {
            return pawn.apparel.WornApparel.Where(a => !ApparelUtility.CanWearTogether(a.def, candidate.def, pawn.RaceProps.body)).ToList();
        }

        public static bool AllowedApparel(Pawn pawn, Apparel item, List<Execution.ManagedApparel> owned, bool considerForbidden = false)
        {
            if (pawn.apparel == null || !item.Spawned || item.Map != pawn.Map || item.Position.Fogged(pawn.Map) || (!considerForbidden && item.IsForbidden(pawn)) || item.WornByCorpse ||
                !item.def.apparel.PawnCanWear(pawn) || !ApparelUtility.HasPartsToWear(pawn, item.def) ||
                (item.def.useHitPoints && (float)item.HitPoints / Mathf.Max(1, item.MaxHitPoints) < 0.5f)) return false;
            if (pawn.outfits?.CurrentApparelPolicy != null && !pawn.outfits.CurrentApparelPolicy.filter.Allows(item)) return false;
            var biocode = item.TryGetComp<CompBiocodable>();
            if (biocode?.Biocoded == true && !CompBiocodable.IsBiocodedFor(item, pawn)) return false;
            if (pawn.equipment?.Primary?.def.IsRangedWeapon == true && ApparelAnalyzer.BlocksRanged(item)) return false;
            return !Conflicts(pawn, item).Any(a => pawn.apparel.IsLocked(a) ||
                (pawn.outfits?.forcedHandler.IsForced(a) == true && !owned.Any(o => o.Pawn == pawn && o.Apparel == a)));
        }

        public static List<EquipmentDecision> Plan(Map map, List<Execution.ManagedApparel> owned, bool considerForbidden = false)
        {
            var proposals = new List<EquipmentDecision>();
            var weapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                .Where(t => !t.Position.Fogged(map) && (considerForbidden || !t.IsForbidden(Faction.OfPlayer)))
                .Select(t => new { Item = t, Profile = EquipmentAnalyzer.Analyze(t) }).ToList();
            var clothes = map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel).OfType<Apparel>()
                .Where(t => !t.Position.Fogged(map) && (considerForbidden || !t.IsForbidden(Faction.OfPlayer)) && !t.WornByCorpse).ToList();
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.Dead || pawn.Downed || pawn.InMentalState) continue;
                if (PawnAnalyzer.IsCombatReady(pawn) && pawn.equipment?.Primary?.TryGetComp<CompBladelinkWeapon>() == null)
                {
                    EquipmentProfile current = EquipmentAnalyzer.Analyze(pawn.equipment?.Primary);
                    // Preserve special-purpose weapons rather than replacing unsupported attack verbs.
                    if (current?.SpecialAttack != true)
                    {
                        float score = EquipmentAnalyzer.Suitability(pawn, current);
                        var candidates = weapons.Where(w => AllowedWeapon(pawn, w.Item, considerForbidden))
                            .Select(w => new { w.Item, Score = EquipmentAnalyzer.Suitability(pawn, w.Profile) })
                            .Where(w => EquipmentPolicy.WorthUpgrade(score, w.Score)).OrderByDescending(w => w.Score).Take(5);
                        foreach (var candidate in candidates)
                            if (pawn.CanReserveAndReach(candidate.Item, PathEndMode.Touch, Danger.None))
                                proposals.Add(new EquipmentDecision { Pawn = pawn, Item = candidate.Item, CurrentScore = score, CandidateScore = candidate.Score,
                                    Reason = $"Arma: {score:0.00} → {candidate.Score:0.00}; perfil {(AutonomousRim.Planning.DefenseProductionPlan.Melee(pawn) ? "corpo a corpo" : "à distância")}; precisão e mira calculadas pelo jogo." });
                    }
                }
                if (pawn.apparel == null) continue;
                var apparelCandidates = clothes.Where(a => AllowedApparel(pawn, a, owned, considerForbidden)).Select(a =>
                {
                    float current = Conflicts(pawn, a).Sum(old => ApparelAnalyzer.Score(pawn, old, map.mapTemperature.OutdoorTemp));
                    float candidate = ApparelAnalyzer.Score(pawn, a, map.mapTemperature.OutdoorTemp);
                    return new EquipmentDecision { Pawn = pawn, Item = a, Wear = true, CurrentScore = current, CandidateScore = candidate,
                        Reason = $"Roupa: {current:0.00} → {candidate:0.00}; proteção das partes cobertas, temperatura, estado e penalidade de movimento." };
                }).Where(d => EquipmentPolicy.WorthUpgrade(d.CurrentScore, d.CandidateScore)).OrderByDescending(d => d.Gain).Take(5);
                foreach (EquipmentDecision candidate in apparelCandidates)
                    if (pawn.CanReserveAndReach(candidate.Item, PathEndMode.Touch, Danger.None)) proposals.Add(candidate);
            }
            return proposals.OrderByDescending(d => d.Gain).ThenBy(d => d.Pawn.thingIDNumber).ThenBy(d => d.Item.thingIDNumber).ToList();
        }
    }
}
