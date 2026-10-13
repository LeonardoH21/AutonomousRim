using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace AutonomousRim.Planning
{
    // Read-only housing recognition: decorations and exact bed rotation do not define shelter.
    public sealed class ExistingHousing
    {
        private int nextTick=-1;
        public List<Building_Bed> Beds=new List<Building_Bed>();
        public void Refresh(Map map,bool force=false)
        {
            int tick=Find.TickManager.TicksGame;if(!force && tick<nextTick)return;nextTick=tick+120;
            Beds=map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Where(b=>b.Spawned && !b.ForPrisoners && !b.Medical &&
                b.def.building.bed_humanlike && !b.IsForbidden(Faction.OfPlayer) && !b.IsBurning() && b.GetRoom() is Room room &&
                !room.Dereferenced && !room.PsychologicallyOutdoors && !room.IsDoorway &&
                (room.Role==RoomRoleDefOf.Bedroom || room.Role==RoomRoleDefOf.Barracks)).Distinct().ToList();
        }
        public bool Ready(Map map,RoomProject project)
        {
            Refresh(map);
            return Beds.Any(b=>project.Interior.Contains(b.Position));
        }
        public int Count(Map map){Refresh(map);return Beds.Count;}
    }
}
