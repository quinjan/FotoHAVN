[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    & node --test scripts/agent-documentation-contract.test.mjs
    if ($LASTEXITCODE -ne 0) {
        throw "Agent documentation contract failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
