# Scanning Available Cameras UI prototype

> PROTOTYPE — throwaway code. Do not promote directly to production.

Question: **How should Scanning Available Cameras, Eligible Camera selection, selected JPEG output, and stable rejection reasons coexist with the live preview and Camera Tuning in Event setup?**

Three variants of the Camera section are mounted in the approved Event setup modal, switchable with `?variant=A|B|C`. Twelve scenarios are switchable with `?scenario=1..12`.

Run from this directory:

```powershell
.\run.ps1
```

Then open <http://127.0.0.1:4180/camera-qualification-prototype/?variant=A&scenario=1> at a 1280 × 720 viewport.

Keyboard:

- `Left` / `Right`: previous or next variant
- `1`–`6`: show a scenario
- `Escape`: close setup and cancel Scanning Available Cameras

In Variant C, the Camera menu toggles from its trigger, closes after a selection, closes on an outside click, and consumes the first `Escape` before Event setup closes.

Per issue #19, every variant keeps the icon-only gear trigger with accessible name `Camera tuning`. The gear expands and collapses the mirrored preview and supported Camera Tuning controls as one region, and remains disabled until an Eligible Camera is selected.

Variant C renders no placeholder or Capture output while Camera Tuning is collapsed. Its expanded region uses equal-height left and right columns, with a larger 3:2 preview and a Camera Tuning panel stretched to the same boundary. Only Capture output appears as small metadata beneath the preview; the redundant mirrored-preview line and separate output box are omitted. The rescan action is labelled `Scan Available Cameras`, and the Camera heading has no redundant status badge.

While Scanning Available Cameras, the compact modal ends immediately after the disabled Camera dropdown. It does not reserve empty Camera-panel space for preview, tuning, or results that are not yet available.

Variant C renders the opened Selected Camera menu in an overlay layer outside the Camera section and modal. The menu is anchored to its trigger without changing the compact modal's height, and still closes on selection, outside click, trigger click, or `Escape`.

Scenarios 7–12 extend the approved Variant C direction with the post-review decisions:

- rescanning suspends preview/tuning, preserves the exact selection, and waits for the complete scan;
- an output-geometry change is ordinary dirty configuration rather than a validation blocker;
- direct activation returns to setup when validated JPEG output dimensions change;
- duplicate friendly names receive stable secondary identity details;
- rejected Cameras remain keyboard-readable while unavailable for selection; and
- shared temporary-storage failure appears once as a setup-level blocker rather than rejecting every Camera.

There is no separate framing-acceptance action or persisted framing-acceptance record. Selecting an Eligible Camera satisfies the Camera gate; Camera tuning remains optional, and save/start expresses operator satisfaction with the configuration.
