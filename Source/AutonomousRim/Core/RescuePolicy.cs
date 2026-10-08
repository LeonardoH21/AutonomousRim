using System;
namespace AutonomousRim.Core
{
    public static class RescuePolicy
    {
        public static bool CanDetach(int defenders,int enemies,float friendly,float hostile) =>
            enemies==0 || defenders>0 && defenders>=enemies && friendly>=hostile*1.1f;
        public static float HelperScore(float medical,float combat,float travelTicks,int deathTicks) =>
            medical*8-combat*.65f-travelTicks*(deathTicks<5000?.12f:.035f);
        public static bool StabilizeFirst(int deathTicks,float transportTicks,float tendTicks,bool bed) =>
            !bed || deathTicks<transportTicks+tendTicks+600;
    }
}
