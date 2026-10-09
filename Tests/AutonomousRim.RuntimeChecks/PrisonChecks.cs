using System;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    // Controlled fixtures validate native jobs. They are not a natural-economy trial.
    public sealed class PrisonChecks : MapComponent
    {
        private static int stage;
        private int start;
        private Pawn recruit,release;
        private bool capturedReported,tendedReported,convertedReported;
        private bool diagnosed;
        public PrisonChecks(Map map):base(map){}
        private void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);}
        private void Pass(string text)=>Log.Message("[PrisonTests] PASS: "+text);
        private Thing SpawnThing(string name,IntVec3 cell,int count=1)
        {
            var def=DefDatabase<ThingDef>.GetNamed(name);
            if(count>def.stackLimit)
            {
                Thing first=null;
                while(count>0)
                {
                    var spot=GenRadial.RadialCellsAround(cell,10,true).First(c=>c.InBounds(map) && c.Standable(map) && !c.GetThingList(map).Any());
                    int batch=Math.Min(count,def.stackLimit);var item=SpawnThing(name,spot,batch);if(first==null)first=item;count-=batch;
                }
                return first;
            }
            var thing=ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.WoodLog:null);thing.stackCount=count;
            if(thing.def.category==ThingCategory.Building)thing.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(thing,cell,map);thing.SetForbidden(false,false);return thing;
        }
        private Pawn SpawnPawn(Faction faction,IntVec3 cell)
        {
            Pawn pawn;
            do{pawn=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,faction,forceGenerateNewPawn:true,canGeneratePawnRelations:false));}
            while(pawn.health.hediffSet.hediffs.Count>0 || pawn.skills.skills.Any(s=>s.TotallyDisabled) ||
                faction==Faction.OfPlayer && DefDatabase<WorkTypeDef>.AllDefsListForReading.Any(pawn.WorkTypeIsDisabled));
            foreach(var trait in pawn.story.traits.allTraits.ToList())pawn.story.traits.RemoveTrait(trait);
            foreach(var skill in pawn.skills.skills)skill.Level=faction==Faction.OfPlayer?20:8;
            foreach(var weapon in pawn.equipment.AllEquipmentListForReading.ToList())pawn.equipment.Remove(weapon);
            GenSpawn.Spawn(pawn,cell,map);
            if(pawn.needs.food!=null)pawn.needs.food.CurLevel=1;if(pawn.needs.rest!=null)pawn.needs.rest.CurLevel=1;
            if(pawn.needs.joy!=null)pawn.needs.joy.CurLevel=1;if(pawn.needs.mood!=null)pawn.needs.mood.CurLevel=1;
            pawn.workSettings?.EnableAndInitializeIfNotAlreadyInitialized();
            if(faction==Faction.OfPlayer)
                foreach(var work in DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w=>!pawn.WorkTypeIsDisabled(w)))pawn.workSettings.SetPriority(work,3);
            return pawn;
        }
        private Building_Bed Room(IntVec3 origin,bool prisoner,bool medical)
        {
            var rect=new CellRect(origin.x,origin.z,6,6);
            foreach(var cell in rect)
            {
                if(rect.IsOnEdge(cell))SpawnThing(cell==origin+new IntVec3(3,0,0)?"Door":"Wall",cell);
                else map.terrainGrid.SetTerrain(cell,DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor"));
                map.roofGrid.SetRoof(cell,RoofDefOf.RoofConstructed);
            }
            var bed=(Building_Bed)SpawnThing("Bed",origin+new IntVec3(1,0,3));
            SpawnThing("Table1x2c",origin+new IntVec3(3,0,2));SpawnThing("DiningChair",origin+new IntVec3(2,0,2));
            if(prisoner)Check(PrisonManager.ConfigureBed(bed),"native prison-cell configuration");
            bed.Medical=medical;return bed;
        }
        private void Wound(Pawn pawn)
        {
            pawn.health.AddHediff(HediffDefOf.Anesthetic);
            var injury=HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Cut"),pawn,pawn.health.hediffSet.GetNotMissingParts().First(p=>p.def==BodyPartDefOf.Leg));
            injury.Severity=3;pawn.health.AddHediff(injury);
        }
        private void SafetySetupChecks()
        {
            // Initial controlled policy fixtures, before native capture begins.
            var residents=map.mapPawns.FreeColonistsSpawned.ToList();
            var state=new PrisonState();var projects=new System.Collections.Generic.List<AutonomousRim.Planning.RoomProject>();
            foreach(var pawn in residents)pawn.drafter.Drafted=true;
            PrisonManager.Apply(map,ColonyStateScanner.Scan(map),projects,state,false);
            Check(state.Orders.Count==0 && residents.All(p=>p.Drafted),"Capture overrode player drafts.");
            foreach(var pawn in residents)pawn.drafter.Drafted=false;
            Pass("safety: drafted helpers preserved; no capture order issued");
            var blocker=SpawnPawn(Faction.OfAncientsHostile,map.Center+new IntVec3(5,0,-12));
            var danger=ColonyStateScanner.Scan(map);Check(danger.Threat.Immediate,"Safety fixture failed to create an immediate threat.");
            state.NextAction=0;PrisonManager.Apply(map,danger,projects,state,false);
            Check(state.Orders.Count==0,"Capture started during an immediate threat.");
            blocker.DeSpawn();Find.WorldPawns.PassToWorld(blocker,PawnDiscardDecideMode.KeepForever);
            Pass("safety: immediate hostile threat suspends captures");
            var ownPatient=residents[0];var originalHediffs=ownPatient.health.hediffSet.hediffs.ToList();Wound(ownPatient);
            state.NextAction=0;PrisonManager.Apply(map,ColonyStateScanner.Scan(map),projects,state,false);
            Check(state.Orders.Count==0,"Enemy capture displaced care of a bleeding/downed colonist.");
            foreach(var fixtureHediff in ownPatient.health.hediffSet.hediffs.Except(originalHediffs).ToList())ownPatient.health.RemoveHediff(fixtureHediff);
            Pass("safety: own bleeding/downed colonist takes precedence over capture");
            var beds=map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Where(b=>b.ForPrisoners).ToList();
            foreach(var bed in beds){bed.ForOwnerType=BedOwnerType.Colonist;bed.GetDistrict()?.Notify_RoomShapeOrContainedBedsChanged();bed.GetRoom()?.Notify_RoomShapeChanged();}
            state.NextAction=0;PrisonManager.Apply(map,ColonyStateScanner.Scan(map),projects,state,false);
            Check(state.Capacity==0 && state.Orders.Count==0,"Capture issued without a valid prison bed.");
            foreach(var bed in beds)Check(PrisonManager.ConfigureBed(bed),"Prison bed fixture did not restore.");
            Pass("safety: no valid prison bed means no capture");
            var shortage=ColonyStateScanner.Scan(map);shortage.EstimatedFoodDays=1.5f;
            state.NextAction=0;PrisonManager.Apply(map,shortage,projects,state,false);
            Check(state.Orders.Count==0,"Capture ignored the two-day food reserve gate.");
            Pass("safety: low reserve blocks new prisoners (controlled reserve input)");
            foreach(var patient in new[]{recruit,release})state.Prisoners.Add(new PrisonerRecord{Pawn=patient,Owned=true,Completed=true,Manual=true});
            state.NextAction=0;PrisonManager.Apply(map,ColonyStateScanner.Scan(map),projects,state,false);
            Check(state.Orders.Count==0 && state.Prisoners.All(r=>r.Manual && r.Completed),"Completed manual prisoner records were overwritten or recaptured.");
            Pass("safety: completed manual prisoner records remain protected");
            map.GetComponent<AutonomousRimMapComponent>().Prison.Prisoners.Add(new PrisonerRecord{Pawn=recruit,Owned=true,Completed=true,
                OriginalFaction=recruit.Faction,Destination=PrisonerDestination.Recruit,Status="Controlled earlier escape record"});
            Pass("recapture setup: same hostile pawn has a completed automatic record; native capture must replace it without duplication");
        }
        private void Setup()
        {
            var ai=map.GetComponent<AutonomousRimMapComponent>();ai.DisableAll();
            var peaceful=DefDatabase<DifficultyDef>.GetNamed("Peaceful");Find.Storyteller.difficultyDef=peaceful;Find.Storyteller.difficulty.CopyFrom(peaceful);
            Find.Storyteller.difficulty.unwaveringPrisoners=false;
            foreach(var pawn in map.mapPawns.AllPawnsSpawned.ToList()){pawn.DeSpawn();Find.WorldPawns.PassToWorld(pawn,PawnDiscardDecideMode.KeepForever);}
            foreach(var t in map.listerThings.AllThings.Where(t=>t is Hive || !(t is Pawn) && t is IAttackTarget && t.HostileTo(Faction.OfPlayer)).ToList())t.Destroy(DestroyMode.Vanish);
            var center=map.Center;
            map.Biome.constantOutdoorTemperature=21;
            foreach(var cell in new CellRect(center.x-35,center.z-25,71,51))
            {
                foreach(var t in cell.GetThingList(map).ToList()){if(t.def.destroyable)t.Destroy(DestroyMode.Vanish);else t.DeSpawn();}
                map.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);map.roofGrid.SetRoof(cell,null);map.fogGrid.Unfog(cell);
            }
            for(int i=0;i<5;i++)SpawnPawn(Faction.OfPlayer,center+new IntVec3(i*2,0,-12));
            for(int i=0;i<7;i++)Room(center+new IntVec3(-25+i*7,0,8),false,false);
            Room(center+new IntVec3(-15,0,-4),true,true);Room(center+new IntVec3(-8,0,-4),true,false);
            SpawnThing("MealSimple",center+new IntVec3(1,0,-10),75);SpawnThing("MealSimple",center+new IntVec3(3,0,-10),75);
            SpawnThing("MedicineHerbal",center+new IntVec3(-10,0,-10),30);
            recruit=SpawnPawn(Find.FactionManager.AllFactions.FirstOrDefault(f=>!f.IsPlayer && f.HostileTo(Faction.OfPlayer) && f.ideos?.PrimaryIdeo!=null)??Faction.OfAncientsHostile,center+new IntVec3(-10,0,-15));
            release=SpawnPawn(Faction.OfAncientsHostile,center+new IntVec3(-5,0,-15));
            foreach(var skill in recruit.skills.skills)skill.Level=20;
            recruit.guest.Recruitable=true;
            foreach(var skill in release.skills.skills){skill.Level=0;skill.passion=Passion.None;}
            Check(AutonomousRim.Planning.PrisonPlanner.CandidateScore(map,recruit)>=8 && AutonomousRim.Planning.PrisonPlanner.CandidateScore(map,release)<8,"Controlled candidate scores incorrect.");
            Wound(recruit);Wound(release);
            if(GenCommandLine.CommandLineArgPassed("autonomousrimprisonsafetytest"))SafetySetupChecks();
            ai.SetAutomation(false,false);ai.SetPrisonAutomation(true);start=Find.TickManager.TicksGame;stage=1;
            Pass("fixture: five healthy skill-20 colonists, separate prison/hospital beds and native enemy eligibility");
        }
        public override void MapComponentTick()
        {
            if(stage==99 || !(GenCommandLine.CommandLineArgPassed("autonomousrimprisontest") || GenCommandLine.CommandLineArgPassed("autonomousrimprisonsafetytest")) || Find.TickManager.TicksGame<300)return;
            try
            {
                if(stage==0){Setup();return;}
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(stage==3)
                {
                    Check(ai.PrisonAutomation && ai.Prison.Prisoners.Count>=2,"Prison state or toggle lost after native save/load.");
                    Check(ai.Prison.Prisoners.Any(r=>r.Completed && r.Pawn?.Faction==Faction.OfPlayer),"Recruited record lost after save/load.");
                    ai.SetPrisonAutomation(false);Check(ai.Prison.Orders.Count==0,"Prison helpers not released on disable.");
                    Pass("native save/load retains records and toggle; disable releases helpers");
                    stage=99;Log.Message("[PrisonTests] DONE");return;
                }
                if(recruit==null)recruit=ai.Prison.Prisoners.FirstOrDefault(r=>r.Destination==PrisonerDestination.Recruit)?.Pawn;
                if(release==null)release=ai.Prison.Prisoners.FirstOrDefault(r=>r.Destination==PrisonerDestination.TreatAndRelease)?.Pawn;
                Check(recruit!=null && release!=null,"Fixture patients not tracked.");
                Check(!recruit.Dead && !release.Dead,"Prison patient died.");
                Check(!capturedReported || recruit.MapHeld==map || recruit.Faction==Faction.OfPlayer,"Recruit candidate escaped from the map before recruitment; inspect failure save.");
                if(!diagnosed && Find.TickManager.TicksGame-start>900 && !capturedReported)
                {
                    diagnosed=true;
                    foreach(var h in map.mapPawns.FreeColonistsSpawned.ToList())
                    {
                        var bed=RestUtility.FindBedFor(recruit,h,false,false,GuestStatus.Prisoner);
                        bool available=(bool)HarmonyLib.AccessTools.Method(typeof(PrisonManager),"Available").Invoke(null,new object[]{h,ai.Prison});
                        float route=bed==null?-1:(float)HarmonyLib.AccessTools.Method(typeof(PrisonManager),"Route").Invoke(null,new object[]{h,recruit.Position,bed.Position,ai.CurrentState});
                        Log.Message($"[PrisonTests] diagnosis {h.LabelShort}: available={available}; capturable={recruit.CanBeCaptured()}; hostile={recruit.HostileTo(Faction.OfPlayer)}; reserve={h.CanReserve(recruit)}; rescue={HealthAIUtility.CanRescueNow(h,recruit,true)}; bed={bed}; route={route}; days={ai.CurrentState.EstimatedFoodDays}; job={h.CurJob?.def.defName}; ownCare={HarmonyLib.AccessTools.Method(typeof(PrisonManager),"OwnPatients").Invoke(null,new object[]{map})}");
                        foreach(var b in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Where(b=>b.ForPrisoners).ToList())
                            Log.Message($"[PrisonTests] bed {b.Position}: valid={RestUtility.IsValidBedFor(b,recruit,h,false,false,false,GuestStatus.Prisoner)}; now={RestUtility.CanUseBedNow(b,recruit,false,false,GuestStatus.Prisoner)}; prisonCell={b.Position.IsInPrisonCell(map)}; role={b.GetRoom()?.Role?.defName}; ideologyForbids={b.CompAssignableToPawn.IdeoligionForbids(recruit)}; reach={h.CanReach(b,PathEndMode.OnCell,Danger.Some)}; medCare={HealthAIUtility.ShouldEverReceiveMedicalCareFromPlayer(recruit)}");
                    }
                }
                if(!capturedReported && recruit.IsPrisonerOfColony && release.IsPrisonerOfColony && recruit.InBed() && release.InBed())
                {
                    var r=ai.Prison.Prisoners.Single(x=>x.Pawn==recruit);var other=ai.Prison.Prisoners.Single(x=>x.Pawn==release);
                    Check(r.Destination==PrisonerDestination.Recruit && other.Destination==PrisonerDestination.TreatAndRelease,"Candidate destination incorrect.");
                    Check(map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Count(b=>!b.ForPrisoners)>=7,"Colonist beds converted into prison beds.");
                    var food=ColonyStateScanner.Scan(map);Check(food.PrisonerCount==2 && food.DailyFoodNutrition>0,"Prisoner demand missing.");
                    recruit.guest.resistance=.01f;if(recruit.ideo!=null)HarmonyLib.AccessTools.Field(typeof(Pawn_IdeoTracker),"certaintyInt").SetValue(recruit.ideo,.001f);
                    capturedReported=true;Pass("two native capture jobs finish in separate beds; recruitment/release selection and food demand");
                }
                if(capturedReported && !tendedReported && !recruit.health.HasHediffsNeedingTend() && !release.health.HasHediffsNeedingTend())
                {tendedReported=true;Pass("native doctor treatment stops untreated injuries and bleeding");}
                if(tendedReported && !convertedReported && ModsConfig.IdeologyActive && recruit.Ideo==Faction.OfPlayer.ideos.PrimaryIdeo)
                {convertedReported=true;Pass("native warden conversion reaches colony ideology");}
                if(capturedReported && recruit.Faction==Faction.OfPlayer && !recruit.IsPrisoner && !release.IsPrisonerOfColony &&
                    ai.Prison.Prisoners.Any(r=>r.Pawn==recruit && r.Completed))
                {
                    Check(tendedReported && (!ModsConfig.IdeologyActive || convertedReported),"Recruitment finished without the required treatment/conversion evidence.");
                    Pass("native recruitment joins colony; recovered second patient released by native job");
                    stage=3;GameDataSaveLoader.SaveGame("PrisonRoundtrip");GameDataSaveLoader.LoadGame("PrisonRoundtrip");return;
                }
                if(Find.TickManager.TicksGame%6000==0)
                    Log.Message($"[PrisonTests] progress tick={Find.TickManager.TicksGame-start}: {ai.Prison.Status}; recruit={recruit.guest?.ExclusiveInteractionMode?.defName}/{recruit.CurJob?.def.defName}; release={release.guest?.ExclusiveInteractionMode?.defName}/{release.CurJob?.def.defName}");
                Check(Find.TickManager.TicksGame-start<300000,"Prison scenario exceeded five game days without completing: "+ai.Prison.Status);
            }
            catch(Exception ex){GameDataSaveLoader.SaveGame("PrisonFailure");stage=99;Log.Error("[PrisonTests] FAIL: "+ex);}
        }
    }
}
