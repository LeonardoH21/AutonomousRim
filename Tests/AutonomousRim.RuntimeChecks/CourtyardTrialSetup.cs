using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    // Equivalent to selecting the starting tile, map size and pawn count in the
    // new-game screens. This changes no generated terrain, resources or research.
    [StaticConstructorOnStartup]
    public static class CourtyardTrialSetup
    {
        private static bool Fresh => GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardtrial") &&
            !GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardresume") && !GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardfromfailure") && !GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardfromstart");
        static CourtyardTrialSetup()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimcourtyardtrial"))return;
            var h=new Harmony("leonardoh21.autonomousrim.courtyardtrial.setup");
            h.Patch(AccessTools.Method(typeof(GameInitData),"ChooseRandomStartingTile"),postfix:new HarmonyMethod(typeof(CourtyardTrialSetup),nameof(Tile)));
            h.Patch(AccessTools.Method(typeof(GameInitData),"PrepForMapGen"),prefix:new HarmonyMethod(typeof(CourtyardTrialSetup),nameof(Size)));
            h.Patch(AccessTools.Method(typeof(ScenPart_ConfigPage_ConfigureStartingPawns),"GenerateStartingPawns"),prefix:new HarmonyMethod(typeof(CourtyardTrialSetup),nameof(Count)));
        }
        private static void Tile(GameInitData __instance)
        {
            if(!Fresh)return;
            __instance.startingTile=TileFinder.RandomSettlementTileFor(Faction.OfPlayer,true,t=>{
                var tile=Find.WorldGrid[t];return tile.PrimaryBiome.defName=="TemperateForest"&&tile.hilliness==Hilliness.Flat&&tile.rainfall<1000;
            });
            Log.Message("[AutonomousRim.CourtyardTrial] NEW GAME: selected flat temperate forest tile through native settlement validation.");
        }
        private static void Size(GameInitData __instance){if(Fresh)__instance.mapSize=325;}
        private static void Count(ScenPart_ConfigPage_ConfigureStartingPawns __instance)
        {if(Fresh)AccessTools.Field(typeof(ScenPart_ConfigPage_ConfigureStartingPawns),"pawnCount").SetValue(__instance,5);}
    }
}
