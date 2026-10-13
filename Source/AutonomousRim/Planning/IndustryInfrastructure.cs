using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Planning
{
    // Bounded additions inside approved workshops. All construction/hauling stays native.
    public static class IndustryInfrastructure
    {
        public const string BufferKind = "Insumos industriais";
        private static readonly string[] Benches = { "HandTailoringBench", "FueledSmithy", "TableMachining", "FabricationBench" };
        public static void Configure(StorageSettings settings,IEnumerable<ThingDef> inputs,bool active)
        {
            settings.filter.SetDisallowAll();
            foreach(var def in inputs.Where(d=>d!=null && !StoragePolicy.IsFood(d) && !d.IsMedicine && d.defName!="Leather_Human").Distinct())
                settings.filter.SetAllow(def,true);
            settings.Priority=active?StoragePriority.Important:StoragePriority.Preferred;
        }
        public static string Describe(StorageSettings settings)
        {
            var f=settings.filter;
            string range(FloatRange r)=>r.min.ToString("R",CultureInfo.InvariantCulture)+","+r.max.ToString("R",CultureInfo.InvariantCulture);
            return settings.Priority+"|"+range(f.AllowedHitPointsPercents)+"|"+range(f.AllowedMentalBreakChance)+"|"+
                (int)f.AllowedQualityLevels.min+","+(int)f.AllowedQualityLevels.max+"|"+
                string.Join(",",f.AllowedThingDefs.Select(d=>d.defName).OrderBy(n=>n,StringComparer.Ordinal))+"|"+
                string.Join(",",DefDatabase<SpecialThingFilterDef>.AllDefsListForReading.Where(d=>!f.Allows(d)).Select(d=>d.defName).OrderBy(n=>n,StringComparer.Ordinal));
        }
        public static string Status(Map map,IEnumerable<RoomProject> projects)
        {
            var industrial=projects.Where(p=>p.LayoutSlot?.StartsWith("industry:")==true).ToList();
            var buffers=industrial.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.StorageKind==BufferKind).ToList();
            int benches=industrial.Count(p=>p.LayoutSlot.StartsWith("industry:bench:") && p.Furniture[0].Complete(map));
            int needed=industrial.Count(p=>p.LayoutSlot.StartsWith("industry:bench:"));
            return $" Indústria: bancadas {benches}/{needed}; prateleiras {buffers.Count(t=>t.Complete(map))}/{buffers.Count}; {buffers.Count(t=>t.SupplyManual)} sob controle manual.";
        }
        private static Building_Storage Shelf(Map map,ConstructionTask task)=>task.Complete(map)?
            task.Position.GetThingList(map).OfType<Building_Storage>().FirstOrDefault(b=>b.def==task.Def && b.Position==task.Position && b.Faction==Faction.OfPlayer):null;
        private static bool Controlled(ConstructionTask task,Building_Storage shelf)
        {
            if(!task.Owned || !task.SettingsConfigured || task.SupplyManual || shelf==null)return false;
            if(task.SupplySignature!=Describe(shelf.GetStoreSettings())) {task.SupplyManual=true;return false;}
            return true;
        }
        public static void Suspend(Map map,IEnumerable<RoomProject> projects)
        {
            foreach(var task in projects.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.StorageKind==BufferKind))
            {
                var shelf=Shelf(map,task);
                if(!Controlled(task,shelf))continue;
                shelf.GetStoreSettings().Priority=StoragePriority.Preferred;
                task.SupplySignature=Describe(shelf.GetStoreSettings());
            }
        }
        private static List<ThingDef> Inputs(Map map,Building_WorkTable table,IEnumerable<Bill_Production> orders)
        {
            var result=orders.Where(b=>b.billStack==table.BillStack).SelectMany(b=>b.recipe.ingredients
                .Select(i=>IndustrySupplyPlan.PreferredInput(map,b.recipe,b,i))).Where(d=>d!=null && !StoragePolicy.IsFood(d) && !d.IsMedicine && d.defName!="Leather_Human").Distinct().ToList();
            var fuel=table.TryGetComp<CompRefuelable>();
            if(result.Count>0 && fuel?.allowAutoRefuel==true && fuel.Props.fuelFilter.Allows(ThingDefOf.WoodLog))result.Add(ThingDefOf.WoodLog);
            return result.Distinct().ToList();
        }
        private static RoomProject Host(List<RoomProject> projects,IntVec3 cell)=>projects.FirstOrDefault(p=>
            (p.Kind=="Estoque" || p.Kind=="Oficina" || p.Kind=="Fabricação") && p.State!=ConstructionState.Paused && p.Interior.Contains(cell));
        private static bool PowerNearby(Map map,List<RoomProject> projects,ThingDef def,IntVec3 cell)=>
            def.GetCompProperties<CompProperties_Power>()==null ||
            map.listerBuildings.allBuildingsColonist.Any(b=>b.def.GetCompProperties<CompProperties_Power>()?.transmitsPower==true && b.OccupiedRect().Cells.Any(c=>c.DistanceToSquared(cell)<=36)) ||
            projects.SelectMany(BaseConstructionManager.Tasks).Any(t=>!t.CancelledByPlayer && t.Def is ThingDef d &&
                d.GetCompProperties<CompProperties_Power>()?.transmitsPower==true && t.Position.DistanceToSquared(cell)<=36);
        private static ConstructionTask Place(Map map,List<RoomProject> projects,RoomProject host,ThingDef def,IntVec3 near)
        {
            var tasks=projects.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.Def is ThingDef && !t.Def.defName.Contains("Conduit")).ToList();
            var occupied=new HashSet<IntVec3>(tasks.SelectMany(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size)));
            occupied.UnionWith(map.listerBuildings.allBuildingsColonist.SelectMany(b=>b.OccupiedRect()));
            var interactions=new HashSet<IntVec3>(tasks.Where(t=>((ThingDef)t.Def).hasInteractionCell).Select(t=>t.Position+((ThingDef)t.Def).interactionCellOffset.RotatedBy(t.Rotation)));
            interactions.UnionWith(map.listerBuildings.allBuildingsColonist.Where(b=>b.def.hasInteractionCell).Select(b=>b.InteractionCell));
            var doors=host.Shell.Where(t=>t.Def is ThingDef d && d.IsDoor).Select(t=>t.Position)
                .Concat(map.listerBuildings.allBuildingsColonist.OfType<Building_Door>().Select(b=>b.Position)).ToList();
            foreach(var c in host.Interior.Cells.Where(c=>c.DistanceToSquared(near)<=64)
                .OrderBy(c=>Math.Min(Math.Min(c.x-host.Interior.minX,host.Interior.maxX-c.x),Math.Min(c.z-host.Interior.minZ,host.Interior.maxZ-c.z)))
                .ThenBy(c=>c.DistanceToSquared(near)))
            foreach(var rot in new[]{Rot4.North,Rot4.East,Rot4.South,Rot4.West})
            {
                var rect=GenAdj.OccupiedRect(c,rot,def.Size);var access=c+def.interactionCellOffset.RotatedBy(rot);
                if(rect.Any(n=>!n.InBounds(map) || !host.Interior.Contains(n) || occupied.Contains(n) || interactions.Contains(n) ||
                    n.x==host.Interior.CenterCell.x || n.z==host.Interior.CenterCell.z || n.GetEdifice(map)!=null ||
                    n.GetThingList(map).Any(t=>t is Blueprint || t is Frame) || n.GetZone(map)!=null && n.GetZone(map)!=host.Stockpile || doors.Any(d=>n.DistanceToSquared(d)<=2)))continue;
                if(def.hasInteractionCell && (!host.Interior.Contains(access) || occupied.Contains(access) || access.GetEdifice(map)!=null ||
                    access.GetThingList(map).Any(t=>t is Blueprint || t is Frame)))continue;
                if(!GenConstruct.CanPlaceBlueprintAt(def,c,rot,map,stuffDef:def.MadeFromStuff?ThingDefOf.WoodLog:null) || !PowerNearby(map,projects,def,c))continue;
                var reach=def.hasInteractionCell?access:c;
                if(!map.mapPawns.FreeColonistsSpawned.Any(p=>WorkPriorityManager.CanWork(p) && p.CanReach(reach,PathEndMode.Touch,Danger.None)))continue;
                return new ConstructionTask{Def=def,Position=c,Rotation=rot,Stuff=def.MadeFromStuff?ThingDefOf.WoodLog:null};
            }
            return null;
        }
        private static void Add(Map map,List<RoomProject> projects,RoomProject host,string key,ConstructionTask task)
        {
            var p=new RoomProject{Kind="Instalações industriais",LayoutSlot=key,Origin=host.Origin,InteriorSize=host.InteriorSize,
                InteriorHeight=host.Height,RequiresRoof=false,Priority=ConstructionPriority.High};
            p.Furniture.Add(task);projects.Add(p);
            // Release only the host's automatic floor zone, never a different player zone.
            if(host.Stockpile!=null)
                foreach(var c in GenAdj.OccupiedRect(task.Position,task.Rotation,task.Def.Size).Where(c=>c.GetZone(map)==host.Stockpile).ToList())host.Stockpile.RemoveCell(c);
        }
        public static void Update(Map map,AutonomousRimMapComponent ai,List<RoomProject> projects)
        {
            if(!ai.BaseAutomation || (!ai.EquipmentAutomation && !ai.GoalGraph.HasDemand) || ai.SecondarySuspended || ai.CurrentState?.HostilePawnCount!=0 ||
                ai.CurrentState.Threat?.Immediate==true || ai.CurrentState.EstimatedFoodDays<3 || EmergencyManager.LocalFire(map))
            {Suspend(map,projects);return;}
            var orders=ai.ManagedProduction.Where(m=>m.Matches && BillDiagnostics.Requested(m.Bill))
                .Select(m=>m.Bill).Take(12).ToList();
            var recipes=LoadoutProgression.Analyze(map,DefenseProductionPlan.Stage(map)).Craft.Keys.Select(name=>
                DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r=>r.products?.Any(p=>p.thingDef.defName==name)==true))
                .Concat(ai.Industry.Requirements.Where(r=>r.Missing>0 && IndustrySupplyPlan.Component(r.Def))
                    .Select(r=>DefDatabase<RecipeDef>.GetNamedSilentFail("Make_"+r.Def.defName)))
                .Concat(ai.GoalGraph.Recipes).Where(r=>r?.AvailableNow==true).Distinct().Take(12).ToList();
            int added=0;
            foreach(var recipe in recipes)
            {
                if(map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Any(t=>t.def.AllRecipes.Contains(recipe)))continue;
                var planned=projects.FirstOrDefault(p=>p.Furniture.Any(t=>t.Def is ThingDef d && d.AllRecipes?.Contains(recipe)==true));
                if(planned!=null)continue; // Preserve priorities, cancellations and approved reference furniture.
                if(added>=2)break;
                var def=Benches.Select(n=>DefDatabase<ThingDef>.GetNamedSilentFail(n)).Concat(DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.thingClass!=null && typeof(Building_WorkTable).IsAssignableFrom(d.thingClass) && d.building?.isNaturalRock!=true)).FirstOrDefault(d=>d?.IsResearchFinished==true && d.AllRecipes.Contains(recipe));
                if(def==null || projects.Any(p=>p.LayoutSlot=="industry:bench:"+def.defName))continue;
                foreach(var host in projects.Where(p=>(p.Kind=="Estoque" || p.Kind=="Oficina" || p.Kind=="Fabricação") &&
                    p.State!=ConstructionState.Paused && (p.Completed || p.FunctionalStorage || p.Shell.Count>0 && p.Shell.All(t=>t.Complete(map)))).ToList())
                {
                    var task=Place(map,projects,host,def,host.Interior.CenterCell);
                    if(task==null)continue;
                    Add(map,projects,host,"industry:bench:"+def.defName,task);added++;break;
                }
            }
            var active=map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Where(t=>!t.IsForbidden(Faction.OfPlayer) &&
                orders.Any(b=>b.billStack==t.BillStack)).Take(4).ToList();
            foreach(var task in projects.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.StorageKind==BufferKind).ToList())
            {
                var shelf=Shelf(map,task);var inputs=task.SupplyBench?.Spawned==true && active.Contains(task.SupplyBench)?Inputs(map,task.SupplyBench,orders):new List<ThingDef>();
                if(shelf==null)
                {
                    if(!task.SettingsConfigured)task.SupplyInputs=inputs;
                    continue;
                }
                if(!Controlled(task,shelf))continue;
                if(inputs.Count>0) {Configure(shelf.GetStoreSettings(),inputs,true);task.SupplyInputs=inputs;}
                else shelf.GetStoreSettings().Priority=StoragePriority.Preferred;
                task.SupplySignature=Describe(shelf.GetStoreSettings());
            }
            var shelfDef=DefDatabase<ThingDef>.GetNamedSilentFail("ShelfSmall");
            if(shelfDef?.IsResearchFinished!=true)return;
            int queued=0;
            foreach(var table in active)
            {
                var host=Host(projects,table.Position);if(host==null)continue;
                var inputs=Inputs(map,table,orders);if(inputs.Count==0)continue;
                for(int i=0;i<2 && queued<2;i++)
                {
                    string key="industry:buffer:"+table.thingIDNumber+":"+i;
                    if(projects.Any(p=>p.LayoutSlot==key))continue;
                    var task=Place(map,projects,host,shelfDef,table.Position);if(task==null)break;
                    task.StorageKind=BufferKind;task.SupplyInputs=inputs;task.SupplyBench=table;
                    Add(map,projects,host,key,task);queued++;
                }
            }
        }
    }
}
