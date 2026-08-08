# UI verification state injection

FotoHAVN's deterministic state-injection seam is compiled only when
`UiVerificationBuild=true`. Normal builds and the field-test publish profile do
not compile the seam or package its approved-injection catalog.

Build and launch one approved canonical state:

```powershell
dotnet build src/FotoHavn.App/FotoHavn.App.csproj -p:UiVerificationBuild=true
src/FotoHavn.App/bin/Debug/net10.0-windows10.0.26100.0/win-x64/FotoHAVN.exe `
  --ui-verification injection.guest-start.ready
```

For deterministic data and transitions, pass a request file:

```powershell
FotoHAVN.exe --ui-verification-request C:\verification\guest-start.json
```

```json
{
  "identity": "injection.guest-start.ready",
  "presentation": {
    "eventId": "0198f5d1-72aa-7000-8000-fotohavn0001",
    "eventName": "Community Night",
    "captureNumber": 1,
    "completedCaptures": 0,
    "countdownSeconds": 3
  },
  "clockUtc": "2026-01-15T10:30:00Z",
  "cameraOutcome": "ready",
  "storageOutcome": "ready",
  "mediaPath": "C:\\verification\\photo-strip.png",
  "script": [
    {
      "onCommand": "StartGuestCycle",
      "injectionIdentity": "injection.capture.countdown-3",
      "focusAutomationId": "FotoHavn.Action.Capture.Primary",
      "announcement": "Capture countdown started."
    }
  ]
}
```

The controller never constructs `CameraBoundary` or
`ExecutableRelativeEventFileSystem`; it supplies deterministic
`ApplicationPresentation` values directly to the production presentation
adapter. Script steps advance only when the named application command is
observed, so transitions do not depend on arbitrary delays.

Every production surface root exposes its contract Automation ID. In a
verification build, `FotoHavn.Verification.RenderSettled` changes from
`rendering` to `settled` after the selected composition completes layout.
