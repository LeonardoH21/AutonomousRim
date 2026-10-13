using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Starting;
using AutonomousRim.UI;
using HarmonyLib;
using RimWorld;
using Verse;
namespace AutonomousRim.RuntimeChecks
{
    public sealed class StartingTeamChecks : MapComponent
    {
        private bool done;public StartingTeamChecks(Map map):base(map){}
        private void Check(bool ok,string text){if(!ok)throw new Exception(text);Log.Message("[StartingTeam] PASS: "+text);}
        public override void MapComponentTick()
        {
            if(Find.TickManager.TicksGame<300 || done || !EliteFixtureBaseline.Ready || !GenCommandLine.CommandLineArgPassed("autonomousrimstartingteamtest"))return;
            done=true;var previous=Current.Game.InitData;
            var requestField=AccessTools.Field(typeof(StartingPawnUtility),"StartingAndOptionalPawnGenerationRequests");var oldRequests=requestField.GetValue(null);
            try
            {
                var originals=EliteFixtureBaseline.Roster(map).Select(p=>p.thingIDNumber).ToArray();
                var data=new GameInitData{startingTile=map.Tile,startingPawnCount=3,playerFaction=Faction.OfPlayer,startingPawnKind=PawnKindDefOf.Colonist,allowedDevelopmentalStages=DevelopmentalStage.Adult};
                Current.Game.InitData=data;requestField.SetValue(null,new List<PawnGenerationRequest>());
                for(int i=0;i<8;i++)data.startingAndOptionalPawns.Add(StartingPawnUtility.NewGeneratedStartingPawn(i));
                var page=new Page_ConfigureStartingPawns();page.PreOpen();double now=0;
                var search=new StartingTeamSearch(page,()=>now);
                Check(search.Selected==3 && search.Available==8,"native menu counts distinguish selected colonists and available reserves");
                search.Start();float firstInitial=search.InitialScore;Check(search.Running && search.NativeValid(),"search starts with native scenario constraints valid");
                float old=search.Rating.Score;int possessions=data.startingPossessions.Count;
                for(int i=0;i<64;i++)
                {
                    now+=0.1;search.Step();
                    Check(search.Rating.Score>=old-0.01f,"valid team score does not decrease at roll "+search.Rolls);old=search.Rating.Score;
                    if(!search.Running)throw new Exception(search.Status);
                }
                Check(search.Rolls==64,"all menu slots rotate through 64 genuine native generations");
                Check(data.startingAndOptionalPawns.Count==8 && data.startingAndOptionalPawns.Distinct().Count()==8,"selection preserves exact roster count and unique pawns");
                Check(data.startingPossessions.Count==possessions && data.startingAndOptionalPawns.All(p=>data.startingPossessions.ContainsKey(p)),"discarded candidates leave no possessions entries; accepted pawns keep native possessions");
                Check(search.NativeValid(),"native page allows final group after optional swaps and rerolls");
                data.startingPawnsRequired=new List<PawnKindCount>{new PawnKindCount{kindDef=PawnKindDefOf.Colonist,count=4}};
                Check(!search.NativeValid(),"scenario required pawn counts are respected");data.startingPawnsRequired=null;
                data.allowedDevelopmentalStages=DevelopmentalStage.Baby;Check(!search.NativeValid(),"scenario developmental stages are respected");data.allowedDevelopmentalStages=DevelopmentalStage.Adult;
                search.Stop();int rolls=search.Rolls;now+=1;search.Step();Check(search.Rolls==rolls,"manual stop keeps selected group without further rerolls");
                search.Start();now=StartingTeamSearch.MaximumSeconds;search.Step();Check(!search.Running && search.Rolls==0,"five-minute deadline stops before another native generation");
                var patches=Harmony.GetPatchInfo(AccessTools.Method(typeof(Page_ConfigureStartingPawns),"DrawPawnList"));
                Check(patches?.Prefixes.Any(p=>p.owner==AutonomousRim.Core.AutonomousRimMod.HarmonyId && p.PatchMethod.DeclaringType==typeof(StartingTeamMenu))==true &&
                    patches.Postfixes.Any(p=>p.owner==AutonomousRim.Core.AutonomousRimMod.HarmonyId && p.PatchMethod.DeclaringType==typeof(StartingTeamMenu)),"installed native menu has both selection button hooks");
                var rolePawn=data.startingAndOptionalPawns[0];
                Check(StartingTeamRating.Read(new[]{rolePawn}).Details.Any(d=>d.Contains("Medicina")),"score explains essential roles and backups");
                search.Start();Current.Game.InitData=previous;search.Step();Check(!search.Running,"leaving the character context safely stops the search");
                Check(EliteFixtureBaseline.Roster(map).Select(p=>p.thingIDNumber).SequenceEqual(originals),"existing six colony characters are untouched by menu test");
                Current.Game.InitData=data;StartingPawnUtility.ClearAllStartingPawns();
                Check(data.startingPossessions.Count==0 && data.startingAndOptionalPawns.Count==0,"native cleanup removes all temporary starting pawns");
                Log.Message("[StartingTeam] RESULT initial="+firstInitial+" best="+old+" generated=64");Log.Message("[StartingTeam] DONE");
            }
            catch(Exception e){Log.Error("[StartingTeam] FAIL: "+e);}
            finally{Current.Game.InitData=previous;requestField.SetValue(null,oldRequests);}
        }
    }
}
