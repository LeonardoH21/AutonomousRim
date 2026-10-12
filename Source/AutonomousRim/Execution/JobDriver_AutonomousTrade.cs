using System.Collections.Generic;
using AutonomousRim.Core;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    public sealed class JobDriver_AutonomousTrade : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(()=>
            {
                var ai=pawn.Map.GetComponent<AutonomousRimMapComponent>();
                bool blocked=!ai.CommerceAutomation || ai.SecondarySuspended || WorkReadiness.NeedsRecovery(pawn);
                if(blocked)ai.Commerce.Blocker="Negociação interrompida: módulo desligado, emergência ou necessidades do negociador "+pawn.LabelShort+".";
                return blocked;
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A,job.targetA.Thing is RimWorld.Building_CommsConsole?PathEndMode.InteractionCell:PathEndMode.Touch);
            yield return Toils_General.Wait(120);
            var trade=ToilMaker.MakeToil("AutonomousRimTrade");
            trade.initAction=()=>CommerceManager.Execute(pawn.Map,pawn,pawn.Map.GetComponent<AutonomousRimMapComponent>().Commerce);
            yield return trade;
        }
    }
}
