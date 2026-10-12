using HarmonyLib;
using Verse;

namespace AutonomousRim.Core
{
    public sealed class AutonomousRimMod : Mod
    {
        public const string HarmonyId = "LeonardoH21.AutonomousRim";
        public static AutonomousRimMod Instance;
        public EvidenceSettings Evidence;

        public AutonomousRimMod(ModContentPack content) : base(content)
        {
            Instance=this; Evidence=GetSettings<EvidenceSettings>();
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll();

            Log.Message("[AutonomousRim] Loaded successfully.");
        }
    }
}
