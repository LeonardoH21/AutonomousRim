using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    // Shared by our equipment planner and native outfit optimization. Manual wear jobs
    // remain authoritative; emergency thermal care takes precedence over a loadout.
    public static class LoadoutApparelPolicy
    {
        private static bool Military(string target)=>target.Contains("Armor") || target.Contains("Helmet") ||
            target.Contains("FlakVest") || target.Contains("ShieldBelt");
        private static bool Enabled(Pawn p)=>p?.Map?.GetComponent<Core.AutonomousRimMapComponent>().EquipmentAutomation==true &&
            p.Map.GetComponent<Core.AutonomousRimMapComponent>().EquipmentAllowedFor(p) &&
            p.IsFreeColonist && p.ageTracker.AgeBiologicalYears>=18 && !p.WorkTagIsDisabled(WorkTags.Violent);
        private static bool ThermalEmergency(Pawn p)=>p.health.hediffSet.hediffs.Any(h=>
            (h.def.defName=="Hypothermia" || h.def.defName=="Heatstroke") && h.Severity>=.35f);
        public static bool Needed(Pawn p,Apparel item)
        {
            if(!Enabled(p) || ThermalEmergency(p) || p.outfits?.CurrentApparelPolicy?.filter.Allows(item)==false)return false;
            var managed=p.Map.GetComponent<Core.AutonomousRimMapComponent>().ManagedApparel;
            if(EquipmentPlanner.Conflicts(p,item).Any(a=>p.apparel.IsLocked(a) ||
                p.outfits?.forcedHandler.IsForced(a)==true && !managed.Any(m=>m.Pawn==p && m.Apparel==a)))return false;
            return LoadoutProgression.Set(p,DefenseProductionPlan.Stage(p.Map)).Where(Military).Any(target=>
                LoadoutProgression.Meets(p,item,target) && !p.apparel.WornApparel.Any(a=>LoadoutProgression.Meets(p,a,target)));
        }
        public static bool Allows(Pawn p,Apparel item)
        {
            if(!Enabled(p) || ThermalEmergency(p))return true;
            int stage=DefenseProductionPlan.Stage(p.Map);
            var protectedTargets=Enumerable.Range(0,stage+1).SelectMany(s=>LoadoutProgression.Set(p,s)).Where(Military).Distinct();
            foreach(var old in EquipmentPlanner.Conflicts(p,item))
                if(protectedTargets.Any(target=>LoadoutProgression.Meets(p,old,target) && !LoadoutProgression.Meets(p,item,target)))return false;
            // Melee protection gets first claim on an available piece. Do not let a
            // ranged colonist consume the only armor needed by an unprotected melee.
            if(!DefenseProductionPlan.Melee(p) && DefenseProductionPlan.Fighters(p.Map).Where(DefenseProductionPlan.Melee)
                .Any(q=>!q.Downed && !q.InMentalState && Needed(q,item) && q.CanReach(item,Verse.AI.PathEndMode.Touch,Danger.None)))return false;
            return true;
        }
    }
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel),nameof(JobGiver_OptimizeApparel.ApparelScoreGain))]
    public static class NativeLoadoutApparelPolicy
    {
        public static void Postfix(Pawn pawn,Apparel ap,ref float __result)
        {
            if(!LoadoutApparelPolicy.Allows(pawn,ap)){__result=-1000;return;}
            if(__result>-1000 && LoadoutApparelPolicy.Needed(pawn,ap))__result=Math.Max(__result,10);
        }
    }
}
