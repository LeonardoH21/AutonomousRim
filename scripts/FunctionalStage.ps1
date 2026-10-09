param(
 [string]$RimWorldDir='C:\Users\Administrador\Downloads\RimWorld.v1.6.4633\RimWorld.v1.6.4633\game',
 [Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9-]+$')][string]$Stage,
 [Parameter(Mandatory=$true)][ValidatePattern('^autonomousrim[a-z]+$')][string]$Flag,
 [Parameter(Mandatory=$true)][string]$SuccessMarker,
 [ValidatePattern('^autonomousrim[a-z0-9]+$')][string[]]$ExtraFlags=@(),
 [string]$ResumeSave,
 [ValidateRange(60,3600)][int]$TimeoutSeconds=600
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$game=(Resolve-Path -LiteralPath $RimWorldDir).Path
if(Get-Process RimWorldWin64 -ErrorAction SilentlyContinue){throw 'Close RimWorld before isolated tests.'}
$profile=Join-Path $root ('.tools\validation\'+$Stage+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path "$profile\Config" -Force|Out-Null
[xml]$config='<ModsConfigData><version>1.6.4633</version><activeMods><li>brrainz.harmony</li><li>ludeon.rimworld</li></activeMods><knownExpansions /></ModsConfigData>'
foreach($expansion in @('Royalty','Ideology','Biotech','Anomaly','Odyssey')){
 if(Test-Path -LiteralPath (Join-Path $game "Data\$expansion")){
  $node=$config.CreateElement('li');$node.InnerText='ludeon.rimworld.'+$expansion.ToLowerInvariant();$config.ModsConfigData.activeMods.AppendChild($node)|Out-Null
 }
}
foreach($package in @('leonardoh21.autonomousrim','leonardoh21.autonomousrim.runtimechecks')){
 $node=$config.CreateElement('li');$node.InnerText=$package;$config.ModsConfigData.activeMods.AppendChild($node)|Out-Null
}
$config.Save("$profile\Config\ModsConfig.xml")
Set-Content -LiteralPath "$profile\Config\Prefs.xml" -Encoding utf8 -Value '<PrefsData><devMode>False</devMode><runInBackground>True</runInBackground><autosaveIntervalDays>100</autosaveIntervalDays><pauseOnLoad>False</pauseOnLoad><pauseOnError>False</pauseOnError><fullscreen>False</fullscreen><volumeMaster>0</volumeMaster></PrefsData>'
$addon=Join-Path $game 'Mods\AutonomousRim.RuntimeChecks'
if(Test-Path -LiteralPath $addon){throw 'Runtime add-on exists; inspect before retrying.'}
$p=$null
$log=Join-Path $profile 'Player.log'
try {
 New-Item -ItemType Directory -Path "$addon\About","$addon\Assemblies" -Force|Out-Null
 Copy-Item -LiteralPath "$root\.tools\runtime-checks\Assemblies\AutonomousRim.RuntimeChecks.dll" -Destination "$addon\Assemblies\AutonomousRim.RuntimeChecks.dll"
 Set-Content -LiteralPath "$addon\About\About.xml" -Encoding utf8 -Value '<ModMetaData><name>AutonomousRim Runtime Checks</name><author>AutonomousRim</author><packageId>leonardoh21.autonomousrim.runtimechecks</packageId><supportedVersions><li>1.6</li></supportedVersions><loadAfter><li>leonardoh21.autonomousrim</li></loadAfter></ModMetaData>'
 $manifest=[ordered]@{stage=$Stage;flag=$Flag;extraFlags=@($ExtraFlags);resumeSaveHash=$(if($ResumeSave){(Get-FileHash -LiteralPath $ResumeSave).Hash}else{$null});sourceCommit=(git -C $root rev-parse HEAD);dllHash=(Get-FileHash "$game\Mods\AutonomousRim\1.6\Assemblies\AutonomousRim.dll").Hash;runtimeHash=(Get-FileHash "$addon\Assemblies\AutonomousRim.RuntimeChecks.dll").Hash;result='RUNNING';profile=$profile}
 $manifest|ConvertTo-Json|Set-Content -LiteralPath "$profile\manifest.json" -Encoding utf8
 $arguments=@('-batchmode','-quicktest',('-'+$Flag),'-autonomousrimstagedtest',('-savedatafolder="'+$profile+'"'),'-logFile',('"'+$log+'"'))
 foreach($extraFlag in $ExtraFlags){$arguments+=('-'+$extraFlag)}
 if($ResumeSave){
  New-Item -ItemType Directory -Path "$profile\Saves" -Force|Out-Null
  Copy-Item -LiteralPath $ResumeSave -Destination "$profile\Saves\ModularResume.rws"
  $arguments+='-autonomousrimprogressionresume'
 }
 $p=Start-Process -FilePath "$game\RimWorldWin64.exe" -ArgumentList $arguments -WindowStyle Hidden -PassThru
 Write-Output "STAGE=$Stage PID=$($p.Id) LOG=$log"
 $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
 while([DateTime]::UtcNow -lt $deadline){
  if($p.HasExited){throw 'Game exited before stage completed.'}
  if(Test-Path -LiteralPath $log){
   $body=Get-Content -LiteralPath $log -Raw
   if($body.Contains('Reached max messages limit. Stopping logging to avoid spam.')){throw "Logging limit reached; native result cannot be verified: $profile"}
   if($body -match '\] FAIL\b|Exception ticking|Error in MapComponent|Exception from long event|XML error:|Config error:|Patching exception|Bed ForPrisoners=false'){throw "Stage failed: $log"}
   if($body.Contains($SuccessMarker)){
    $manifest.result='PASS'
    if($Flag -eq 'autonomousrimcombatfive'){
     $outcome=[regex]::Match($body,'\[FiveCombatTrials\] RESULT [^\r\n]*outcome=([^;]+)').Groups[1].Value
     if(!$outcome){throw 'Combat completed without a recorded outcome.'}
     $manifest.outcome=$outcome
     $manifest.result=if($outcome -eq 'DERROTA'){'TACTICAL_FAILURE'}elseif($outcome -eq 'SEM_DESFECHO'){'INCONCLUSIVE'}else{'COMPLETED'}
     if('autonomousrimpostbattlecare' -in $ExtraFlags){
      $manifest.careResult=if($body.Contains('[FiveCombatTrials] CARE PASS:')){'PASS'}else{'NOT_VERIFIED'}
      if($manifest.careResult -eq 'NOT_VERIFIED' -and $manifest.result -eq 'COMPLETED'){$manifest.result='INCONCLUSIVE'}
     }
    }
    $manifest|ConvertTo-Json|Set-Content -LiteralPath "$profile\manifest.json" -Encoding utf8
    Select-String -LiteralPath $log -Pattern 'PASS\b|DONE|FiveCombatTrials\] RESULT'|ForEach-Object {$_.Line};return
   }
  }
  Start-Sleep -Seconds 2
 }
 throw "Stage timeout; evidence preserved: $profile"
}catch {
 if($manifest){
  $manifest.result='FAIL';$manifest.error=$_.Exception.Message
  if($manifest.error.StartsWith('Stage timeout;') -and (
     $Flag -eq 'autonomousrimmodulartrial' -and (Test-Path -LiteralPath "$profile\Saves\ModularCheckpoint.rws") -or
     'autonomousrimpostbattlecare' -in $ExtraFlags -and (Test-Path -LiteralPath "$profile\Saves\CombatCareCheckpoint.rws"))){$manifest.result='INCOMPLETE_TIMEOUT'}
  elseif($manifest.error.StartsWith('Logging limit reached;')){$manifest.result='OBSERVABILITY_FAILURE'}
  $manifest|ConvertTo-Json|Set-Content -LiteralPath "$profile\manifest.json" -Encoding utf8
 }
 throw
}finally {
 if($p -and !$p.HasExited){Stop-Process -Id $p.Id;$p.WaitForExit(10000)|Out-Null}
 foreach($relative in @('Assemblies\AutonomousRim.RuntimeChecks.dll','About\About.xml')){
  $file=Join-Path $addon $relative;if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}
 }
 foreach($directory in @("$addon\Assemblies","$addon\About",$addon)){
  if((Test-Path -LiteralPath $directory) -and !(Get-ChildItem -LiteralPath $directory -Force)){Remove-Item -LiteralPath $directory}
 }
}
