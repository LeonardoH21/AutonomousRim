using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    public static class CommerceDeliveryObserver
    {
        // Valid only during the synchronous native transaction on the game thread.
        public static List<CommerceDelivery> Pending;
    }
    [HarmonyPatch(typeof(TradeUtility),nameof(TradeUtility.SpawnDropPod))]
    public static class CommerceShipmentObserver
    {
        public static void Prefix(Thing t)
        {
            var delivery=CommerceDeliveryObserver.Pending?.FirstOrDefault(d=>d.Def==t.def);
            delivery?.ShipmentIds.Add(t.ThingID);
        }
    }
    [HarmonyPatch(typeof(ActiveTransporter),"PodOpen")]
    public static class CommerceArrivalObserver
    {
        public sealed class Arrival
        {
            public Map Map;
            public CommerceDelivery Delivery;
            public int Before,Count;
        }
        public static void Prefix(ActiveTransporter __instance,out List<Arrival> __state)
        {
            __state=new List<Arrival>();
            var map=__instance.Map;
            if(map==null || __instance.Contents==null)return;
            var commerce=map.GetComponent<AutonomousRimMapComponent>().Commerce;
            foreach(var delivery in commerce.Deliveries)
            {
                int count=__instance.Contents.innerContainer.Where(t=>delivery.ShipmentIds.Contains(t.ThingID)).Sum(t=>t.stackCount);
                if(count>0)__state.Add(new Arrival{Map=map,Delivery=delivery,Before=CommercePlanner.Stock(map,delivery.Def),Count=count});
            }
        }
        public static void Postfix(List<Arrival> __state)
        {
            foreach(var arrival in __state)
            {
                if(CommercePlanner.Stock(arrival.Map,arrival.Delivery.Def)<arrival.Before+arrival.Count)continue;
                var commerce=arrival.Map.GetComponent<AutonomousRimMapComponent>().Commerce;
                arrival.Delivery.Count-=arrival.Count;
                if(arrival.Delivery.Count<=0)commerce.Deliveries.Remove(arrival.Delivery);
                commerce.Record("Entrega orbital confirmada pelo pod: "+arrival.Delivery.Def.LabelCap+" ×"+arrival.Count);
                commerce.OpportunitySignature=null;
            }
        }
    }
}
