using AutonomousRim.Core;
using RimWorld;
using UnityEngine;
using Verse;
using System.Linq;

namespace AutonomousRim.UI
{
    public sealed class MainTabWindow_Inspector : MainTabWindow
    {
        private Vector2 scrollPosition;
        private float contentHeight = 1000f;
        private int lootPage;
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

            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "AutonomousRim — Colônia, equipamentos e automação");
            Rect viewport = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 36f);
            Rect content = new Rect(0f, 0f, viewport.width - 20f, contentHeight);
            Widgets.BeginScrollView(viewport, ref scrollPosition, content);
            var listing = new Listing_Standard();
            listing.Begin(content);
            listing.Label(state.ToString());
            var component = map.GetComponent<AutonomousRimMapComponent>();
            bool foodAutomation = component.FoodAutomation;
            bool workAutomation = component.WorkAutomation;
            listing.CheckboxLabeled("Alimentação automática: caça, abate e refeições", ref foodAutomation);
            listing.CheckboxLabeled("Gerenciar prioridades de trabalho dos colonos", ref workAutomation);
            if (foodAutomation != component.FoodAutomation || workAutomation != component.WorkAutomation)
                component.SetAutomation(foodAutomation, workAutomation);
            listing.Label(component.ManagementStatus);
            listing.Label("Prioridades usam o modo numérico. Ao desligar, a IA restaura prioridades e remove suas ordens que não foram editadas pelo jogador.");
            ThreatState threat = state.Threat;
            listing.Label($"Threat: {threat.Risk} | Humans: {threat.Humanlikes} | Mechs: {threat.Mechanoids} | Animals: {threat.Animals} | Other: {threat.Other}");
            listing.Label($"Ranged: {threat.Ranged} | Melee/unarmed: {threat.Melee} | Strength heuristic: allies {threat.FriendlyStrength:0.0} / enemies {threat.EnemyStrength:0.0}");
            listing.Label(threat.LastTransition);
            listing.Label("Threat detection is based on hostile pawns, not raid incidents. Turrets, traps and special abilities are not modeled.");
            listing.Label($"Comida armazenada: {state.FoodNutrition:0.0} nutrição / ~{state.EstimatedFoodDays:0.0} dias | Consumo dos colonos: {state.DailyFoodNutrition:0.0}/dia");
            listing.Label("Reserva estimada com fome e dietas dos colonos; visitantes, animais, acesso físico e deterioração futura não estão incluídos.");
            foreach (var resource in state.Resources)
                listing.Label($"{resource.Key}: {resource.Value}");
            listing.GapLine();
            listing.Label($"Armas e roupas identificadas no mapa: {state.Loot.Count} (inclui itens proibidos e equipamentos em cadáveres)");
            int pages = Mathf.Max(1, (state.Loot.Count + 39) / 40);
            lootPage = Mathf.Clamp(lootPage, 0, pages - 1);
            if (pages > 1 && listing.ButtonText($"Equipamentos: página {lootPage + 1}/{pages} — próxima página")) lootPage = (lootPage + 1) % pages;
            foreach (LootProfile loot in state.Loot.Skip(lootPage * 40).Take(40))
            {
                listing.Label($"{loot.Kind}: {loot.Name} | Estado: {loot.Condition:P0} | Proibido: {loot.Forbidden} | Usado por cadáver: {loot.Tainted}");
                listing.Label(loot.Location);
                if (loot.Weapon != null) listing.Label($"DPS: {loot.Weapon.DamagePerSecond:0.0} | Alcance: {loot.Weapon.Range:0.0}");
                else listing.Label($"Proteção cortante: {loot.SharpArmor:P0} | Contusão: {loot.BluntArmor:P0}");
            }
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
