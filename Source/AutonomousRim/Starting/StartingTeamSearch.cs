using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
namespace AutonomousRim.Starting
{
    public sealed class StartingTeamSearch
    {
        public const int MaximumSeconds=300;
        private readonly Page_ConfigureStartingPawns page;private readonly GameInitData data;
        private readonly Stopwatch clock=new Stopwatch();private double nextRoll;
        private readonly Func<double> time;
        private static readonly System.Reflection.PropertyInfo NativeReport=AccessTools.Property(typeof(Page_ConfigureStartingPawns),"ExtraCanDoNextReport");
        public bool Running {get;private set;} public int Rolls {get;private set;} public int Improvements {get;private set;}
        public float InitialScore {get;private set;} public StartingTeamRating Rating {get;private set;}
        public string Status {get;private set;}="Pronto para procurar o melhor grupo encontrado em até cinco minutos.";
        public double Elapsed=>time(); public int Selected=>data.startingPawnCount; public int Available=>data.startingAndOptionalPawns.Count;
        public StartingTeamSearch(Page_ConfigureStartingPawns page,Func<double> elapsed=null)
        {this.page=page;data=Find.GameInitData;time=elapsed??(()=>clock.Elapsed.TotalSeconds);StartingTeamRating.ResetCache();Refresh();}
        public void Refresh(){Rating=StartingTeamRating.Read(data.startingAndOptionalPawns.Take(Selected));}
        public void Start()
        {
            if(data==null || Selected<1 || Available<Selected || Available>40 || TutorSystem.TutorialMode)
            {Status="Seleção indisponível: verificar cenário, número de personagens ou tutorial.";return;}
            StartingTeamRating.ResetCache();Rolls=Improvements=0;Refresh();InitialScore=Rating.Score;clock.Restart();nextRoll=0;Running=true;Status="Buscando uma equipe com funções complementares.";
            ImproveAvailable();Refresh();
        }
        public void Stop(string reason="Busca encerrada; melhor equipe encontrada mantida.")
        {Running=false;clock.Stop();Status=reason;Refresh();if(!NativeValid())Status+=" Requisitos do cenário ainda não atendidos.";}
        public bool NativeValid()=>Find.GameInitData==data && data!=null && ((AcceptanceReport)NativeReport.GetValue(page)).Accepted && StartingPawnUtility.WorkTypeRequirementsSatisfied();
        private void Swap(int a,int b)
        {
            var list=data.startingAndOptionalPawns;var pawn=list[a];list[a]=list[b];list[b]=pawn;
            var first=StartingPawnUtility.GetGenerationRequest(a);var second=StartingPawnUtility.GetGenerationRequest(b);
            StartingPawnUtility.SetGenerationRequest(a,second);StartingPawnUtility.SetGenerationRequest(b,first);
        }
        public void ImproveAvailable()
        {
            // Bounded local search across every available character, with native scenario validation.
            for(int pass=0;pass<Selected;pass++)
            {
                Refresh();float best=Rating.Score;bool valid=NativeValid();int first=-1,second=-1;
                for(int a=0;a<Selected;a++)for(int b=Selected;b<Available;b++)
                {
                    Swap(a,b);
                    try{if(NativeValid()){float score=StartingTeamRating.Read(data.startingAndOptionalPawns.Take(Selected)).Score;if(!valid || score>best+0.01f){best=score;valid=true;first=a;second=b;}}}
                    finally{Swap(a,b);}
                }
                if(first<0)break;Swap(first,second);Improvements++;
            }
        }
        public void Step()
        {
            if(!Running)return;
            if(Find.GameInitData!=data){Stop("Tela/cenário mudou; busca interrompida.");return;}
            if(Elapsed>=MaximumSeconds || Rolls>=5000){Stop();return;}
            if(Elapsed<nextRoll)return;nextRoll=Elapsed+0.05;
            int index=Rolls%Available;var list=data.startingAndOptionalPawns;Pawn original=list[index],candidate=null;
            try
            {
                Refresh();float previous=Rating.Score;bool previousValid=NativeValid();
                candidate=StartingPawnUtility.NewGeneratedStartingPawn(index);Rolls++;
                if(candidate==null || candidate.Dead || candidate.Downed)throw new InvalidOperationException("Gerador retornou personagem indisponível.");
                bool keep=false;
                if(index<Selected)
                {
                    list[index]=candidate;
                    var trial=StartingTeamRating.Read(list.Take(Selected));
                    keep=NativeValid() && (!previousValid || trial.Score>previous+0.01f);
                    if(!keep)list[index]=original;
                }
                else
                {
                    list[index]=candidate;
                    // Give a new reserve candidate a chance to replace any compatible starter.
                    float best=previous;int chosen=-1;
                    for(int slot=0;slot<Selected;slot++)
                    {
                        Swap(slot,index);
                        try{if(NativeValid()){float score=StartingTeamRating.Read(list.Take(Selected)).Score;if(!previousValid || score>best+0.01f){chosen=slot;best=score;previousValid=true;}}}
                        finally{Swap(slot,index);}
                    }
                    if(chosen>=0){Swap(chosen,index);keep=true;}
                    else {keep=StartingTeamRating.Individual(candidate)>StartingTeamRating.Individual(original)+0.01f;if(!keep)list[index]=original;}
                }
                if(keep){Discard(original);if(StartingTeamRating.Read(list.Take(Selected)).Score>previous+0.01f)Improvements++;}
                else Discard(candidate);
                candidate=null;Refresh();
                Status="Roletados "+Rolls+" • melhorias "+Improvements+" • "+Math.Max(0,MaximumSeconds-(int)Elapsed)+" s restantes.";
                if(Elapsed>=MaximumSeconds)Stop();
            }
            catch(Exception error)
            {
                if(candidate!=null && !list.Contains(candidate))Discard(candidate);
                if(candidate!=null && list[index]==candidate){list[index]=original;Discard(candidate);}
                Stop("Busca interrompida: "+error.Message);Log.Warning("[AutonomousRim.Starting] "+error.Message);
            }
        }
        public static void Discard(Pawn pawn)
        {
            if(pawn==null || pawn.Spawned || Find.GameInitData.startingAndOptionalPawns.Contains(pawn))return;
            PawnUtility.TryDestroyStartingColonistFamily(pawn);pawn.relations.ClearAllRelations();
            PawnComponentsUtility.RemoveComponentsOnDespawned(pawn);Find.GameInitData.startingPossessions.Remove(pawn);
            Find.WorldPawns.PassToWorld(pawn,PawnDiscardDecideMode.Discard);
        }
    }
}
