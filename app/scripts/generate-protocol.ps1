<#
.SYNOPSIS
Regenerates the protocol schema and the UI's protocol types from the C# records in FotoHavn.Protocol.

.DESCRIPTION
1. Exports the JSON Schema with JsonSchemaExporter into src/FotoHavn.Protocol/schema/protocol.schema.json, by running
   FotoHavn.Protocol.Tests' schema drift test in update mode (FOTOHAVN_UPDATE_PROTOCOL=1).
2. Generates ui/src/protocol/generated.ts from that schema with json-schema-to-typescript (npm run generate:protocol).

Commit both files with the record change. The .NET drift test and the UI's vitest drift test fail when either is stale.
With -UpdateSnapshots it also rewrites the serialization snapshots in tests/FotoHavn.Protocol.Tests/Snapshots; review
their diff before committing. Run from anywhere; it works relative to app/. Requires `npm ci` in ui/ once.

.EXAMPLE
./scripts/generate-protocol.ps1

.EXAMPLE
./scripts/generate-protocol.ps1 -UpdateSnapshots
#>
[CmdletBinding()]
param(
    [switch] $UpdateSnapshots
)

$ErrorActionPreference = 'Stop'
$appRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $appRoot 'tests/FotoHavn.Protocol.Tests/FotoHavn.Protocol.Tests.csproj'

$previous = $env:FOTOHAVN_UPDATE_PROTOCOL
$env:FOTOHAVN_UPDATE_PROTOCOL = '1'
try {
    $filter = if ($UpdateSnapshots) { @() } else { @('--filter-class', 'FotoHavn.Protocol.Tests.SchemaDriftTests') }
    dotnet test --project $testProject @filter
    if ($LASTEXITCODE -ne 0) { throw "Exporting the protocol schema failed (dotnet test exit code $LASTEXITCODE)." }
}
finally {
    $env:FOTOHAVN_UPDATE_PROTOCOL = $previous
}

Push-Location (Join-Path $appRoot 'ui')
try {
    npm run --silent generate:protocol
    if ($LASTEXITCODE -ne 0) { throw "Generating ui/src/protocol/generated.ts failed (npm exit code $LASTEXITCODE)." }
}
finally {
    Pop-Location
}
