[CmdletBinding()]
param(
    [string]$OutputPath = "artifacts/field-test/FotoHAVN-win-x64"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$fieldTestRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts/field-test"))
$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    [System.IO.Path]::GetFullPath($OutputPath)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
}

$fieldTestPrefix = $fieldTestRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedOutput.StartsWith($fieldTestPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputPath must resolve below $fieldTestRoot."
}

$sdkSettings = Get-Content -Raw (Join-Path $repositoryRoot "global.json") | ConvertFrom-Json
$expectedSdk = [string]$sdkSettings.sdk.version
$actualSdk = (& dotnet --version).Trim()
if ($actualSdk -ne $expectedSdk) {
    throw "FotoHAVN requires .NET SDK $expectedSdk exactly; found $actualSdk."
}

Push-Location $repositoryRoot
try {
    & dotnet restore FotoHAVN.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "Locked package restore failed. Run a normal restore only when intentionally updating package locks."
    }

    if (Test-Path -LiteralPath $resolvedOutput) {
        Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
    }
    New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

    & dotnet publish src/FotoHavn.App/FotoHavn.App.csproj `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --no-restore `
        --output $resolvedOutput `
        -p:PublishProfile=FieldTest-win-x64
    if ($LASTEXITCODE -ne 0) {
        throw "Portable field-test publish failed."
    }

    $requiredFiles = @(
        "FotoHAVN.exe",
        "FotoHAVN.dll",
        "FotoHAVN.deps.json",
        "FotoHAVN.runtimeconfig.json",
        "FotoHAVN.pri",
        "App.xbf",
        "MainWindow.xbf",
        "Assets/Fonts/Inter-VariableFont_opsz,wght.ttf",
        "coreclr.dll",
        "Microsoft.WindowsAppRuntime.dll"
    )
    foreach ($requiredFile in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $resolvedOutput $requiredFile) -PathType Leaf)) {
            throw "Portable output is missing $requiredFile."
        }
    }

    $forbiddenPackages = Get-ChildItem -LiteralPath $resolvedOutput -Recurse -File |
        Where-Object { $_.Extension -in ".msix", ".msixbundle", ".appx", ".appxbundle" }
    if ($forbiddenPackages) {
        throw "Portable output unexpectedly contains an installer or app package."
    }

    $eventsRoot = Join-Path $resolvedOutput "Events"
    New-Item -ItemType Directory -Path $eventsRoot -Force | Out-Null
    $writeProbe = Join-Path $eventsRoot ".write-probe"
    Set-Content -LiteralPath $writeProbe -Value "FotoHAVN field-test write probe" -NoNewline
    Remove-Item -LiteralPath $writeProbe -Force
    Remove-Item -LiteralPath $eventsRoot -Force

    $appProject = [xml](Get-Content -Raw "src/FotoHavn.App/FotoHavn.App.csproj")
    $windowsAppSdkReference = $appProject.SelectSingleNode("//PackageReference[@Include='Microsoft.WindowsAppSDK']")
    if ($null -eq $windowsAppSdkReference) {
        throw "Microsoft.WindowsAppSDK does not have an exact PackageReference."
    }
    $windowsAppSdk = $windowsAppSdkReference.Version
    $gitCommit = (& git rev-parse HEAD).Trim()
    $manifest = [ordered]@{
        application = "FotoHAVN"
        applicationVersion = [string]$appProject.Project.PropertyGroup.Version
        runtimeIdentifier = "win-x64"
        configuration = "Release"
        selfContained = $true
        unpackaged = $true
        singleFile = $false
        dotnetSdk = $actualSdk
        windowsAppSdk = [string]$windowsAppSdk
        gitCommit = $gitCommit
        builtAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        executableSha256 = (Get-FileHash -Algorithm SHA256 (Join-Path $resolvedOutput "FotoHAVN.exe")).Hash.ToLowerInvariant()
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $resolvedOutput "field-test-build.json") -Encoding utf8

    Write-Host "Portable field-test folder: $resolvedOutput"
    Write-Host "Executable SHA-256: $($manifest.executableSha256)"
} finally {
    Pop-Location
}
