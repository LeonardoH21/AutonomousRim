using System;
using System.Linq;

namespace AutonomousRim.Core
{
    // Value-only copies. No Pawn, Map, Unity object or native path query crosses threads.
    public sealed class FighterSnapshot
    {
        public readonly int Id, X, Z, AttackingId;
        public readonly bool Ranged, Special, BluntDamage;
        public readonly float Health, Power, Dps, Penetration, Sharp, Blunt, Range, Speed;
        public FighterSnapshot(int id, int x, int z, int attackingId, bool ranged, bool special,
            float health, float power, float dps, float penetration, float sharp, float blunt, float range, float speed,bool bluntDamage=false)
        { Id=id; X=x; Z=z; AttackingId=attackingId; Ranged=ranged; Special=special; Health=health; Power=power;
            Dps=dps; Penetration=penetration; Sharp=sharp; Blunt=blunt; Range=range; Speed=speed; BluntDamage=bluntDamage; }
        public float Distance(FighterSnapshot other) => (float)Math.Sqrt((X-other.X)*(X-other.X)+(Z-other.Z)*(Z-other.Z));
    }
    public sealed class TargetAssessment
    {
        public readonly int PawnId, TargetId;
        public readonly float Score, Advantage;
        public TargetAssessment(int pawnId,int targetId,float score,float advantage)
        { PawnId=pawnId; TargetId=targetId; Score=score; Advantage=advantage; }
    }
    public static class TacticalPolicy
    {
        // Advisory expectation, not an invulnerability guarantee. Coverage is included in armor copies.
        public static float DamageFraction(float armor, float penetration) =>
            Math.Max(0.18f, 1f - Math.Min(1.1f, Math.Max(0, armor-penetration))*0.65f);
        public static TargetAssessment[] Assess(FighterSnapshot[] allies,FighterSnapshot[] enemies)
        {
            return allies.SelectMany(a=>enemies.Select(e=>
            {
                float friendly=allies.Where(f=>f.Distance(a)<24).Sum(f=>f.Power*DamageFraction(f.BluntDamage?e.Blunt:e.Sharp,f.Penetration));
                float hostile=enemies.Where(f=>f.Distance(e)<16).Sum(f=>f.Power*DamageFraction(f.BluntDamage?a.Blunt:a.Sharp,f.Penetration));
                float ratio=friendly/Math.Max(1,hostile);
                bool help=allies.Any(f=>f.Id!=a.Id && f.Distance(a)<24 && (e.AttackingId==f.Id || !e.Ranged && e.Distance(f)<4));
                float score=(help?35:0)+(e.Ranged && !a.Ranged?16:0)+(1-e.Health)*10-a.Distance(e)*1.2f+
                    Math.Min(3,ratio)*6-(e.Special?12:0);
                return new TargetAssessment(a.Id,e.Id,score,ratio);
            })).ToArray();
        }
        public static bool CanPress(float health,float advantage,int friends,int enemies,float distance,float exposure,bool special)
            => health>=0.72f && !special && distance<=24 && exposure<health*25 && advantage>=0.85f &&
                enemies<=Math.Max(1,friends)*1.7f;
    }
}
