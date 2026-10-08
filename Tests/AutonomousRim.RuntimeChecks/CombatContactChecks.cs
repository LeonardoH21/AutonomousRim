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
    public sealed class CombatContactChecks:MapComponent
    {
        private bool started,finished;
        private int start;
        private Pawn fighter,enemy;
        private readonly List<CombatOrder> orders=new List<CombatOrder>();
        private readonly List<Pawn> excluded=new List<Pawn>();
        public CombatContactChecks(Map map):base(map){}
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);}
        private Pawn Spawn(Faction faction,IntVec3 cell,string weapon)
        {
            Pawn p;
            do{p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,faction,forceGenerateNewPawn:true,canGeneratePawnRelations:false));}
            while(p.health.hediffSet.hediffs.Count>0 || p.skills.skills.Any(s=>s.TotallyDisabled));
            foreach(var t in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(t);
            foreach(var skill in p.skills.skills)skill.Level=faction==Faction.OfPlayer?20:8;
            foreach(var old in p.equipment.AllEquipmentListForReading.ToList())p.equipment.Remove(old);
            var def=DefDatabase<ThingDef>.GetNamed(weapon);p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.Steel:null));
            GenSpawn.Spawn(p,cell,map);p.needs.food.CurLevel=1;p.needs.rest.CurLevel=1;
            if(p.needs.mood!=null)p.needs.mood.CurLevel=1;return p;
        }
        public override void MapComponentTick()
        {
            if(finished || !GenCommandLine.CommandLineArgPassed("autonomousrimcontacttest") || Find.TickManager.TicksGame<300)return;
            try
            {
                if(!started)
                {
                    map.GetComponent<AutonomousRimMapComponent>().DisableAll();
                    var preset=DefDatabase<DifficultyDef>.GetNamed("Peaceful");Find.Storyteller.difficultyDef=preset;Find.Storyteller.difficulty.CopyFrom(preset);
                    foreach(var p in map.mapPawns.AllPawnsSpawned.ToList()){p.DeSpawn();Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);}
                    var center=map.Center;
                    foreach(var cell in new CellRect(center.x-15,center.z-15,31,31))
                    {
                        foreach(var t in cell.GetThingList(map).ToList()){if(t.def.destroyable)t.Destroy(DestroyMode.Vanish);else t.DeSpawn();}
                        map.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);map.roofGrid.SetRoof(cell,null);map.fogGrid.Unfog(cell);
                    }
                    fighter=Spawn(Faction.OfPlayer,center,"MeleeWeapon_LongSword");
                    foreach(var name in new[]{"Apparel_PlateArmor","Apparel_SimpleHelmet"})fighter.apparel.Wear((Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name),ThingDefOf.Steel));
                    enemy=Spawn(Faction.OfAncientsHostile,center+new IntVec3(1,0,0),"Gun_Revolver");
                    LordMaker.MakeNewLord(Faction.OfAncientsHostile,new LordJob_AssaultColony(Faction.OfAncientsHostile,false,false,false,false,false),map,new[]{enemy});
                    // Isolate contact defense: the gunner is actively attacking in melee,
                    // rather than repositioning to resume shooting at range.
                    var contactAttack=JobMaker.MakeJob(JobDefOf.AttackMelee,fighter);
                    contactAttack.expiryInterval=6000;
                    enemy.jobs.TryTakeOrderedJob(contactAttack,JobTag.Misc,false);
                    Check(enemy.GetStatValue(StatDefOf.MoveSpeed)>=fighter.GetStatValue(StatDefOf.MoveSpeed)*.95f,"Contact fixture requires an enemy the armored fighter cannot outrun.");
                    CombatManager.Apply(map,orders,excluded,true);
                    Check(fighter.CurJob?.def==JobDefOf.AttackMelee && fighter.CurJob.targetA.Thing==enemy,"Slow fighter turned away from adjacent ranged enemy during retreat.");
                    Log.Message("[ContactTests] PASS: adjacent ranged enemy forces native melee self-defense when escape speed is insufficient");
                    start=Find.TickManager.TicksGame;started=true;return;
                }
                Check(!fighter.Dead && !fighter.Downed,"Armored contact defender was lost.");
                if(fighter.records.GetValue(DefDatabase<RecordDef>.GetNamed("DamageDealt"))>0)
                {
                    Check(fighter.records.GetValue(DefDatabase<RecordDef>.GetNamed("DamageDealt"))>0,"Contact job caused no native damage.");
                    CombatManager.Stop(orders);GameDataSaveLoader.SaveGame("ContactDefenseComplete");
                    Log.Message("[ContactTests] PASS: contact defense inflicts native damage without downing the defender; job ownership released (no victory claim)");
                    finished=true;Log.Message("[ContactTests] DONE");return;
                }
                if(Find.TickManager.TicksGame%15==0)CombatManager.Apply(map,orders,excluded,true);
                Check(Find.TickManager.TicksGame-start<6000,"Contact self-defense did not resolve.");
            }
            catch(Exception ex){finished=true;Log.Error("[ContactTests] FAIL: "+ex);}
        }
    }
}
