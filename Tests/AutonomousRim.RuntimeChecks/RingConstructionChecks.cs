using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    // Geometry/state fixture only: a separate generated colony receives flat soil.
    // No claim about complete native construction time or all-biome thermal performance.
    public sealed class RingConstructionChecks : MapComponent
    {
        private static int stage;
        public RingConstructionChecks(Map map) : base(map) { }
        private static void Check(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
        public override void MapComponentTick()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimringtest") || stage==99 || Find.TickManager.TicksGame%60!=0 || map.mapPawns.FreeColonistsSpawnedCount==0)return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(stage==2)
                {
                    Check(!ai.BaseAutomation,"Disabled automation state lost on native load.");
                    Check(ai.BaseProjects.Count(p=>p.Kind=="Quarto")==7,"Expanded bedroom list lost on native load.");
                    Check(ai.BaseProjects.Single(p=>p.LayoutSlot=="rice").GrowingZone.GetPlantDefToGrow().defName=="Plant_Potato","Manual crop changed on second load.");
                    stage=99;Log.Message("[AutonomousRim.RingTests] PASS: separated 4x4 cooking/butchering, tier-one essentials, bedrooms 3/4/7/12, reserved power/perimeter, exhaust shaft, crop bounds/filters, mining/deconstruction cleanup, bounded funded issuance, native enabled/disabled save/load and preserved manual crop.");return;
                }
                if(stage==1)
                {
                    Check(ai.BaseAutomation,"Saved enabled state changed on load.");
                    ai.SetBaseAutomation(false);
                    var loaded=ai.BaseProjects.ToList();
                    Check(loaded.Any(p=>p.Kind==RingBasePlanner.ReservationKind),"Reservation missing after native load.");
                    Check(loaded.Count(p=>p.GrowingZone!=null)==5,"Growing zone references lost on load.");
                    var rice=loaded.Single(p=>p.LayoutSlot=="rice");
                    Check(rice.GrowingZone.GetPlantDefToGrow().defName=="Plant_Potato","Manual crop change was overwritten.");
                    int oldCount=loaded.Count; ai.PreviewBase(); Check(ai.BaseProjects.Count==oldCount,"Replanning duplicated modules after load.");
                    var pawn=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);
                    GenSpawn.Spawn(pawn,map.mapPawns.FreeColonistsSpawned.First().Position,map);
                    ai.PreviewBase();Check(ai.BaseProjects.Count(p=>p.Kind=="Quarto")==4,"Fourth colonist did not receive a reserved bedroom.");
                    var savedAnchor=ai.BaseProjects.Single(p=>p.Kind==RingBasePlanner.ReservationKind).LayoutAnchor;
                    var opening=RingBasePlanner.At(savedAnchor,17,0);
                    var oldWall=ai.BaseProjects.Single(p=>p.LayoutSlot=="bed0").Shell.Single(t=>t.Position==opening);
                    var builtWall=ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.WoodLog);builtWall.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(builtWall,opening,map);
                    oldWall.Owned=true;oldWall.WasCompleted=true;
                    for(int i=0;i<3;i++)GenSpawn.Spawn(PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer),pawn.Position,map);
                    ai.PreviewBase();Check(ai.BaseProjects.Count(p=>p.Kind=="Quarto")==7,"Seventh colonist did not receive a reserved extension.");
                    Check(ai.BaseProjects.Single(p=>p.LayoutSlot=="bed6").ClearCells.Contains(opening),"Owned wall opening was not recorded.");
                    foreach(var hostile in map.mapPawns.AllPawnsSpawned.Where(p=>p.HostileTo(Faction.OfPlayer)).ToList())hostile.DeSpawn();
                    // The loaded enabled component has already executed this tick; emulate a later toggle.
                    AccessTools.Field(typeof(AutonomousRimMapComponent),"lastConstructionTick").SetValue(ai,-1);
                    ai.SetBaseAutomation(true);Check(ai.BaseAutomation,"Enable failed after load.");
                    Check(map.designationManager.DesignationOn(builtWall,DesignationDefOf.Deconstruct)!=null,"Owned opening lacks a native deconstruction order: "+ai.BaseStatus+"; "+string.Join("; ",ai.BaseProjects.Where(p=>p.LayoutSlot=="bed6").Select(p=>p.State+":"+p.BlockReason)));
                    var giver=(WorkGiver_Scanner)DefDatabase<WorkGiverDef>.AllDefsListForReading.First(d=>d.giverClass==typeof(WorkGiver_AutonomousConstruction) && d.workType==WorkTypeDefOf.Construction).Worker;
                    var builder=map.mapPawns.FreeColonistsSpawned.First();
                    Check(giver.PotentialWorkThingsGlobal(builder).Contains(builtWall),"Opening was omitted from prioritized construction targets.");
                    Check(giver.JobOnThing(builder,builtWall,false)!=null,"Opening cannot produce a native deconstruction job.");
                    ai.SetBaseAutomation(false);Check(!ai.BaseAutomation,"Disable failed after load.");
                    Check(map.designationManager.DesignationOn(builtWall,DesignationDefOf.Deconstruct)==null,"Owned opening deconstruction not cleaned on disable.");
                    GameDataSaveLoader.SaveGame("RingPlanDisabledRoundtrip");stage=2;GameDataSaveLoader.LoadGame("RingPlanDisabledRoundtrip");return;
                }
                Check(ai.BaseAutomation,"New colony should start with requested base automation enabled.");
                ai.DisableAll();
                var hostiles=map.mapPawns.AllPawnsSpawned.Where(p=>p.HostileTo(Faction.OfPlayer)).ToList();
                foreach(var hostile in hostiles)hostile.DeSpawn();
                Log.Message("[AutonomousRim.RingTests] FIXTURE: isolating geometry from "+hostiles.Count+" generated hostiles; flat uncovered soil, skilled pawns and supplied materials.");
                var first=map.mapPawns.FreeColonistsSpawned.First();
                var anchor=first.Position-new IntVec3(30,0,22);
                foreach(var c in RingBasePlanner.Envelope(anchor).ExpandedBy(1))
                {
                    Check(c.InBounds(map),"Fixture envelope out of bounds.");
                    foreach(var t in c.GetThingList(map).Where(t=>!(t is Pawn)).ToList()) {if(t.def.destroyable)t.Destroy();else t.DeSpawn();}
                    map.terrainGrid.SetTerrain(c,DefDatabase<TerrainDef>.GetNamed("Soil"));map.roofGrid.SetRoof(c,null);map.fogGrid.Unfog(c);
                }
                foreach(string name in new[]{"Electricity","AirConditioning","ComplexFurniture","Stonecutting","ComplexClothing"})
                    Find.ResearchManager.FinishProject(DefDatabase<ResearchProjectDef>.GetNamed(name),false,null,false);
                foreach(var pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    foreach(var trait in pawn.story.traits.allTraits.ToList())pawn.story.traits.RemoveTrait(trait);
                    pawn.story.Childhood=DefDatabase<BackstoryDef>.AllDefsListForReading.First(b=>b.slot==BackstorySlot.Childhood && b.workDisables==WorkTags.None);
                    if(pawn.story.Adulthood!=null)pawn.story.Adulthood=DefDatabase<BackstoryDef>.AllDefsListForReading.First(b=>b.slot==BackstorySlot.Adulthood && b.workDisables==WorkTags.None);
                    pawn.Notify_DisabledWorkTypesChanged();foreach(var skill in pawn.skills.skills)skill.Level=20;
                    if(!pawn.workSettings.Initialized)pawn.workSettings.EnableAndInitialize();
                    pawn.workSettings.SetPriority(WorkTypeDefOf.Construction,1);
                    pawn.workSettings.SetPriority(WorkTypeDefOf.Mining,1);
                }
                foreach(var def in new[]{ThingDefOf.WoodLog,ThingDefOf.Steel})for(int i=0;i<6;i++)
                {var item=ThingMaker.MakeThing(def);item.stackCount=def.stackLimit;GenSpawn.Spawn(item,first.Position+new IntVec3(i,0,0),map);}
                var plan=RingBasePlanner.Create(map,anchor,3);
                Check(plan!=null,"Native ring layout rejected.");
                Check(plan.Count(p=>p.Kind=="Quarto")==3,"Wrong initial bedroom count.");
                var kitchen=plan.Single(p=>p.Kind=="Cozinha");var butcher=plan.Single(p=>p.Kind=="Abate");
                Check(kitchen.InteriorSize==4 && kitchen.Height==4 && butcher.InteriorSize==4 && butcher.Height==4,"Food rooms must be 4x4 internally.");
                Check(kitchen.Furniture.All(t=>t.Def.defName!="TableButcher") && butcher.Furniture.Any(t=>t.Def.defName=="TableButcher"),"Butcher table is not isolated.");
                Check(!kitchen.Interior.Overlaps(butcher.Interior),"Food interiors overlap.");
                Check(plan.Where(p=>new[]{"Estoque","Cozinha","Abate","Refeitório"}.Contains(p.Kind)).All(p=>p.Priority==ConstructionPriority.Critical),"Essentials have unequal priorities.");
                var stock=plan.Single(p=>p.Kind=="Estoque");Check(stock.InteriorSize==12 && stock.Height==11,"Stock geometry changed.");
                var power=plan.Single(p=>p.Kind=="Energia e climatização");var perimeter=plan.Single(p=>p.Kind=="Muro externo");
                Check(power.Furniture.Count(t=>t.Def.defName=="WoodFiredGenerator")>=2,"No generator supply.");
                Check(power.Furniture.Where(t=>t.Def.defName=="WoodFiredGenerator").All(t=>perimeter.Interior.Contains(t.Position)),"Generators outside perimeter.");
                Check(perimeter.Shell.Count(t=>t.Def==ThingDefOf.Door)==4,"Perimeter needs two two-cell exits.");
                var full=RingBasePlanner.Create(map,anchor,12);Check(full!=null && full.Count(p=>p.Kind=="Quarto")==12,"Twelve-colonist template rejected.");
                var hall=plan.Single(p=>p.Kind=="Corredor");var freezer=plan.Single(p=>p.Kind=="Freezer");
                Check(freezer.Shell.Where(t=>t.Def.defName=="Cooler").All(t=>t.TargetTemperature==-2),"Freezer target changed.");
                foreach(var cooler in freezer.Shell.Where(t=>t.Def.defName=="Cooler"))
                {var hot=cooler.Position+IntVec3.East;Check(!plan.Where(p=>p.RequiresRoof).Any(p=>p.RoofArea.Contains(hot)),"Exhaust shaft will be roofed.");}
                Check(plan.Where(p=>p.Crop!=null).Sum(p=>p.StorageCells.Count)==662,"Crop area changed or overlaps.");
                Check(plan.Single(p=>p.LayoutSlot=="hemp").StorageCells.Count==12,"Hemp reserve changed.");
                AccessTools.Field(typeof(AutonomousRimMapComponent),"baseProjects").SetValue(ai,plan);
                int countBefore=plan.Count;ai.PreviewBase();Check(plan.Count==countBefore,"Duplicate modules on repeated preview.");
                // Clearing generated hives/ancient structures can spawn defenders during fixture preparation.
                foreach(var hostile in map.mapPawns.AllPawnsSpawned.Where(p=>p.HostileTo(Faction.OfPlayer)).ToList())hostile.DeSpawn();
                var firstStatus=BaseConstructionManager.Apply(map,plan);
                Check(map.listerThings.AllThings.Any(t=>t is Blueprint_Build),"Funded fixture issued no blueprints: "+firstStatus+"; "+string.Join("; ",plan.Take(6).Select(p=>p.Kind+":"+p.State+":"+p.BlockReason)));
                Check(map.listerThings.AllThings.Count(t=>t is Blueprint_Build)<=BaseConstructionManager.MaxPerCycle,"Issuance exceeded cycle limit.");
                Check(plan.Single(p=>p.Kind=="Estoque").Stockpile!=null,"Standard storage is not immediately available.");
                Check(!stock.Stockpile.GetStoreSettings().AllowedToAccept(DefDatabase<ThingDef>.GetNamed("MealSimple")),"Normal stock permits meals.");
                Check(!plan.Any(p=>p.GrowingZone!=null),"Crops created before essentials.");
                foreach(var p in plan.Where(p=>p.Priority==ConstructionPriority.Critical || p.Kind=="Quarto"))p.Completed=true;
                BaseConstructionManager.Apply(map,plan);
                Check(plan.Count(p=>p.GrowingZone!=null)==5,"Native crop zones missing: "+string.Join("; ",plan.Where(p=>p.Crop!=null || p.Priority==ConstructionPriority.Critical).Select(p=>p.LayoutSlot+":"+p.Completed+":"+p.State+":"+p.BlockReason)));
                Check(plan.Single(p=>p.LayoutSlot=="rice").GrowingZone.Cells.Count==212,"Rice did not fill its corner.");
                plan.Single(p=>p.LayoutSlot=="rice").GrowingZone.SetPlantDefToGrow(DefDatabase<ThingDef>.GetNamed("Plant_Potato"));
                var rockCell=RingBasePlanner.At(anchor,0,0);
                GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Granite")),rockCell,map);
                var prep=new RoomProject{Kind="Preparação do terreno",RequiresRoof=false,MineCells=new List<IntVec3>{rockCell}};plan.Add(prep);
                BaseConstructionManager.Apply(map,plan);Check(map.designationManager.DesignationAt(rockCell,DesignationDefOf.Mine)!=null,"Mining not designated.");
                Check(rockCell.GetEdifice(map)!=null,"Mining obstacle removed instantly.");
                BaseConstructionManager.Stop(map,plan);Check(map.designationManager.DesignationAt(rockCell,DesignationDefOf.Mine)==null,"Owned mining designation not cleaned.");
                plan.Remove(prep);
                AccessTools.Field(typeof(AutonomousRimMapComponent),"baseAutomation").SetValue(ai,true);
                GameDataSaveLoader.SaveGame("RingPlanRoundtrip");stage=1;GameDataSaveLoader.LoadGame("RingPlanRoundtrip");
            }
            catch(Exception e){stage=99;Log.Error("[AutonomousRim.RingTests] FAIL: "+e);}
        }
    }
}
