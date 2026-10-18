param(
    [ValidateSet("1.6", "1.7")]
    [string]$GameVersion = "1.7",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$GameAssemblyDir = "",
    [string]$HarmonyAssemblyPath = ""
)

$ErrorActionPreference = "Stop"
$msbuildPath = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
$projectPath = Join-Path $PSScriptRoot "Source\MagicStorage\MagicStorage.csproj"
$buildArguments = @(
    $projectPath,
    "/restore",
    "/p:Configuration=$Configuration",
    "/p:GameVersion=$GameVersion",
    "/nologo",
    "/verbosity:minimal"
)
if ($GameAssemblyDir) {
    $buildArguments += "/p:GameAssemblyDir=$([IO.Path]::GetFullPath($GameAssemblyDir))"
}
if ($HarmonyAssemblyPath) {
    $buildArguments += "/p:HarmonyAssemblyPath=$([IO.Path]::GetFullPath($HarmonyAssemblyPath))"
}

Write-Output "Building RimWorld $GameVersion ($Configuration) -> $GameVersion\Assemblies"
& $msbuildPath @buildArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
