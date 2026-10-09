param(
 [Parameter(Mandatory=$true,ParameterSetName='Legacy')][ValidatePattern('^[a-zA-Z0-9-]+$')][string]$RunName,
 [Parameter(Mandatory=$true,ParameterSetName='Validation')][string]$ValidationProfile,
 [Parameter(ParameterSetName='Validation')][switch]$Integrated,
 [switch]$Progression,
 [switch]$CopyToGameSaves
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$profile=if($ValidationProfile){(Resolve-Path -LiteralPath $ValidationProfile).Path}else{Join-Path $projectRoot ('.tools\modular-construction-'+$RunName)}
if($ValidationProfile){$RunName=Split-Path $profile -Leaf}
$source=Join-Path $profile $(if($Integrated){'Saves\IntegratedTwentyDaysComplete.rws'}elseif($Progression){'Saves\ModularSteelComplete.rws'}else{'Saves\ModularInitialComplete.rws'})
if(!(Test-Path -LiteralPath $source)){throw 'Completed native trial with working refrigeration is required.'}
$document=New-Object System.Xml.XmlDocument
$document.PreserveWhitespace=$true
$document.Load($source)
$savedMilestones=@($document.SelectNodes("//*[@Class='AutonomousRim.RuntimeChecks.ModularConstructionTrial']/nativeTrialMilestones/li") | ForEach-Object {$_.InnerText})
$logs=@(Get-ChildItem -LiteralPath $profile -Filter 'Player*.log')
if(!($logs|Where-Object {
 $text=Get-Content -LiteralPath $_.FullName -Raw
 # Resumed trials preserve the refrigeration milestone in the save rather than repeat its first log entry.
 $coldProof=$text.Contains('MILESTONE refrigeration:') -or ($Integrated -and 'refrigeration' -in $savedMilestones)
 $text.Contains('[AutonomousRim.ModularTrial] PASS:') -and $coldProof -and
 (!$Progression -or $text.Contains('native research, organized workshop')) -and
 (!$Integrated -or $text.Contains('[AutonomousRim.ModularTrial] PASS: integrated twenty days;'))
})){throw 'Completed native trial with working refrigeration is required.'}
if($Progression -or $Integrated){
 $craftProof=@($document.SelectNodes("//*[@Class='AutonomousRim.RuntimeChecks.ModularConstructionTrial']/nativeCraftProof/li") | ForEach-Object {$_.InnerText})
 foreach($required in @('Apparel_SimpleHelmet','Apparel_PlateArmor','MeleeWeapon_LongSword')){
  if($required -notin $craftProof){throw "Native crafting proof missing: $required"}
 }
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
$name=$(if($Integrated){'AutonomousRim-Integrada-20dias-'}else{'AutonomousRim-Modular-13x13-'})+$RunName+'.rws'
$destination=Join-Path $profile ('Saves\'+$name)
if(Test-Path -LiteralPath $destination){throw 'Export already exists; preserve the existing copy.'}
$document.Save($destination)
Write-Output "Exported native colony: $destination"
if($CopyToGameSaves){
 $playerSaveFolder=Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves'
 New-Item -ItemType Directory -Path $playerSaveFolder -Force|Out-Null
 $playerSave=Join-Path $playerSaveFolder $name
 if(Test-Path -LiteralPath $playerSave){throw 'Player save already exists; it will not be overwritten.'}
 Copy-Item -LiteralPath $destination -Destination $playerSave
 if((Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath $playerSave).Hash){throw 'Save copy verification failed.'}
 Write-Output "Copied and verified: $playerSave"
}
