using System;
using System.IO;
using System.Linq;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using AutonomousRim.Core;

namespace AutonomousRim.RuntimeChecks
{
    // Initial setup only, equivalent to scenario selection and the supplied preset.
    // No production AI patches and no changes when this test flag is absent.
    [StaticConstructorOnStartup]
    public static class EnhancedWorldSetup
    {
        public static bool Enabled => GenCommandLine.CommandLineArgPassed("autonomousrimenhancedtest") || GenCommandLine.CommandLineArgPassed("autonomousrimelitesix");
        private static bool configured;
        static EnhancedWorldSetup()
        {
            if(!Enabled)return;
            var h=new Harmony("leonardoh21.autonomousrim.enhanced.setup");
            h.Patch(AccessTools.Method(typeof(Scenario),"PreConfigure"),prefix:new HarmonyMethod(typeof(EnhancedWorldSetup),nameof(Scenario)));
            h.Patch(AccessTools.Method(typeof(ScenPart_ConfigPage_ConfigureStartingPawns),"GenerateStartingPawns"),postfix:new HarmonyMethod(typeof(EnhancedWorldSetup),nameof(Pawns)));
            h.Patch(AccessTools.Method(typeof(GameInitData),"PrepForMapGen"),prefix:new HarmonyMethod(typeof(EnhancedWorldSetup),nameof(Settings)));
        }
        private static bool Scenario()
        {
            if(configured)return true;
            configured=true;
            RimWorld.Scenario loaded;
            if(!GameDataSaveLoader.TryLoadScenario(Path.Combine(GenFilePaths.SaveDataFolderPath,"Scenarios","AutonomousRim_Enhanced_World.rsc"),ScenarioCategory.CustomLocal,out loaded))throw new Exception("Enhanced scenario failed to load");
            Current.Game.Scenario=loaded;
            loaded.PreConfigure();
            return false;
        }
        private static void Settings(GameInitData __instance)
        {
            __instance.mapSize=325;
            __instance.permadeath=false;
            var difficulty=DefDatabase<DifficultyDef>.GetNamed("Medium");
            Find.Storyteller.def=StorytellerDefOf.Cassandra;
            Find.Storyteller.difficultyDef=difficulty;
            Find.Storyteller.difficulty.CopyFrom(difficulty);
        }
        public static XmlDocument Preset()
        {
            var doc=new XmlDocument();
            doc.Load(Path.Combine(GenFilePaths.SaveDataFolderPath,"PrepareCarefully","AutonomousRim_EliteSix_RW1.6_PC1.6.2.pcp"));
            return doc;
        }
        private static void Pawns()
        {
            var nodes=Preset().SelectNodes("/preset/pawns/li");
            if(nodes.Count!=6 || Find.GameInitData.startingAndOptionalPawns.Count!=6)throw new Exception("Expected exactly six preset colonists");
            for(int i=0;i<6;i++)Apply(Find.GameInitData.startingAndOptionalPawns[i],nodes[i]);
            Log.Message("[EnhancedWorld] SETUP: six exact preset profiles imported using native defs, recipes and body parts; no additional equipment.");
        }
        private static void Apply(Pawn p,XmlNode n)
        {
            // Vanilla human parent-name generation casts Name to NameTriple.
            // Keep the preset nickname while avoiding its Single-name incompatibility.
            p.Name=new NameTriple(n["nickName"].InnerText,n["nickName"].InnerText,"");
            foreach(var relation in p.relations.DirectRelations.ToList())p.relations.RemoveDirectRelation(relation.def,relation.otherPawn);
            p.gender=(Gender)Enum.Parse(typeof(Gender),n["gender"].InnerText);
            p.story.bodyType=DefDatabase<BodyTypeDef>.GetNamed(n["bodyType"].InnerText);
            p.story.hairDef=DefDatabase<HairDef>.GetNamed(n["hairDef"].InnerText);
            p.story.Childhood=Backstory(n["childhood"].InnerText);
            p.story.Adulthood=Backstory(n["adulthood"].InnerText);
            p.ageTracker.AgeBiologicalTicks=long.Parse(n["biologicalAgeInTicks"].InnerText);
            p.ageTracker.AgeChronologicalTicks=long.Parse(n["chronologicalAgeInTicks"].InnerText);
            foreach(var t in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(t);
            foreach(XmlNode t in n.SelectNodes("traits/li"))p.story.traits.GainTrait(new Trait(DefDatabase<TraitDef>.GetNamed(t["def"].InnerText),int.Parse(t["degree"].InnerText),true));
            foreach(XmlNode s in n.SelectNodes("skills/li"))
            {
                var skill=p.skills.GetSkill(DefDatabase<SkillDef>.GetNamed(s["name"].InnerText));
                skill.Level=int.Parse(s["value"].InnerText);skill.passion=(Passion)Enum.Parse(typeof(Passion),s["passion"].InnerText);skill.xpSinceLastLevel=0;
            }
            p.apparel.DestroyAll();p.equipment.DestroyAllEquipment();p.inventory.innerContainer.ClearAndDestroyContents();
            foreach(var g in p.genes.GenesListForReading.ToList())p.genes.RemoveGene(g);
            p.genes.SetXenotype(DefDatabase<XenotypeDef>.GetNamed("Baseliner"));
            foreach(var hd in p.health.hediffSet.hediffs.ToList())p.health.RemoveHediff(hd);
            foreach(XmlNode implant in n.SelectNodes("implants/li"))
            {
                var recipe=DefDatabase<RecipeDef>.GetNamed(implant["recipe"].InnerText);
                int index=implant["bodyPartIndex"]==null?0:int.Parse(implant["bodyPartIndex"].InnerText);
                var part=p.RaceProps.body.AllParts.Where(b=>b.def.defName==implant["bodyPart"].InnerText).ElementAt(index);
                if(recipe.addsHediff==null || !recipe.appliedOnFixedBodyParts.Contains(part.def))throw new Exception("Invalid preset implant: "+recipe.defName);
                p.health.AddHediff(recipe.addsHediff,part);
            }
            if(p.WorkTypeIsDisabled(WorkTypeDefOf.Construction) || p.WorkTagIsDisabled(WorkTags.Violent))throw new Exception("Preset has unexpected incapacity: "+p.LabelShort);
        }
        private static BackstoryDef Backstory(string name)
        {
            var result=DefDatabase<BackstoryDef>.GetNamedSilentFail(name)??DefDatabase<BackstoryDef>.AllDefs.FirstOrDefault(b=>b.identifier==name);
            if(result==null)throw new Exception("Missing preset backstory: "+name);
            return result;
        }
    }
}
