using System;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class EmergencyChecks : MapComponent
    {
        private bool finished, initialized, rescued, renewed;
        private int clearTick;
        private Pawn helper, victim;
        private Pawn enemy;
        private Hediff_Injury careInjury;
        private bool careVerified;
        private IntVec3 center;
        private int researchOriginal;
        private void Check(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
        public EmergencyChecks(Map map):base(map){}
        public override void MapComponentUpdate()
        {
            if(finished || !GenCommandLine.CommandLineArgPassed("autonomousrimemergencytest"))return;
            foreach(var w in Find.WindowStack.Windows.Where(w=>w.forcePause).ToList())w.Close(false);
            Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
        }
        private Pawn Spawn(string kind,Faction faction,IntVec3 cell)
        {
            var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed(kind),faction,forceGenerateNewPawn:true,canGeneratePawnRelations:false));
            if(faction==Faction.OfPlayer)
            {
                while(p.WorkTagIsDisabled(WorkTags.Caring) || p.WorkTagIsDisabled(WorkTags.Hauling) || p.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) || p.WorkTypeIsDisabled(WorkTypeDefOf.Cleaning))
                    p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,faction,forceGenerateNewPawn:true,canGeneratePawnRelations:false));
                foreach(var old in p.equipment.AllEquipmentListForReading.ToList())p.equipment.Remove(old);
            }
            GenSpawn.Spawn(p,cell,map); return p;
        }
        private void Detection(string kind,int category)
        {
            var ai=map.GetComponent<AutonomousRimMapComponent>(); ai.SetEmergencyAutomation(false);
            var p=Spawn(kind,category==1?null:Faction.OfAncientsHostile,center+new IntVec3(35,0,0));
            if(category==1)Check(p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent),"Manhunter state setup failed.");
            if(category==0)
            {
                foreach(var item in p.equipment.AllEquipmentListForReading.ToList())p.equipment.Remove(item);
                p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle")));
            }
            var threat=ThreatScanner.Scan(map,ColonyStateScanner.Scan(map));
            Check(threat.Sources.Contains(p) && threat.ActiveCount>0,"Missed active "+kind);
            Check(category==0?threat.Humanlikes>0:category==1?threat.Manhunters>0:category==2?threat.Mechanoids>0:threat.Insects>0,"Wrong classification: "+kind);
            if(category==0)Check(!EmergencyManager.Safe(map,p.Position+new IntVec3(-28,0,0),threat),"Long-range hostile weapon exposure ignored.");
            ai.SetEmergencyAutomation(true);
            Check(ai.Emergency.Phase==EmergencyPhase.Danger && ai.SecondarySuspended && ai.ExpansionSuspended,"Threat failed to activate global gates.");
            Check(helper.workSettings.GetPriority(WorkTypeDefOf.Research)==0 && helper.workSettings.GetPriority(WorkTypeDefOf.Hauling)==0,"Secondary work was not suspended.");
            var apparelJob=AccessTools.Method(typeof(JobGiver_OptimizeApparel),"TryGiveJob").Invoke(new JobGiver_OptimizeApparel(),new object[]{helper});
            Check(apparelJob==null,"Native apparel optimization was not gated.");
            Log.Message("[AutonomousRim.EmergencyTests] PASS detection: "+kind+"; "+threat.Classification);
            p.Destroy(DestroyMode.Vanish);
        }
        private void Setup()
        {
            var ai=map.GetComponent<AutonomousRimMapComponent>(); ai.DisableAll(); center=map.Center;
            foreach(var p in map.mapPawns.AllPawnsSpawned.ToList()) { p.DeSpawn(); Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever); }
            foreach(var t in map.listerThings.AllThings.Where(t=>t is Hive || !(t is Pawn) && t is IAttackTarget && t.HostileTo(Faction.OfPlayer)).ToList())t.Destroy(DestroyMode.Vanish);
            foreach(var cell in new CellRect(center.x-40,center.z-20,81,41).Cells)
            {
                foreach(var t in cell.GetThingList(map).ToList())t.Destroy(DestroyMode.Vanish);
                map.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil); map.roofGrid.SetRoof(cell,null); map.fogGrid.Unfog(cell);
            }
            helper=Spawn("Colonist",Faction.OfPlayer,center+new IntVec3(-24,0,0));
            victim=Spawn("Colonist",Faction.OfPlayer,center+new IntVec3(-26,0,0));
            helper.workSettings.EnableAndInitializeIfNotAlreadyInitialized(); helper.workSettings.SetPriority(WorkTypeDefOf.Research,3);
            researchOriginal=helper.workSettings.GetPriority(WorkTypeDefOf.Research);
            Detection("Colonist",0); Detection("Wolf_Timber",1); Detection("Mech_Scyther",2); Detection("Megaspider",3);
            ai.SetEmergencyAutomation(false);
            var hive=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Hive")); hive.SetFaction(Faction.OfInsects);
            GenSpawn.Spawn(hive,center+new IntVec3(35,0,0),map);
            ai.SetEmergencyAutomation(true);
            Check(ai.CurrentState.Threat.Structures>0 && ai.Emergency.Phase==EmergencyPhase.Danger,"Hive without active pawns was missed.");
            Log.Message("[AutonomousRim.EmergencyTests] PASS detection: hive structure without hostile pawns"); hive.Destroy(DestroyMode.Vanish);
            ai.SetEmergencyAutomation(false);
            var deer=Spawn("Deer",null,center+new IntVec3(20,0,0));
            Check(ThreatScanner.Scan(map,ColonyStateScanner.Scan(map)).ActiveCount==0,"Peaceful wildlife triggered danger."); deer.Destroy(DestroyMode.Vanish);
            Log.Message("[AutonomousRim.EmergencyTests] PASS detection: peaceful wildlife ignored");
            var bed=(Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed,ThingDefOf.WoodLog); bed.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(bed,center+new IntVec3(-20,0,0),map); bed.Medical=true;
            victim.health.AddHediff(HediffDefOf.Anesthetic); Check(victim.Downed,"Rescue fixture must be incapacitated.");
            enemy=Spawn("Colonist",Faction.OfAncientsHostile,center+new IntVec3(35,0,0));
            ai.SetEmergencyAutomation(true);
            Check(helper.CurJob?.def==JobDefOf.Rescue,"Safe native rescue was not dispatched.");
            initialized=true;
        }
        public override void MapComponentTick()
        {
            if(finished || !GenCommandLine.CommandLineArgPassed("autonomousrimemergencytest") || Find.TickManager.TicksGame<300)return;
            try
            {
                if(!initialized) { Setup(); return; }
                int tick=Find.TickManager.TicksGame;
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(!rescued)
                {
                    if(!victim.InBed()) { if(tick>3000)throw new InvalidOperationException("Native rescue did not reach the medical bed."); return; }
                    rescued=true; Log.Message("[AutonomousRim.EmergencyTests] PASS rescue: incapacitated colonist carried into medical bed by native job");
                    victim.health.RemoveHediff(victim.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Anesthetic));
                    var dangerCell=enemy.Position+new IntVec3(-2,0,0);
                    Check(!EmergencyManager.Safe(map,dangerCell,ai.CurrentState.Threat) && !EmergencyManager.Route(helper,dangerCell,ai.CurrentState.Threat),"Unsafe rescue approach was accepted.");
                    Log.Message("[AutonomousRim.EmergencyTests] PASS safety: exposed rescue route rejected");
                    enemy.Destroy(DestroyMode.Vanish); clearTick=tick;
                    helper.workSettings.SetPriority(WorkTypeDefOf.Mining,1); // Explicit manual override during incident.
                }
                if(!renewed && ai.Emergency.Phase==EmergencyPhase.Recovery)
                {
                    Check(helper.workSettings.GetPriority(WorkTypeDefOf.Hauling)>0 && helper.workSettings.GetPriority(WorkTypeDefOf.Research)==0,"Recovery did not restore work gradually: haul="+helper.workSettings.GetPriority(WorkTypeDefOf.Hauling)+" research="+helper.workSettings.GetPriority(WorkTypeDefOf.Research)+" changes="+string.Join(" | ",ai.Emergency.Work.Where(c=>c.Pawn==helper).Select(c=>c.Work.defName+":"+c.Original+"/"+c.Applied+"/manual="+c.UserOverride)));
                    enemy=Spawn("Colonist",Faction.OfAncientsHostile,center+new IntVec3(35,0,0));
                    ai.SetEmergencyAutomation(true);
                    Check(ai.Emergency.Phase==EmergencyPhase.Danger,"New threat did not interrupt recovery."); enemy.Destroy(DestroyMode.Vanish);
                    clearTick=tick; renewed=true;
                    Log.Message("[AutonomousRim.EmergencyTests] PASS recovery: staged work and re-entry on renewed threat");
                }
                if(renewed && !careVerified && tick-clearTick>=3300 && careInjury==null)
                {
                    careInjury=(Hediff_Injury)HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Bruise"),helper,helper.RaceProps.body.corePart);
                    careInjury.Severity=3; helper.health.AddHediff(careInjury);
                    Check(EmergencyManager.NeedsCare(map),"Temporary injury did not prolong recovery.");
                }
                if(careInjury!=null && tick-clearTick>=3900)
                {
                    Check(ai.Emergency.Phase==EmergencyPhase.Recovery,"Recovery ended while temporary injury remained.");
                    helper.health.RemoveHediff(careInjury); careInjury=null; careVerified=true;
                    Log.Message("[AutonomousRim.EmergencyTests] PASS health: recovery retained past minimum duration for a temporary injury");
                }
                if(renewed && ai.Emergency.Phase==EmergencyPhase.Normal)
                {
                    Check(tick-clearTick>=3600,"Emergency released without safety confirmation/recovery delay.");
                    Check(helper.workSettings.GetPriority(WorkTypeDefOf.Research)==researchOriginal,"Original work priority was not restored.");
                    Check(WorkPriorityManager.RawPriority(helper,WorkTypeDefOf.Mining)==1,"Manual priority override lost.");
                    ai.SetEmergencyAutomation(false); Check(!helper.Drafted,"Emergency toggle retained AI draft.");
                    GameDataSaveLoader.SaveGame("AutonomousRim-Emergencia-Verificada");
                    Log.Message("[AutonomousRim.EmergencyTests] PASS restoration: originals restored, manual edit preserved, AI controls released");
                    Log.Message("[AutonomousRim.EmergencyTests] DONE"); finished=true;
                }
                if(tick-clearTick>15000)throw new InvalidOperationException("Emergency recovery timeout: "+ai.EmergencyStatus);
            }
            catch(Exception ex) { finished=true; Log.Error("[AutonomousRim.EmergencyTests] FAIL "+ex); }
        }
    }
}
