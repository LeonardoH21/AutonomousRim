using System.Linq;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    public static class WorkReadiness
    {
        public static bool CanBuildRecoveryFurniture(Pawn pawn) => pawn.Map != null &&
            pawn.Map.GetComponent<AutonomousRim.Core.AutonomousRimMapComponent>().BaseAutomation &&
            !pawn.Map.GetComponent<AutonomousRim.Core.AutonomousRimMapComponent>().ExpansionSuspended &&
            pawn.Map.GetComponent<AutonomousRim.Core.AutonomousRimMapComponent>().BaseProjects.Any(p => p.Kind == "Recreação inicial" &&
                p.Furniture.Exists(t => !t.Complete(pawn.Map) && !t.CancelledByPlayer)) &&
            !SeriousMedicalNeed(pawn) && (pawn.needs?.rest?.CurLevelPercentage ?? 1) >= .35f &&
            (pawn.needs?.food?.CurLevelPercentage ?? 1) >= .2f && (pawn.needs?.mood?.CurLevelPercentage ?? 1) >= .3f;
        public static bool SeriousMedicalNeed(Pawn pawn) => pawn.Downed ||
            HealthAIUtility.ShouldBeTendedNowByPlayerUrgent(pawn) ||
            pawn.health.summaryHealth.SummaryHealthPercent < .75f ||
            pawn.health.hediffSet.BleedRateTotal > .15f || pawn.health.hediffSet.PainTotal > .5f ||
            pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) < .7f;
        public static bool NeedsRecovery(Pawn pawn) => pawn.Downed ||
            SeriousMedicalNeed(pawn) ||
            (pawn.needs?.rest?.CurLevelPercentage ?? 1f) < .25f ||
            (pawn.needs?.joy?.CurLevelPercentage ?? 1f) < .2f ||
            (pawn.needs?.mood?.CurLevelPercentage ?? 1f) < .3f ||
            (pawn.needs?.food?.CurLevelPercentage ?? 1f) < .15f;

        public static bool Restored(Pawn pawn) => !pawn.Downed &&
            !HealthAIUtility.ShouldSeekMedicalRest(pawn) &&
            (pawn.needs?.rest?.CurLevelPercentage ?? 1f) >= .6f &&
            (pawn.needs?.joy?.CurLevelPercentage ?? 1f) >= .45f &&
            (pawn.needs?.mood?.CurLevelPercentage ?? 1f) >= .4f &&
            (pawn.needs?.food?.CurLevelPercentage ?? 1f) >= .3f;

        public static bool SelfCare(WorkTypeDef work) => work.defName == "Patient" ||
            work.defName == "PatientBedRest" || work.defName == "BasicWorker";

        public static bool CanProduceEmergencyFood(Pawn pawn) => !pawn.Downed &&
            !SeriousMedicalNeed(pawn) &&
            (pawn.needs?.rest?.CurLevelPercentage ?? 1f) >= .25f &&
            (pawn.needs?.joy?.CurLevelPercentage ?? 1f) >= .2f &&
            (pawn.needs?.mood?.CurLevelPercentage ?? 1f) >= .3f;
    }
}
