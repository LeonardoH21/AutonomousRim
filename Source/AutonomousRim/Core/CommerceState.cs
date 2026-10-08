using System.Collections.Generic;
using RimWorld;
using Verse;
using AutonomousRim.Execution;
using RimWorld.Planet;
using Verse.AI.Group;

namespace AutonomousRim.Core
{
    public sealed class CommerceNeed : IExposable
    {
        public ThingDef Def;
        public int Count, Priority;
        public string Reason;
        public void ExposeData()
        {
            Scribe_Defs.Look(ref Def,"def"); Scribe_Values.Look(ref Count,"count");
            Scribe_Values.Look(ref Priority,"priority"); Scribe_Values.Look(ref Reason,"reason");
        }
    }
    public sealed class CommerceDelivery : IExposable
    {
        public ThingDef Def;
        public int Count, Baseline, Tick;
        public void ExposeData()
        {
            Scribe_Defs.Look(ref Def,"def"); Scribe_Values.Look(ref Count,"count");
            Scribe_Values.Look(ref Baseline,"baseline"); Scribe_Values.Look(ref Tick,"tick");
        }
    }
    public sealed class CommerceDrugPolicy : IExposable
    {
        public Pawn Pawn;
        public DrugPolicy Previous, Applied;
        public string Signature;
        public bool Manual;
        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn,"pawn"); Scribe_References.Look(ref Previous,"previous");
            Scribe_References.Look(ref Applied,"applied"); Scribe_Values.Look(ref Signature,"signature");
            Scribe_Values.Look(ref Manual,"manual");
        }
    }
    public sealed class CommerceState : IExposable
    {
        public List<CommerceNeed> Needs = new List<CommerceNeed>();
        public List<CommerceDelivery> Deliveries = new List<CommerceDelivery>();
        public List<ManagedFoodBill> Bills = new List<ManagedFoodBill>();
        public List<CommerceDrugPolicy> Policies = new List<CommerceDrugPolicy>();
        public List<string> History = new List<string>();
        public Pawn Negotiator, Visitor;
        public TradeShip Ship;
        public string JobId, Status = "Comércio automático desligado.", CaravanStatus = "Expedição ainda não avaliada.";
        public int NextAttempt, LastTrade, Target, Silver, Reserve, Budget, LastDemandTick;
        public float FundingGap;
        public string Product = "SmokeleafJoint";
        public bool ExpeditionsEnabled, Returning, ExpeditionManual;
        public List<Pawn> ExpeditionPawns=new List<Pawn>();
        public Caravan Expedition;
        public Settlement Destination;
        public Lord FormationLord;
        public int ExpeditionStarted, NextExpedition;
        public string ExpeditionStage;
        public int ExpeditionEquipmentStage = -1;
        public List<CommerceNeed> ExpeditionMaterials=new List<CommerceNeed>(), ExpeditionGear=new List<CommerceNeed>();
        public void Record(string message)
        {
            History.Add($"Dia {Find.TickManager.TicksGame/GenDate.TicksPerDay}: {message}");
            if(History.Count>12)History.RemoveAt(0);
            Log.Message("[AutonomousRim.Commerce] "+message);
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref Needs,"needs",LookMode.Deep);
            Scribe_Collections.Look(ref Deliveries,"deliveries",LookMode.Deep);
            Scribe_Collections.Look(ref Bills,"bills",LookMode.Deep);
            Scribe_Collections.Look(ref Policies,"policies",LookMode.Deep);
            Scribe_Collections.Look(ref History,"history",LookMode.Value);
            Scribe_References.Look(ref Negotiator,"negotiator"); Scribe_References.Look(ref Visitor,"visitor");
            Scribe_References.Look(ref Ship,"ship"); Scribe_Values.Look(ref JobId,"jobId");
            Scribe_Values.Look(ref Status,"status"); Scribe_Values.Look(ref CaravanStatus,"caravanStatus");
            Scribe_Values.Look(ref NextAttempt,"nextAttempt"); Scribe_Values.Look(ref LastTrade,"lastTrade");
            Scribe_Values.Look(ref LastDemandTick,"lastDemandTick"); Scribe_Values.Look(ref FundingGap,"fundingGap");
            Scribe_Values.Look(ref Target,"target"); Scribe_Values.Look(ref Product,"product","SmokeleafJoint");
            Scribe_Values.Look(ref ExpeditionsEnabled,"expeditionsEnabled"); Scribe_Values.Look(ref Returning,"returning");
            Scribe_Values.Look(ref ExpeditionManual,"expeditionManual");
            Scribe_Collections.Look(ref ExpeditionPawns,"expeditionPawns",LookMode.Reference);
            Scribe_References.Look(ref Expedition,"expedition"); Scribe_References.Look(ref Destination,"destination");
            Scribe_References.Look(ref FormationLord,"formationLord");
            Scribe_Values.Look(ref ExpeditionStarted,"expeditionStarted"); Scribe_Values.Look(ref NextExpedition,"nextExpedition");
            Scribe_Values.Look(ref ExpeditionStage,"expeditionStage");
            Scribe_Values.Look(ref ExpeditionEquipmentStage,"expeditionEquipmentStage",-1);
            Scribe_Collections.Look(ref ExpeditionMaterials,"expeditionMaterials",LookMode.Deep);
            Scribe_Collections.Look(ref ExpeditionGear,"expeditionGear",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)
            {
                Needs=Needs??new List<CommerceNeed>(); Deliveries=Deliveries??new List<CommerceDelivery>();
                Bills=Bills??new List<ManagedFoodBill>(); Policies=Policies??new List<CommerceDrugPolicy>();
                History=History??new List<string>();
                ExpeditionPawns=ExpeditionPawns??new List<Pawn>();
                ExpeditionMaterials=ExpeditionMaterials??new List<CommerceNeed>(); ExpeditionGear=ExpeditionGear??new List<CommerceNeed>();
            }
        }
    }
}
