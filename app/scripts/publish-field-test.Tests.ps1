#Requires -Version 7.0
# Tests for publish-field-test.ps1. They run the real script in a child pwsh against a throw-away copy of the app
# folder, with fake `dotnet` and `npm` first on PATH, a tiny real .cab (makecab/expand.exe) standing in for the
# WebView2 runtime, and a loopback HTTP listener standing in for Microsoft's download server. No network, no
# publish, and each run takes a second or two.
#
# Run: pwsh -NoProfile -Command "Invoke-Pester ./scripts/publish-field-test.Tests.ps1"   (from app/)
# The file uses only syntax shared by Pester 3.4 (the version Windows ships) and Pester 5.

Describe 'publish-field-test.ps1' {
    BeforeAll {
        $script:Root = Join-Path ([System.IO.Path]::GetTempPath()) "fotohavn pester $([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $script:RealScript = Join-Path $PSScriptRoot 'publish-field-test.ps1'
        $script:Pwsh = (Get-Command pwsh).Source
        $script:RuntimeName = 'Microsoft.WebView2.FixedVersionRuntime.155.0.4283.60.x64'
        New-Item -ItemType Directory -Force -Path $script:Root | Out-Null

        # A tiny runtime tree and its .cab. expand.exe -F:* restores the top-level folder like the real cab does.
        $payload = Join-Path $script:Root "payload\$($script:RuntimeName)"
        New-Item -ItemType Directory -Force -Path (Join-Path $payload 'Locales') | Out-Null
        Set-Content -Path (Join-Path $payload 'msedgewebview2.exe') -Value 'fake runtime executable'
        Set-Content -Path (Join-Path $payload 'Locales\en-US.pak') -Value 'fake locale'
        $script:CabDirectory = Join-Path $script:Root 'cab'
        New-Item -ItemType Directory -Force -Path $script:CabDirectory | Out-Null
        $script:Cab = Join-Path $script:CabDirectory "$($script:RuntimeName).cab"
        $ddf = @(
            '.OPTION EXPLICIT'
            '.Set Cabinet=on'
            '.Set RptFileName=nul'
            '.Set InfFileName=nul'
            '.Set Compress=on'
            '.Set CabinetFileCountThreshold=0'
            '.Set FolderFileCountThreshold=0'
            '.Set FolderSizeThreshold=0'
            '.Set MaxCabinetSize=0'
            '.Set MaxDiskFileCount=0'
            '.Set MaxDiskSize=0'
            ".Set CabinetNameTemplate=$($script:RuntimeName).cab"
            ".Set DiskDirectory1=$($script:CabDirectory)"
            ".Set DestinationDir=$($script:RuntimeName)"
            "`"$payload\msedgewebview2.exe`" msedgewebview2.exe"
            ".Set DestinationDir=$($script:RuntimeName)\Locales"
            "`"$payload\Locales\en-US.pak`" en-US.pak"
        )
        $ddfPath = Join-Path $script:Root 'runtime.ddf'
        Set-Content -Path $ddfPath -Value $ddf
        & makecab.exe /F $ddfPath | Out-Null
        if (-not (Test-Path $script:Cab)) { throw 'makecab did not produce the test cab.' }
        $script:CabHash = (Get-FileHash $script:Cab -Algorithm SHA256).Hash.ToLowerInvariant()
        $script:CabSize = (Get-Item $script:Cab).Length
        $files = @(Get-ChildItem $payload -Recurse -File)
        $script:ExpandedSize = ($files | Measure-Object Length -Sum).Sum
        $script:ExpandedCount = $files.Count

        $script:Manifest = [ordered]@{
            version = '155.0.4283.60'
            architecture = 'x64'
            url = "https://example.invalid/files/abc/$($script:RuntimeName).cab"
            sha256 = $script:CabHash
            sizeBytes = $script:CabSize
            expandedSizeBytes = $script:ExpandedSize
            expandedFileCount = $script:ExpandedCount
            runtimeFolderName = $script:RuntimeName
        }

        # Fake dotnet and npm. FAKE_LOG records every call; FAKE_DOTNET_MODE picks what `dotnet publish` produces.
        $bin = Join-Path $script:Root 'bin'
        New-Item -ItemType Directory -Force -Path $bin | Out-Null
        $script:Bin = $bin
        Set-Content -Path (Join-Path $bin 'dotnet.cmd') -Value "@echo off`r`n`"$($script:Pwsh)`" -NoProfile -File `"%~dp0fake-dotnet.ps1`" %*`r`nexit /b %ERRORLEVEL%"
        Set-Content -Path (Join-Path $bin 'npm.cmd') -Value "@echo off`r`necho npm %* >> `"%FAKE_LOG%`"`r`nexit /b 0"
        $managedDll = Join-Path ([System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'System.Collections.dll'
        Set-Content -Path (Join-Path $bin 'fake-dotnet.ps1') -Value @"
`$arguments = @(`$args)
Add-Content -Path `$env:FAKE_LOG -Value (('dotnet ' + (`$arguments -join ' ')) -replace '-p (?=\w+=)', '-p:')
if (`$env:FAKE_DOTNET_MODE -eq 'fail') { exit 7 }
`$out = `$arguments[[array]::IndexOf(`$arguments, '-o') + 1]
New-Item -ItemType Directory -Force -Path (Join-Path `$out 'wwwroot') | Out-Null
Set-Content -Path (Join-Path `$out 'FOTOHAVN.exe') -Value 'fake exe'
Set-Content -Path (Join-Path `$out 'wwwroot\index.html') -Value 'fake ui'
if (`$env:FAKE_DOTNET_MODE -ne 'no-dll') { Copy-Item -LiteralPath '$managedDll' -Destination (Join-Path `$out 'FOTOHAVN.dll') }
if (`$env:FAKE_DOTNET_MODE -eq 'e2e-marker') { Set-Content -Path (Join-Path `$out 'wwwroot\e2e-build.txt') -Value 'e2e' }
exit 0
"@

        # A throw-away app folder, in a git repository so the short SHA and the dirty check are real.
        $template = Join-Path $script:Root 'template'
        $script:Template = $template
        New-Item -ItemType Directory -Force -Path (Join-Path $template 'app\scripts'), (Join-Path $template 'app\ui'), (Join-Path $template 'app\src\FotoHavn.Host.Windows') | Out-Null
        Set-Content -Path (Join-Path $template 'app\ui\package.json') -Value '{ "name": "fotohavn-ui", "version": "1.2.3" }'
        Set-Content -Path (Join-Path $template 'app\src\FotoHavn.Host.Windows\FotoHavn.Host.Windows.csproj') -Value '<Project />'
        Set-Content -Path (Join-Path $template '.gitignore') -Value 'app/output/'
        Copy-Item $script:RealScript (Join-Path $template 'app\scripts\publish-field-test.ps1')
        & git -C $template init --quiet
        & git -C $template add --all 2>$null
        & git -C $template -c user.name=test -c user.email=test@example.invalid commit --quiet -m test

        function Write-Manifest($sandbox, $manifest) {
            $manifest | ConvertTo-Json | Set-Content -Path (Join-Path $sandbox 'app\scripts\webview2-fixed-version.json')
        }

        function New-Sandbox($manifest = $script:Manifest) {
            $sandbox = Join-Path $script:Root "sb $([guid]::NewGuid().ToString('N').Substring(0, 8))"
            Copy-Item $script:Template $sandbox -Recurse
            if ($null -ne $manifest) {
                Write-Manifest $sandbox $manifest
                & git -C $sandbox add --all 2>$null
                & git -C $sandbox -c user.name=test -c user.email=test@example.invalid commit --quiet -m manifest
            }
            $sandbox
        }

        function Invoke-Publish($sandbox, [string[]] $Arguments = @(), [hashtable] $Environment = @{}) {
            $log = Join-Path $sandbox 'fake.log'
            Set-Content -Path $log -Value ''
            $saved = @{}
            $variables = @{ FAKE_LOG = $log; PATH = "$($script:Bin);$env:PATH"; FotoHavnE2E = $null }
            foreach ($name in $Environment.Keys) { $variables[$name] = $Environment[$name] }
            foreach ($name in $variables.Keys) {
                $saved[$name] = [Environment]::GetEnvironmentVariable($name)
                [Environment]::SetEnvironmentVariable($name, $variables[$name])
            }
            try {
                $script = Join-Path $sandbox 'app\scripts\publish-field-test.ps1'
                $text = & $script:Pwsh -NoProfile -File $script @Arguments 2>&1 | Out-String
                $exit = $LASTEXITCODE
            }
            finally {
                foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
            }
            [pscustomobject]@{
                Sha = (& git -C $sandbox rev-parse --short=8 HEAD 2>$null)
                ExitCode = $exit
                Text = ($text -replace '\s*\r?\n[\s|]*', ' ')
                Calls = @(Get-Content $log | ForEach-Object { $_.Trim() } | Where-Object { $_ })
                Output = Join-Path $sandbox 'app\output\field\FOTOHAVN'
                ZipDirectory = Join-Path $sandbox 'app\output\field'
                Zips = @(Get-ChildItem (Join-Path $sandbox 'app\output\field') -Filter *.zip -ErrorAction SilentlyContinue)
            }
        }

        function Get-ZipEntries($zip) {
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
            try { @($archive.Entries | ForEach-Object { $_.FullName }) } finally { $archive.Dispose() }
        }

        function Get-Local($sandbox) { @('-WebView2Cab', $script:Cab, '-CacheDirectory', (Join-Path $sandbox 'cache')) }

        function Assert-GuardRefuses($result) {
            $result.ExitCode | Should Be 1
            ($result.Text -match 'FOTOHAVN field builds must not include E2E hooks') | Should Be $true
            ($result.Calls -join "`n") | Should Be ''
            $result.Zips.Count | Should Be 0
        }

        function Assert-NothingBuilt($result) {
            ($result.Calls -match '^dotnet publish').Count | Should Be 0
            $result.Zips.Count | Should Be 0
        }
    }

    AfterAll {
        Remove-Item -LiteralPath $script:Root -Recurse -Force -ErrorAction SilentlyContinue
    }

    Context 'E2E guards' {
        It 'refuses -FotoHavnE2E' {
            $sandbox = New-Sandbox
            Assert-GuardRefuses (Invoke-Publish $sandbox (@('-FotoHavnE2E') + (Get-Local $sandbox)))
        }

        It 'refuses -p:FotoHavnE2E=true' {
            $sandbox = New-Sandbox
            Assert-GuardRefuses (Invoke-Publish $sandbox (@('-p:FotoHavnE2E=true') + (Get-Local $sandbox)))
        }

        It 'refuses DefineConstants with E2E_HOOKS' {
            $sandbox = New-Sandbox
            Assert-GuardRefuses (Invoke-Publish $sandbox (@('-p:DefineConstants=DEBUG;E2E_HOOKS') + (Get-Local $sandbox)))
        }

        It 'refuses the FotoHavnE2E environment variable' {
            $sandbox = New-Sandbox
            Assert-GuardRefuses (Invoke-Publish $sandbox (Get-Local $sandbox) @{ FotoHavnE2E = 'true' })
        }

        It 'refuses an E2E UI build in ui/dist' {
            $sandbox = New-Sandbox
            New-Item -ItemType Directory -Force -Path (Join-Path $sandbox 'app\ui\dist') | Out-Null
            Set-Content -Path (Join-Path $sandbox 'app\ui\dist\e2e-build.txt') -Value 'e2e'
            $result = Invoke-Publish $sandbox (Get-Local $sandbox)
            $result.ExitCode | Should Be 1
            ($result.Text -match 'holds an E2E UI build') | Should Be $true
            Assert-NothingBuilt $result
        }

        It 'refuses a published wwwroot that holds the E2E UI marker' {
            $sandbox = New-Sandbox
            $result = Invoke-Publish $sandbox (Get-Local $sandbox) @{ FAKE_DOTNET_MODE = 'e2e-marker' }
            $result.ExitCode | Should Be 1
            ($result.Text -match 'marks an E2E UI build') | Should Be $true
            $result.Zips.Count | Should Be 0
        }

        It 'refuses a publish output without FOTOHAVN.dll' {
            $sandbox = New-Sandbox
            $result = Invoke-Publish $sandbox (Get-Local $sandbox) @{ FAKE_DOTNET_MODE = 'no-dll' }
            $result.ExitCode | Should Be 1
            ($result.Text -match 'FOTOHAVN\.dll is missing') | Should Be $true
            $result.Zips.Count | Should Be 0
        }

        It 'has no switch that skips the runtime' {
            $sandbox = New-Sandbox
            $command = Get-Command (Join-Path $sandbox 'app\scripts\publish-field-test.ps1')
            ($command.Parameters.Keys -match 'Skip') | Should Be $null
            $result = Invoke-Publish $sandbox (@('-SkipRuntime') + (Get-Local $sandbox))
            ($result.ExitCode -ne 0) | Should Be $true
            Assert-NothingBuilt $result
        }
    }

    Context 'a good build' {
        BeforeAll {
            $script:GoodSandbox = New-Sandbox
            $script:Good = Invoke-Publish $script:GoodSandbox (@('-p:SomeFlag=1') + (Get-Local $script:GoodSandbox))
        }

        It 'succeeds and names the zip' {
            $script:Good.ExitCode | Should Be 0
            $script:Good.Zips.Count | Should Be 1
            $script:Good.Zips[0].Name | Should Be "FOTOHAVN-field-1.2.3-$($script:Good.Sha).zip"
            ($script:Good.Text -match 'Field-test zip: .*\.zip \(') | Should Be $true
        }

        It 'places the runtime in runtime/webview2 beside the exe' {
            Join-Path $script:Good.Output 'FOTOHAVN.exe' | Should Exist
            Join-Path $script:Good.Output 'runtime\webview2\msedgewebview2.exe' | Should Exist
            Join-Path $script:Good.Output 'runtime\webview2\Locales\en-US.pak' | Should Exist
            Join-Path $script:Good.Output "runtime\$($script:RuntimeName)" | Should Not Exist
        }

        It 'zips one FOTOHAVN folder with the app, wwwroot, the runtime, and the build info' {
            $entries = Get-ZipEntries $script:Good.Zips[0].FullName
            ($entries -contains 'FOTOHAVN/FOTOHAVN.exe') | Should Be $true
            ($entries -contains 'FOTOHAVN/wwwroot/index.html') | Should Be $true
            ($entries -contains 'FOTOHAVN/runtime/webview2/msedgewebview2.exe') | Should Be $true
            ($entries -contains 'FOTOHAVN/build-info.json') | Should Be $true
            @($entries | Where-Object { -not $_.StartsWith('FOTOHAVN/') }).Count | Should Be 0
        }

        It 'records the runtime pin in build-info.json' {
            $info = Get-Content (Join-Path $script:Good.Output 'build-info.json') -Raw | ConvertFrom-Json
            $info.version | Should Be '1.2.3'
            $info.commit | Should Be $script:Good.Sha
            $info.webView2.version | Should Be '155.0.4283.60'
            $info.webView2.sha256 | Should Be $script:CabHash
        }

        It 'builds the UI, then publishes self-contained win-x64 without E2E hooks' {
            $calls = $script:Good.Calls
            ($calls -contains 'npm run build') | Should Be $true
            $publish = @($calls | Where-Object { $_ -match '^dotnet publish' })
            $publish.Count | Should Be 1
            ($publish[0] -match ' -r win-x64 ') | Should Be $true
            ($publish[0] -match '--self-contained true') | Should Be $true
            ($publish[0] -match '-p:FotoHavnE2E=false') | Should Be $true
            ($publish[0] -match '-p:SomeFlag=1') | Should Be $true
            ($publish[0] -match '-c Release') | Should Be $true
            ($calls.IndexOf('npm run build') -lt $calls.IndexOf($publish[0])) | Should Be $true
        }

        It 'runs npm ci only when ui/node_modules is missing' {
            ($script:Good.Calls -contains 'npm ci') | Should Be $true
            $sandbox = New-Sandbox
            New-Item -ItemType Directory -Force -Path (Join-Path $sandbox 'app\ui\node_modules') | Out-Null
            $result = Invoke-Publish $sandbox (Get-Local $sandbox)
            $result.ExitCode | Should Be 0
            ($result.Calls -contains 'npm ci') | Should Be $false
        }
    }

    Context 'zip naming' {
        It 'uses -AppVersion over ui/package.json' {
            $sandbox = New-Sandbox
            $result = Invoke-Publish $sandbox (@('-AppVersion', '2.0.0-rc.1') + (Get-Local $sandbox))
            $result.ExitCode | Should Be 0
            $result.Zips[0].Name | Should Be "FOTOHAVN-field-2.0.0-rc.1-$($result.Sha).zip"
        }

        It 'marks a build from a dirty working tree' {
            $sandbox = New-Sandbox
            Set-Content -Path (Join-Path $sandbox 'app\ui\notes.txt') -Value 'uncommitted'
            $result = Invoke-Publish $sandbox (Get-Local $sandbox)
            $result.ExitCode | Should Be 0
            $result.Zips[0].Name | Should Be "FOTOHAVN-field-1.2.3-$($result.Sha)-dirty.zip"
        }

        It 'uses nogit outside a repository' {
            $sandbox = New-Sandbox
            Remove-Item (Join-Path $sandbox '.git') -Recurse -Force
            $result = Invoke-Publish $sandbox (Get-Local $sandbox)
            $result.ExitCode | Should Be 0
            $result.Zips[0].Name | Should Be 'FOTOHAVN-field-1.2.3-nogit.zip'
        }

        It 'rejects a version that is not a version' {
            $sandbox = New-Sandbox
            $result = Invoke-Publish $sandbox (@('-AppVersion', '1.0 beta') + (Get-Local $sandbox))
            $result.ExitCode | Should Be 1
            ($result.Text -match 'is not a version') | Should Be $true
            Assert-NothingBuilt $result
        }
    }

    Context 'the pinned runtime' {
        It 'fails closed on a SHA-256 mismatch before building anything' {
            $manifest = [ordered]@{} + $script:Manifest
            $manifest.sha256 = ('0' * 64)
            $sandbox = New-Sandbox $manifest
            $result = Invoke-Publish $sandbox (Get-Local $sandbox)
            $result.ExitCode | Should Be 1
            ($result.Text -match 'SHA-256') | Should Be $true
            ($result.Text -match 'unverified') | Should Be $true
            ($result.Calls -join "`n") | Should Be ''
            $result.Output | Should Not Exist
            Assert-NothingBuilt $result
        }

        It 'fails closed on a size mismatch' {
            $manifest = [ordered]@{} + $script:Manifest
            $manifest.sizeBytes = $script:CabSize + 1
            $sandbox = New-Sandbox $manifest
            $result = Invoke-Publish $sandbox (Get-Local $sandbox)
            $result.ExitCode | Should Be 1
            ($result.Text -match 'bytes') | Should Be $true
            $result.Output | Should Not Exist
        }

        It 'fails when -WebView2Cab does not exist' {
            $sandbox = New-Sandbox
            $result = Invoke-Publish $sandbox @('-WebView2Cab', (Join-Path $sandbox 'missing.cab'))
            $result.ExitCode | Should Be 1
            ($result.Text -match 'does not exist') | Should Be $true
            $result.Output | Should Not Exist
        }

        It 'fails and leaves no runtime or zip when the cab does not expand' {
            $garbage = Join-Path $script:Root 'garbage.cab'
            Set-Content -Path $garbage -Value 'this is not a cabinet'
            $manifest = [ordered]@{} + $script:Manifest
            $manifest.sha256 = (Get-FileHash $garbage -Algorithm SHA256).Hash.ToLowerInvariant()
            $manifest.sizeBytes = (Get-Item $garbage).Length
            $sandbox = New-Sandbox $manifest
            $result = Invoke-Publish $sandbox @('-WebView2Cab', $garbage)
            ($result.ExitCode -ne 0) | Should Be $true
            Join-Path $result.Output 'runtime' | Should Not Exist
            $result.Zips.Count | Should Be 0
        }

        It 'fails and leaves no runtime or zip when the expanded size differs from the manifest' {
            $manifest = [ordered]@{} + $script:Manifest
            $manifest.expandedSizeBytes = $script:ExpandedSize + 1
            $sandbox = New-Sandbox $manifest
            $result = Invoke-Publish $sandbox (Get-Local $sandbox)
            $result.ExitCode | Should Be 1
            ($result.Text -match 'expanded runtime has') | Should Be $true
            Join-Path $result.Output 'runtime' | Should Not Exist
            $result.Zips.Count | Should Be 0
        }

        It 'passes the dotnet exit code through' {
            $sandbox = New-Sandbox
            $result = Invoke-Publish $sandbox (Get-Local $sandbox) @{ FAKE_DOTNET_MODE = 'fail' }
            $result.ExitCode | Should Be 7
            $result.Zips.Count | Should Be 0
        }
    }

    Context 'the manifest' {
        It 'fails when the manifest file is missing' {
            $sandbox = New-Sandbox $null
            $result = Invoke-Publish $sandbox (Get-Local $sandbox)
            $result.ExitCode | Should Be 1
            ($result.Text -match 'manifest is missing') | Should Be $true
            Assert-NothingBuilt $result
        }

        It 'fails when the manifest is not JSON' {
            $sandbox = New-Sandbox $null
            Set-Content -Path (Join-Path $sandbox 'app\scripts\webview2-fixed-version.json') -Value '{ not json'
            $result = Invoke-Publish $sandbox (Get-Local $sandbox)
            $result.ExitCode | Should Be 1
            ($result.Text -match 'not valid JSON') | Should Be $true
        }

        It 'names each missing field' {
            foreach ($field in 'version', 'architecture', 'url', 'sha256', 'sizeBytes', 'expandedSizeBytes', 'expandedFileCount', 'runtimeFolderName') {
                $manifest = [ordered]@{}
                foreach ($key in $script:Manifest.Keys) { if ($key -ne $field) { $manifest[$key] = $script:Manifest[$key] } }
                $sandbox = New-Sandbox $manifest
                $result = Invoke-Publish $sandbox (Get-Local $sandbox)
                $result.ExitCode | Should Be 1
                ($result.Text -match "'$field' is missing or empty") | Should Be $true
                Assert-NothingBuilt $result
            }
        }

        It 'rejects an empty field' {
            $manifest = [ordered]@{} + $script:Manifest
            $manifest.sha256 = ' '
            $sandbox = New-Sandbox $manifest
            $result = Invoke-Publish $sandbox (Get-Local $sandbox)
            $result.ExitCode | Should Be 1
            ($result.Text -match "'sha256' is missing or empty") | Should Be $true
        }

        It 'rejects an architecture other than x64' {
            $manifest = [ordered]@{} + $script:Manifest
            $manifest.architecture = 'arm64'
            $sandbox = New-Sandbox $manifest
            $result = Invoke-Publish $sandbox (Get-Local $sandbox)
            $result.ExitCode | Should Be 1
            ($result.Text -match "'architecture' must be x64") | Should Be $true
        }

        It 'rejects a bump that left the url or folder name on the old version' {
            $manifest = [ordered]@{} + $script:Manifest
            $manifest.version = '155.0.4283.61'
            $sandbox = New-Sandbox $manifest
            $result = Invoke-Publish $sandbox (Get-Local $sandbox)
            $result.ExitCode | Should Be 1
            ($result.Text -match "'url' must end with") | Should Be $true
            ($result.Text -match "'runtimeFolderName' must be") | Should Be $true
        }

        It 'rejects a plain http url and a malformed hash' {
            $manifest = [ordered]@{} + $script:Manifest
            $manifest.url = $manifest.url.Replace('https://', 'http://')
            $manifest.sha256 = 'abc'
            $sandbox = New-Sandbox $manifest
            $result = Invoke-Publish $sandbox (Get-Local $sandbox)
            $result.ExitCode | Should Be 1
            ($result.Text -match "'url' must be an absolute https URL") | Should Be $true
            ($result.Text -match "'sha256' must be 64 hex digits") | Should Be $true
        }
    }

    Context 'the download' {
        BeforeAll {
            function Start-Server($payloadPath) {
                $listener = [System.Net.HttpListener]::new()
                $port = Get-Random -Minimum 20000 -Maximum 40000
                $listener.Prefixes.Add("http://127.0.0.1:$port/")
                $listener.Start()
                $state = [hashtable]::Synchronized(@{ Hits = 0 })
                $shell = [powershell]::Create()
                [void] $shell.AddScript({
                    param($listener, $payloadPath, $state)
                    while ($listener.IsListening) {
                        try { $context = $listener.GetContext() } catch { break }
                        $state.Hits++
                        $bytes = [System.IO.File]::ReadAllBytes($payloadPath)
                        $context.Response.ContentLength64 = $bytes.Length
                        $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                        $context.Response.Close()
                    }
                }).AddArgument($listener).AddArgument($payloadPath).AddArgument($state)
                [void] $shell.BeginInvoke()
                [pscustomobject]@{ Listener = $listener; Shell = $shell; Port = $port; State = $state }
            }

            function Stop-Server($server) {
                $server.Listener.Stop()
                $server.Shell.Dispose()
            }

            function Get-ServedManifest($port) {
                $manifest = [ordered]@{} + $script:Manifest
                $manifest.url = "http://127.0.0.1:$port/files/abc/$($script:RuntimeName).cab"
                $manifest
            }
        }

        It 'downloads the cab, verifies it, caches it, and reuses the cache' {
            $server = Start-Server $script:Cab
            try {
                $sandbox = New-Sandbox (Get-ServedManifest $server.Port)
                $cache = Join-Path $sandbox 'cache dir'
                $first = Invoke-Publish $sandbox @('-CacheDirectory', $cache)
                $first.ExitCode | Should Be 0
                $server.State.Hits | Should Be 1
                Join-Path $cache "$($script:RuntimeName).cab" | Should Exist
                Join-Path $cache "$($script:RuntimeName).cab.download" | Should Not Exist

                $second = Invoke-Publish $sandbox @('-CacheDirectory', $cache)
                $second.ExitCode | Should Be 0
                $server.State.Hits | Should Be 1
            }
            finally {
                Stop-Server $server
            }
        }

        It 'deletes a download that does not match and builds nothing' {
            $wrong = Join-Path $script:Root 'wrong.cab'
            Set-Content -Path $wrong -Value 'not the pinned runtime'
            $server = Start-Server $wrong
            try {
                $sandbox = New-Sandbox (Get-ServedManifest $server.Port)
                $cache = Join-Path $sandbox 'cache'
                $result = Invoke-Publish $sandbox @('-CacheDirectory', $cache)
                $result.ExitCode | Should Be 1
                ($result.Text -match 'downloaded WebView2 runtime') | Should Be $true
                @(Get-ChildItem $cache -ErrorAction SilentlyContinue).Count | Should Be 0
                Assert-NothingBuilt $result
            }
            finally {
                Stop-Server $server
            }
        }

        It 'replaces a corrupt cached cab with a fresh download' {
            $server = Start-Server $script:Cab
            try {
                $sandbox = New-Sandbox (Get-ServedManifest $server.Port)
                $cache = Join-Path $sandbox 'cache'
                New-Item -ItemType Directory -Force -Path $cache | Out-Null
                Set-Content -Path (Join-Path $cache "$($script:RuntimeName).cab") -Value 'corrupt'
                $result = Invoke-Publish $sandbox @('-CacheDirectory', $cache)
                $result.ExitCode | Should Be 0
                $server.State.Hits | Should Be 1
            }
            finally {
                Stop-Server $server
            }
        }

        It 'explains a failed download' {
            $sandbox = New-Sandbox
            $manifest = [ordered]@{} + $script:Manifest
            $manifest.url = "http://127.0.0.1:1/files/abc/$($script:RuntimeName).cab"
            Write-Manifest $sandbox $manifest
            $result = Invoke-Publish $sandbox @('-CacheDirectory', (Join-Path $sandbox 'cache'))
            $result.ExitCode | Should Be 1
            ($result.Text -match 'Downloading the WebView2 runtime failed') | Should Be $true
            ($result.Text -match '-WebView2Cab') | Should Be $true
        }
    }
}
