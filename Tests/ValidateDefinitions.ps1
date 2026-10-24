$ErrorActionPreference = 'Stop'
$xmlFiles = @(Get-ChildItem -LiteralPath 'About','Defs','Patches','Languages','1.6','1.7' -Recurse -Filter '*.xml')
foreach ($file in $xmlFiles) {
    [xml]$doc = Get-Content -LiteralPath $file.FullName -Raw
    if ($doc.OuterXml -match 'StorageFood|TakeStorageFood|食物取货口') { throw "Obsolete food outlet definition in $file" }
}
[xml]$project = Get-Content -LiteralPath 'Source\MagicStorage\MagicStorage.csproj' -Raw
$included = @($project.Project.ItemGroup.Compile.Include)
$root = (Resolve-Path -LiteralPath 'Source\MagicStorage').Path
foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }) {
    if ($included -notcontains $file.FullName.Substring($root.Length + 1)) { throw "Missing Compile Include $file" }
}
foreach ($path in $included) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $path))) { throw "Missing source file $path" }
}

# RimWorld selects entire XML files by relative path, with version folders taking
# precedence over the shared root. Parsing only root Defs misses shadowed content.
$modRoot = (Resolve-Path -LiteralPath '.').Path
$beamNames = @('MS_Mote_ConstructionSupplyBeam', 'MS_Mote_ConstructionRepairBeam', 'MS_Mote_ConstructionRepairBeamFallback')
foreach ($gameVersion in @('1.6', '1.7')) {
    $effectiveFiles = @{}
    foreach ($folder in @((Join-Path $modRoot $gameVersion), $modRoot)) {
        $defsFolder = Join-Path $folder 'Defs'
        if (-not (Test-Path -LiteralPath $defsFolder)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $defsFolder -Recurse -Filter '*.xml') {
            $relativePath = $file.FullName.Substring($folder.Length + 1)
            if (-not $effectiveFiles.ContainsKey($relativePath)) { $effectiveFiles[$relativePath] = $file }
        }
    }
    $beams = @{}
    foreach ($file in $effectiveFiles.Values) {
        [xml]$defs = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($def in $defs.SelectNodes('/Defs/ThingDef')) {
            $defName = [string]$def.defName
            if ($beamNames -notcontains $defName) { continue }
            if ($beams.ContainsKey($defName)) { throw "Duplicate effective beam in RimWorld ${gameVersion}: $defName" }
            $beams[$defName] = $def
        }
    }
    foreach ($defName in $beamNames) {
        if (-not $beams.ContainsKey($defName)) { throw "Missing effective beam in RimWorld ${gameVersion}: $defName (check versioned XML overrides)" }
    }
    foreach ($defName in $beamNames | Where-Object { $_ -ne 'MS_Mote_ConstructionSupplyBeam' }) {
        $beam = $beams[$defName]
        $lifetime = [decimal]$beam.mote.fadeInTime + [decimal]$beam.mote.solidTime + [decimal]$beam.mote.fadeOutTime
        if ($lifetime -ne 3 -or $beam.thingClass -ne 'MoteDualAttached' -or $beam.graphicData.color -ne '(0.55, 0.85, 1)') {
            throw "Invalid repair beam lifetime/class/color in RimWorld ${gameVersion}: $defName"
        }
    }
    if ($beams['MS_Mote_ConstructionRepairBeamFallback'].HasAttribute('MayRequire')) {
        throw "Core repair fallback must not require a DLC in RimWorld $gameVersion"
    }
    if ([string]$beams['MS_Mote_ConstructionRepairBeam'].graphicData.borderMipMaps -ne
        [string]$beams['MS_Mote_ConstructionSupplyBeam'].graphicData.borderMipMaps) {
        throw "Repair beam must retain the supply beam's version-specific border mipmaps in RimWorld $gameVersion"
    }
    Write-Output "PASS: RimWorld $gameVersion effective XML retains supply/repair beams, repair timing/color and version-specific graphics."
}
Write-Output "PASS: $($xmlFiles.Count) XML documents valid; C# file list complete; obsolete food outlet definitions absent."
