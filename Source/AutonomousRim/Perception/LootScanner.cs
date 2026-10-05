using System.Collections.Generic;
using AutonomousRim.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace AutonomousRim.Perception
{
    public static class LootScanner
    {
        public static List<LootProfile> Scan(Map map)
        {
            var result = new List<LootProfile>();
            foreach (Thing weapon in map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon))
                if (!weapon.Position.Fogged(map)) result.Add(Describe(weapon, weapon.Position.ToString(), weapon.IsForbidden(Faction.OfPlayer)));
            foreach (Thing apparel in map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel))
                if (!apparel.Position.Fogged(map)) result.Add(Describe(apparel, apparel.Position.ToString(), apparel.IsForbidden(Faction.OfPlayer)));
            foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                var corpse = thing as Corpse;
                if (corpse?.InnerPawn == null || corpse.Position.Fogged(map)) continue;
                Pawn pawn = corpse.InnerPawn;
                string source = $"No cadáver de {pawn.LabelShortCap} em {corpse.Position}; requer despir";
                if (pawn.equipment != null)
                    foreach (Thing weapon in pawn.equipment.AllEquipmentListForReading)
                        result.Add(Describe(weapon, source, corpse.IsForbidden(Faction.OfPlayer)));
                if (pawn.apparel != null)
                    foreach (Apparel apparel in pawn.apparel.WornApparel)
                        result.Add(Describe(apparel, source, corpse.IsForbidden(Faction.OfPlayer)));
            }
            return result;
        }

        private static LootProfile Describe(Thing thing, string location, bool forbidden)
        {
            var apparel = thing as Apparel;
            return new LootProfile
            {
                Name = thing.LabelCap.ToString(), Kind = apparel == null ? "Arma" : "Roupa",
                Location = location, Forbidden = forbidden, Tainted = apparel?.WornByCorpse ?? false,
                Condition = thing.def.useHitPoints ? (float)thing.HitPoints / Mathf.Max(1, thing.MaxHitPoints) : 1f,
                Weapon = apparel == null ? EquipmentAnalyzer.Analyze(thing) : null,
                SharpArmor = apparel == null ? 0f : thing.GetStatValue(StatDefOf.ArmorRating_Sharp),
                BluntArmor = apparel == null ? 0f : thing.GetStatValue(StatDefOf.ArmorRating_Blunt)
            };
        }
    }
}
