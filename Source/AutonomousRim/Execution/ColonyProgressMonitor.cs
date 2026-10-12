using System.Linq;
using AutonomousRim.Core;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    public static class ColonyProgressMonitor
    {
        public static void Sample(Map map, AutonomousRimMapComponent ai)
        {
            int tick = Find.TickManager.TicksGame;
            var state = ai.Progress;
            var active = new System.Collections.Generic.HashSet<string>();
            var project = Find.ResearchManager.GetProject();
            if (project != null)
            {
                string key = "research:" + project.defName; active.Add(key);
                var result=ResearchDiagnostics.Inspect(map,ai);
                state.Observe(key,"Pesquisa: "+project.LabelCap,Find.ResearchManager.GetProgress(project),result.Eligible,result.Code,result.Detail,tick);
            }
            foreach (var room in ai.BaseProjects.Where(p => !p.Completed))
            {
                string key = "room:" + (room.LayoutSlot ?? room.Kind + ":" + room.Origin); active.Add(key);
                var tasks = room.Shell.Concat(room.Furniture).Where(t => t.Def != null && !t.CancelledByPlayer).ToList();
                int complete = tasks.Count(t => t.Complete(map));
                string detail = "Obras prontas: " + complete + "/" + tasks.Count + ". " +
                    (ai.ExpansionSuspended ? "Pausada por segurança/recuperação." : string.IsNullOrWhiteSpace(room.BlockReason) ? "Estado: " + room.State : room.BlockReason);
                // Construction already has its own native progress watchdog; expose its result.
                state.Observe(key, room.Kind, complete, false, room.State.ToString(), detail, tick,
                    room.Stalled && ai.BaseAutomation && !ai.ExpansionSuspended && map.mapPawns.FreeColonistsSpawned.Any(p=>
                        WorkPriorityManager.CanWork(p) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction) && p.workSettings.GetPriority(WorkTypeDefOf.Construction)>0 && BillDiagnostics.OnShift(p)));
            }
            var bills = map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
                .SelectMany(t => t.BillStack.Bills.OfType<Bill_Production>().Select(b => new { Table = t, Bill = b })).ToList();
            foreach (var pair in bills) active.Add("bill:" + pair.Bill.GetUniqueLoadID());
            // Rotate six bills per sample. Large workshops do not trigger an unbounded path scan.
            int start = bills.Count == 0 ? 0 : ((tick / 600) * 6) % bills.Count;
            foreach (var pair in bills.Skip(start).Concat(bills.Take(start)).Take(6))
            {
                string key = "bill:" + pair.Bill.GetUniqueLoadID();
                var entry = state.Get(key);
                BillDiagnostic result;
                try { result = BillDiagnostics.Inspect(map, pair.Table, pair.Bill, entry.CandidateCursor); }
                catch (System.Exception error)
                {
                    result = new BillDiagnostic("diagnostic-error", "Diagnóstico indisponível para esta receita: " + error.GetType().Name + ". Conferir log; as outras tarefas continuam.");
                    if (entry.Status != "diagnostic-error") Log.Warning("[AutonomousRim.Diagnostics] " + pair.Bill.recipe.defName + ": " + error);
                }
                entry.CandidateCursor = (entry.CandidateCursor + 3) % 60000;
                var driver = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.CurJob?.bill == pair.Bill)?.jobs.curDriver as JobDriver_DoBill;
                if (driver != null)
                {
                    if (entry.WorkStart == driver.billStartTick && entry.WorkLeft > driver.workLeft + .001f)
                    { entry.EligibleTicks = 0; entry.LastProgress = tick; }
                    entry.WorkStart = driver.billStartTick; entry.WorkLeft = driver.workLeft;
                }
                if (ai.SecondarySuspended && pair.Bill.recipe.workSkill != SkillDefOf.Cooking)
                    result = new BillDiagnostic("emergency", "Produção secundária pausada por emergência/recuperação.");
                state.Observe(key, pair.Bill.LabelCap + " • " + pair.Table.LabelCap, entry.Completions,
                    result.Eligible, result.Code, result.Detail, tick);
            }
            foreach (var removed in state.Entries.Where(e => !active.Contains(e.Key)).ToList())
                state.Record(tick, "Objetivo saiu do plano ativo (concluído, removido ou substituído): " + (removed.Label ?? removed.Key));
            state.Entries.RemoveAll(e => !active.Contains(e.Key));
        }
    }
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.Notify_IterationCompleted))]
    public static class ProductionProgressObserver
    {
        public static void Postfix(Bill_Production __instance, Pawn billDoer)
        {
            var ai = billDoer?.Map?.GetComponent<AutonomousRimMapComponent>();
            if (ai == null || billDoer.Faction != Faction.OfPlayer) return;
            var entry = ai.Progress.Get("bill:" + __instance.GetUniqueLoadID());
            entry.Completions++;
            ai.Progress.TotalCompletedProduction++;
            if (entry.Completions == 1 || entry.Completions % 10 == 0)
                ai.Progress.Record(Find.TickManager.TicksGame, __instance.LabelCap + ": " + entry.Completions + " lotes concluídos; último por " + billDoer.LabelShort + ".", "Produção",entry.Key);
            // Count completed native recipe iterations, never queued jobs or stock deltas.
        }
    }
}
