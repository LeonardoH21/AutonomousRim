using System;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;
using AutonomousRim.Core;
using AutonomousRim.Planning;

namespace AutonomousRim.Execution
{
    public static class StoragePolicy
    {
        public static bool IsFood(ThingDef def) => def.IsWithinCategory(DefDatabase<ThingCategoryDef>.GetNamed("Foods")) ||
            def.ingestible?.HumanEdible == true && def.ingestible.drugCategory == DrugCategory.None;

        public static void Configure(StorageSettings settings, string kind, bool shelf = false)
        {
            var filter = settings.filter;
            filter.SetDisallowAll();
            settings.Priority = kind == "Medicamentos" ? StoragePriority.Critical :
                kind == "Freezer" || kind == "Armas" ? StoragePriority.Important :
                kind == "Despejo" ? StoragePriority.Preferred : StoragePriority.Normal;
            if (shelf) settings.Priority = kind == "Estoque" ? StoragePriority.Preferred : StoragePriority.Critical;
            var humanMeats = new HashSet<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading.Where(r => r.race?.Humanlike == true).Select(r => r.race.meatDef));
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.EverStorable(false)))
            {
                bool animalCorpse = def.IsCorpse && def.ingestible?.sourceDef?.race?.Animal == true;
                bool allow = kind == "Freezer" ? IsFood(def) && !def.IsCorpse && !humanMeats.Contains(def) :
                    kind == "Medicamentos" ? def.IsMedicine :
                    kind == "Armas" ? def.IsWeapon :
                    kind == "Roupas" ? def.IsApparel :
                    kind == "Despejo" ? animalCorpse || def.thingCategories?.Contains(DefDatabase<ThingCategoryDef>.GetNamed("StoneChunks")) == true :
                    !IsFood(def) && !def.IsCorpse && def.defName != "Chemfuel" &&
                        def.thingCategories?.Contains(DefDatabase<ThingCategoryDef>.GetNamed("MortarShells")) != true;
                if (allow) filter.SetAllow(def, true);
            }
        }

        public static string ManageFoodStorage(Map map, ColonyState state, IReadOnlyList<RoomProject> projects)
        {
            if (map == null || state == null) return "Armazenamento de comida aguardando mapa.";
            var freezer = projects?.FirstOrDefault(p => p.Kind == "Freezer");
            var general = projects?.FirstOrDefault(p => p.Kind == "Estoque");
            if (freezer?.Stockpile != null)
            {
                var settings = freezer.Stockpile.GetStoreSettings();
                settings.Priority = state.FreezerNearFull ? StoragePriority.Critical : StoragePriority.Important;
            }
            if (general?.Stockpile != null)
            {
                var settings = general.Stockpile.GetStoreSettings();
                settings.Priority = StoragePriority.Normal;
            }
            QueueShelves(map, general, 6);
            QueueShelves(map, freezer, state.FreezerNearFull ? 8 : 4);

            if (state.FreezerCapacityCells <= 0)
                return "Sem freezer funcional; alimentos perecíveis aguardam refrigeração.";
            if (state.FreezerNearFull)
                return "Freezer quase cheio: prioridade Critical para entrada, produção excessiva pausada e prateleiras/expansão avaliadas.";
            return $"Freezer organizado: {state.FreezerFillRatio:P0} ocupado; comida aceita no frio e estoque geral mantém filtro sem refeições.";
        }

        private static void QueueShelves(Map map, RoomProject freezer, int desired)
        {
            // Upgrade completed rooms only; preserve player cancellations and room access.
            if (freezer == null || !freezer.Completed || freezer.State == ConstructionState.Paused ||
                freezer.Furniture.Any(t => t.CancelledByPlayer)) return;
            ThingDef shelf = DefDatabase<ThingDef>.GetNamedSilentFail("ShelfSmall");
            if (shelf == null) return; // Planned prerequisites feed the normal research planner.
            int existing = freezer.Furniture.Count(t => t.Def?.defName == shelf.defName);
            var occupied = new HashSet<IntVec3>(freezer.Shell.SelectMany(t => GenAdj.OccupiedRect(t.Position, t.Rotation, t.Def.Size)));
            occupied.UnionWith(freezer.Furniture.Where(t => t.Def is ThingDef).SelectMany(t => GenAdj.OccupiedRect(t.Position, t.Rotation, t.Def.Size)));
            while (existing < desired)
            {
                IntVec3 cell = freezer.Interior.Cells.Where(c =>
                    c.x != freezer.Interior.CenterCell.x && c.z != freezer.Interior.CenterCell.z &&
                    !freezer.Shell.Any(t => t.Def is ThingDef door && door.IsDoor &&
                        (t.Position.x == c.x || t.Position.z == c.z)) && !occupied.Contains(c) &&
                    c.GetEdifice(map) == null && (c.GetZone(map) == null || c.GetZone(map) == freezer.Stockpile)).DefaultIfEmpty(IntVec3.Invalid).First();
                if (!cell.IsValid) break;
                var task = new ConstructionTask
                {
                    Def = shelf, Position = cell, Stuff = shelf.MadeFromStuff ? ThingDefOf.WoodLog : null,
                    Rotation = Rot4.North, StorageKind = freezer.Kind
                };
                freezer.Furniture.Add(task);
                occupied.Add(cell);
                existing++;
                freezer.Completed = false;
                freezer.Priority = ConstructionPriority.High;
            }
        }
    }
}
