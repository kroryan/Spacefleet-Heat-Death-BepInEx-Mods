$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$docsDir = Resolve-Path (Join-Path $scriptDir "..\..")
$gameRoot = Resolve-Path (Join-Path $docsDir "..")
$roots = @(
    (Join-Path $gameRoot "ModSource"),
    (Join-Path $docsDir "developer\source")
)

foreach ($rootPath in $roots) {
    if (-not (Test-Path -LiteralPath $rootPath)) {
        continue
    }

    $root = (Resolve-Path -LiteralPath $rootPath).Path
    Get-ChildItem -LiteralPath $root -Recurse -Force -Directory |
        Where-Object { $_.Name -eq "bin" -or $_.Name -eq "obj" } |
        ForEach-Object {
            $resolved = $_.FullName
            if (-not $resolved.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to delete outside source root: $resolved"
            }

            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
}

Write-Host "Removed bin/obj source artifacts."
