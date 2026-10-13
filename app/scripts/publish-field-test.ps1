#Requires -Version 7.0
<#
.SYNOPSIS
    Publishes the FOTOHAVN field-test build (minimal in M1; M10 completes it).

.DESCRIPTION
    Refuses any request to include the E2E test hooks: -FotoHavnE2E, -p:FotoHavnE2E=true, -p:DefineConstants with
    E2E_HOOKS, or the FotoHavnE2E environment variable. A field build never contains them.

    Builds the field UI (`npm ci` when ui/node_modules is missing, then `npm run build`), publishes, and then checks the
    publish output: no FOTOHAVN or FotoHavn.* assembly may carry the FotoHavnE2E assembly metadata or a type in an .E2E
    namespace, and wwwroot/ may not hold the E2E UI marker (e2e-build.txt). Paths resolve relative to app/.

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
$guardText = 'FOTOHAVN field builds must not include E2E hooks.'
$guardMessage = "$guardText Remove -p:FotoHavnE2E=true before publishing; E2E builds are for local end-to-end runs only."
$e2eUiMarker = 'e2e-build.txt'

function Stop-WithGuard([string] $message) {
    Write-Error $message -ErrorAction Continue
    exit 1
}

# Returns why an assembly counts as an E2E build, or nothing when it doesn't.
function Get-E2EHookEvidence([string] $path) {
    $stream = [System.IO.File]::OpenRead($path)
    try {
        $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            if (-not $pe.HasMetadata) {
                return
            }
            $metadata = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            if ($metadata.IsAssembly) {
                foreach ($handle in $metadata.GetAssemblyDefinition().GetCustomAttributes()) {
                    $attribute = $metadata.GetCustomAttribute($handle)
                    if ($attribute.Constructor.Kind -ne [System.Reflection.Metadata.HandleKind]::MemberReference) {
                        continue
                    }
                    $constructor = $metadata.GetMemberReference([System.Reflection.Metadata.MemberReferenceHandle] $attribute.Constructor)
                    if ($constructor.Parent.Kind -ne [System.Reflection.Metadata.HandleKind]::TypeReference) {
                        continue
                    }
                    $type = $metadata.GetTypeReference([System.Reflection.Metadata.TypeReferenceHandle] $constructor.Parent)
                    if ($metadata.GetString($type.Name) -ne 'AssemblyMetadataAttribute') {
                        continue
                    }
                    $value = $metadata.GetBlobReader($attribute.Value)
                    if ($value.ReadUInt16() -eq 1 -and $value.ReadSerializedString() -eq 'FotoHavnE2E') {
                        'carries [assembly: AssemblyMetadata("FotoHavnE2E", ...)]'
                    }
                }
            }
            foreach ($handle in $metadata.TypeDefinitions) {
                $namespace = $metadata.GetString($metadata.GetTypeDefinition($handle).Namespace)
                if ($namespace.Contains('.E2E', [System.StringComparison]::Ordinal)) {
                    "contains namespace $namespace"
                    break
                }
            }
        }
        finally {
            $pe.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

$e2eRequested = $FotoHavnE2E.IsPresent -or
    ($env:FotoHavnE2E -eq 'true') -or
    [bool]($Property | Where-Object { $_ -match '^\s*FotoHavnE2E\s*=\s*true\s*$' -or $_ -match '^\s*DefineConstants\s*=.*\bE2E_HOOKS\b' })
if ($e2eRequested) {
    Stop-WithGuard $guardMessage
}

$appRoot = Split-Path -Parent $PSScriptRoot
if (-not $Output) {
    $Output = Join-Path $appRoot 'output/field-test/FOTOHAVN'
}

# The field UI: always rebuilt, so wwwroot/ never ships a stale or E2E bundle.
$uiDir = Join-Path $appRoot 'ui'
Push-Location $uiDir
try {
    if (-not (Test-Path (Join-Path $uiDir 'node_modules'))) {
        & npm ci
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    & npm run build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
if (Test-Path (Join-Path $uiDir "dist/$e2eUiMarker")) {
    Stop-WithGuard "$guardText ui/dist holds an E2E UI build ($e2eUiMarker)."
}

$project = Join-Path $appRoot 'src/FotoHavn.Host.Windows/FotoHavn.Host.Windows.csproj'
$msbuildProperties = @($Property | ForEach-Object { "-p:$_" })
& dotnet publish $project -c $Configuration -r win-x64 --self-contained true -p:FotoHavnE2E=false -o $Output @msbuildProperties
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

# Check what was actually published, whatever the build properties said.
$problems = [System.Collections.Generic.List[string]]::new()
$assemblies = Get-ChildItem -Path $Output -Filter *.dll -File -Recurse |
    Where-Object { $_.BaseName -ceq 'FOTOHAVN' -or $_.BaseName.StartsWith('FotoHavn.', [System.StringComparison]::Ordinal) }
if (-not ($assemblies | Where-Object { $_.Name -ceq 'FOTOHAVN.dll' })) {
    $problems.Add("FOTOHAVN.dll is missing from $Output, so the hook check proves nothing.")
}
foreach ($assembly in $assemblies) {
    foreach ($evidence in Get-E2EHookEvidence $assembly.FullName) {
        $problems.Add("$($assembly.FullName) $evidence.")
    }
}
foreach ($marker in Get-ChildItem -Path $Output -Filter $e2eUiMarker -File -Recurse) {
    $problems.Add("$($marker.FullName) marks an E2E UI build.")
}
if ($problems.Count -gt 0) {
    Stop-WithGuard "$guardText The publish output in $Output is not a field build:`n$($problems -join "`n")"
}

Write-Host "Field-test build published to $Output (no E2E hooks, field UI)."
exit 0
