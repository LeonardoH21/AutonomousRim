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
    // Disposable EliteSix scenario. Research completion below is fixture setup, never campaign progress.
    public sealed class ModularExecutionChecks : MapComponent
    {
        private bool started,done;
        private int start;
        private List<RoomProject> rooms;
        public ModularExecutionChecks(Map map):base(map){}
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private bool Blocked(RoomProject p)=>(bool)AccessTools.Method(typeof(BaseConstructionManager),"DependsOn").Invoke(null,new object[]{map,p,rooms});
        public override void MapComponentTick()
        {
            if(done || !GenCommandLine.CommandLineArgPassed("autonomousrimmodularexecutiontest") || !EliteFixtureBaseline.Ready || Find.TickManager.TicksGame<300)return;
            try
            {
                if(!started)
                {
                    started=true;start=Find.TickManager.TicksGame;
                    EliteFixtureBaseline.Roster(map);
                    var ai=map.GetComponent<AutonomousRimMapComponent>();
                    ai.EnableAll();Check(ai.AllAutomationEnabled,"Master switch failed to enable all modules.");
                    rooms=(List<RoomProject>)ai.BaseProjects;
                    ModularBasePlanner.Plan(map,rooms);
                    var dining=rooms.Single(p=>p.Kind=="Refeitório e recreação");
                    Check(dining.Footprint.Width==13 && dining.Footprint.Height==13 && !Blocked(dining),"Dining not planned/executable during initial shelter construction.");
                    Check(dining.Furniture.Any(t=>t.Def.defName=="Table3x3c") && dining.Furniture.Count(t=>t.Def.defName=="DiningChair")>=6,"Dining has no usable table/seats.");
                    Log.Message("[ModularExecutionTests] PASS: full 13x13 dining planned immediately with large table and dining chairs.");
                    var power=rooms.Single(p=>p.Kind=="Energia e climatização");
                    Check(power.InteriorSize==11 && power.Height==5,"Energy does not use two quarters.");
                    var crops=rooms.Where(p=>p.Crop!=null && ModularBasePlanner.IsModular(p)).ToList();
                    Check(crops.Count>=5 && crops.Select(p=>p.Crop).Distinct().Count()>=5,"Five initial fields missing: "+string.Join(",",crops.Select(p=>p.Crop.defName)));
                    foreach(var crop in crops)
                    {
                        Check(crop.StorageCells.Count==169 && crop.StorageCells.Distinct().Count()==169 && !Blocked(crop),"Field not full 13x13 or blocked by bedrooms.");
                        Check(!crop.RequiresRoof && crop.NoRoofCells.Count==169,"Covered growing field.");
                    }
                    BaseConstructionManager.Apply(map,rooms);
                    Check(crops.All(p=>p.GrowingZone?.Cells.Count==169 && p.GrowingZone.GetPlantDefToGrow()==p.Crop),"Native zones not created completely.");
                    Check(rooms.Any(p=>p.Kind=="Despejo" && p.Stockpile!=null),"Dump absent.");
                    Log.Message("[ModularExecutionTests] PASS: five native 169-cell zones with crops, uncovered; dump created; energy 11x5 interior.");
                    Check(rooms.Where(p=>ModularBasePlanner.IsModular(p) && p.Crop==null).All(p=>p.Furniture.Any(t=>t.Def.defName=="WallLamp")),"Essential room light missing.");
                    // Local corridor dependency must not wait for a completely different unfinished room.
                    var path=rooms.First(p=>p.Kind=="Corredor");
                    var prefix=path.LayoutSlot.Replace("corridor:","mod:")+":";
                    var locals=rooms.Where(p=>p.LayoutSlot?.StartsWith(prefix)==true).ToList();
                    var shells=locals.ToDictionary(p=>p,p=>p.Shell);
                    foreach(var room in locals)room.Shell=new List<ConstructionTask>();
                    Check(!Blocked(path),"Unrelated unfinished module blocks corridor.");
                    foreach(var room in locals)room.Shell=shells[room];
                    Check(path.Furniture.All(t=>t.Def.defName=="PavedTile"),"Wrong corridor floor.");
                    Check(CostListCalculator.CostListAdjusted(DefDatabase<TerrainDef>.GetNamed("PavedTile"),null).Any(c=>c.thingDef==ThingDefOf.Steel && c.count==2),"Native concrete cost differs from requested two steel.");
                    Log.Message("[ModularExecutionTests] PASS: lights included in essential rooms; concrete corridor gates are local and native cost is two steel.");
                    // Controlled unlock transition: no purchased/free research enters the natural campaign.
                    foreach(var name in new[]{"Smithing","Machining","DrugProduction","MicroelectronicsBasics","MultiAnalyzer","Fabrication"})
                        foreach(var r in StrategicPlanner.ResearchRoute(new[]{DefDatabase<ResearchProjectDef>.GetNamed(name)}))
                            Find.ResearchManager.FinishProject(r,false,null,false);
                    ModularBasePlanner.Plan(map,rooms);
                    foreach(var name in new[]{"FueledSmithy","TableMachining","DrugLab","FabricationBench"})
                        Check(rooms.SelectMany(BaseConstructionManager.Tasks).Any(t=>t.Def.defName==name),"Unlocked bench not planned: "+name);
                    foreach(var room in rooms.Where(p=>p.Kind=="Oficina" || p.Kind=="Fabricação"))
                    {
                        var tasks=room.Furniture.Where(t=>t.Def is ThingDef).ToList();
                        foreach(var task in tasks)
                        {
                            var rect=GenAdj.OccupiedRect(task.Position,task.Rotation,task.Def.Size);
                            Check(rect.All(room.Interior.Contains),"Furniture outside workshop.");
                            Check(!tasks.Any(other=>other!=task && rect.Overlaps(GenAdj.OccupiedRect(other.Position,other.Rotation,other.Def.Size))),"Workshop furniture overlap.");
                            if(((ThingDef)task.Def).hasInteractionCell)
                            {
                                var access=task.Position+((ThingDef)task.Def).interactionCellOffset.RotatedBy(task.Rotation);
                                Check(room.Interior.Contains(access) && !tasks.Any(other=>GenAdj.OccupiedRect(other.Position,other.Rotation,other.Def.Size).Contains(access)),"Workbench interaction blocked.");
                            }
                        }
                    }
                    int count=rooms.SelectMany(BaseConstructionManager.Tasks).Count(t=>t.Def is ThingDef);
                    ModularBasePlanner.Plan(map,rooms);
                    Check(rooms.SelectMany(BaseConstructionManager.Tasks).Count(t=>t.Def is ThingDef)==count,"Repeated plan duplicates installations.");
                    Log.Message("[ModularExecutionTests] PASS: unlock -> workbench task; workshop footprints/access valid; repeated planning does not duplicate.");
                    ai.SetStrategyAutomation(false); // Keep controlled unlock check separate from organic research.
                    Log.Message("[ModularExecutionTests] Waiting for native sowing and construction by original colonists at 3x.");
                }
                if(Find.TickManager.TicksGame%300!=0)return;
                EliteFixtureBaseline.VerifyOriginalsPresent(map);
                int planted=rooms.Where(p=>p.Crop!=null).Sum(p=>p.StorageCells.Count(c=>c.GetPlant(map)?.def==p.Crop));
                int walls=rooms.SelectMany(p=>p.Shell).Distinct().Count(t=>t.Complete(map));
                if(Find.TickManager.TicksGame%3000==0)
                    Log.Message($"[ModularExecutionTests] Progress tick={Find.TickManager.TicksGame-start}; planted={planted}; walls={walls}; "+
                        string.Join("; ",map.mapPawns.FreeColonistsSpawned.Select(p=>p.LabelShort+" "+p.CurJob?.def.defName+
                            " grow="+p.workSettings.GetPriority(WorkTypeDefOf.Growing)+" build="+p.workSettings.GetPriority(WorkTypeDefOf.Construction))));
                if(planted>=12 && walls>=8)
                {
                    Log.Message($"[ModularExecutionTests] PASS: native labor planted {planted} cells and completed {walls} wall/door tasks; original six preserved.");
                    GameDataSaveLoader.SaveGame("ModularExecutionEvidence");
                    done=true;Log.Message("[ModularExecutionTests] DONE");
                }
                else if(Find.TickManager.TicksGame-start>30000)throw new InvalidOperationException($"Native execution timeout: planted={planted}, walls={walls}.");
            }
            catch(Exception e){done=true;Log.Error("[ModularExecutionTests] FAIL "+e);GameDataSaveLoader.SaveGame("ModularExecutionFailure");}
        }
    }
}
