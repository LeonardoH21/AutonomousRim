using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Execution;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    public static class CommerceInfrastructure
    {
        public static void Add(Map map,List<RoomProject> projects,RecipeDef recipe)
        {
            var stock=projects.FirstOrDefault(p=>p.Kind=="Estoque" && (p.Completed || p.FunctionalStorage));
            if(stock==null)return;
            var research=projects.FirstOrDefault(p=>p.Kind=="Pesquisa" || p.Kind=="Laboratório");
            var workshop=projects.FirstOrDefault(p=>p.Kind=="Oficina" || p.Kind=="Fabricação");
            var occupied=projects.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.Def is ThingDef && !t.Def.defName.Contains("Conduit"))
                .SelectMany(t=>GenAdj.OccupiedRect(t.Position,t.Rotation,t.Def.Size)).ToHashSet();
            occupied.UnionWith(projects.SelectMany(BaseConstructionManager.Tasks).Where(t=>t.Def is ThingDef d && d.hasInteractionCell)
                .Select(t=>t.Position+((ThingDef)t.Def).interactionCellOffset.RotatedBy(t.Rotation)));
            occupied.UnionWith(map.listerBuildings.allBuildingsColonist.Where(b=>b.def.hasInteractionCell).Select(b=>b.InteractionCell));
            void add(RoomProject room,string name,string key,string storage=null)
            {
                var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if(room==null || def==null || projects.Any(p=>p.LayoutSlot==key))return;
                if(storage==null && (map.listerBuildings.allBuildingsColonist.Any(b=>b.def==def) ||
                    projects.SelectMany(BaseConstructionManager.Tasks).Any(t=>t.Def==def)))return;
                var positions=room.Interior.Cells.OrderBy(c=>c.DistanceToSquared(room.Interior.CenterCell));
                foreach(var c in positions)
                {
                    var task=new ConstructionTask{Def=def,Position=c,Stuff=def.MadeFromStuff?ThingDefOf.WoodLog:null,StorageKind=storage};
                    var rect=GenAdj.OccupiedRect(c,Rot4.North,def.Size);
                    var interaction=c+def.interactionCellOffset;
                    if(rect.Any(n=>!room.Interior.Contains(n) || occupied.Contains(n) || n.GetEdifice(map)!=null ||
                        n.GetThingList(map).Any(t=>t is Blueprint || t is Frame) || def.terrainAffordanceNeeded!=null && !n.GetTerrain(map).affordances.Contains(def.terrainAffordanceNeeded)))continue;
                    if(rect.Any(n=>n.x==room.Interior.CenterCell.x || n.z==room.Interior.CenterCell.z))continue;
                    if(def.hasInteractionCell && (!room.Interior.Contains(interaction) || occupied.Contains(interaction) || interaction.GetEdifice(map)!=null))continue;
                    if(room.Shell.Any(t=>t.Def is ThingDef door && door.IsDoor && rect.Any(n=>n.DistanceToSquared(t.Position)<=2)))continue;
                    var finish=new RoomProject{Kind="Instalações comerciais",LayoutSlot=key,Origin=room.Origin,
                        InteriorSize=room.InteriorSize,InteriorHeight=room.Height,RequiresRoof=false,Priority=ConstructionPriority.Normal};
                    finish.Furniture.Add(task);
                    occupied.UnionWith(rect);if(def.hasInteractionCell)occupied.Add(interaction);
                    if(def.GetCompProperties<CompProperties_Power>()!=null)
                    {
                        var nearest=map.listerBuildings.allBuildingsColonist.Where(b=>b.def.GetCompProperties<CompProperties_Power>()?.transmitsPower==true)
                            .Select(b=>b.Position).Concat(projects.SelectMany(BaseConstructionManager.Tasks)
                                .Where(t=>t.Def is ThingDef d && d.GetCompProperties<CompProperties_Power>()?.transmitsPower==true).Select(t=>t.Position))
                            .OrderBy(n=>n.DistanceToSquared(c)).DefaultIfEmpty(IntVec3.Invalid).First();
                        if(nearest.IsValid && nearest.DistanceToSquared(c)<10000)
                        {
                            var route=new List<IntVec3>();
                            for(int x=System.Math.Min(c.x,nearest.x);x<=System.Math.Max(c.x,nearest.x);x++)route.Add(new IntVec3(x,0,c.z));
                            for(int z=System.Math.Min(c.z,nearest.z);z<=System.Math.Max(c.z,nearest.z);z++)route.Add(new IntVec3(nearest.x,0,z));
                            foreach(var n in route.Distinct())
                            {
                                if(projects.SelectMany(BaseConstructionManager.Tasks).Any(t=>t.Position==n && t.Def.defName.Contains("Conduit")))continue;
                                if(n.GetEdifice(map)?.def.GetCompProperties<CompProperties_Power>()?.transmitsPower==true)continue;
                                bool wall=projects.SelectMany(p=>p.Shell).Any(t=>t.Position==n && t.Def==ThingDefOf.Wall) || n.GetEdifice(map)?.def==ThingDefOf.Wall;
                                var cable=DefDatabase<ThingDef>.GetNamedSilentFail(wall?"PowerConduit":"HiddenConduit");
                                if(cable!=null)finish.Furniture.Add(new ConstructionTask{Def=cable,Position=n});
                            }
                        }
                    }
                    projects.Add(finish);return;
                }
            }
            if(recipe!=null && !map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Any(t=>t.def.AllRecipes.Contains(recipe)))
            {
                // Prefer the researched lab; initial joints can use the native crafting spot.
                bool lab=DefDatabase<ThingDef>.GetNamedSilentFail("DrugLab")?.IsResearchFinished==true;
                add(workshop??stock,lab?"DrugLab":"CraftingSpot",lab?"commerce:lab":"commerce:crafting");
            }
            add(research??workshop??stock,"CommsConsole","commerce:console");
            add(stock,"OrbitalTradeBeacon","commerce:beacon");
            for(int i=0;i<2;i++)add(stock,"ShelfSmall","commerce:shelf:"+i,"Mercadorias");
        }
    }
}
