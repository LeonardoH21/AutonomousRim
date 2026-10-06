param(
    [switch]$Diagnose,
    [switch]$FinishBase,
    [string]$SourceSave = 'C:\Users\Administrador\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves\TESTE DO MOD.rws'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$game = 'C:\Users\Administrador\Downloads\RimWorld.v1.6.4633\RimWorld.v1.6.4633\game'
$userData = 'C:\Users\Administrador\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios'
if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) { throw 'RimWorld is already running.' }
$profile = Join-Path $root '.tools\saved-colony'
New-Item -ItemType Directory -Force -Path "$profile\Config", "$profile\Saves" | Out-Null
if (!(Test-Path -LiteralPath $SourceSave -PathType Leaf)) { throw "Save not found: $SourceSave" }
Copy-Item -LiteralPath $SourceSave -Destination "$profile\Saves\TESTE DO MOD.rws"
[xml]$config = Get-Content -LiteralPath "$userData\Config\ModsConfig.xml" -Raw
$node = $config.CreateElement('li'); $node.InnerText = 'leonardoh21.autonomousrim.runtimechecks'
$config.ModsConfigData.activeMods.AppendChild($node) | Out-Null
$config.Save("$profile\Config\ModsConfig.xml")
Set-Content -LiteralPath "$profile\Config\Prefs.xml" -Encoding utf8 -Value '<PrefsData><devMode>True</devMode><runInBackground>True</runInBackground><autosaveIntervalDays>100</autosaveIntervalDays><pauseOnLoad>False</pauseOnLoad><pauseOnError>False</pauseOnError><fullscreen>False</fullscreen><screenWidth>1280</screenWidth><screenHeight>720</screenHeight><volumeMaster>0</volumeMaster></PrefsData>'
$addon = Join-Path $game 'Mods\AutonomousRim.RuntimeChecks'
if (Test-Path -LiteralPath $addon) { throw 'Test add-on already exists; inspect before reusing it.' }
New-Item -ItemType Directory -Force -Path "$addon\About", "$addon\Assemblies" | Out-Null
Copy-Item -LiteralPath "$root\.tools\runtime-checks\Assemblies\AutonomousRim.RuntimeChecks.dll" -Destination "$addon\Assemblies\AutonomousRim.RuntimeChecks.dll"
Set-Content -LiteralPath "$addon\About\About.xml" -Encoding utf8 -Value '<ModMetaData><name>AutonomousRim Runtime Checks</name><author>AutonomousRim</author><packageId>leonardoh21.autonomousrim.runtimechecks</packageId><supportedVersions><li>1.6</li></supportedVersions></ModMetaData>'
$log = Join-Path $profile ('Player-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '.log')
$argsList = @('-batchmode','-quicktest','-autonomousrimsavedtest', "-savedatafolder=`"$profile`"", '-logFile', "`"$log`"")
if ($Diagnose) { $argsList += '-autonomousrimdiagnose' }
if ($FinishBase) { $argsList += '-autonomousrimfinishbase' }
$p = Start-Process -FilePath "$game\RimWorldWin64.exe" -ArgumentList $argsList -WindowStyle Hidden -PassThru
Write-Output "PID=$($p.Id) LOG=$log"
try {
    $deadline = [DateTime]::UtcNow.AddHours(5)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($p.HasExited) { throw 'Game exited unexpectedly.' }
        if ((Test-Path -LiteralPath $log) -and (Select-String -LiteralPath $log -SimpleMatch '[AutonomousRim.SaveTest] FAIL:' -Quiet)) { throw "Trial failed; inspect $log" }
        if ((Test-Path -LiteralPath $log) -and (Select-String -LiteralPath $log -SimpleMatch '[AutonomousRim.SaveTest] FINISHED' -Quiet)) { Get-Content -LiteralPath $log -Tail 8; return }
        Start-Sleep -Seconds 5
    }
    throw 'Trial timeout; inspect log.'
} finally {
    if (!$p.HasExited) { Stop-Process -Id $p.Id; $p.WaitForExit(10000) | Out-Null }
    Remove-Item -LiteralPath "$addon\Assemblies\AutonomousRim.RuntimeChecks.dll", "$addon\About\About.xml"
    foreach ($d in @("$addon\Assemblies", "$addon\About", $addon)) { if (!(Get-ChildItem -LiteralPath $d -Force)) { Remove-Item -LiteralPath $d } }
}
