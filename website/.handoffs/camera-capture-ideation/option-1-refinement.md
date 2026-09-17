# Option 1 refinement — 2026-09-18

## Implemented selection

The user approved implementation of the centered translucent countdown with the circle removed. Implemented in CameraWorkspace.tsx / CameraWorkspace.module.css and integrated into OnlinePhotobooth.tsx. useCamera.ts now supports facing-mode/device selection and recovery. cameraGeometry.ts aligns live framing with the existing capture pipeline. See implementation/design-qa.md for checks, repairs, screenshots and device limits. Earlier proposed circle and upper-center badge are superseded. No deployment or commit.

## Latest revision: centered translucent countdown

The user requested a centered countdown with lower opacity and styling more connected to the camera. This supersedes the upper-center badge proposal below.

Latest visual: concept-1-centered-countdown.png. Built-in Image Gen edited the preceding refinement. Proposed treatment: Cormorant Garamond medium numeral in warm ivory at about 60% opacity, surrounded by a single unfilled hairline circle at about 25% opacity, echoing the shutter ring. Center both on the visible photograph, not the desktop container. Initial mobile sizing target is a roughly 100px numeral within a 144px ring; responsive tuning and bright-background readability require rendered validation. No dark badge or whole-image dimming. Soft fades between 3, 2, and 1; remove the complete overlay at exposure. Reduced motion uses immediate numeral changes. The overlay is presentation only and never appears in saved photographs. Keep Stop outside the photograph and reserve state geometry. These are proposed design values, not measured properties of the generated illustration.

Status: selected option being refined; no production implementation performed. Prior images retained for comparison. Exact generation prompt is in option-1-centered-countdown-prompt.txt.

## Previous refinement

The user selected option 1 for refinement, favoring familiar mobile-phone camera controls. They explicitly allow the temporary countdown to overlay the viewfinder. This supersedes the earlier recommendation to keep all countdown text outside the photograph. Production implementation is not started.

Visual: concept-1-refined-countdown.png, generated with built-in Image Gen from concept-1-quiet-camera.png. Original concept retained.

## Proposed countdown

- Upper center of the actual visible photo, anchored to that photo rather than the full desktop container or letterbox gutters.
- Compact warm-white numeral on translucent ebony circular backing. Initial sizing target: 56–64px backing / 40–44px numeral on mobile; 80px backing / 56px numeral on larger screens. Tune after rendered testing.
- Top inset 16–24px on mobile and 24–32px on larger screens, relative to the photo edge; respect any visible crop region and safe areas.
- Fixed placement for 3, 2, 1. Remove at exposure, never bake into the saved photograph. No whole-image darkening or large center overlay.
- Continue showing photo count/progress in the header and Stop in the bottom dock. Reserve header/dock space so image geometry does not move between ready, countdown, feedback, or stop.
- This is a fixed position, not face-aware positioning. It reduces interference with the middle of the photograph but cannot guarantee no overlap for every composition. The user explicitly permits a temporary overlay.
- Phone orientation/camera position varies; the rationale is drawing attention upward, not claiming exact alignment with every device lens.

## Visual-review notes

The revised board shows the upper-center overlay on all three capture previews without a full-image veil. Generated typography and screen proportions remain conceptual. Header height equality and exact crop matching still need implementation validation; the generated board alone is not evidence of those behaviors. Missing small mobile photo-count text in the illustration does not remove that requirement.

## Next step

Review countdown placement and continue refining the selected direction before implementation.
