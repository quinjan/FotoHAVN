#Requires -Version 7.0
<#
.SYNOPSIS
    Publishes the FOTOHAVN field-test build: a self-contained win-x64 app that carries a pinned WebView2 Fixed Version
    runtime, as a versioned zip.

.DESCRIPTION
    Refuses any request to include the E2E test hooks: -FotoHavnE2E, -p:FotoHavnE2E=true, -p:DefineConstants with
    E2E_HOOKS, or the FotoHavnE2E environment variable. A field build never contains them.

    Steps, cheapest failure first:
      1. Read scripts/webview2-fixed-version.json (version, architecture, url, sha256, sizeBytes, expandedSizeBytes,
         runtimeFolderName). A missing or malformed field fails.
      2. Obtain the pinned runtime .cab: -WebView2Cab, else the cache folder, else a download from the manifest URL.
         Its SHA-256 and size must match the manifest before anything is expanded; a mismatch fails, and a downloaded
         file that does not match is deleted, never cached.
      3. Build the field UI (`npm ci` when ui/node_modules is missing, then `npm run build`) and refuse an E2E UI build.
      4. `dotnet publish` (self-contained, win-x64, no E2E hooks) and check the output: no FOTOHAVN or FotoHavn.*
         assembly may carry the FotoHavnE2E assembly metadata or a type in an .E2E namespace, and wwwroot/ may not hold
         the E2E UI marker (e2e-build.txt).
      5. Expand the runtime (`expand.exe`) to runtime/webview2 beside FOTOHAVN.exe (where HostPaths looks for it) and
         check msedgewebview2.exe, the expanded size, and the file count against the manifest.
      6. Write build-info.json and the zip FOTOHAVN-field-<version>-<shortSha>[-dirty].zip, which holds one top-level
         FOTOHAVN folder. The version is "version" in ui/package.json (the one product version in the repository), or
         -AppVersion.

    A field build always carries the runtime: there is no switch to skip it. Paths resolve relative to app/.

.PARAMETER Output
    The publish folder (default output/field/FOTOHAVN). Only runtime/webview2 inside it is cleaned before a build.

.PARAMETER ZipDirectory
    Where the zip goes (default output/field).

.PARAMETER WebView2Cab
    A local Fixed Version .cab to use instead of the cache or a download. It is verified like any other.

.PARAMETER ManifestPath
    The runtime manifest (default scripts/webview2-fixed-version.json).

.PARAMETER CacheDirectory
    Where downloaded runtimes are kept, outside the repository (default %LOCALAPPDATA%\FOTOHAVN\webview2-cache).

.EXAMPLE
    ./scripts/publish-field-test.ps1
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $Output,
    [string] $ZipDirectory,
    [string] $WebView2Cab,
    [string] $ManifestPath,
    [string] $CacheDirectory,
    [string] $AppVersion,
    [switch] $FotoHavnE2E,
    # MSBuild properties as Name=Value, also accepted in the dotnet form -p:Name=Value.
    [Alias('p')]
    [string[]] $Property = @()
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$guardText = 'FOTOHAVN field builds must not include E2E hooks.'
$guardMessage = "$guardText Remove -p:FotoHavnE2E=true before publishing; E2E builds are for local end-to-end runs only."
$e2eUiMarker = 'e2e-build.txt'
$runtimeRelativeFolder = 'runtime/webview2'
$runtimeExecutable = 'msedgewebview2.exe'

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

function Fail([string] $message) {
    throw $message
}

function Get-FileSha256([string] $path) {
    (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# The manifest is the only place the pin lives: every field is required, and the ones that must agree with each other
# (version, architecture, file name, folder name) are checked so a half-edited bump fails here.
function Read-RuntimeManifest([string] $path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Fail "The WebView2 runtime manifest is missing: $path"
    }
    try {
        $manifest = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    }
    catch {
        Fail "The WebView2 runtime manifest $path is not valid JSON: $($_.Exception.Message)"
    }

    $problems = [System.Collections.Generic.List[string]]::new()
    foreach ($name in 'version', 'architecture', 'url', 'sha256', 'sizeBytes', 'expandedSizeBytes', 'expandedFileCount', 'runtimeFolderName') {
        $value = $manifest.PSObject.Properties[$name]
        if ($null -eq $value -or $null -eq $value.Value -or ($value.Value -is [string] -and [string]::IsNullOrWhiteSpace($value.Value))) {
            $problems.Add("'$name' is missing or empty.")
        }
    }
    if ($problems.Count -eq 0) {
        if ($manifest.version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
            $problems.Add("'version' must look like 155.0.4283.60, not '$($manifest.version)'.")
        }
        if ($manifest.architecture -cne 'x64') {
            $problems.Add("'architecture' must be x64 (the field build is win-x64), not '$($manifest.architecture)'.")
        }
        if ($manifest.sha256 -notmatch '^[0-9a-fA-F]{64}$') {
            $problems.Add("'sha256' must be 64 hex digits.")
        }
        foreach ($name in 'sizeBytes', 'expandedSizeBytes', 'expandedFileCount') {
            $number = 0L
            if (-not [long]::TryParse([string] $manifest.$name, [ref] $number) -or $number -le 0) {
                $problems.Add("'$name' must be a positive whole number.")
            }
        }
        $uri = $null
        $isUrl = [uri]::TryCreate([string] $manifest.url, [System.UriKind]::Absolute, [ref] $uri)
        if (-not $isUrl -or -not ($uri.Scheme -ceq 'https' -or ($uri.Scheme -ceq 'http' -and $uri.IsLoopback))) {
            $problems.Add("'url' must be an absolute https URL.")
        }
        else {
            $expectedName = "Microsoft.WebView2.FixedVersionRuntime.$($manifest.version).$($manifest.architecture)"
            if ((Split-Path -Leaf $uri.AbsolutePath) -cne "$expectedName.cab") {
                $problems.Add("'url' must end with $expectedName.cab.")
            }
            if ($manifest.runtimeFolderName -cne $expectedName) {
                $problems.Add("'runtimeFolderName' must be $expectedName.")
            }
        }
    }
    if ($problems.Count -gt 0) {
        Fail "The WebView2 runtime manifest $path is not usable:`n$($problems -join "`n")"
    }
    $manifest
}

# Returns what is wrong with a runtime .cab, or nothing when it is the pinned one.
function Get-CabProblem([string] $path, $manifest) {
    $actualSize = (Get-Item -LiteralPath $path).Length
    if ($actualSize -ne [long] $manifest.sizeBytes) {
        return "is $actualSize bytes; the manifest pins $($manifest.sizeBytes)."
    }
    $actualHash = Get-FileSha256 $path
    if ($actualHash -cne $manifest.sha256.ToLowerInvariant()) {
        return "has SHA-256 $actualHash; the manifest pins $($manifest.sha256.ToLowerInvariant())."
    }
}

function Get-VerifiedRuntimeCab([object] $Manifest, [string] $LocalCab, [string] $CacheDirectory) {
    $fileName = Split-Path -Leaf ([uri] $Manifest.url).AbsolutePath

    if ($LocalCab) {
        if (-not (Test-Path -LiteralPath $LocalCab -PathType Leaf)) {
            Fail "-WebView2Cab $LocalCab does not exist."
        }
        $problem = Get-CabProblem $LocalCab $Manifest
        if ($problem) {
            Fail "-WebView2Cab $LocalCab $problem Refusing to use an unverified WebView2 runtime."
        }
        return (Resolve-Path -LiteralPath $LocalCab).Path
    }

    $cached = Join-Path $CacheDirectory $fileName
    if (Test-Path -LiteralPath $cached -PathType Leaf) {
        $problem = Get-CabProblem $cached $Manifest
        if (-not $problem) {
            return $cached
        }
        Write-Warning "The cached runtime $cached $problem Downloading it again."
        Remove-Item -LiteralPath $cached -Force
    }

    New-Item -ItemType Directory -Force -Path $CacheDirectory | Out-Null
    $partial = "$cached.download"
    Write-Host "Downloading $($Manifest.url)"
    try {
        Invoke-WebRequest -Uri $Manifest.url -OutFile $partial -UseBasicParsing
    }
    catch {
        Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue
        Fail "Downloading the WebView2 runtime failed: $($_.Exception.Message) Pass -WebView2Cab <path> to use a file you already have."
    }
    $problem = Get-CabProblem $partial $Manifest
    if ($problem) {
        Remove-Item -LiteralPath $partial -Force
        Fail "The downloaded WebView2 runtime from $($Manifest.url) $problem It was deleted; nothing was built."
    }
    Move-Item -LiteralPath $partial -Destination $cached -Force
    $cached
}

# Expands the verified cab to <Output>/runtime/webview2 and checks it against the manifest. A failure removes the
# runtime folder, so a half-expanded runtime never ends up in a zip.
function Install-Runtime([object] $Manifest, [string] $Cab, [string] $Output) {
    $runtimeParent = Join-Path $Output 'runtime'
    $target = Join-Path $Output $runtimeRelativeFolder
    if (Test-Path -LiteralPath $runtimeParent) {
        Remove-Item -LiteralPath $runtimeParent -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $runtimeParent | Out-Null
    try {
        & expand.exe '-F:*' $Cab $runtimeParent | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Fail "expand.exe failed with exit code $LASTEXITCODE for $Cab."
        }
        $expanded = Join-Path $runtimeParent $Manifest.runtimeFolderName
        if (-not (Test-Path -LiteralPath $expanded -PathType Container)) {
            Fail "The runtime cab did not expand to $($Manifest.runtimeFolderName)."
        }
        Move-Item -LiteralPath $expanded -Destination $target
        if (-not (Test-Path -LiteralPath (Join-Path $target $runtimeExecutable) -PathType Leaf)) {
            Fail "$runtimeExecutable is missing from the expanded runtime in $target."
        }
        $files = @(Get-ChildItem -LiteralPath $target -Recurse -File)
        $expandedSize = ($files | Measure-Object -Property Length -Sum).Sum
        if ($files.Count -ne [long] $Manifest.expandedFileCount -or $expandedSize -ne [long] $Manifest.expandedSizeBytes) {
            Fail "The expanded runtime has $($files.Count) files and $expandedSize bytes; the manifest pins $($Manifest.expandedFileCount) files and $($Manifest.expandedSizeBytes) bytes."
        }
    }
    catch {
        Remove-Item -LiteralPath $runtimeParent -Recurse -Force -ErrorAction SilentlyContinue
        throw
    }
    $target
}

function Get-ProductVersion([string] $Override, [string] $PackageJson) {
    $version = $Override
    if (-not $version) {
        if (-not (Test-Path -LiteralPath $PackageJson -PathType Leaf)) {
            Fail "Cannot read the product version: $PackageJson is missing. Pass -AppVersion."
        }
        $version = (Get-Content -LiteralPath $PackageJson -Raw | ConvertFrom-Json).version
    }
    if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$') {
        Fail "The product version '$version' is not a version like 1.2.3 or 1.2.3-rc.1."
    }
    $version
}

function Get-ShortSha([string] $AppRoot) {
    try {
        $sha = (& git -C $AppRoot rev-parse --short=8 HEAD 2>$null)
        if ($LASTEXITCODE -eq 0 -and $sha -match '^[0-9a-f]{8}$') {
            return $sha
        }
    }
    catch {
    }
    'nogit'
}

function Get-ZipName([string] $Version, [string] $AppRoot) {
    $name = "FOTOHAVN-field-$Version-$(Get-ShortSha $AppRoot)"
    try {
        $status = (& git -C $AppRoot status --porcelain -- . 2>$null)
        if ($LASTEXITCODE -eq 0 -and $status) {
            $name += '-dirty'
        }
    }
    catch {
    }
    "$name.zip"
}

function New-FieldZip([string] $SourceDirectory, [string] $ZipDirectory, [string] $ZipName) {
    New-Item -ItemType Directory -Force -Path $ZipDirectory | Out-Null
    $zipPath = Join-Path (Resolve-Path -LiteralPath $ZipDirectory).Path $ZipName
    $partial = "$zipPath.partial"
    Remove-Item -LiteralPath $partial, $zipPath -Force -ErrorAction SilentlyContinue
    try {
        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            (Resolve-Path -LiteralPath $SourceDirectory).Path,
            $partial,
            [System.IO.Compression.CompressionLevel]::Optimal,
            $true)
        Move-Item -LiteralPath $partial -Destination $zipPath
    }
    finally {
        Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue
    }
    $zipPath
}

$e2eRequested = $FotoHavnE2E.IsPresent -or
    ($env:FotoHavnE2E -eq 'true') -or
    [bool]($Property | Where-Object { $_ -match '^\s*FotoHavnE2E\s*=\s*true\s*$' -or $_ -match '^\s*DefineConstants\s*=.*\bE2E_HOOKS\b' })
if ($e2eRequested) {
    Stop-WithGuard $guardMessage
}

$appRoot = Split-Path -Parent $PSScriptRoot
if (-not $Output) {
    $Output = Join-Path $appRoot 'output/field/FOTOHAVN'
}
if (-not $ZipDirectory) {
    $ZipDirectory = Join-Path $appRoot 'output/field'
}
if (-not $ManifestPath) {
    $ManifestPath = Join-Path $PSScriptRoot 'webview2-fixed-version.json'
}
if (-not $CacheDirectory) {
    $CacheDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FOTOHAVN/webview2-cache'
}
$ProgressPreference = 'SilentlyContinue'

# The pinned runtime comes first: it is the cheapest thing to get wrong, and a bad hash must stop the build before
# anything is published.
$manifest = Read-RuntimeManifest $ManifestPath
$cab = Get-VerifiedRuntimeCab -Manifest $manifest -LocalCab $WebView2Cab -CacheDirectory $CacheDirectory
Write-Host "WebView2 Fixed Version $($manifest.version) ($($manifest.architecture)): $cab (SHA-256 verified)."

$productVersion = Get-ProductVersion -Override $AppVersion -PackageJson (Join-Path $appRoot 'ui/package.json')
$zipName = Get-ZipName -Version $productVersion -AppRoot $appRoot

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

$runtimeFolder = Install-Runtime -Manifest $manifest -Cab $cab -Output $Output
$sizeBytes = (Get-ChildItem -Path $Output -Recurse -File | Measure-Object -Property Length -Sum).Sum

$buildInfo = [ordered]@{
    product = 'FOTOHAVN'
    version = $productVersion
    commit = Get-ShortSha $appRoot
    webView2 = [ordered]@{
        version = $manifest.version
        architecture = $manifest.architecture
        sha256 = $manifest.sha256
        folder = $runtimeRelativeFolder
    }
}
$buildInfo | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $Output 'build-info.json') -Encoding utf8

$zipPath = New-FieldZip -SourceDirectory $Output -ZipDirectory $ZipDirectory -ZipName $zipName
$zipSize = (Get-Item -LiteralPath $zipPath).Length
Write-Host ("Runtime: {0}" -f $runtimeFolder)
Write-Host ("Unpacked size: {0:N1} MB" -f ($sizeBytes / 1MB))
Write-Host ("Field-test zip: {0} ({1:N1} MB)" -f $zipPath, ($zipSize / 1MB))
exit 0
