using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.Planning
{
    public static class MeleeTactics
    {
        public sealed class Plan
        {
            public Pawn Target;
            public IntVec3 Approach;
            public bool Strike;
            public string Role;
        }
        public static Plan PlanAttack(Map map,Pawn pawn,Pawn target,List<Pawn> allies,List<Pawn> enemies,
            IntVec3 anchor,HashSet<IntVec3> occupied,Dictionary<int,int> assignments,float advantage)
        {
            float distance=pawn.Position.DistanceTo(target.Position);
            bool adjacent=distance<1.6f && GenSight.LineOfSight(pawn.Position,target.Position,map,true);
            var friends=allies.Where(a=>!a.Downed && a.Position.DistanceTo(target.Position)<26).ToList();
            var nearby=enemies.Where(e=>e.Position.DistanceTo(target.Position)<14).ToList();
            // Let larger melee groups enter our defended line. Charging out would
            // surrender nearby-friendly shooting protection and expose both sides.
            if(!CombatManager.Ranged(target) && distance>3.5f && nearby.All(e=>!CombatManager.Ranged(e)) &&
                nearby.Count>friends.Count(a=>!CombatManager.Ranged(a)) && advantage<2.4f)return null;
            if(!pawn.CanReach(target,PathEndMode.Touch,Danger.Deadly) || target.Position.DistanceTo(anchor)>32)return null;
            var own=CombatEquipmentScanner.Copy(pawn);
            // Arrival time matters: armor reduces expected exposure, never removes it.
            float exposure=0;
            foreach(var enemy in enemies.Where(CombatManager.Ranged))
            {
                var weapon=CombatEquipmentScanner.Copy(enemy);
                if(enemy.Position.DistanceTo(pawn.Position)<=weapon.Range && GenSight.LineOfSight(enemy.Position,pawn.Position,map))
                    exposure+=CombatManager.ExpectedRangedDps(enemy,pawn.Position,weapon.Dps)*TacticalPolicy.DamageFraction(weapon.BluntDamage?own.Blunt:own.Sharp,weapon.Penetration)*
                        Math.Min(10,distance/Math.Max(1,own.Speed));
            }
            bool help=allies.Any(a=>a!=pawn && (target.CurJob?.targetA.Thing==a || !CombatManager.Ranged(target) && target.Position.DistanceTo(a.Position)<4));
            if(!adjacent && !TacticalPolicy.CanPress(own.Health,advantage,friends.Count,nearby.Count,distance,exposure,nearby.Any(e=>CombatEquipmentScanner.Copy(e).Special)))return null;
            var options=GenAdj.AdjacentCells.Select(d=>target.Position+d).Where(c=>c.InBounds(map) && c.Standable(map) &&
                !occupied.Contains(c) && !c.GetThingList(map).OfType<Pawn>().Any(p=>p!=pawn && !p.Downed) &&
                !c.GetThingList(map).Any(t=>t is Fire) && GenSight.LineOfSight(c,target.Position,map,true))
                .Select(c=>new { Cell=c,Score=Score(map,pawn,target,c,allies,nearby,assignments) })
                .OrderByDescending(c=>c.Score).ToList();
            if(adjacent)return new Plan{Target=target,Approach=pawn.Position,Strike=true,Role=help?"Melee: proteger aliado":"Melee: manter pressão"};
            foreach(var option in options)
            {
                if(!pawn.CanReach(option.Cell,PathEndMode.OnCell,Danger.Deadly))continue;
                using(var path=map.pathFinder.FindPathNow(pawn.Position,option.Cell,TraverseParms.For(pawn,Danger.Deadly)))
                {
                    if(!path.Found || path.NodesReversed.Any(c=>c.GetThingList(map).Any(t=>t is Fire)))continue;
                    // Do not cross another melee's reach to chase a shooter behind it.
                    if(path.NodesReversed.Any(c=>nearby.Any(e=>e!=target && !CombatManager.Ranged(e) && c.DistanceTo(e.Position)<2 &&
                        !allies.Any(a=>a!=pawn && !CombatManager.Ranged(a) && a.Position.DistanceTo(e.Position)<2))))continue;
                    if(distance>3 && path.NodesReversed.Count>Math.Min(36,distance*1.8f+5))continue;
                    // A wall at the starting cell must not hide the exposure of
                    // the rest of the route. Estimate the time actually in firing lanes.
                    float routeExposure=0;
                    foreach(var shooter in enemies.Where(CombatManager.Ranged))
                    {
                        var weapon=CombatEquipmentScanner.Copy(shooter);
                        float damage=path.NodesReversed.Sum(c=>CombatManager.ExpectedRangedDps(shooter,c,weapon.Dps));
                        routeExposure+=damage*TacticalPolicy.DamageFraction(weapon.BluntDamage?own.Blunt:own.Sharp,weapon.Penetration)/Math.Max(1,own.Speed);
                    }
                    if(!TacticalPolicy.CanPress(own.Health,advantage,friends.Count,nearby.Count,distance,routeExposure,nearby.Any(e=>CombatEquipmentScanner.Copy(e).Special)))continue;
                }
                bool flank=allies.Any(a=>a!=pawn && !CombatManager.Ranged(a) &&
                    (a.Position.DistanceTo(target.Position)<4 || assignments.TryGetValue(a.thingIDNumber,out int id) && id==target.thingIDNumber));
                return new Plan{Target=target,Approach=option.Cell,Strike=distance<2.8f,
                    Role=help?"Melee: cooperar no resgate":flank?"Melee: flanco coordenado":"Melee: aproximar com vantagem"};
            }
            return null;
        }
        private static float Score(Map map,Pawn pawn,Pawn target,IntVec3 c,List<Pawn> allies,List<Pawn> enemies,Dictionary<int,int> assignments)
        {
            float score=-pawn.Position.DistanceTo(c)*1.5f-c.GetTerrain(map).pathCost*0.08f;
            foreach(var partner in allies.Where(a=>a!=pawn && !CombatManager.Ranged(a) &&
                (a.Position.DistanceTo(target.Position)<4 || assignments.TryGetValue(a.thingIDNumber,out int id) && id==target.thingIDNumber)))
            {
                float ax=c.x-target.Position.x,az=c.z-target.Position.z;
                float bx=partner.Position.x-target.Position.x,bz=partner.Position.z-target.Position.z;
                float dot=(ax*bx+az*bz)/Math.Max(1,(float)Math.Sqrt((ax*ax+az*az)*(bx*bx+bz*bz)));
                score+=(1-dot)*5; // Different sides, not two allies queued in one doorway.
            }
            score-=enemies.Count(e=>e!=target && !CombatManager.Ranged(e) && e.Position.DistanceTo(c)<3)*9;
            score+=CoverUtility.CalculateOverallBlockChance(c,target.Position,map)*3;
            return score;
        }
    }
}
