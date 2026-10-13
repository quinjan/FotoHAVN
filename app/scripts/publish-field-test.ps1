<#
.SYNOPSIS
    Publishes the FOTOHAVN field-test build (minimal in M1; M10 completes it).

.DESCRIPTION
    Refuses any request to include the E2E test hooks: -FotoHavnE2E, -p:FotoHavnE2E=true, or the FotoHavnE2E
    environment variable. A field build never contains them. Paths resolve relative to app/.

.EXAMPLE
    ./scripts/publish-field-test.ps1
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $Output,
    [switch] $FotoHavnE2E,
    # MSBuild properties as Name=Value, also accepted in the dotnet form -p:Name=Value.
    [Alias('p')]
    [string[]] $Property = @()
)

$ErrorActionPreference = 'Stop'
$guardMessage = 'FOTOHAVN field builds must not include E2E hooks. Remove -p:FotoHavnE2E=true before publishing; E2E builds are for local end-to-end runs only.'

$e2eRequested = $FotoHavnE2E.IsPresent -or
    ($env:FotoHavnE2E -eq 'true') -or
    [bool]($Property | Where-Object { $_ -match '^\s*FotoHavnE2E\s*=\s*true\s*$' })
if ($e2eRequested) {
    Write-Error $guardMessage -ErrorAction Continue
    exit 1
}

$appRoot = Split-Path -Parent $PSScriptRoot
if (-not $Output) {
    $Output = Join-Path $appRoot 'output/field-test/FOTOHAVN'
}

$project = Join-Path $appRoot 'src/FotoHavn.Host.Windows/FotoHavn.Host.Windows.csproj'
$msbuildProperties = @($Property | ForEach-Object { "-p:$_" })
& dotnet publish $project -c $Configuration -r win-x64 --self-contained true -p:FotoHavnE2E=false -o $Output @msbuildProperties
exit $LASTEXITCODE
