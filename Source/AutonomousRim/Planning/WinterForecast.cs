using System;
using System.Linq;
using AutonomousRim.Core;
using RimWorld;
using Verse;

namespace AutonomousRim.Planning
{
    public sealed class WinterForecast
    {
        public int DaysUntilCold=-1, WarmClothingMissing, FuelRequired;
        public float FoodRequiredDays, HarvestNutrition;
        public string Status="Previsão sazonal aguardando leitura.";
        public void Update(Map map,ColonyState state)
        {
            DaysUntilCold=-1;HarvestNutrition=0;
            if(map.Tile<0)return;
            var longitude=Find.WorldGrid.LongLatOf(map.Tile);
            for(int day=0;day<=30;day++)
                if(GenDate.Season(Find.TickManager.TicksAbs+day*60000,longitude)==Season.Winter || day==0 && state.OutdoorTemperature<0)
                {DaysUntilCold=day;break;}
            FoodRequiredDays=DaysUntilCold>=0?15:state.TargetFoodDays;
            WarmClothingMissing=DaysUntilCold>=0?map.mapPawns.FreeColonistsSpawned.Count(p=>p.GetStatValue(StatDefOf.ComfyTemperatureMin)>-10):0;
            float burn=map.listerBuildings.allBuildingsColonist.Select(b=>b.TryGetComp<CompRefuelable>())
                .Where(c=>c!=null && c.Props.fuelFilter.Allows(ThingDefOf.WoodLog)).Sum(c=>c.Props.fuelConsumptionRate);
            FuelRequired=DaysUntilCold<0?0:Math.Min(5000,Math.Max(state.ColonistCount*100,(int)Math.Ceiling(burn*15)));
            foreach(var plant in map.listerThings.AllThings.OfType<Plant>().Where(p=>p.Spawned && p.def.plant.harvestedThingDef?.IsNutritionGivingIngestible==true &&
                p.Position.GetZone(map) is Zone_Growing && p.HarvestableNow))
                HarvestNutrition+=plant.def.plant.harvestYield*plant.def.plant.harvestedThingDef.GetStatValueAbstract(StatDefOf.Nutrition)*Find.Storyteller.difficulty.cropYieldFactor;
            Status=DaysUntilCold<0?"Sem inverno sazonal nos próximos 30 dias; extremos de temperatura continuam monitorados.":
                "Prazo: "+DaysUntilCold+" dias até inverno/frio observado • comida "+state.EstimatedFoodDays.ToString("0.0")+" / "+FoodRequiredDays+
                " dias • roupas com conforto insuficiente para −10 °C: "+WarmClothingMissing+" • madeira prevista: "+FuelRequired+
                " • colheita pronta estimada: "+HarvestNutrition.ToString("0.0")+" nutrição (sem contar como estoque).";
        }
    }
}
