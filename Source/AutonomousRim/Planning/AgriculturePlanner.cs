using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Execution;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    public static class AgriculturePlanner
    {
        public static void AddModular(Map map,List<RoomProject> projects)
        {
            int population=map.mapPawns.FreeColonistsSpawnedCount;
            foreach(var name in new[]{"Plant_Rice","Plant_Potato","Plant_Cotton","Plant_Healroot","Plant_Smokeleaf"})
            {
                var crop=DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if(crop?.plant==null)continue;
                // Vanilla grows cotton and smokeleaf; do not invent unavailable flax/hemp defs.
                int desired=name=="Plant_Rice" ? Math.Max(169,population*24) : 169;
                AddModularCrop(map,projects,crop,desired);
            }
        }

        public static void AddModularCrop(Map map,List<RoomProject> projects,ThingDef crop,int desired)
        {
            var root=projects.FirstOrDefault(p=>p.Kind==ModularBasePlanner.ReservationKind);
            if(root==null || crop?.plant==null)return;
            int existing=projects.Where(p=>p.Crop==crop && ModularBasePlanner.IsModular(p) && p.StorageCells.Count==169).Sum(p=>p.StorageCells.Count);
            if(existing>=desired)return;
            // Reserve whole grid cells. Adjacent farms must not consume the corridors or the future prison.
            foreach(var grid in ModularBasePlanner.Grid())
            {
                string prefix=$"mod:{grid.x}:{grid.z}:";
                if(projects.Any(p=>p.LayoutSlot?.StartsWith(prefix)==true))continue;
                var origin=root.LayoutAnchor+new IntVec3(grid.x*16,0,grid.z*16);
                var rect=new CellRect(origin.x,origin.z,13,13);
                if(!rect.ExpandedBy(3).InBounds(map) || projects.Any(p=>p.Crop==null && p.Kind!=ModularBasePlanner.ReservationKind && p.Footprint.Overlaps(rect)))continue;
                if(rect.Any(c=>c.Fogged(map) || c.GetZone(map)!=null || c.Roofed(map) ||
                    c.GetTerrain(map).fertility<crop.plant.fertilityMin || c.GetEdifice(map)!=null ||
                    c.GetThingList(map).Any(t=>t is Blueprint || t is Frame)))continue;
                if(!map.mapPawns.FreeColonistsSpawned.Any(p=>p.CanReach(rect.CenterCell,Verse.AI.PathEndMode.OnCell,Danger.None)))continue;
                projects.Add(new RoomProject{Kind="Plantação de "+crop.label,LayoutSlot=prefix+"15",LayoutAnchor=root.LayoutAnchor,
                    Origin=origin,InteriorSize=11,Crop=crop,RequiresRoof=false,StorageCells=rect.Cells.ToList(),
                    NoRoofCells=rect.Cells.ToList(),Priority=ConstructionPriority.High});
                return; // One full field per crop/review; no partial circular patches.
            }
        }
        public static void AddCommercial(Map map,List<RoomProject> projects,ThingDef crop,int desired)
        {
            if(projects.Any(p=>p.Kind==ModularBasePlanner.ReservationKind)) { AddModularCrop(map,projects,crop,desired); return; }
            var stock=projects.FirstOrDefault(p=>p.Kind=="Estoque" && (p.Completed || p.FunctionalStorage));
            if(stock==null)return;
            int existing=map.zoneManager.AllZones.OfType<Zone_Growing>().Where(z=>z.GetPlantDefToGrow()==crop).Sum(z=>z.Cells.Count)+
                projects.Where(p=>p.Crop==crop && p.GrowingZone==null).Sum(p=>p.StorageCells.Count);
            if(existing>=desired || !PlantUtility.GrowthSeasonNow(stock.Interior.CenterCell,map,crop))return;
            // Keep the entire approved envelope and future modules available; commercial plots grow outside it.
            var reserved=projects.Where(p=>p.Crop==null).SelectMany(p=>p.Footprint).ToHashSet();
            reserved.UnionWith(projects.Where(p=>p.Crop!=null).SelectMany(p=>p.StorageCells));
            var candidates=GenRadial.RadialCellsAround(stock.Interior.CenterCell,75,true).Where(c=>c.InBounds(map) && !c.Fogged(map) &&
                !reserved.Contains(c) && c.GetZone(map)==null && !c.Roofed(map) && c.GetEdifice(map)==null &&
                c.GetTerrain(map).fertility>=crop.plant.fertilityMin && c.GetThingList(map).All(t=>!(t is Blueprint) && !(t is Frame)) &&
                map.mapPawns.FreeColonistsSpawned.Any(p=>p.CanReach(c,Verse.AI.PathEndMode.OnCell,Danger.None))).ToList();
            if(candidates.Count<16)return;
            var origin=candidates[0];
            var cells=candidates.Where(c=>c.DistanceToSquared(origin)<=100).Take(System.Math.Min(36,desired-existing)).ToList();
            if(cells.Count<16)return;
            var rect=CellRect.FromLimits(cells.Min(c=>c.x),cells.Min(c=>c.z),cells.Max(c=>c.x),cells.Max(c=>c.z));
            projects.Add(new RoomProject{Kind="Plantação comercial",LayoutSlot="commerce:crop:"+projects.Count(p=>p.Kind=="Plantação comercial"),
                Origin=rect.Min,InteriorSize=System.Math.Max(1,rect.Width-2),InteriorHeight=System.Math.Max(1,rect.Height-2),
                Crop=crop,StorageCells=cells,NoRoofCells=cells,RequiresRoof=false,Priority=ConstructionPriority.Low});
        }
        public static int ClothDemand(Map map)
        {
            int population=map.mapPawns.FreeColonistsSpawnedCount;
            // Native recipe costs, not a fixed guessed cost per colonist.
            int perPerson=0;
            foreach(string name in new[]{"Apparel_Pants","Apparel_CollarShirt","Apparel_Parka","Apparel_Tuque"})
            {
                var recipe=DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r=>r.products?.Any(p=>p.thingDef.defName==name)==true);
                if(recipe!=null)perPerson+=recipe.ingredients.Where(i=>i.filter.Allows(ThingDefOf.Cloth)).Sum(i=>i.CountRequiredOfFor(ThingDefOf.Cloth,recipe));
            }
            int stock=map.listerThings.ThingsOfDef(ThingDefOf.Cloth).Where(t=>t.Spawned && !t.IsForbidden(Faction.OfPlayer)).Sum(t=>t.stackCount);
            var equipment=LoadoutProgression.Analyze(map,DefenseProductionPlan.Stage(map));
            int defenseCloth=equipment.Materials.TryGetValue("Cloth",out int need)?need:0;
            return Math.Max(population*30,population*perPerson+defenseCloth-stock);
        }
        public static void Add(Map map,List<RoomProject> projects)
        {
            if(projects.Any(p=>p.Kind==ModularBasePlanner.ReservationKind)) { AddModular(map,projects); return; }
            if(!projects.Any(p=>p.Kind=="Estoque" && (p.Completed || p.FunctionalStorage)))return;
            var center=projects.First(p=>p.Kind=="Estoque").Interior.CenterCell;
            var occupied=projects.Where(p=>p.Crop==null && p.Kind!=ModularBasePlanner.ReservationKind && p.Kind!=RingBasePlanner.ReservationKind)
                .SelectMany(p=>p.Footprint.Cells).ToHashSet();
            occupied.UnionWith(projects.Where(p=>p.Crop!=null).SelectMany(p=>p.StorageCells));
            void crop(string name,int desired,string kind)
            {
                var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);if(def?.plant==null)return;
                if(!map.mapPawns.FreeColonistsSpawned.Any(p=>!p.WorkTypeIsDisabled(WorkTypeDefOf.Growing) && p.skills.GetSkill(SkillDefOf.Plants).Level>=def.plant.sowMinSkill))return;
                int existing=map.zoneManager.AllZones.OfType<Zone_Growing>().Where(z=>z.GetPlantDefToGrow()==def).Sum(z=>z.Cells.Count);
                existing+=projects.Where(p=>p.Crop==def && p.GrowingZone==null).Sum(p=>p.StorageCells.Count);
                if(existing>=desired)return;
                string slot="agriculture:"+name+":"+projects.Count(p=>p.Crop==def);
                var candidates=GenRadial.RadialCellsAround(center,65,true).Where(c=>c.InBounds(map) && !c.Fogged(map) &&
                    !occupied.Contains(c) && c.GetZone(map)==null && !c.Roofed(map) && c.GetEdifice(map)==null &&
                    c.GetTerrain(map).fertility>=def.plant.fertilityMin && c.GetThingList(map).All(t=>!(t is Blueprint) && !(t is Frame)) &&
                    map.mapPawns.FreeColonistsSpawned.Any(p=>p.CanReach(c,Verse.AI.PathEndMode.OnCell,Danger.None)))
                    .OrderBy(c=>c.DistanceToSquared(center)).ToList();
                if(candidates.Count<16)return;
                var origin=candidates.First();
                var cells=candidates.Where(c=>c.DistanceToSquared(origin)<=100).Take(Math.Min(144,desired-existing)).ToList();
                if(cells.Count<16)return;
                var rect=CellRect.FromLimits(cells.Min(c=>c.x),cells.Min(c=>c.z),cells.Max(c=>c.x),cells.Max(c=>c.z));
                projects.Add(new RoomProject{Kind=kind,LayoutSlot=slot,Origin=new IntVec3(rect.minX,0,rect.minZ),
                    InteriorSize=Math.Max(1,rect.Width-2),InteriorHeight=Math.Max(1,rect.Height-2),RequiresRoof=false,
                    Crop=def,StorageCells=cells,NoRoofCells=cells,Priority=ConstructionPriority.High});
                occupied.UnionWith(cells);
            }
            int population=map.mapPawns.FreeColonistsSpawnedCount;
            crop("Plant_Rice",Math.Max(36,(population+map.mapPawns.AllPawnsSpawned.Count(p=>p.IsPrisonerOfColony))*24),"Plantação de comida");
            var cotton=DefDatabase<ThingDef>.GetNamed("Plant_Cotton");
            crop("Plant_Cotton",Math.Max(36,(int)Math.Ceiling(ClothDemand(map)/Math.Max(1f,cotton.plant.harvestYield))),"Plantação de algodão");
            crop("Plant_Healroot",Math.Max(24,(population+map.mapPawns.AllPawnsSpawned.Count(p=>p.IsPrisonerOfColony))*6),"Plantação medicinal");
        }
    }
}
