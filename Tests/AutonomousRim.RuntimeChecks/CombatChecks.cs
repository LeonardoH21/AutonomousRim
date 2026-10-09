using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AutonomousRim.RuntimeChecks
{
    // Three isolated fixtures, never loaded over a player's save. Only setup
    // supplies pawns/equipment/terrain; damage, projectiles and pathing are native.
    public sealed class CombatChecks : MapComponent
    {
        private int scenario, start, coverSamples, retreatSamples, meleeSamples;
        private bool finished;
        private IntVec3 center;
        private readonly List<Pawn> allies=new List<Pawn>(), enemies=new List<Pawn>();
        private Pawn civilian, manual;
        private Building retreatDoor;
        private string manualJob;
        private readonly List<Thing> fixture=new List<Thing>();
        public CombatChecks(Map map):base(map) {}
        public override void MapComponentUpdate()
        {
            if(!GenCommandLine.CommandLineArgPassed("autonomousrimcombattest") || finished) return;
            foreach(var w in Find.WindowStack.Windows.Where(w=>w.forcePause).ToList()) w.Close(false);
            Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
        }
        private void Check(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
        private Pawn Spawn(bool hostile,IntVec3 cell,string weapon)
        {
            var request=new PawnGenerationRequest(PawnKindDefOf.Colonist,hostile?Faction.OfAncientsHostile:Faction.OfPlayer,forceGenerateNewPawn:true,canGeneratePawnRelations:false);
            var p=PawnGenerator.GeneratePawn(request);
            while(p.WorkTagIsDisabled(WorkTags.Violent)) p=PawnGenerator.GeneratePawn(request);
            p.skills.GetSkill(SkillDefOf.Shooting).Level=10;
            p.skills.GetSkill(SkillDefOf.Melee).Level=10;
            foreach(var trait in p.story.traits.allTraits.ToList()) p.story.traits.RemoveTrait(trait);
            foreach(var old in p.equipment.AllEquipmentListForReading.ToList()) p.equipment.Remove(old);
            GenSpawn.Spawn(p,cell,map); fixture.Add(p);
            if(weapon!=null)
            {
                var def=DefDatabase<ThingDef>.GetNamed(weapon);
                var item=(ThingWithComps)ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.Steel:null);
                p.equipment.AddEquipment(item);
            }
            if(!hostile) p.apparel.Wear((Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_FlakVest")));
            return p;
        }
        private void Building(string name,int x,int z)
        {
            var def=DefDatabase<ThingDef>.GetNamed(name);
            var thing=ThingMaker.MakeThing(def,def.MadeFromStuff?(name=="Wall" || name=="Door"?DefDatabase<ThingDef>.GetNamed("BlocksGranite"):ThingDefOf.Cloth):null);
            thing.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(thing,center+new IntVec3(x,0,z),map); fixture.Add(thing);
            if(name=="Door") retreatDoor=(Building)thing;
        }
        private void Setup()
        {
            var component=map.GetComponent<AutonomousRimMapComponent>();
            component.DisableAll();
            foreach(var thing in fixture.Where(t=>!t.Destroyed).ToList())
            {
                if(thing is Pawn p)
                {
                    if(p.Spawned) { p.DeSpawn(); Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever); }
                }
                else thing.Destroy(DestroyMode.Vanish);
            }
            fixture.Clear(); allies.Clear(); enemies.Clear(); coverSamples=retreatSamples=meleeSamples=0;
            if(scenario==0)
            {
                var peaceful=DefDatabase<DifficultyDef>.GetNamed("Peaceful");
                Find.Storyteller.difficultyDef=peaceful;Find.Storyteller.difficulty.CopyFrom(peaceful);
                center=map.Center;
                foreach(var p in map.mapPawns.AllPawnsSpawned.ToList()) { p.DeSpawn(); Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever); }
                var area=new CellRect(center.x-40,center.z-25,81,51);
                foreach(var cell in area.Cells)
                {
                    foreach(var thing in cell.GetThingList(map).ToList()) thing.Destroy(DestroyMode.Vanish);
                    map.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil); map.roofGrid.SetRoof(cell,null); map.fogGrid.Unfog(cell);
                }
                // Clearing an ancient ruin can wake new mechs from its contents.
                // Remove those before the explicit participants are created.
                foreach(var thing in map.listerThings.AllThings.Where(t=>t is Hive || !(t is Pawn) && t is Verse.AI.IAttackTarget && t.HostileTo(Faction.OfPlayer)).ToList())thing.Destroy(DestroyMode.Vanish);
                foreach(var pawn in map.mapPawns.AllPawnsSpawned.ToList())
                { pawn.DeSpawn();Find.WorldPawns.PassToWorld(pawn,PawnDiscardDecideMode.KeepForever); }
            }
            scenario++; start=Find.TickManager.TicksGame;
            allies.Add(Spawn(false,center+new IntVec3(scenario==1?-7:-8,0,scenario==1?0:-2),"Gun_AssaultRifle"));
            allies.Add(Spawn(false,center+new IntVec3(-8,0,scenario==1?0:2),"Gun_AssaultRifle"));
            allies.Add(Spawn(false,center+new IntVec3(scenario==1?-4:-9,0,0),"MeleeWeapon_LongSword"));
            // Protected control subjects isolate the manual/undrafted assertions
            // from the combat performance of the three participating fighters.
            for(int z=16;z<=21;z++) for(int x=-31;x<=-29;x++)
                if(x==-31 || x==-29 || z==16 || z==18 || z==19 || z==21) Building("Wall",x,z);
            civilian=Spawn(false,center+new IntVec3(-30,0,17),null);
            manual=Spawn(false,center+new IntVec3(-30,0,20),"Gun_BoltActionRifle");
            manual.drafter.Drafted=true; manual.drafter.FireAtWill=false;
            var wait=JobMaker.MakeJob(JobDefOf.Wait); wait.expiryInterval=100000;
            manual.jobs.TryTakeOrderedJob(wait,JobTag.Misc,false); manualJob=wait.GetUniqueLoadID();
            if(scenario==1)
            {
                for(int z=-9;z<=9;z++) if(z!=0) Building("Wall",0,z);
                for(int x=-4;x<4;x++) { Building("Wall",x,-1); Building("Wall",x,1); }
                Building("Sandbags",-5,-2); Building("Sandbags",-5,2);
            }
            else
            {
                for(int z=-4;z<=4;z++) if(z!=0) Building("Sandbags",-7,z);
                for(int z=-8;z<=8;z++) Building("Wall",-20,z);
                if(scenario==3)
                {
                    for(int x=-16;x<=-11;x++) for(int z=-4;z<=4;z++)
                    {
                        if(x==-16 || x==-11 || z==-4 || z==4) Building(x==-11 && z==0?"Door":"Wall",x,z);
                        map.roofGrid.SetRoof(center+new IntVec3(x,0,z),RoofDefOf.RoofConstructed);
                    }
                }
            }
            int count=scenario==1?1:scenario==2?2:9;
            for(int i=0;i<count;i++) enemies.Add(Spawn(true,center+new IntVec3(scenario==1?-2:12+i%3,0,scenario==1?0:(i/3)*2-2),scenario==2?"Gun_Revolver":"MeleeWeapon_Knife"));
            LordMaker.MakeNewLord(Faction.OfAncientsHostile,new LordJob_AssaultColony(Faction.OfAncientsHostile,false,false,false,false,false),map,enemies);
            Check(!CombatManager.Enemies(map).Except(enemies).Any(),"Unrelated hostile pawn contaminates the fixture.");
            if(scenario==2)
            {
                float raw=AutonomousRim.Perception.CombatEquipmentScanner.Copy(enemies[0]).Dps;
                Check(CombatManager.ExpectedRangedDps(enemies[0],allies[0].Position,raw)<raw,"Incoming exposure ignored native accuracy and cover.");
                Check(CombatManager.ExpectedRangedDps(enemies[0],center+new IntVec3(-21,0,0),raw)==0,"Blocked firing lane was treated as incoming damage.");
            }
            if(scenario==3)
            {
                var originalEnemyPositions=enemies.Take(3).Select(e=>e.Position).ToArray();
                try
                {
                    var nearbyEnemies=enemies.Take(3).ToList();
                    for(int i=0;i<nearbyEnemies.Count;i++)nearbyEnemies[i].Position=allies[0].Position+new IntVec3(8,0,i);
                    var supportOrders=allies.Select(a=>new CombatOrder{Pawn=a}).ToList();
                    var overwhelmed=HarmonyLib.AccessTools.Method(typeof(CombatManager),"Overwhelmed");
                    Check(!(bool)overwhelmed.Invoke(null,new object[]{allies[0],supportOrders,nearbyEnemies}),"Supported three-versus-three fixture unexpectedly overwhelmed.");
                    foreach(var helper in supportOrders.Where(o=>o.Pawn!=allies[0]))helper.Retreated=true;
                    Check((bool)overwhelmed.Invoke(null,new object[]{allies[0],supportOrders,nearbyEnemies}),"Withdrawing allies were counted as active defensive support.");
                    Check(!(bool)overwhelmed.Invoke(null,new object[]{allies[0],supportOrders,new List<Pawn>()}),"Withdrawing pawn cannot recover after threats disappear.");
                }
                finally{for(int i=0;i<originalEnemyPositions.Length;i++)enemies[i].Position=originalEnemyPositions[i];}
                var shelter=HarmonyLib.AccessTools.Method(typeof(CombatManager),"Shelter");
                Check(!(bool)shelter.Invoke(null,new object[]{map,retreatDoor.Position,enemies}),"Doorway was incorrectly classified as a retreat shelter.");
                Check((bool)shelter.Invoke(null,new object[]{map,center+new IntVec3(-14,0,0),enemies}),"Enclosed interior was rejected as a retreat shelter.");
                var originalPosition=allies[0].Position;
                try
                {
                    allies[0].Position=center+new IntVec3(8,0,-10);
                    var farOrder=new CombatOrder{Pawn=allies[0],Anchor=allies[0].Position};
                    var destination=(IntVec3)HarmonyLib.AccessTools.Method(typeof(CombatManager),"Position").Invoke(null,
                        new object[]{map,farOrder,enemies[0],enemies,allies,true,new HashSet<IntVec3>()});
                    Check(destination.DistanceTo(allies[0].Position)>12 && (bool)shelter.Invoke(null,new object[]{map,destination,enemies}),
                        "Retreat ignored a known reachable shelter beyond the local search radius.");
                }
                finally{allies[0].Position=originalPosition;}
                var cut=HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Cut"),civilian,civilian.health.hediffSet.GetNotMissingParts().First(b=>b.def==BodyPartDefOf.Leg));
                cut.Severity=16;civilian.health.AddHediff(cut);
                Check(civilian.health.hediffSet.BleedRateTotal>.35f && HealthUtility.TicksUntilDeathDueToBloodLoss(civilian)>12000,"Non-immediate bleeding fixture invalid.");
                Check(!(bool)HarmonyLib.AccessTools.Method(typeof(CombatManager),"Wounded").Invoke(null,new object[]{civilian}),"Treatable bleeding alone forced immediate combat withdrawal.");
            }
            if(scenario==3) allies[0].TakeDamage(new DamageInfo(DamageDefOf.Cut,18,0,-1,enemies[0],allies[0].health.hediffSet.GetNotMissingParts().First(b=>b.def==BodyPartDefOf.Leg)));
            component.SetCombatAutomation(true);
            if(scenario==3)component.SetEmergencyAutomation(true);
            meleeSamples += component.CombatOrders.Count(o=>o.Role=="Interceptar / ajudar aliado" || o.Role?.StartsWith("Melee:")==true);
            Log.Message($"[AutonomousRim.CombatTests] START {scenario}: center={center}; allies=3; enemies={count}; native third speed; no healing during battle; start="+string.Join(" | ",allies.Select(p=>p.Position+"/health="+p.health.summaryHealth.SummaryHealthPercent+"/bleed="+p.health.hediffSet.BleedRateTotal)));
        }
        public override void MapComponentTick()
        {
            if(finished || !GenCommandLine.CommandLineArgPassed("autonomousrimcombattest") || Find.TickManager.TicksGame<300) return;
            try
            {
                if(scenario==0) { Setup(); return; }
                int elapsed=Find.TickManager.TicksGame-start;
                var component=map.GetComponent<AutonomousRimMapComponent>();
                if(elapsed%60==0)
                {
                    Check(!CombatManager.Enemies(map).Except(enemies).Any(),"Unrelated hostile pawn appeared during the fixture.");
                    foreach(var order in component.CombatOrders)
                    {
                        if(order.Retreated) retreatSamples++;
                        if(order.Role=="Interceptar / ajudar aliado" || order.Role?.StartsWith("Melee:")==true) meleeSamples++;
                        if(CombatManager.Ranged(order.Pawn) && enemies.Any(e=>e.Spawned && CoverUtility.CalculateOverallBlockChance(order.Pawn.Position,e.Position,map)>0.1f)) coverSamples++;
                    }
                    Check(!civilian.Drafted,"Unarmed civilian was drafted.");
                    Check(manual.Drafted && manual.CurJob?.GetUniqueLoadID()==manualJob,"Manual draft/order was overridden.");
                    if(elapsed%600==0) Log.Message($"[AutonomousRim.CombatTests] PROGRESS {scenario}: ticks={elapsed}; threats={enemies.Count(e=>!e.Dead&&!e.Downed)}; downed={allies.Count(p=>p.Downed)}; cover={coverSamples}; intercept={meleeSamples}; retreat={retreatSamples}; jobs="+string.Join(" | ",allies.Select(p=>p.Position+"/"+p.CurJob?.def.defName+"/damage="+p.records.GetValue(DefDatabase<RecordDef>.GetNamed("DamageDealt")))));
                }
                bool neutralized=enemies.All(e=>e.Dead||e.Downed);
                if(scenario<3 && neutralized || scenario==3 && elapsed>=1800)
                {
                    Check(allies.All(p=>!p.Dead&&!p.Downed),"A controlled colonist died or was downed.");
                    if(scenario<3) Check(allies.Any(p=>p.records.GetValue(DefDatabase<RecordDef>.GetNamed("DamageDealt"))>0),"No native damage dealt.");
                    if(scenario==1) Check(meleeSamples>0,"Melee never intercepted at the chokepoint.");
                    if(scenario==2) Check(coverSamples>0,"Ranged fighters did not use cover.");
                    if(scenario==3) Check(retreatSamples>0 && allies.All(p=>p.Position.GetRoom(map)==(center+new IntVec3(-14,0,0)).GetRoom(map)) &&
                        retreatDoor is Building_Door door && !door.Open,"Squad did not withdraw into the closed-door shelter.");
                    component.SetCombatAutomation(false);
                    Check(allies.All(p=>!p.Drafted),"Disabling retained AI drafts.");
                    Check(manual.Drafted,"Disabling released a manual draft.");
                    GameDataSaveLoader.SaveGame("AutonomousRim-Combate-"+scenario);
                    Log.Message($"[AutonomousRim.CombatTests] PASS {scenario}: ticks={elapsed}; neutralized={neutralized}; deaths={allies.Count(p=>p.Dead)}; downed={allies.Count(p=>p.Downed)}; wounded={allies.Count(p=>p.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury))}; cover={coverSamples}; intercept={meleeSamples}; retreat={retreatSamples}.");
                    if(scenario==3) { finished=true; Find.TickManager.CurTimeSpeed=TimeSpeed.Paused; }
                    else Setup();
                }
                if(elapsed>12000) throw new InvalidOperationException("Battle timeout: "+component.CombatStatus);
            }
            catch(Exception ex) { finished=true; GameDataSaveLoader.SaveGame("AutonomousRim-Combate-Falha"); Log.Error("[AutonomousRim.CombatTests] FAIL "+scenario+": "+ex); Find.TickManager.CurTimeSpeed=TimeSpeed.Paused; }
        }
    }
}
