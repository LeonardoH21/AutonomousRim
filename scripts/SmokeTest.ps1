param(
    [Parameter(Mandatory = $true)][string]$RimWorldDir,
    [ValidateRange(30, 600)][int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$gameRoot = (Resolve-Path -LiteralPath $RimWorldDir).Path
$gameExe = Join-Path $gameRoot 'RimWorldWin64.exe'
if (!(Test-Path -LiteralPath $gameExe)) { throw 'RimWorld executable not found.' }
if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) { throw 'Close RimWorld before running this isolated test.' }
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'Mods\AutonomousRim\1.6\Assemblies\AutonomousRim.dll'))) { throw 'Install the mod before testing.' }
$profile = Join-Path $projectRoot '.tools\perception-test'
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
Set-Content -LiteralPath (Join-Path $configFolder 'Prefs.xml') -Encoding utf8 -Value '<PrefsData><devMode>True</devMode><runInBackground>True</runInBackground><pauseOnLoad>False</pauseOnLoad><pauseOnError>False</pauseOnError><fullscreen>False</fullscreen><screenWidth>1280</screenWidth><screenHeight>720</screenHeight><volumeMaster>0</volumeMaster></PrefsData>'
$logFile = Join-Path $profile ('Player-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '.log')
$testProcess = $null
try {
    $testProcess = Start-Process -FilePath $gameExe -ArgumentList @('-batchmode', '-quicktest', '-screen-width', '1280', '-screen-height', '720', ('-savedatafolder="' + $profile + '"'), '-logFile', ('"' + $logFile + '"')) -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($testProcess.HasExited) { throw 'RimWorld exited before the colony test completed.' }
        if (Test-Path -LiteralPath $logFile) {
            $logText = Get-Content -LiteralPath $logFile -Raw
            if ($logText -match 'Exception|Error while|XML error|Config error') { throw "Game reported an error. Inspect $logFile" }
            $scanCount = ([regex]::Matches($logText, '\[AutonomousRim\] Scan:')).Count
            if ($scanCount -ge 2 -and $logText.Contains('[AutonomousRim] Loaded successfully.')) {
                Write-Output "PASS: mod loaded and $scanCount colony scans completed without exceptions. Log: $logFile"
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
}
