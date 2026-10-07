param([switch]$CopyToGameSaves)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$profile=Join-Path $projectRoot '.tools\courtyard-construction'
$source=Join-Path $profile 'Saves\CourtyardFinished.rws'
$evidence=Join-Path $profile 'courtyard-result.txt'
if(!(Test-Path -LiteralPath $source) -or !(Test-Path -LiteralPath $evidence)){
 throw 'The complete native-construction save and its verification evidence are required.'
}
if(!(Get-Content -LiteralPath $evidence -Raw).Contains('planned task references verified')){
 throw 'Full-construction verification evidence is missing.'
}
$document=New-Object System.Xml.XmlDocument
$document.PreserveWhitespace=$true
$document.Load($source)
$observer=$document.SelectSingleNode("//*[@Class='AutonomousRim.RuntimeChecks.CourtyardConstructionTrial']")
if(!$observer -or $observer.SelectSingleNode('courtyardFinished').InnerText -ne 'True'){
 throw 'The saved observer did not confirm full construction.'
}
# Remove only test-observer components and their matching mod metadata entries.
# Keep the playable colony, resources, jobs, buildings and automation as saved.
foreach($node in @($document.SelectNodes("//*[@Class and starts-with(@Class, 'AutonomousRim.RuntimeChecks.')]"))){
 $node.ParentNode.RemoveChild($node)|Out-Null
}
$ids=@($document.SelectNodes('/savegame/meta/modIds/li'))
for($index=$ids.Count-1;$index -ge 0;$index--){
 if($ids[$index].InnerText -ne 'leonardoh21.autonomousrim.runtimechecks'){continue}
 foreach($listName in @('modIds','modNames','modSteamIds')){
  $entries=@($document.SelectNodes("/savegame/meta/$listName/li"))
  if($entries.Count -ne $ids.Count){throw "Unexpected metadata list: $listName"}
  $entries[$index].ParentNode.RemoveChild($entries[$index])|Out-Null
 }
}
$name='AutonomousRim-Nucleo-Completo.rws'
$destination=Join-Path $profile ('Saves\'+$name)
$document.Save($destination)
Write-Output "Exported completed playable colony: $destination"
if($CopyToGameSaves){
 $playerSaveFolder=Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves'
 New-Item -ItemType Directory -Path $playerSaveFolder -Force|Out-Null
 $playerSave=Join-Path $playerSaveFolder $name
 Copy-Item -LiteralPath $destination -Destination $playerSave -Force
 if((Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath $playerSave).Hash){throw 'Save copy verification failed.'}
 Write-Output "Copied and verified in the player save folder: $playerSave"
}
