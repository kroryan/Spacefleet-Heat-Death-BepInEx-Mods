$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$docsDir = Resolve-Path (Join-Path $scriptDir "..\..")
$gameRoot = Resolve-Path (Join-Path $docsDir "..")
$project = Join-Path $docsDir "developer\source\SpacefleetCoreFixes\SpacefleetCoreFixes.csproj"
$modpackPlugins = Join-Path $docsDir "modpack-root\BepInEx\plugins"

dotnet build $project -c Release

New-Item -ItemType Directory -Force -Path $modpackPlugins | Out-Null
Copy-Item -LiteralPath (Join-Path $docsDir "developer\source\SpacefleetCoreFixes\bin\Release\SpacefleetCoreFixes.dll") -Destination $modpackPlugins -Force
Copy-Item -LiteralPath (Join-Path $docsDir "developer\source\SpacefleetCoreFixes\bin\Release\SpacefleetCoreFixes.pdb") -Destination $modpackPlugins -Force

Write-Host "Built SpacefleetCoreFixes and updated modpack-root."
Write-Host "Game root: $gameRoot"
