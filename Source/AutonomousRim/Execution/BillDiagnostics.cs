using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    public sealed class BillDiagnostic
    {
        public string Code, Detail;
        public bool Eligible;
        public BillDiagnostic(string code, string detail, bool eligible = false)
        { Code = code; Detail = detail; Eligible = eligible; }
    }
    public static class BillDiagnostics
    {
        // Invoke the game's ingredient allocator: mixing, fixed ingredients, radius, forbidden
        // stacks and reachability must match native crafting, not a sum of guessed resources.
        private delegate bool IngredientSearch(Bill bill, Pawn pawn, Thing giver, List<ThingCount> chosen, List<IngredientCount> missing);
        private static readonly IngredientSearch FindIngredients = AccessTools.MethodDelegate<IngredientSearch>(
            AccessTools.Method(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients"));
        private delegate UnfinishedThing UnfinishedSearch(Pawn pawn, Bill_ProductionWithUft bill);
        private static readonly UnfinishedSearch FindUnfinished = AccessTools.MethodDelegate<UnfinishedSearch>(
            AccessTools.Method(typeof(WorkGiver_DoBill), "ClosestUnfinishedThingForBill"));
        public static bool TryAllocate(Bill bill, Pawn pawn, Building_WorkTable table, out List<ThingCount> chosen)
        {
            chosen = new List<ThingCount>();
            if (bill is Bill_ProductionWithUft production)
            {
                var unfinished = production.BoundUft;
                if (unfinished != null)
                {
                    // BoundWorker's native getter can clear ownership. A diagnostic must
                    // only read the creator and leave that transition to the game's job giver.
                    return unfinished.Spawned && unfinished.Map == pawn.Map && unfinished.Creator == pawn && pawn.Spawned && !pawn.Downed && pawn.HostFaction == null &&
                        !unfinished.IsForbidden(pawn) && pawn.CanReserveAndReach(unfinished, PathEndMode.Touch, Danger.Some);
                }
                unfinished = FindUnfinished(pawn, production);
                if (unfinished != null && pawn.CanReserveAndReach(unfinished, PathEndMode.InteractionCell, Danger.Some))
                    return true; // Ingredients already belong to this item; reserve no new batch.
            }
            return FindIngredients(bill, pawn, table, chosen, new List<IngredientCount>());
        }
        public static bool Requested(Bill_Production bill)
        {
            if (bill.suspended || bill.DeletedOrDereferenced) return false;
            if (bill.repeatMode == BillRepeatModeDefOf.RepeatCount) return bill.repeatCount > 0;
            if (bill.repeatMode != BillRepeatModeDefOf.TargetCount) return true;
            int count = bill.recipe.WorkerCounter.CountProducts(bill);
            return count < bill.targetCount && !(bill.pauseWhenSatisfied && bill.paused && count > bill.unpauseWhenYouHave);
        }
        public static bool OnShift(Pawn pawn) => !pawn.Drafted && !pawn.InMentalState && !WorkReadiness.NeedsRecovery(pawn) &&
            (pawn.needs?.food?.CurLevelPercentage ?? 1) >= .15f && (pawn.needs?.joy?.CurLevelPercentage ?? 1) >= .15f &&
            (pawn.timetable == null || pawn.timetable.CurrentAssignment == TimeAssignmentDefOf.Work ||
             pawn.timetable.CurrentAssignment == TimeAssignmentDefOf.Anything);
        private static string IngredientDetails(Map map,Building_WorkTable table,Bill_Production bill,Pawn pawn)
        {
            if(bill is Bill_ProductionWithUft uft && uft.BoundUft!=null)
                return "Peça inacabada vinculada a "+uft.BoundUft.Creator?.LabelShort+": conferir localização, proibição e reserva da peça.";
            var items=map.listerThings.AllThings.Where(t=>t.Spawned && t.def.category==ThingCategory.Item && !t.Position.Fogged(map)).ToList();
            var details=new List<string>();
            foreach(var ingredient in bill.recipe.ingredients.Take(4))
            {
                var matching=items.Where(t=>ingredient.filter.Allows(t)).ToList();
                var filtered=matching.Where(t=>ingredient.IsFixedIngredient || bill.ingredientFilter.Allows(t)).ToList();
                var nearby=filtered.Where(t=>t.Position.DistanceToSquared(table.Position)<=bill.ingredientSearchRadius*bill.ingredientSearchRadius).ToList();
                var allowed=nearby.Where(t=>!t.IsForbidden(pawn) && t.Position.IsInAllowedArea(pawn)).ToList();
                string reason=matching.Count==0?"sem estoque conhecido":filtered.Count==0?"excluído pelo filtro da Bill":
                    nearby.Count==0?"fora do raio "+bill.ingredientSearchRadius:allowed.Count==0?"proibido ou fora da área permitida":
                    "pilhas candidatas "+allowed.Sum(t=>t.stackCount)+"; conferir quantidade/mistura, condição, acesso e reservas";
                var alternative=ingredient.filter.AllowedThingDefs.Where(d=>ingredient.IsFixedIngredient || bill.ingredientFilter.Allows(d))
                    .OrderByDescending(d=>allowed.Where(t=>t.def==d).Sum(t=>t.stackCount)).ThenBy(d=>d.defName,StringComparer.Ordinal).FirstOrDefault();
                string quantity=alternative==null?"":"; usando "+alternative.LabelCap+": "+ingredient.CountRequiredOfFor(alternative,bill.recipe,bill)+
                    " necessários, "+allowed.Where(t=>t.def==alternative).Sum(t=>t.stackCount)+" candidatos no raio/área";
                details.Add(ingredient.filter.Summary+": "+reason+quantity);
            }
            return string.Join("; ",details)+". Quantidades candidatas não garantem uma combinação válida; a busca nativa recusou o lote amostrado.";
        }
        public static BillDiagnostic Inspect(Map map, Building_WorkTable table, Bill_Production bill, int candidateCursor = 0)
        {
            if (bill.suspended) return new BillDiagnostic("suspended", "Bill suspensa; configuração preservada.");
            // ShouldDoNow changes pause flags. Read its repeat-mode conditions without mutating them.
            if (bill.repeatMode == BillRepeatModeDefOf.RepeatCount && bill.repeatCount <= 0)
                return new BillDiagnostic("satisfied", "Quantidade solicitada concluída.");
            if (bill.repeatMode == BillRepeatModeDefOf.TargetCount)
            {
                int count = bill.recipe.WorkerCounter.CountProducts(bill);
                if (count >= bill.targetCount || bill.pauseWhenSatisfied && bill.paused && count > bill.unpauseWhenYouHave)
                    return new BillDiagnostic("satisfied", "Meta atendida ou aguardando limiar de retomada: " + count + "/" + bill.targetCount + ".");
            }
            if (!bill.recipe.AvailableNow) return new BillDiagnostic("research", "Receita indisponível: conferir pesquisa/requisitos.");
            if (!table.CurrentlyUsableForBills())
            {
                var power=table.TryGetComp<CompPowerTrader>();var fuel=table.TryGetComp<CompRefuelable>();
                return new BillDiagnostic("bench",power!=null && !power.PowerOn?"Sem energia na bancada; conferir rede, interruptor e geração.":
                    fuel!=null && !fuel.HasFuel?"Sem combustível; autoabastecimento "+(fuel.allowAutoRefuel?"ligado":"desligado")+".":"Bancada indisponível; conferir avaria e requisitos nativos.");
            }
            if (table.IsForbidden(Faction.OfPlayer) || !table.InteractionCell.Standable(map) || table.IsBurning())
                return new BillDiagnostic("access", "Bancada proibida, queimando ou célula de trabalho obstruída.");
            var working = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.CurJob?.bill == bill);
            if (working != null) return new BillDiagnostic("working", "Em execução por " + working.LabelShort + ".", true);
            var workTypes = DefDatabase<WorkGiverDef>.AllDefsListForReading.Where(g =>
                typeof(WorkGiver_DoBill).IsAssignableFrom(g.giverClass) && g.fixedBillGiverDefs?.Contains(table.def) == true &&
                (bill.recipe.requiredGiverWorkType == null || bill.recipe.requiredGiverWorkType == g.workType)).Select(g => g.workType).Distinct().ToList();
            if (workTypes.Count == 0) return new BillDiagnostic("unsupported", "Bancada de mod sem profissão reconhecida; diagnóstico limitado.");
            var assigned = map.mapPawns.FreeColonistsSpawned.Where(p => p.workSettings != null &&
                workTypes.Any(w => !p.WorkTypeIsDisabled(w) && p.workSettings.GetPriority(w) > 0)).ToList();
            // A bound unfinished item has exactly one eligible creator. Do not lose
            // that worker behind the bounded three-candidate ingredient sample.
            if (bill is Bill_ProductionWithUft production && production.BoundUft != null)
                assigned = assigned.Where(p => p == production.BoundUft.Creator).ToList();
            if (assigned.Count == 0) return new BillDiagnostic("worker", "Nenhum colono atribuído à profissão desta bancada.");
            var skilled = assigned.Where(p => bill.PawnAllowedToStartAnew(p) && bill.recipe.FirstSkillRequirementPawnDoesntSatisfy(p) == null).ToList();
            if (skilled.Count == 0) return new BillDiagnostic("skill", "Atribuídos não atendem habilidades ou restrições da Bill.");
            var shift = skilled.Where(OnShift).ToList();
            if (shift.Count == 0) return new BillDiagnostic("shift", "Aguardando turno ou recuperação dos colonos habilitados.");
            var accessible = shift.Where(p => !table.IsForbidden(p) && p.CanReach(table.InteractionCell, PathEndMode.OnCell, Danger.Some)).ToList();
            if (accessible.Count == 0) return new BillDiagnostic("route", "Nenhum trabalhador no turno alcança a bancada dentro da área permitida.");
            // Bound path searches per bill. Other candidates will be inspected on later samples.
            int offset = candidateCursor % accessible.Count;
            foreach (var pawn in accessible.Skip(offset).Concat(accessible.Take(offset)).Take(3))
            {
                if (TryAllocate(bill, pawn, table, out var chosen))
                    return new BillDiagnostic("ready", chosen.Count==0 && bill is Bill_ProductionWithUft
                        ? "Peça inacabada e acesso validados; " + pawn.LabelShort + " pode retomar sem um novo lote de ingredientes."
                        : "Ingredientes e acesso validados; " + pawn.LabelShort + " disponível. "+WorkCoordination.DescribeConflict(pawn,workTypes), true);
            }
            return new BillDiagnostic("ingredients", IngredientDetails(map,table,bill,accessible[offset]));
        }
    }
}
