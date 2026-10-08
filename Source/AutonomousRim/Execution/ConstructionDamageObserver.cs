using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    // Only confirmed damage destruction is rebuilt. Player deconstruction/cancellation remains authoritative.
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class ConstructionDamageObserver
    {
        public sealed class DamageState
        {
            public Map Map;
            public List<ConstructionTask> Tasks;
        }
        private static void Prefix(Thing __instance, out DamageState __state)
        {
            __state = null;
            if (!__instance.Spawned || !(__instance is Building) || __instance is Frame || __instance.Faction != Faction.OfPlayer) return;
            var ai = __instance.Map.GetComponent<AutonomousRimMapComponent>();
            if (!ai.BaseAutomation) return;
            var tasks = ai.BaseProjects.SelectMany(BaseConstructionManager.Tasks).Where(t => t.Owned && !t.CancelledByPlayer &&
                t.Def == __instance.def && t.Position == __instance.Position && t.Rotation == __instance.Rotation && t.Stuff == __instance.Stuff).ToList();
            if (tasks.Count > 0) __state = new DamageState { Map = __instance.Map, Tasks = tasks };
        }
        private static void Postfix(Thing __instance, DamageState __state)
        {
            if (__state == null || !__instance.Destroyed) return;
            foreach (var task in __state.Tasks)
            {
                task.WasCompleted = false; task.Issued = false; task.Pending = null; task.Owned = false;
                task.SettingsConfigured = false; task.TemperatureConfigured = false;
            }
            foreach (var project in __state.Map.GetComponent<AutonomousRimMapComponent>().BaseProjects
                .Where(p => BaseConstructionManager.Tasks(p).Any(__state.Tasks.Contains)))
            { project.Completed = false; project.State = ConstructionState.Planned; }
            Log.Message("[AutonomousRim] Reconstrução após dano confirmada: " + __instance.def.defName + ". Materiais e trabalho nativos após segurança.");
        }
    }
}
