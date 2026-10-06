using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;

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
    }
}
