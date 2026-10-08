using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Planning;
using AutonomousRim.Perception;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace AutonomousRim.RuntimeChecks
{
    // One fresh native process per case: outcomes are recorded even when the
    // colony loses. Only harness/setup faults abort a case as FAIL.
    public sealed class FiveCombatTrials : MapComponent
    {
        private readonly List<Pawn> allies=new List<Pawn>(), enemies=new List<Pawn>();
        private readonly HashSet<int> initialMelee=new HashSet<int>();
        private bool started, finished, hard;
        private int number, start, seed, cover, melee, retreat;
        private IntVec3 center;
        private Building_Door door;
        private string layout, weapons, roster;
        private Random random;
        private bool Revision=>GenCommandLine.CommandLineArgPassed("autonomousrimmeleerevision");
        private int flankSamples,approachSamples,idleSamples,firstMeleeTick=-1;
        public FiveCombatTrials(Map map):base(map){}
        public override void MapComponentUpdate()
        {
            if(finished || !GenCommandLine.CommandLineArgPassed("autonomousrimcombatfive"))return;
            foreach(var w in Find.WindowStack.Windows.Where(w=>w.forcePause).ToList())w.Close(false);
            Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
        }
        private static void Check(bool valid,string message) { if(!valid)throw new InvalidOperationException(message); }
        private string Pick(params string[] choices)=>choices[random.Next(choices.Length)];
        private ThingWithComps Item(string name)
        {
            var def=DefDatabase<ThingDef>.GetNamed(name);
            var item=(ThingWithComps)ThingMaker.MakeThing(def,def.MadeFromStuff?(name=="Apparel_Pants" || name=="Apparel_BasicShirt"?ThingDefOf.Cloth:ThingDefOf.Steel):null);
            item.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Colony);
            return item;
        }
        private Pawn Spawn(string kind,Faction faction,IntVec3 cell,string weapon,bool friendly)
        {
            Pawn pawn;
            do
            {
                pawn=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed(kind),faction,forceGenerateNewPawn:true,canGeneratePawnRelations:false));
                if(pawn.story!=null)foreach(var trait in pawn.story.traits.allTraits.ToList())pawn.story.traits.RemoveTrait(trait);
            } while(pawn.Downed || pawn.health.summaryHealth.SummaryHealthPercent<0.99f || Revision && pawn.health.hediffSet.hediffs.Count>0 ||
                pawn.health.hediffSet.PainTotal>0 || pawn.WorkTagIsDisabled(WorkTags.Violent) ||
                Revision && friendly && pawn.skills.skills.Any(s=>s.TotallyDisabled) ||
                !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving) || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && pawn.RaceProps.Humanlike);
            if(pawn.skills!=null)
            {
                if(Revision && friendly)foreach(var skill in pawn.skills.skills){skill.Level=20;skill.xpSinceLastLevel=0;}
                pawn.skills.GetSkill(SkillDefOf.Shooting).Level=friendly?10:random.Next(6,13);
                pawn.skills.GetSkill(SkillDefOf.Melee).Level=friendly?10:random.Next(6,13);
                if(Revision && friendly){pawn.skills.GetSkill(SkillDefOf.Shooting).Level=20;pawn.skills.GetSkill(SkillDefOf.Melee).Level=20;}
            }
            if(weapon!=null)
            {
                foreach(var old in pawn.equipment.AllEquipmentListForReading.ToList())pawn.equipment.Remove(old);
                pawn.equipment.AddEquipment(Item(weapon));
            }
            if(pawn.RaceProps.Humanlike)
            {
                foreach(var old in pawn.apparel.WornApparel.ToList())pawn.apparel.Remove(old);
                pawn.apparel.Wear((Apparel)Item("Apparel_Pants")); pawn.apparel.Wear((Apparel)Item("Apparel_BasicShirt"));
                if(friendly || random.NextDouble()<0.35)pawn.apparel.Wear((Apparel)Item(Revision && friendly && weapon?.StartsWith("MeleeWeapon_")==true?"Apparel_PlateArmor":"Apparel_FlakVest"));
                if(friendly || random.NextDouble()<0.25)pawn.apparel.Wear((Apparel)Item("Apparel_SimpleHelmet"));
            }
            GenSpawn.Spawn(pawn,cell,map);
            if(Revision && friendly)
            {
                if(pawn.needs.food!=null)pawn.needs.food.CurLevelPercentage=1;
                if(pawn.needs.rest!=null)pawn.needs.rest.CurLevelPercentage=1;
                if(pawn.needs.joy!=null)pawn.needs.joy.CurLevelPercentage=1;
                if(pawn.needs.mood!=null)pawn.needs.mood.CurLevelPercentage=1;
            }
            return pawn;
        }
        private void Build(string name,int x,int z)
        {
            var def=DefDatabase<ThingDef>.GetNamed(name);
            var thing=ThingMaker.MakeThing(def,def.MadeFromStuff?(name=="Sandbags"?ThingDefOf.Cloth:DefDatabase<ThingDef>.GetNamed("BlocksGranite")):null);
            thing.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(thing,center+new IntVec3(x,0,z),map);
            if(thing is Building_Door d)door=d;
        }
        private string Manifest(Pawn pawn)=>pawn.LabelShort+":"+pawn.kindDef.defName+"/"+(pawn.equipment?.Primary?.def.defName??"natural")+
            "/armor="+string.Join("+",pawn.apparel?.WornApparel.Select(a=>a.def.defName)??Enumerable.Empty<string>())+
            "/shoot="+(pawn.skills?.GetSkill(SkillDefOf.Shooting).Level??0)+"/melee="+(pawn.skills?.GetSkill(SkillDefOf.Melee).Level??0);
        private void Setup()
        {
            number=Enumerable.Range(1,5).FirstOrDefault(i=>GenCommandLine.CommandLineArgPassed("autonomousrimcase"+i));
            Check(number>0,"Select a case from 1 to 5.");
            hard=GenCommandLine.CommandLineArgPassed("autonomousrimdisadvantage"); seed=20261007+number*101+(hard?10000:0); random=new Random(seed);
            var ai=map.GetComponent<AutonomousRimMapComponent>(); ai.DisableAll(); center=map.Center;
            if(Revision)
            {
                var preset=DefDatabase<DifficultyDef>.GetNamed("Peaceful");Find.Storyteller.difficultyDef=preset;Find.Storyteller.difficulty.CopyFrom(preset);
                CombatTacticalPlanner.ParallelEnabled=!GenCommandLine.CommandLineArgPassed("autonomousrimsynchronous");
                CombatTacticalPlanner.MinimumPairs=1;
            }
            foreach(var pawn in map.mapPawns.AllPawnsSpawned.ToList()) { pawn.DeSpawn(); Find.WorldPawns.PassToWorld(pawn,PawnDiscardDecideMode.KeepForever); }
            foreach(var thing in map.listerThings.AllThings.Where(t=>t is Hive || !(t is Pawn) && t is Verse.AI.IAttackTarget && t.HostileTo(Faction.OfPlayer)).ToList())thing.Destroy(DestroyMode.Vanish);
            foreach(var c in new CellRect(center.x-50,center.z-40,101,81).Cells)
            {
                foreach(var thing in c.GetThingList(map).ToList())thing.Destroy(DestroyMode.Vanish);
                map.terrainGrid.SetTerrain(c,TerrainDefOf.Soil); map.roofGrid.SetRoof(c,null); map.fogGrid.Unfog(c);
            }
            foreach(var pawn in map.mapPawns.AllPawnsSpawned.ToList())
            { pawn.DeSpawn();Find.WorldPawns.PassToWorld(pawn,PawnDiscardDecideMode.KeepForever); }
            // Available fallback, never invulnerable: native enemies may smash it.
            for(int x=-29;x<=-20;x++)for(int z=-6;z<=6;z++)
            {
                if(x==-29 || x==-20 || z==-6 || z==6)Build(x==-20 && z==0?"Door":"Wall",x,z);
                map.roofGrid.SetRoof(center+new IntVec3(x,0,z),RoofDefOf.RoofConstructed);
            }
            var gun1=number%2==0?"Gun_BoltActionRifle":"Gun_AssaultRifle";
            var gun2=number%2==0?"Gun_Revolver":"Gun_PumpShotgun";
            allies.Add(Spawn("Colonist",Faction.OfPlayer,center+new IntVec3(-13,0,-3),gun1,true));
            allies.Add(Spawn("Colonist",Faction.OfPlayer,center+new IntVec3(-13,0,3),gun2,true));
            allies.Add(Spawn("Colonist",Faction.OfPlayer,center+new IntVec3(-10,0,-1),"MeleeWeapon_LongSword",true));
            allies.Add(Spawn("Colonist",Faction.OfPlayer,center+new IntVec3(-10,0,1),"MeleeWeapon_Mace",true));
            foreach(var pawn in allies.Where(p=>!CombatManager.Ranged(p)))initialMelee.Add(pawn.thingIDNumber);
            Check(allies.Count(CombatManager.Ranged)==2 && allies.All(p=>p.apparel.WornApparel.Any(a=>a.def.defName=="Apparel_FlakVest" || Revision && a.def.defName=="Apparel_PlateArmor") &&
                p.apparel.WornApparel.Any(a=>a.def.defName=="Apparel_SimpleHelmet")),"Expected two ranged, two melee, four vests and four helmets.");
            if(number==1)
            {
                layout="vantagem: cobertura e duas passagens";
                for(int z=-12;z<=12;z++)if(z!=-1 && z!=1)Build("Wall",-8,z);
                for(int z=-5;z<=5;z++)if(z!=-1 && z!=1)Build("Sandbags",-12,z);
            }
            else if(number==2)
            {
                layout="equilibrio: cobertura nos dois lados";
                for(int z=-6;z<=6;z++)if(z!=0) { Build("Sandbags",-12,z); Build("Sandbags",9,z); }
            }
            else if(number==3)layout=hard?"desvantagem: dois flancos, sem cobertura inicial":"equilibrio: campo aberto, sem cobertura inicial";
            else if(number==4)
            {
                layout="manhunters: aproximação corpo a corpo";
                for(int z=-6;z<=6;z++)if(z!=0)Build("Sandbags",-12,z);
            }
            else
            {
                layout=hard?"desvantagem: superioridade mechanoid":"vantagem numerica: quatro contra dois mechs";
                for(int z=-6;z<=6;z++)if(z!=0)Build("Sandbags",-12,z);
            }
            int count=Revision?(number==3?8:4):(hard?new[]{3,4,6,6,5}:new[]{3,4,4,4,2})[number-1];
            for(int i=0;i<count;i++)
            {
                var cell=center+new IntVec3(13+i%2,0,(i-count/2)*3);
                if(hard && number==3 && i>=count/2)cell=center+new IntVec3(-7+(i-count/2)*3,0,19);
                string kind=number==4?Pick("Wolf_Timber","WildBoar","Warg"):number==5?Pick("Mech_Scyther","Mech_Lancer"):"Colonist";
                string weapon=number<4?Pick("Gun_Revolver","Gun_Autopistol","Gun_BoltActionRifle","MeleeWeapon_Knife","MeleeWeapon_Mace"):null;
                if(Revision)
                {
                    kind="Colonist";
                    weapon=number==1?"Gun_Revolver":number==2?"Bow_Short":number==3?"Gun_AssaultRifle":number==4?"MeleeWeapon_Knife":i<2?"Gun_Revolver":"MeleeWeapon_Mace";
                    // Short approach isolates cooperation rather than a prolonged firing-line crossing.
                    if(number==2 || number==5)cell=center+new IntVec3(-2+i%2,0,(i-count/2)*3);
                    if(number==4)cell=center+new IntVec3(4+i%2,0,(i-count/2)*3);
                }
                var enemy=Spawn(kind,number==4?null:Faction.OfAncientsHostile,cell,weapon,false); enemies.Add(enemy);
                if(Revision && number==4)enemy.SetFaction(Faction.OfAncientsHostile);
                if(number==4 && !Revision)Check(enemy.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent),"Manhunter setup failed.");
            }
            if(number!=4 || Revision)LordMaker.MakeNewLord(Faction.OfAncientsHostile,new LordJob_AssaultColony(Faction.OfAncientsHostile,false,false,false,false,false),map,enemies);
            Check(!CombatManager.Enemies(map).Except(enemies).Any(),"Unrelated hostile pawn contaminates the fixture.");
            if(Revision)
            {
                layout=new[]{"portas e paredes: atiradores inimigos","cooperação melee contra arqueiros","retirada contra oito rifles","interceptação e proteção dos ranged","combate misto e flanqueamento"}[number-1];
                foreach(var p in allies)Log.Message("[FiveCombatTrials] SETUP CHECK "+p.LabelShort+" skills="+string.Join(",",p.skills.skills.Select(s=>s.def.defName+":"+s.Level))+" traits="+p.story.traits.allTraits.Count+" hediffs="+string.Join(",",p.health.hediffSet.hediffs.Select(h=>h.def.defName)));
                Check(allies.All(p=>p.skills.skills.All(s=>s.Level==20) && p.story.traits.allTraits.Count==0 && p.health.hediffSet.hediffs.Count==0),"Level-20 healthy trait-free starting colonists required.");
                Log.Message("[FiveCombatTrials] MELEE REVISION: Peaceful; all colonist skills 20; no initial hediffs or traits; parallel="+CombatTacticalPlanner.ParallelEnabled);
                foreach(var p in allies.Concat(enemies)){var value=CombatEquipmentScanner.Copy(p);Log.Message("[FiveCombatTrials] EQUIPMENT "+p.LabelShort+" power="+value.Power+" armorSharp="+value.Sharp+" armorBlunt="+value.Blunt+" penetration="+value.Penetration);}
            }
            weapons=string.Join(" | ",allies.Select(Manifest)); roster=string.Join(" | ",enemies.Select(Manifest));
            Log.Message($"[FiveCombatTrials] START case={number}; seed={seed}; hard={hard}; {layout}; 2 melee+2 ranged; armor="+
                (Revision?"2 steel plates, 2 flak vests, 4 helmets":"4 flak vests, 4 helmets")+"; third speed selected; no healing during battle");
            Log.Message("[FiveCombatTrials] ALLIES "+weapons); Log.Message("[FiveCombatTrials] ENEMIES "+roster);
            start=Find.TickManager.TicksGame; started=true; ai.SetEmergencyAutomation(true);
        }
        private float Record(IEnumerable<Pawn> pawns,string name)=>pawns.Sum(p=>p.records?.GetValue(DefDatabase<RecordDef>.GetNamed(name))??0);
        private void Finish(string outcome,int ticks,bool neutralized,bool escaped)
        {
            var ai=map.GetComponent<AutonomousRimMapComponent>(); ai.DisableAll();
            GameDataSaveLoader.SaveGame("AutonomousRim-Combate-Misto-"+number);
            string row=$"case={number}; seed={seed}; outcome={outcome}; ticks={ticks}; enemies={enemies.Count}; remaining={enemies.Count(p=>!p.Dead&&!p.Downed&&p.Spawned)}; deaths={allies.Count(p=>p.Dead)}; downed={allies.Count(p=>!p.Dead&&p.Downed)}; injured={allies.Count(p=>p.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury && h.Severity>0))}; neutralized={neutralized}; sheltered={escaped}; alliedShots={Record(allies,"ShotsFired")}; enemyShots={Record(enemies,"ShotsFired")}; alliedDamage={Record(allies,"DamageDealt").ToString(CultureInfo.InvariantCulture)}; coverSamples={cover}; interceptSamples={melee}; retreatSamples={retreat}; flankSamples={flankSamples}; approachSamples={approachSamples}; idleMeleeSamples={idleSamples}; firstMeleeTick={firstMeleeTick}; meleeDamage={Record(allies.Where(p=>initialMelee.Contains(p.thingIDNumber)),"DamageDealt")}; workerJobs={AnalysisWorker.Completed}; workerMs={AnalysisWorker.Milliseconds:F2}; combatCalls={CombatManager.AnalysisCalls}; combatMs={CombatManager.AnalysisMilliseconds:F2}";
            Log.Message("[FiveCombatTrials] RESULT "+row);
            File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"result.txt"),row+Environment.NewLine+layout+Environment.NewLine+"ALLIES "+weapons+Environment.NewLine+"ENEMIES "+roster);
            foreach(var pawn in allies)Log.Message($"[FiveCombatTrials] COLONIST {pawn.LabelShort}: dead={pawn.Dead}; downed={pawn.Downed}; health={pawn.health.summaryHealth.SummaryHealthPercent}; bleed={pawn.health.hediffSet.BleedRateTotal}; position={pawn.Position}");
            finished=true; Log.Message("[FiveCombatTrials] DONE"); Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
        }
        public override void MapComponentTick()
        {
            if(finished || !GenCommandLine.CommandLineArgPassed("autonomousrimcombatfive") || Find.TickManager.TicksGame<300)return;
            try
            {
                if(!started) { Setup(); return; }
                int ticks=Find.TickManager.TicksGame-start;
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(ticks%60==0)
                {
                    Check(!CombatManager.Enemies(map).Except(enemies).Any(),"Unrelated hostile pawn appeared during the fixture.");
                    foreach(var order in ai.CombatOrders)
                    {
                        if(order.Retreated)retreat++;
                        if(order.Role=="Interceptar / ajudar aliado" || order.Role?.StartsWith("Melee:")==true)melee++;
                        if(order.Role=="Melee: flanco coordenado")flankSamples++;
                        if(!CombatManager.Ranged(order.Pawn) && order.Pawn.CurJob?.def==JobDefOf.Goto)approachSamples++;
                        if(!order.Retreated && !CombatManager.Ranged(order.Pawn) && order.Pawn.CurJob?.def==JobDefOf.Wait_Combat)idleSamples++;
                        if(!CombatManager.Ranged(order.Pawn) && order.Pawn.CurJob?.def==JobDefOf.AttackMelee && firstMeleeTick<0)firstMeleeTick=ticks;
                        if(CombatManager.Ranged(order.Pawn) && enemies.Any(e=>e.Spawned && !e.Dead && CoverUtility.CalculateOverallBlockChance(order.Pawn.Position,e.Position,map)>0.1f))cover++;
                    }
                    if(ticks%600==0)Log.Message($"[FiveCombatTrials] PROGRESS case={number}; ticks={ticks}; deaths={allies.Count(p=>p.Dead)}; downed={allies.Count(p=>p.Downed)}; threats={enemies.Count(p=>!p.Dead&&!p.Downed)}; shots={Record(allies,"ShotsFired")}/{Record(enemies,"ShotsFired")}; emergency={ai.Emergency.Phase}; jobs="+string.Join(" | ",allies.Select(p=>p.LabelShort+":"+p.Position+"/"+p.CurJob?.def.defName)));
                }
                bool neutralized=enemies.All(p=>p.Dead || p.Downed || !p.Spawned);
                bool escaped=allies.All(p=>p.Spawned && !p.Dead && !p.Downed && p.Position.GetRoom(map)==(center+new IntVec3(-25,0,0)).GetRoom(map)) && !door.Destroyed && !door.Open;
                if(neutralized)Finish(allies.Any(p=>p.Dead)?"VITORIA_COM_MORTES":"VITORIA",ticks,true,escaped);
                else if(allies.All(p=>p.Dead || p.Downed))Finish("DERROTA",ticks,false,false);
                else if(ticks>=1800 && escaped && retreat>0)Finish("RETIRADA",ticks,false,true);
                else if(ticks>=6000)Finish("SEM_DESFECHO",ticks,false,escaped);
            }
            catch(Exception ex) { finished=true; Log.Error("[FiveCombatTrials] FAIL case="+number+": "+ex); Find.TickManager.CurTimeSpeed=TimeSpeed.Paused; }
        }
    }
}
