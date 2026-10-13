using System;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using RimWorld;
using Verse;
using Verse.AI;
namespace AutonomousRim.RuntimeChecks
{
    public sealed class TacticalContinuityChecks : MapComponent
    {
        private bool started,done,casting;private int tick;private Pawn caster,enemy;private Thing cargo;private IntVec3 destination;
        public TacticalContinuityChecks(Map map):base(map){}
        private void Check(bool ok,string label){if(!ok)throw new Exception(label);Log.Message("[TacticalContinuity] PASS: "+label);}
        public override void MapComponentUpdate(){if(!GenCommandLine.CommandLineArgPassed("autonomousrimtacticalcontinuitytest"))return;foreach(var w in Find.WindowStack.Windows.Where(w=>w.forcePause).ToList())w.Close(false);Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;}
        public override void MapComponentTick()
        {
            if(done || !EliteFixtureBaseline.Ready || !GenCommandLine.CommandLineArgPassed("autonomousrimtacticalcontinuitytest") || Find.TickManager.TicksGame<300)return;
            try
            {
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(!started)
                {
                    started=true;ai.DisableAll();var roster=EliteFixtureBaseline.Roster(map);caster=roster[0];
                    foreach(var p in roster)p.jobs.StopAll();
                    var center=map.Center;
                    foreach(var c in new CellRect(center.x-10,center.z-10,21,21)) {foreach(var t in c.GetThingList(map).Where(t=>!(t is Pawn)).ToList())t.Destroy(DestroyMode.Vanish);map.terrainGrid.SetTerrain(c,TerrainDefOf.Soil);map.fogGrid.Unfog(c);map.roofGrid.SetRoof(c,null);}
                    caster.Position=center;roster[1].Position=center+new IntVec3(3,0,1);
                    enemy=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfAncientsHostile,forceGenerateNewPawn:true,canGeneratePawnRelations:false));
                    GenSpawn.Spawn(enemy,center+new IntVec3(3,0,0),map);
                    var wall=ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.WoodLog);wall.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(wall,center+new IntVec3(4,0,0),map);
                    enemy.jobs.StartJob(JobMaker.MakeJob(JobDefOf.AttackMelee,wall));ai.Doctrine.Update(map);
                    Check(ai.Doctrine.Breach && ai.Doctrine.PriorityTargets.Contains(enemy.thingIDNumber),"wall attacker is recognized as priority breach threat");
                    Check(!PsycastSupport.TryStun(new CombatOrder{Pawn=caster,LastOrderTick=-300},enemy),"undrafted caster without an available power issues no cast");
                    caster.ChangePsylinkLevel(Math.Max(0,1-caster.GetPsylinkLevel()),false);caster.abilities.GainAbility(DefDatabase<AbilityDef>.GetNamed("Stun"));caster.psychicEntropy.OffsetPsyfocusDirectly(1);
                    caster.drafter.Drafted=true;caster.drafter.FireAtWill=false;caster.stances.SetStance(new Stance_Mobile());caster.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait_Combat));
                    var order=new CombatOrder{Pawn=caster,LastOrderTick=-300};
                    var ability=caster.abilities.abilities.First(a=>a.def.defName=="Stun");
                    bool issued=PsycastSupport.TryStun(order,enemy);ability.GizmoDisabled(out var reason);
                    Check(issued,"native valid stun cast is dispatched against nearby threat; report="+reason+" cast="+ability.CanCast+" apply="+ability.CanApplyOn((LocalTargetInfo)enemy)+" sight="+ability.verb.CanHitTarget(enemy)+" busy="+caster.stances.FullBodyBusy);
                    Check(caster.CurJob.ability?.def.defName=="Stun" && order.JobId==caster.CurJob.GetUniqueLoadID(),"cast ownership records actual native ability job");
                    casting=true;tick=Find.TickManager.TicksGame;return;
                }
                if(casting)
                {
                    if(enemy.stances.stunner.Stunned)
                    {
                        Check(true,"native psycast actually stunned the enemy");enemy.Destroy(DestroyMode.Vanish);casting=false;caster.jobs.StopAll();caster.drafter.Drafted=false;
                        foreach(var p in EliteFixtureBaseline.Roster(map))p.jobs.StopAll();
                        destination=map.Center+new IntVec3(-5,0,0);var zone=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager);zone.settings.filter.SetDisallowAll();zone.settings.filter.SetAllow(ThingDefOf.WoodLog,true);map.zoneManager.RegisterZone(zone);zone.AddCell(destination);
                        cargo=ThingMaker.MakeThing(ThingDefOf.WoodLog);cargo.stackCount=10;GenSpawn.Spawn(cargo,map.Center+new IntVec3(-2,0,0),map);
                        var job=HaulAIUtility.HaulToCellStorageJob(caster,cargo,destination,false);Check(job!=null,"native hauling allocator finds destination");caster.jobs.StartJob(job);LogisticsMonitor.Observe(map);tick=Find.TickManager.TicksGame;return;
                    }
                    if(Find.TickManager.TicksGame-tick>500)throw new Exception("Native stun did not apply: "+caster.CurJob?.def);return;
                }
                LogisticsMonitor.Observe(map);
                if(ai.Logistics.Rows.Any(r=>r.Item=="WoodLog"&&r.Trips>0))
                {
                    Check(destination.GetThingList(map).Any(t=>t.def==ThingDefOf.WoodLog&&t.stackCount>=10),"completed hauling placed actual cargo in approved storage");
                    Check(ai.Logistics.Rows.First(r=>r.Item=="WoodLog").Ticks>0,"logistics measured actual native job duration");
                    EliteFixtureBaseline.VerifyOriginalsPresent(map);done=true;Log.Message("[TacticalContinuity] DONE");return;
                }
                if(Find.TickManager.TicksGame-tick>2500)throw new Exception("Native hauling metric absent: "+caster.CurJob?.def);
            }
            catch(Exception e){done=true;Log.Error("[TacticalContinuity] FAIL: "+e);}
        }
    }
}
