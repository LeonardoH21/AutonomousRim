param(
 [Parameter(Mandatory=$true)][string[]]$BaselineSaves,
 [ValidateRange(1,8)][int]$Replicas=2,
 [switch]$CompareParallel,
 [string]$RimWorldDir='C:\Users\Administrador\Downloads\RimWorld.v1.6.4633\RimWorld.v1.6.4633\game',
 [ValidateRange(120,3600)][int]$TimeoutSeconds=900
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root ('outputs\Learning\'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $output -Force|Out-Null
$runs=@()
$combined=$null
foreach($baseline in $BaselineSaves){
 $source=(Resolve-Path -LiteralPath $baseline).Path
 $sourceHash=(Get-FileHash -LiteralPath $source).Hash
 for($replica=1;$replica -le $Replicas;$replica++){
  # Alternate order to avoid always favoring the second arm with a warmed machine.
  $arms=if($replica%2){@('control','candidate')}else{@('candidate','control')}
  if($CompareParallel){$arms+=@('synchronous')}
  foreach($arm in $arms){
   $stage='policy-'+$arm+'-'+$sourceHash.Substring(0,8).ToLowerInvariant()+'-r'+$replica
   $flags=@('autonomousrimfixturebaseline',('autonomousrimreplica'+$replica))
   if($arm -eq 'candidate'){$flags+='autonomousrimcandidate'}
   if($arm -eq 'synchronous'){$flags+='autonomousrimsynchronous'}
   & "$PSScriptRoot\FunctionalStage.ps1" -RimWorldDir $RimWorldDir -Stage $stage -Flag autonomousrimlearningtest -SuccessMarker '[LearningPerformance] DONE' -ExtraFlags $flags -ResumeSave $source -TimeoutSeconds $TimeoutSeconds
   $profile=Get-ChildItem -LiteralPath "$root\.tools\validation" -Directory -Filter "$stage-*"|Sort-Object Name -Descending|Select-Object -First 1
   $target=Join-Path $output $stage
   New-Item -ItemType Directory -Path $target -Force|Out-Null
   foreach($name in @('manifest.json','Player.log','PolicyTrials.xml','Performance.csv')){Copy-Item -LiteralPath (Join-Path $profile.FullName $name) -Destination $target}
   [xml]$trial=Get-Content -LiteralPath (Join-Path $target 'PolicyTrials.xml') -Raw
   if(!$combined){$combined=$trial}else{
    foreach($node in $trial.TrialExchange.Trials.ChildNodes){$combined.TrialExchange.Trials.AppendChild($combined.ImportNode($node,$true))|Out-Null}
   }
   $runs+=[ordered]@{arm=$arm;replica=$replica;sourceHash=$sourceHash;profile=$profile.FullName;result='PASS'}
   $runs|ConvertTo-Json -Depth 5|Set-Content -LiteralPath "$output\runs.json" -Encoding utf8
   $combined.Save("$output\AutonomousRimPolicyTrials.xml")
   if((Get-FileHash -LiteralPath $source).Hash -ne $sourceHash){throw 'Personal baseline changed during comparison.'}
  }
 }
}
Write-Output "RESULTS=$output"
Write-Output 'A completed comparison is not approval: import the XML in Diagnostics; the native gate checks independent colonies, repetitions, compatibility and regressions.'
