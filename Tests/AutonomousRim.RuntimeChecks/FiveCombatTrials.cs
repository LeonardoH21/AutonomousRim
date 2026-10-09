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
        private List<Pawn> allies=new List<Pawn>(), enemies=new List<Pawn>();
        private List<Pawn> eliteRoster=new List<Pawn>();
        private bool Elite=>GenCommandLine.CommandLineArgPassed("autonomousrimelitesix");
        private int ExpectedAllies=>Elite?6:4;
        private HashSet<int> initialMelee=new HashSet<int>();
        private bool started, finished, hard;
        private int number, start, seed, cover, melee, retreat;
        private IntVec3 center;
        private Building_Door door;
        private string layout, weapons, roster;
        private Random random;
        private bool Revision=>GenCommandLine.CommandLineArgPassed("autonomousrimmeleerevision");
        private bool Varied=>GenCommandLine.CommandLineArgPassed("autonomousrimvariedthreats");
        private int flankSamples,approachSamples,idleSamples,firstMeleeTick=-1;
        private bool careStarted, hospitalWitness;
        private int careStart;
        private float initialTends;
        private string combatResult;
        private bool resumeResultLogged;
        private bool PostBattleCare=>GenCommandLine.CommandLineArgPassed("autonomousrimpostbattlecare");
        private static bool resumeLoaded;
        public FiveCombatTrials(Map map):base(map){}
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref allies,"combatAllies",LookMode.Reference);
            Scribe_Collections.Look(ref enemies,"combatEnemies",LookMode.Reference);
            Scribe_Collections.Look(ref initialMelee,"combatInitialMelee",LookMode.Value);
            Scribe_Values.Look(ref started,"combatStarted");Scribe_Values.Look(ref finished,"combatFinished");
            Scribe_Values.Look(ref number,"combatCase");Scribe_Values.Look(ref start,"combatStart");Scribe_Values.Look(ref seed,"combatSeed");
            Scribe_Values.Look(ref hard,"combatHard");Scribe_Values.Look(ref center,"combatCenter");Scribe_References.Look(ref door,"combatDoor");
            Scribe_Values.Look(ref layout,"combatLayout");Scribe_Values.Look(ref weapons,"combatWeapons");Scribe_Values.Look(ref roster,"combatRoster");
            Scribe_Values.Look(ref cover,"combatCover");Scribe_Values.Look(ref melee,"combatMelee");Scribe_Values.Look(ref retreat,"combatRetreat");
            Scribe_Values.Look(ref flankSamples,"combatFlank");Scribe_Values.Look(ref approachSamples,"combatApproach");Scribe_Values.Look(ref idleSamples,"combatIdle");
            Scribe_Values.Look(ref firstMeleeTick,"combatFirstMelee",-1);
            Scribe_Values.Look(ref careStarted,"combatCareStarted");Scribe_Values.Look(ref careStart,"combatCareStart");
            Scribe_Values.Look(ref hospitalWitness,"combatHospitalWitness");Scribe_Values.Look(ref initialTends,"combatInitialTends");
            Scribe_Values.Look(ref combatResult,"combatResult");
            if(Scribe.mode==LoadSaveMode.PostLoadInit)
            {allies=allies??new List<Pawn>();enemies=enemies??new List<Pawn>();initialMelee=initialMelee??new HashSet<int>();}
        }
        public override void MapComponentUpdate()
        {
            if((GenCommandLine.CommandLineArgPassed("autonomousrimcombatresume") || GenCommandLine.CommandLineArgPassed("autonomousrimcombatbaseline")) && !resumeLoaded && Current.ProgramState==ProgramState.Playing)
            {resumeLoaded=true;GameDataSaveLoader.LoadGame("ModularResume");return;}
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
            if(friendly && Elite)
            {
                string name=new[]{"Alpha","Charlie","Bravo","Foxtrot","Delta","Echo"}[allies.Count];
                pawn=eliteRoster.Single(p=>p.Name.ToStringShort==name);
                Check(!pawn.Dead && !pawn.Downed,"Original EliteSix combatant unavailable: "+name);
            }
            else
            {
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
            if(Revision && friendly && !Elite)
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
            if(Revision && !Elite)
            {
                var preset=DefDatabase<DifficultyDef>.GetNamed("Peaceful");Find.Storyteller.difficultyDef=preset;Find.Storyteller.difficulty.CopyFrom(preset);
                CombatTacticalPlanner.ParallelEnabled=!GenCommandLine.CommandLineArgPassed("autonomousrimsynchronous");
                CombatTacticalPlanner.MinimumPairs=1;
            }
            if(Elite)
            {
                eliteRoster=map.mapPawns.FreeColonistsSpawned.ToList();
                Check(eliteRoster.Count==6 && Current.Game.Scenario.name=="AutonomousRim Enhanced World","Combat fixture requires six original Enhanced World colonists");
            }
            foreach(var pawn in map.mapPawns.AllPawnsSpawned.ToList()) { pawn.DeSpawn(); if(!eliteRoster.Contains(pawn))Find.WorldPawns.PassToWorld(pawn,PawnDiscardDecideMode.KeepForever); }
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
            if(Elite)
            {
                allies.Add(Spawn("Colonist",Faction.OfPlayer,center+new IntVec3(-15,0,-5),"Gun_BoltActionRifle",true));
                allies.Add(Spawn("Colonist",Faction.OfPlayer,center+new IntVec3(-15,0,5),"Gun_Revolver",true));
                Check(allies.Select(p=>p.thingIDNumber).ToHashSet().SetEquals(eliteRoster.Select(p=>p.thingIDNumber)),"Combat must preserve all six original pawn IDs");
            }
            foreach(var pawn in allies.Where(p=>!CombatManager.Ranged(p)))initialMelee.Add(pawn.thingIDNumber);
            if(PostBattleCare)
            {
                var fallbackDoor=door;
                // Controlled initial facilities/supplies. Subsequent damage, rescue,
                // tending and healing are native; no health edits after setup.
                var hospital=new CellRect(center.x-44,center.z-8,12,17);
                foreach(var cell in hospital)
                {
                    var offset=cell-center;
                    if(hospital.IsOnEdge(cell))Build(cell==center+new IntVec3(-33,0,0)?"Door":"Wall",offset.x,offset.z);
                    else map.terrainGrid.SetTerrain(cell,DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor"));
                    map.roofGrid.SetRoof(cell,RoofDefOf.RoofConstructed);map.areaManager.Home[cell]=true;
                }
                for(int i=0;i<ExpectedAllies;i++)
                {
                    var bed=(Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed,ThingDefOf.WoodLog);bed.SetFaction(Faction.OfPlayer);bed.Medical=true;
                    GenSpawn.Spawn(bed,center+new IntVec3(-42+i%3*3,0,5-i/3*4),map);
                }
                void careFurniture(string name,IntVec3 offset)
                {
                    var def=DefDatabase<ThingDef>.GetNamed(name);
                    var furniture=ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.WoodLog:null);furniture.SetFaction(Faction.OfPlayer);
                    GenSpawn.Spawn(furniture,center+offset,map);
                    var fuel=(furniture as ThingWithComps)?.TryGetComp<CompRefuelable>();
                    if(fuel!=null)fuel.Refuel(fuel.Props.fuelCapacity);
                }
                careFurniture("Table1x2c",new IntVec3(-40,0,-1));
                careFurniture("DiningChair",new IntVec3(-41,0,-1));careFurniture("DiningChair",new IntVec3(-39,0,-1));
                careFurniture("HorseshoesPin",new IntVec3(-35,0,-4));careFurniture("TorchLamp",new IntVec3(-38,0,2));
                for(int i=0;i<8;i++)
                {
                    var supply=Item("MealSurvivalPack");supply.stackCount=10;GenSpawn.Spawn(supply,center+new IntVec3(-42+i,0,-5),map);supply.SetForbidden(false,false);
                }
                var medicine=Item("MedicineHerbal");medicine.stackCount=30;GenSpawn.Spawn(medicine,center+new IntVec3(-41,0,-3),map);medicine.SetForbidden(false,false);
                initialTends=Record(allies,"TimesTendedOther");
                // Build() records doors; the hospital entrance must not replace
                // the combat refuge used by the retreat outcome assertion.
                door=fallbackDoor;
                Log.Message("[FiveCombatTrials] CARE SETUP: "+ExpectedAllies+" medical beds, table/chairs, recreation, fueled torch, 80 survival meals, 30 herbal medicine; native recovery only.");
            }
            Check(allies.Count(CombatManager.Ranged)==(Elite?4:2) && allies.Count==ExpectedAllies && allies.All(p=>p.apparel.WornApparel.Any(a=>a.def.defName=="Apparel_FlakVest" || Revision && a.def.defName=="Apparel_PlateArmor") &&
                p.apparel.WornApparel.Any(a=>a.def.defName=="Apparel_SimpleHelmet")),"Expected mixed ranged/melee roster with armor and helmets for every ally.");
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
                layout=hard?"desvantagem: superioridade mechanoid":Elite?"equilibrio numerico: seis contra seis mechs":"vantagem numerica: quatro contra dois mechs";
                for(int z=-6;z<=6;z++)if(z!=0)Build("Sandbags",-12,z);
            }
            int count=Revision && !Varied?(number==3?8:4):(hard?new[]{3,4,6,6,5}:new[]{3,4,4,4,2})[number-1];
            if(Elite)count=(hard?new[]{6,6,10,12,9}:new[]{4,6,9,8,6})[number-1];
            for(int i=0;i<count;i++)
            {
                var cell=center+new IntVec3(13+i%2,0,(i-count/2)*3);
                if(hard && number==3 && i>=count/2)cell=center+new IntVec3(-7+(i-count/2)*3,0,19);
                string kind=number==4?Pick("Wolf_Timber","WildBoar","Warg"):number==5?Pick("Mech_Scyther","Mech_Lancer"):"Colonist";
                string weapon=number<4?Pick("Gun_Revolver","Gun_Autopistol","Gun_BoltActionRifle","MeleeWeapon_Knife","MeleeWeapon_Mace"):null;
                if(Revision && !Varied)
                {
                    kind="Colonist";
                    weapon=number==1?"Gun_Revolver":number==2?"Bow_Short":number==3?"Gun_AssaultRifle":number==4?"MeleeWeapon_Knife":i<2?"Gun_Revolver":"MeleeWeapon_Mace";
                    // Short approach isolates cooperation rather than a prolonged firing-line crossing.
                    if(number==2 || number==5)cell=center+new IntVec3(-2+i%2,0,(i-count/2)*3);
                    if(number==4)cell=center+new IntVec3(4+i%2,0,(i-count/2)*3);
                }
                var enemy=Spawn(kind,number==4?null:Faction.OfAncientsHostile,cell,weapon,false); enemies.Add(enemy);
                if(Revision && !Varied && number==4)enemy.SetFaction(Faction.OfAncientsHostile);
                if(number==4 && (!Revision || Varied))Check(enemy.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent),"Manhunter setup failed.");
            }
            if(number!=4 || Revision && !Varied)LordMaker.MakeNewLord(Faction.OfAncientsHostile,new LordJob_AssaultColony(Faction.OfAncientsHostile,false,false,false,false,false),map,enemies);
            Check(!CombatManager.Enemies(map).Except(enemies).Any(),"Unrelated hostile pawn contaminates the fixture.");
            if(Revision && !Elite)
            {
                if(!Varied)layout=new[]{"portas e paredes: atiradores inimigos","cooperação melee contra arqueiros","retirada contra oito rifles","interceptação e proteção dos ranged","combate misto e flanqueamento"}[number-1];
                foreach(var p in allies)Log.Message("[FiveCombatTrials] SETUP CHECK "+p.LabelShort+" skills="+string.Join(",",p.skills.skills.Select(s=>s.def.defName+":"+s.Level))+" traits="+p.story.traits.allTraits.Count+" hediffs="+string.Join(",",p.health.hediffSet.hediffs.Select(h=>h.def.defName)));
                Check(allies.All(p=>p.skills.skills.All(s=>s.Level==20) && p.story.traits.allTraits.Count==0 && p.health.hediffSet.hediffs.Count==0),"Level-20 healthy trait-free starting colonists required.");
                Log.Message("[FiveCombatTrials] MELEE REVISION: Peaceful; all colonist skills 20; no initial hediffs or traits; parallel="+CombatTacticalPlanner.ParallelEnabled);
                foreach(var p in allies.Concat(enemies)){var value=CombatEquipmentScanner.Copy(p);Log.Message("[FiveCombatTrials] EQUIPMENT "+p.LabelShort+" power="+value.Power+" armorSharp="+value.Sharp+" armorBlunt="+value.Blunt+" penetration="+value.Penetration);}
            }
            weapons=string.Join(" | ",allies.Select(Manifest)); roster=string.Join(" | ",enemies.Select(Manifest));
            Log.Message($"[FiveCombatTrials] START case={number}; seed={seed}; hard={hard}; {layout}; "+(Elite?"six original EliteSix; 2 melee+4 ranged; Cassandra/Medium; preserved traits, skills, implants and scenario factors; ":"2 melee+2 ranged; ")+"armor="+
                (Elite?"2 steel plates, 4 flak vests, 6 helmets":Revision?"2 steel plates, 2 flak vests, 4 helmets":"4 flak vests, 4 helmets")+"; controlled initial combat facilities/equipment only; third speed selected; no scripted healing");
            Log.Message("[FiveCombatTrials] ALLIES "+weapons); Log.Message("[FiveCombatTrials] ENEMIES "+roster);
            start=Find.TickManager.TicksGame; started=true; ai.SetEmergencyAutomation(true);
        }
        private float Record(IEnumerable<Pawn> pawns,string name)=>pawns.Sum(p=>p.records?.GetValue(DefDatabase<RecordDef>.GetNamed(name))??0);
        private void Finish(string outcome,int ticks,bool neutralized,bool escaped)
        {
            var ai=map.GetComponent<AutonomousRimMapComponent>();
            if(!PostBattleCare || !neutralized || allies.Any(p=>p.Dead))ai.DisableAll();
            GameDataSaveLoader.SaveGame("AutonomousRim-Combate-Misto-"+number);
            string row=$"case={number}; seed={seed}; outcome={outcome}; ticks={ticks}; enemies={enemies.Count}; remaining={enemies.Count(p=>!p.Dead&&!p.Downed&&p.Spawned)}; deaths={allies.Count(p=>p.Dead)}; downed={allies.Count(p=>!p.Dead&&p.Downed)}; injured={allies.Count(p=>p.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury && h.Severity>0))}; neutralized={neutralized}; sheltered={escaped}; alliedShots={Record(allies,"ShotsFired")}; enemyShots={Record(enemies,"ShotsFired")}; alliedDamage={Record(allies,"DamageDealt").ToString(CultureInfo.InvariantCulture)}; coverSamples={cover}; interceptSamples={melee}; retreatSamples={retreat}; flankSamples={flankSamples}; approachSamples={approachSamples}; idleMeleeSamples={idleSamples}; firstMeleeTick={firstMeleeTick}; meleeDamage={Record(allies.Where(p=>initialMelee.Contains(p.thingIDNumber)),"DamageDealt")}; workerJobs={AnalysisWorker.Completed}; workerMs={AnalysisWorker.Milliseconds:F2}; combatCalls={CombatManager.AnalysisCalls}; combatMs={CombatManager.AnalysisMilliseconds:F2}";
            Log.Message("[FiveCombatTrials] RESULT "+row);
            combatResult=row;
            File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"result.txt"),row+Environment.NewLine+layout+Environment.NewLine+"ALLIES "+weapons+Environment.NewLine+"ENEMIES "+roster);
            foreach(var pawn in allies)Log.Message($"[FiveCombatTrials] COLONIST {pawn.LabelShort}: dead={pawn.Dead}; downed={pawn.Downed}; health={pawn.health.summaryHealth.SummaryHealthPercent}; bleed={pawn.health.hediffSet.BleedRateTotal}; position={pawn.Position}");
            if(PostBattleCare && neutralized && !allies.Any(p=>p.Dead))
            {
                Check(allies.Any(p=>p.Downed || p.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury)),"No injured combatant to validate post-battle care.");
                careStarted=true;careStart=Find.TickManager.TicksGame;
                ai.SetAutomation(false,true);ai.SetScheduleAutomation(true);ai.SetEmergencyAutomation(true);
                GameDataSaveLoader.SaveGame("CombatCareCheckpoint");
                Log.Message("[FiveCombatTrials] CARE START: victory recorded; recovery not yet approved.");return;
            }
            if(PostBattleCare)Log.Message("[FiveCombatTrials] CARE NOT VERIFIED: combat did not leave a living victorious team.");
            finished=true; Log.Message("[FiveCombatTrials] DONE"); Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
        }
        public override void MapComponentTick()
        {
            if(finished || !GenCommandLine.CommandLineArgPassed("autonomousrimcombatfive") || Find.TickManager.TicksGame<300)return;
            try
            {
                if((GenCommandLine.CommandLineArgPassed("autonomousrimcombatresume") || GenCommandLine.CommandLineArgPassed("autonomousrimcombatbaseline")) && !resumeLoaded)return;
                if(GenCommandLine.CommandLineArgPassed("autonomousrimcombatresume"))
                {
                    Check(started && careStarted && PostBattleCare && allies.Count==ExpectedAllies && !string.IsNullOrEmpty(combatResult),"Resume requires a native care checkpoint; participants cannot be replaced.");
                    if(!resumeResultLogged){resumeResultLogged=true;Log.Message("[FiveCombatTrials] RESULT "+combatResult+"; restoredFromCheckpoint=True");}
                }
                if(!started) { Setup(); return; }
                int ticks=Find.TickManager.TicksGame-start;
                var ai=map.GetComponent<AutonomousRimMapComponent>();
                if(careStarted)
                {
                    if(Find.TickManager.TicksGame%30!=0)return;
                    Check(allies.Count==ExpectedAllies && allies.All(p=>p!=null && !p.Dead),"An original combatant died during native recovery.");
                    var recurrentThreats=CombatManager.Enemies(map);
                    Check(!recurrentThreats.Except(enemies).Any(),"Unrelated new threat interrupted post-battle recovery.");
                    hospitalWitness|=allies.Any(p=>p.InBed() && p.CurrentBed()?.Medical==true);
                    float tends=Record(allies,"TimesTendedOther")-initialTends;
                    // A downed original enemy may recover naturally. Keep the
                    // original care deadline and let emergency/combat handle it.
                    bool recovered=recurrentThreats.Count==0 && allies.All(p=>p.Spawned && !p.Downed && !p.Drafted && !p.InMentalState &&
                        (p.needs.food?.CurLevelPercentage??1)>.15f && (p.needs.rest?.CurLevelPercentage??1)>.15f &&
                        p.health.hediffSet.BleedRateTotal==0 && !p.health.HasHediffsNeedingTend() &&
                        !HealthAIUtility.ShouldSeekMedicalRest(p) && p.health.summaryHealth.SummaryHealthPercent>=.9f);
                    if(recovered && hospitalWitness && tends>0 && ai.Emergency.Phase==EmergencyPhase.Normal)
                    {
                        ai.DisableAll();GameDataSaveLoader.SaveGame("CombatCareComplete");
                        Log.Message("[FiveCombatTrials] CARE PASS: "+ExpectedAllies+" original combatants alive, native hospital/tending, no bleeding or medical rest, health >=90%, undrafted, emergency returned to Normal; residual scars/injuries may remain.");
                        finished=true;Log.Message("[FiveCombatTrials] DONE");Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;return;
                    }
                    if(Find.TickManager.TicksGame%2490==0)
                    {
                        GameDataSaveLoader.SaveGame("CombatCareCheckpoint");
                        Log.Message($"[FiveCombatTrials] CARE PROGRESS hours={(Find.TickManager.TicksGame-careStart)/2500f:F2}; tends={tends}; hospital={hospitalWitness}; recovered={recovered}; recurrentThreats={recurrentThreats.Count}; emergency={ai.Emergency.Phase}");
                    }
                    Check(Find.TickManager.TicksGame-careStart<GenDate.TicksPerDay*8,"Native recovery exceeded eight days without meeting its criteria.");return;
                }
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
            catch(Exception ex)
            {
                if(PostBattleCare && started)GameDataSaveLoader.SaveGame("CombatCareFailure");
                finished=true; Log.Error("[FiveCombatTrials] FAIL case="+number+": "+ex); Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
            }
        }
    }
}
