using System;
using System.Linq;
using AutonomousRim.Perception;
using Verse;
using System.Collections.Generic;
using AutonomousRim.Execution;
using RimWorld;
using AutonomousRim.Planning;

namespace AutonomousRim.Core
{
    public sealed class AutonomousRimMapComponent : MapComponent
    {
        private const int ScanIntervalTicks = 360;
        private const int LootIntervalTicks = 60;
        private const int ThreatIntervalTicks = 15;
        private bool emergencyAutomation = true;
        private EmergencyState emergency = new EmergencyState();
        public EmergencyState Emergency => emergency;
        public bool EmergencyAutomation => emergencyAutomation;
        public bool SecondarySuspended => emergencyAutomation && emergency.BlockSecondary;
        public bool ExpansionSuspended => emergencyAutomation && emergency.BlockExpansion;
        public bool EffectiveCombat => combatAutomation || emergencyAutomation && emergency.Phase==EmergencyPhase.Danger;
        public string EmergencyStatus => $"Emergência: {emergency.Phase}; {emergency.Decision}. Vulneráveis: {CurrentState?.Threat?.Vulnerable.Count ?? 0}; resgates sem rota/cama segura: {emergency.DeferredRescues}; sem abrigo acessível: {emergency.MissingShelters}.";

        public ColonyState CurrentState { get; private set; }
        public ExistingHousing Housing {get;}=new ExistingHousing();
        public ConstructionWorkIndex WorkIndex {get;}=new ConstructionWorkIndex();
        private ProgressState progress = new ProgressState();
        public ProgressState Progress => progress;
        private LogisticsState logistics=new LogisticsState();
        public LogisticsState Logistics=>logistics;
        private RecoveryState recovery=new RecoveryState();
        public RecoveryState Recovery=>recovery;
        private List<ManagedFoodBill> dependencyBills=new List<ManagedFoodBill>();
        public GoalDependencyPlanner GoalGraph { get; } = new GoalDependencyPlanner();
        public WinterForecast Winter { get; } = new WinterForecast();
        public DefenseDoctrine Doctrine {get;}=new DefenseDoctrine();
        public OpportunityPlanner Opportunities {get;}=new OpportunityPlanner();
        public DlcReadiness Dlc {get;}=new DlcReadiness();
        private int observedThreatCount;
        private PostBattleCare aftercare=new PostBattleCare();
        public PostBattleCare Aftercare=>aftercare;
        public IEnumerable<ManagedFoodBill> ManagedProduction => ownedBills.Concat(defenseBills).Concat(industryBills).Concat(infrastructureBills).Concat(commerce.Bills).Concat(dependencyBills);
        private ObservationMemory observations=new ObservationMemory();
        public ObservationMemory Observations => observations;
        public TickRateMeter Performance { get; } = new TickRateMeter();
        public PolicyTrialSession Trial { get; } = new PolicyTrialSession();
        public string PolicyConfiguration => "learning-v2|continuity-v1|"+string.Join(",",new[]{baseAutomation,foodAutomation,workAutomation,scheduleAutomation,
            equipmentAutomation,strategyAutomation,lootAutomation,combatAutomation,emergencyAutomation,commerceAutomation,prisonAutomation})+
            "|trade="+commerce.RefreshTicks+","+commerce.JointReserve+","+commerce.SilverTarget+
            "|parallel="+CombatTacticalPlanner.ParallelEnabled+","+CombatTacticalPlanner.MinimumPairs;
        public string PolicyScenario => "colonos="+map.mapPawns.FreeColonistsSpawnedCount+"|estacao="+GenLocalDate.Season(map);
        public PolicyVerdict PolicyComparison => AutonomousRimMod.Instance?.Evidence?.Compare(observations.Latest?.Context,PolicyConfiguration,PolicyScenario)
            ?? new PolicyVerdict {Detail="Arquivo de comparação indisponível."};
        public bool ApplyProvenPolicy()
        {
            if(!PolicyComparison.Approved)return false;
            SetObservationPolicy("food-buffer-v1");return observations.Policy=="food-buffer-v1";
        }
        public List<RoomObservation> ObservedRooms { get; private set; } = new List<RoomObservation>();
        public void ObserveNow(bool rooms=true)
        {
            string transition=CurrentState?.Threat?.LastTransition;
            CurrentState=ColonyStateScanner.Scan(map);
            if(transition!=null)CurrentState.Threat.LastTransition=transition;
            var frame=ObservationReader.Capture(map,CurrentState,this);
            observations.Add(frame,rooms);
            progress.Bind(map.uniqueID,frame.Tick,frame.Context+" • "+frame.Summary,frame.Policy);
            if(rooms)ObservedRooms=RoomObserver.Read(map);
        }
        public void SetObservationPolicy(string policy)
        {
            if(!observations.ChangePolicy(policy))return;
            ObserveNow();progress.Record(Find.TickManager.TicksGame,"Política alimentar: "+policy+"; anterior "+observations.PreviousPolicy,"Estratégia","food-policy");
        }
        public string ArchiveObservation()
        {
            var evidence=StrategyEvidenceBuilder.From(observations,observations.ColonyId);
            if(evidence==null)return "Exportação indisponível: requer um dia observado, sem ameaça, caídos, fome ou mudança de população nesta sessão.";
            var mod=AutonomousRimMod.Instance;
            if(mod?.Evidence==null)return "Arquivo de evidências indisponível.";
            if(!mod.Evidence.Add(evidence))return "Janela já registrada.";
            mod.WriteSettings();return "Janela registrada entre colônias; nenhuma política foi aplicada automaticamente.";
        }
        private bool foodAutomation;
        private bool workAutomation;
        private bool scheduleAutomation = true;
        private bool equipmentAutomation;
        private bool combatAutomation;
        private bool prisonAutomation;
        private PrisonState prison = new PrisonState();
        public PrisonState Prison => prison;
        public bool PrisonAutomation => prisonAutomation;
        public void SetPrisonAutomation(bool enabled)
        {
            prisonAutomation=enabled;
            var projects=baseProjects.Where(p=>p.LayoutSlot?.StartsWith("prison:")==true).ToList();
            if(!enabled){PrisonManager.Stop(prison);BaseConstructionManager.Stop(map,projects);}
            else
            {
                foreach(var project in projects.Where(p=>p.State==ConstructionState.Paused && !p.Completed))project.State=ConstructionState.Planned;
                PrisonManager.Resume(prison);
                prison.LastPlan=-600;
                if(CurrentState!=null)PrisonManager.Apply(map,CurrentState,baseProjects,prison,baseAutomation);
            }
        }
        private bool commerceAutomation;
        private CommerceState commerce = new CommerceState();
        public bool CommerceAutomation => commerceAutomation;
        public CommerceState Commerce => commerce;
        public void SetCommerceAutomation(bool enabled)
        {
            commerceAutomation=enabled;
            var commercial=baseProjects.Where(p=>p.LayoutSlot?.StartsWith("commerce:")==true).ToList();
            if(!enabled)
            {
                CommerceManager.Stop(commerce);
                BaseConstructionManager.Stop(map,commercial);
            }
            else
            {
                foreach(var project in commercial.Where(p=>p.State==ConstructionState.Paused && !p.Completed))project.State=ConstructionState.Planned;
                if(CurrentState!=null)CommerceManager.Apply(map,CurrentState,baseProjects,commerce,baseAutomation);
            }
        }
        private List<CombatOrder> combatOrders = new List<CombatOrder>();
        private List<Pawn> combatExcluded = new List<Pawn>();
        public bool CombatAutomation => combatAutomation;
        public string CombatStatus { get; private set; } = "Combate cooperativo desligado.";
        public IReadOnlyList<CombatOrder> CombatOrders => combatOrders;
        public IReadOnlyList<ConstructionOrder> ConstructionOrders=>constructionOrders;
        public IReadOnlyList<EquipmentOrder> EquipmentOrders=>equipmentOrders;
        public void NotifyManualCombatOrder(Pawn pawn)
        {
            if(!combatOrders.Any(o=>o.Pawn==pawn))return;
            combatOrders.RemoveAll(o=>o.Pawn==pawn);
            if(!combatExcluded.Contains(pawn))combatExcluded.Add(pawn);
        }
        public bool CanAssignMedical(Pawn pawn)
        {
            if(combatExcluded.Contains(pawn))return false;
            var owned=combatOrders.FirstOrDefault(o=>o.Pawn==pawn);
            return owned!=null ? pawn.CurJob?.playerForced!=true || pawn.CurJob.GetUniqueLoadID()==owned.JobId :
                !pawn.Drafted && pawn.CurJob?.playerForced!=true;
        }
        public bool ReleaseCombatForMedical(Pawn pawn)
        {
            if(!CanAssignMedical(pawn))return false;
            var owned=combatOrders.FirstOrDefault(o=>o.Pawn==pawn);
            if(owned!=null){CombatManager.Stop(new List<CombatOrder>{owned});combatOrders.Remove(owned);}
            return true;
        }
        private bool baseAutomation = true;
        private bool strategyAutomation = true;
        private StrategicPlan strategicPlan = new StrategicPlan();
        private FailureMemory failureMemory = new FailureMemory();
        public bool StrategyAutomation => strategyAutomation;
        public StrategicPlan Strategy => strategicPlan;
        public FailureMemory FailureMemory => failureMemory;
        public string FailureStatus => failureMemory?.Status ?? "Análise de falha indisponível.";
        private int lastPlanTick = -3600;
        private int lastConstructionTick = -120;
        private int lastConstructionReview = -120;
        private int plannedPawnCount;
        private bool plannedClimateResearch;
        private List<ConstructionOrder> constructionOrders = new List<ConstructionOrder>();
        private List<Thing> constructionGathering = new List<Thing>();
        private List<IntVec3> gatheringMineCells = new List<IntVec3>();
        public string GatheringStatus { get; private set; } = "Coleta desativada.";
        private bool lootAutomation = true;
        private int lastLootTick = -600;
        private List<Thing> releasedLoot = new List<Thing>();
        public bool LootAutomation => lootAutomation;
        public string LootStatus { get; private set; } = "Allow gradual desativado.";
        private List<RoomProject> baseProjects = new List<RoomProject>();
        public bool BaseAutomation => baseAutomation;
        public IReadOnlyList<RoomProject> BaseProjects => baseProjects;
        public string BaseStatus { get; private set; } = "Base automática desativada. Gere um plano para ver os módulos iniciais.";
        private List<EquipmentOrder> equipmentOrders = new List<EquipmentOrder>();
        private List<ManagedApparel> managedApparel = new List<ManagedApparel>();
        private List<Pawn> equipmentExcluded = new List<Pawn>();
        internal List<ManagedApparel> ManagedApparel => managedApparel;
        public bool EquipmentAutomation => equipmentAutomation;
        public string EquipmentStatus { get; private set; } = "Autoequipamento desativado.";
        private List<Pawn> ownedHunts = new List<Pawn>();
        private List<ManagedFoodBill> defenseBills = new List<ManagedFoodBill>();
        public IReadOnlyList<ManagedFoodBill> DefenseBills => defenseBills;
        private List<ManagedFoodBill> industryBills = new List<ManagedFoodBill>();
        public IReadOnlyList<ManagedFoodBill> IndustryBills => industryBills;
        public IndustrySupplyPlan Industry { get; } = new IndustrySupplyPlan();
        public WorkCoordination Coordination { get; } = new WorkCoordination();
        private EquipmentDemandState equipmentDemands = new EquipmentDemandState();
        public EquipmentDemandState EquipmentDemands => equipmentDemands;
        private List<ManagedFoodBill> infrastructureBills = new List<ManagedFoodBill>();
        private List<ManagedFoodBill> ownedBills = new List<ManagedFoodBill>();
        private List<WorkPriorityChange> workChanges = new List<WorkPriorityChange>();
        public IReadOnlyList<WorkPriorityChange> WorkChanges => workChanges;
        private List<ScheduleChange> scheduleChanges = new List<ScheduleChange>();
        public bool FoodAutomation => foodAutomation;
        public bool WorkAutomation => workAutomation;
        public bool ScheduleAutomation => scheduleAutomation;
        public string ScheduleStatus { get; private set; } = "Agenda automática aguardando a primeira avaliação.";
        public string ManagementStatus { get; private set; } = "Automação desativada neste mapa.";
        public string StorageStatus { get; private set; } = "Armazenamento aguardando avaliação.";

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref emergencyAutomation,"emergencyAutomation",true);
            Scribe_Deep.Look(ref emergency,"emergency");
            Scribe_Values.Look(ref foodAutomation, "foodAutomation");
            Scribe_Values.Look(ref workAutomation, "workAutomation");
            Scribe_Values.Look(ref scheduleAutomation, "scheduleAutomation", true);
            Scribe_Values.Look(ref equipmentAutomation, "equipmentAutomation");
            Scribe_Values.Look(ref combatAutomation, "combatAutomation");
            Scribe_Values.Look(ref prisonAutomation,"prisonAutomation");
            Scribe_Deep.Look(ref prison,"prison");
            Scribe_Values.Look(ref commerceAutomation,"commerceAutomation");
            Scribe_Deep.Look(ref commerce,"commerce");
            Scribe_Collections.Look(ref combatOrders, "combatOrders", LookMode.Deep);
            Scribe_Collections.Look(ref combatExcluded, "combatExcluded", LookMode.Reference);
            Scribe_Values.Look(ref baseAutomation, "baseAutomation");
            Scribe_Values.Look(ref strategyAutomation, "strategyAutomation");
            Scribe_Deep.Look(ref strategicPlan, "strategicPlan");
            Scribe_Deep.Look(ref failureMemory, "failureMemory");
            Scribe_Values.Look(ref lastPlanTick, "lastPlanTick", -3600);
            Scribe_Values.Look(ref plannedPawnCount, "plannedPawnCount");
            Scribe_Values.Look(ref plannedClimateResearch, "plannedClimateResearch");
            Scribe_Collections.Look(ref constructionOrders, "constructionOrders", LookMode.Deep);
            // Native map compression does not preserve individual mineable IDs.
            // Save their cells; keep ordinary plant references for wood gathering.
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                gatheringMineCells = constructionGathering.FindAll(t => t is Mineable && t.Spawned).ConvertAll(t => t.Position);
                var plants = constructionGathering.FindAll(t => !(t is Mineable));
                Scribe_Collections.Look(ref plants, "constructionGathering", LookMode.Reference);
            }
            else Scribe_Collections.Look(ref constructionGathering, "constructionGathering", LookMode.Reference);
            Scribe_Collections.Look(ref gatheringMineCells, "gatheringMineCells", LookMode.Value);
            Scribe_Values.Look(ref lootAutomation, "lootAutomation");
            Scribe_Values.Look(ref lastLootTick, "lastLootTick", -600);
            Scribe_Collections.Look(ref releasedLoot, "releasedLoot", LookMode.Reference);
            Scribe_Collections.Look(ref baseProjects, "baseProjects", LookMode.Deep);
            Scribe_Collections.Look(ref equipmentOrders, "equipmentOrders", LookMode.Deep);
            Scribe_Collections.Look(ref managedApparel, "managedApparel", LookMode.Deep);
            Scribe_Collections.Look(ref equipmentExcluded, "equipmentExcluded", LookMode.Reference);
            Scribe_Collections.Look(ref ownedHunts, "ownedHunts", LookMode.Reference);
            Scribe_Collections.Look(ref defenseBills, "defenseBills", LookMode.Deep);
            Scribe_Collections.Look(ref industryBills, "industryBills", LookMode.Deep);
            Scribe_Collections.Look(ref infrastructureBills,"infrastructureBills",LookMode.Deep);
            Scribe_Collections.Look(ref ownedBills, "ownedBills", LookMode.Deep);
            Scribe_Collections.Look(ref workChanges, "workChanges", LookMode.Deep);
            Scribe_Collections.Look(ref scheduleChanges, "scheduleChanges", LookMode.Deep);
            Scribe_Deep.Look(ref progress, "progressDiagnostics");
            Scribe_Values.Look(ref observedThreatCount,"observedThreatCount");
            Scribe_Deep.Look(ref logistics,"logistics");
            Scribe_Deep.Look(ref recovery,"automaticRecovery");
            Scribe_Deep.Look(ref aftercare,"postBattleCare");
            Scribe_Collections.Look(ref dependencyBills,"dependencyBills",LookMode.Deep);
            Scribe_Deep.Look(ref observations,"observations");
            Scribe_Deep.Look(ref equipmentDemands, "equipmentDemands");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                progress = progress ?? new ProgressState();
                logistics=logistics??new LogisticsState();
                recovery=recovery??new RecoveryState();
                aftercare=aftercare??new PostBattleCare();
                dependencyBills=dependencyBills??new List<ManagedFoodBill>();
                observations=observations??new ObservationMemory();
                equipmentDemands = equipmentDemands ?? new EquipmentDemandState();
                strategicPlan = strategicPlan ?? new StrategicPlan();
                emergency = emergency ?? new EmergencyState();
                commerce=commerce??new CommerceState();
                prison=prison??new PrisonState();
                combatOrders = combatOrders ?? new List<CombatOrder>();
                combatExcluded = combatExcluded ?? new List<Pawn>();
                combatOrders.RemoveAll(o => o.Pawn == null);
                combatExcluded.RemoveAll(p => p == null);
                failureMemory = failureMemory ?? new FailureMemory();
                ownedHunts = ownedHunts ?? new List<Pawn>();
                defenseBills = defenseBills ?? new List<ManagedFoodBill>();
                industryBills = industryBills ?? new List<ManagedFoodBill>();
                infrastructureBills=infrastructureBills??new List<ManagedFoodBill>();
                ownedBills = ownedBills ?? new List<ManagedFoodBill>();
                workChanges = workChanges ?? new List<WorkPriorityChange>();
                scheduleChanges = scheduleChanges ?? new List<ScheduleChange>();
                workChanges.RemoveAll(c => c.Pawn == null || c.Work == null);
                equipmentOrders = equipmentOrders ?? new List<EquipmentOrder>();
                managedApparel = managedApparel ?? new List<ManagedApparel>();
                equipmentExcluded = equipmentExcluded ?? new List<Pawn>();
                equipmentOrders.RemoveAll(o => o.Pawn == null);
                managedApparel.RemoveAll(o => o.Pawn == null || o.Apparel == null);
                equipmentExcluded.RemoveAll(p => p == null);
                baseProjects = baseProjects ?? new List<RoomProject>();
                releasedLoot = releasedLoot ?? new List<Thing>();
                releasedLoot.RemoveAll(t => t == null || t.Destroyed);
                constructionOrders = constructionOrders ?? new List<ConstructionOrder>();
                constructionOrders.RemoveAll(o => o.Pawn == null);
                constructionGathering = constructionGathering ?? new List<Thing>();
                constructionGathering.RemoveAll(t => t == null || t.Destroyed);
                scheduleChanges.RemoveAll(c => c.Pawn == null || c.Pawn.Dead);
            }
        }

        public void PreviewBase()
        {
            BaseStatus = BasePlanner.Plan(map, baseProjects);
            lastPlanTick = Find.TickManager.TicksGame;
            plannedPawnCount = map.mapPawns.FreeColonistsSpawnedCount;
            plannedClimateResearch = DefDatabase<ResearchProjectDef>.GetNamed("AirConditioning").IsFinished;
        }

        public void SetBaseAutomation(bool enabled)
        {
            if (!enabled)
            {
                ConstructionWorkManager.Stop(constructionOrders);
                ConstructionResourceManager.Stop(map, constructionGathering);
                BaseConstructionManager.Stop(map, baseProjects);
                IndustryInfrastructure.Suspend(map,baseProjects);
                FoodManager.RemoveOwnedBills(infrastructureBills);
                foreach(var task in baseProjects.SelectMany(p=>p.Shell).Where(t=>t.UpgradeFrom?.Spawned==true && t.UpgradeMaterial!=null))
                {
                    map.designationManager.DesignationOn(task.UpgradeFrom,DesignationDefOf.Deconstruct)?.Delete();
                    StoneProductionManager.CancelDemolition(map,task.UpgradeFrom);
                    task.Stuff=task.UpgradeFrom.Stuff;task.UpgradeMaterial=null;task.UpgradeFrom=null;
                    task.UpgradeDeclined=true;task.WasCompleted=true;
                }
            }
            baseAutomation = enabled;
            if (enabled)
            {
                foreach(var project in baseProjects.Where(p=>p.State==ConstructionState.Paused &&
                    !BaseConstructionManager.Tasks(p).Any(t=>t.CancelledByPlayer)))project.State=ConstructionState.Planned;
                PreviewBase();
                // Reconcile queued storage before issuing construction on load
                // or toggle; the periodic food/storage scan may not be due yet.
                StorageStatus=StoragePolicy.ManageFoodStorage(map,CurrentState??ColonyStateScanner.Scan(map),baseProjects);
                if (lootAutomation && CurrentState != null)
                {
                    LootStatus = LootAccessManager.Apply(map, CurrentState, true, baseProjects, managedApparel, EquipmentAllowedFor, releasedLoot, out int released, (foodAutomation ? ownedBills : new List<ManagedFoodBill>()).Concat(equipmentAutomation ? defenseBills : new List<ManagedFoodBill>()).ToList());
                    lastLootTick = Find.TickManager.TicksGame;
                }
                ExecuteConstruction(Find.TickManager.TicksGame);
            }
            else BaseStatus = "Base automática desligada. Projetos pendentes da IA cancelados; estruturas e obras com materiais mantidas.";
        }

        public void DisableAll()
        {
            FoodManager.RemoveOwnedBills(dependencyBills);
            SetPrisonAutomation(false);
            SetCommerceAutomation(false);
            SetEmergencyAutomation(false);
            SetCombatAutomation(false);
            SetStrategyAutomation(false);
            SetLootAutomation(false);
            SetBaseAutomation(false);
            SetEquipmentAutomation(false);
            SetAutomation(false, false);
            SetScheduleAutomation(false);
        }

        public bool AllAutomationEnabled => BaseAutomation && EquipmentAutomation && FoodAutomation && WorkAutomation &&
            LootAutomation && StrategyAutomation && ScheduleAutomation && CombatAutomation && EmergencyAutomation && CommerceAutomation && PrisonAutomation;
        public void EnableAll()
        {
            SetAutomation(true,true);
            SetScheduleAutomation(true);
            SetBaseAutomation(true);
            SetLootAutomation(true);
            SetEquipmentAutomation(true);
            SetStrategyAutomation(true);
            SetCombatAutomation(true);
            SetEmergencyAutomation(true);
            SetCommerceAutomation(true);
            SetPrisonAutomation(true);
        }

        public void SetCombatAutomation(bool enabled)
        {
            if(!enabled && emergencyAutomation) SetEmergencyAutomation(false);
            combatAutomation = enabled;
            if (!enabled) { CombatManager.Stop(combatOrders); combatExcluded.Clear(); }
            CombatStatus = enabled ? CombatManager.Apply(map, combatOrders, combatExcluded) : "Combate cooperativo desligado; controle da IA liberado.";
        }

        public void SetEmergencyAutomation(bool enabled)
        {
            emergencyAutomation=enabled;
            if(!enabled) { EmergencyManager.Stop(emergency); if(!combatAutomation)CombatManager.Stop(combatOrders); }
            else { CurrentState=ColonyStateScanner.Scan(map); RefreshThreat(Find.TickManager.TicksGame); }
        }

        public void SetScheduleAutomation(bool enabled)
        {
            if (!enabled) ScheduleManager.Restore(scheduleChanges);
            scheduleAutomation = enabled;
            ScheduleStatus = enabled ? ScheduleManager.Apply(map, CurrentState ?? ColonyStateScanner.Scan(map), scheduleChanges, true) : "Agenda automática desligada; agenda restaurada quando possível.";
        }

        public void SetStrategyAutomation(bool enabled)
        {
            if (!enabled && strategicPlan.OwnedResearch != null && Find.ResearchManager.GetProject() == strategicPlan.OwnedResearch)
            {
                if (strategicPlan.PreviousResearch?.IsFinished == false) Find.ResearchManager.SetCurrentProject(strategicPlan.PreviousResearch);
                else Find.ResearchManager.StopProject(strategicPlan.OwnedResearch);
            }
            if (enabled && !strategyAutomation)
            {
                strategicPlan.ResearchOverride = false;
                strategicPlan.PreviousResearch = Find.ResearchManager.GetProject();
                // Explicit re-enable adopts the current selection without replacing it.
                strategicPlan.OwnedResearch = strategicPlan.PreviousResearch;
            }
            strategyAutomation = enabled;
            EvaluateStrategy();
        }

        public void EvaluateStrategy()
        {
            var before=Find.ResearchManager.GetProject();
            ObserveNow(false);
            StrategicPlanner.Evaluate(map, CurrentState, baseProjects, strategicPlan, strategyAutomation);
            var after=Find.ResearchManager.GetProject();
            if(before!=after)progress.Record(Find.TickManager.TicksGame,"Pesquisa selecionada: "+(after?.LabelCap.ToString()??"nenhuma")+
                ". "+strategicPlan.ResearchStatus+" Foco: "+strategicPlan.CurrentFocus,"Estratégia","research:"+after?.defName);
            if (strategyAutomation && baseAutomation && !ExpansionSuspended && StrategicInfrastructure.Add(map, baseProjects)) PreviewBase();
        }

        public void EvaluateFailure()
        {
            CurrentState = CurrentState ?? ColonyStateScanner.Scan(map);
            FailureAnalyzer.Evaluate(map, CurrentState, failureMemory);
        }

        public void SetLootAutomation(bool enabled)
        {
            lootAutomation = enabled;
            if (enabled) ManageColony();
            else LootStatus = "Allow desligado; itens já liberados permanecem disponíveis.";
        }

        public void SetEquipmentAutomation(bool enabled)
        {
            if (!enabled) { IndustryInfrastructure.Suspend(map,baseProjects); EquipmentManager.Stop(equipmentOrders, managedApparel); FoodManager.RemoveOwnedBills(defenseBills);
                FoodManager.RemoveOwnedBills(industryBills); Industry.Clear();
                if (!baseAutomation) ConstructionResourceManager.Stop(map, constructionGathering); }
            equipmentAutomation = enabled;
            CurrentState = ColonyStateScanner.Scan(map);
            EquipmentStatus = enabled && !ExpansionSuspended ? EquipmentManager.Apply(map, CurrentState, equipmentOrders, managedApparel, equipmentExcluded) : "Autoequipamento suspenso/desativado; equipamentos atuais mantidos.";
        }

        public bool EquipmentAllowedFor(Pawn pawn) => !equipmentExcluded.Contains(pawn);

        public void SetEquipmentAllowedFor(Pawn pawn, bool allowed)
        {
            if (allowed) equipmentExcluded.Remove(pawn);
            else if (!equipmentExcluded.Contains(pawn))
            {
                equipmentExcluded.Add(pawn);
                var orders = equipmentOrders.FindAll(o => o.Pawn == pawn);
                var apparel = managedApparel.FindAll(o => o.Pawn == pawn);
                EquipmentManager.Stop(orders, apparel);
                equipmentOrders.RemoveAll(o => o.Pawn == pawn);
                managedApparel.RemoveAll(o => o.Pawn == pawn);
            }
        }

        public void SetAutomation(bool food, bool work)
        {
            if (foodAutomation && !food) { FoodManager.CancelHunts(map, ownedHunts); FoodManager.RemoveOwnedBills(ownedBills); }
            if (workAutomation && !work) WorkPriorityManager.Restore(workChanges);
            if (!workAutomation && work) Find.PlaySettings.useWorkPriorities = true;
            foodAutomation = food;
            workAutomation = work;
            string transition = CurrentState?.Threat?.LastTransition;
            CurrentState = ColonyStateScanner.Scan(map);
            if (transition != null) CurrentState.Threat.LastTransition = transition;
            ManageColony();
        }

        public AutonomousRimMapComponent(Map map) : base(map)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (gatheringMineCells != null)
                foreach (var cell in gatheringMineCells)
                    if (cell.InBounds(map) && cell.GetEdifice(map) is Mineable rock && !constructionGathering.Contains(rock)) constructionGathering.Add(rock);
            CurrentState = ColonyStateScanner.Scan(map);
            progress.ResetSampling(Find.TickManager?.TicksGame ?? 0);
            observations.ResetSession(Find.TickManager?.TicksGame??0);
            observations.ColonyId=Current.Game.GetComponent<ObservationIdentity>().ColonyId;
            ObserveNow();
            bool studyReplay=PolicyStudyController.Replaying;
            PolicyStudyController.MapLoaded(map,this);
            if(!studyReplay && AutonomousRimMod.Instance?.Evidence?.ApplyValidatedPolicies==true)ApplyProvenPolicy();
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if(Find.TickManager!=null)Performance.Update(Find.TickManager.TicksGame,(int)Find.TickManager.CurTimeSpeed,Find.TickManager.Paused || LongEventHandler.AnyEventNowOrWaiting);
            if (strategyAutomation) ResearchNoticeManager.CloseOwnedCompletion(strategicPlan.OwnedResearch);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            int ticks = Find.TickManager?.TicksGame ?? 0;
            if (CurrentState != null && ticks > 0 && ticks % ThreatIntervalTicks == 0 &&
                (CurrentState.Threat.ActiveCount>0 || ticks%120==0 || map.mapPawns.AllPawnsSpawned.Any(ThreatScanner.Active)))RefreshThreat(ticks);
            if (EffectiveCombat && ticks > 0 && ticks % CombatManager.Interval == 0 &&
                (combatOrders.Count>0 || CurrentState.Threat.ActiveCount>0 || map.mapPawns.AllPawnsSpawned.Any(ThreatScanner.Active)))
                CombatStatus = CombatManager.Apply(map, combatOrders, combatExcluded,emergencyAutomation && emergency.Phase==EmergencyPhase.Danger && EmergencyManager.Retreat(CurrentState.Threat));
            else if(combatOrders.Count>0 && !EffectiveCombat)CombatManager.Stop(combatOrders);


            if (baseAutomation && ticks > 0 && ticks % BaseConstructionManager.ExecutionInterval == 0) ExecuteConstruction(ticks);
            if (CurrentState != null && ticks > 0 && ticks % ThreatIntervalTicks == 0)
            {
                if (equipmentAutomation) EquipmentManager.TrackOrders(equipmentOrders, managedApparel);
            }
            // Slow strategy is staggered and never gates approved construction jobs.
            if (ticks > 0 && ticks % 1200 == 113 && baseAutomation && !ExpansionSuspended) RefreshConstructionPlan(ticks);
            if (ticks > 0 && ticks % 1800 == 73) EvaluateStrategy();
            if (ticks > 0 && ticks % 1800 == 173) EvaluateFailure();
            if (ticks <= 0) return;
            if (ticks % 600 == 317) { ObserveNow(); ColonyProgressMonitor.Sample(map, this); AutomaticRecovery.Apply(map,this); }
            if(ticks%1200==397)GoalGraph.Update(map,this,dependencyBills);
            if(ticks%1200==427)Winter.Update(map,CurrentState);
            if(ticks%120==37)Doctrine.Update(map);
            if(ticks%30==7)LogisticsMonitor.Observe(map);
            if(ticks%2500==487){Opportunities.Update(map,this);Dlc.Update(map);}
            if(ticks%600==457)aftercare.Update(map,this);
            if (ticks % 600 == 347) equipmentDemands.Reconcile(map, this);
            if (ticks % ScanIntervalTicks == 0)
            {
                string lastTransition = CurrentState?.Threat?.LastTransition;
                CurrentState = ColonyStateScanner.Scan(map);
                if (lastTransition != null) CurrentState.Threat.LastTransition = lastTransition;
                if (Prefs.DevMode) Log.Message($"[AutonomousRim] Scan: {CurrentState}");
            }
            if (ticks % 30 == 0 && CurrentState != null) ManageColony(false);
            if(prisonAutomation && CurrentState!=null && ticks%60==45 &&
                (ticks%600==45 || prison.Orders.Count>0 || map.mapPawns.AllPawnsSpawned.Any(p=>p.IsPrisonerOfColony || p.Downed && p.HostileTo(Faction.OfPlayer))))
                PrisonManager.Apply(map,CurrentState,baseProjects,prison,baseAutomation);
            if(commerceAutomation && CurrentState!=null && ticks%600==210)
                CommerceManager.Apply(map,CurrentState,baseProjects,commerce,baseAutomation);
            if(CurrentState!=null && ticks%600==240 && (commerceAutomation || commerce.ExpeditionPawns.Count>0))
                CommerceCaravanManager.Apply(map,CurrentState,commerce,commerceAutomation);
            Trial.Tick(map,this);
        }

        private void ManageColony(bool force = true)
        {
            int cycle = Find.TickManager.TicksGame;
            // One fresh observation for all decisions in this economic cycle. Purely idle
            // cycles do not rescan. Every emitted job still revalidates its native target.
            bool decisionDue=force || workAutomation && cycle%600==0 || scheduleAutomation && cycle%300==30 ||
                foodAutomation && cycle%600==60 || equipmentAutomation && (cycle%600==120 || cycle%900==90) ||
                baseAutomation && (cycle%300==150 || cycle%600==240);
            if(decisionDue && (CurrentState==null || cycle-CurrentState.Tick>=180))ObserveNow(false);
            if(emergencyAutomation && emergency.BlockSecondary)
            {
                if (equipmentAutomation && (force || cycle % 600 == 120)) Industry.Update(map,this,industryBills);
                IndustryInfrastructure.Suspend(map,baseProjects);
                EmergencyManager.Apply(map,CurrentState,emergency);
                if(scheduleAutomation)ScheduleStatus=ScheduleManager.Apply(map,CurrentState,scheduleChanges,true);
                ManagementStatus="Emergência: trabalho secundário suspenso; recuperação libera medicina e alimentação antes da expansão.";
                if(emergency.Phase==EmergencyPhase.Recovery && !CurrentState.Threat.Immediate && !EmergencyManager.LocalFire(map) && foodAutomation && (force || cycle % 600 == 60))
                    ManagementStatus=FoodManager.Apply(map,CurrentState,ownedHunts,ownedBills,baseProjects,survivalOnly:true)+" Recuperação: produção secundária permanece suspensa.";
                return;
            }
            if (lootAutomation && CurrentState != null && Find.TickManager.TicksGame - lastLootTick >= LootIntervalTicks)
            {
                lastLootTick = Find.TickManager.TicksGame;
                LootStatus = LootAccessManager.Apply(map, CurrentState, baseAutomation, baseProjects, managedApparel, EquipmentAllowedFor, releasedLoot, out int released, (foodAutomation ? ownedBills : new List<ManagedFoodBill>()).Concat(equipmentAutomation ? defenseBills : new List<ManagedFoodBill>()).ToList());
                if (released > 0)
                {
                    string transition = CurrentState.Threat.LastTransition;
                    CurrentState = ColonyStateScanner.Scan(map);
                    CurrentState.Threat.LastTransition = transition;
                }
            }
            if (workAutomation && Find.PlaySettings.useWorkPriorities && (force || cycle % 600 == 0)) WorkPriorityManager.Apply(map, CurrentState, workChanges,
                baseAutomation && baseProjects.Exists(p => !p.Completed && BaseConstructionManager.Tasks(p).Any()), constructionGathering.Count > 0, Find.ResearchManager.GetProject() != null);
            if (scheduleAutomation && (force || cycle % 300 == 30)) ScheduleStatus = ScheduleManager.Apply(map, CurrentState, scheduleChanges, true);
            if (force || cycle % 600 == 60)
                ManagementStatus = foodAutomation ? FoodManager.Apply(map, CurrentState, ownedHunts, ownedBills, baseProjects) : "Comida / roupas automáticas desativadas.";
            if (workAutomation && (force || cycle % 600 == 60)) ManagementStatus += Find.PlaySettings.useWorkPriorities
                ? " Prioridades ativas; alterações manuais do jogador são preservadas."
                : " Prioridades pausadas: o modo numérico de trabalho foi desligado pelo jogador.";
            if (baseAutomation && (force || cycle % 300 == 150)) StorageStatus=StoragePolicy.ManageFoodStorage(map, CurrentState, baseProjects)+IndustryInfrastructure.Status(map,baseProjects);
            if (baseAutomation && (force || cycle % 1200 == 150)) AgriculturePlanner.Add(map,baseProjects);
            if (baseAutomation && (force || cycle % 600 == 150)) StoneProductionManager.Apply(map,CurrentState,baseProjects,infrastructureBills);
            if (equipmentAutomation && (force || cycle % 600 == 120)) DefenseProductionPlan.Apply(map, CurrentState, defenseBills);
            if (equipmentAutomation && (force || cycle % 600 == 120)) Industry.Update(map,this,industryBills);
            if (force || cycle % 600 == 120) IndustryInfrastructure.Update(map,this,baseProjects);
            // Industry must gather inputs even when base automation has no projects or is off.
            if ((baseAutomation || equipmentAutomation || GoalGraph.HasDemand) && (force || cycle % 600 == 240))
                GatheringStatus = ConstructionResourceManager.Apply(map, baseAutomation ? baseProjects : new List<RoomProject>(), constructionGathering,
                    ManagedProduction.ToList(), GatheringTargets());
            if (equipmentAutomation && (force || cycle % 900 == 90)) EquipmentStatus = EquipmentManager.Apply(map, CurrentState, equipmentOrders, managedApparel, equipmentExcluded);
            if (force && baseAutomation) ExecuteConstruction(cycle);
        }

        private Dictionary<ThingDef,int> GatheringTargets()
        {
            var result=equipmentAutomation ? new Dictionary<ThingDef,int>(Industry.Targets) : new Dictionary<ThingDef,int>();
            foreach(var pair in GoalGraph.Supplies)result[pair.Key]=Math.Max(result.TryGetValue(pair.Key,out int old)?old:0,pair.Value);
            if(baseAutomation && Winter.DaysUntilCold>=0 && Winter.DaysUntilCold<=15)result[ThingDefOf.WoodLog]=Math.Max(result.TryGetValue(ThingDefOf.WoodLog,out int wood)?wood:0,Winter.FuelRequired);
            return result;
        }

        private void RefreshConstructionPlan(int ticks)
        {
            bool climate = DefDatabase<ResearchProjectDef>.GetNamed("AirConditioning").IsFinished;
            if (baseProjects.Count == 0 && ticks - lastPlanTick >= 1200 || plannedPawnCount != map.mapPawns.FreeColonistsSpawnedCount || plannedClimateResearch != climate ||
                baseProjects.Exists(p => p.Kind == RingBasePlanner.ReservationKind || p.Kind == ModularBasePlanner.ReservationKind) && ticks - lastPlanTick >= 1200)
                PreviewBase();
        }

        private void ExecuteConstruction(int ticks)
        {
            if(ExpansionSuspended) { BaseStatus="Construção/decoração suspensas durante emergência e recuperação."; return; }
            if (lastConstructionTick == ticks) return;
            lastConstructionTick = ticks;
            if (baseProjects.Count == 0) return;
            if (ticks - lastConstructionReview >= BaseConstructionManager.ReviewInterval)
            {
                lastConstructionReview = ticks;
                BaseStatus = BaseConstructionManager.Apply(map, baseProjects);WorkIndex.Invalidate();
            }
            ConstructionWorkManager.Apply(map, baseProjects, constructionOrders);
        }

        public void NotifyConstructionJobEnding(Pawn pawn, Verse.AI.Job job, Verse.AI.JobCondition condition) =>
            ConstructionWorkManager.NotifyEnding(pawn, job, condition, baseProjects, constructionOrders);

        private void RefreshThreat(int ticks)
        {
            ThreatState previous = CurrentState.Threat;
            ThreatState next = ThreatScanner.Scan(map, CurrentState);
            next.LastTransition = previous.LastTransition;
            if ((observedThreatCount == 0) != (next.ActiveCount == 0))
            {
                next.LastTransition = next.ActiveCount > 0
                    ? $"Tick {ticks}: hostile presence detected."
                    : $"Tick {ticks}: no active hostile pawns remain.";
                Log.Message($"[AutonomousRim] Map {map.uniqueID}: {next.LastTransition}");
            }
            CurrentState.Threat = next;
            CurrentState.HostilePawnCount = next.ActiveCount;
            var previousPhase=emergency.Phase;
            emergency.Update(next.Immediate || EmergencyManager.CriticalFire(map),EmergencyManager.NeedsCare(map),ticks);
            if(previousPhase!=emergency.Phase) {
                Log.Message($"[AutonomousRim.Emergency] {previousPhase} → {emergency.Phase}; {next.Classification}");
                progress.Record(ticks,previousPhase+" → "+emergency.Phase+"; "+next.Classification+"; "+emergency.Decision,"Emergência","threat");
            }
            if(emergencyAutomation)
            {
                if(emergency.BlockSecondary)
                {
                    if(commerceAutomation)CommerceManager.Suspend(commerce);
                    if(emergency.Phase!=EmergencyPhase.Recovery || next.Immediate || EmergencyManager.LocalFire(map))FoodManager.CancelHunts(map,ownedHunts);
                    EquipmentManager.CancelPending(equipmentOrders);
                    ConstructionWorkManager.Stop(constructionOrders);
                    EmergencyManager.Apply(map,CurrentState,emergency);
                    if(scheduleAutomation)ScheduleStatus=ScheduleManager.Apply(map,CurrentState,scheduleChanges,true);
                }
                else EmergencyManager.Apply(map,CurrentState,emergency);
            }
            if (observedThreatCount == 0 && next.ActiveCount > 0) FailureAnalyzer.RecordRaidStart(map, CurrentState, failureMemory);
            if (observedThreatCount > 0 && next.ActiveCount == 0) {FailureAnalyzer.RecordRaidEnd(CurrentState, failureMemory);aftercare.Begin(map);}
            observedThreatCount=next.ActiveCount;
            if (foodAutomation && next.ActiveCount > 0) FoodManager.CancelHunts(map, ownedHunts);
            if (equipmentAutomation && next.ActiveCount > 0) EquipmentManager.CancelPending(equipmentOrders);
        }
    }
}
