using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    // Basic maintenance must remain possible while constructors have a long
    // queue. All fuel, reservations, access and auto-refuel rules stay native.
    public sealed class WorkGiver_EssentialRefuel : WorkGiver_Scanner
    {
        private static readonly WorkGiver_Refuel Native = new WorkGiver_Refuel();
        private static bool EmptyEssential(Thing thing) =>
            thing?.Faction == Faction.OfPlayer &&
            (thing.def.defName == "WoodFiredGenerator" || thing.def.defName == "FueledStove") &&
            thing.TryGetComp<CompRefuelable>()?.HasFuel == false;
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            var ai=pawn.Map?.GetComponent<AutonomousRimMapComponent>();
            return ai?.BaseAutomation!=true || ai.SecondarySuspended || ai.CurrentState?.Threat.Immediate==true ||
                !WorkPriorityManager.CanWork(pawn) || WorkReadiness.NeedsRecovery(pawn) || !pawn.workSettings.Initialized ||
                pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) || pawn.workSettings.GetPriority(WorkTypeDefOf.Hauling)==0 ||
                pawn.workSettings.GetPriority(def.workType)==0;
        }
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) =>
            pawn.Map.listerBuildings.allBuildingsColonist.Where(EmptyEssential);
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override Danger MaxPathDanger(Pawn pawn) => Danger.None;
        public override bool Prioritized => true;
        public override float GetPriority(Pawn pawn, TargetInfo target) => target.Thing?.def.defName=="FueledStove"?2000:1000;
        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false) =>
            !ShouldSkip(pawn,false) && EmptyEssential(thing) && Native.HasJobOnThing(pawn,thing,false);
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false) =>
            HasJobOnThing(pawn,thing,false)?Native.JobOnThing(pawn,thing,false):null;
    }
}
