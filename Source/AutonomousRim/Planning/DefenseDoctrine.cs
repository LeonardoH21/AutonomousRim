using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;
using AutonomousRim.Execution;
namespace AutonomousRim.Planning
{
    public sealed class DefenseDoctrine
    {
        public HashSet<int> PriorityTargets=new HashSet<int>();
        public bool Internal, Siege, Breach, Sapper;
        public string Status="Sem ameaça especial observada.";
        public void Update(Map map)
        {
            Internal=Siege=Breach=Sapper=false;PriorityTargets.Clear();
            foreach(var enemy in CombatManager.Enemies(map))
            {
                string lord=enemy.GetLord()?.LordJob?.GetType().Name??"";
                string duty=enemy.mindState?.duty?.def?.defName??"";
                bool inside=map.areaManager.Home[enemy.Position] && enemy.Position.GetRoom(map)?.PsychologicallyOutdoors==false;
                bool siege=lord.IndexOf("Siege",StringComparison.OrdinalIgnoreCase)>=0;
                bool breach=enemy.CurJob?.targetA.Thing is Building wall && wall.def==ThingDefOf.Wall && wall.Faction==Faction.OfPlayer || lord.IndexOf("Breach",StringComparison.OrdinalIgnoreCase)>=0 || duty.IndexOf("Breach",StringComparison.OrdinalIgnoreCase)>=0;
                bool sapper=duty.IndexOf("Sapper",StringComparison.OrdinalIgnoreCase)>=0 || duty.IndexOf("Dig",StringComparison.OrdinalIgnoreCase)>=0;
                Internal|=inside;Siege|=siege;Breach|=breach;Sapper|=sapper;
                if(inside || breach || sapper)PriorityTargets.Add(enemy.thingIDNumber);
            }
            Status=Internal?"Invasão interna: interceptar próximos dos aliados; abandonar a âncora antiga.":Breach||Sapper?
                "Ataque à parede: priorizar invasores no alcance, sem presumir entrada pela killbox.":Siege?
                "Cerco: preservar cobertura; sem assalto automático fora da base. Artilharia/contra-cerco pendentes.":"Defesa normal: cobertura e interceptação local.";
        }
    }
}
