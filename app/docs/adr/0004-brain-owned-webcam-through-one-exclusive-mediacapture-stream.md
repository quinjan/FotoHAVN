---
status: accepted
date: 2026-10-13
---

# Brain-owned webcam through one exclusive MediaCapture stream

The brain owns the webcam through Windows `MediaCapture`. During an Active Event, one long-lived, exclusive video stream feeds both the live preview and every Capture, and each Capture is the first fresh frame after the countdown ends. Preview frames stream to React; the booth never uses `getUserMedia`.

Replaces: ADR 0001, *Capture from one Windows video stream*, which is deleted in M1 (D19). This ADR carries its still-valid decisions forward and changes who owns the stream and how the preview reaches the screen.

## Context

- Version 1 supports webcams and other Windows video-capture devices only. Canon EDSDK and any camera that isn't a Windows video-capture device are deferred (D5); see [`windows-camera-devices.md`](../research/windows-camera-devices.md) and [`canon-eos-r100-windows.md`](../research/canon-eos-r100-windows.md).
- The UI is React in WebView2 over a .NET brain ([ADR 0003](0003-react-ui-in-wpf-webview2-over-a-dotnet-brain.md)). Capture timing, freshness, failure handling, and Operator Assistance are rules, so they belong to the brain.
- The Event's mirror setting applies to both the live preview and the saved Captures, as on the website (D22).

## Decision

**Carried forward from ADR 0001.**

- Every device in `DeviceClass.VideoCapture` is treated as the same kind of camera.
- Format: prefer 1920×1080 at 30 fps, with a bounded fallback down to 1280×720 at 15 fps.
- One long-lived, exclusive video stream. It isn't reopened for each Guest Cycle.
- Each Capture is taken from the first fresh frame after the countdown.
- No Photo stream, no exhaustive mode probes, no Camera Tuning, no persisted formats, and no proprietary camera integrations. A future high-resolution or vendor integration needs its own adapter and its own ADR.

**Ownership.** The brain owns the camera through `FotoHavn.Camera.Windows`. The UI only draws frames the brain sends and never reaches the camera directly.

**Discovery and identity.**

- `DeviceInformation.FindAllAsync(DeviceClass.VideoCapture)` plus a `DeviceWatcher` subscribed to `Added`, `Updated`, and `Removed`.
- The device ID is the identity; the name is for display only.
- The Camera Binding stores the exact device ID. The app never silently substitutes another camera: if the bound camera is missing, Event setup says so and keeps the binding until the operator chooses another, and Start Event stays in Event setup with the reason.

**Opening.** `MediaCapture` with `VideoDeviceId`, `StreamingCaptureMode.Video`, `SharingMode.ExclusiveControl`, and `MemoryPreference.Cpu`. A single `MediaFrameReader` per Active Event feeds preview and Captures. Ending the Event releases the camera.

**Readiness and failure.**

- A camera becomes an Eligible Camera only after a fresh frame arrives in a usable format. Start Event requires one.
- Event setup shows the selected camera's state: Checking, Ready, Not available, In use by another app, or In use by the running Event. In the last case, the binding can be saved, but the preview and eligibility check wait until that Event ends.
- When the Windows camera privacy setting blocks access (`E_ACCESSDENIED` on open), the failure classification reports Not available with the reason "Camera access is off in Windows privacy settings." (Proposed default Q16, plan section 18; confirmed by the owner at the M0 gate.)
- During an Active Event, these are camera failures: frames older than **2 seconds**, `MediaCapture.Failed`, loss of exclusive control, and removal of the device. During a Guest Cycle, each cancels any countdown, keeps committed Captures, and puts the Active Event into Operator Assistance.
- Outside a Guest Cycle, on Start or in the guest gallery, a camera failure doesn't enter Operator Assistance. Start shows Start unavailable, with `STEP INSIDE` disabled, until a fresh frame proves the camera again, and recovers by itself. The guest gallery keeps working because it needs no camera, and returning from it to Start shows Start unavailable.
- The Operator Screen's running-Event panel shows the camera state in text either way ([ADR 0008](0008-operator-and-guest-screens-on-bound-displays.md)).
- During a Guest Cycle, `RETRY` re-proves the camera with a fresh frame, checks storage, and checks the bound display; then the Guest Cycle returns to the step it was on. When no Guest Cycle is running (Operator Assistance entered from a guest display loss on Start or in the guest gallery, or from a storage failure at `STEP INSIDE`), `RETRY` checks only the bound display and storage; then the Guest Screen returns to Start, which shows Start unavailable if the camera still isn't eligible.

**Capture.**

- The brain asks for the first frame whose timestamp is later than the countdown's end, never an earlier one.
- The frame is converted to BGRA8, mirrored per the Event setting, and encoded as JPEG at quality 92, then committed by the Event store ([ADR 0005](0005-sqlite-metadata-with-file-artifacts-for-event-storage.md)).
- The saved Capture is the full frame. Cropping to a photograph window happens only at composition ([ADR 0006](0006-brain-side-composition-with-an-app-owned-catalog.md)).

**Policies in the brain.** Format selection with fallback, frame freshness and staleness, first-frame-after-timestamp selection, failure classification, and Retry decisions are pure policies in `FotoHavn.Core` with unit tests. The adapter only performs Windows I/O and reports facts.

**Live preview.**

- Preview frames travel on a dedicated frames channel, separate from presentation messages.
- During an Active Event, the bound camera's frames go only to the guest connection. The operator connection gets frames only for the camera previewed in Event setup.
- In the WPF host, the adapter downsizes frames (target 1280 px wide) into a reused `CoreWebView2SharedBuffer` and posts it with `PostSharedBufferToScript`. The UI draws it on a canvas, mirrored by CSS when the Event says so. The headless host sends the same frames as binary WebSocket messages.
- The brain keeps at most one frame in flight and drops the rest, so the preview can never queue memory.
- The UI overlays the target window's aspect ratio using the website's camera geometry rules ([`cameraGeometry.ts`](../../../website/src/components/onlinePhotobooth/cameraGeometry.ts)), ported into the UI, not imported.

**Fake camera.** The headless host and tests use a fake camera that produces a deterministic moving test pattern and fixture photographs, and that fails, stalls, or disappears on command.

## Spike S1 result

> **S1 result: pending (M5).** Spike S1 runs at the start of M5. It measures raw BGRA and JPEG preview frames at 960, 1280, and 1920 px wide on the booth laptop, recording CPU, latency, and dropped frames. Record here: the chosen frame format and transport (shared buffer, or the JPEG-over-virtual-host fallback), the preview width, and the **frame-rate floor** that end-to-end tests assert.

## Considered options

- **`getUserMedia` in WebView2** was rejected (D4). The page would own the device, capture timing and failure rules would split between JavaScript and .NET, and the brain couldn't guarantee the first-fresh-frame rule or Operator Assistance.
- **The Photo stream, `CapturePhotoToStorageFileAsync`, or `LowLagPhotoCapture`**, suggested by the research, stay rejected as in ADR 0001. The booth deliberately trades maximum still-image resolution and vendor-specific control for fast discovery, fewer device transitions, and a smaller recovery surface.
- **Reopening the camera for each Guest Cycle** was rejected as in ADR 0001, for the same reason.
- **Shared, read-only access** was rejected: it can't configure the stream format, so the brain couldn't apply its format policy.
- **Canon EDSDK** was deferred (D5).
- **Re-binding a missing camera automatically** by instance or container ID, which the research suggests when the match is unambiguous, was rejected. The Camera Binding matches the device ID only, and the operator re-chooses.

## Consequences

- Capture resolution is limited to the video stream, at least 1280×720.
- A USB webcam moved to another port may need to be re-chosen in Event setup.
- Another app holding the webcam shows "In use by another app" in Event setup and causes Operator Assistance during a Guest Cycle, or Start unavailable outside one (risk R1).
- `FotoHavn.Camera.Windows` is excluded from the coverage gate; its policies are unit-tested in the brain, and the adapter is proven by hardware integration tests and end-to-end tests with a real camera ([ADR 0007](0007-unit-first-testing-with-real-camera-end-to-end.md)).
- End-to-end tests assert frame properties, not pixels: frame age under 500 ms, the S1 frame-rate floor, and Captures that are decodable JPEGs of at least 1280×720 and not blank.
