param(
    [Parameter(Mandatory = $true)][string]$RimWorldDir,
    [ValidateRange(60, 7200)][int]$TimeoutSeconds = 1800,
    [switch]$Baseline,
    [switch]$LoadStart,
    [switch]$LoadCheckpoint,
    [switch]$Peaceful,
    [switch]$SkilledFixture,
    [switch]$PlanOnly,
    [switch]$CraftOnly,
    [switch]$RingPlanOnly,
    [switch]$StrategyOnly,
    [switch]$RuntimeChecks = $true,
    [switch]$Visible
)
$ErrorActionPreference = 'Stop'
if ($StrategyOnly -and ($RingPlanOnly -or $Baseline -or $LoadStart -or $LoadCheckpoint -or $PlanOnly -or $CraftOnly -or $SkilledFixture)) { throw 'StrategyOnly is a separate strategy/state fixture.' }
if ($RingPlanOnly -and ($Baseline -or $LoadStart -or $LoadCheckpoint -or $PlanOnly -or $CraftOnly -or $SkilledFixture)) { throw 'RingPlanOnly is a separate geometry/state fixture; do not combine it with other trial modes.' }
if ($LoadCheckpoint -and ($Baseline -or $LoadStart)) { throw 'LoadCheckpoint cannot be combined with Baseline or LoadStart.' }
if ($SkilledFixture -and $Baseline) { throw 'SkilledFixture is a functional fixture, not the ordinary-skill baseline benchmark.' }
if ($PlanOnly -and ($Baseline -or $LoadCheckpoint)) { throw 'PlanOnly validates a new plan, not a baseline or recovery checkpoint.' }
if ($CraftOnly -and (!$SkilledFixture -or $PlanOnly -or $Baseline -or $LoadStart -or $LoadCheckpoint)) { throw 'CraftOnly requires only SkilledFixture and its exported completed save.' }
$projectRoot = Split-Path $PSScriptRoot -Parent
$gameRoot = (Resolve-Path -LiteralPath $RimWorldDir).Path
$gameExe = Join-Path $gameRoot 'RimWorldWin64.exe'
if (!(Test-Path -LiteralPath $gameExe)) { throw 'RimWorld executable not found.' }
if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) { throw 'Close RimWorld before running this isolated test.' }
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'Mods\AutonomousRim\1.6\Assemblies\AutonomousRim.dll'))) { throw 'Install the mod before testing.' }
$profile = Join-Path $projectRoot $(if ($SkilledFixture) { '.tools\skilled-construction' } else { '.tools\normal-construction' })
$configFolder = Join-Path $profile 'Config'
New-Item -ItemType Directory -Path $configFolder -Force | Out-Null
[xml]$config = '<ModsConfigData><version>1.6.4633</version><activeMods><li>brrainz.harmony</li><li>ludeon.rimworld</li></activeMods><knownExpansions /></ModsConfigData>'
foreach ($expansion in @('Royalty', 'Ideology', 'Biotech', 'Anomaly', 'Odyssey')) {
    if (Test-Path -LiteralPath (Join-Path $gameRoot "Data\$expansion")) {
        $package = 'ludeon.rimworld.' + $expansion.ToLowerInvariant()
        $modNode = $config.CreateElement('li'); $modNode.InnerText = $package
        $config.ModsConfigData.activeMods.AppendChild($modNode) | Out-Null
    }
}
$modNode = $config.CreateElement('li'); $modNode.InnerText = 'leonardoh21.autonomousrim'
$config.ModsConfigData.activeMods.AppendChild($modNode) | Out-Null
$config.Save((Join-Path $configFolder 'ModsConfig.xml'))
# Native queued autosave requires OnGUI in Unity batch mode. Keep the observer's
# explicit start/final saves, and put automatic saves beyond this isolated test.
Set-Content -LiteralPath (Join-Path $configFolder 'Prefs.xml') -Encoding utf8 -Value '<PrefsData><devMode>True</devMode><runInBackground>True</runInBackground><autosaveIntervalDays>100</autosaveIntervalDays><pauseOnLoad>False</pauseOnLoad><pauseOnError>False</pauseOnError><fullscreen>False</fullscreen><screenWidth>1280</screenWidth><screenHeight>720</screenHeight><volumeMaster>0</volumeMaster></PrefsData>'
$logFile = Join-Path $profile ('Player-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '.log')
$testProcess = $null
$runtimeMod = $null
try {
    $gameArgs = @('-quicktest', '-screen-width', '1280', '-screen-height', '720', ('-savedatafolder="' + $profile + '"'), '-logFile', ('"' + $logFile + '"'))
    # Unity batch mode skips OnGUI; runtime checks include real HUD rendering.
    if (!$Visible) { $gameArgs = @('-batchmode') + $gameArgs }
    if ($RuntimeChecks) {
        $hudCapture = Join-Path $profile 'AutonomousRim-HUD.png'
        if (Test-Path -LiteralPath $hudCapture) { Remove-Item -LiteralPath $hudCapture }
        $checkDll = Join-Path $projectRoot '.tools\runtime-checks\Assemblies\AutonomousRim.RuntimeChecks.dll'
        if (!(Test-Path -LiteralPath $checkDll)) { throw 'Build Tests/AutonomousRim.RuntimeChecks before requesting runtime checks.' }
        $checkDestination = Join-Path $gameRoot 'Mods\AutonomousRim.RuntimeChecks'
        if (Test-Path -LiteralPath $checkDestination) { throw 'Runtime test directory already exists; inspect it before retrying.' }
        $runtimeMod = $checkDestination
        New-Item -ItemType Directory -Path (Join-Path $runtimeMod 'Assemblies'), (Join-Path $runtimeMod 'About') -Force | Out-Null
        Copy-Item -LiteralPath $checkDll -Destination (Join-Path $runtimeMod 'Assemblies\AutonomousRim.RuntimeChecks.dll')
        Set-Content -LiteralPath (Join-Path $runtimeMod 'About\About.xml') -Encoding utf8 -Value '<ModMetaData><name>AutonomousRim Runtime Checks</name><author>AutonomousRim</author><packageId>leonardoh21.autonomousrim.runtimechecks</packageId><supportedVersions><li>1.6</li></supportedVersions><loadAfter><li>leonardoh21.autonomousrim</li></loadAfter></ModMetaData>'
        $modNode = $config.CreateElement('li'); $modNode.InnerText = 'leonardoh21.autonomousrim.runtimechecks'
        $config.ModsConfigData.activeMods.AppendChild($modNode) | Out-Null
        $config.Save((Join-Path $configFolder 'ModsConfig.xml'))
        $gameArgs += $(if ($StrategyOnly) { '-autonomousrimstrategytest' } elseif ($RingPlanOnly) { '-autonomousrimringtest' } else { '-autonomousrimnormaltest' })
        if ($Baseline) { $gameArgs += '-autonomousrimbaseline' }
        if ($LoadStart) { $gameArgs += '-autonomousrimloadstart' }
        if ($LoadCheckpoint) { $gameArgs += '-autonomousrimloadcheckpoint' }
        if ($Peaceful) { $gameArgs += '-autonomousrimpeaceful' }
        if ($SkilledFixture) { $gameArgs += '-autonomousrimskilledfixture' }
        if ($PlanOnly) { $gameArgs += '-autonomousrimplanonly' }
        if ($CraftOnly) { $gameArgs += '-autonomousrimcrafttest' }
        
    }
    $windowStyle = if ($Visible) { 'Normal' } else { 'Hidden' }
    $testProcess = Start-Process -FilePath $gameExe -ArgumentList $gameArgs -WindowStyle $windowStyle -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($testProcess.HasExited) { throw 'RimWorld exited before the colony test completed.' }
        if (Test-Path -LiteralPath $logFile) {
            $logText = Get-Content -LiteralPath $logFile -Raw
            if ($logText.Contains('[AutonomousRim.StrategyTests] FAIL:')) { throw "Strategy test failed. Inspect $logFile" }
            if ($logText.Contains('[AutonomousRim.StrategyTests] PASS:')) { Write-Output "PASS: native strategic planning, research ownership, upgrades, initial allow and save/load. Log: $logFile"; return }
            if ($logText.Contains('[AutonomousRim.NormalTests] FAIL:') -or $logText.Contains('[AutonomousRim.RingTests] FAIL:')) { throw "Native construction failed. Inspect $logFile" }
            if ($logText -match 'Exception|Error while|XML error|Config error|Attempted to calculate value for disabled stat|Two power nets on the same cell') { throw "Game reported an error. Inspect $logFile" }
            if ($logText.Contains('[AutonomousRim.RingTests] PASS:')) { Write-Output "PASS: ring geometry, priorities, zones and native save/load. Log: $logFile"; return }
            if ($logText.Contains('[AutonomousRim.NormalTests] PASS:')) {
                if ($SkilledFixture -and $CraftOnly) {
                    & (Join-Path $PSScriptRoot 'ExportSkilledFixture.ps1') -SourceSave ConstructionFinishedUpdated
                }
                elseif ($SkilledFixture -and !$PlanOnly) {
                    & (Join-Path $PSScriptRoot 'ExportSkilledFixture.ps1') -SourceSave ConstructionStart
                    & (Join-Path $PSScriptRoot 'ExportSkilledFixture.ps1') -SourceSave ConstructionFinished
                }
                $checked = if ($PlanOnly) { 'compact planning/storage checks' } elseif ($CraftOnly) { 'production/save/load checks' } else { 'native construction' }
                Write-Output "PASS: $checked completed. Log: $logFile"
                return
            }
        }
        Start-Sleep -Seconds 1
    }
    throw "Timed out waiting for colony scans. Inspect $logFile"
}
finally {
    if ($testProcess -and !$testProcess.HasExited) {
        Stop-Process -Id $testProcess.Id -ErrorAction SilentlyContinue
        $testProcess.WaitForExit(10000) | Out-Null
    }
    if ($runtimeMod) {
        # Remove only the exact two files created here, then their empty directories.
        foreach ($relative in @('Assemblies\AutonomousRim.RuntimeChecks.dll', 'About\About.xml')) {
            $createdFile = Join-Path $runtimeMod $relative
            if (Test-Path -LiteralPath $createdFile) { Remove-Item -LiteralPath $createdFile }
        }
        foreach ($createdDirectory in @((Join-Path $runtimeMod 'Assemblies'), (Join-Path $runtimeMod 'About'), $runtimeMod)) {
            if ((Test-Path -LiteralPath $createdDirectory) -and !(Get-ChildItem -LiteralPath $createdDirectory -Force)) {
                Remove-Item -LiteralPath $createdDirectory
            }
        }
    }
}
