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
            return $"{bill.repeatMode?.defName}|{bill.targetCount}|{bill.suspended}|{bill.ingredientSearchRadius}|{bill.allowedSkillRange}|{bill.pauseWhenSatisfied}|{bill.unpauseWhenYouHave}|{bill.includeTainted}|{bill.includeEquipped}|" +
                string.Join(",", bill.ingredientFilter.AllowedThingDefs.Where(d => bill.recipe.fixedIngredientFilter?.Allows(d) != false).Select(d => d.defName).OrderBy(n => n));
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref Bill, "bill");
            Scribe_Values.Look(ref Signature, "signature");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && Bill != null && Signature != null)
            {
                // Native saving intersects a bill's filter with its recipe. Default
                // Root filters (notably tailoring) must compare only effective defs,
                // or a save/load cycle falsely looks like a manual ingredient edit.
                int separator = Signature.LastIndexOf('|');
                if (separator >= 0)
                    Signature = Signature.Substring(0, separator + 1) + string.Join(",", Signature.Substring(separator + 1).Split(',')
                        .Select(name => DefDatabase<ThingDef>.GetNamedSilentFail(name)).Where(d => d != null && Bill.recipe.fixedIngredientFilter?.Allows(d) != false)
                        .Select(d => d.defName).OrderBy(n => n));
                if (Signature == LegacyDescribe(Bill)) Signature = Describe(Bill);
            }
        }

        private static string LegacyDescribe(Bill_Production bill) =>
            $"{bill.repeatMode?.defName}|{bill.targetCount}|{bill.suspended}|{bill.ingredientSearchRadius}|{bill.allowedSkillRange}|" +
            string.Join(",", bill.ingredientFilter.AllowedThingDefs.Where(d => bill.recipe.fixedIngredientFilter?.Allows(d) != false).Select(d => d.defName).OrderBy(n => n));
    }
}
