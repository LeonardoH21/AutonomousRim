using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Execution;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Planning
{
    // One bounded, read-only native inventory per map. Room shapes and template coordinates
    // are not evidence of a service; usable furniture, recipes and storage are.
    public sealed class ExistingFacilities
    {
        public sealed class Service
        {
            public string Kind, Detail;
            public IntVec3 Anchor;
            public Room Room;
            public int Capacity;
        }
        public readonly List<Service> Services = new List<Service>();
        private int nextTick = -1;
        public void Invalidate() { nextTick = -1; }
        private static bool Indoors(Room room) => room != null && !room.Dereferenced && !room.PsychologicallyOutdoors && !room.IsDoorway;
        public void Refresh(Map map, IReadOnlyList<RoomProject> projects, bool force = false)
        {
            int tick = Find.TickManager.TicksGame;
            if (!force && tick < nextTick) return;
            nextTick = tick + 300; Services.Clear();
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed && !p.Dead).ToList();
            bool Accessible(IntVec3 cell,PathEndMode mode=PathEndMode.Touch) => cell.InBounds(map) && !cell.Fogged(map) &&
                pawns.Any(p => cell.IsInAllowedArea(p) && p.CanReach(cell, mode, Danger.None));
            var buildings = map.listerBuildings.allBuildingsColonist.Where(b =>
                (b is Building_Bed || b is Building_WorkTable || b is Building_ResearchBench || b is Building_Storage ||
                 b.def.building?.joyKind!=null || b.def.building?.isSittable==true || b.def.surfaceType==SurfaceType.Eat || b.def.defName=="Cooler" || b.TryGetComp<CompPowerPlant>()!=null || b.TryGetComp<CompPowerBattery>()!=null) &&
                b.Spawned && !b.IsForbidden(Faction.OfPlayer) && !b.IsBurning() &&
                (!b.def.hasInteractionCell || b.InteractionCell.Standable(map)) &&
                Accessible(b.def.hasInteractionCell ? b.InteractionCell : b.Position,b.def.hasInteractionCell?PathEndMode.OnCell:PathEndMode.Touch)).ToList();
            // Never count this planner's own unfinished template as a manual substitute.
            var owned = new HashSet<Building>();
            foreach (var task in projects.SelectMany(p => p.Shell.Concat(p.Furniture)).Where(t => t.Owned && t.Def is ThingDef && t.Position.InBounds(map)))
                foreach (var b in task.Position.GetThingList(map).OfType<Building>().Where(b => b.def == task.Def && b.Position == task.Position && b.Rotation==task.Rotation)) owned.Add(b);
            bool Manual(Building b) => !owned.Contains(b);
            void Add(string kind, Building b, int count = 1, string detail = null)
            {
                if (!Manual(b)) return;
                var room = b.GetRoom();
                if (Services.Any(s => s.Kind == kind && (room != null ? s.Room == room : s.Anchor == b.Position))) return;
                Services.Add(new Service { Kind = kind, Anchor = b.Position, Room = room, Capacity = count, Detail = detail ?? b.LabelCap.ToString() });
            }
            var housing=map.GetComponent<AutonomousRim.Core.AutonomousRimMapComponent>().Housing;
            bool shelterReady=housing.Count(map)>=map.mapPawns.FreeColonistsSpawnedCount;
            foreach (var b in buildings)
            {
                var room = b.GetRoom();
                if(b is Building_Bed existingBed && shelterReady && housing.Beds.Contains(existingBed))
                    Add("Quarto",b,detail:"Abrigo suficiente para a população atual; quartos manuais preservados.");
                if (b.TryGetComp<CompPowerPlant>() != null && b.TryGetComp<CompPowerTrader>()?.PowerNet != null)
                    Add("Energia e climatização", b, detail: "Geração conectada; combustível, rede e capacidade continuam sendo monitorados.");
                if (b.def.building?.joyKind != null) Add("Recreação inicial", b);
                if (!Indoors(room)) continue;
                var battery=b.TryGetComp<CompPowerBattery>();
                if(battery?.PowerNet!=null && buildings.Any(source=>source.TryGetComp<CompPowerPlant>()!=null && source.TryGetComp<CompPowerTrader>()?.PowerNet==battery.PowerNet))
                    Add("Energia e climatização",b,detail:"Banco de baterias conectado à geração; configuração manual preservada.");
                if (b is Building_ResearchBench) Add("Pesquisa", b);
                if (b is Building_Bed bed && bed.Medical && bed.ForOwnerType==BedOwnerType.Colonist && bed.def.building.bed_humanlike)
                    Add("Hospital", b, buildings.OfType<Building_Bed>().Count(x => x.Medical && x.ForOwnerType==BedOwnerType.Colonist && x.GetRoom() == room),
                        "Camas médicas reconhecidas; capacidade mínima alvo: " + Math.Max(1,(map.mapPawns.FreeColonistsSpawnedCount+3)/4) + ".");
                if (b is Building_WorkTable table)
                {
                    var recipes = table.def.AllRecipes;
                    if (recipes.Any(r => r.products?.Any(p => p.thingDef.ingestible?.HumanEdible == true && p.thingDef.ingestible.IsMeal) == true)) Add("Cozinha", b);
                    if (recipes.Any(r => r.specialProducts?.Any(s => s.ToString() == "Butchery") == true)) Add("Abate", b);
                    if (recipes.Any(r => r.products?.Any(p => p.thingDef.IsApparel || p.thingDef.IsWeapon || p.thingDef == ThingDefOf.BlocksGranite) == true)) Add("Oficina", b);
                    if (table.def.defName == "FabricationBench") Add("Fabricação", b);
                }
                if (b.def.building?.joyKind != null && buildings.Any(x => x.GetRoom() == room && x.def.surfaceType == SurfaceType.Eat &&
                    buildings.Any(seat => seat.GetRoom() == room && seat.def.building?.isSittable == true && seat.OccupiedRect().Cells.Any(c => x.OccupiedRect().ExpandedBy(1).Contains(c)))))
                    Add("Refeitório e recreação", b);
                if(b.def.building?.joyKind!=null)Add("Sala social",b);
                if(b.def.surfaceType==SurfaceType.Eat && buildings.Any(seat=>seat.GetRoom()==room && seat.def.building?.isSittable==true &&
                    seat.OccupiedRect().Cells.Any(c=>b.OccupiedRect().ExpandedBy(1).Contains(c))))Add("Refeitório",b);
            }
            var ownedZones = new HashSet<Zone_Stockpile>(projects.Where(p => p.Stockpile != null).Select(p => p.Stockpile));
            var stores = new List<Tuple<Room, IntVec3, StorageSettings, int>>();
            foreach (var shelf in buildings.OfType<Building_Storage>().Where(Manual))
                stores.Add(Tuple.Create(shelf.GetRoom(), shelf.Position, shelf.GetStoreSettings(), shelf.OccupiedRect().Count()));
            foreach (var zone in map.zoneManager.AllZones.OfType<Zone_Stockpile>().Where(z => !ownedZones.Contains(z)))
                foreach (var group in zone.Cells.Where(c=>Accessible(c)).GroupBy(c => c.GetRoom(map)))
                    stores.Add(Tuple.Create(group.Key, group.First(), zone.GetStoreSettings(), group.Count()));
            foreach (var group in stores.Where(s => Indoors(s.Item1)).GroupBy(s => s.Item1))
            {
                var room = group.Key;
                void Storage(string kind, List<Tuple<Room, IntVec3, StorageSettings, int>> matching, int minimum)
                {
                    int capacity = matching.Sum(s => s.Item4);
                    if (capacity < minimum) return;
                    Services.Add(new Service { Kind = kind, Anchor = matching[0].Item2, Room = room, Capacity = capacity,
                        Detail = capacity + " células de armazenamento; " + (kind=="Freezer"?"temperatura atual "+room.Temperature.ToString("0.0")+" °C; ":"") + "filtros e prioridades do jogador preservados." });
                }
                Storage("Estoque", group.Where(s => s.Item3.filter.Allows(ThingDefOf.WoodLog) && s.Item3.filter.Allows(ThingDefOf.Steel)).ToList(), 4);
                if(buildings.OfType<Building_Bed>().Any(b=>b.Medical && b.ForOwnerType==BedOwnerType.Colonist && b.GetRoom()==room))
                    Storage("Medicamentos", group.Where(s => s.Item3.filter.Allows(ThingDefOf.MedicineHerbal)).ToList(), 2);
                bool cooler = buildings.Any(b => b.def.defName == "Cooler" && (b.Position - b.Rotation.FacingCell).GetRoom(map) == room &&
                    b.TryGetComp<CompTempControl>()?.TargetTemperature <= 0);
                if (cooler) Storage("Freezer", group.Where(s => s.Item3.filter.AllowedThingDefs.Any(d => d.ingestible?.HumanEdible == true)).ToList(), 4);
            }
        }

        public void Reconcile(Map map, List<RoomProject> projects)
        {
            Refresh(map, projects);
            var used = new HashSet<Service>();
            // Revalidate serialized bindings first; a missing facility cannot satisfy a goal forever.
            foreach (var p in projects.Where(p => p.ExistingFacility).ToList())
            {
                var match = Services.FirstOrDefault(s => s.Kind == p.ExistingFacilityKind && s.Anchor == p.ExistingFacilityAnchor);
                if (match != null) { used.Add(match); continue; }
                p.ExistingFacility = false; p.Completed = false; p.State = ConstructionState.Planned;
                if (p.ExistingFacilityProxy) projects.Remove(p);
            }
            foreach (var service in Services)
            {
                var same = projects.FirstOrDefault(p => p.Kind==service.Kind && p.ExistingFacility && p.ExistingFacilityKind == service.Kind && p.ExistingFacilityAnchor == service.Anchor);
                if (same == null && !used.Contains(service))
                {
                    // A separate manual service can replace a queued plan; own physical buildings are retained.
                    same = projects.FirstOrDefault(p => p.Kind == service.Kind && !p.ExistingFacility && !p.Completed);
                    if (same == null && projects.Any(p => p.Kind == service.Kind && !p.ExistingFacility)) continue;
                    if (same == null)
                    {
                        var cells = service.Room != null && Indoors(service.Room) ? service.Room.Cells.ToList() : new List<IntVec3> { service.Anchor };
                        int minX = cells.Min(c => c.x), minZ = cells.Min(c => c.z);
                        same = new RoomProject { Kind = service.Kind, ReferenceLayout = "existing-service", LayoutSlot = "existing:" + service.Kind + ":" + service.Anchor,
                            Origin = new IntVec3(minX - 1, 0, minZ - 1), InteriorSize = cells.Max(c => c.x) - minX + 1,
                            InteriorHeight = cells.Max(c => c.z) - minZ + 1, RequiresRoof = false, ExistingFacilityProxy = true };
                        projects.Add(same);
                    }
                    Bind(same, service);
                    foreach(var redundant in projects.Where(p=>p!=same && p.Kind==service.Kind && !p.ExistingFacility && !p.Completed))Bind(redundant,service);
                    foreach(var parent in projects.Where(p=>p.Kind==service.Kind && p.ExistingFacility && p.ExistingFacilityAnchor==service.Anchor).ToList())
                        foreach (var child in projects.Where(p => p.LayoutSlot == "prep:" + parent.LayoutSlot || p.LayoutSlot == "entry:" + parent.LayoutSlot ||
                            p.LayoutSlot == "interior:" + parent.LayoutSlot || p.LayoutSlot == "heat:" + parent.LayoutSlot)) Bind(child, service);
                }
                if (same != null) { same.Completed = true; same.State = ConstructionState.Completed; same.Stalled = false; same.BlockReason = "Instalação existente em " + service.Anchor + ": " + service.Detail; }
            }
            // Withdraw only this AI's unbuilt, unshared blueprints. Never destroy frames,
            // finished furniture, player blueprints, filters, roof areas or thermostats.
            foreach (var p in projects.Where(p => p.ExistingFacility))
            foreach (var t in p.Shell.Concat(p.Furniture).Where(t => t.Owned && t.Pending is Blueprint_Build && t.Pending.Spawned))
                if (!projects.Any(other => !other.ExistingFacility && other.Shell.Concat(other.Furniture).Contains(t)))
                { t.Pending.Destroy(DestroyMode.Vanish); t.Pending = null; t.Issued = false; t.Owned = false; }
        }
        private static void Bind(RoomProject p, Service s)
        {
            p.ExistingFacility = true; p.ExistingFacilityKind = s.Kind; p.ExistingFacilityAnchor = s.Anchor;
            p.Completed = true; p.State = ConstructionState.Completed;p.Stalled=false;
            if(p.Kind=="Quarto")p.HousingRecognized=true;
            p.BlockReason="Instalação existente em "+s.Anchor+": "+s.Detail;
        }
    }
}
