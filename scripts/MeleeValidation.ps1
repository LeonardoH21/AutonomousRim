param(
 [string]$ResumeSave='C:\Users\Administrador\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves\TESTE V3.rws',
 [string]$RimWorldDir='C:\Users\Administrador\Downloads\RimWorld.v1.6.4633\RimWorld.v1.6.4633\game',
 [ValidateRange(1,6)][int]$FirstCase=1,
 [ValidateRange(1,6)][int]$LastCase=6,
 [ValidateRange(60,3600)][int]$TimeoutSeconds=600,
 [switch]$IncludeProbe
)
$ErrorActionPreference='Stop'
if($FirstCase -gt $LastCase){throw 'FirstCase must not exceed LastCase.'}
$runner=Join-Path $PSScriptRoot 'FunctionalStage.ps1'
$projectRoot=Split-Path $PSScriptRoot -Parent
$productionPath=Join-Path $RimWorldDir 'Mods\AutonomousRim\1.6\Assemblies\AutonomousRim.dll'
$runtimePath=Join-Path $projectRoot '.tools\runtime-checks\Assemblies\AutonomousRim.RuntimeChecks.dll'
$productionHash=(Get-FileHash -LiteralPath $productionPath).Hash
$runtimeHash=(Get-FileHash -LiteralPath $runtimePath).Hash
function Assert-CombatBinaries {
 if((Get-FileHash -LiteralPath $productionPath).Hash -ne $productionHash -or
    (Get-FileHash -LiteralPath $runtimePath).Hash -ne $runtimeHash -or
    (Get-FileHash -LiteralPath (Join-Path $projectRoot '1.6\Assemblies\AutonomousRim.dll')).Hash -ne $productionHash){
  throw 'Combat binaries changed or are not installed consistently. Build/install first, then resume the selected case.'
 }
}
Assert-CombatBinaries
if($IncludeProbe){
 & $runner -RimWorldDir $RimWorldDir -Stage melee-cooperation-probe -Flag autonomousrimmeleecoordinationtest -ExtraFlags autonomousrimelitesix,autonomousrimfixturebaseline -SuccessMarker '[MeleeCoordination] DONE' -ResumeSave $ResumeSave -TimeoutSeconds $TimeoutSeconds
}
# Each case gets a fresh process and a disposable copy of the same six originals.
# Tactical defeats/inconclusive fights remain recorded as such by FunctionalStage.
$cases=@(
 @{name='melee-after-balanced';number=2;varied=$false;hard=$false},
 @{name='melee-after-mixed';number=3;varied=$true;hard=$false},
 @{name='melee-doors-advantage';number=1;varied=$true;hard=$false},
 @{name='melee-two-flanks-disadvantage';number=3;varied=$true;hard=$true},
 @{name='melee-manhunters';number=4;varied=$true;hard=$false},
 @{name='melee-mechanoids';number=5;varied=$true;hard=$false}
)
for($caseIndex=$FirstCase;$caseIndex -le $LastCase;$caseIndex++){
 Assert-CombatBinaries
 $case=$cases[$caseIndex-1]
 $flags=@('autonomousrimelitesix','autonomousrimcombatbaseline','autonomousrimmeleerevision',('autonomousrimcase'+$case.number))
 if($case.varied){$flags+='autonomousrimvariedthreats'}
 if($case.hard){$flags+='autonomousrimdisadvantage'}
 Write-Output "MELEE CASE $caseIndex/6: $($case.name)"
 & $runner -RimWorldDir $RimWorldDir -Stage $case.name -Flag autonomousrimcombatfive -ExtraFlags $flags -SuccessMarker '[FiveCombatTrials] DONE' -ResumeSave $ResumeSave -TimeoutSeconds $TimeoutSeconds
}
