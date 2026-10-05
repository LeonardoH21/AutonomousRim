using AutonomousRim.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace AutonomousRim.UI
{
    public sealed class MainTabWindow_Inspector : MainTabWindow
    {
        private Vector2 scrollPosition;
        private float contentHeight = 1000f;
        public override Vector2 RequestedTabSize => new Vector2(760f, 580f);

        public override void DoWindowContents(Rect inRect)
        {
            Map map = Find.CurrentMap;
            ColonyState state = map?.GetComponent<AutonomousRimMapComponent>()?.CurrentState;
            if (state == null)
            {
                Widgets.Label(inRect, "AutonomousRim: waiting for a colony scan. Open a colony and allow the game to run.");
                return;
            }

            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "AutonomousRim — Colony inspector (observation mode)");
            Rect viewport = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 36f);
            Rect content = new Rect(0f, 0f, viewport.width - 20f, contentHeight);
            Widgets.BeginScrollView(viewport, ref scrollPosition, content);
            var listing = new Listing_Standard();
            listing.Begin(content);
            listing.Label(state.ToString());
            ThreatState threat = state.Threat;
            listing.Label($"Threat: {threat.Risk} | Humans: {threat.Humanlikes} | Mechs: {threat.Mechanoids} | Animals: {threat.Animals} | Other: {threat.Other}");
            listing.Label($"Ranged: {threat.Ranged} | Melee/unarmed: {threat.Melee} | Strength heuristic: allies {threat.FriendlyStrength:0.0} / enemies {threat.EnemyStrength:0.0}");
            listing.Label(threat.LastTransition);
            listing.Label("Threat detection is based on hostile pawns, not raid incidents. Turrets, traps and special abilities are not modeled.");
            listing.Label($"Stored food: {state.FoodNutrition:0.0} nutrition / ~{state.EstimatedFoodDays:0.0} days (adult baseline)");
            listing.Label("Food estimate excludes genes, guests, animals and spoilage. Combat score is an initial heuristic.");
            foreach (var resource in state.Resources)
                listing.Label($"{resource.Key}: {resource.Value}");
            listing.GapLine();
            foreach (PawnProfile pawn in state.Pawns)
            {
                listing.Label($"{pawn.Name} — {pawn.Role} | Health: {pawn.Health:P0} | Combat: {pawn.CombatValue:0.0}");
                listing.Label($"Shooting: {pawn.Shooting} | Melee: {pawn.Melee} | Medical: {pawn.Medical}");
                listing.Label($"Weapon: {pawn.Weapon} | Range: {pawn.WeaponRange:0.0}");
                if (pawn.Equipment != null)
                {
                    listing.Label($"Weapon DPS: {pawn.Equipment.DamagePerSecond:0.0} | Penetration: {pawn.Equipment.ArmorPenetration:P0} | Condition: {pawn.Equipment.Condition:P0}");
                    listing.Label(pawn.Equipment.Notes);
                }
                listing.Label($"Suggested weapon: {pawn.RecommendedWeapon}");
                listing.Label($"Apparel: {pawn.Apparel}");
                listing.Label($"Armor by garment (coverage not combined): {pawn.ArmorDetails}");
                listing.Label($"Traits: {pawn.Traits}");
                listing.GapLine();
            }
            listing.End();
            contentHeight = Mathf.Max(viewport.height, listing.CurHeight + 24f);
            Widgets.EndScrollView();
        }
    }
}
