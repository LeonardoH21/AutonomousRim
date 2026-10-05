using HarmonyLib;
using Verse;

namespace AutonomousRim.Core
{
    public sealed class AutonomousRimMod : Mod
    {
        public const string HarmonyId = "LeonardoH21.AutonomousRim";

        public AutonomousRimMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll();

            Log.Message("[AutonomousRim] Loaded successfully.");
        }
    }
}
