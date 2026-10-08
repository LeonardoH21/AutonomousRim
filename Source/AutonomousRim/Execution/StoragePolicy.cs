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
            int generalStacks = map.listerThings.AllThings.Count(t => t.Spawned && t.def.EverStorable(false) &&
                t.def.category == ThingCategory.Item && !IsFood(t.def) && !t.def.IsCorpse && !t.Position.Fogged(map));
            int shelfTarget(RoomProject room,int stacks)
            {
                if(room==null)return 0;
                int current=room.Furniture.Count(t=>t.Def?.defName=="ShelfSmall");
                int floor=room.StorageCells.Count;
                int reserve=(int)Math.Ceiling(stacks*1.25f);
                // A shelf replaces one floor cell and adds two additional stack positions.
                int pressure=Math.Max(0,(reserve-floor+1)/2);
                bool established=Find.TickManager.TicksGame>=3*GenDate.TicksPerDay;
                int desired=Math.Max(pressure,established && stacks>=6?3:0);
                return Math.Min(current+2,Math.Min(36,desired));
            }
            int foodStacks=map.listerThings.AllThings.Count(t=>t.Spawned && IsFood(t.def) && !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer));
            QueueShelves(map,general,shelfTarget(general,generalStacks),projects);
            QueueShelves(map,freezer,shelfTarget(freezer,foodStacks),projects);
            int builtShelves=general?.Furniture.Count(t=>t.Def?.defName=="ShelfSmall" && t.Complete(map))??0;
            int capacity=(general?.StorageCells.Count??0)+builtShelves*2;
            string depot=$" Depósito: {generalStacks} pilhas conhecidas, {capacity} posições estimadas; {builtShelves} prateleiras prontas.";
            if(generalStacks>capacity && capacity>0)depot+=" Capacidade insuficiente: mobiliário interno prioritário; preservar corredores.";

            if (state.FreezerCapacityCells <= 0)
                return "Sem freezer funcional; alimentos perecíveis aguardam refrigeração."+depot;
            if (state.FreezerNearFull)
                return "Freezer quase cheio: prioridade Critical para entrada, produção excessiva pausada e prateleiras/expansão avaliadas."+depot;
            return $"Freezer organizado: {state.FreezerFillRatio:P0} ocupado; comida aceita no frio e estoque geral mantém filtro sem refeições."+depot;
        }

        private static void QueueShelves(Map map, RoomProject freezer, int desired, IReadOnlyList<RoomProject> projects)
        {
            // Upgrade completed rooms only; preserve player cancellations and room access.
            if (freezer == null || !freezer.Completed || freezer.State == ConstructionState.Paused) return;
            ThingDef shelf = DefDatabase<ThingDef>.GetNamedSilentFail("ShelfSmall");
            if (shelf == null) return; // Planned prerequisites feed the normal research planner.
            int existing = freezer.Furniture.Count(t => t.Def?.defName == shelf.defName);
            var occupied = new HashSet<IntVec3>(freezer.Shell.SelectMany(t => GenAdj.OccupiedRect(t.Position, t.Rotation, t.Def.Size)));
            occupied.UnionWith(freezer.Furniture.Where(t => t.Def is ThingDef).SelectMany(t => GenAdj.OccupiedRect(t.Position, t.Rotation, t.Def.Size)));
            occupied.UnionWith(projects.Where(p => p != freezer).SelectMany(p => p.Furniture)
                .Where(t => t.Def is ThingDef && !t.Def.defName.Contains("Conduit"))
                .SelectMany(t => GenAdj.OccupiedRect(t.Position, t.Rotation, t.Def.Size)));
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
