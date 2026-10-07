param(
 [Parameter(Mandatory=$true)][ValidatePattern('^[a-zA-Z0-9-]+$')][string]$RunName,
 [switch]$CopyToGameSaves
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$profile=Join-Path $projectRoot ('.tools\modular-construction-'+$RunName)
$source=Join-Path $profile 'Saves\ModularInitialComplete.rws'
$logs=@(Get-ChildItem -LiteralPath $profile -Filter 'Player-*.log')
if(!(Test-Path -LiteralPath $source) -or !($logs|Where-Object {
 $text=Get-Content -LiteralPath $_.FullName -Raw
 $text.Contains('[AutonomousRim.ModularTrial] PASS:') -and $text.Contains('MILESTONE refrigeration:')
})){throw 'Completed native trial with working refrigeration is required.'}
$document=New-Object System.Xml.XmlDocument
$document.PreserveWhitespace=$true
$document.Load($source)
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
$name='AutonomousRim-Modular-13x13-'+$RunName+'.rws'
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
