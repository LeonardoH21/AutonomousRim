using System.Linq;
using AutonomousRim.Starting;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace AutonomousRim.UI
{
    public sealed class Dialog_StartingTeam : Window
    {
        private readonly StartingTeamSearch search;public override Vector2 InitialSize=>new Vector2(660,600);
        public Dialog_StartingTeam(Page_ConfigureStartingPawns page)
        {search=new StartingTeamSearch(page);doCloseX=true;absorbInputAroundWindow=true;closeOnClickedOutside=false;}
        public override void WindowUpdate(){base.WindowUpdate();search.Step();}
        public override void PostClose(){search.Stop();base.PostClose();}
        public override void DoWindowContents(Rect rect)
        {
            Text.Font=GameFont.Medium;Widgets.Label(new Rect(0,0,rect.width,36),"AutonomousRim • Escolher equipe");Text.Font=GameFont.Small;
            float y=42;Widgets.Label(new Rect(0,y,rect.width,45),search.Selected+" colonos para começar • "+search.Available+" personagens disponíveis. Limite: 5 minutos.");y+=48;
            Widgets.Label(new Rect(0,y,rect.width,48),"Procura por funções complementares, paixões, saúde e capacidade de combate. Mantém o melhor grupo encontrado, com as regras do cenário.");y+=52;
            Widgets.Label(new Rect(0,y,rect.width,42),search.Status);y+=46;
            Widgets.Label(new Rect(0,y,rect.width,26),"Nota do grupo: "+search.Rating.Score.ToString("0.0")+(search.Rolls>0?" • inicial "+search.InitialScore.ToString("0.0"):""));y+=30;
            foreach(var detail in search.Rating.Details.Take(9)){float h=Mathf.Max(24,Text.CalcHeight(detail,rect.width));Widgets.Label(new Rect(0,y,rect.width,h),detail);y+=h+3;}
            if(Widgets.ButtonText(new Rect(0,rect.height-42,240,36),search.Running?"Parar e manter equipe":"Buscar equipe por até 5 min")) {if(search.Running)search.Stop();else search.Start();}
            if(Widgets.ButtonText(new Rect(rect.width-150,rect.height-42,150,36),"Voltar"))Close();
        }
    }
    [HarmonyPatch(typeof(Page_ConfigureStartingPawns),"DrawPawnList")]
    public static class StartingTeamMenu
    {
        public static void Prefix(ref Rect rect,out Rect __state){__state=rect;rect.height-=36;}
        public static void Postfix(Page_ConfigureStartingPawns __instance,Rect __state)
        {
            if(Widgets.ButtonText(new Rect(__state.x,__state.yMax-32,__state.width,30),"Escolher com IA"))Find.WindowStack.Add(new Dialog_StartingTeam(__instance));
        }
    }
}
