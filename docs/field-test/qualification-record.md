# FotoHAVN portable field-test qualification record

Use one copy of this record for each release candidate and booth rig. A release is **not qualified** until every required row has evidence and a passing result. Record observations; do not turn them into universal Camera or performance claims.

## Release candidate

| Field | Recorded value |
| --- | --- |
| Qualification date and operator | |
| Git commit | |
| Application version (from `field-test-build.json`) | |
| `field-test-build.json` attached | |
| FotoHAVN.exe SHA-256 | |
| .NET SDK (from `field-test-build.json`) | |
| Windows App SDK (from `field-test-build.json`) | |
| Published folder location | |

Build from a clean checkout:

```powershell
./scripts/publish-field-test.ps1
./scripts/test-portable-launch.ps1
```

The publish script performs a locked restore, creates a self-contained unpackaged `win-x64` folder, rejects MSIX/AppX output, verifies WinUI resources, probes the adjacent location for write access, and writes `field-test-build.json`. The launch script proves that the app creates its executable-relative `Events` root, foregrounds the existing window on a second launch, and leaves one surviving process. Preserve the complete folder and manifest as release evidence.

## Laptop and fixed display

| Field | Recorded value |
| --- | --- |
| Manufacturer and model | |
| Windows edition, version, and OS build | |
| CPU architecture | |
| Fully updated at (local time) | |
| AC power connected | |
| Lid open | |
| Primary display and scaling | |
| FotoHAVN canvas observed at exactly 1280 × 720 | |
| Sleep, hibernation, and display shutoff suppressed during Active Event | |
| Published folder is in a writable location | |
| Adjacent `Events` root created and writable | |

## Camera and driver

| Field | Recorded value |
| --- | --- |
| Camera manufacturer and model | |
| Connection and built-in USB port used | |
| Driver provider, version, and date | |
| Saved `DeviceInformation.Id` | |
| Displayed Available Camera label | |
| Selected format and frame rate | |
| Fallback tier(s) attempted and observed result | |
| Third-party Windows video Camera available? | |
| Saved Capture and Photo Strip evidence paths | |

If no third-party Camera is available, record that limitation here and make no Camera, hot-plug, DSLR, capture-card, or virtual-Camera compatibility claim:

> Limitation:

## Native end-to-end checks

Record **Pass**, **Fail**, or **Not run**, plus an artifact path, timestamp, screenshot, log, or concise observation for each row.

| Check | Result | Evidence / observation |
| --- | --- | --- |
| Published `FotoHAVN.exe` launches from the writable portable folder | | |
| A second launch activates the existing window and leaves one process | | |
| Second launch creates no second Camera or storage owner | | |
| Create Event with full name, explicit Camera selection, and `No Printer` | | |
| Edit saved Event name and Camera Binding | | |
| Discovery lists Cameras without opening or taking test Captures | | |
| Only explicit Camera selection acquires exclusive ownership | | |
| Camera dropdown shows compact status and crop-matched preview | | |
| Save and activate Event; exit between Guest Cycles | | |
| Guest Start readiness recovers only after operator Retry | | |
| Four ordered Captures complete on one continuous video stream | | |
| Each Capture is the first fresh full-frame, unmirrored post-countdown JPEG | | |
| No Photo-stream changes, hidden probe Captures, or per-Cycle reopening | | |
| Operator Assistance retains progress and Retry repeats first missing Capture | | |
| Photo Strip preview lasts ten uninterrupted visible seconds and returns to Start | | |
| Camera disconnect after Photo Strip commit does not interrupt completion and blocks the next Start | | |
| Event deletion quarantine survives restart and Retry Deletion is idempotent | | |
| Full Event name appears in Saved Events, Active Event, and Photo Strip | | |

## Disconnect and reconnect checks

Run once during guest Start and once during Capture using a third-party Windows video Camera when one is available.

| Check | Result | Evidence / observation |
| --- | --- | --- |
| Disconnect during Start blocks admission and calls for operator | | |
| Reconnect during Start does not substitute or resume automatically | | |
| Operator Retry reopens the exact saved Camera Binding | | |
| Disconnect during Capture retains completed Captures | | |
| Reconnect during Capture does not substitute or resume automatically | | |
| Retry uses a new countdown for only the first missing Capture | | |

## Display, accessibility, and motion pass

| Check | Result | Evidence / observation |
| --- | --- | --- |
| Approved hierarchy and copy match the 1280 × 720 design QA references | | |
| Guest Start and operator Retry targets are at least 64 px | | |
| Keyboard navigation and visible focus cover every interactive control | | |
| Controls expose meaningful accessible names | | |
| Capture progress is textual as well as visual | | |
| Operator Assistance is exposed as an assertive alert | | |
| Windows animation-disabled setting removes nonessential motion | | |

## Release decision

| Field | Recorded value |
| --- | --- |
| Decision (`Qualified` / `Not qualified`) | |
| Open failures and limitations | |
| Evidence archive location | |
| Approver and date | |

Any relevant Windows update or device-driver update invalidates the Camera portion of this record. Revalidate the Camera on the booth rig before returning it to service.
