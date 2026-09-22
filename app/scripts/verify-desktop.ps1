[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $repoRoot

function Assert-LastExitCode {
    param([Parameter(Mandatory)][string]$Step)

    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repoRoot
try {
    & dotnet restore FotoHAVN.slnx --locked-mode
    Assert-LastExitCode "dotnet restore"

    & dotnet build FotoHAVN.slnx --no-restore
    Assert-LastExitCode "dotnet build"

    & dotnet test FotoHAVN.slnx --no-build --no-restore
    Assert-LastExitCode "dotnet test"

    & "$PSScriptRoot/verify-design-contract.ps1"
    Assert-LastExitCode "repository contract verification"

    & "$workspaceRoot/scripts/verify-agent-docs.ps1"
    Assert-LastExitCode "agent documentation verification"
}
finally {
    Pop-Location
}
