param(
    [Parameter(Mandatory=$true)][string]$RimWorldDir,
    [ValidateRange(60,14400)][int]$TimeoutSeconds=7200,
    [switch]$GeometryOnly,
    [switch]$Resume,
    [switch]$FromFailure,
    [switch]$FromStart
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$gameRoot=(Resolve-Path -LiteralPath $RimWorldDir).Path
if(Get-Process RimWorldWin64 -ErrorAction SilentlyContinue){throw 'Close RimWorld before running the isolated trial.'}
$profile=Join-Path $projectRoot '.tools\courtyard-construction'
$configFolder=Join-Path $profile 'Config'
New-Item -ItemType Directory -Path $configFolder -Force | Out-Null
[xml]$config='<ModsConfigData><version>1.6.4633</version><activeMods><li>brrainz.harmony</li><li>ludeon.rimworld</li></activeMods><knownExpansions /></ModsConfigData>'
foreach($expansion in @('Royalty','Ideology','Biotech','Anomaly','Odyssey')){
 if(Test-Path -LiteralPath (Join-Path $gameRoot "Data\$expansion")){
  $item=$config.CreateElement('li');$item.InnerText='ludeon.rimworld.'+$expansion.ToLowerInvariant();$config.ModsConfigData.activeMods.AppendChild($item)|Out-Null
 }
}
foreach($package in @('leonardoh21.autonomousrim','leonardoh21.autonomousrim.runtimechecks')){
 $item=$config.CreateElement('li');$item.InnerText=$package;$config.ModsConfigData.activeMods.AppendChild($item)|Out-Null
}
$config.Save((Join-Path $configFolder 'ModsConfig.xml'))
Set-Content -LiteralPath (Join-Path $configFolder 'Prefs.xml') -Encoding utf8 -Value '<PrefsData><devMode>False</devMode><runInBackground>True</runInBackground><autosaveIntervalDays>100</autosaveIntervalDays><pauseOnLoad>False</pauseOnLoad><pauseOnError>False</pauseOnError><fullscreen>False</fullscreen><volumeMaster>0</volumeMaster></PrefsData>'
$logFile=Join-Path $profile ('Player-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'.log')
$runtimeMod=Join-Path $gameRoot 'Mods\AutonomousRim.RuntimeChecks'
if(Test-Path -LiteralPath $runtimeMod){throw 'Runtime directory exists; inspect before reusing.'}
$testProcess=$null
try{
 New-Item -ItemType Directory -Path (Join-Path $runtimeMod 'Assemblies'),(Join-Path $runtimeMod 'About') -Force|Out-Null
 Copy-Item -LiteralPath (Join-Path $projectRoot '.tools\runtime-checks\Assemblies\AutonomousRim.RuntimeChecks.dll') -Destination (Join-Path $runtimeMod 'Assemblies\AutonomousRim.RuntimeChecks.dll')
 Set-Content -LiteralPath (Join-Path $runtimeMod 'About\About.xml') -Encoding utf8 -Value '<ModMetaData><name>AutonomousRim Runtime Checks</name><author>AutonomousRim</author><packageId>leonardoh21.autonomousrim.runtimechecks</packageId><supportedVersions><li>1.6</li></supportedVersions><loadAfter><li>leonardoh21.autonomousrim</li></loadAfter></ModMetaData>'
 $gameArgs=@('-batchmode','-quicktest','-autonomousrimcourtyardtrial',('-savedatafolder="'+$profile+'"'),'-logFile',('"'+$logFile+'"'))
 if($GeometryOnly){$gameArgs+='-autonomousrimcourtyardgeometry'}
 if($Resume){$gameArgs+='-autonomousrimcourtyardresume'}
 if($FromFailure){$gameArgs+='-autonomousrimcourtyardfromfailure'}
 if($FromStart){$gameArgs+='-autonomousrimcourtyardfromstart'}
 $testProcess=Start-Process -FilePath (Join-Path $gameRoot 'RimWorldWin64.exe') -ArgumentList $gameArgs -WindowStyle Hidden -PassThru
 Write-Output "Trial process $($testProcess.Id); log: $logFile"
 $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
 while([DateTime]::UtcNow -lt $deadline){
  if($testProcess.HasExited){throw "Game exited. Inspect $logFile"}
  if(Test-Path -LiteralPath $logFile){
   $logText=Get-Content -LiteralPath $logFile -Raw
   if($logText.Contains('[AutonomousRim.CourtyardTrial] FAIL:')){throw "Native trial failed. Inspect $logFile"}
   if($GeometryOnly -and $logText.Contains('[AutonomousRim.CourtyardTrial] GEOMETRY PASS:')){Write-Output "PASS: native geometry. $logFile";return}
   if($logText.Contains('[AutonomousRim.CourtyardTrial] PASS:')){Write-Output "PASS: full normal construction. $logFile";return}
   if($logText.Contains('[AutonomousRim.CourtyardTrial] CHECKPOINT:')){Write-Output "Checkpoint saved for Resume. $logFile";return}
  }
  Start-Sleep -Seconds 2
 }
 throw "Trial time limit; checkpoint retained for Resume. Inspect $logFile"
}finally{
 if($testProcess -and !$testProcess.HasExited){Stop-Process -Id $testProcess.Id -ErrorAction SilentlyContinue;$testProcess.WaitForExit(10000)|Out-Null}
 foreach($relative in @('Assemblies\AutonomousRim.RuntimeChecks.dll','About\About.xml')){
  $createdFile=Join-Path $runtimeMod $relative;if(Test-Path -LiteralPath $createdFile){Remove-Item -LiteralPath $createdFile}
 }
 foreach($folder in @((Join-Path $runtimeMod 'Assemblies'),(Join-Path $runtimeMod 'About'),$runtimeMod)){
  if((Test-Path -LiteralPath $folder)-and !(Get-ChildItem -LiteralPath $folder -Force)){Remove-Item -LiteralPath $folder}
 }
}
