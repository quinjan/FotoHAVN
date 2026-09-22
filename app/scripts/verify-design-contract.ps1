[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    $testPaths = Get-ChildItem -LiteralPath tests -File -Filter "*.test.mjs" |
        Where-Object Name -ne "agent-documentation-contract.test.mjs" |
        Sort-Object Name |
        ForEach-Object FullName

    if (-not $testPaths) {
        throw "No repository contract tests were found under tests/."
    }

    & node --test @testPaths
    if ($LASTEXITCODE -ne 0) {
        throw "Design and runtime contract tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
