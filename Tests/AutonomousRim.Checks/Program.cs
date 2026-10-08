using System;
using AutonomousRim.Core;
using System.Linq;
using System.Threading;

internal static class Program
{
    private static int checks;

    private static void Equal<T>(T expected, T actual, string scenario)
    {
        if (!Equals(expected, actual)) throw new Exception($"{scenario}: expected {expected}, got {actual}");
        checks++;
    }

    private static void Main()
    {
        Equal(12f, CombatMath.BurstDps(8f, 3, 2f), "Three-shot burst amortizes damage over the complete firing cycle");
        Equal(6f, CombatMath.BurstDps(8f, 3, 4f), "Doubling the cycle halves sustained damage");
        Equal(0f, CombatMath.BurstDps(8f, 3, 0f), "Zero-duration attacks do not produce infinite scores");
        Equal(0f, CombatMath.BurstDps(8f, 0, 2f), "Attacks without shots have no projectile DPS");
        Equal(0f, CombatMath.BurstDps(8f, 3, float.NaN), "Invalid cycle cannot enter a ranking");
        Equal(0f, CombatMath.BurstDps(float.NaN, 3, 2f), "Invalid damage cannot enter a ranking");
        Equal(0f, CombatMath.BurstDps(float.PositiveInfinity, 3, 2f), "Unbounded damage cannot enter a ranking");
        Equal("Clear", CombatMath.AssessRisk(0, 0, 0f, 0f), "Empty map is clear even without defenders");
        Equal("Critical", CombatMath.AssessRisk(1, 0, 1f, 100f), "Presence of enemies without capable defenders is critical");
        Equal("Critical", CombatMath.AssessRisk(1, 3, 20f, 0f), "Zero allied strength cannot hide a threat");
        Equal("High", CombatMath.AssessRisk(2, 3, 150f, 100f), "Strength ratio at the high-risk boundary");
        Equal("High", CombatMath.AssessRisk(6, 3, 10f, 100f), "Numerical disadvantage is considered independently of strength");
        Equal("Threat present", CombatMath.AssessRisk(1, 3, 10f, 100f), "Weak enemies remain a threat rather than being called safe");
        Equal(true, ColonyPolicy.SafePrey(false, 0f, false, false, false), "Passive wild prey is eligible");
        Equal(false, ColonyPolicy.SafePrey(true, 0f, false, false, false), "Predators are excluded even without a revenge roll");
        Equal(false, ColonyPolicy.SafePrey(false, 0.01f, false, false, false), "Even a small retaliation chance is excluded");
        Equal(false, ColonyPolicy.SafePrey(false, 0f, true, false, false), "Owned animals are not automatic prey");
        Equal(false, ColonyPolicy.SafePrey(false, 0f, false, true, false), "Downed animals are not designated for hunting");
        Equal(false, ColonyPolicy.SafePrey(false, 0f, false, false, true), "Hostile animals are handled as threats");
        Equal(6, ColonyPolicy.MealTarget(1.6f, 0.9f), "Three-day meal reserve rounds up");
        Equal(0, ColonyPolicy.MealTarget(0f, 0.9f), "No eaters require no meals");
        Equal(400, ColonyPolicy.MealTarget(1000f, 0.9f), "Production reserve has a bounded target");
        Equal(true, ColonyPolicy.WorkScore(10, 2, 0) > ColonyPolicy.WorkScore(10, 0, 0), "Passion distinguishes equal skill levels");
        Equal(true, ColonyPolicy.WorkScore(10, 0, 0) > ColonyPolicy.WorkScore(10, 0, 2), "Specialist load spreads professions");
        Equal(true, EquipmentPolicy.PreferMelee(true, 20, 3), "Brawler preference survives a higher shooting skill");
        Equal(false, EquipmentPolicy.PreferMelee(false, 14, 8), "Good shooters keep a ranged role");
        Equal(true, EquipmentPolicy.PreferMelee(false, 5, 15), "Large melee advantage creates a melee role");
        Equal(true, EquipmentPolicy.RoleMultiplier(false, true, true) > EquipmentPolicy.RoleMultiplier(true, true, true), "Brawler prefers melee equipment");
        Equal(false, EquipmentPolicy.WorthUpgrade(10f, 12f), "Marginal upgrades do not cause repeated swapping");
        Equal(true, EquipmentPolicy.WorthUpgrade(10f, 13f), "A material score improvement allows replacement");
        Equal(false, EquipmentPolicy.WorthUpgrade(1f, float.NaN), "Invalid scores never trigger equipment jobs");
        Equal(false, EquipmentPolicy.WorthUpgrade(1f, float.PositiveInfinity), "Infinite scores never trigger equipment jobs");
        Equal(6f, EquipmentPolicy.EffectiveBurstDps(24f, 2f, 1f, 2f, 0f), "Pawn aiming time is part of the attack cycle");
        Equal(8f, EquipmentPolicy.EffectiveBurstDps(24f, 2f, 0.5f, 2f, 0f), "Faster aiming increases effective burst damage rate");
        Equal(0f, EquipmentPolicy.EffectiveBurstDps(24f, 0f, 1f, 0f, 0f), "Zero-duration firing remains invalid");
        Equal(true,TacticalPolicy.DamageFraction(1f,0.1f)<TacticalPolicy.DamageFraction(0.1f,0.1f),"Armor reduces weak weapon exposure");
        Equal(true,TacticalPolicy.DamageFraction(1f,0.9f)>TacticalPolicy.DamageFraction(1f,0.1f),"Penetration defeats an armor advantage");
        Equal(true,TacticalPolicy.DamageFraction(4f,0)>0,"Armor never implies immunity");
        Equal(true,TacticalPolicy.CanPress(1,1.3f,2,2,16,15,false),"Healthy equal-number advantage permits closing on a shooter");
        Equal(false,TacticalPolicy.CanPress(0.5f,2,2,2,8,5,false),"Injured melee is not sent into a charge");
        Equal(false,TacticalPolicy.CanPress(1,2,2,6,8,5,false),"Equipment does not justify severe numerical disadvantage");
        Equal(false,TacticalPolicy.CanPress(1,2,2,2,12,100,false),"Exposed approach is rejected");
        Equal(false,TacticalPolicy.CanPress(1,2,2,2,23,30,false),"Long crossing under fire is rejected even with equipment advantage");
        Equal(false,TacticalPolicy.CanPress(1,2,2,2,12,5,true),"Unknown special attack prevents optimistic charge");
        var friendly=new[]{new FighterSnapshot(1,0,0,0,false,false,1,60,12,.3f,1,.5f,1.5f,4),new FighterSnapshot(2,1,0,0,false,false,1,60,12,.3f,1,.5f,1.5f,4)};
        var foes=new[]{new FighterSnapshot(3,12,0,2,true,false,1,40,5,.1f,0,0,24,4),new FighterSnapshot(4,8,0,0,false,false,1,30,8,.2f,0,0,1.5f,4)};
        var expected=TacticalPolicy.Assess(friendly,foes);
        Equal(3,expected.Where(a=>a.PawnId==1).OrderByDescending(a=>a.Score).First().TargetId,"Melee prioritizes helping its partner under fire");
        TargetAssessment[] computed=null; int thread=0;
        using(var done=new ManualResetEventSlim(false))
        {
            Equal(true,AnalysisWorker.Submit(()=>{computed=TacticalPolicy.Assess(friendly,foes);thread=Thread.CurrentThread.ManagedThreadId;done.Set();}),"Worker accepts value-only analysis");
            Equal(true,done.Wait(5000),"Parallel analysis completes");
        }
        Equal(false,thread==Thread.CurrentThread.ManagedThreadId,"Calculation uses a separate CPU thread");
        Equal(true,expected.Select(a=>a.Score).SequenceEqual(computed.Select(a=>a.Score)),"Parallel and synchronous decisions match");
        Equal(true,RescuePolicy.CanDetach(3,2,160,70),"Three defenders against two permits a rescue");
        Equal(false,RescuePolicy.CanDetach(2,6,160,70),"Do not abandon defense during numerical disadvantage");
        Equal(false,RescuePolicy.CanDetach(3,2,50,100),"Numbers alone do not defeat stronger enemies");
        Equal(true,RescuePolicy.CanDetach(0,0,0,0),"Safe postbattle care does not require defenders");
        Equal(true,RescuePolicy.HelperScore(20,35,100,4000)>RescuePolicy.HelperScore(4,70,100,4000),"A skilled doctor with lower combat value is preferred");
        Equal(true,RescuePolicy.StabilizeFirst(1000,900,300,true),"Stabilize before a journey that risks bleeding death");
        Equal(false,RescuePolicy.StabilizeFirst(10000,900,300,true),"Stable patient can be carried first");
        Equal(true,RescuePolicy.StabilizeFirst(10000,900,300,false),"No bed must not prevent safe bleeding treatment");
        Console.WriteLine($"Passed {checks} combat and colony policy checks.");
    }
}
