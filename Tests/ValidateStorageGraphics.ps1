$ErrorActionPreference = 'Stop'
[xml]$definitions = Get-Content -LiteralPath 'Defs\ThingDefs\StorageBuildings.xml' -Raw
$conduit = $definitions.SelectSingleNode('/Defs/ThingDef[defName="MS_StorageConduit"]')
if ($conduit.drawStyleCategory -ne 'Conduits') { throw 'Storage conduit must use the native conduit drag category.' }
if ($conduit.graphicData.graphicClass -ne 'MagicStorage.Graphic_StorageConduit') { throw 'Missing custom conduit graphic.' }
if ($conduit.building.blueprintGraphicData.graphicClass -ne 'MagicStorage.Graphic_StorageConduit') { throw 'Blueprint must use storage connectivity.' }
if ($conduit.graphicData.linkType -or $conduit.building.blueprintGraphicData.linkType) { throw 'linkType would double-wrap the custom linked graphic.' }
if ($conduit.graphicData.texPath -ne 'Things/Building/Linked/PowerConduit_Atlas' -or
    $conduit.building.blueprintGraphicData.texPath -ne 'Things/Building/Linked/PowerConduit_Blueprint_Atlas') { throw 'Built and blueprint atlases must remain distinct.' }
if ($conduit.drawerType -ne 'MapMeshOnly' -or $conduit.altitudeLayer -ne 'Conduits') { throw 'Ground cable must use the conduit mesh layer.' }
if ($conduit.uiIconPath -ne 'Things/Building/Linked/PowerConduit_MenuIcon') { throw 'Mouse ghost must have a single-image conduit icon.' }
foreach ($def in $definitions.SelectNodes('/Defs/ThingDef[comps/li[@Class="MagicStorage.CompProperties_StorageNode"]]')) {
    if ($def.SelectSingleNode('comps/li[contains(@Class,"CompProperties_Power")]')) { throw 'Storage nodes must not join the electric network.' }
}
Write-Output 'PASS: native conduit dragging, separate blueprint atlas, icon ghost and independent storage connectivity configured.'
