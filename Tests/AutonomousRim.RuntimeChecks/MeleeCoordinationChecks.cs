using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    // Native jobs and damage; only the initial arena/equipment are spawned.
    public sealed class MeleeCoordinationChecks : MapComponent
    {
        private bool started,finished;
        private int start,jointSamples;
        private List<Pawn> roster;
        private Pawn first,second,contact,distant;
        private readonly List<CombatOrder> orders=new List<CombatOrder>();
        private readonly List<Pawn> excluded=new List<Pawn>();
        private float firstDamage,secondDamage;
        public MeleeCoordinationChecks(Map map):base(map){}
        private static void Check(bool condition,string message)
        {if(!condition)throw new InvalidOperationException(message);}
        private void Equip(Pawn pawn,string name)
        {
            foreach(var old in pawn.equipment.AllEquipmentListForReading.ToList())pawn.equipment.Remove(old);
            var def=DefDatabase<ThingDef>.GetNamed(name);
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.Steel:null));
        }
        private Pawn Enemy(IntVec3 position,string weapon)
        {
            Pawn pawn;
            do{pawn=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfAncientsHostile,
                forceGenerateNewPawn:true,canGeneratePawnRelations:false));}while(pawn.Downed || pawn.health.hediffSet.hediffs.Count>0);
            Equip(pawn,weapon);GenSpawn.Spawn(pawn,position,map);return pawn;
        }
        private static float Damage(Pawn pawn)=>pawn.records.GetValue(DefDatabase<RecordDef>.GetNamed("DamageDealt"));
        public override void MapComponentTick()
        {
            if(finished || !GenCommandLine.CommandLineArgPassed("autonomousrimmeleecoordinationtest") ||
                !EliteFixtureBaseline.Ready || Find.TickManager.TicksGame<300)return;
            try
            {
                if(!started)
                {
                    map.GetComponent<AutonomousRim.Core.AutonomousRimMapComponent>().DisableAll();
                    roster=EliteFixtureBaseline.Roster(map);
                    foreach(var pawn in map.mapPawns.AllPawnsSpawned.ToList())
                    {pawn.DeSpawn();if(!roster.Contains(pawn))Find.WorldPawns.PassToWorld(pawn,PawnDiscardDecideMode.KeepForever);}
                    foreach(var thing in map.listerThings.AllThings.Where(t=>!(t is Pawn) && t is Verse.AI.IAttackTarget && t.HostileTo(Faction.OfPlayer)).ToList())thing.Destroy(DestroyMode.Vanish);
                    var center=map.Center;
                    foreach(var cell in new CellRect(center.x-40,center.z-40,81,81))
                    {
                        foreach(var thing in cell.GetThingList(map).ToList())thing.Destroy(DestroyMode.Vanish);
                        map.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);map.roofGrid.SetRoof(cell,null);map.fogGrid.Unfog(cell);
                    }
                    first=roster.Single(p=>p.Name.ToStringShort=="Bravo");second=roster.Single(p=>p.Name.ToStringShort=="Foxtrot");
                    Equip(first,"MeleeWeapon_LongSword");Equip(second,"MeleeWeapon_Mace");
                    GenSpawn.Spawn(first,center+new IntVec3(-1,0,-1),map);GenSpawn.Spawn(second,center+new IntVec3(1,0,1),map);
                    int offset=0;
                    foreach(var pawn in roster.Where(p=>p!=first && p!=second))
                    {Equip(pawn,"Gun_Revolver");GenSpawn.Spawn(pawn,center+new IntVec3(-6,0,-3+offset++*2),map);}
                    contact=Enemy(center,"MeleeWeapon_Mace");distant=Enemy(center+new IntVec3(0,0,27),"Gun_Revolver");
                    Check(CombatManager.Enemies(map).Count==2,"Unexpected threat in isolated melee arena");
                    var occupied=new HashSet<IntVec3>();var assignments=new Dictionary<int,int>();
                    var contactPlan=MeleeTactics.PlanAttack(map,first,contact,roster,new List<Pawn>{contact,distant},
                        center+new IntVec3(-40,0,0),occupied,assignments,0);
                    Check(contactPlan?.Strike==true,"Contact must remain defendable beyond the old anchor and approach limits");
                    Log.Message("[MeleeCoordination] PASS: adjacent defense beyond old anchor; no advance into unsafe distant target");
                    foreach(var pawn in new[]{first,second})
                    {
                        pawn.drafter.Drafted=true;
                        orders.Add(new CombatOrder{Pawn=pawn,Anchor=pawn.Position,LastOrderTick=-300,
                            MeleeTarget=distant,MeleeTargetTick=Find.TickManager.TicksGame,OriginalFireAtWill=pawn.drafter.FireAtWill});
                    }
                    CombatManager.Apply(map,orders,excluded);
                    Check(new[]{first,second}.All(p=>p.CurJob?.def==JobDefOf.AttackMelee && p.CurJob.targetA.Thing==contact),
                        "Both melee must reject their unsafe preferred target and attack the safe shared contact");
                    Log.Message("[MeleeCoordination] PASS: both original melee take native AttackMelee on safe shared alternative");
                    firstDamage=Damage(first);secondDamage=Damage(second);
                    var enemyAttack=JobMaker.MakeJob(JobDefOf.AttackMelee,first);enemyAttack.expiryInterval=1200;
                    contact.jobs.TryTakeOrderedJob(enemyAttack,JobTag.Misc,false);
                    start=Find.TickManager.TicksGame;started=true;return;
                }
                int elapsed=Find.TickManager.TicksGame-start;
                if(elapsed%15==0)
                {
                    CombatManager.Apply(map,orders,excluded);
                    if(new[]{first,second}.All(p=>p.Spawned && !p.Downed && p.CurJob?.def==JobDefOf.AttackMelee &&
                        p.CurJob.targetA.Thing==contact && p.Position.DistanceTo(contact.Position)<1.6f))jointSamples++;
                    Check(roster.All(p=>!p.Dead && !p.Downed),"Original colonist lost in cooperation probe");
                    if(Damage(first)>firstDamage && Damage(second)>secondDamage && jointSamples>0)
                    {
                        Log.Message($"[MeleeCoordination] PASS: native damage from both original melee; Bravo={Damage(first)-firstDamage}; Foxtrot={Damage(second)-secondDamage}; jointSamples={jointSamples}; ticks={elapsed}");
                        CombatManager.Stop(orders);EliteFixtureBaseline.VerifyOriginalsPresent(map);
                        finished=true;Log.Message("[MeleeCoordination] DONE");Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;return;
                    }
                    Check(elapsed<1200,"Native pair failed to damage shared target within 1200 ticks");
                }
            }
            catch(Exception error){finished=true;Log.Error("[MeleeCoordination] FAIL: "+error);Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;}
        }
    }
}
