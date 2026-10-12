using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AutonomousRim.UI;
using AutonomousRim.Core;
using AutonomousRim.Perception;
using AutonomousRim.Planning;
using RimWorld;
using UnityEngine;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    // Presentation audit only: a paused copy of a save, no control clicks or gameplay changes.
    public sealed class HudPresentationChecks : MapComponent
    {
        private int page;
        private float next;
        private bool started, requested, done;
        private static bool loaded;
        public HudPresentationChecks(Map map) : base(map) { }
        public override void MapComponentUpdate()
        {
            if (done || !GenCommandLine.CommandLineArgPassed("autonomousrimhudpresentation") || Current.ProgramState != ProgramState.Playing) return;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                if (!loaded) { loaded = true; GameDataSaveLoader.LoadGame("ModularResume"); return; }
                if (Time.realtimeSinceStartup < next) return;
                if (!started)
                {
                    var ai = map.GetComponent<AutonomousRimMapComponent>();
                    // Refresh read-only observation while paused; never toggle the planners.
                    typeof(AutonomousRimMapComponent).GetProperty("CurrentState").SetValue(ai, ColonyStateScanner.Scan(map), null);
                    var beforeResearch=Find.ResearchManager.GetProject();int beforeProjects=ai.BaseProjects.Count;int beforeThings=map.listerThings.AllThings.Count;
                    var report=ProgressDashboard.Read(map,ai);
                    var route=DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r=>!r.IsFinished).Take(8).ToList();
                    if(route.Count>=7)
                    {
                        var queue=ProgressDashboard.ResearchQueue(route[0],route.Concat(route));
                        if(queue.Count!=6 || queue.Distinct().Count()!=6 || queue[0]!=route[0])throw new Exception("Six-project research queue/selected-project precedence failed.");
                    }
                    if(report.Gear.Items.Count!=DefenseProductionPlan.Fighters(map).Sum(p=>LoadoutProgression.Set(p,report.Gear.Stage).Count()))throw new Exception("Per-colonist loadout slots missing.");
                    var available=report.Gear.Items.Where(r=>r.State=="Disponível para equipar").Select(r=>r.Existing).ToList();
                    if(available.Any(t=>t==null) || available.Distinct().Count()!=available.Count)throw new Exception("Equipment inventory allocated twice.");
                    if(beforeResearch!=Find.ResearchManager.GetProject() || beforeProjects!=ai.BaseProjects.Count || beforeThings!=map.listerThings.AllThings.Count)throw new Exception("Read-only dashboard changed gameplay.");
                    foreach(var material in report.Gear.Materials)if(material.Value<0)throw new Exception("Invalid gear resource estimate.");
                    Log.Message("[HudPresentation] PASS: research queue capped at six, selection first, all colonist slots, unique available gear, resources and read-only snapshot.");
                    foreach (Window w in Find.WindowStack.Windows.ToList()) Find.WindowStack.TryRemove(w, false);
                    Find.MainTabsRoot.SetCurrentTab(DefDatabase<MainButtonDef>.GetNamed("AutonomousRimInspector"), false);
                    started = true; next = Time.realtimeSinceStartup + 3; return;
                }
                var hud = Find.WindowStack.Windows.OfType<MainTabWindow_Inspector>().FirstOrDefault();
                if (hud == null) throw new Exception("HUD was not opened.");
                string file = Path.Combine(GenFilePaths.SaveDataFolderPath, "HUD-" + page + ".png");
                if (!requested)
                {
                    ScreenCapture.CaptureScreenshot(file); requested = true; next = Time.realtimeSinceStartup + 1; return;
                }
                if (!File.Exists(file)) return;
                Log.Message("[HudPresentation] CAPTURE: page=" + page + " file=" + file);
                page++;
                if (page == 10) { done = true; Log.Message("[HudPresentation] DONE: ten pages rendered; manual image inspection required; controls not clicked."); return; }
                typeof(MainTabWindow_Inspector).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, page);
                requested = false; next = Time.realtimeSinceStartup + 2;
            }
            catch (Exception e) { done = true; Log.Error("[HudPresentation] FAIL: " + e); }
        }
    }
}
