param(
 [string]$RimWorldDir='C:\Users\Administrador\Downloads\RimWorld.v1.6.4633\RimWorld.v1.6.4633\game',
 [string]$ResumeSave,
 [switch]$Visible
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
if(!$ResumeSave){$ResumeSave=Join-Path $taskRoot 'outputs\EnhancedWorld\AutonomousRim_Enhanced_World_EliteSix_BASE.rws'}
& "$PSScriptRoot\Build.ps1" -RimWorldDir $RimWorldDir
& "$taskRoot\.tools\dotnet\dotnet.exe" build "$taskRoot\Tests\AutonomousRim.RuntimeChecks\AutonomousRim.RuntimeChecks.csproj" --no-restore -c Release "-p:RimWorldDir=$RimWorldDir"
if($LASTEXITCODE -ne 0){throw 'Runtime build failed.'}
& "$PSScriptRoot\Install.ps1" -RimWorldDir $RimWorldDir
& "$PSScriptRoot\FunctionalStage.ps1" -RimWorldDir $RimWorldDir -Stage all-modules-admin -Flag autonomousrimallmodulestest -ExtraFlags autonomousrimfixturebaseline -SuccessMarker '[AllModules] DONE' -ResumeSave $ResumeSave -TimeoutSeconds 600 -Visible:$Visible
