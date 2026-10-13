using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Planning;
using RimWorld;
using Verse;
namespace AutonomousRim.Execution
{
    public sealed class ConstructionWorkIndex
    {
        private int nextTick=-1,projectCount;
        public readonly Dictionary<Thing,RoomProject> Owners=new Dictionary<Thing,RoomProject>();
        public readonly Dictionary<Thing,ConstructionTask> Tasks=new Dictionary<Thing,ConstructionTask>();
        public readonly Dictionary<IntVec3,RoomProject> ClearOwners=new Dictionary<IntVec3,RoomProject>();
        public readonly HashSet<IntVec3> Protected=new HashSet<IntVec3>();
        public readonly HashSet<IntVec3> Interactions=new HashSet<IntVec3>();
        public readonly HashSet<IntVec3> DebrisCells=new HashSet<IntVec3>();
        public List<Thing> Chunks=new List<Thing>();
        public void Invalidate(){nextTick=-1;}
        public void Refresh(Map map,IReadOnlyList<RoomProject> projects)
        {
            int tick=Find.TickManager.TicksGame;if(tick<nextTick && projectCount==projects.Count)return;
            nextTick=tick+60;projectCount=projects.Count;Owners.Clear();Tasks.Clear();ClearOwners.Clear();Protected.Clear();Interactions.Clear();DebrisCells.Clear();
            foreach(var project in projects.OrderBy(p=>p.Priority))
            {
                foreach(var cell in project.OwnedClearCells)if(!ClearOwners.ContainsKey(cell))ClearOwners[cell]=project;
                var stage=project.Started && !project.Completed && project.State!=ConstructionState.Paused ? new HashSet<ConstructionTask>(BaseConstructionManager.CurrentStage(map,project)) : null;
                foreach(var task in BaseConstructionManager.Tasks(project))
                {
                    var cells=task.Def is TerrainDef?(IEnumerable<IntVec3>)new[]{task.Position}:GenAdj.OccupiedRect(task.Position,task.Rotation,task.Def.Size);
                    Protected.UnionWith(cells);
                    if(task.Def is ThingDef planned && planned.hasInteractionCell)
                        Protected.UnionWith(ThingUtility.InteractionCellsWhenAt(planned,task.Position,task.Rotation,map).Where(c=>c.InBounds(map)));
                    if(task.Pending?.Spawned==true && !Owners.ContainsKey(task.Pending)){Owners[task.Pending]=project;Tasks[task.Pending]=task;}
                    if(project.Completed || project.State==ConstructionState.Paused || task.CancelledByPlayer || task.Complete(map))continue;
                    DebrisCells.UnionWith(cells);
                    if(task.Def is ThingDef def && def.hasInteractionCell)
                    {
                        var access=ThingUtility.InteractionCellsWhenAt(def,task.Position,task.Rotation,map).Where(c=>c.InBounds(map)).ToList();
                        DebrisCells.UnionWith(access);Protected.UnionWith(access);
                        if(stage?.Contains(task)==true && task.Pending?.Spawned!=true)Interactions.UnionWith(access);
                    }
                }
            }
            var category=DefDatabase<ThingCategoryDef>.GetNamed("StoneChunks");
            Chunks=map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Where(t=>t.Spawned &&
                t.def.thingCategories?.Contains(category)==true && DebrisCells.Contains(t.Position)).ToList();
        }
    }
}
