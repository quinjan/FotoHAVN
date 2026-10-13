---
status: accepted
date: 2026-10-13
---

# Operator and Guest Screens on bound displays

The desktop booth has two windows. The Operator Screen opens on the Windows primary display. The Guest Screen opens full screen on the exact display stored in the Event's Guest Display Binding when the Event starts, and closes when it ends. Only one Event runs at a time, the app never silently moves the Guest Screen to another display, and `END EVENT` on the trusted Operator Screen is the main way to end an Active Event.

Replaces: the corresponding part of the deleted WinUI 3 app, which ran the Active Event in a single window and left it only through a guest-facing 1.5-second hold with no PIN (`app/docs/design-system/patterns/operator-exit.md`, deleted in M1).

## Context

- A booth usually has a laptop for the operator and a separate touch monitor facing guests. Some setups have only one display.
- The operator needs to watch the running Event, see Operator Assistance, manage its gallery, and prepare other Events without interrupting guests.
- Guests must never reach operator screens, and guest content must not leak into the next Event.
- Windows display identities can change after driver updates or docking-station changes (risk R9), and touch from the guest monitor can map to the wrong display (risk R8).

## Decision

**Two windows, one Active Event (D32, D34).**

- The Operator window opens maximized on the primary display; the operator may move or resize it.
- The Guest window is borderless, covers the bound display's full bounds (not just the work area), and stays topmost in field builds.
- Both declare per-monitor DPI awareness (v2) and scale independently on their own displays.
- The Operator window follows Windows display scaling. The Guest window ignores it for layout: the host sets the guest WebView2 zoom factor to 1 / (display scale), and reapplies it when the display's scale changes, so the guest layout always works in the display's physical pixels ([`DESIGN.md`](../../DESIGN.md#scaling)). (Proposed default Q14, plan section 18; confirmed by the owner at the M0 gate.)
- Both use one React UI build and share one `CoreWebView2Environment`; each has its own WebView2 controller, bridge, and role (`operator` or `guest`) assigned by the host ([ADR 0003](0003-react-ui-in-wpf-webview2-over-a-dotnet-brain.md)). The Guest window's WebView2 is created at Start Event and disposed when the Event ends.
- Only one Event runs at a time, whatever the number of displays. Other displays are only choices for where the Guest Screen goes.
- Both screens are landscape only, take touch, mouse, and keyboard, and are verified at 1280×720, 1366×768, 1920×1080, and 2560×1440 (D15). For the Guest Screen these are physical resolutions.

**Guest Display Binding (D33).**

- The host's display adapter lists active displays with `QueryDisplayConfig` and `DisplayConfigGetDeviceInfo`: monitor device path, friendly name, bounds, work area, DPI, and whether it is primary. It watches display changes and only reports facts.
- Event setup lists every Available Display by name, resolution, and position, marking the one showing the Operator Screen. `IDENTIFY DISPLAYS` shows a borderless, non-activating overlay with each display's number and name for 3 s.
- The binding stores the monitor device path plus the friendly name for display. Matching uses the device path only and never falls back to position or index. The same monitor on another port counts as a different display, and the operator re-chooses it.
- If the bound display isn't connected, Event setup says so and keeps the binding until the operator chooses another. Start Event stays in Event setup with "The chosen guest display isn't connected." The app never substitutes another display.
- Binding validation, the missing-display check at Start, disconnect handling, and single-screen detection are pure rules in `FotoHavn.Core` with unit tests.

**Operator Screen during an Active Event (D35).**

- A running-Event panel shows read-only facts with text labels: guest display state, camera state (whether or not a Guest Cycle is running), current guest step, completed Guest Cycles, and time since start. Its actions are `OPEN GALLERY` (the live operator view of the Event Gallery, including delete, opened without the hold or PIN; proposed default Q9, plan section 18; confirmed by the owner at the M0 gate), `RETRY` (only during Operator Assistance), and `END EVENT`. The panel sits above the Event list only; on every other Operator Screen view, an Operator Assistance alert appears as an inline status line with a text link back to the panel ([`DESIGN.md`](../../DESIGN.md#operator-screen)).
- The running Event can't be edited, deleted, or restarted, and no Event can be started. The brain rejects those commands with reason `eventRunning`; the UI also disables them.
- The operator can create, edit, and delete other Events and browse their galleries.

**Ending an Active Event (D36, D16).**

- `END EVENT` on the Operator Screen asks for confirmation but no PIN, because the Operator Screen is trusted. Confirming ends any unfinished Guest Cycle as `operator_exit` (its Captures stay; a Guest Cycle on Keepsake is already `completed` and stays so), closes the Guest Screen, and releases the camera and wake lock.
- The Guest Screen keeps a backup exit on every view: a discreet 3-second press-and-hold in the top-right corner (or Enter or Space held for 3 seconds on the focused control; proposed default Q12, plan section 18; confirmed by the owner at the M0 gate), then the Event's Operator PIN on an on-screen digit pad. Five wrong attempts lock the dialog for 30 seconds. A correct PIN ends the Event exactly as `END EVENT` does.

**Guest display lost (D37).** If the bound display disconnects at any time during an Active Event, including on Start or in the guest gallery, the Guest window closes with it and the Event pauses in Operator Assistance: any countdown is cancelled, Phase Timers pause, saved Captures stay, and the Operator Screen alerts. During a Guest Cycle, `RETRY` re-proves the camera with a fresh frame, checks storage, and checks the bound display, reopening the Guest Screen once the same display returns; then Phase Timers resume and the Guest Cycle returns to the step it was on. When no Guest Cycle is running (Operator Assistance entered from a guest display loss on Start or in the guest gallery, or from a storage failure at `STEP INSIDE`), `RETRY` checks only the bound display and storage; then the Guest Screen returns to Start, which shows Start unavailable if the camera still isn't eligible. Camera and storage failures enter Operator Assistance only during a Guest Cycle, or for storage, while creating the Guest Cycle record at `STEP INSIDE`. Outside one, an ineligible camera or unwritable storage shows Start unavailable instead, and the guest gallery shows its own inline error if it can't read ([ADR 0004](0004-brain-owned-webcam-through-one-exclusive-mediacapture-stream.md), [ADR 0005](0005-sqlite-metadata-with-file-artifacts-for-event-storage.md)).

**Single-screen setups (D38).** If the bound display is the one showing the Operator Screen, or there is only one display, the Guest Screen covers it full screen. Event setup notes "The Guest Screen will cover this screen. Use the hold and PIN to return." The operator returns with the hold and PIN. If the Operator Screen's display disconnects and Windows moves the Operator window onto the guest display, it sits behind the topmost Guest Screen until the Event ends.

**Input.** Touch must map to the guest display through Windows "Tablet PC Settings → Setup", documented in the local development guide. The Guest window takes touch without needing keyboard focus, so mouse use on the Operator Screen doesn't interrupt guests.

**Fakes.** The headless host and tests use a fake display adapter with configurable displays, connects, and disconnects. End-to-end tests use virtual displays mapped onto the real monitor ([ADR 0007](0007-unit-first-testing-with-real-camera-end-to-end.md)).

## Considered options

- **A single window for operator and guests** was rejected. The operator couldn't watch or manage the running Event without leaving it, and the guest-facing hold would stay the only way out.
- **Guest-facing hold and PIN as the only exit** was rejected for two-screen setups (D36). It stays as the backup and as the only way out in single-screen setups (D38).
- **Moving the Guest Screen to another display**, or matching a display by position or index, was rejected (D33). A wrong match could show guests the wrong screen or hide the Guest Screen from them.
- **Running more than one Event at a time** on several displays is out of v1 (D34).
- **Keeping the Guest window's WebView2 between Events** was rejected so its process can't carry guest content into the next Event.

## Consequences

- A guest monitor moved to another port, or renamed by a driver or dock change, blocks Start until the operator re-chooses it (risk R9).
- The booth checklist must include touch mapping for the guest monitor (risk R8).
- Two WebView2 controls with live preview can strain the booth laptop; spike S5 measures it, and the operator preview's frame rate may be capped.
- In single-screen setups, the running-Event panel isn't visible during the Event, and a lost PIN is recovered only by closing the app from Windows, which ends the Active Event (D17).
- Physical two-display behavior is proven by `@physical-displays` scenarios and manual checks, not by every end-to-end run.
