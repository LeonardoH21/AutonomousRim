using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using RimWorld;
using Verse;

namespace AutonomousRim.Core
{
    public sealed class EquipmentGoal : IExposable
    {
        public string Key, Target, Status, Detail, Responsible;
        public Pawn Recipient;
        public int Stage, Created, Changed, Completed = -1, ReviewDue;
        public bool Current;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Key,"key"); Scribe_Values.Look(ref Target,"target");
            Scribe_References.Look(ref Recipient,"recipient"); Scribe_Values.Look(ref Stage,"stage");
            Scribe_Values.Look(ref Status,"status"); Scribe_Values.Look(ref Detail,"detail");
            Scribe_Values.Look(ref Responsible,"responsible"); Scribe_Values.Look(ref Created,"created");
            Scribe_Values.Look(ref Changed,"changed"); Scribe_Values.Look(ref Completed,"completed",-1);
            Scribe_Values.Look(ref ReviewDue,"reviewDue"); Scribe_Values.Look(ref Current,"current");
        }
    }
    // Goals survive load; native jobs, bills and current inventory remain authoritative.
    public sealed class EquipmentDemandState : IExposable
    {
        public List<EquipmentGoal> Goals = new List<EquipmentGoal>();
        public int LastSample = -1;
        public void Reconcile(Map map, AutonomousRimMapComponent ai)
        {
            int tick=Find.TickManager.TicksGame;
            if(ai.CurrentState?.Tick!=tick)ai.ObserveNow(false);
            LastSample=tick;
            foreach(var goal in Goals) goal.Current=false;
            int stage=DefenseProductionPlan.Stage(map);
            var checkpoint=LoadoutProgression.Analyze(map,stage);
            ai.Coordination.Refresh(map,ai.WorkChanges);
            foreach(var row in checkpoint.Items)
            {
                string key=row.Pawn.GetUniqueLoadID()+":"+stage+":"+row.Target;
                var goal=Goals.FirstOrDefault(g=>g.Key==key);
                if(goal==null)
                {
                    goal=new EquipmentGoal {Key=key,Recipient=row.Pawn,Target=row.Target,Stage=stage,
                        Created=tick,Changed=tick,ReviewDue=tick+120000}; // Review horizon, not a promised ETA.
                    Goals.Add(goal);
                }
                goal.Current=true; goal.Responsible=null;
                string status,detail;
                if(row.State=="Equipado") {status="equipped";detail="Peça equivalente ou melhor efetivamente equipada.";}
                else if(!ai.EquipmentAutomation) {status="disabled";detail="Autoequipamento desligado; demanda apenas informativa.";}
                else if(ai.SecondarySuspended || ai.CurrentState?.Threat?.Immediate==true || EmergencyManager.CriticalFire(map)) {status="emergency";detail="Aguardando segurança e recuperação.";}
                else if(!ai.EquipmentAllowedFor(row.Pawn)) {status="manual";detail="Colono excluído do autoequipamento.";}
                else if(row.State=="Disponível para equipar") {status="delivery";detail="Peça disponível e acessível; ainda não vestida/equipada.";}
                else if(row.State!="Fabricar") {status="manual";detail=row.State;}
                else
                {
                    var recipe=DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r=>r.products?.Any(p=>p.thingDef.defName==row.Target)==true);
                    if(recipe==null) {status="acquisition";detail="Sem receita; aquisição comercial depende do módulo de comércio.";}
                    else if(!recipe.AvailableNow) {status="research";detail="Pesquisa pendente: "+string.Join(", ",recipe.researchPrerequisites?.Where(r=>!r.IsFinished).Select(r=>r.LabelCap.ToString())??Enumerable.Empty<string>());}
                    else
                    {
                        var tables=map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Where(t=>t.def.AllRecipes.Contains(recipe)).ToList();
                        var bill=tables.SelectMany(t=>t.BillStack.Bills.OfType<Bill_Production>()).Where(b=>b.recipe==recipe)
                            .OrderByDescending(b=>ai.DefenseBills.Any(m=>m.Bill==b && m.Matches)).FirstOrDefault();
                        if(tables.Count==0) {status="bench";detail="Construir bancada compatível antes da fabricação.";}
                        else if(bill==null) {status="order";detail="Aguardando emissão de Bill e condições do checkpoint.";}
                        else if(bill.suspended || !ai.DefenseBills.Any(m=>m.Bill==bill && m.Matches))
                        {status="manual";detail="Bill suspensa ou sob controle manual; não emitir coleta para substituí-la.";}
                        else
                        {
                            var demand=ai.Coordination.Equipment.FirstOrDefault(d=>d.Bill==bill);
                            var worker=map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p=>p.CurJob?.bill==bill);
                            if(worker!=null) {status="crafting";detail="Trabalho nativo em execução.";goal.Responsible=worker.LabelShort;}
                            else if(demand?.Ready==true)
                            {
                                status="ready";detail="Bancada, fabricante e ingredientes verificados; aguardando trabalho nativo.";
                                goal.Responsible=demand.ReadyPawns.OrderBy(p=>demand.Works.Min(w=>p.workSettings.GetPriority(w)==0?5:p.workSettings.GetPriority(w))).First().LabelShort;
                            }
                            else {status="blocked";detail=demand?.Blocker??"Aguardando análise nativa da Bill.";}
                        }
                    }
                }
                if(goal.Status!=status) {
                    goal.Changed=tick;
                    ai.Progress.Record(tick,row.Pawn.LabelShort+" • "+row.Target+" → "+status+": "+detail+
                        (goal.Responsible==null?"":" Responsável: "+goal.Responsible),"Equipamentos",key);
                }
                goal.Status=status;goal.Detail=detail;
                if(status=="equipped" && goal.Completed<0) goal.Completed=tick;
                else if(status!="equipped" && goal.Completed>=0) {goal.Completed=-1;goal.ReviewDue=tick+120000;}
            }
            // Retired goals are history, never evidence that their gear was delivered.
            foreach(var goal in Goals.Where(g=>!g.Current)) goal.Responsible=null;
            var obsolete=Goals.Where(g=>!g.Current).OrderByDescending(g=>g.Changed).Skip(64).ToList();
            Goals.RemoveAll(obsolete.Contains);
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref Goals,"goals",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)
            {Goals=Goals??new List<EquipmentGoal>();Goals.RemoveAll(g=>g==null || g.Key==null);LastSample=-1;}
        }
    }
}
