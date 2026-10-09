using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutonomousRim.Core;
using UnityEngine;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    // Does not click any control. Computer Use must operate the real HUD;
    // this observer verifies resulting native state and the saved toggles.
    public sealed class HudInteractionChecks : MapComponent
    {
        private bool started, saved, done;
        private readonly HashSet<string> enabled=new HashSet<string>(), disabled=new HashSet<string>();
        private readonly Dictionary<string,bool> previous=new Dictionary<string,bool>();
        private static readonly Dictionary<string,Func<AutonomousRimMapComponent,bool>> Controls=
            new Dictionary<string,Func<AutonomousRimMapComponent,bool>>
            {
                {"base",a=>a.BaseAutomation},{"equipment",a=>a.EquipmentAutomation},
                {"food",a=>a.FoodAutomation},{"work",a=>a.WorkAutomation},
                {"allow",a=>a.LootAutomation},{"strategy",a=>a.StrategyAutomation},
                {"schedule",a=>a.ScheduleAutomation},{"combat",a=>a.CombatAutomation},
                {"emergency",a=>a.EmergencyAutomation},{"commerce",a=>a.CommerceAutomation},
                {"prison",a=>a.PrisonAutomation},{"expeditions",a=>a.Commerce.ExpeditionsEnabled}
            };
        public HudInteractionChecks(Map map):base(map){}
        public override void MapComponentUpdate()
        {
            if(done || !GenCommandLine.CommandLineArgPassed("autonomousrimhudtest") || Current.ProgramState!=ProgramState.Playing)return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(!started)
                {
                    ai.DisableAll();ai.Commerce.ExpeditionsEnabled=false;ai.EvaluateStrategy();
                    foreach(var control in Controls)previous[control.Key]=control.Value(ai);
                    if(previous.Values.Any(v=>v))throw new InvalidOperationException("HUD fixture did not start with all controls off.");
                    started=true;Log.Message("[HudAudit] READY: click each real control on/off; paused isolated colony; no observer-generated clicks.");
                }
                Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
                foreach(var control in Controls)
                {
                    bool value=control.Value(ai);
                    if(previous[control.Key]==value)continue;
                    previous[control.Key]=value;
                    if(value)enabled.Add(control.Key);else if(enabled.Contains(control.Key))disabled.Add(control.Key);
                    Log.Message($"[HudAudit] CHANGE {control.Key}={value}; on={enabled.Count}/12; off={disabled.Count}/12");
                }
                if(!saved && enabled.Count==Controls.Count && disabled.Count==Controls.Count && previous.Values.All(v=>!v))
                {
                    GameDataSaveLoader.SaveGame("HudAuditComplete");
                    ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath,"HudAuditComplete.png"));
                    saved=true;
                }
                if(saved && File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath,"HudAuditComplete.png")))
                {done=true;Log.Message("[HudAudit] DONE: twelve control states enabled then disabled, all off in native save; screenshot preserved. Coupled state changes are logged; visual audit must confirm actual clicks.");}
            }
            catch(Exception ex){done=true;Log.Error("[HudAudit] FAIL: "+ex);}
        }
    }
}
