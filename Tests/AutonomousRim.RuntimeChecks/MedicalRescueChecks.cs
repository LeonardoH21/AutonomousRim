using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class MedicalRescueChecks : MapComponent
    {
        private bool done,started,careReported;
        private int stage,start,careFinished;
        private Pawn doctor,patient;
        private Pawn[] fighters,enemies;
        private IntVec3 center;
        private bool Elite=>GenCommandLine.CommandLineArgPassed("autonomousrimelitesix");
        private List<Pawn> eliteRoster=new List<Pawn>();
        private int eliteSpawnIndex;
        public MedicalRescueChecks(Map map):base(map){}
        private void Check(bool c,string m){if(!c)throw new InvalidOperationException(m);}
        private ThingWithComps Item(string name)
        {
            var def=DefDatabase<ThingDef>.GetNamed(name);
            var t=(ThingWithComps)ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.Steel:null);
            t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Colony);return t;
        }
        private Pawn Spawn(bool friendly,IntVec3 pos,string weapon,int medical)
        {
            Pawn p;
            if(friendly && Elite)p=eliteRoster[eliteSpawnIndex++];
            else
            {
            do { p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,friendly?Faction.OfPlayer:Faction.OfAncientsHostile,forceGenerateNewPawn:true,canGeneratePawnRelations:false)); }
            while(p.health.hediffSet.hediffs.Count>0 || p.skills.skills.Any(s=>s.TotallyDisabled));
            foreach(var trait in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(trait);
            foreach(var skill in p.skills.skills)skill.Level=friendly?20:6;
            p.skills.GetSkill(SkillDefOf.Medicine).Level=medical;
            }
            foreach(var t in p.equipment.AllEquipmentListForReading.ToList())p.equipment.Remove(t);
            if(weapon!=null)p.equipment.AddEquipment(Item(weapon));
            foreach(var t in p.apparel.WornApparel.ToList())p.apparel.Remove(t);
            p.apparel.Wear((Apparel)Item("Apparel_PowerArmor"));
            p.apparel.Wear((Apparel)Item("Apparel_SimpleHelmet"));
            GenSpawn.Spawn(p,pos,map);
            if(!(friendly && Elite))
            {
                if(p.needs.food!=null)p.needs.food.CurLevel=1;if(p.needs.rest!=null)p.needs.rest.CurLevel=1;
                if(p.needs.joy!=null)p.needs.joy.CurLevel=1;if(p.needs.mood!=null)p.needs.mood.CurLevel=1;
            }
            return p;
        }
        private void Build(string name,int x,int z)
        {
            var def=DefDatabase<ThingDef>.GetNamed(name);var t=ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.WoodLog:null);
            t.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(t,center+new IntVec3(x,0,z),map);
            if(t is Building_Bed b)b.Medical=true;
        }
        private void Setup()
        {
            var ai=map.GetComponent<AutonomousRimMapComponent>();ai.DisableAll();center=map.Center;
            if(Elite)
            {
                var originals=EliteFixtureBaseline.Roster(map);
                eliteRoster=new[]{"Alpha","Delta","Bravo","Foxtrot","Charlie","Echo"}.Select(n=>originals.Single(p=>((NameTriple)p.Name).Nick==n)).ToList();
                eliteSpawnIndex=0;
            }
            else {var peaceful=DefDatabase<DifficultyDef>.GetNamed("Peaceful");Find.Storyteller.difficultyDef=peaceful;Find.Storyteller.difficulty.CopyFrom(peaceful);}
            foreach(var p in map.mapPawns.AllPawnsSpawned.ToList()){p.DeSpawn();if(!eliteRoster.Contains(p))Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);}
            foreach(var t in map.listerThings.AllThings.Where(t=>t is Hive || !(t is Pawn)&&t is IAttackTarget&&t.HostileTo(Faction.OfPlayer)).ToList())t.Destroy(DestroyMode.Vanish);
            foreach(var c in new CellRect(center.x-35,center.z-16,71,33).Cells)
            {
                foreach(var t in c.GetThingList(map).ToList()){if(t.def.destroyable)t.Destroy(DestroyMode.Vanish);else t.DeSpawn();}
                map.terrainGrid.SetTerrain(c,TerrainDefOf.Soil);map.roofGrid.SetRoof(c,null);map.fogGrid.Unfog(c);
            }
            foreach(var p in map.mapPawns.AllPawnsSpawned.ToList()){p.GetLord()?.Notify_PawnLost(p,PawnLostCondition.ExitedMap);p.DeSpawn();Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);}
            foreach(var bed in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().ToList())bed.Destroy(DestroyMode.Vanish);
            for(int x=-18;x<=-4;x++)for(int z=-5;z<=5;z++)
            {
                if(x==-18||x==-4||z==-5||z==5)Build(x==-4&&z==0?"Door":"Wall",x,z);
                map.roofGrid.SetRoof(center+new IntVec3(x,0,z),RoofDefOf.RoofConstructed);
            }
            if(stage==1)Build("Bed",-16,2);
            doctor=Spawn(true,center+new IntVec3(-12,0,-2),"Gun_Revolver",20);
            patient=Spawn(true,center+new IntVec3(-10,0,2),null,1);
            fighters=new[]{Spawn(true,center+new IntVec3(1,0,-2),"MeleeWeapon_LongSword",1),
                Spawn(true,center+new IntVec3(1,0,2),"MeleeWeapon_Mace",1),Spawn(true,center+new IntVec3(-3,0,0),"Gun_AssaultRifle",1)};
            if(Elite)fighters=fighters.Concat(new[]{Spawn(true,center+new IntVec3(-3,0,4),"Gun_AssaultRifle",1)}).ToArray();
            enemies=Enumerable.Range(0,6).Select(i=>Spawn(false,center+new IntVec3(i<2?2:24,0,i<2?i*4-2:i*2-4),"Gun_Revolver",1)).ToArray();
            foreach(var e in enemies.Skip(2))e.health.AddHediff(HediffDefOf.Anesthetic);
            patient.health.AddHediff(HediffDefOf.Anesthetic);
            var cut=HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Cut"),patient,patient.health.hediffSet.GetNotMissingParts().First(b=>b.def==(Elite?BodyPartDefOf.Torso:BodyPartDefOf.Leg)));
            cut.Severity=10;patient.health.AddHediff(cut);
            var blood=HediffMaker.MakeHediff(HediffDefOf.BloodLoss,patient);blood.Severity=stage==1?.4f:.985f;patient.health.AddHediff(blood);
            patient.playerSettings.medCare=MedicalCareCategory.Best;
            var meds=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MedicineHerbal"));meds.stackCount=8;GenSpawn.Spawn(meds,center+new IntVec3(-14,0,0),map);
            LordMaker.MakeNewLord(Faction.OfAncientsHostile,new LordJob_AssaultColony(Faction.OfAncientsHostile,false,false,false,false,false),map,enemies.Take(2));
            ai.SetCombatAutomation(true);
            Check(ai.CombatOrders.Count==(Elite?5:4),"Expected all capable combatants drafted before rescue.");
            ai.SetEmergencyAutomation(true);
            Check(ai.CurrentState.Threat.ActiveCount==2,"Four neutralized enemies must not count as active.");
            Check(ai.Emergency.Medical.Count==1 && ai.Emergency.Medical[0].Helper==doctor,"Weak armed medical specialist was not selected.");
            Check(ai.CombatOrders.Count==(Elite?4:3) && !doctor.Drafted,"Rescue must retain the remaining defenders versus two enemies.");
            Check(doctor.CurJob?.def==(stage==1?JobDefOf.Rescue:JobDefOf.TendPatient),"Wrong rescue/stabilization native job.");
            Log.Message($"[MedicalRescueTests] PASS dispatch {stage}: {(Elite?6:5)} colonists; 6 enemies, 4 neutralized; {(Elite?4:3)} defenders vs 2; doctor20 detached; deathTicks={HealthUtility.TicksUntilDeathDueToBloodLoss(patient)}; job={doctor.CurJob.def.defName}");
            started=true;careReported=false;start=Find.TickManager.TicksGame;
        }
        public override void MapComponentUpdate()
        {
            if(done||!GenCommandLine.CommandLineArgPassed("autonomousrimmedicalrescuetest"))return;
            foreach(var w in Find.WindowStack.Windows.Where(w=>w.forcePause).ToList())w.Close(false);
            Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
        }
        public override void MapComponentTick()
        {
            if(!EliteFixtureBaseline.Ready)return;
            if(done||!GenCommandLine.CommandLineArgPassed("autonomousrimmedicalrescuetest")||Find.TickManager.TicksGame<300)return;
            try
            {
                if(!started){stage=GenCommandLine.CommandLineArgPassed("autonomousrimmedicalfieldtest")?2:1;Setup();return;}
                EliteFixtureBaseline.VerifyOriginalsPresent(map);
                Check(!patient.Dead,"Patient died of bleeding before native treatment.");
                if(patient.health.hediffSet.BleedRateTotal>0)
                {
                    Check(!doctor.Drafted,"Doctor was recruited again while rescuing/tending.");
                    if(Find.TickManager.TicksGame-start>5000)throw new InvalidOperationException("Rescue/tend timeout: "+doctor.CurJob?.def.defName);
                    return;
                }
                if(!careReported)
                {
                    if(stage==1)
                    {
                        Check(patient.InBed(),"Treatment did not follow transport to bed.");
                        Log.Message("[MedicalRescueTests] PASS hospital: native rescue followed by native tend; bleeding stopped, patient alive; no healing after setup.");
                    }
                    else
                    {
                        Check(!patient.InBed(),"Field stabilization fixture unexpectedly had a bed.");
                        Log.Message("[MedicalRescueTests] PASS stabilization: native tending without a bed stopped bleeding before the deadline.");
                    }
                    careReported=true;careFinished=Find.TickManager.TicksGame;
                }
                if(MedicalRescueManager.Reserved(doctor))
                {
                    Check(Find.TickManager.TicksGame-careFinished<1000,"Completed care did not release the medical helper automatically.");
                    return;
                }
                Log.Message($"[MedicalRescueTests] PASS release {stage}: helper released automatically after native care.");
                if(stage==1 && !Elite){stage=2;started=false;Setup();return;}
                var ai=map.GetComponent<AutonomousRimMapComponent>();ai.SetEmergencyAutomation(false);
                doctor.drafter.Drafted=true;var manual=JobMaker.MakeJob(JobDefOf.Wait_Combat);doctor.jobs.TryTakeOrderedJob(manual,JobTag.Misc,false);
                ai.SetEmergencyAutomation(true);
                Check(doctor.Drafted && doctor.CurJob.GetUniqueLoadID()==manual.GetUniqueLoadID(),"Manual order was overwritten.");
                var unsafeEnemy=Spawn(false,center+new IntVec3(30,0,10),"Gun_Revolver",1);
                var liveThreat=AutonomousRim.Perception.ThreatScanner.Scan(map,ColonyStateScanner.Scan(map));
                Check(!MedicalRescueManager.Safe(map,unsafeEnemy.Position,liveThreat),"Rescue safety allowed an active enemy contact cell.");
                unsafeEnemy.Destroy(DestroyMode.Vanish);
                Log.Message("[MedicalRescueTests] PASS safety/manual: enemy contact rejected; player draft/order preserved.");
                ai.DisableAll();Log.Message("[MedicalRescueTests] DONE");done=true;Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
            }
            catch(Exception ex){GameDataSaveLoader.SaveGame("MedicalRescueFailure");done=true;Log.Error("[MedicalRescueTests] FAIL: "+ex);Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;}
        }
    }
}
