using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Planning
{
    public static class PrisonPlanner
    {
        public const string ReservationKind="Reserva da prisão";
        public static void Plan(Map map,List<RoomProject> projects,PrisonState state)
        {
            var hospital=projects.FirstOrDefault(p=>p.Kind=="Hospital");
            var stock=projects.FirstOrDefault(p=>p.Kind=="Estoque" && (p.Completed || p.FunctionalStorage));
            if(stock==null){state.Status="Prisão aguarda estoque funcional e abrigo dos colonos.";return;}
            var marker=projects.FirstOrDefault(p=>p.Kind==ReservationKind);
            var anchor=hospital?.Interior.CenterCell??stock.Interior.CenterCell;
            if(marker==null)
            {
                var modular=projects.FirstOrDefault(p=>p.Kind==ModularBasePlanner.ReservationKind);
                IEnumerable<IntVec3> candidates=modular!=null?ModularBasePlanner.Grid().Select(g=>modular.LayoutAnchor+new IntVec3(g.x*ModularBasePlanner.Stride,0,g.z*ModularBasePlanner.Stride)):
                    GenRadial.RadialCellsAround(anchor,45,true).Where(c=>c.x%2==0 && c.z%2==0);
                foreach(var origin in candidates.Where(c=>c.DistanceToSquared(anchor)<45*45).OrderBy(c=>c.DistanceToSquared(anchor)))
                {
                    var rect=new CellRect(origin.x,origin.z,11,11);
                    if(!rect.ExpandedBy(2).InBounds(map) || projects.Any(p=>p.Footprint.ExpandedBy(2).Overlaps(rect)) ||
                        rect.ExpandedBy(1).Any(c=>c.CloseToEdge(map,10) || c.Fogged(map) || c.GetZone(map)!=null || map.areaManager.NoRoof[c] ||
                            !c.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy) ||
                            c.GetThingList(map).Any(t=>!(t is Pawn) && !(t is Plant) && !(t is Filth) && !(t is Mineable) &&
                                !(t.def.category==ThingCategory.Item && t.def.EverHaulable))))continue;
                    var approach=origin+new IntVec3(3,0,-1);
                    if(!map.mapPawns.FreeColonistsSpawned.Any(p=>p.CanReach(approach,PathEndMode.OnCell,Danger.None)))continue;
                    marker=new RoomProject{Kind=ReservationKind,LayoutSlot="prison:reserve",Origin=origin,InteriorSize=9,InteriorHeight=9,
                        RequiresRoof=false,Completed=true};
                    projects.Add(marker);break;
                }
                if(marker==null){state.Status="Sem espaço firme e acessível próximo do núcleo para a prisão; preservar expansão e plantações.";return;}
            }
            int desired=state.Capacity>=3 && projects.Count(p=>p.Kind=="Cela" && p.Completed)>=2?4:3;
            var shared=projects.SelectMany(p=>p.Shell).GroupBy(t=>t.Position).ToDictionary(g=>g.Key,g=>g.First());
            for(int i=0;i<desired;i++)
            {
                string key="prison:cell:"+i;
                if(projects.Any(p=>p.LayoutSlot==key))continue;
                var origin=marker.Origin+new IntVec3(i%2*5,0,i/2*5);
                var room=new RoomProject{Kind=i==2?"Enfermaria de prisioneiros":"Cela",LayoutSlot=key,Origin=origin,
                    InteriorSize=4,InteriorHeight=4,Priority=ConstructionPriority.Normal};
                var door=i<2?origin+new IntVec3(3,0,0):origin+new IntVec3(i==2?0:5,0,3);
                foreach(var c in room.Footprint.EdgeCells)
                {
                    if(!shared.TryGetValue(c,out var task))
                    {
                        task=new ConstructionTask{Def=c==door?ThingDefOf.Door:ThingDefOf.Wall,Stuff=ThingDefOf.WoodLog,Position=c,
                            Rotation=c==door && i>=2?Rot4.East:Rot4.North};shared[c]=task;
                    }
                    room.Shell.Add(task);
                }
                room.Furniture.Add(new ConstructionTask{Def=ThingDefOf.Bed,Stuff=ThingDefOf.WoodLog,Position=origin+new IntVec3(1,0,3),PrisonerBed=true,MedicalBed=i==2});
                // Finishing is separate: a lamp/table research cannot delay a capture bed.
                var finish=new RoomProject{Kind="Acabamento da prisão",LayoutSlot="prison:finish:"+i,Origin=origin,
                    InteriorSize=4,InteriorHeight=4,RequiresRoof=false,Priority=ConstructionPriority.Low};
                foreach(var c in room.Interior.Concat(new[]{door}))finish.Furniture.Add(new ConstructionTask{Def=DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor"),Position=c,OriginalTerrain=c.GetTerrain(map)});
                finish.Furniture.Add(new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed("Table1x2c"),Stuff=ThingDefOf.WoodLog,Position=origin+new IntVec3(3,0,3)});
                finish.Furniture.Add(new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed("DiningChair"),Stuff=ThingDefOf.WoodLog,Position=origin+new IntVec3(3,0,2)});
                finish.Furniture.Add(new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed("WallLamp"),Position=origin+new IntVec3(4,0,4)});
                if(map.mapTemperature.OutdoorTemp<15)
                    finish.Furniture.Add(new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed("Heater"),Position=origin+new IntVec3(4,0,1),TargetTemperature=21});
                else if(map.mapTemperature.OutdoorTemp>28)
                    finish.Furniture.Add(new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed("PassiveCooler"),Stuff=null,Position=origin+new IntVec3(4,0,1)});
                var prep=new RoomProject{Kind="Preparação do terreno",LayoutSlot="prison:prep:"+i,Origin=origin,InteriorSize=4,InteriorHeight=4,RequiresRoof=false,Priority=ConstructionPriority.Normal};
                foreach(var c in room.Footprint.Concat(new[]{door+(i<2?IntVec3.South:i==2?IntVec3.West:IntVec3.East)}).Distinct())
                {
                    if(c.GetEdifice(map) is Mineable)prep.MineCells.Add(c);
                    if(c.GetThingList(map).OfType<Plant>().Any())prep.PlantCells.Add(c);
                }
                if(prep.MineCells.Count>0 || prep.PlantCells.Count>0)projects.Add(prep);
                projects.Add(room);projects.Add(finish);
            }
            if(!projects.Any(p=>p.LayoutSlot=="prison:access"))
            {
                var access=new RoomProject{Kind="Acesso da prisão",LayoutSlot="prison:access",Origin=marker.Origin,
                    InteriorSize=9,InteriorHeight=9,RequiresRoof=false,Priority=ConstructionPriority.Low};
                foreach(var c in marker.Footprint.ExpandedBy(1).Where(c=>!marker.Footprint.Contains(c)))
                    access.Furniture.Add(new ConstructionTask{Def=DefDatabase<TerrainDef>.GetNamed("Concrete"),Position=c,OriginalTerrain=c.GetTerrain(map)});
                var prep=new RoomProject{Kind="Preparação do terreno",LayoutSlot="prison:access-prep",Origin=marker.Origin,
                    InteriorSize=9,InteriorHeight=9,RequiresRoof=false,Priority=ConstructionPriority.Low};
                foreach(var c in access.Furniture.Select(t=>t.Position))
                {
                    if(c.GetEdifice(map) is Mineable)prep.MineCells.Add(c);
                    if(c.GetThingList(map).OfType<Plant>().Any())prep.PlantCells.Add(c);
                }
                if(prep.MineCells.Count>0 || prep.PlantCells.Count>0)projects.Add(prep);
                projects.Add(access);
            }
            foreach(var finish in projects.Where(p=>p.LayoutSlot?.StartsWith("prison:finish:")==true).ToList())Wire(map,projects,finish);
        }
        private static void Wire(Map map,List<RoomProject> projects,RoomProject finish)
        {
            int before=finish.Furniture.Count;
            foreach(var appliance in finish.Furniture.Where(t=>t.Def is ThingDef d && d.GetCompProperties<CompProperties_Power>()!=null && !d.defName.Contains("Conduit")).ToList())
            {
                var nearest=map.listerBuildings.allBuildingsColonist.Where(b=>b.def.GetCompProperties<CompProperties_Power>()?.transmitsPower==true)
                    .Select(b=>b.Position).Concat(projects.Where(p=>p!=finish).SelectMany(BaseConstructionManager.Tasks)
                        .Where(t=>t.Def is ThingDef d && d.GetCompProperties<CompProperties_Power>()?.transmitsPower==true).Select(t=>t.Position))
                    .OrderBy(c=>c.DistanceToSquared(appliance.Position)).DefaultIfEmpty(IntVec3.Invalid).First();
                if(!nearest.IsValid || nearest.DistanceToSquared(appliance.Position)>3600)continue;
                var route=new List<IntVec3>();
                for(int x=Math.Min(appliance.Position.x,nearest.x);x<=Math.Max(appliance.Position.x,nearest.x);x++)route.Add(new IntVec3(x,0,appliance.Position.z));
                for(int z=Math.Min(appliance.Position.z,nearest.z);z<=Math.Max(appliance.Position.z,nearest.z);z++)route.Add(new IntVec3(nearest.x,0,z));
                if(route.Any(c=>!c.InBounds(map) || c.Fogged(map) || !c.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy)))continue;
                foreach(var c in route.Distinct())
                {
                    if(projects.SelectMany(BaseConstructionManager.Tasks).Any(t=>t.Position==c && t.Def.defName.Contains("Conduit")) ||
                        c.GetEdifice(map)?.def.GetCompProperties<CompProperties_Power>()?.transmitsPower==true)continue;
                    bool wall=projects.SelectMany(p=>p.Shell).Any(t=>t.Position==c && t.Def==ThingDefOf.Wall) || c.GetEdifice(map)?.def==ThingDefOf.Wall;
                    finish.Furniture.Add(new ConstructionTask{Def=DefDatabase<ThingDef>.GetNamed(wall?"PowerConduit":"HiddenConduit"),Position=c});
                    if(c.GetEdifice(map) is Mineable)
                    {
                        var key="prison:wire-prep:"+finish.LayoutSlot;
                        var prep=projects.FirstOrDefault(p=>p.LayoutSlot==key);
                        if(prep==null)
                        {
                            prep=new RoomProject{Kind="Preparação do terreno",LayoutSlot=key,Origin=finish.Origin,InteriorSize=4,
                                RequiresRoof=false,Priority=ConstructionPriority.Normal};projects.Add(prep);
                        }
                        if(!prep.MineCells.Contains(c))prep.MineCells.Add(c);prep.Completed=false;
                    }
                }
                if(finish.Furniture.Count>before)finish.Completed=false;
            }
        }
        public static float CandidateScore(Map map,Pawn pawn)
        {
            float score=0;
            foreach(var skill in new[]{SkillDefOf.Medicine,SkillDefOf.Cooking,SkillDefOf.Plants,SkillDefOf.Construction,SkillDefOf.Crafting,SkillDefOf.Intellectual,SkillDefOf.Shooting,SkillDefOf.Melee,SkillDefOf.Social})
            {
                var record=pawn.skills?.GetSkill(skill);if(record==null || record.TotallyDisabled)continue;
                int best=map.mapPawns.FreeColonistsSpawned.Where(p=>p.skills?.GetSkill(skill).TotallyDisabled==false).Select(p=>p.skills.GetSkill(skill).Level).DefaultIfEmpty(0).Max();
                float value=record.Level*.5f+(int)record.passion*2+Math.Max(0,record.Level-best)*1.5f;
                if(best<6)value*=1.4f;
                score=Math.Max(score,value);
            }
            if(AutonomousRim.Perception.TraitAnalyzer.HasActiveTrait(pawn,"Tough"))score+=3;
            if(AutonomousRim.Perception.TraitAnalyzer.HasActiveTrait(pawn,"Pyromaniac"))score-=5;
            score-=pawn.health.hediffSet.hediffs.Count(h=>h is Hediff_MissingPart)*1.5f;
            if(pawn.ageTracker.AgeBiologicalYears>=65)score-=3;
            return score;
        }
    }
}
