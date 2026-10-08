using System;
using System.Linq;
using System.Collections.Generic;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    // Observe the user's unmodified save. No spawned resources, skill changes,
    // healing, difficulty changes or accelerated tick multiplier.
    public sealed class SavedColonyChecks : MapComponent
    {
        private static bool loaded;
        private bool started, finished;
        private int start, last, lastCheckpointDay;
        private float pauseAttempt;
        private List<RoomProject> finishingProjects;
        public SavedColonyChecks(Map map) : base(map) { }
        public override void MapComponentUpdate()
        {
            if (GenCommandLine.CommandLineArgPassed("autonomousrimsavedtest") && !loaded && Current.ProgramState == ProgramState.Playing)
            {
                loaded = true;
                GameDataSaveLoader.LoadGame("TESTE DO MOD");
                return;
            }
            if (loaded && !started && GenCommandLine.CommandLineArgPassed("autonomousrimsavedtest")) MapComponentTick();
            if (!started || finished || !GenCommandLine.CommandLineArgPassed("autonomousrimsavedtest") || !Find.TickManager.Paused) return;
            if (UnityEngine.Time.realtimeSinceStartup - pauseAttempt < 5) return;
            pauseAttempt = UnityEngine.Time.realtimeSinceStartup;
            foreach (var window in Find.WindowStack.Windows.Where(w => w.forcePause).ToList())
            {
                if (window is Dialog_NamePlayerFactionAndSettlement naming)
                {
                    AccessTools.Method(typeof(Dialog_NamePlayerFactionAndSettlement), "Named").Invoke(naming, new object[] { AccessTools.Field(typeof(Dialog_GiveName), "curName").GetValue(naming) });
                    AccessTools.Method(typeof(Dialog_NamePlayerFactionAndSettlement), "NamedSecond").Invoke(naming, new object[] { AccessTools.Field(typeof(Dialog_GiveName), "curSecondName").GetValue(naming) });
                }
                Log.Message("[AutonomousRim.SaveTest] DIALOG: " + window.GetType().Name);
                window.Close(false);
            }
            Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
        }
        public override void MapComponentTick()
        {
            if (finished || !GenCommandLine.CommandLineArgPassed("autonomousrimsavedtest")) return;
            if (!loaded)
            {
                loaded = true;
                GameDataSaveLoader.LoadGame("TESTE DO MOD");
                return;
            }
            if (map.mapPawns.FreeColonistsSpawnedCount == 0)
            {
                if (started)
                {
                    finished = true;
                    GameDataSaveLoader.SaveGame("AutonomousRim-Teste-Falha");
                    Log.Error("[AutonomousRim.SaveTest] FAIL: no surviving colonists.");
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                }
                return;
            }
            var c = map.GetComponent<AutonomousRimMapComponent>();
            int tick = Find.TickManager.TicksGame;
            if (!started)
            {
                if (GenCommandLine.CommandLineArgPassed("autonomousrimdiagnose")) ExpansionRevisionChecks.Run(map);
                started = true; start = tick; last = tick - 6000;
                lastCheckpointDay = tick / 60000;
                c.SetAutomation(true, true); c.SetScheduleAutomation(true);
                c.SetLootAutomation(true); c.SetBaseAutomation(true);
                c.SetEquipmentAutomation(true); c.SetStrategyAutomation(true);
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                finishingProjects = c.BaseProjects.ToList();
                Log.Message("[AutonomousRim.SaveTest] START tick=" + tick + "; difficulty=" + Find.Storyteller.difficultyDef.defName);
            }
            // Persist native colony state daily so an interrupted process can resume
            // toward the original tick deadline without losing the trial.
            if (!GenCommandLine.CommandLineArgPassed("autonomousrimdiagnose") &&
                !GenCommandLine.CommandLineArgPassed("autonomousrimfinishbase") &&
                tick / 60000 > lastCheckpointDay)
            {
                lastCheckpointDay = tick / 60000;
                GameDataSaveLoader.SaveGame("AutonomousRim-Teste-Progresso");
                Log.Message("[AutonomousRim.SaveTest] CHECKPOINT tick=" + tick);
            }
            if (tick - last >= 6000)
            {
                last = tick;
                Log.Message("[AutonomousRim.SaveTest] FARM cells=" + map.zoneManager.AllZones.OfType<Zone_Growing>().Sum(z => z.Cells.Count) + "; planted=" + map.zoneManager.AllZones.OfType<Zone_Growing>().Sum(z => z.Cells.Count(cell => cell.GetPlant(map)?.def == z.GetPlantDefToGrow())) + "; priorities=" + string.Join(" | ", map.mapPawns.FreeColonistsSpawned.Select(p => p.LabelShort + "/grow=" + p.workSettings.GetPriority(WorkTypeDefOf.Growing) + "/build=" + p.workSettings.GetPriority(WorkTypeDefOf.Construction) + "/mine=" + p.workSettings.GetPriority(WorkTypeDefOf.Mining))));
                Log.Message("[AutonomousRim.SaveTest] PROGRESS day=" + ((tick-start)/60000f).ToString("F2") + "; base=" + c.BaseStatus + "; loot=" + c.LootStatus + "; gathering=" + c.GatheringStatus + "; manage=" + c.ManagementStatus + "; foodDays=" + c.CurrentState.EstimatedFoodDays + "; projects=" + c.BaseProjects.Count + "; complete=" + c.BaseProjects.Count(p => p.Completed) + "; buildings=" + map.listerBuildings.allBuildingsColonist.Count + "; pawns=" + string.Join(" | ", map.mapPawns.FreeColonistsSpawned.Select(p => p.LabelShort + "/" + p.Position + "/job=" + p.CurJob?.def.defName + "/food=" + p.needs.food?.CurLevel + "/rest=" + p.needs.rest?.CurLevel + "/mood=" + p.needs.mood?.CurLevel)));
            }
            bool finishBase = GenCommandLine.CommandLineArgPassed("autonomousrimfinishbase");
            bool baseFinished = finishingProjects != null && finishingProjects.All(p => p.Completed);
            if (finishBase && (tick-start >= 180000 || tick-start >= 6000 && baseFinished) ||
                !finishBase && (tick >= 1200235 || GenCommandLine.CommandLineArgPassed("autonomousrimdiagnose") && tick-start >= 6000))
            {
                finished = true;
                GameDataSaveLoader.SaveGame(finishBase ? "AutonomousRim-Teste-Acabamento" : GenCommandLine.CommandLineArgPassed("autonomousrimdiagnose") ? "AutonomousRim-Diagnostico" : "AutonomousRim-Teste-20dias");
                if (finishBase && !baseFinished) Log.Error("[AutonomousRim.SaveTest] FAIL: finishing projects remain incomplete after three days.");
                Log.Message("[AutonomousRim.SaveTest] FINISHED elapsedTicks=" + (tick-start) + "; surviving=" + map.mapPawns.FreeColonistsSpawnedCount);
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            }
        }
    }
}
