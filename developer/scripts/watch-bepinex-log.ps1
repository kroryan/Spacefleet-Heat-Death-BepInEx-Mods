$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$docsDir = Resolve-Path (Join-Path $scriptDir "..\..")
$gameRoot = Resolve-Path (Join-Path $docsDir "..")
$log = Join-Path $gameRoot "BepInEx\LogOutput.log"

if (-not (Test-Path -LiteralPath $log)) {
    Write-Host "No BepInEx log found yet. Start the game once, close it, then run this again."
    exit 1
}

Get-Content -LiteralPath $log -Wait -Tail 80
