param(
    [Parameter(Mandatory = $true)][string]$RimWorldDir,
    [string]$HarmonyDir
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$compiler = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { 'dotnet' }
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\packages'
$buildArgs = @('build', (Join-Path $projectRoot 'Source\AutonomousRim\AutonomousRim.csproj'), '--configuration', 'Release', "-p:RimWorldDir=$RimWorldDir")
if ($HarmonyDir) { $buildArgs += "-p:HarmonyDir=$HarmonyDir" }
& $compiler @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'AutonomousRim build failed.' }
