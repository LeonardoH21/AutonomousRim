using System.Linq;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class StagedTestSpeed : MapComponent
    {
        public StagedTestSpeed(Map map):base(map){}
        public override void MapComponentUpdate()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimstagedtest") || GenCommandLine.CommandLineArgPassed("autonomousrimhudtest"))return;
            foreach(var window in Find.WindowStack.Windows.Where(w=>w.forcePause).ToList())window.Close(false);
            Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
        }
    }
}
