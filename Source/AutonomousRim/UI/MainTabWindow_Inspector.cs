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
            var component = map.GetComponent<AutonomousRimMapComponent>();
            float half = (inRect.width - 8f) / 2f;
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y + 36f, half, 30f), $"Base automática: {(component.BaseAutomation ? "LIGADA" : "DESLIGADA")}"))
                component.SetBaseAutomation(!component.BaseAutomation);
            if (Widgets.ButtonText(new Rect(inRect.x + half + 8f, inRect.y + 36f, half, 30f), $"Itens / autoequipar: {(component.EquipmentAutomation ? "LIGADO" : "DESLIGADO")}"))
                component.SetEquipmentAutomation(!component.EquipmentAutomation);
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y + 70f, half, 30f), $"Comida / roupas: {(component.FoodAutomation ? "LIGADA" : "DESLIGADA")}"))
                component.SetAutomation(!component.FoodAutomation, component.WorkAutomation);
            if (Widgets.ButtonText(new Rect(inRect.x + half + 8f, inRect.y + 70f, half, 30f), $"Prioridades: {(component.WorkAutomation ? "LIGADAS" : "DESLIGADAS")}"))
                component.SetAutomation(component.FoodAutomation, !component.WorkAutomation);
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y + 104f, half, 30f), "Planejar base (sem iniciar obras)")) component.PreviewBase();
            if (Widgets.ButtonText(new Rect(inRect.x + half + 8f, inRect.y + 104f, half, 30f), "Desligar todas as automações")) component.DisableAll();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y + 138f, inRect.width, 30f), $"Itens do chão / allow gradual: {(component.LootAutomation ? "LIGADO" : "DESLIGADO")}"))
                component.SetLootAutomation(!component.LootAutomation);
            state = component.CurrentState;
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y + 172f, half, 30f), $"Planejamento / pesquisa: {(component.StrategyAutomation ? "LIGADO" : "DESLIGADO")}"))
                component.SetStrategyAutomation(!component.StrategyAutomation);
            if (Widgets.ButtonText(new Rect(inRect.x + half + 8f, inRect.y + 172f, half, 30f), "Reavaliar metas estratégicas")) component.EvaluateStrategy();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y + 206f, half, 30f), $"Agenda dinâmica: {(component.ScheduleAutomation ? "LIGADA" : "DESLIGADA")}"))
                component.SetScheduleAutomation(!component.ScheduleAutomation);
            if (Widgets.ButtonText(new Rect(inRect.x + half + 8f, inRect.y + 206f, half, 30f), "Analisar risco/falha agora")) component.EvaluateFailure();
            Rect viewport = new Rect(inRect.x, inRect.y + 244f, inRect.width, inRect.height - 244f);
            Rect content = new Rect(0f, 0f, viewport.width - 20f, contentHeight);
            Widgets.BeginScrollView(viewport, ref scrollPosition, content);
            var listing = new Listing_Standard();
            listing.Begin(content);
            listing.Label(state.ToString());
            listing.Label(component.BaseStatus);
            listing.Label("Plano em anel: estoque e alimentação primeiro; quartos 5×5 por colono, cozinha e abate 4×4 separados; expansão reservada, energia e perímetro. Planos antigos são preservados.");
            listing.Label("Rota final: " + component.Strategy.VictoryRoute);
            listing.Label($"Horizonte: {component.Strategy.HorizonDays} dias | Dia atual: {component.Strategy.LastKnownDay} | Restantes: {component.Strategy.EstimatedDaysRemaining}");
            listing.Label(component.Strategy.StabilityStatus);
            listing.Label("Foco atual: " + component.Strategy.CurrentFocus);
            listing.Label("Próximo foco: " + component.Strategy.NextFocus);
            listing.Label(component.Strategy.ResearchStatus);
            listing.Label(component.Strategy.TerrainStatus);
            listing.Label(component.Strategy.MountainPlanStatus);
            listing.Label(component.ScheduleStatus);
            listing.Label("Diagnóstico de colapso: " + component.FailureStatus);
            if (component.FailureMemory.Latest != null)
            {
                var report=component.FailureMemory.Latest;
                listing.Label("Último relatório: " + report.PrimaryCause);
                listing.Label("Cadeia: " + string.Join(" → ", report.CausalChain.Take(4)));
                foreach(var improvement in report.Improvements.Take(4)) listing.Label("Ajuste aprendido: " + improvement);
            }
            listing.Label("Checkpoints de combate preservados: " + component.FailureMemory.Raids.Count + "; evidências de falhas: " + component.FailureMemory.Reports.Count);
            listing.Label("A agenda usa Sleep, Work, Recreation, Anything e Meditation quando disponível. Incêndios, hostis e feridos entram em emergência; depois há recuperação de descanso e recreação. Mudanças manuais de horários são preservadas.");
            foreach (var goal in component.Strategy.Goals)
                listing.Label($"{goal.Horizon} — {goal.Description}: {goal.Status}");
            listing.Label("Próximas pesquisas: " + string.Join(" → ", component.Strategy.Route.Take(8).Select(r => r.LabelCap.ToString())));
            foreach (var unlock in component.Strategy.Unlocks) listing.Label("Tecnologia: " + unlock);
            listing.Label(AutonomousRim.Planning.CompactBasePlanner.ClimateSummary(map));
            foreach (var project in component.BaseProjects)
                listing.Label($"{project.Kind} {(project.RequiresRoof ? $"{project.InteriorSize}×{project.Height}" : "instalações")} em {project.Origin}: {project.State}; prioridade {project.Priority}. {project.BlockReason}" +
                    (project.FunctionalStorage && !project.Completed ? " Estoque utilizável; proteção climática ainda em obra." : ""));
            listing.Label(component.GatheringStatus);
            listing.Label(component.ManagementStatus);
            listing.Label(component.EquipmentStatus);
            listing.Label(component.LootStatus);
            listing.Label("Quartos novos: cama junto à parede, cabeceira, cômoda e vaso; área 2×2 reservada para futura cama de casal. Allow prioriza necessidades e respeita acesso, políticas e reproibições manuais.");
            listing.Label("Autoequipamento usa tarefas do jogo e respeita peças forçadas pelo jogador. Ao desligar, libera as fixações da IA e mantém os equipamentos atuais.");
            listing.Label("Prioridades usam o modo numérico. Ao desligar, a IA restaura prioridades e remove suas ordens que não foram editadas pelo jogador.");
            ThreatState threat = state.Threat;
            listing.Label($"Threat: {threat.Risk} | Humans: {threat.Humanlikes} | Mechs: {threat.Mechanoids} | Animals: {threat.Animals} | Other: {threat.Other}");
            listing.Label($"Ranged: {threat.Ranged} | Melee/unarmed: {threat.Melee} | Strength heuristic: allies {threat.FriendlyStrength:0.0} / enemies {threat.EnemyStrength:0.0}");
            listing.Label(threat.LastTransition);
            listing.Label("Threat detection is based on hostile pawns, not raid incidents. Turrets, traps and special abilities are not modeled.");
            listing.Label($"Comida armazenada: {state.FoodNutrition:0.0} nutrição / ~{state.EstimatedFoodDays:0.0} dias | Consumo dos colonos: {state.DailyFoodNutrition:0.0}/dia");
            listing.Label($"Reserva de comida: {state.FoodReserveLevel} — {state.FoodReserveStatus}");
            listing.Label($"Cooking: {state.PreferredMeal} | alvo dinâmico {state.CookingTargetCount} refeições | reserva longa {state.StrategicMealTargetCount}");
            listing.Label($"Perecíveis: {state.PerishableFoodNutrition:0.0} | longa duração: {state.LongLifeFoodNutrition:0.0} | próximos de estragar: {state.NearSpoilingNutrition:0.0}");
            listing.Label($"Freezer: {state.FreezerUsedCells}/{state.FreezerCapacityCells} células ({state.FreezerFillRatio:P0}) | {state.FoodStorageStatus}");
            listing.Label("Reserva em dias usa fome e dietas dos colonos, alimentos permitidos no mapa e risco de deterioração; visitantes, animais e produção futura são excluídos.");
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
                Pawn actualPawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.thingIDNumber == pawn.PawnId);
                if (actualPawn != null)
                {
                    bool allowed = component.EquipmentAllowedFor(actualPawn);
                    listing.CheckboxLabeled($"Permitir autoequipamento para {pawn.Name}", ref allowed);
                    if (allowed != component.EquipmentAllowedFor(actualPawn)) component.SetEquipmentAllowedFor(actualPawn, allowed);
                }
                listing.Label($"Perfil preferido: {pawn.PreferredCombatRole}");
                listing.Label($"Shooting: {pawn.Shooting} | Melee: {pawn.Melee} | Medical: {pawn.Medical}");
                listing.Label($"Weapon: {pawn.Weapon} | Range: {pawn.WeaponRange:0.0}");
                if (pawn.Equipment != null)
                {
                    listing.Label($"Weapon DPS: {pawn.Equipment.DamagePerSecond:0.0} | Penetration: {pawn.Equipment.ArmorPenetration:P0} | Condition: {pawn.Equipment.Condition:P0}");
                    listing.Label(pawn.Equipment.Notes);
                }
                listing.Label($"Suggested weapon: {pawn.RecommendedWeapon}");
                listing.Label($"Roupa sugerida: {pawn.RecommendedApparel}");
                listing.Label(pawn.EquipmentReason);
                listing.Label($"Apparel: {pawn.Apparel}");
                listing.Label($"Armor by garment (coverage not combined): {pawn.ArmorDetails}");
                listing.Label($"Traits: {pawn.Traits}");
                listing.Label(pawn.TraitEffects);
                listing.Label(pawn.DerivedStats);
                listing.GapLine();
            }
            listing.End();
            contentHeight = Mathf.Max(viewport.height, listing.CurHeight + 24f);
            Widgets.EndScrollView();
        }
    }
}
