$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$docsDir = Resolve-Path (Join-Path $scriptDir "..\..")
$sourceDir = Join-Path $docsDir "developer\source"
$pluginsDir = Join-Path $docsDir "modpack-root\BepInEx\plugins"

$mods = @(
    "SpacefleetCoreFixes",
    "SpacefleetModMenu",
    "SpacefleetConsole",
    "SpacefleetEconomyDebug",
    "SpacefleetBattleDebug"
)

New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null

foreach ($mod in $mods) {
    $project = Join-Path $sourceDir "$mod\$mod.csproj"
    dotnet build $project -c Release

    $outputDir = Join-Path $sourceDir "$mod\bin\Release"
    Copy-Item -LiteralPath (Join-Path $outputDir "$mod.dll") -Destination $pluginsDir -Force
    Copy-Item -LiteralPath (Join-Path $outputDir "$mod.pdb") -Destination $pluginsDir -Force
}

Write-Host "Built all Spacefleet mods and updated modpack-root."
