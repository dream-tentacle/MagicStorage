$ErrorActionPreference = 'Stop'
# Fixtures are read through RimSage and saved in obj; never read installed game XML.
[xml]$combined = '<Defs/>'
foreach ($fixture in Get-ChildItem -LiteralPath 'Tests\obj\FoodVanillaRefs' -Filter '*.xml') {
    [xml]$doc = Get-Content -LiteralPath $fixture.FullName -Raw
    foreach ($node in $doc.DocumentElement.ChildNodes) {
        if ($node.NodeType -eq [System.Xml.XmlNodeType]::Element) {
            [void]$combined.DocumentElement.AppendChild($combined.ImportNode($node, $true))
        }
    }
}
$caravanBefore = @($combined.SelectNodes('Defs/DutyDef[contains(defName, "Caravan")]') | ForEach-Object { $_.OuterXml }) -join ''
$nodeCount = 0
$workerCount = 0
foreach ($path in @('Patches\StorageFoodThinkTree.xml', 'Patches\StorageFoodWorkGivers.xml')) {
    [xml]$patchDoc = Get-Content -LiteralPath $path -Raw
    foreach ($operation in $patchDoc.Patch.Operation) {
        $targets = @($combined.SelectNodes($operation.xpath))
        if ($targets.Count -eq 0) { throw "No vanilla target for $($operation.xpath)" }
        foreach ($target in $targets) {
            if ($operation.match.Class -eq 'PatchOperationAttributeSet') {
                $beforeChildren = $target.InnerXml
                $target.SetAttribute($operation.match.attribute, $operation.match.value)
                if ($target.InnerXml -ne $beforeChildren) { throw 'Node settings changed' }
                $nodeCount++
            } else {
                $replacement = $combined.ImportNode($operation.SelectSingleNode('match/value/*'), $true)
                [void]$target.ParentNode.ReplaceChild($replacement, $target)
                $workerCount++
            }
        }
    }
}
$caravanAfter = @($combined.SelectNodes('Defs/DutyDef[contains(defName, "Caravan")]') | ForEach-Object { $_.OuterXml }) -join ''
if ($caravanBefore -ne $caravanAfter) { throw 'Caravan duties changed' }
$xmlFiles = @(Get-ChildItem -LiteralPath 'About','Defs','Patches','Languages' -Recurse -Filter '*.xml')
foreach ($file in $xmlFiles) { [xml]$doc = Get-Content -LiteralPath $file.FullName -Raw }
[xml]$jobs = Get-Content -LiteralPath 'Defs\JobDefs\StorageFoodJobs.xml' -Raw
$jobNames = @($jobs.Defs.JobDef | Where-Object { $_.defName } | ForEach-Object { $_.defName })
[xml]$en = Get-Content -LiteralPath 'Languages\English\DefInjected\JobDef\MagicStorage.xml' -Raw
foreach ($name in $jobNames) {
    if (-not $en.DocumentElement.SelectSingleNode("$name.reportString")) { throw "Missing English job report $name" }
}
[xml]$project = Get-Content -LiteralPath 'Source\MagicStorage\MagicStorage.csproj' -Raw
$included = @($project.Project.ItemGroup.Compile.Include)
$root = (Resolve-Path -LiteralPath 'Source\MagicStorage').Path
foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }) {
    if ($included -notcontains $file.FullName.Substring($root.Length + 1)) { throw "Missing Compile Include $file" }
}
Write-Output "PASS: $nodeCount vanilla think nodes and $workerCount work/mental-state entries adapted; node settings and caravan duties preserved; $($jobNames.Count) purposes translated; $($xmlFiles.Count) XML documents valid."
