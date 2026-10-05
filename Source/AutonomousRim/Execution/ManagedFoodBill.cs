using System.Linq;
using RimWorld;
using Verse;

namespace AutonomousRim.Execution
{
    public sealed class ManagedFoodBill : IExposable
    {
        public Bill_Production Bill;
        public string Signature;
        public bool Matches => Bill != null && !Bill.DeletedOrDereferenced && Signature == Describe(Bill);

        public static string Describe(Bill_Production bill)
        {
            return $"{bill.repeatMode?.defName}|{bill.targetCount}|{bill.suspended}|{bill.ingredientSearchRadius}|{bill.allowedSkillRange}|" +
                string.Join(",", bill.ingredientFilter.AllowedThingDefs.Select(d => d.defName).OrderBy(n => n));
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref Bill, "bill");
            Scribe_Values.Look(ref Signature, "signature");
        }
    }
}
