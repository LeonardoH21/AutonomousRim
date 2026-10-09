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
        public int InitialResearch = -1;
        public System.Collections.Generic.HashSet<string> Crafted = new System.Collections.Generic.HashSet<string>();
        public System.Collections.Generic.HashSet<string> CompletedRecipes = new System.Collections.Generic.HashSet<string>();
        public System.Collections.Generic.HashSet<int> UsedBenches = new System.Collections.Generic.HashSet<int>();
        private static bool resumed;
        private int startTick;
        private float lastResume;
        private System.Collections.Generic.List<Pawn> initialPawns = new System.Collections.Generic.List<Pawn>();
        private System.Collections.Generic.HashSet<string> milestones = new System.Collections.Generic.HashSet<string>();
        private readonly System.Collections.Generic.HashSet<ConstructionTask> checkedFurniture = new System.Collections.Generic.HashSet<ConstructionTask>();
        private bool Progression => GenCommandLine.CommandLineArgPassed("autonomousrimprogressiontrial");
        private bool Integrated => GenCommandLine.CommandLineArgPassed("autonomousrimintegratedtest");
        private bool Enabled => GenCommandLine.CommandLineArgPassed("autonomousrimmodulartrial");
        public ModularConstructionTrial(Map map) : base(map) { }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref InitialResearch, "initialResearchProof", -1);
            Scribe_Values.Look(ref startTick,"nativeTrialStartTick");
            Scribe_Collections.Look(ref milestones,"nativeTrialMilestones",LookMode.Value);
            Scribe_Collections.Look(ref Crafted, "nativeCraftProof", LookMode.Value);
            Scribe_Collections.Look(ref CompletedRecipes,"nativeRecipeProof",LookMode.Value);
            Scribe_Collections.Look(ref UsedBenches, "usedBenchProof", LookMode.Value);
            Scribe_Collections.Look(ref initialPawns, "initialPawnProof", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Crafted = Crafted ?? new System.Collections.Generic.HashSet<string>();
                CompletedRecipes=CompletedRecipes??new System.Collections.Generic.HashSet<string>();
                UsedBenches = UsedBenches ?? new System.Collections.Generic.HashSet<int>();
                initialPawns = initialPawns ?? new System.Collections.Generic.List<Pawn>();
                milestones=milestones??new System.Collections.Generic.HashSet<string>();
            }
        }
        public override void FinalizeInit()
        {
            if (Enabled) map.GetComponent<AutonomousRimMapComponent>().SetBaseAutomation(false);
        }
        public override void MapComponentUpdate()
        {
            if (Enabled && !resumed && GenCommandLine.CommandLineArgPassed("autonomousrimprogressionresume") && Current.ProgramState == ProgramState.Playing)
            { resumed = true; GameDataSaveLoader.LoadGame("ModularResume"); return; }
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
            if (GenCommandLine.CommandLineArgPassed("autonomousrimprogressionresume") && !resumed) return;
            if (!Enabled || finished || Find.TickManager.TicksGame % 30 != 0) return;
            var ai = map.GetComponent<AutonomousRimMapComponent>();
            int tick = Find.TickManager.TicksGame;
            try
            {
                if (!started)
                {
                    if (tick < 120 || map.mapPawns.FreeColonistsSpawnedCount == 0) return;
                    bool continuing = GenCommandLine.CommandLineArgPassed("autonomousrimprogressionresume");
                    if (!continuing && GenCommandLine.CommandLineArgPassed("autonomousrimpeacefultrial"))
                    {
                        var preset = DefDatabase<DifficultyDef>.GetNamed("Peaceful");
                        Find.Storyteller.difficultyDef = preset; Find.Storyteller.difficulty.CopyFrom(preset);
                        Log.Message("[AutonomousRim.ModularTrial] SETUP: native Peaceful difficulty; construction/research/crafting validation, not combat validation.");
                    }
                    while (!continuing && map.mapPawns.FreeColonistsSpawnedCount < 5)
                    {
                        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                        GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(map.mapPawns.FreeColonistsSpawned.First().Position, map, 6), map);
                    }
                    if (!GenCommandLine.CommandLineArgPassed("autonomousrimprogressionresume"))
                    foreach (var p in map.mapPawns.FreeColonistsSpawned)
                        foreach (var s in p.skills.skills) { s.Level = 20; s.xpSinceLastLevel = 0; }
                    if (Progression) ExpansionRevisionChecks.Run(map);
                    ai.SetAutomation(true, true); ai.SetLootAutomation(true);
                    ai.SetScheduleAutomation(true); ai.SetStrategyAutomation(true); ai.SetEquipmentAutomation(true);
                    ai.SetBaseAutomation(true);
                    if(Integrated){ai.SetPrisonAutomation(true);ai.SetCommerceAutomation(true);ai.SetEmergencyAutomation(true);}
                    if (!ai.BaseProjects.Any(p => p.Kind == ModularBasePlanner.ReservationKind)) throw new Exception("No modular site: " + ai.BaseStatus);
                    foreach (var p in ai.BaseProjects.Where(ModularBasePlanner.IsModular))
                    {
                        if (p.InteriorSize != 5 && p.InteriorSize != 11 || p.Height != 5 && p.Height != 11) throw new Exception("Wrong module interior.");
                        if (p.Shell.Any(t => p.Interior.Contains(t.Position))) throw new Exception("Partition inside merged room.");
                    }
                    if (!RingBasePlanner.Validate(map, ai.BaseProjects.ToList()))
                    {
                        foreach(var finish in ai.BaseProjects.Where(p=>p.LayoutSlot?.StartsWith("prison:finish:")==true))
                        {
                            var room=ai.BaseProjects.FirstOrDefault(p=>p.LayoutSlot==finish.LayoutSlot.Replace("prison:finish:","prison:cell:"));
                            foreach(var lamp in finish.Furniture.Where(t=>t.Def.defName=="WallLamp"))
                                Log.Message($"[AutonomousRim.ModularTrial] GEOMETRY lamp={lamp.Position}; rotation={lamp.Rotation}; issued={lamp.Issued}; completed={lamp.WasCompleted}; cancelled={lamp.CancelledByPlayer}; parent={room?.LayoutSlot}; support={ (lamp.Position+lamp.Rotation.FacingCell).GetEdifice(map)?.def.defName}; cells="+
                                    string.Join(";",room?.Interior.Cells.Select(c=>c+":"+c.GetEdifice(map)?.def.defName+":north="+(c+IntVec3.North).GetEdifice(map)?.def.defName)??Enumerable.Empty<string>()));
                        }
                        GameDataSaveLoader.SaveGame("GeometryFailure");
                        throw new Exception("Geometry/interaction/terrain invalid.");
                    }
                    started = true; if(!continuing || startTick<=0)startTick = tick;
                    if (InitialResearch < 0) InitialResearch = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Count(r => r.IsFinished);
                    if (initialPawns.Count == 0)
                        initialPawns.AddRange(continuing ? PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Where(p => p.IsColonist && p.Faction == Faction.OfPlayer) : map.mapPawns.FreeColonistsSpawned);
                    if (initialPawns.Count != 5 || initialPawns.Any(p => p == null || p.Dead))
                        throw new Exception("Initial five colonists not alive; continuation cannot replace missing pawns.");
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
                    foreach (var furniture in room.Furniture.Where(t => t.Issued && !t.WasCompleted && t.Pending?.Spawned == true && !(t.Def is TerrainDef)))
                        if (!ai.SecondarySuspended && checkedFurniture.Add(furniture) &&
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
                if (!Progression && !Integrated && cold && milestones.Contains("Freezer") && milestones.Contains("Quarto") && milestones.Contains("Cozinha") && milestones.Contains("Abate") && milestones.Contains("Estoque"))
                {
                    GameDataSaveLoader.SaveGame("ModularInitialComplete"); finished = true;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    Log.Message("[AutonomousRim.ModularTrial] PASS: initial rooms, floors, beds, kitchen, butcher, stockpile, freezer complete by native work.");
                }
                bool progressionComplete = Progression && ResearchCraftTrialObserver.Complete(map, ai);
                if (Progression && !Integrated && cold && progressionComplete)
                {
                    GameDataSaveLoader.SaveGame("ModularSteelComplete"); finished = true;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    Log.Message("[AutonomousRim.ModularTrial] PASS: native research, organized workshop, upgraded storage, crafted steel helmet/plate/sword and equipped clothing; elapsed days=" + ((tick-startTick)/60000f));
                }
                if(Integrated)
                {
                    bool nativeMeals=proofMeals();
                    bool services=new[]{"Hospital","Pesquisa","Oficina"}.All(kind=>ai.BaseProjects.Any(p=>p.Kind==kind && p.Completed));
                    Milestone("native-cooking",nativeMeals);Milestone("secondary-services",services);Milestone("prison-built",ai.Prison.Capacity>=3);
                    if(tick-startTick>=20*GenDate.TicksPerDay && cold && progressionComplete && services && nativeMeals && ai.Prison.Capacity>=3)
                    {
                        GameDataSaveLoader.SaveGame("IntegratedTwentyDaysComplete");finished=true;Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
                        Log.Message("[AutonomousRim.ModularTrial] PASS: integrated twenty days; five original colonists alive; native core/floors/refrigeration, research/crafting/equipment, cooking, hospital, workshop and usable prison. No resources granted after native start.");
                    }
                }
                if (tick - startTick > (Progression || Integrated ? 1800000 : 180000) && !finished) throw new Exception("Native trial deadline reached without its required milestones; inspect checkpoint.");
            }
            catch (Exception e)
            {
                finished = true; Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                GameDataSaveLoader.SaveGame("ModularFailure");
                Log.Error("[AutonomousRim.ModularTrial] FAIL: " + e);
            }
        }
        private bool proofMeals()=>Crafted.Any(name=>DefDatabase<ThingDef>.GetNamedSilentFail(name)?.ingestible?.IsMeal==true);
    }
}
