# Integrated-camera field-test result

Decision date: 2026-08-04

## Verdict

Accept the capability-based Windows camera boundary for the first field-test build. The field-test PC's **Integrated Webcam** proved the complete automated path through `Windows.Media.Capture.MediaCapture`, and the operator accepted that result as sufficient for this ticket.

This verdict does **not** claim that a third-party camera was tested. None was available. It also does not promise compatibility by camera brand or device type: every selectable Camera must pass the same runtime capability and readiness gates below.

## Minimum eligible-camera contract

A Camera is selectable only when all of these pass on the current Windows machine:

1. exact `DeviceInformation.Id` resolution with no silent device substitution;
2. exclusive `MediaCapture` initialization in video-only mode;
3. a decoded color preview stream of at least 640×480 at 15 fps;
4. a photo stream of at least 1280×720 that can be encoded to JPEG;
5. receipt of a fresh preview frame;
6. a writable Guest Cycle output location.

The guest preview is mirrored only by a UI transform. Captures are saved from the untransformed camera stream.

## Evidence from the Integrated Webcam

- Windows build: `10.0.26200.0`
- exact saved interface binding resolved after closing and reopening the prototype;
- exclusive initialization passed;
- first fresh preview frame arrived 491 ms after initialization completed;
- observed NV12 preview and photo modes: 640×360, 640×480, 1280×720, 1280×960, and 1920×1080, all reported at 30 fps;
- selected preview: 640×480 at 30 fps;
- selected photo: 1920×1080;
- four ordered JPEG Captures saved successfully in 94, 73, 69, and 55 ms;
- all four Captures had distinct SHA-256 digests, confirming four distinct file writes.

The private JPEGs and full machine-readable report remain in the ignored local `evidence/` directory and are not committed.

## Qualification

Third-party plug-and-play enumeration/capture and physical disconnect/reconnect were not exercised because no external camera was available. The prototype implements `DeviceWatcher` added/updated/removed reporting and forces a new initialization after removal, but that behavior remains unproven on this rig. Compatibility therefore remains capability-based and must be established per connected Camera at runtime.
