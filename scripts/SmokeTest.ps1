param(
    [Parameter(Mandatory = $true)][string]$RimWorldDir,
    [ValidateRange(30, 600)][int]$TimeoutSeconds = 180,
    [switch]$RuntimeChecks
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
$runtimeMod = $null
try {
    $gameArgs = @('-batchmode', '-quicktest', '-screen-width', '1280', '-screen-height', '720', ('-savedatafolder="' + $profile + '"'), '-logFile', ('"' + $logFile + '"'))
    if ($RuntimeChecks) {
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
        $gameArgs += '-autonomousrimtest'
    }
    $testProcess = Start-Process -FilePath $gameExe -ArgumentList $gameArgs -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($testProcess.HasExited) { throw 'RimWorld exited before the colony test completed.' }
        if (Test-Path -LiteralPath $logFile) {
            $logText = Get-Content -LiteralPath $logFile -Raw
            if ($logText -match 'Exception|Error while|XML error|Config error') { throw "Game reported an error. Inspect $logFile" }
            $scanCount = ([regex]::Matches($logText, '\[AutonomousRim\] Scan:')).Count
            if ($scanCount -ge 2 -and $logText.Contains('[AutonomousRim] Loaded successfully.') -and
                (!$RuntimeChecks -or $logText.Contains('[AutonomousRim.Tests] PASS:'))) {
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
