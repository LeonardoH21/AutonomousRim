<?xml version="1.0" encoding="utf-8"?>
<savedscenario>
  <meta>
    <gameVersion>1.6.4633 rev1261</gameVersion>
    <modIds>
      <li>brrainz.harmony</li>
      <li>ludeon.rimworld</li>
      <li>ludeon.rimworld.royalty</li>
      <li>ludeon.rimworld.ideology</li>
      <li>ludeon.rimworld.biotech</li>
      <li>ludeon.rimworld.anomaly</li>
      <li>ludeon.rimworld.odyssey</li>
      <li>leonardoh21.autonomousrim</li>
    </modIds>
    <modSteamIds>
      <li>0</li>
      <li>0</li>
      <li>0</li>
      <li>0</li>
      <li>0</li>
      <li>0</li>
      <li>0</li>
      <li>0</li>
    </modSteamIds>
    <modNames>
      <li>Harmony</li>
      <li>Core</li>
      <li>Royalty</li>
      <li>Ideology</li>
      <li>Biotech</li>
      <li>Anomaly</li>
      <li>Odyssey</li>
      <li>AutonomousRim</li>
    </modNames>
  </meta>
  <scenario>
    <name>AutonomousRim Enhanced World</name>
    <summary>Seis colonos Elite Six; MiningYield x5, PlantHarvestYield x3, ConstructionSpeed x2, ResearchSpeed x3.</summary>
    <description>Ambiente definitivo de testes AutonomousRim. Recursos iniciais vanilla de Crashlanded; carregar o preset Elite Six no Prepare Carefully. Cassandra e dificuldade media sao configuradas ao criar a campanha.</description>
    <playerFaction>
      <def>PlayerFaction</def>
      <factionDef>PlayerColony</factionDef>
    </playerFaction>
    <surfaceLayer>
      <def>SurfaceLayerFixed</def>
      <layer>Surface</layer>
      <settingsDef>Surface</settingsDef>
      <hide>True</hide>
      <tag>Surface</tag>
      <connections>
        <li>
          <tag>Orbit</tag>
          <zoomMode>ZoomOut</zoomMode>
        </li>
      </connections>
    </surfaceLayer>
    <parts>
      <li Class="ScenPart_PlanetLayer">
        <def>PlanetLayer</def>
        <tag>Orbit</tag>
        <layer>Orbit</layer>
        <settingsDef>Orbit</settingsDef>
        <hide>true</hide>
        <connections>
          <li>
            <zoomMode>ZoomIn</zoomMode>
            <tag>Surface</tag>
          </li>
        </connections>
      </li>
      <li Class="ScenPart_ConfigPage_ConfigureStartingPawns">
        <def>ConfigPage_ConfigureStartingPawns</def>
        <pawnCount>6</pawnCount>
        <pawnChoiceCount>6</pawnChoiceCount>
      </li>
      <li Class="ScenPart_PlayerPawnsArriveMethod">
        <def>PlayerPawnsArriveMethod</def>
        <method>DropPods</method>
        <visible>false</visible>
      </li>
      <li Class="ScenPart_ForcedHediff">
        <def>ForcedHediff</def>
        <visible>false</visible>
        <context>PlayerStarter</context>
        <chance>0.5</chance>
        <hediff>CryptosleepSickness</hediff>
        <hideOffMap>true</hideOffMap>
        <severityRange>1~1</severityRange>
      </li>
      <li Class="ScenPart_StartingThing_Defined">
        <def>StartingThing_Defined</def>
        <thingDef>Silver</thingDef>
        <count>800</count>
      </li>
      <li Class="ScenPart_StartingThing_Defined">
        <def>StartingThing_Defined</def>
        <thingDef>MealSurvivalPack</thingDef>
        <count>50</count>
      </li>
      <li Class="ScenPart_StartingThing_Defined">
        <def>StartingThing_Defined</def>
        <thingDef>MedicineIndustrial</thingDef>
        <count>30</count>
      </li>
      <li Class="ScenPart_StartingThing_Defined">
        <def>StartingThing_Defined</def>
        <thingDef>ComponentIndustrial</thingDef>
        <count>30</count>
      </li>
      <li Class="ScenPart_StartingThing_Defined">
        <def>StartingThing_Defined</def>
        <thingDef>Gun_BoltActionRifle</thingDef>
        <count>1</count>
      </li>
      <li Class="ScenPart_StartingThing_Defined">
        <def>StartingThing_Defined</def>
        <thingDef>Gun_Revolver</thingDef>
        <count>1</count>
      </li>
      <li Class="ScenPart_StartingThing_Defined">
        <def>StartingThing_Defined</def>
        <thingDef>MeleeWeapon_Knife</thingDef>
        <stuff>Plasteel</stuff>
        <count>1</count>
      </li>
      <li Class="ScenPart_StartingThing_Defined">
        <def>StartingThing_Defined</def>
        <thingDef>Apparel_FlakPants</thingDef>
        <count>1</count>
      </li>
      <li Class="ScenPart_StartingThing_Defined">
        <def>StartingThing_Defined</def>
        <thingDef>Apparel_FlakVest</thingDef>
        <count>1</count>
      </li>
      <li Class="ScenPart_StartingThing_Defined">
        <def>StartingThing_Defined</def>
        <thingDef>Apparel_AdvancedHelmet</thingDef>
        <stuff>Plasteel</stuff>
        <count>1</count>
      </li>
      <li Class="ScenPart_StartingAnimal">
        <def>StartingAnimal</def>
        <count>1</count>
        <bondToRandomPlayerPawnChance>1.0</bondToRandomPlayerPawnChance>
      </li>
      <li Class="ScenPart_ScatterThingsNearPlayerStart">
        <def>ScatterThingsNearPlayerStart</def>
        <thingDef>Steel</thingDef>
        <count>450</count>
      </li>
      <li Class="ScenPart_ScatterThingsNearPlayerStart">
        <def>ScatterThingsNearPlayerStart</def>
        <thingDef>WoodLog</thingDef>
        <count>300</count>
      </li>
      <li Class="ScenPart_ScatterThingsAnywhere">
        <def>ScatterThingsAnywhere</def>
        <thingDef>ShipChunk</thingDef>
        <allowRoofed>false</allowRoofed>
        <count>3</count>
      </li>
      <li Class="ScenPart_ScatterThingsAnywhere">
        <def>ScatterThingsAnywhere</def>
        <thingDef>Steel</thingDef>
        <count>720</count>
      </li>
      <li Class="ScenPart_ScatterThingsAnywhere">
        <def>ScatterThingsAnywhere</def>
        <thingDef>MealSurvivalPack</thingDef>
        <count>7</count>
      </li>
      <li Class="ScenPart_GameStartDialog">
        <def>GameStartDialog</def>
        <textKey>GameStartDialog</textKey>
        <closeSound>GameStartSting</closeSound>
      </li>
      <li Class="ScenPart_StatFactor">
        <def>StatFactor</def>
        <stat>MiningYield</stat>
        <factor>5</factor>
      </li>
      <li Class="ScenPart_StatFactor">
        <def>StatFactor</def>
        <stat>PlantHarvestYield</stat>
        <factor>3</factor>
      </li>
      <li Class="ScenPart_StatFactor">
        <def>StatFactor</def>
        <stat>ConstructionSpeed</stat>
        <factor>2</factor>
      </li>
      <li Class="ScenPart_StatFactor">
        <def>StatFactor</def>
        <stat>ResearchSpeed</stat>
        <factor>3</factor>
      </li>
    </parts>
  </scenario>
</savedscenario>