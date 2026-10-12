using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    // Test-only isolation, never installed with the production mod. Storyteller and
    // scenario factors remain intact; explicit safety fixtures still spawn real fires.
    [StaticConstructorOnStartup]
    public static class CommercePeacefulFixture
    {
        static CommercePeacefulFixture()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimcommercepeaceful"))return;
            new Harmony("autonomousrim.runtimechecks.commercepeaceful").Patch(
                AccessTools.Method(typeof(Storyteller),nameof(Storyteller.TryFire)),
                prefix:new HarmonyMethod(typeof(CommercePeacefulFixture),nameof(Prefix)));
        }
        public static bool Prefix(FiringIncident fi,ref bool __result)
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimcommercepeaceful") ||
                (fi.def.category!=IncidentCategoryDefOf.ThreatBig && fi.def.category!=IncidentCategoryDefOf.ThreatSmall))return true;
            __result=false;return false;
        }
    }
}
