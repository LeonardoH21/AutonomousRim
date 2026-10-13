using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;
namespace AutonomousRim.Starting
{
    public sealed class StartingTeamRating
    {
        public float Score; public List<string> Details=new List<string>();
        private static readonly string[] Skills={"Construction","Cooking","Plants","Crafting","Medicine","Intellectual","Social","Mining"};
        private static readonly string[] Work={"Construction","Cooking","Growing","Crafting","Doctor","Research","Warden","Mining"};
        private static readonly string[] Labels={"Construção","Cozinha","Cultivo","Fabricação","Medicina","Pesquisa","Social","Mineração"};
        private sealed class Profile {public float[] Skills;public float Value;}
        private static ConditionalWeakTable<Pawn,Profile> cache=new ConditionalWeakTable<Pawn,Profile>();
        public static void ResetCache(){cache=new ConditionalWeakTable<Pawn,Profile>();}
        private static Profile Capture(Pawn p)=>cache.GetValue(p,pawn=>new Profile{Skills=Enumerable.Range(0,Skills.Length).Select(r=>ComputeSkill(pawn,r)).ToArray(),Value=ComputeIndividual(pawn)});
        public static float Skill(Pawn pawn,int role)=>Capture(pawn).Skills[role];
        private static float ComputeSkill(Pawn pawn,int role)
        {
            var skill=DefDatabase<SkillDef>.GetNamedSilentFail(Skills[role]);var work=DefDatabase<WorkTypeDef>.GetNamedSilentFail(Work[role]);
            var record=skill==null?null:pawn.skills?.GetSkill(skill);
            if(record==null || record.TotallyDisabled || work!=null && pawn.WorkTypeIsDisabled(work))return 0;
            float speed=Math.Min(1.6f,pawn.GetStatValue(StatDefOf.WorkSpeedGlobal));
            float capacity=Math.Min(pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness),pawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation));
            return (record.Level*(1+0.18f*(int)record.passion)+2*(int)record.passion)*Math.Max(0.1f,speed)*capacity;
        }
        public static float Individual(Pawn p)=>p==null?-1000:Capture(p).Value;
        private static float ComputeIndividual(Pawn p)
        {
            if(p==null || p.Dead || p.Downed)return -1000;
            float value=12*p.health.summaryHealth.SummaryHealthPercent+4*p.health.capacities.GetLevel(PawnCapacityDefOf.Moving)-8*p.health.hediffSet.PainTotal;
            value-=Math.Max(0,p.GetStatValue(StatDefOf.MentalBreakThreshold)-0.35f)*45;
            foreach(var h in p.health.hediffSet.hediffs)
                if(h is Hediff_Addiction || h.def.chronic)value-=5;
            if(Perception.TraitAnalyzer.HasActiveTrait(p,"Pyromaniac"))value-=12;
            if(p.ageTracker.AgeBiologicalYears>55)value-=(p.ageTracker.AgeBiologicalYears-55)*0.25f;
            if(p.WorkTagIsDisabled(WorkTags.Hauling))value-=5;
            return value+Enumerable.Range(0,Skills.Length).Sum(r=>ComputeSkill(p,r))*0.08f;
        }
        public static StartingTeamRating Read(IEnumerable<Pawn> pawns)
        {
            var team=pawns.ToList();var result=new StartingTeamRating();
            if(team.Count==0){result.Score=-10000;return result;}
            result.Score=team.Sum(Individual);
            var scores=team.Select(p=>Enumerable.Range(0,Skills.Length).Select(r=>Skill(p,r)).ToArray()).ToArray();
            for(int role=0;role<Skills.Length;role++)
            {
                int r=role;var ranked=team.Select((p,i)=>new{Pawn=p,Value=scores[i][r]}).OrderByDescending(x=>x.Value).ToList();
                float best=ranked[0].Value,backup=ranked.Count>1?ranked[1].Value:0;
                result.Score+=best*(role<6?1.5f:0.6f)+backup*0.25f;
                if(role<6 && best<3)result.Score-=25+(3-best)*5;
                result.Details.Add(Labels[role]+": "+ranked[0].Pawn.LabelShort+" • aptidão "+best.ToString("0.0")+" • suplente "+backup.ToString("0.0"));
            }
            // Assign different primary specialists; one excellent pawn cannot fund six simultaneous jobs.
            int roles=Math.Min(6,team.Count),size=1<<roles;var dp=Enumerable.Repeat(-100000f,size).ToArray();dp[0]=0;
            foreach(var scoresForPawn in scores)
            {
                var next=(float[])dp.Clone();
                for(int mask=0;mask<size;mask++)if(dp[mask]>-99999)
                    for(int r=0;r<roles;r++)if((mask&(1<<r))==0)next[mask|(1<<r)]=Math.Max(next[mask|(1<<r)],dp[mask]+scoresForPawn[r]);
                dp=next;
            }
            result.Score+=dp[size-1]*1.5f;
            var fighters=team.Where(p=>!p.WorkTagIsDisabled(WorkTags.Violent) && p.health.capacities.CapableOf(PawnCapacityDefOf.Moving) && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)).ToList();
            result.Score-=Math.Max(0,Math.Min(team.Count,team.Count>=5?4:team.Count>=3?2:1)-fighters.Count)*35;
            result.Score+=fighters.Select(p=>Math.Max(p.skills?.GetSkill(SkillDefOf.Shooting).Level??0,p.skills?.GetSkill(SkillDefOf.Melee).Level??0)).Sum()*0.5f;
            var melee=fighters.OrderByDescending(p=>Perception.TraitAnalyzer.HasActiveTrait(p,"Brawler")?100:0)
                .ThenByDescending(p=>(p.skills?.GetSkill(SkillDefOf.Melee).Level??0)-(p.skills?.GetSkill(SkillDefOf.Shooting).Level??0))
                .ThenBy(p=>p.thingIDNumber).Take(Math.Min(2,(fighters.Count+1)/2)).ToList();
            result.Score+=melee.Sum(p=>p.skills?.GetSkill(SkillDefOf.Melee).Level??0)*0.7f;
            result.Score+=fighters.Except(melee).Sum(p=>p.skills?.GetSkill(SkillDefOf.Shooting).Level??0)*0.7f;
            result.Details.Add("Combate: "+melee.Count+" melee, "+(fighters.Count-melee.Count)+" ranged. Combatentes aptos: "+fighters.Count+"/"+team.Count+". Nota inclui saúde, paixões, capacidades e risco de traços.");
            return result;
        }
    }
}
