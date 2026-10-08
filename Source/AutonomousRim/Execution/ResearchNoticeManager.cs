using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    public static class ResearchNoticeManager
    {
        // Native completion notices force-pause the game. Close only the exact notice of an AI-owned project.
        public static void CloseOwnedCompletion(ResearchProjectDef owned)
        {
            if (owned == null || !owned.IsFinished || Find.WindowStack == null) return;
            string expected = "ResearchFinished".Translate(owned.LabelCap) + "\n\n" + owned.description;
            foreach (var window in Find.WindowStack.Windows.OfType<Dialog_NodeTree>().ToList())
                if (AccessTools.Field(typeof(Dialog_NodeTree), "curNode").GetValue(window) is DiaNode node &&
                    node.text.ToString() == expected && node.options.Count == 2 &&
                    AccessTools.Field(typeof(DiaOption), "text").GetValue(node.options[1]).ToString() == "ResearchScreen".Translate().ToString())
                {
                    window.Close(false);
                    Log.Message("[AutonomousRim.Research] Conclusão automática confirmada: " + owned.defName);
                }
        }
    }
}
