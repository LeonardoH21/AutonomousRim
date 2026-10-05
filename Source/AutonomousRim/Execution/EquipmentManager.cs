using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    public static class EquipmentManager
    {
        public static bool CanAct(Pawn pawn)
        {
            if (!WorkPriorityManager.CanWork(pawn) || pawn.IsPrisoner || pawn.jobs == null || pawn.jobs.jobQueue.Count > 0 ||
                !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
                pawn.needs.food?.CurCategory >= HungerCategory.UrgentlyHungry || pawn.needs.rest?.CurLevel < 0.2f) return false;
            Job job = pawn.CurJob;
            if (job == null) return true;
            if (job.playerForced || !job.def.playerInterruptible || job.workGiverDef?.workType == WorkTypeDefOf.Doctor) return false;
            return job.def != JobDefOf.Equip && job.def != JobDefOf.Wear && job.def != JobDefOf.Ingest &&
                   job.def != JobDefOf.LayDown && job.def != JobDefOf.Hunt && job.def != JobDefOf.Rescue &&
                   job.def != JobDefOf.TendPatient && job.def != JobDefOf.BeatFire && job.def != JobDefOf.Flee &&
                   job.def != JobDefOf.GotoSafeTemperature && job.def != JobDefOf.Wait_SafeTemperature;
        }

        public static void TrackOrders(List<EquipmentOrder> orders, List<ManagedApparel> owned)
        {
            foreach (EquipmentOrder order in orders.Where(o => o.Pending).ToList())
            {
                if (order.Pawn == null || order.Item == null || order.Item.Destroyed) { order.Pending = false; RestoreReleased(order); continue; }
                bool complete = order.Wear ? order.Pawn.apparel?.Wearing(order.Item) == true : order.Pawn.equipment?.Primary == order.Item;
                if (complete)
                {
                    if (order.Wear && !order.WasForced && order.Pawn.outfits != null)
                    {
                        order.Pawn.outfits.forcedHandler.SetForced((Apparel)order.Item, true);
                        if (!owned.Any(o => o.Pawn == order.Pawn && o.Apparel == order.Item))
                            owned.Add(new ManagedApparel { Pawn = order.Pawn, Apparel = (Apparel)order.Item });
                    }
                    order.Pending = false;
                    order.ReleasedApparel.Clear();
                }
                else if (order.Pawn.CurJob?.GetUniqueLoadID() != order.JobId)
                {
                    order.Pending = false;
                    RestoreReleased(order);
                }
            }
            foreach (ManagedApparel apparel in owned.ToList())
            {
                if (apparel.Pawn?.apparel?.Wearing(apparel.Apparel) != true)
                {
                    if (apparel.Apparel != null) apparel.Pawn?.outfits?.forcedHandler.SetForced(apparel.Apparel, false);
                    owned.Remove(apparel);
                }
                else if (apparel.Pawn.outfits?.forcedHandler.IsForced(apparel.Apparel) != true)
                    owned.Remove(apparel);
                else if (apparel.Pawn.outfits.CurrentApparelPolicy != null && !apparel.Pawn.outfits.CurrentApparelPolicy.filter.Allows(apparel.Apparel))
                {
                    apparel.Pawn.outfits.forcedHandler.SetForced(apparel.Apparel, false);
                    owned.Remove(apparel);
                }
            }
        }

        public static string Apply(Map map, ColonyState state, List<EquipmentOrder> orders, List<ManagedApparel> owned, List<Pawn> excluded)
        {
            TrackOrders(orders, owned);
            if (state.HostilePawnCount > 0) { CancelPending(orders); return "Trocas suspensas: há hostis no mapa."; }
            var usedItems = new HashSet<Thing>(orders.Where(o => o.Pending).Select(o => o.Item));
            var assignedPawns = new HashSet<Pawn>(orders.Where(o => o.Pending).Select(o => o.Pawn));
            int issued = 0;
            int ticks = Find.TickManager.TicksGame;
            foreach (EquipmentDecision decision in state.EquipmentDecisions)
            {
                if (issued >= 2) break;
                Pawn pawn = decision.Pawn;
                EquipmentOrder last = orders.FirstOrDefault(o => o.Pawn == pawn);
                if (excluded.Contains(pawn) || assignedPawns.Contains(pawn) || usedItems.Contains(decision.Item) ||
                    (last != null && ticks - last.IssuedTick < 1800) || !CanAct(pawn)) continue;
                if (decision.Wear ? !EquipmentPlanner.AllowedApparel(pawn, (Apparel)decision.Item, owned) : !EquipmentPlanner.AllowedWeapon(pawn, decision.Item)) continue;
                if (!pawn.CanReserveAndReach(decision.Item, PathEndMode.Touch, Danger.None)) continue;
                var order = new EquipmentOrder { Pawn = pawn, Item = decision.Item, Wear = decision.Wear, IssuedTick = ticks,
                    WasForced = decision.Wear && pawn.outfits?.forcedHandler.IsForced((Apparel)decision.Item) == true };
                if (decision.Wear)
                    foreach (Apparel conflict in EquipmentPlanner.Conflicts(pawn, (Apparel)decision.Item))
                        if (owned.Any(o => o.Pawn == pawn && o.Apparel == conflict) && pawn.outfits?.forcedHandler.IsForced(conflict) == true)
                        {
                            order.ReleasedApparel.Add(conflict);
                            pawn.outfits.forcedHandler.SetForced(conflict, false);
                        }
                Job job = JobMaker.MakeJob(decision.Wear ? JobDefOf.Wear : JobDefOf.Equip, decision.Item);
                order.JobId = job.GetUniqueLoadID();
                if (!pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, false) || pawn.CurJob?.GetUniqueLoadID() != order.JobId)
                {
                    pawn.jobs.jobQueue.RemoveAll(pawn, j => j.GetUniqueLoadID() == order.JobId);
                    RestoreReleased(order);
                    continue;
                }
                order.Pending = true;
                if (last != null) orders.Remove(last);
                orders.Add(order);
                assignedPawns.Add(pawn); usedItems.Add(decision.Item); issued++;
                Log.Message($"[AutonomousRim] Equipment: {pawn.LabelShortCap} → {decision.Item.LabelCap}. {decision.Reason}");
            }
            return $"Autoequipamento: {issued} novas ordens; {orders.Count(o => o.Pending)} em andamento. Intervalo mínimo por colono: 30 s de jogo.";
        }

        private static void RestoreReleased(EquipmentOrder order)
        {
            foreach (Apparel apparel in order.ReleasedApparel)
                if (apparel != null && order.Pawn?.apparel?.Wearing(apparel) == true) order.Pawn.outfits?.forcedHandler.SetForced(apparel, true);
            order.ReleasedApparel.Clear();
        }

        public static void CancelPending(List<EquipmentOrder> orders)
        {
            foreach (EquipmentOrder order in orders.Where(o => o.Pending || o.Pawn?.CurJob?.GetUniqueLoadID() == o.JobId))
            {
                if (order.Pawn?.CurJob?.GetUniqueLoadID() == order.JobId) order.Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                order.Pawn?.jobs?.jobQueue.RemoveAll(order.Pawn, j => j.GetUniqueLoadID() == order.JobId);
                order.Pending = false;
                RestoreReleased(order);
            }
        }

        public static void Stop(List<EquipmentOrder> orders, List<ManagedApparel> owned)
        {
            TrackOrders(orders, owned);
            CancelPending(orders);
            foreach (ManagedApparel apparel in owned)
                if (apparel.Apparel != null) apparel.Pawn?.outfits?.forcedHandler.SetForced(apparel.Apparel, false);
            owned.Clear(); orders.Clear();
        }
    }
}
