using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    public sealed class LoadoutCheckpoint
    {
        public int Stage, Fighters, Melee, Ranged, Ready, PendingEquip;
        public Dictionary<string,int> Craft = new Dictionary<string,int>();
        public Dictionary<string,int> BillTargets = new Dictionary<string,int>();
        public Dictionary<string,int> Materials = new Dictionary<string,int>();
        public List<string> Blockers = new List<string>();
        public bool Complete => Fighters > 0 && Ready == Fighters;
        public string Status => $"Etapa {Stage+1}: {Ready}/{Fighters} conjuntos vestidos; melee {Melee}, ranged {Ranged}; {PendingEquip} peças melhores disponíveis; fabricar {Craft.Values.Sum()} peças. " + string.Join("; ",Blockers.Distinct());
    }

    public static class LoadoutProgression
    {
        private static readonly Dictionary<string,Thing> references = new Dictionary<string,Thing>();
        private static Thing Reference(string name)
        {
            if(references.TryGetValue(name,out var t))return t;
            var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if(def==null)return null;
            // Unspawned normal-quality objects are stat references only, never colony inventory.
            t=ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.Steel:null);
            t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Colony);
            references[name]=t;return t;
        }
        public static IEnumerable<string> Set(Pawn p,int stage)
        {
            bool melee=DefenseProductionPlan.Melee(p);
            yield return "Apparel_CollarShirt";yield return "Apparel_Pants";
            if(stage==0)
            {
                if(melee)yield return "Apparel_PlateArmor";
                yield return "Apparel_SimpleHelmet";
                yield return melee?"MeleeWeapon_LongSword":"Bow_Short";
            }
            else if(stage<3)
            {
                yield return melee?"Apparel_PlateArmor":"Apparel_FlakVest";
                yield return "Apparel_AdvancedHelmet";
                yield return melee?"MeleeWeapon_LongSword":stage==1?"Gun_HeavySMG":"Gun_AssaultRifle";
            }
            else
            {
                bool cat=stage>=4 && DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_ArmorCataphract")!=null;
                yield return cat?"Apparel_ArmorCataphract":"Apparel_PowerArmor";
                yield return cat?"Apparel_ArmorHelmetCataphract":"Apparel_PowerArmorHelmet";
                if(melee)yield return "Apparel_ShieldBelt";
                yield return melee?"MeleeWeapon_LongSword":"Gun_ChargeRifle";
            }
        }
        public static bool Meets(Pawn p,Thing item,string target)
        {
            if(item==null || !DefenseProductionPlan.Usable(item))return false;
            var reference=Reference(target);if(reference==null)return true;
            if(reference.def.IsWeapon)
            {
                if(!item.def.IsWeapon || item.def.IsRangedWeapon!=reference.def.IsRangedWeapon)return false;
                var actual=EquipmentAnalyzer.Analyze(item);
                return actual?.SpecialAttack!=true && !EquipmentPolicy.WorthUpgrade(EquipmentAnalyzer.Suitability(p,actual),EquipmentAnalyzer.Suitability(p,EquipmentAnalyzer.Analyze(reference)));
            }
            if(!(item is Apparel a) || !(reference is Apparel r) || !ApparelUtility.HasPartsToWear(p,item.def))return false;
            if(target=="Apparel_CollarShirt")return item.def.defName==target || item.def.defName=="Apparel_BasicShirt";
            if(target=="Apparel_Pants")return item.def.defName==target || item.def.defName=="Apparel_FlakPants";
            if(target=="Apparel_ShieldBelt")return ApparelAnalyzer.BlocksRanged(a);
            // Compare protection/coverage/quality at a neutral temperature; winter clothing cannot satisfy armor.
            if(a.GetStatValue(StatDefOf.ArmorRating_Sharp)<r.GetStatValue(StatDefOf.ArmorRating_Sharp)*.85f)return false;
            if(!a.def.apparel.bodyPartGroups.Intersect(r.def.apparel.bodyPartGroups).Any())return false;
            return !EquipmentPolicy.WorthUpgrade(ApparelAnalyzer.Score(p,a,20),ApparelAnalyzer.Score(p,r,20));
        }
        public static bool Equipped(Pawn p,int stage)=>Set(p,stage).All(name=>
            Meets(p,p.equipment?.Primary,name) || p.apparel.WornApparel.Any(a=>Meets(p,a,name)));
        private static ThingDef Material(IngredientCount ingredient)=>ingredient.filter.AllowedThingDefs.Where(d=>d.defName!="Leather_Human")
            .OrderByDescending(d=>d==ThingDefOf.Cloth || d==ThingDefOf.Steel).ThenBy(d=>d.defName,StringComparer.Ordinal).FirstOrDefault();
        public static LoadoutCheckpoint Analyze(Map map,int stage)
        {
            var fighters=DefenseProductionPlan.Fighters(map).OrderByDescending(p=>
                (p.skills?.GetSkill(DefenseProductionPlan.Melee(p)?SkillDefOf.Melee:SkillDefOf.Shooting).Level??0)).ThenBy(p=>p.thingIDNumber).ToList();
            var result=new LoadoutCheckpoint{Stage=stage,Fighters=fighters.Count,Melee=fighters.Count(DefenseProductionPlan.Melee)};
            result.Ranged=result.Fighters-result.Melee;
            var ground=map.listerThings.AllThings.Where(t=>t.Spawned && (t.def.IsWeapon || t.def.IsApparel) &&
                !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer) && DefenseProductionPlan.Usable(t)).ToList();
            var allocated=new HashSet<Thing>();
            var managed=map.GetComponent<AutonomousRimMapComponent>().ManagedApparel;
            foreach(var p in fighters)
            {
                if(Equipped(p,stage))result.Ready++;
                foreach(var name in Set(p,stage))
                {
                    if(Meets(p,p.equipment?.Primary,name) || p.apparel.WornApparel.Any(a=>Meets(p,a,name)))continue;
                    var reference=Reference(name);
                    if(reference is Apparel proposed && (p.outfits?.CurrentApparelPolicy?.filter.Allows(proposed)==false ||
                        !ApparelUtility.HasPartsToWear(p,proposed.def) || EquipmentPlanner.Conflicts(p,proposed).Any(a=>
                            p.apparel.IsLocked(a) || p.outfits?.forcedHandler.IsForced(a)==true && !managed.Any(m=>m.Pawn==p && m.Apparel==a))))
                    {result.Blockers.Add(p.LabelShort+": "+name+" bloqueado por corpo, política ou roupa manual; fabricação adiada");continue;}
                    if(reference?.def.IsWeapon==true && EquipmentAnalyzer.Analyze(p.equipment?.Primary)?.SpecialAttack==true)
                    {result.Blockers.Add(p.LabelShort+": arma especial preservada");continue;}
                    var existing=ground.FirstOrDefault(t=>!allocated.Contains(t) && Meets(p,t,name) &&
                        p.CanReach(t,Verse.AI.PathEndMode.Touch,Danger.None) &&
                        (t is Apparel a ? EquipmentPlanner.AllowedApparel(p,a,managed) : EquipmentPlanner.AllowedWeapon(p,t)));
                    if(existing!=null){allocated.Add(existing);result.PendingEquip++;continue;}
                    result.Craft[name]=result.Craft.TryGetValue(name,out int count)?count+1:1;
                }
            }
            foreach(var pair in result.Craft)
            {
                int count=ground.Concat(fighters.SelectMany(p=>p.apparel.WornApparel.Cast<Thing>())).Concat(fighters.Select(p=>p.equipment?.Primary).Where(t=>t!=null))
                    .Where(t=>t.def.defName==pair.Key && DefenseProductionPlan.Usable(t)).Sum(t=>t.stackCount);
                result.BillTargets[pair.Key]=count+pair.Value;
                var recipe=DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r=>r.products?.Any(prod=>prod.thingDef.defName==pair.Key)==true);
                if(recipe==null){result.Blockers.Add(pair.Key+": sem receita; procurar aquisição");continue;}
                if(!recipe.AvailableNow)result.Blockers.Add(pair.Key+": pesquisa pendente");
                if(!map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().Any(t=>t.def.AllRecipes.Contains(recipe)))
                    result.Blockers.Add(pair.Key+": bancada pendente");
                if(!map.mapPawns.FreeColonistsSpawned.Any(p=>WorkPriorityManager.CanWork(p) && (recipe.skillRequirements==null || recipe.skillRequirements.All(s=>p.skills.GetSkill(s.skill).Level>=s.minLevel))))
                    result.Blockers.Add(pair.Key+": habilidade de fabricação insuficiente");
                foreach(var ingredient in recipe.ingredients)
                {
                    var def=Material(ingredient);
                    if(def==null)continue;
                    int need=ingredient.CountRequiredOfFor(def,recipe)*pair.Value;
                    result.Materials[def.defName]=(result.Materials.TryGetValue(def.defName,out int old)?old:0)+need;
                }
            }
            // Components are inputs too: include their native precursor costs (gold/plasteel/steel)
            // for the portion that is not already stocked. Spacer expands before industrial.
            foreach(string component in new[]{"ComponentSpacer","ComponentIndustrial"})
            {
                if(!result.Materials.TryGetValue(component,out int required))continue;
                int available=map.listerThings.AllThings.Where(t=>t.Spawned && t.def.defName==component && !t.IsForbidden(Faction.OfPlayer)).Sum(t=>t.stackCount);
                int missing=Math.Max(0,required-available);
                var recipe=DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r=>r.products?.Any(p=>p.thingDef.defName==component)==true);
                if(recipe==null || missing==0)continue;
                foreach(var ingredient in recipe.ingredients)
                {
                    var def=Material(ingredient);if(def==null)continue;
                    result.Materials[def.defName]=(result.Materials.TryGetValue(def.defName,out int old)?old:0)+ingredient.CountRequiredOfFor(def,recipe)*missing;
                }
            }
            foreach(var pair in result.Materials)
            {
                int stock=map.listerThings.AllThings.Where(t=>t.Spawned && t.def.defName==pair.Key && !t.IsForbidden(Faction.OfPlayer) && !t.Position.Fogged(map)).Sum(t=>t.stackCount);
                if(stock<pair.Value)result.Blockers.Add(pair.Key+": faltam "+(pair.Value-stock)+" para completar a etapa");
            }
            return result;
        }
        public static int Stage(Map map)
        {for(int stage=0;stage<4;stage++)if(!DefenseProductionPlan.Fighters(map).All(p=>Equipped(p,stage)))return stage;return 4;}
    }
}
