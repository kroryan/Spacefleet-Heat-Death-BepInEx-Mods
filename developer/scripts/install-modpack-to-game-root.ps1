$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$docsDir = Resolve-Path (Join-Path $scriptDir "..\..")
$gameRoot = Resolve-Path (Join-Path $docsDir "..")
$modpackRoot = Resolve-Path (Join-Path $docsDir "modpack-root")

Copy-Item -LiteralPath (Join-Path $modpackRoot ".doorstop_version") -Destination $gameRoot -Force
Copy-Item -LiteralPath (Join-Path $modpackRoot "doorstop_config.ini") -Destination $gameRoot -Force
Copy-Item -LiteralPath (Join-Path $modpackRoot "winhttp.dll") -Destination $gameRoot -Force
Copy-Item -LiteralPath (Join-Path $modpackRoot "BepInEx") -Destination $gameRoot -Recurse -Force

Write-Host "Installed modpack-root into:"
Write-Host $gameRoot
