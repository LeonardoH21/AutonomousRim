param([string]$RimWorldDir = 'C:\Users\Administrador\Downloads\RimWorld.v1.6.4633\RimWorld.v1.6.4633\game', [int]$TimeoutSeconds = 900, [switch]$EmergencyChecks, [switch]$WorkScheduleChecks, [ValidateRange(0,5)][int]$TrialCase=0, [switch]$Disadvantage)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$game=(Resolve-Path -LiteralPath $RimWorldDir).Path
if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) { throw 'Close RimWorld before running combat fixtures.' }
if(($TrialCase -gt 0 -and ($EmergencyChecks -or $WorkScheduleChecks)) -or ($EmergencyChecks -and $WorkScheduleChecks)) { throw 'Select one test mode.' }
$profile=Join-Path $root $(if($TrialCase -gt 0){".tools\combat-five\case$TrialCase"}elseif($WorkScheduleChecks){'.tools\work-schedule-tests'}elseif($EmergencyChecks){'.tools\emergency-tests'}else{'.tools\combat-tests'})
New-Item -ItemType Directory -Force -Path "$profile\Config" | Out-Null
[xml]$config='<ModsConfigData><version>1.6.4633</version><activeMods><li>brrainz.harmony</li><li>ludeon.rimworld</li><li>leonardoh21.autonomousrim</li><li>leonardoh21.autonomousrim.runtimechecks</li></activeMods><knownExpansions /></ModsConfigData>'
$config.Save("$profile\Config\ModsConfig.xml")
Set-Content -LiteralPath "$profile\Config\Prefs.xml" -Encoding utf8 -Value '<PrefsData><devMode>True</devMode><runInBackground>True</runInBackground><autosaveIntervalDays>100</autosaveIntervalDays><pauseOnLoad>False</pauseOnLoad><pauseOnError>False</pauseOnError><volumeMaster>0</volumeMaster></PrefsData>'
$addon=Join-Path $game 'Mods\AutonomousRim.RuntimeChecks'
if (Test-Path -LiteralPath $addon) { throw 'Test add-on exists; inspect before retrying.' }
New-Item -ItemType Directory -Force -Path "$addon\About","$addon\Assemblies" | Out-Null
Copy-Item -LiteralPath "$root\.tools\runtime-checks\Assemblies\AutonomousRim.RuntimeChecks.dll" -Destination "$addon\Assemblies\AutonomousRim.RuntimeChecks.dll"
Set-Content -LiteralPath "$addon\About\About.xml" -Encoding utf8 -Value '<ModMetaData><name>AutonomousRim Runtime Checks</name><author>AutonomousRim</author><packageId>leonardoh21.autonomousrim.runtimechecks</packageId><supportedVersions><li>1.6</li></supportedVersions><loadAfter><li>leonardoh21.autonomousrim</li></loadAfter></ModMetaData>'
$log=Join-Path $profile ('Player-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'.log')
$p=$null
try {
    $flag=if($WorkScheduleChecks){'-autonomousrimworkscheduletest'}elseif($EmergencyChecks){'-autonomousrimemergencytest'}else{'-autonomousrimcombattest'}
    if($TrialCase -gt 0) { $flag=@('-autonomousrimcombatfive',"-autonomousrimcase$TrialCase"); if($Disadvantage){$flag+='-autonomousrimdisadvantage'} }
    $arguments=@('-batchmode','-quicktest')+@($flag)+@("-savedatafolder=`"$profile`"",'-logFile',"`"$log`"")
    $p=Start-Process -FilePath "$game\RimWorldWin64.exe" -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output "PID=$($p.Id) LOG=$log"
    $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while([DateTime]::UtcNow -lt $deadline) {
        if($p.HasExited) { throw 'Game exited before combat validation.' }
        if(Test-Path -LiteralPath $log) {
            $body=Get-Content -LiteralPath $log -Raw
            if($body -match '(CombatTests|EmergencyTests|FiveCombatTrials|WorkScheduleTests)\] FAIL|Exception ticking|Error in MapComponent|Exception from long event|Patching exception') { throw "Native test failed: $log" }
            if($WorkScheduleChecks -and $body -match 'WorkScheduleTests\] DONE') { Select-String -LiteralPath $log -Pattern 'WorkScheduleTests\] PASS' | ForEach-Object {$_.Line}; return }
            if($TrialCase -gt 0 -and $body -match 'FiveCombatTrials\] DONE') { Select-String -LiteralPath $log -Pattern 'FiveCombatTrials\] RESULT' | ForEach-Object {$_.Line}; return }
            if((!$EmergencyChecks -and $body -match 'CombatTests\] PASS 3:') -or ($EmergencyChecks -and $body -match 'EmergencyTests\] DONE')) { Select-String -LiteralPath $log -Pattern '(CombatTests|EmergencyTests)\] PASS' | ForEach-Object {$_.Line}; return }
        }
        Start-Sleep -Seconds 2
    }
    throw "Combat test timeout: $log"
} finally {
    if($p -and !$p.HasExited) { Stop-Process -Id $p.Id; $p.WaitForExit(10000) | Out-Null }
    Remove-Item -LiteralPath "$addon\Assemblies\AutonomousRim.RuntimeChecks.dll","$addon\About\About.xml"
    foreach($dir in @("$addon\Assemblies","$addon\About",$addon)) { if(!(Get-ChildItem -LiteralPath $dir -Force)) { Remove-Item -LiteralPath $dir } }
}
