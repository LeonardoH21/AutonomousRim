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
            return names.Select(n => pawns.Single(p => ((NameTriple)p.Name).Nick == n)).ToList();
        }
    }
}
