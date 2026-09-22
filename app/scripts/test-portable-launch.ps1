[CmdletBinding()]
param(
    [string]$PublishPath = "artifacts/field-test/FotoHAVN-win-x64"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$resolvedPublishPath = if ([System.IO.Path]::IsPathRooted($PublishPath)) {
    [System.IO.Path]::GetFullPath($PublishPath)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $PublishPath))
}
$executable = Join-Path $resolvedPublishPath "FotoHAVN.exe"
$eventsRoot = Join-Path $resolvedPublishPath "Events"
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Portable executable not found at $executable. Run publish-field-test.ps1 first."
}
if (Test-Path -LiteralPath $eventsRoot) {
    throw "Expected a fresh portable folder without an Events root. Run publish-field-test.ps1 again before this test."
}

if (-not ("FotoHavn.FieldTest.NativeWindow" -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

namespace FotoHavn.FieldTest
{
    public static class NativeWindow
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
    }
}
"@
}

$otherInstances = Get-Process FotoHAVN -ErrorAction SilentlyContinue
if ($otherInstances) {
    throw "Close running FotoHAVN processes before the portable launch test."
}

$primary = $null
try {
    $primary = Start-Process -FilePath $executable -PassThru
    $windowDeadline = [DateTimeOffset]::UtcNow.AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 200
        $primary.Refresh()
    } while (-not $primary.HasExited -and $primary.MainWindowHandle -eq [IntPtr]::Zero -and [DateTimeOffset]::UtcNow -lt $windowDeadline)

    if ($primary.HasExited) {
        throw "Portable FotoHAVN exited before showing its primary window (exit code $($primary.ExitCode))."
    }
    if ($primary.MainWindowHandle -eq [IntPtr]::Zero) {
        throw "Portable FotoHAVN did not expose a primary window within 15 seconds."
    }
    if (-not (Test-Path -LiteralPath $eventsRoot -PathType Container)) {
        throw "Portable FotoHAVN did not create its executable-relative Events root."
    }

    $writeProbe = Join-Path $eventsRoot ".launch-write-probe"
    Set-Content -LiteralPath $writeProbe -Value "FotoHAVN launch write probe" -NoNewline
    Remove-Item -LiteralPath $writeProbe -Force

    $primaryWindow = $primary.MainWindowHandle
    $redirected = Start-Process -FilePath $executable -PassThru
    if (-not $redirected.WaitForExit(15000)) {
        throw "The redirected second launch did not exit within 15 seconds."
    }
    if ($redirected.ExitCode -ne 0) {
        throw "The redirected second launch exited with code $($redirected.ExitCode)."
    }

    $primary.Refresh()
    if ($primary.HasExited -or $primary.MainWindowHandle -ne $primaryWindow) {
        throw "The primary FotoHAVN window did not survive activation redirection."
    }

    $activationDeadline = [DateTimeOffset]::UtcNow.AddSeconds(5)
    while ([FotoHavn.FieldTest.NativeWindow]::GetForegroundWindow() -ne $primaryWindow -and [DateTimeOffset]::UtcNow -lt $activationDeadline) {
        Start-Sleep -Milliseconds 100
    }
    if ([FotoHavn.FieldTest.NativeWindow]::GetForegroundWindow() -ne $primaryWindow) {
        throw "The redirected second launch did not activate the existing FotoHAVN window."
    }

    $portableProcesses = @(Get-Process FotoHAVN -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and [System.IO.Path]::GetFullPath($_.Path) -eq [System.IO.Path]::GetFullPath($executable)
    })
    if ($portableProcesses.Count -ne 1 -or $portableProcesses[0].Id -ne $primary.Id) {
        throw "Expected one portable FotoHAVN process after the second launch; found $($portableProcesses.Count)."
    }

    Write-Host "Portable launch, executable-relative Events root, and single-instance activation passed for process $($primary.Id)."
} finally {
    if ($null -ne $primary) {
        $primary.Refresh()
        if (-not $primary.HasExited) {
            Stop-Process -Id $primary.Id
            $primary.WaitForExit(5000) | Out-Null
        }
    }
}
