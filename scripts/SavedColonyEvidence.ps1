param(
    [Parameter(Mandatory=$true)][string]$Save,
    [Parameter(Mandatory=$true)][string]$Log,
    [switch]$RequireTwentyDays
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$profile = Join-Path $root '.tools\saved-colony'
$doc = New-Object System.Xml.XmlDocument
$doc.Load((Resolve-Path -LiteralPath $Save).Path)
$ticks = [int]$doc.SelectSingleNode('/savegame/game/tickManager/ticksGame').InnerText
$expected = @('Human691','Human572','Human715','Human795','Human581')
$pawns = foreach ($id in $expected) {
    $pawn = $doc.SelectSingleNode("//*[@Class='Pawn'][id='$id']")
    if (!$pawn) { throw "Original colonist missing: $id" }
    $health = $pawn.SelectSingleNode('healthTracker/healthState')
    if ($health -and $health.InnerText -eq 'Dead') { throw "Original colonist died: $id" }
    [pscustomobject]@{
        Id = $id
        Position = $pawn.SelectSingleNode('pos').InnerText
        HealthState = $(if ($health) { $health.InnerText } else { 'Mobile (native default)' })
        Malnutrition = @($pawn.SelectNodes("healthTracker/hediffSet/hediffs/li[def='Malnutrition']")).Count
    }
}
$projects = @($doc.SelectNodes("//*[@Class='AutonomousRim.Core.AutonomousRimMapComponent']/baseProjects/li"))
$completed = @($projects | Where-Object { $_.SelectSingleNode('completed').InnerText -eq 'True' }).Count
$lines = Get-Content -LiteralPath $Log
$rows = foreach ($line in $lines) {
    if ($line -match 'PROGRESS day=([\d.]+).*foodDays=([^;]+); projects=(\d+); complete=(\d+); buildings=(\d+);') {
        [pscustomobject]@{ Day=$Matches[1]; FoodDays=$Matches[2]; Projects=$Matches[3]; Completed=$Matches[4]; Buildings=$Matches[5] }
    }
}
$rows | Export-Csv -LiteralPath "$profile\progress.csv" -NoTypeInformation -Encoding utf8
$original = 'C:\Users\Administrador\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves\TESTE DO MOD.rws'
$originalHash = (Get-FileHash -LiteralPath $original).Hash
if ($originalHash -ne 'C5161A189E42263EF63185DDBA7529739240FBD538859B05E20E8C84B5325945') { throw 'Original save changed.' }
if ($RequireTwentyDays -and ($ticks -lt 1200235 -or !($lines | Select-String -SimpleMatch '[AutonomousRim.SaveTest] FINISHED'))) { throw 'Twenty-day trial has not finished.' }
$evidence = [pscustomobject]@{
    Save=(Resolve-Path -LiteralPath $Save).Path
    Log=(Resolve-Path -LiteralPath $Log).Path
    Tick=$ticks
    DaysAfterInitialArrival=($ticks-235)/60000.0
    Colonists=$pawns
    ProjectCount=$projects.Count
    CompletedProjects=$completed
    UnfinishedProjects=@($projects | Where-Object { $_.SelectSingleNode('completed').InnerText -ne 'True' } | ForEach-Object { [pscustomobject]@{ Kind=$_.kind; State=$_.state; Reason=$_.blockReason } })
    AllowEvents=@($lines | Select-String -SimpleMatch '[AutonomousRim] Allow:').Count
    NativeSowObservations=@($lines | Select-String -SimpleMatch '/job=Sow/').Count
    NativeCookingObservations=@($lines | Select-String -SimpleMatch '/job=DoBill/').Count
    NativeHuntObservations=@($lines | Select-String -SimpleMatch '/job=Hunt/').Count
    GameplayExceptions=@($lines | Select-String -Pattern 'Exception ticking|Error in MapComponent|Exception in MapComponent').Count
    CrossReferenceWarnings=@($lines | Select-String -SimpleMatch 'Could not resolve cross-reference').Count
    OriginalSHA256=$originalHash
}
$evidence | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$profile\evidence.json" -Encoding utf8
$evidence | ConvertTo-Json -Depth 6
