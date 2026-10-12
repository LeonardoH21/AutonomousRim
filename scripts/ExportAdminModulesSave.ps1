param([Parameter(Mandatory=$true)][string]$ValidationProfile,[switch]$CopyToGameSaves)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskProfile=(Resolve-Path -LiteralPath $ValidationProfile).Path
$taskLog=Get-Content -LiteralPath (Join-Path $taskProfile 'Player.log') -Raw
if(!$taskLog.Contains('[AllModules] DONE') -or !$taskLog.Contains('[AllModules] PASS: native physical geometry') -or $taskLog.Contains('[AllModules] FAIL')){throw 'Completed admin module audit is required.'}
$taskDocument=New-Object System.Xml.XmlDocument
$taskDocument.PreserveWhitespace=$true
$taskDocument.Load((Join-Path $taskProfile 'Saves\AutonomousRim-Modulos-Admin.rws'))
foreach($node in @($taskDocument.SelectNodes("//*[@Class and starts-with(@Class,'AutonomousRim.RuntimeChecks.')]"))){$node.ParentNode.RemoveChild($node)|Out-Null}
$taskIds=@($taskDocument.SelectNodes('/savegame/meta/modIds/li'))
for($index=$taskIds.Count-1;$index -ge 0;$index--){
 if($taskIds[$index].InnerText -ne 'leonardoh21.autonomousrim.runtimechecks'){continue}
 foreach($listName in @('modIds','modNames','modSteamIds')){
  $entries=@($taskDocument.SelectNodes("/savegame/meta/$listName/li"))
  if($entries.Count -ne $taskIds.Count){throw 'Unexpected save metadata.'}
  $entries[$index].ParentNode.RemoveChild($entries[$index])|Out-Null
 }
}
$taskFolder=Join-Path $taskRoot 'outputs\ModuleAudit'
New-Item -ItemType Directory -Path $taskFolder -Force|Out-Null
$taskName='AutonomousRim-Modulos-ADMIN-'+(Split-Path $taskProfile -Leaf)+'.rws'
$taskOutput=Join-Path $taskFolder $taskName
if(Test-Path -LiteralPath $taskOutput){throw 'Export already exists; will not overwrite.'}
$taskDocument.Save($taskOutput)
Write-Output "Exported ADMIN fixture (not survival proof): $taskOutput"
if($CopyToGameSaves){
 $taskDestination=Join-Path $env:USERPROFILE ('AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves\'+$taskName)
 if(Test-Path -LiteralPath $taskDestination){throw 'Player save already exists; will not overwrite.'}
 Copy-Item -LiteralPath $taskOutput -Destination $taskDestination
 if((Get-FileHash -LiteralPath $taskOutput).Hash -ne (Get-FileHash -LiteralPath $taskDestination).Hash){throw 'Export hash mismatch.'}
 Write-Output "Copied and verified: $taskDestination"
}
