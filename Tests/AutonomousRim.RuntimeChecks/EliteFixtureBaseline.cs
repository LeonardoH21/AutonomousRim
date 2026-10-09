using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace AutonomousRim.RuntimeChecks
{
    // Disposable mechanic fixtures load the same user-approved baseline before setup.
    public sealed class EliteFixtureBaseline : MapComponent
    {
        private static bool loaded;
        private static List<string> originalIds;
        public static bool Enabled => GenCommandLine.CommandLineArgPassed("autonomousrimfixturebaseline");
        public static bool Ready => !Enabled || loaded;
        public EliteFixtureBaseline(Map map) : base(map) { }
        public override void MapComponentUpdate()
        {
            if (!Enabled || Current.ProgramState != ProgramState.Playing) return;
            if (!loaded) { loaded = true; GameDataSaveLoader.LoadGame("ModularResume"); return; }
            foreach (var window in Find.WindowStack.Windows.Where(w => w.forcePause).ToList()) window.Close(false);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
        }
        public static List<Pawn> Roster(Map map)
        {
            var names = new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot" };
            var pawns = map.mapPawns.FreeColonistsSpawned.ToList();
            if (Current.Game.Scenario.name != "AutonomousRim Enhanced World" ||
                Find.Storyteller.def.defName != "Cassandra" || Find.Storyteller.difficultyDef.defName != "Medium" ||
                pawns.Count != 6 || names.Any(n => pawns.Count(p => p.Name is NameTriple name && name.Nick == n) != 1))
                throw new InvalidOperationException("Fixture requires the original six Enhanced World profiles and Cassandra/Medium.");
            foreach (var factor in new[] { Tuple.Create("MiningYield", 5f), Tuple.Create("PlantHarvestYield", 3f), Tuple.Create("ConstructionSpeed", 2f), Tuple.Create("ResearchSpeed", 3f) })
                if (Current.Game.Scenario.GetStatFactor(DefDatabase<StatDef>.GetNamed(factor.Item1)) != factor.Item2)
                    throw new InvalidOperationException("Fixture scenario factor changed: " + factor.Item1);
            var result = names.Select(n => pawns.Single(p => ((NameTriple)p.Name).Nick == n)).ToList();
            originalIds = result.Select(p => p.GetUniqueLoadID()).ToList();
            return result;
        }
        public static void VerifyOriginalsPresent(Map map)
        {
            if (!Enabled) return;
            if (originalIds == null || originalIds.Count != 6 || originalIds.Any(id =>
                !map.mapPawns.FreeColonistsSpawned.Any(p => !p.Dead && p.GetUniqueLoadID() == id)))
                throw new InvalidOperationException("An original EliteSix colonist died, was replaced or did not return to the fixture map.");
        }
    }
}
