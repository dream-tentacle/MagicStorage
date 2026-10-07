$ErrorActionPreference = 'Stop'
$xmlFiles = @(Get-ChildItem -LiteralPath 'About','Defs','Patches','Languages' -Recurse -Filter '*.xml')
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
Write-Output "PASS: $($xmlFiles.Count) XML documents valid; C# file list complete; obsolete food outlet definitions absent."
