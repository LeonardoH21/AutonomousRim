param(
 [string]$RimWorldDir='C:\Users\Administrador\Downloads\RimWorld.v1.6.4633\RimWorld.v1.6.4633\game',
 [ValidateSet('Setup','Construction','Integrated','Combat','Prison','PrisonSafety','PrisonBreak','Commerce','CommerceSafety','Orbital','Caravan')][string]$Stage='Setup',
 [ValidateRange(1,5)][int]$Case=1,
 [switch]$Disadvantage,
 [switch]$PostBattleCare,
 [string]$ResumeSave,
 [switch]$Visible
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
& "$PSScriptRoot\Build.ps1" -RimWorldDir $RimWorldDir
& "$taskRoot\.tools\dotnet\dotnet.exe" build "$taskRoot\Tests\AutonomousRim.RuntimeChecks\AutonomousRim.RuntimeChecks.csproj" -c Release "-p:RimWorldDir=$RimWorldDir"
if($LASTEXITCODE -ne 0){throw 'Runtime build failed; do not launch stale tests.'}
& "$PSScriptRoot\Install.ps1" -RimWorldDir $RimWorldDir
if($Stage -eq 'Setup'){
 if($ResumeSave){throw 'Setup creates a fresh baseline; use Construction or Integrated for continuation.'}
 & "$PSScriptRoot\FunctionalStage.ps1" -RimWorldDir $RimWorldDir -Stage enhanced-world-elite-six -Flag autonomousrimenhancedtest -SuccessMarker '[EnhancedWorld] DONE' -TimeoutSeconds 600 -Visible:$Visible
}else{
 if(!$ResumeSave){
  $ResumeSave=Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves\AutonomousRim_Enhanced_World_EliteSix_BASE.rws'
 }
 [xml]$taskSave=Get-Content -LiteralPath $ResumeSave -Raw
 if($taskSave.savegame.game.scenario.name -ne 'AutonomousRim Enhanced World'){throw 'This runner requires the enhanced six-colonist campaign.'}
 if($Stage -eq 'Combat'){
  $taskFlags=@('autonomousrimelitesix','autonomousrimcombatbaseline','autonomousrimvariedthreats','autonomousrimmeleerevision',('autonomousrimcase'+$Case))
  if($Disadvantage){$taskFlags+='autonomousrimdisadvantage'}
  if($PostBattleCare){$taskFlags+='autonomousrimpostbattlecare'}
  & "$PSScriptRoot\FunctionalStage.ps1" -RimWorldDir $RimWorldDir -Stage ('enhanced-combat-'+$Case) -Flag autonomousrimcombatfive -ExtraFlags $taskFlags -ResumeSave $ResumeSave -SuccessMarker '[FiveCombatTrials] DONE' -TimeoutSeconds 3600 -Visible:$Visible
  return
 }
 if($Stage -in @('Prison','PrisonSafety','PrisonBreak','Commerce','CommerceSafety','Orbital','Caravan')){
  $taskFixtureFlags=@('autonomousrimelitesix','autonomousrimfixturebaseline')
  $taskFixtureFlag=switch($Stage){
   Prison {'autonomousrimprisontest'}
   PrisonSafety {'autonomousrimprisonsafetytest'}
   PrisonBreak {'autonomousrimprisonbreaktest'}
   Commerce {'autonomousrimcommercetest'}
   CommerceSafety {'autonomousrimcommercesafetytest'}
   Orbital {'autonomousrimorbitaltest'}
   Caravan {'autonomousrimcaravantest'}
  }
  $taskFixtureMarker=if($Stage -eq 'PrisonBreak'){'[PrisonBreakTests] DONE'}elseif($Stage.StartsWith('Prison')){'[PrisonTests] DONE'}else{'[CommerceTests] DONE'}
  & "$PSScriptRoot\FunctionalStage.ps1" -RimWorldDir $RimWorldDir -Stage ('enhanced-'+$Stage.ToLowerInvariant()) -Flag $taskFixtureFlag -ExtraFlags $taskFixtureFlags -ResumeSave $ResumeSave -SuccessMarker $taskFixtureMarker -TimeoutSeconds 3600 -Visible:$Visible
  return
 }
 $taskFlags=@('autonomousrimelitesix')
 $taskMarker='[AutonomousRim.ModularTrial] PASS: initial rooms'
 if($Stage -eq 'Integrated'){$taskFlags+=@('autonomousrimintegratedtest','autonomousrimprogressiontrial');$taskMarker='[AutonomousRim.ModularTrial] PASS: integrated twenty days'}
 & "$PSScriptRoot\FunctionalStage.ps1" -RimWorldDir $RimWorldDir -Stage ('enhanced-'+$Stage.ToLowerInvariant()) -Flag autonomousrimmodulartrial -ExtraFlags $taskFlags -ResumeSave $ResumeSave -SuccessMarker $taskMarker -TimeoutSeconds 3600 -Visible:$Visible
}
