param([Parameter(Mandatory = $true)][string]$RimWorldDir)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$gameRoot = (Resolve-Path -LiteralPath $RimWorldDir).Path
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'RimWorldWin64.exe'))) { throw 'Select the folder containing RimWorldWin64.exe.' }
if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) { throw 'Close RimWorld before installing the mod.' }
$dll = Join-Path $projectRoot '1.6\Assemblies\AutonomousRim.dll'
if (!(Test-Path -LiteralPath $dll)) { throw 'Build the mod before installing.' }
$destination = Join-Path $gameRoot 'Mods\AutonomousRim'
if (Test-Path -LiteralPath $destination) {
    $metadata = Join-Path $destination 'About\About.xml'
    if (!(Test-Path -LiteralPath $metadata)) { throw 'Destination exists without AutonomousRim metadata.' }
    [xml]$existing = Get-Content -LiteralPath $metadata
    if ($existing.ModMetaData.packageId -ne 'LeonardoH21.AutonomousRim') { throw 'Destination belongs to another mod.' }
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($folder in @('About', 'Defs', 'Patches', '1.6')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $folder) -Destination $destination -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'LoadFolders.xml') -Destination $destination -Force
$installedDll = Join-Path $destination '1.6\Assemblies\AutonomousRim.dll'
if ((Get-FileHash -LiteralPath $dll).Hash -ne (Get-FileHash -LiteralPath $installedDll).Hash) { throw 'Installed DLL verification failed.' }
Write-Output "Installed and verified: $destination"
