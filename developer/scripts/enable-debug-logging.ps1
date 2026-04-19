$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$docsDir = Resolve-Path (Join-Path $scriptDir "..\..")
$gameRoot = Resolve-Path (Join-Path $docsDir "..")
$cfg = Join-Path $gameRoot "BepInEx\config\BepInEx.cfg"

if (-not (Test-Path -LiteralPath $cfg)) {
    Write-Host "BepInEx.cfg not found. Start the game once with BepInEx installed first."
    exit 1
}

$text = Get-Content -LiteralPath $cfg -Raw
$text = $text -replace "(?m)^Enabled = false$", "Enabled = true"
$text = $text -replace "(?m)^LogLevels = Fatal, Error, Warning, Message, Info$", "LogLevels = All"
$text = $text -replace "(?m)^LogChannels = Warn, Error$", "LogChannels = Warn, Error, Debug"
Set-Content -LiteralPath $cfg -Value $text -Encoding UTF8

Write-Host "Enabled BepInEx console/debug logging in:"
Write-Host $cfg
