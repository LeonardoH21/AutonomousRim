param(
    [string]$RimWorldDir='C:\Users\Administrador\Downloads\RimWorld.v1.6.4633\RimWorld.v1.6.4633\game',
    [int]$TimeoutPerTest=900,
    [switch]$Disadvantage
)
$ErrorActionPreference='Stop'
for($trialIndex=1;$trialIndex -le 5;$trialIndex++) {
    Write-Output "Starting native mixed-equipment combat $trialIndex/5"
    & (Join-Path $PSScriptRoot 'CombatTest.ps1') -RimWorldDir $RimWorldDir -TimeoutSeconds $TimeoutPerTest -TrialCase $trialIndex -Disadvantage:$Disadvantage
}
