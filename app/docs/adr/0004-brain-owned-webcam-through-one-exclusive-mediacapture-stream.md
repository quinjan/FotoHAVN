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
- In the WPF host, the adapter downsizes frames (1280 px wide, as raw RGBA pixels; Spike S1) into a reused `CoreWebView2SharedBuffer` and posts it with `PostSharedBufferToScript`. The UI draws it on a canvas, mirrored by CSS when the Event says so. The headless host sends the same frames as binary WebSocket messages.
- The brain keeps at most one frame in flight and drops the rest, so the preview can never queue memory.
- The UI overlays the target window's aspect ratio using the website's camera geometry rules ([`cameraGeometry.ts`](../../../website/src/components/onlinePhotobooth/cameraGeometry.ts)), ported into the UI, not imported.

**Fake camera.** The headless host and tests use a fake camera that produces a deterministic moving test pattern and fixture photographs, and that fails, stalls, or disappears on command.

## Spike S1 result

**Decision.** Preview frames travel as **raw RGBA pixels in a reused `CoreWebView2SharedBuffer`**, posted with `PostSharedBufferToScript` (the plan's proposal, confirmed), at **1280 px wide** (aspect ratio kept). The host swizzles BGRA to RGBA during the downsize pass, so the page does `putImageData` with no per-pixel work, then calls `chrome.webview.releaseBuffer` and acks. One frame is in flight; the host doesn't write the buffer again until the ack. The JPEG shared buffer and JPEG over a virtual host both work at 960 and 1280 px, but cost more CPU and latency and can't hold 30 fps at 1920 px, so they are fallbacks only.

**Method.** Dell Pro Max 16 MC16250 (Intel Core Ultra 7 265H, 16 logical cores, Intel Arc 140T iGPU), Windows 11 Enterprise 26200, WebView2 runtime 155.0.4283.45 (`CoreWebView2Environment.BrowserVersionString`) with Microsoft.Web.WebView2 1.0.4258.31 and SkiaSharp 3.119.4. This is the developer laptop, not the booth laptop. The camera is "Integrated Webcam" (USB), opened as in this ADR (`ExclusiveControl`, `MemoryPreference.Cpu`, one `MediaFrameReader`, `Realtime`, BGRA8). It supports NV12 at 1920×1080, 1280×960, 1280×720, 640×480, and 640×360, all at 30 fps; the spike used 1920×1080 at 30 fps. The harness was a throwaway WPF app with one WebView2 control, serving its page from the virtual host. For each camera frame, it checked whether the page had acked the previous frame; if not, it dropped the frame before doing any work. Otherwise it scaled with SkiaSharp (linear filter) into the shared buffer (raw) or a scratch buffer then JPEG quality 80 (JPEG), and posted it. Latency runs from the frame's arrival in the host to the ack arriving in the host, so it includes the downsize, the dispatcher hop, the page's decode and draw, and the ack trip, but not the camera's own pipeline. CPU is `Process.TotalProcessorTime` deltas of the host plus every `msedgewebview2.exe` descendant of the browser process, as a percentage of one core; the machine-wide figure divides by 16. Each cell ran 3 s of warm-up and 20 s of measurement, and the table shows the median of 3 repetitions. The spike code wasn't committed.

**Camera limit on this run.** The camera delivered only about 20 fps at every format, with or without the Bgra8 conversion, and its frames were Windows' "camera off" placeholder (black with a crossed-out camera icon), so it was shuttered or blocked at the time. A real scene therefore couldn't be measured. The first full matrix ran on those real frames (all nine cells delivered 19.7–19.8 fps for 19.9 fps arriving, at most 1.2% dropped, which only shows the transports keep up with the camera). The table below comes from a second full matrix where the same pipeline ran against a photograph-like synthetic 1920×1080 source (smooth gradients plus per-pixel noise, 8 distinct frames) paced at 30 fps, while the real camera stayed open and converting, so JPEG size and encode time aren't those of a black frame. The raw path's cost doesn't depend on content.

| Transport | Width | Delivered fps | Dropped | Latency p50 / p95 (ms) | CPU, one core (machine-wide) | Saturation fps |
|---|---|---|---|---|---|---|
| Raw RGBA, shared buffer | 960 | 29.7 | 0.7% | 3.9 / 5.6 | 35% (2.2%) | 304 |
| **Raw RGBA, shared buffer** | **1280** | **29.7** | **1.0%** | **7.8 / 9.7** | **48% (3.0%)** | **132** |
| Raw RGBA, shared buffer | 1920 | 29.7 | 0.8% | 5.0 / 6.5 | 37% (2.3%) | 190 |
| JPEG, shared buffer | 960 | 29.8 | 0.7% | 10.4 / 12.5 | 54% (3.4%) | 97 |
| JPEG, shared buffer | 1280 | 29.7 | 1.0% | 19.3 / 21.2 | 82% (5.1%) | 50 |
| JPEG, shared buffer | 1920 | 16.1 | 46% | 34.6 / 36.9 | 82% (5.1%) | 28 |
| JPEG, virtual host | 960 | 29.8 | 1.0% | 11.7 / 14.0 | 62% (3.9%) | 85 |
| JPEG, virtual host | 1280 | 29.7 | 1.0% | 20.8 / 22.7 | 89% (5.6%) | 46 |
| JPEG, virtual host | 1920 | 14.9 | 51% | 37.2 / 39.8 | 76% (4.7%) | 27 |

"Saturation fps" is a single run where the host re-sent the cached frame as soon as each ack arrived, so it shows the transport's ceiling, not the camera's. The camera-only baseline (frames arriving and converting to BGRA8 in the platform, nothing sent) was 16% of one core at 20 fps and is included in every CPU figure above. Working set was about 320–380 MB for the host and 410–440 MB for WebView2 (raw), or 720–790 MB (JPEG).

Findings. Raw wins at every width: JPEG encode costs 5.7 ms at 960, 10 ms at 1280, and 25 ms at 1920 on a single thread, and the transport itself is cheap either way. Raw at 1920 is cheaper for the host than at 1280 because a 1080p camera needs no scaling (a 0.9 ms swizzle instead of a 5.3 ms downscale), but its 8.3 MB frames cost WebView2 more (18% against 14%), and 1280 is enough for the guest preview, so 1280 stays the default. The shared buffer's `Buffer` pointer must be read once on the UI thread and cached; reading it from the camera thread fails with a cast error. At 1280 the raw path has 4× headroom over 30 fps, and the host's cost is the downscale (5.3 ms) plus the platform's BGRA8 conversion.

**Frame-rate floor for end-to-end tests: 15 fps**, measured as preview frames drawn per second over at least 5 s of steady state with a real camera, plus at most 10% of arrived camera frames dropped when the camera delivers at least 15 fps. The transport isn't the limit: at 30 fps in, it delivers 29.7 fps with p95 under 10 ms. The camera is: this machine's camera delivered 19.9 fps in the measured condition, a floor of 20 would flake, and the ADR's own fallback format is 15 fps. A floor of 15 fps leaves margin on both, and the second assertion catches transport regressions regardless of camera speed. The "frame age under 500 ms" assertion is realistic: the host-to-draw latency is about 10 ms p95 at 1280 raw, so the remaining budget belongs to the camera's own pipeline and the 2 s staleness rule, with more than 20× margin on the part the host controls.

**Fallback rule.** Keep raw shared buffer unless `CreateSharedBuffer` or `PostSharedBufferToScript` throws or isn't supported by the runtime. Then use JPEG over the virtual host (it needs only `WebResourceRequested`) at 1280 px or below, never 1920, and log it as a degraded preview. If the delivered rate stays under the floor while the camera arrives above it for 5 s, step the width down to 960 on the same transport. Don't use the JPEG shared buffer: at the same width it costs about as much as the virtual host, and a third code path isn't worth it.

**S5 (operator and guest previews at once).** Two WebView2 controls in one environment, both receiving the raw 1280 px frame at 30 fps with their own shared buffers and their own in-flight state, with the downsize done once per frame: both delivered 29.7 fps with p95 latency about 9.1 ms, 0.8% dropped, and 55% of one core in total (3.5% of the machine), against 48% for one view. The second view cost about 8 points of one core, and the WebView2 working set barely moved (438 MB), so no frame-rate cap is needed on the operator preview on this machine.

**Verified end to end (M5).** The shipped path (the real adapter into the WPF host's shared buffer, the page's `putImageData`, the ack) was measured by `smoke/camera-live` on the same machine and placeholder frames: the booth delivered 20.0 fps at every sample after the 2 s warm-up (the camera delivers about 20), the page drew 19.8 fps, 0 frames were dropped, and the booth's frame age peaked at 95 ms over 10 s.

**Pending.** This ran on the developer machine, with the camera showing its placeholder frames and a synthetic 30 fps source. Re-measuring on the booth laptop with a real scene is a manual check, listed under the M5 manual checks (plan R6, S1); re-measure S5 there too. If the booth laptop can't hold 29 fps at 1280 raw, drop to 960 first.

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
