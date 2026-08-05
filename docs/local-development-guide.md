# Local development guide

FotoHAVN is a Windows 11 x64 desktop application built with .NET 10 and WinUI 3. It cannot be built or run natively on macOS or Linux.

## Prerequisites

- Windows 11 x64, version 21H2 (build 22000) or later.
- The .NET SDK selected by [`global.json`](../global.json): .NET 10 SDK `10.0.302`, or a compatible later patch in the same feature band.
- The Windows 11 SDK `10.0.26100` when the required targeting pack is not already installed. It can be added through the Visual Studio Installer.
- Internet access to restore packages from NuGet.org.
- A Windows video-capture device for exercising Camera selection and the live preview. Automated tests do not require Camera hardware.

For local Camera access, open **Settings > Privacy & security > Camera** and enable Camera access and desktop-app Camera access.

## Restore and build

Run commands from the repository root:

```powershell
dotnet --version
dotnet restore FotoHAVN.slnx
dotnet build FotoHAVN.slnx
```

The SDK version reported by `dotnet --version` must satisfy `global.json`.

If a build reports that `FotoHAVN.exe` is locked, close the running FotoHAVN application and build again. FotoHAVN permits only one running application instance.

## Run the application

```powershell
dotnet run --project src/FotoHavn.App/FotoHavn.App.csproj
```

From **Saved Events**, select **New Event**, enter an Event name, explicitly select an Available Camera, and select **No Printer**. A Camera is ready only after FotoHAVN opens its exact Windows device ID and receives a suitable live frame.

The application stores Event manifests in an `Events` directory beside the running executable. During a normal Debug run, that directory is under:

```text
src/FotoHavn.App/bin/Debug/net10.0-windows10.0.26100.0/win-x64/Events
```

Removing or editing that directory changes local development data, so preserve it when testing saved Event behavior.

## Run tests

Run the complete test suite:

```powershell
dotnet test FotoHAVN.slnx
```

Run one test project while iterating:

```powershell
dotnet test tests/FotoHavn.AcceptanceTests/FotoHavn.AcceptanceTests.csproj
dotnet test tests/FotoHavn.WindowsIntegrationTests/FotoHavn.WindowsIntegrationTests.csproj
```

The acceptance tests exercise operator-visible behavior through the application orchestrator. The Windows integration tests exercise Camera identity, ownership, format fallback, frame readiness, rendering policy, release, removal, and reconstruction seams without requiring physical hardware.

## Create a local Release build

```powershell
dotnet publish src/FotoHavn.App/FotoHavn.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true
```

The portable output is written to:

```text
src/FotoHavn.App/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish
```

Launch `FotoHAVN.exe` from that directory. Its Event data will be stored in the adjacent `Events` directory.

## Common Camera problems

- **Access denied**: enable Windows Camera privacy access for desktop applications.
- **In use by another app**: close applications that own the Camera, including conferencing tools and Camera utilities, then select it again.
- **Unavailable**: the Camera did not expose one of FotoHAVN's bounded field-test video formats.
- **Disconnected**: reconnect the Camera and explicitly select it again. FotoHAVN never substitutes a Camera with a different Windows device ID.
- **No live preview**: confirm the Camera reports 1920 × 1080 at 30 or 15 fps, or 1280 × 720 at 30 or 15 fps.
