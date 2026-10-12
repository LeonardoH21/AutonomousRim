using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;

namespace AutonomousRim.Perception
{
    public static class ObservationReader
    {
        private static string contentFingerprint;
        private static string ContentFingerprint()
        {
            if(contentFingerprint!=null)return contentFingerprint;
            try {
                // Content changes under the same package ID must not reuse old evidence.
                // Hash once per process, not once per observation. Settings remain contextual.
                var signatures=new List<string>();
                foreach(var mod in LoadedModManager.RunningModsListForReading.OrderBy(m=>m.PackageIdPlayerFacing)) {
                    using(var sha=System.Security.Cryptography.SHA256.Create()) {
                        foreach(var file in System.IO.Directory.EnumerateFiles(mod.RootDir,"*",System.IO.SearchOption.AllDirectories)
                            .Where(f=>f.EndsWith(".xml",System.StringComparison.OrdinalIgnoreCase)||f.EndsWith(".dll",System.StringComparison.OrdinalIgnoreCase)).OrderBy(f=>f,System.StringComparer.Ordinal)) {
                            var name=System.Text.Encoding.UTF8.GetBytes(file.Substring(mod.RootDir.Length).Replace('\\','/'));
                            sha.TransformBlock(name,0,name.Length,name,0);
                            using(var stream=System.IO.File.OpenRead(file)) {
                                var buffer=new byte[65536];int count;while((count=stream.Read(buffer,0,buffer.Length))>0)sha.TransformBlock(buffer,0,count,buffer,0);
                            }
                        }
                        sha.TransformFinalBlock(new byte[0],0,0);
                        signatures.Add(mod.PackageIdPlayerFacing+":"+System.BitConverter.ToString(sha.Hash).Replace("-",""));
                    }
                }
                return contentFingerprint=string.Join(",",signatures);
            }catch(System.Exception error) {Log.Warning("[AutonomousRim] Contexto de conteúdo não verificável: "+error.GetType().Name);return contentFingerprint="unverified";}
        }
        public static ObservationFrame Capture(Map map, ColonyState state, AutonomousRimMapComponent ai)
        {
            var research=Find.ResearchManager.GetProject();
            return new ObservationFrame { Tick=state.Tick, MapId=map.uniqueID, Colonists=state.ColonistCount,
                Enemies=state.HostilePawnCount, Downed=state.DownedColonists, Meals=state.StoredMealCount,
                FoodDays=state.EstimatedFoodDays, FoodNutrition=state.FoodNutrition, DailyNutrition=state.DailyFoodNutrition,
                BatteryEnergy=map.listerBuildings.allBuildingsColonist.Sum(b=>b.TryGetComp<CompPowerBattery>()?.StoredEnergy??0),
                Research=research==null?0:Find.ResearchManager.GetProgress(research), ResearchDef=research?.defName??"nenhuma",
                Completed=ai.Progress.TotalCompletedProduction, Policy=ai.Observations.Policy,
                Resources=string.Join(", ",state.Resources.OrderBy(r=>r.Key).Select(r=>r.Key+"="+r.Value)),
                Workers=string.Join("; ",map.mapPawns.FreeColonistsSpawned.Select(p=>p.LabelShort+"="+(p.CurJob?.def.defName??"sem job")+
                    "["+string.Join(",",DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w=>p.workSettings!=null && !p.WorkTypeIsDisabled(w) && p.workSettings.GetPriority(w)>0)
                        .Select(w=>w.defName+":"+p.workSettings.GetPriority(w)))+"]")),
                Context=VersionControl.CurrentVersionString+"|"+map.Biome.defName+"|"+Find.Storyteller.def.defName+"|"+
                    Find.Storyteller.difficultyDef?.defName+"|"+DifficultyFingerprint()+"|"+
                    ContentFingerprint() };
        }
        private static string DifficultyFingerprint() => string.Join(";",Find.Storyteller.difficulty.GetType().GetFields()
            .Where(f=>f.FieldType.IsPrimitive || f.FieldType.IsEnum).OrderBy(f=>f.Name)
            .Select(f=>f.Name+"="+System.Convert.ToString(f.GetValue(Find.Storyteller.difficulty),System.Globalization.CultureInfo.InvariantCulture)));
    }

    public sealed class RoomObservation
    {
        public int Id, Cells, Unroofed, Doors, Vents;
        public float Temperature;
        public string Role, Problems;
        public readonly List<string> Connections=new List<string>();
    }
    public static class RoomObserver
    {
        // Inspect existing native rooms, not planned rectangles. Paths remain native job checks.
        public static List<RoomObservation> Read(Map map)
        {
            var rooms=map.regionGrid.AllRooms
                .Where(r=>r!=null && !r.Dereferenced && !r.IsDoorway && !r.PsychologicallyOutdoors && r.CellCount<=1200)
                .Where(r=>r.ContainedAndAdjacentThings.Any(t=>t.Faction==Faction.OfPlayer) || r.Cells.Any(c=>map.areaManager.Home[c]))
                .Distinct().OrderBy(r=>r.ID).Take(64).ToList();
            var result=new List<RoomObservation>();
            foreach(var room in rooms)
            {
                var row=new RoomObservation {Id=room.ID,Cells=room.CellCount,Unroofed=room.OpenRoofCount,
                    Role=room.Role?.LabelCap.ToString()??"Sem função",Temperature=room.Temperature};
                foreach(var door in room.BorderCells.Where(c=>c.InBounds(map)).Select(c=>c.GetEdifice(map)).OfType<Building_Door>().Distinct())
                {
                    row.Doors++;
                    var destinations=GenAdj.CardinalDirections.Select(d=>door.Position+d).Where(c=>c.InBounds(map))
                        .Select(c=>c.GetRoom(map)).Where(r=>r!=null && r!=room && !r.IsDoorway).Distinct();
                    foreach(var other in destinations) row.Connections.Add("porta "+door.ThingID+" → "+
                        (other.PsychologicallyOutdoors?"exterior":"sala #"+other.ID)+
                        (door.IsForbidden(Faction.OfPlayer)?" (proibida)":"")+(door.Open?" (aberta)":""));
                }
                row.Vents=room.ContainedAndAdjacentThings.OfType<Building>().Distinct().Count(b=>b.def.defName=="Vent");
                var problems=new List<string>();
                if(row.Doors==0)problems.Add("sem porta detectada"); if(row.Unroofed>0)problems.Add(row.Unroofed+" células sem teto");
                // Low temperature in a freezer is intentional; avoid a universal cold alarm.
                if(room.ContainedBeds.Any() && (row.Temperature<10 || row.Temperature>32))problems.Add("temperatura inadequada para descanso");
                row.Problems=string.Join("; ",problems); result.Add(row);
            }
            return result;
        }
    }
    public sealed class ItemCatalogEntry
    {
        public ThingDef Item;
        public readonly List<RecipeDef> Recipes=new List<RecipeDef>();
        public readonly List<ThingDef> Benches=new List<ThingDef>();
        public string Route => Recipes.Count==0 ? "Sem produto de receita fixa: obter por coleta, comércio ou mecanismo próprio do mod" :
            string.Join("; ",Recipes.Take(3).Select(r=>r.LabelCap+" ["+string.Join(", ",(r.researchPrerequisites??new List<ResearchProjectDef>())
                .Concat(r.researchPrerequisite==null?Enumerable.Empty<ResearchProjectDef>():new[]{r.researchPrerequisite}).Distinct().Select(p=>p.LabelCap.ToString()))+"]"))+
            " • bancadas: "+string.Join(", ",Benches.Select(b=>b.LabelCap.ToString()));
    }
    public static class ItemCatalog
    {
        private static Dictionary<ThingDef,ItemCatalogEntry> entries;
        public static IReadOnlyDictionary<ThingDef,ItemCatalogEntry> Entries
        {
            get
            {
                if(entries!=null)return entries;
                entries=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.category==ThingCategory.Item)
                    .ToDictionary(d=>d,d=>new ItemCatalogEntry{Item=d});
                foreach(var recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
                    foreach(var product in recipe.products??new List<ThingDefCountClass>())
                        if(entries.TryGetValue(product.thingDef,out var row))row.Recipes.Add(recipe);
                foreach(var bench in DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.IsWorkTable))
                    foreach(var recipe in bench.AllRecipes)
                        foreach(var product in recipe.products??new List<ThingDefCountClass>())
                            if(entries.TryGetValue(product.thingDef,out var row) && !row.Benches.Contains(bench))row.Benches.Add(bench);
                return entries;
            }
        }
    }
}
