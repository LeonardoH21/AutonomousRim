using System;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class BasicAutomationControlsChecks : MapComponent
    {
        private bool done;
        public BasicAutomationControlsChecks(Map map):base(map){}
        public override void MapComponentTick()
        {
            if(done || !GenCommandLine.CommandLineArgPassed("autonomousrimskiplegacyconstruction") || Find.TickManager.TicksGame<1100)return;
            done=true;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();ai.DisableAll();
                if(ai.BaseAutomation || ai.CommerceAutomation || ai.PrisonAutomation || ai.WorkAutomation || ai.FoodAutomation || ai.LootAutomation)
                    throw new InvalidOperationException("Disable-all left an automation enabled.");
                CommerceManager.Suspend(new CommerceState());CommerceManager.Stop(new CommerceState());
                var prison=new PrisonState();prison.Orders.Add(new PrisonOrder());PrisonManager.Stop(prison);
                if(prison.Orders.Count!=0)throw new InvalidOperationException("Invalid prisoner order retained.");
                Log.Message("[AutonomousRim.ControlsTests] PASS: disable-all, idle commerce stop/suspend and missing-helper prisoner cleanup.");
                var worker=map.mapPawns.FreeColonistsSpawned.First(WorkPriorityManager.CanWork);
                LootAccessChecks.Run(map,worker,ai);
            }
            catch(Exception ex){Log.Error("[AutonomousRim.ControlsTests] FAIL: "+ex);}
        }
    }
}
