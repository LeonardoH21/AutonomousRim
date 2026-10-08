using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Perception;
using AutonomousRim.Planning;
using AutonomousRim.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Execution
{
    // Defensive native jobs only. Ownership prevents overriding a player's
    // draft/order; excluded pawns stay manual until this engagement ends.
    public static class CombatManager
    {
        public static long AnalysisCalls;
        public static double AnalysisMilliseconds;
        public const int Interval = 15;
        private static readonly Dictionary<(int Pawn,IntVec3 Cell),float> shotChances=new Dictionary<(int,IntVec3),float>();
        private static int shotChanceTick=-1;
        public static float ExpectedRangedDps(Pawn shooter,IntVec3 cell,float rawDps)
        {
            int tick=Find.TickManager.TicksGame;
            if(tick!=shotChanceTick){shotChances.Clear();shotChanceTick=tick;}
            var key=(shooter.thingIDNumber,cell);
            if(!shotChances.TryGetValue(key,out float chance))
            {
                var verb=shooter.equipment?.Primary?.TryGetComp<CompEquippable>()?.PrimaryVerb;
                chance=verb?.CanHitTargetFrom(shooter.Position,cell)==true?ShotReport.HitReportFor(shooter,verb,cell).TotalEstimatedHitChance:0;
                shotChances[key]=chance;
            }
            return rawDps*chance;
        }
        public static bool Ranged(Pawn p) => p.equipment?.Primary?.def.IsRangedWeapon == true;
        public static List<Pawn> Enemies(Map map) => map.mapPawns.AllPawnsSpawned
            .Where(ThreatScanner.Active).ToList();
        public static bool Eligible(Pawn p) => p.Spawned && p.Faction == Faction.OfPlayer && p.IsColonist &&
            PawnAnalyzer.IsCombatReady(p) && !MedicalRescueManager.Reserved(p) && !PrisonManager.Reserved(p) && p.drafter != null && p.equipment?.Primary != null &&
            (p.needs?.rest?.CurLevel ?? 1) > 0.08f &&
            (p.equipment.Primary.TryGetComp<CompEquippable>()?.PrimaryVerb?.verbProps.defaultProjectile?.projectile?.explosionRadius ?? 0) <= 0;

        public static bool FriendlyLane(IntVec3 from, IntVec3 to, Pawn shooter, IEnumerable<Pawn> allies)
        {
            float dx = to.x-from.x, dz = to.z-from.z, length = dx*dx+dz*dz;
            if (length <= 0) return true;
            foreach (var friend in allies.Where(a => a != shooter && a.Spawned && !a.Dead))
            {
                if (friend.Position.DistanceTo(from) < 5f) continue; // Native nearby-friendly protection.
                float t = ((friend.Position.x-from.x)*dx+(friend.Position.z-from.z)*dz)/length;
                if (t <= 0 || t >= 1) continue;
                float x = from.x+t*dx-friend.Position.x, z = from.z+t*dz-friend.Position.z;
                if (x*x+z*z < 1.5f) return false;
            }
            return true;
        }
        private static bool SafeCell(Map map, Pawn p, IntVec3 c) => c.InBounds(map) && c.Standable(map) &&
            !c.Fogged(map) && !c.GetThingList(map).OfType<Pawn>().Any(other => other != p && !other.Downed) &&
            !c.GetThingList(map).Any(t => t.def.defName == "Fire") &&
            (c.GetEdifice(map) as Building_Door)?.Open != false;
        private static float Distance(IntVec3 c, IEnumerable<Pawn> enemies) => enemies.Min(e => c.DistanceTo(e.Position));
        private static bool Wounded(Pawn p) => p.health.summaryHealth.SummaryHealthPercent < 0.65f ||
            p.health.hediffSet.BleedRateTotal > 0 && HealthUtility.TicksUntilDeathDueToBloodLoss(p)<12000 ||
            p.health.hediffSet.PainTotal > p.GetStatValue(StatDefOf.PainShockThreshold)*0.9f ||
            (p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0)>0.3f ||
            (p.needs?.rest?.CurLevel ?? 1)<0.08f;
        private static bool Overwhelmed(Pawn p, List<Pawn> allies, List<Pawn> enemies)
        {
            var near = enemies.Where(e => e.Position.DistanceTo(p.Position) < 16).ToList();
            var help = allies.Where(a => !a.Downed && a.Position.DistanceTo(p.Position) < 18).ToList();
            return near.Count > 0 && (near.Count > help.Count * 2 ||
                near.Sum(PawnAnalyzer.EstimateCombatValue) > Math.Max(1, help.Sum(PawnAnalyzer.EstimateCombatValue)) * 1.65f);
        }
        private static Pawn Target(Pawn p, List<Pawn> allies, List<Pawn> enemies)
        {
            var current=p.CurJob?.targetA.Thing as Pawn;
            if(Ranged(p) && p.CurJob?.def==JobDefOf.AttackStatic && current!=null && enemies.Contains(current) &&
                p.equipment.Primary.TryGetComp<CompEquippable>().PrimaryVerb.CanHitTarget(current) && FriendlyLane(p.Position,current.Position,p,allies)) return current;
            return enemies.OrderByDescending(e => allies.Any(a => a != p && a.Position.DistanceTo(p.Position) < 18 &&
                (e.CurJob?.targetA.Thing == a || !Ranged(e) && e.Position.DistanceTo(a.Position) < 5)))
            .ThenBy(e => e.Position.DistanceTo(p.Position)).First();
        }

        private static IntVec3 Position(Map map, CombatOrder order, Pawn target, List<Pawn> enemies, List<Pawn> allies, bool retreat, HashSet<IntVec3> occupied)
        {
            var p = order.Pawn;
            if(retreat && Shelter(map,p.Position,enemies) && enemies.All(e=>!GenSight.LineOfSight(e.Position,p.Position,map))) return p.Position;
            var protection=CombatEquipmentScanner.Copy(p);
            var shooters=enemies.Where(Ranged).Select(e=>new { Pawn=e,Stats=CombatEquipmentScanner.Copy(e),Verb=e.equipment.Primary.TryGetComp<CompEquippable>()?.PrimaryVerb }).ToList();
            var verb = p.equipment.Primary.TryGetComp<CompEquippable>()?.PrimaryVerb;
            float range = verb?.verbProps.range ?? 1.5f;
            float min = verb?.verbProps.minRange ?? 0;
            float preferred = Math.Max(min+3, Math.Min(range*0.72f, 24));
            var candidates = GenRadial.RadialCellsAround(p.Position, retreat ? 12 : 8, true)
                .Concat(GenRadial.RadialCellsAround(order.Anchor, 6, true)).Distinct().Where(c => SafeCell(map,p,c) && !occupied.Contains(c));
            if (retreat)
                candidates = candidates.Where(c => c == p.Position ||
                    Distance(c,enemies)>Distance(p.Position,enemies)+1 || Shelter(map,c,enemies));
            var ranked = candidates.Select(c =>
            {
                float d = c.DistanceTo(target.Position);
                float nearest = Distance(c,enemies);
                bool sight = Ranged(p) ? verb != null && verb.CanHitTargetFrom(c,target) : GenSight.LineOfSight(c,target.Position,map);
                float cover = sight ? CoverUtility.CalculateOverallBlockChance(c,target.Position,map) : 0;
                float move = c.DistanceTo(p.Position);
                // Evaluate every incoming firing lane, not just the selected target or our own gun's range.
                float incoming=shooters.Where(e=>e.Pawn.CurJob?.def!=JobDefOf.AttackMelee).Sum(e=>ExpectedRangedDps(e.Pawn,c,e.Stats.Dps)*
                    TacticalPolicy.DamageFraction(e.Stats.BluntDamage?protection.Blunt:protection.Sharp,e.Stats.Penetration));
                float score;
                if (retreat) score = nearest*2.5f + (incoming<=0 ? 12 : cover*15) - incoming*12 - move*0.65f - c.DistanceTo(order.Anchor)*0.08f + (Shelter(map,c,enemies)?45:0);
                else if (Ranged(p))
                {
                    bool shot = sight && d <= range && d >= min && FriendlyLane(c,target.Position,p,allies);
                    score = (shot ? 18 : -25)+cover*28-Math.Abs(d-preferred)*0.15f-move*0.8f;
                    foreach (var e in enemies.Where(e => !Ranged(e)))
                    {
                        float danger = Math.Max(5,e.GetStatValue(StatDefOf.MoveSpeed)*1.5f+2);
                        if(c.DistanceTo(e.Position)<danger) score -= (danger-c.DistanceTo(e.Position))*10;
                    }
                    score -= incoming*6;
                    if(incoming>protection.Dps*1.25f && !allies.Any(a=>!Ranged(a) && enemies.Any(e=>a.Position.DistanceTo(e.Position)<2)))score-=30;
                }
                else
                {
                    int walls = GenAdj.CardinalDirections.Count(v => (c+v).InBounds(map) && (c+v).GetEdifice(map)?.def.passability==Traversability.Impassable);
                    float friendDistance = allies.Where(Ranged).Select(a=>c.DistanceTo(a.Position)).DefaultIfEmpty(4).Min();
                    score = Math.Min(2,walls)*7-Math.Abs(friendDistance-3)*2-Math.Abs(d-4)*0.2f-move*0.8f;
                    score -= c.DistanceTo(order.Anchor)*0.8f;
                    if(enemies.All(Ranged)) score+=cover*28;
                    score-=incoming*8; // Interceptors wait behind cover until a supported approach is feasible.
                    if (allies.Any(a=>Ranged(a) && c.DistanceTo(a.Position)<d)) score+=3;
                }
                score -= Math.Max(0,c.DistanceTo(order.Anchor)-18)*2; // Never pursue far into hostile terrain.
                score -= c.GetTerrain(map).pathCost*(retreat?0.15f:0.05f);
                return new { Cell=c, Score=score };
            }).OrderByDescending(x=>x.Score).Take(12);
            if(!Ranged(p) && !retreat)
            {
                var here=ranked.FirstOrDefault(x=>x.Cell==p.Position);
                if(here!=null && ranked.First().Score-here.Score<2) return p.Position;
            }
            foreach (var cell in ranked)
            {
                if(!p.CanReach(cell.Cell,PathEndMode.OnCell,Danger.Deadly)) continue;
                if(retreat)
                {
                    using(var path=map.pathFinder.FindPathNow(p.Position,cell.Cell,TraverseParms.For(p,Danger.Deadly)))
                    {
                        if(!path.Found || path.NodesReversed.Any(node=>enemies.Any(e=>node.DistanceTo(e.Position)<Math.Min(3,p.Position.DistanceTo(e.Position))-0.1f) ||
                            node.GetThingList(map).Any(t=>t.def.defName=="Fire"))) continue;
                    }
                }
                return cell.Cell;
            }
            return p.Position;
        }
        private static bool Shelter(Map map,IntVec3 cell,List<Pawn> enemies)
        {
            var room=cell.GetRoom(map);
            return !(cell.GetEdifice(map) is Building_Door) && room!=null && !room.PsychologicallyOutdoors && room.CellCount<120 &&
                enemies.All(e=>e.Position.GetRoom(map)!=room);
        }
        private static Pawn ContactEnemy(Pawn pawn,List<Pawn> enemies)=>enemies.Where(e=>e.Position.DistanceTo(pawn.Position)<1.6f &&
            GenSight.LineOfSight(pawn.Position,e.Position,pawn.Map,true) && pawn.CanReach(e,PathEndMode.Touch,Danger.Deadly))
            .OrderBy(e=>e.health.summaryHealth.SummaryHealthPercent).FirstOrDefault();
        private static bool CanSupport(Map map,Pawn friend,Pawn melee,Pawn target,List<Pawn> allies)
        {
            if(friend==melee)return true;
            if(!Ranged(friend))return friend.Position.DistanceTo(target.Position)<12 ||
                friend.Position.DistanceTo(melee.Position)<8 && friend.CanReach(target,PathEndMode.Touch,Danger.Deadly);
            var verb=friend.equipment.Primary.TryGetComp<CompEquippable>()?.PrimaryVerb;
            if(verb==null)return false;
            if(verb.CanHitTarget(target) && FriendlyLane(friend.Position,target.Position,friend,allies))return true;
            // A repositioning shooter is support only if a nearby reachable firing
            // cell exists. Do not cancel an approach for a transient blocked lane.
            if(friend.Position.DistanceTo(melee.Position)>12)return false;
            return GenRadial.RadialCellsAround(friend.Position,4,true).Any(c=>SafeCell(map,friend,c) &&
                verb.CanHitTargetFrom(c,target) && FriendlyLane(c,target.Position,friend,allies) &&
                friend.CanReach(c,PathEndMode.OnCell,Danger.Deadly));
        }
        private static bool Issue(CombatOrder order, Job job, string role)
        {
            var p=order.Pawn;
            if (p.CurJob?.def==job.def && p.CurJob.targetA==job.targetA) { order.Role=role; return false; }
            job.expiryInterval=job.def==JobDefOf.AttackStatic?1800:600; job.checkOverrideOnExpire=true;
            job.preventFriendlyFire=true;
            if(job.def==JobDefOf.Goto) job.locomotionUrgency=LocomotionUrgency.Sprint;
            if (!p.jobs.TryTakeOrderedJob(job,JobTag.Misc,false)) return false;
            order.JobId=job.GetUniqueLoadID(); order.LastOrderTick=Find.TickManager.TicksGame; order.Role=role;
            Log.Message($"[AutonomousRim.Combat] {p.LabelShort}: {role}; {job.def.defName} → {job.targetA}");
            return true;
        }
        public static string Apply(Map map,List<CombatOrder> orders,List<Pawn> excluded,bool globalRetreat=false)
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            try { return ApplyCore(map,orders,excluded,globalRetreat); }
            finally { AnalysisCalls++; AnalysisMilliseconds+=timer.Elapsed.TotalMilliseconds; }
        }
        private static string ApplyCore(Map map,List<CombatOrder> orders,List<Pawn> excluded,bool globalRetreat)
        {
            shotChances.Clear(); // Native geometry/stat reports remain on the game thread and expire every decision.
            var enemies=Enemies(map);
            if(enemies.Count==0) { CombatTacticalPlanner.Clear(map); Stop(orders); excluded.Clear(); return "Combate: sem ameaças; colonos da IA liberados para a rotina."; }
            var colonists=map.mapPawns.FreeColonistsSpawned.ToList();
            foreach(var order in orders.ToList())
            {
                var p=order.Pawn;
                if(p==null || !p.Spawned || p.Dead || !PawnAnalyzer.IsCombatReady(p)) { Stop(new List<CombatOrder>{order}); orders.Remove(order); continue; }
                if(!p.Drafted || p.CurJob?.playerForced==true && p.CurJob.GetUniqueLoadID()!=order.JobId)
                { p.drafter.FireAtWill=order.OriginalFireAtWill; excluded.Add(p); orders.Remove(order); }
            }
            foreach(var p in colonists.Where(Eligible).Where(p=>!p.Drafted && !excluded.Contains(p) &&
                !orders.Any(o=>o.Pawn==p) && p.CurJob?.playerForced!=true && enemies.Any(e=>e.Position.DistanceTo(p.Position)<50)))
            {
                p.drafter.Drafted=true;
                bool fire=p.drafter.FireAtWill; p.drafter.FireAtWill=false;
                orders.Add(new CombatOrder { Pawn=p, Anchor=p.Position, LastOrderTick=-300, OriginalFireAtWill=fire });
            }
            var allies=orders.Select(o=>o.Pawn).Where(PawnAnalyzer.IsCombatReady).ToList();
            var assessments=CombatTacticalPlanner.Analyze(map,allies,enemies);
            var assignments=new Dictionary<int,int>();
            var occupied=new HashSet<IntVec3>();
            foreach(var order in orders.OrderByDescending(o=>Ranged(o.Pawn)))
            {
                var p=order.Pawn;
                if(!PawnAnalyzer.IsCombatReady(p)) continue;
                var target=Target(p,colonists,enemies);
                if(!Ranged(p))
                {
                    var ranked=assessments.Where(a=>a.PawnId==p.thingIDNumber).OrderByDescending(a=>a.Score+
                        (assignments.Count(pair=>pair.Value==a.TargetId)==1?8:assignments.Count(pair=>pair.Value==a.TargetId)>1?-10:0)).ToList();
                    var existing=p.CurJob?.targetA.Thing as Pawn;
                    if(p.CurJob?.def==JobDefOf.AttackMelee && existing!=null && enemies.Contains(existing) &&
                        p.CanReach(existing,PathEndMode.Touch,Danger.Deadly))target=existing;
                    else if(order.MeleeTarget!=null && enemies.Contains(order.MeleeTarget) &&
                        Find.TickManager.TicksGame-order.MeleeTargetTick<240 &&
                        order.MeleeTarget.Position.DistanceTo(order.Anchor)<=32 &&
                        p.CanReach(order.MeleeTarget,PathEndMode.Touch,Danger.Deadly))target=order.MeleeTarget;
                    else target=ranked.Select(a=>enemies.FirstOrDefault(e=>e.thingIDNumber==a.TargetId &&
                        p.CanReach(e,PathEndMode.Touch,Danger.Deadly))).FirstOrDefault(e=>e!=null)??target;
                    assignments[p.thingIDNumber]=target.thingIDNumber;
                }
                bool danger=globalRetreat||Wounded(p)||Overwhelmed(p,allies,enemies);
                if(!Ranged(p) && colonists.Any(a=>a!=p && a.equipment?.Primary!=null && !Ranged(a) &&
                    a.Position.DistanceTo(p.Position)<24 && (!PawnAnalyzer.IsCombatReady(a) || Wounded(a) || orders.Any(o=>o.Pawn==a && o.Retreated))) &&
                    enemies.Count(e=>e.Position.DistanceTo(p.Position)<14)>1 &&
                    !orders.Any(o=>o.Pawn!=p && !Ranged(o.Pawn) && !o.Retreated && !Wounded(o.Pawn) && o.Pawn.Position.DistanceTo(p.Position)<8))
                    danger=true; // Withdraw the pair together rather than leaving one interceptor surrounded.
                if(danger && !order.Retreated) { order.Retreated=true; order.RetreatUntilTick=Find.TickManager.TicksGame+600; }
                if(order.Retreated && !danger && Find.TickManager.TicksGame>order.RetreatUntilTick &&
                    Distance(p.Position,enemies)>10 && enemies.Count<=allies.Count &&
                    enemies.Sum(PawnAnalyzer.EstimateCombatValue)<allies.Sum(PawnAnalyzer.EstimateCombatValue)*1.2f)
                    order.Retreated=false;
                var touching=ContactEnemy(p,enemies);
                if(touching!=null && touching.GetStatValue(StatDefOf.MoveSpeed)>=p.GetStatValue(StatDefOf.MoveSpeed)*0.95f &&
                    !orders.Any(o=>o.Pawn!=p && !o.Retreated && !Ranged(o.Pawn) && !Wounded(o.Pawn) &&
                        o.Pawn.Position.DistanceTo(touching.Position)<1.6f))
                {
                    // A slower pawn cannot kite an attacker already in contact.
                    // Keep attacking instead of repeatedly turning its back to run.
                    Issue(order,JobMaker.MakeJob(JobDefOf.AttackMelee,touching),"Melee: defender contato sem velocidade para escapar");
                    occupied.Add(p.Position); continue;
                }
                if(!order.Retreated && !Ranged(p))
                {
                    var support=orders.Where(o=>!o.Retreated && !Wounded(o.Pawn) && PawnAnalyzer.IsCombatReady(o.Pawn) &&
                        CanSupport(map,o.Pawn,p,target,colonists))
                        .Select(o=>o.Pawn).ToList();
                    var localEnemies=enemies.Where(e=>e.Position.DistanceTo(target.Position)<14).ToList();
                    var liveAssessment=TacticalPolicy.Assess(support.Select(CombatEquipmentScanner.Copy).ToArray(),localEnemies.Select(CombatEquipmentScanner.Copy).ToArray());
                    float advantage=liveAssessment.FirstOrDefault(a=>a.PawnId==p.thingIDNumber && a.TargetId==target.thingIDNumber)?.Advantage??0;
                    if(p.CurJob?.def==JobDefOf.Goto && p.CurJob.GetUniqueLoadID()==order.JobId &&
                        order.Role?.StartsWith("Melee:")==true && Find.TickManager.TicksGame-order.LastOrderTick<150 &&
                        p.CurJob.targetA.Cell.DistanceTo(target.Position)<5 && advantage>=0.85f &&
                        !occupied.Contains(p.CurJob.targetA.Cell) && !p.CurJob.targetA.Cell.GetThingList(map).Any(t=>t is Fire))
                    { occupied.Add(p.CurJob.targetA.Cell); continue; }
                    var meleePlan=MeleeTactics.PlanAttack(map,p,target,support,enemies,order.Anchor,occupied,assignments,advantage);
                    if(meleePlan!=null)
                    {
                        if(order.MeleeTarget!=target){order.MeleeTarget=target;order.MeleeTargetTick=Find.TickManager.TicksGame;}
                        // Keep native warmups and attacks intact. Reissue only on a changed target/approach.
                        if(p.CurJob?.def==JobDefOf.AttackMelee && p.CurJob.targetA.Thing==target &&
                            (p.stances.FullBodyBusy || p.Position.DistanceTo(target.Position)<3))
                            order.Role=meleePlan.Role;
                        else if(meleePlan.Strike)
                            Issue(order,JobMaker.MakeJob(JobDefOf.AttackMelee,target),meleePlan.Role);
                        else Issue(order,JobMaker.MakeJob(JobDefOf.Goto,meleePlan.Approach),meleePlan.Role);
                        occupied.Add(meleePlan.Approach); continue;
                    }
                    if(order.Role?.StartsWith("Melee:")==true && p.CurJob?.GetUniqueLoadID()==order.JobId &&
                        (p.CurJob.def==JobDefOf.Goto || p.CurJob.def==JobDefOf.AttackMelee && p.Position.DistanceTo(target.Position)>=3.5f))
                        Issue(order,JobMaker.MakeJob(JobDefOf.Wait_Combat),"Melee: avanço suspenso por falta de apoio");
                }
                // Recovery requires a cooldown, spacing and a favorable force ratio.
                bool retreat=order.Retreated;
                if(retreat && p.CurJob?.def==JobDefOf.Goto && p.CurJob.GetUniqueLoadID()==order.JobId &&
                    Find.TickManager.TicksGame-order.LastOrderTick<90 && !occupied.Contains(p.CurJob.targetA.Cell) &&
                    (Distance(p.CurJob.targetA.Cell,enemies)>Distance(p.Position,enemies)+1 || Shelter(map,p.CurJob.targetA.Cell,enemies)) &&
                    !p.CurJob.targetA.Cell.GetThingList(map).Any(t=>t is Fire))
                { occupied.Add(p.CurJob.targetA.Cell); continue; }
                bool pressure=enemies.Any(e=>!Ranged(e) && e.Position.DistanceTo(p.Position)<5 &&
                    !orders.Any(o=>!o.Retreated && !Ranged(o.Pawn) && !o.Pawn.Downed && o.Pawn.Position.DistanceTo(e.Position)<2.5f));
                if(!retreat && !pressure && p.stances.FullBodyBusy &&
                    (p.CurJob?.def==JobDefOf.AttackStatic && p.CurJob.targetA.Thing==target &&
                        p.equipment.Primary.TryGetComp<CompEquippable>().PrimaryVerb.CanHitTarget(target) && FriendlyLane(p.Position,target.Position,p,colonists) ||
                     p.CurJob?.def==JobDefOf.AttackMelee && p.CurJob.targetA.Thing==target && target.Position.DistanceTo(p.Position)<3.5f)) { occupied.Add(p.Position); continue; }
                if(Find.TickManager.TicksGame-order.LastOrderTick<180 && !retreat && !pressure)
                { occupied.Add(p.CurJob?.def==JobDefOf.Goto?p.CurJob.targetA.Cell:p.Position); continue; }
                var position=Position(map,order,target,enemies,colonists,retreat,occupied);
                if(retreat)
                {
                    bool canPause=!Overwhelmed(p,allies,enemies) && p.health.summaryHealth.SummaryHealthPercent>=0.8f &&
                        (!enemies.Any(e=>!Ranged(e)) || enemies.Where(e=>!Ranged(e)).All(e=>e.GetStatValue(StatDefOf.MoveSpeed)<=p.GetStatValue(StatDefOf.MoveSpeed)));
                    if(Ranged(p) && canPause && Distance(p.Position,enemies)>Math.Max(8,enemies.Max(e=>e.GetStatValue(StatDefOf.MoveSpeed))*1.5f+2) &&
                        (CoverUtility.CalculateOverallBlockChance(p.Position,target.Position,map)>0.25f || enemies.All(e=>!Ranged(e))) &&
                        p.equipment.Primary.TryGetComp<CompEquippable>().PrimaryVerb.CanHitTarget(target) && FriendlyLane(p.Position,target.Position,p,colonists))
                        Issue(order,JobMaker.MakeJob(JobDefOf.AttackStatic,target),"Recuo: fogo de cobertura");
                    else if(position!=p.Position && (Distance(position,enemies)>Distance(p.Position,enemies)+1 || Shelter(map,position,enemies)) && !occupied.Contains(position))
                        Issue(order,JobMaker.MakeJob(JobDefOf.Goto,position),"Recuo protegido");
                    else if(ContactEnemy(p,enemies) is Pawn contact)
                        Issue(order,JobMaker.MakeJob(JobDefOf.AttackMelee,contact),"Recuo: defender contato sem rota segura");
                    else Issue(order,JobMaker.MakeJob(JobDefOf.Wait_Combat),"Recuo: manter abrigo");
                }
                else if(!Ranged(p) && target.Position.DistanceTo(p.Position)<1.6f &&
                    (!Ranged(target) || target.Position.DistanceTo(p.Position)<1.6f) &&
                    (target.Position.DistanceTo(p.Position)<1.6f || allies.Any(a=>Ranged(a) && a.equipment.Primary.TryGetComp<CompEquippable>().PrimaryVerb.CanHitTarget(target))) &&
                    allies.Count(a=>Ranged(a)?a.equipment.Primary.TryGetComp<CompEquippable>().PrimaryVerb.CanHitTarget(target):a.Position.DistanceTo(target.Position)<8)>=
                        enemies.Count(e=>e.Position.DistanceTo(target.Position)<8) &&
                    p.CanReach(target,PathEndMode.Touch,Danger.Deadly))
                    Issue(order,JobMaker.MakeJob(JobDefOf.AttackMelee,target),"Interceptar / ajudar aliado");
                else if(Ranged(p) && (position.DistanceTo(p.Position)<=2 ||
                    CoverUtility.CalculateOverallBlockChance(position,target.Position,map)-CoverUtility.CalculateOverallBlockChance(p.Position,target.Position,map)<0.15f) &&
                    p.equipment.Primary.TryGetComp<CompEquippable>().PrimaryVerb.CanHitTarget(target) &&
                    FriendlyLane(p.Position,target.Position,p,colonists) && !pressure)
                {
                    // Keep aiming/burst jobs intact rather than resetting every scan.
                    Issue(order,JobMaker.MakeJob(JobDefOf.AttackStatic,target),"Cobertura / fogo de apoio");
                }
                else if(position!=p.Position && !occupied.Contains(position))
                    Issue(order,JobMaker.MakeJob(JobDefOf.Goto,position),Ranged(p)?"Reposicionar atirador":"Guardar passagem / proteger atirador");
                else if(ContactEnemy(p,enemies) is Pawn contact)
                    Issue(order,JobMaker.MakeJob(JobDefOf.AttackMelee,contact),"Melee: defender aliado em contato");
                else Issue(order,JobMaker.MakeJob(JobDefOf.Wait_Combat),"Manter posição defensiva");
                occupied.Add(position);
            }
            return $"Combate cooperativo: {orders.Count} controlados; {orders.Count(o=>o.Retreated)} em recuo; {excluded.Count} sob controle manual. {enemies.Count} ameaças.";
        }
        public static void Stop(List<CombatOrder> orders)
        {
            foreach(var order in orders)
            {
                var p=order.Pawn;
                if(p?.drafter==null) continue;
                p.drafter.FireAtWill=order.OriginalFireAtWill;
                if(p.CurJob?.GetUniqueLoadID()==order.JobId) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                if(p.CurJob?.playerForced!=true) p.drafter.Drafted=false;
            }
            orders.Clear();
        }
    }
}

