param([Parameter(Mandatory=$true)][string]$SourceSave, [Parameter(Mandatory=$true)][string]$DestinationSave)
$ErrorActionPreference = 'Stop'
$doc = New-Object System.Xml.XmlDocument
$doc.PreserveWhitespace = $true
$doc.Load((Resolve-Path -LiteralPath $SourceSave).Path)
foreach ($node in @($doc.SelectNodes("//*[@Class and starts-with(@Class, 'AutonomousRim.RuntimeChecks.')]"))) { $node.ParentNode.RemoveChild($node) | Out-Null }
$ids = @($doc.SelectNodes('/savegame/meta/modIds/li'))
for ($i = $ids.Count-1; $i -ge 0; $i--) {
    if ($ids[$i].InnerText -ne 'leonardoh21.autonomousrim.runtimechecks') { continue }
    foreach ($name in @('modIds','modNames','modSteamIds')) {
        $nodes = @($doc.SelectNodes("/savegame/meta/$name/li"))
        if ($nodes.Count -ne $ids.Count) { throw "Unexpected metadata alignment: $name" }
        $nodes[$i].ParentNode.RemoveChild($nodes[$i]) | Out-Null
    }
}
if (([IO.Path]::GetFullPath($SourceSave)) -eq ([IO.Path]::GetFullPath($DestinationSave))) { throw 'Export must preserve the source save.' }
if (Test-Path -LiteralPath $DestinationSave) { throw 'Export destination already exists; select a new name.' }
$doc.Save($DestinationSave)
Write-Output "Playable save exported: $DestinationSave"
