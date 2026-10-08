using System;
using System.Linq;
using System.Runtime.CompilerServices;
using AutonomousRim.Core;
using RimWorld;
using Verse;

namespace AutonomousRim.Perception
{
    public static class CombatEquipmentScanner
    {
        private sealed class CachedStats
        {
            public int Tick=-9999,Signature;
            public float Sharp,Blunt,Penetration,Dps,Range,Speed,Skill;
            public bool Ranged,Special,BluntDamage;
        }
        private static readonly ConditionalWeakTable<Pawn,CachedStats> cache=new ConditionalWeakTable<Pawn,CachedStats>();
        private static float Armor(Pawn pawn,StatDef stat)
        {
            var parts=pawn.RaceProps.body.AllParts.Where(p=>p.depth==BodyPartDepth.Outside && p.coverageAbs>0 && !pawn.health.hediffSet.PartIsMissing(p)).ToList();
            float total=parts.Sum(p=>p.coverageAbs);
            if(total<=0)return pawn.GetStatValue(stat);
            return parts.Sum(p=>p.coverageAbs*(pawn.GetStatValue(stat)+
                (pawn.apparel?.WornApparel.Where(a=>a.def.apparel.CoversBodyPart(p)).Sum(a=>a.GetStatValue(stat))??0)))/total;
        }
        public static FighterSnapshot Copy(Pawn pawn)
        {
            var cached=cache.GetValue(pawn,_=>new CachedStats());
            int tick=Find.TickManager?.TicksGame??0;
            int signature=pawn.equipment?.Primary?.thingIDNumber??0;
            if(pawn.apparel!=null)foreach(var armor in pawn.apparel.WornApparel)signature=unchecked(signature*31+armor.thingIDNumber+armor.HitPoints);
            if(signature==cached.Signature && tick-cached.Tick<60)
                return Snapshot(pawn,cached);
            var item=pawn.equipment?.Primary; var profile=EquipmentAnalyzer.Analyze(item);
            float penetration=profile?.ArmorPenetration??0;
            if(profile?.Ranged!=true && item!=null)
            {
                var stat=DefDatabase<StatDef>.GetNamedSilentFail("MeleeWeapon_AverageArmorPenetration");
                if(stat!=null)penetration=item.GetStatValue(stat);
            }
            float sharp=Armor(pawn,StatDefOf.ArmorRating_Sharp),blunt=Armor(pawn,StatDefOf.ArmorRating_Blunt);
            float dps=profile?.DamagePerSecond??pawn.GetStatValue(DefDatabase<StatDef>.GetNamed("MeleeDPS"));
            if(item==null)penetration=pawn.GetStatValue(DefDatabase<StatDef>.GetNamed("MeleeArmorPenetration"));
            float skill=pawn.skills?.GetSkill(profile?.Ranged==true?SkillDefOf.Shooting:SkillDefOf.Melee).Level??8;
            bool bluntDamage=profile?.Ranged==true ? item?.TryGetComp<CompEquippable>()?.PrimaryVerb?.verbProps.defaultProjectile?.projectile?.damageDef?.armorCategory?.defName=="Blunt" :
                (item?.def.tools??pawn.def.tools)?.OrderByDescending(t=>t.power/Math.Max(.1f,t.cooldownTime)).FirstOrDefault()?.capacities?.Any(c=>c.defName=="Blunt")==true;
            cached.Tick=tick;cached.Signature=signature;cached.Sharp=sharp;cached.Blunt=blunt;cached.Dps=dps;cached.Skill=skill;
            cached.Penetration=penetration;cached.Range=profile?.Range??1.5f;cached.Ranged=profile?.Ranged==true;
            cached.Special=profile?.SpecialAttack==true;cached.BluntDamage=bluntDamage;cached.Speed=pawn.GetStatValue(StatDefOf.MoveSpeed);
            return Snapshot(pawn,cached);
        }
        private static FighterSnapshot Snapshot(Pawn pawn,CachedStats value)
        {
            float health=pawn.health.summaryHealth.SummaryHealthPercent;
            float power=(12+value.Skill+value.Dps)*health*(1+Math.Min(1.2f,value.Sharp)*0.45f);
            return new FighterSnapshot(pawn.thingIDNumber,pawn.Position.x,pawn.Position.z,
                (pawn.CurJob?.targetA.Thing as Pawn)?.thingIDNumber??0,value.Ranged,value.Special,
                health,power,value.Dps,value.Penetration,value.Sharp,value.Blunt,value.Range,value.Speed,value.BluntDamage);
        }
    }
}
