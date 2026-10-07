using System;
using System.IO;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    // Normal game observer. Only the permitted initial pawn count/skills differ.
    public sealed class ModularConstructionTrial : MapComponent
    {
        private bool started, finished;
        private int startTick;
        private float lastResume;
        private readonly System.Collections.Generic.List<Pawn> initialPawns = new System.Collections.Generic.List<Pawn>();
        private readonly System.Collections.Generic.HashSet<string> milestones = new System.Collections.Generic.HashSet<string>();
        private bool Enabled => GenCommandLine.CommandLineArgPassed("autonomousrimmodulartrial");
        public ModularConstructionTrial(Map map) : base(map) { }
        public override void FinalizeInit()
        {
            if (Enabled) map.GetComponent<AutonomousRimMapComponent>().SetBaseAutomation(false);
        }
        public override void MapComponentUpdate()
        {
            if (!Enabled || !started || finished || UnityEngine.Time.realtimeSinceStartup - lastResume < 5) return;
            lastResume = UnityEngine.Time.realtimeSinceStartup;
            foreach (var w in Find.WindowStack.Windows.Where(w => w.forcePause).ToList())
            {
                if (w is Dialog_NamePlayerFactionAndSettlement naming)
                {
                    AccessTools.Method(typeof(Dialog_NamePlayerFactionAndSettlement), "Named").Invoke(naming, new[] { AccessTools.Field(typeof(Dialog_GiveName), "curName").GetValue(naming) });
                    AccessTools.Method(typeof(Dialog_NamePlayerFactionAndSettlement), "NamedSecond").Invoke(naming, new[] { AccessTools.Field(typeof(Dialog_GiveName), "curSecondName").GetValue(naming) });
                    naming.Close(false);
                }
            }
            if (!Find.WindowStack.Windows.Any(w => w.forcePause)) Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
        }
        private void Milestone(string name, bool reached)
        {
            if (reached && milestones.Add(name)) Log.Message($"[AutonomousRim.ModularTrial] MILESTONE {name}: {Find.TickManager.TicksGame - startTick} ticks; {(Find.TickManager.TicksGame - startTick) / 2500f:F2} hours.");
        }
        public override void MapComponentTick()
        {
            if (!Enabled || finished || Find.TickManager.TicksGame % 30 != 0) return;
            var ai = map.GetComponent<AutonomousRimMapComponent>();
            int tick = Find.TickManager.TicksGame;
            try
            {
                if (!started)
                {
                    if (tick < 120 || map.mapPawns.FreeColonistsSpawnedCount == 0) return;
                    while (map.mapPawns.FreeColonistsSpawnedCount < 5)
                    {
                        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                        GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(map.mapPawns.FreeColonistsSpawned.First().Position, map, 6), map);
                    }
                    foreach (var p in map.mapPawns.FreeColonistsSpawned)
                        foreach (var s in p.skills.skills) { s.Level = 20; s.xpSinceLastLevel = 0; }
                    ai.SetAutomation(true, true); ai.SetLootAutomation(true);
                    ai.SetScheduleAutomation(true); ai.SetStrategyAutomation(true); ai.SetEquipmentAutomation(true);
                    ai.SetBaseAutomation(true);
                    if (!ai.BaseProjects.Any(p => p.Kind == ModularBasePlanner.ReservationKind)) throw new Exception("No modular site: " + ai.BaseStatus);
                    foreach (var p in ai.BaseProjects.Where(ModularBasePlanner.IsModular))
                    {
                        if (p.InteriorSize != 5 && p.InteriorSize != 11 || p.Height != 5 && p.Height != 11) throw new Exception("Wrong module interior.");
                        if (p.Shell.Any(t => p.Interior.Contains(t.Position))) throw new Exception("Partition inside merged room.");
                    }
                    if (!RingBasePlanner.Validate(map, ai.BaseProjects.ToList())) throw new Exception("Geometry/interaction/terrain invalid.");
                    started = true; startTick = tick;
                    initialPawns.AddRange(map.mapPawns.FreeColonistsSpawned);
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                    GameDataSaveLoader.SaveGame("ModularStart");
                    Log.Message("[AutonomousRim.ModularTrial] START: five skill-20 colonists; native resources, needs, research, costs and work; speed 3x.");
                }
                var tasks = ai.BaseProjects.SelectMany(BaseConstructionManager.Tasks).GroupBy(t => new { t.Position, t.Def, t.Rotation }).Select(g => g.First()).ToList();
                if (initialPawns.Any(p => p.Dead)) throw new Exception("Starting colonist died; native trial cannot be accepted.");
                int walls = tasks.Count(t => t.Def == ThingDefOf.Wall && t.Complete(map));
                Milestone("first-order", tasks.Any(t => t.Issued));
                Milestone("first-delivery", tasks.Any(t => t.Pending is Frame f && f.resourceContainer.Count > 0));
                Milestone("first-wall", walls > 0); Milestone("seven-walls", walls >= 7);
                Milestone("first-bedroom", ai.BaseProjects.Any(p => p.Kind == "Quarto" && p.Completed));
                foreach (var kind in new[] { "Quarto", "Cozinha", "Abate", "Estoque", "Freezer" })
                    Milestone(kind, ai.BaseProjects.Any(p => p.Kind == kind) && ai.BaseProjects.Where(p => p.Kind == kind).All(p => p.Completed));
                foreach (var room in ai.BaseProjects.Where(ModularBasePlanner.IsModular))
                    if (room.Furniture.Any(t => t.Issued && !(t.Def is TerrainDef)) &&
                        (room.Shell.Any(t => !t.Complete(map)) || room.Furniture.Any(t => t.Def is TerrainDef && !t.Complete(map))))
                        throw new Exception("Furniture issued before shell/floors: " + room.LayoutSlot);
                if (tick - startTick > 20000 && walls < 7) throw new Exception("Fewer than seven walls after eight hours; responsiveness regression.");
                if (tick % 2490 == 0)
                {
                    Log.Message($"[AutonomousRim.ModularTrial] PROGRESS hours={(tick - startTick) / 2500f:F2}; walls={walls}; completed={tasks.Count(t => t.Complete(map))}/{tasks.Count}; {ai.BaseStatus}; {ai.GatheringStatus}; food={ai.CurrentState.EstimatedFoodDays:F1}; jobs=" +
                        string.Join(";", map.mapPawns.FreeColonistsSpawned.Select(p => p.LabelShort + ":" + p.CurJob?.def.defName + ":C" + p.workSettings.GetPriority(WorkTypeDefOf.Construction))));
                    GameDataSaveLoader.SaveGame("ModularCheckpoint");
                }
                var freezer = ai.BaseProjects.First(p => p.Kind == "Freezer");
                bool cold = freezer.Completed && freezer.Interior.CenterCell.GetTemperature(map) < 0 &&
                    freezer.Shell.Where(t => t.Def.defName == "Cooler").All(t => t.Position.GetThingList(map).OfType<ThingWithComps>().Any(b => b.def == t.Def && b.TryGetComp<CompPowerTrader>()?.PowerOn == true));
                Milestone("refrigeration", cold);
                if (cold && milestones.Contains("Freezer") && milestones.Contains("Quarto") && milestones.Contains("Cozinha") && milestones.Contains("Abate") && milestones.Contains("Estoque"))
                {
                    GameDataSaveLoader.SaveGame("ModularInitialComplete"); finished = true;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    Log.Message("[AutonomousRim.ModularTrial] PASS: initial rooms, floors, beds, kitchen, butcher, stockpile, freezer complete by native work.");
                }
                if (tick - startTick > 180000 && !finished) throw new Exception("Initial core incomplete after three days; diagnose latency/materials before accepting.");
            }
            catch (Exception e)
            {
                finished = true; Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                GameDataSaveLoader.SaveGame("ModularFailure");
                Log.Error("[AutonomousRim.ModularTrial] FAIL: " + e);
            }
        }
    }
}
