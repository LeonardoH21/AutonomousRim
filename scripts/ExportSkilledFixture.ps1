param(
    [ValidateSet('ConstructionStart', 'ConstructionFinished', 'ConstructionFinishedUpdated')][string]$SourceSave = 'ConstructionStart'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$saveFolder = Join-Path $projectRoot '.tools\skilled-construction\Saves'
$source = Join-Path $saveFolder ($SourceSave + '.rws')
if (!(Test-Path -LiteralPath $source)) { throw "Skilled fixture save not found: $source" }
$document = New-Object System.Xml.XmlDocument
$document.PreserveWhitespace = $true
$document.Load($source)
# Export the already saved test colony without its observer add-on. Colonists,
# resources, structures and production automation remain exactly as saved.
foreach ($node in @($document.SelectNodes("//*[@Class and starts-with(@Class, 'AutonomousRim.RuntimeChecks.')]"))) {
    $node.ParentNode.RemoveChild($node) | Out-Null
}
$ids = @($document.SelectNodes('/savegame/meta/modIds/li'))
for ($index = $ids.Count - 1; $index -ge 0; $index--) {
    if ($ids[$index].InnerText -ne 'leonardoh21.autonomousrim.runtimechecks') { continue }
    foreach ($listName in @('modIds', 'modNames', 'modSteamIds')) {
        $entries = @($document.SelectNodes("/savegame/meta/$listName/li"))
        if ($entries.Count -ne $ids.Count) { throw "Unexpected save metadata list: $listName" }
        $entries[$index].ParentNode.RemoveChild($entries[$index]) | Out-Null
    }
}
$name = if ($SourceSave -eq 'ConstructionStart') { 'AutonomousRim-Teste-Skill20-Inicio' } else { 'AutonomousRim-Teste-BasePronta' }
$destination = Join-Path $saveFolder ($name + '.rws')
$document.Save($destination)
Write-Output "Exported playable test save: $destination"
