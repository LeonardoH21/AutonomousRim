using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Planning;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    public sealed class EquipmentDemand
    {
        public Bill_Production Bill;
        public Building_WorkTable Table;
        public List<WorkTypeDef> Works = new List<WorkTypeDef>();
        public List<Pawn> ReadyPawns = new List<Pawn>();
        public Dictionary<ThingDef, int> Materials = new Dictionary<ThingDef, int>();
        public string Blocker;
        public bool Ready => ReadyPawns.Count > 0;
    }
    // Per-map derived plan, rebuilt after load. Native bills/jobs and workChanges retain ownership.
    // Reservations are advisory budgets for NEW optional AI blueprints, never native stack locks.
    public sealed class WorkCoordination
    {
        public List<EquipmentDemand> Equipment = new List<EquipmentDemand>();
        public Dictionary<ThingDef, int> Materials = new Dictionary<ThingDef, int>();
        public List<string> Roles = new List<string>();
        public Pawn Researcher;
        public int LastTick = -1;
        private bool equipmentEnabled, emergency;
        private int finishedResearch = -1, benchCount = -1;
        private ResearchProjectDef researchProject;
        private int observationTick = -1;
        private Dictionary<WorkTypeDef,List<Pawn>> previousRoles = new Dictionary<WorkTypeDef,List<Pawn>>();
        public float StabilityBonus(Pawn pawn,WorkTypeDef work) => previousRoles.TryGetValue(work,out var owners) && owners.Contains(pawn)?6f:0f;
        public static string DescribeConflict(Pawn pawn,IEnumerable<WorkTypeDef> works)
        {
            var requested=works.Where(w=>w!=null).ToList();
            int priority=requested.Count==0?4:requested.Where(w=>pawn.workSettings.GetPriority(w)>0).Select(w=>pawn.workSettings.GetPriority(w)).DefaultIfEmpty(4).Min();
            var competitors=DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w=>!requested.Contains(w) && !WorkReadiness.SelfCare(w) &&
                !pawn.WorkTypeIsDisabled(w) && pawn.workSettings.GetPriority(w)>0 && pawn.workSettings.GetPriority(w)<=priority)
                .OrderBy(w=>pawn.workSettings.GetPriority(w)).ThenByDescending(w=>w.naturalPriority).Take(4).Select(w=>w.LabelCap+"="+pawn.workSettings.GetPriority(w));
            string competing=string.Join(", ",competitors);
            return "Job atual: "+(pawn.CurJob?.def?.label??"nenhum")+"; "+(competing.Length==0?"sem prioridades concorrentes detectadas.":"prioridades concorrentes: "+competing+". O job nativo ainda decide disponibilidade/reserva.");
        }
        public HashSet<WorkTypeDef> ReadyWorks => Equipment.Where(d => d.Ready).SelectMany(d => d.Works).ToHashSet();
        public bool CanProduce(Pawn pawn, WorkTypeDef work) => Equipment.Any(d => d.ReadyPawns.Contains(pawn) && d.Works.Contains(work));
        public void Refresh(Map map, IReadOnlyList<WorkPriorityChange> changes, bool force = false)
        {
            var ai = map.GetComponent<AutonomousRimMapComponent>();
            int tick = Find.TickManager.TicksGame;
            int research=DefDatabase<ResearchProjectDef>.AllDefsListForReading.Count(r=>r.IsFinished);
            int benches=map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Count();
            var project=Find.ResearchManager.GetProject();
            if (!force && observationTick==(ai.CurrentState?.Tick??-1) && research==finishedResearch && benches==benchCount && project==researchProject && LastTick >= 0 && tick >= LastTick && tick - LastTick < 600 &&
                equipmentEnabled == ai.EquipmentAutomation && emergency == ai.SecondarySuspended) return;
            finishedResearch=research;benchCount=benches;researchProject=project;
            observationTick=ai.CurrentState?.Tick??-1;
            LastTick = tick; equipmentEnabled = ai.EquipmentAutomation; emergency = ai.SecondarySuspended;
            Equipment.Clear(); Materials.Clear();
            if (emergency) { Researcher = null; Roles.Clear(); return; }
            var available = map.mapPawns.FreeColonistsSpawned.Where(p => WorkPriorityManager.CanWork(p) && !WorkReadiness.NeedsRecovery(p)).ToList();
            var givers = DefDatabase<WorkGiverDef>.AllDefsListForReading.Where(g => g.workType != null &&
                typeof(WorkGiver_DoBill).IsAssignableFrom(g.giverClass)).ToList();
            var tables = map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().ToList();
            foreach (var table in tables)
            foreach (var bill in table.BillStack.Bills.OfType<Bill_Production>().Where(b =>
                b.recipe.products?.Any(p => p.thingDef.IsWeapon || p.thingDef.IsApparel || IndustrySupplyPlan.Component(p.thingDef) &&
                    ai.IndustryBills.Concat(ai.DefenseBills).Any(m=>m.Matches && m.Bill==b)) == true))
            {
                if (!BillDiagnostics.Requested(bill)) continue;
                var demand = new EquipmentDemand { Bill = bill, Table = table };
                Equipment.Add(demand);
                demand.Works = givers.Where(g => g.fixedBillGiverDefs?.Contains(table.def) == true &&
                    (bill.recipe.requiredGiverWorkType == null || bill.recipe.requiredGiverWorkType == g.workType)).Select(g => g.workType).Distinct().ToList();
                if (!bill.recipe.AvailableNow || !table.CurrentlyUsableForBills() || table.IsForbidden(Faction.OfPlayer) ||
                    table.IsBurning() || !table.InteractionCell.Standable(map))
                { demand.Blocker = "Pesquisa, energia, combustível ou acesso da bancada."; continue; }
                var candidates = available.Where(p => demand.Works.Any(w => !p.WorkTypeIsDisabled(w) && !ManualDisabled(p, w, changes)) &&
                    bill.PawnAllowedToStartAnew(p) && bill.recipe.FirstSkillRequirementPawnDoesntSatisfy(p) == null &&
                    !table.IsForbidden(p) && p.CanReach(table.InteractionCell, PathEndMode.OnCell, Danger.Some)).ToList();
                foreach (var pawn in candidates)
                {
                    if (pawn.CurJob?.bill == bill) { demand.ReadyPawns.Add(pawn); continue; }
                    if (!BillDiagnostics.TryAllocate(bill, pawn, table, out var chosen)) continue;
                    demand.ReadyPawns.Add(pawn);
                    if (demand.Materials.Count == 0)
                        foreach (var group in chosen.GroupBy(c => c.Thing.def)) demand.Materials[group.Key] = group.Sum(c => c.Count);
                }
                if(!demand.Ready && candidates.Count>0)
                    foreach(var ingredient in bill.recipe.ingredients)
                    {
                        var def=IndustrySupplyPlan.PreferredInput(map,bill.recipe,bill,ingredient);
                        if(def!=null)demand.Materials[def]=(demand.Materials.TryGetValue(def,out int old)?old:0)+ingredient.CountRequiredOfFor(def,bill.recipe,bill);
                    }
                demand.Blocker = demand.Ready ? "Produção viável; aguardando/executando trabalho nativo." :
                    candidates.Count == 0 ? "Sem fabricante compatível ou acesso permitido." : "Ingredientes utilizáveis indisponíveis.";
            }
            // Protect only untouched AI bills, and only one upcoming iteration of at most
            // two workable orders, including partial stock for a blocked next batch. Never claim a whole future loadout or reserve food/medicine.
            if (equipmentEnabled && (ai.CurrentState?.EstimatedFoodDays ?? 0) >= 1)
            {
                var stock = BaseConstructionManager.Available(map);
                foreach (var demand in Equipment.Where(d => d.Materials.Count>0 && ai.DefenseBills.Concat(ai.IndustryBills).Any(b => b.Matches && b.Bill == d.Bill)).Take(2))
                foreach (var cost in demand.Materials.Where(c => !c.Key.IsNutritionGivingIngestible && c.Key != ThingDefOf.MedicineHerbal && c.Key != ThingDefOf.MedicineIndustrial && c.Key != ThingDefOf.MedicineUltratech))
                {
                    Materials.TryGetValue(cost.Key, out int held); stock.TryGetValue(cost.Key, out int free);
                    int amount=Math.Min(Math.Max(0,free),held+cost.Value);
                    if(amount>0)Materials[cost.Key]=amount;
                }
            }
        }
        private static bool ManualDisabled(Pawn pawn, WorkTypeDef work, IReadOnlyList<WorkPriorityChange> changes) =>
            changes.Any(c => c.Pawn == pawn && c.Work == work && (c.UserOverride || WorkPriorityManager.RawPriority(pawn, work) != c.Applied) && WorkPriorityManager.RawPriority(pawn, work) == 0);
        public int Reserve(Map map, RoomProject project, ThingDef def)
        {
            // High is used by optional industrial additions too; it cannot bypass the
            // equipment budget merely by being an automatic planner's default priority.
            bool shelter=project.RequiresRoof && project.Shell.Any(t=>!t.Complete(map)) &&
                new[]{"Quarto","Estoque","Freezer","Cozinha","Abate","Hospital","Refeitório e recreação"}.Contains(project.Kind);
            bool power=project.Priority<=ConstructionPriority.High && new[]{"Energia e climatização","Conexão elétrica"}.Contains(project.Kind);
            return project.Priority==ConstructionPriority.Critical || shelter || power ? 0 : Materials.TryGetValue(def,out int count)?count:0;
        }
        public void ReportRoles(Dictionary<WorkTypeDef, List<Pawn>> roles)
        {
            previousRoles=roles.ToDictionary(r=>r.Key,r=>r.Value.ToList());
            Researcher = roles.TryGetValue(WorkTypeDefOf.Research, out var scientists) ? scientists.FirstOrDefault() : null;
            Roles=roles.Where(r=>r.Key==WorkTypeDefOf.Construction || r.Key==WorkTypeDefOf.Research || ReadyWorks.Contains(r.Key) ||
                r.Key==WorkTypeDefOf.Doctor || r.Key.defName=="Cooking").Select(r=>
                {
                    var map=r.Value.FirstOrDefault()?.Map;
                    var backup=map?.mapPawns.FreeColonistsSpawned.Where(p=>!r.Value.Contains(p) && WorkPriorityManager.CanWork(p) &&
                        !WorkReadiness.NeedsRecovery(p) && !p.WorkTypeIsDisabled(r.Key) && p.workSettings.GetPriority(r.Key)>0 &&
                        (!ReadyWorks.Contains(r.Key) || CanProduce(p,r.Key)))
                        .OrderByDescending(p=>p.skills?.AverageOfRelevantSkillsFor(r.Key)??0).ThenBy(p=>p.thingIDNumber).FirstOrDefault();
                    return r.Key.LabelCap.ToString()+": "+string.Join(", ",r.Value.Select(p=>p.LabelShort))+
                        (backup==null?"; sem suplente disponível":"; suplente habilitado: "+backup.LabelShort)+
                        "; habilidades relevantes: "+string.Join(", ",r.Value.Select(p=>p.LabelShort+" "+(p.skills?.AverageOfRelevantSkillsFor(r.Key)??0).ToString("0.0")))+
                        ". Seleção considera aptidão, paixões, demandas e estabilidade; prioridade manual é preservada.";
                }).ToList();
        }
        public bool ProtectFromConstruction(Pawn pawn, WorkTypeDef requested)
        {
            if (LastTick < 0 || pawn.Map == null) return false;
            if (Equipment.Any(d => d.Ready && d.ReadyPawns.Contains(pawn) && BillDiagnostics.Requested(d.Bill) && d.Table.CurrentlyUsableForBills() &&
                d.Works.Any(w => pawn.workSettings.GetPriority(w) > 0 && pawn.workSettings.GetPriority(w) < pawn.workSettings.GetPriority(requested)))) return true;
            var project = Find.ResearchManager.GetProject();
            return pawn == Researcher && project != null && pawn.workSettings.GetPriority(WorkTypeDefOf.Research) > 0 &&
                pawn.workSettings.GetPriority(WorkTypeDefOf.Research) < pawn.workSettings.GetPriority(requested) &&
                pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_ResearchBench>().Any(b => project.CanBeResearchedAt(b, false) &&
                    !b.IsForbidden(pawn) && pawn.CanReach(b.InteractionCell, PathEndMode.OnCell, Danger.Some));
        }
    }
}
