## Online camera workspace — 2026-09-18

**Findings:** no remaining actionable P0/P1/P2 issue in the implemented camera layout and tested browser flow. Physical-camera verification remains a limit.

### Visual authority and comparison

Source: `.handoffs/camera-capture-ideation/concept-1-centered-countdown.png` (1464 × 1075 board), plus the user's explicit instruction to remove the circle around the countdown. This supersedes the ring in the raster reference. Existing DESIGN.md typography and colors were retained.

Opened the source board and rendered mobile/desktop screenshots together in the same comparison input, and compared the iPad capture separately alongside the same board. This is a paired-image inspection, not a pixel-diff or fabricated composite. Device panels within the board are generated, scaled illustrations; actual CSS viewports and JPEG capture dimensions match 390 × 844, 834 × 1194 and 1440 × 1024. Captures also cover 320 × 568 and 844 × 390. The browser reserves a 15px gutter; measured workspace widths are consequently 375, 819, 1425, 305 and 829px. No horizontal page overflow was observed.

Final evidence in `.handoffs/camera-capture-ideation/implementation/`:
- `mobile-countdown-final.jpg`, `mobile-ready-final.jpg` (390 × 844).
- `phone-320-ready.jpg` (320 × 568).
- `ipad-ready-final.jpg`, `ipad-countdown.jpg` (834 × 1194).
- `desktop-ready-final.jpg`, `desktop-countdown.jpg` (1440 × 1024).
- `landscape-ready.jpg` (844 × 390).
- `signature-mobile-ready.jpg` (390 × 844), `download.jpg` (834 × 1194).
- `responsive-metrics.json` records measured mobile/iPad/desktop geometry.

The portrait comparison uses Panorama Four, matching the mock. Ready screenshots include a selected-photo retake; their status copy intentionally differs from first capture. Countdown screenshots show photograph 1 and, in some cases, a five-second timer instead of the mock's photograph 2/three seconds. These are the same layout states with different session data. No photography fidelity claim: the in-app browser supplies a black stream with a camera-unavailable graphic, rather than a physical camera image. It still provides video frames and permits exercising canvas capture. Final images are JPEG bytes saved with .jpg extensions.

Focused countdown/control review used the readable full-resolution 390px capture and computed styles: countdown text is rgba(251,248,242,0.6), transparent container, zero border. There is no circle or badge. A separate focused composite was unnecessary because those elements are legible in the full mobile frame. Number fade-in means screenshots can capture less than its steady-state opacity.

### Five fidelity surfaces

- **Typography:** Cormorant Garamond for brand/countdown, Manrope for controls; restrained serif numeral, legible labeled camera controls. Small-screen labels no longer wrap awkwardly.
- **Layout:** viewport-owned camera surface, compact header/progress, largest fitted exact-crop preview, persistent separate shutter dock. Secondary controls become invisible/inert during countdown while preserving their space. Short landscape screens use a side dock. Notice space is reserved. iPad preview bounds were exactly x84/y84/651×868 before and during countdown.
- **Colors/tokens:** existing off-white, cream, ebony and muted-brass tokens; translucent ivory number without a full-image veil. Exposure feedback is brief and removed for reduced motion.
- **Imagery/crop:** live video is the image source, not a generated photograph shipped as UI. Preview geometry matches existing 4:3 normalization followed by the selected template crop; portrait, wide and near-square templates have regression coverage. Landmark/face matching against actual phone camera output remains unverified.
- **Copy/content/icons:** Start/Resume, Stop, timer choices, Mirror, Flip camera, Exit and retake/review controls are functional. Phosphor icons are reused. Imported-photo fallback remains available, including a failed-camera retake.

### Comparison and repair history

1. Initial browser inspection found a moderate preview shift when a retake status message disappeared during countdown (P2). Reserved a stable notice area; repeated measurements on iPad show identical photo bounds in ready/capture.
2. At 320px, Flip camera and replacement labels wrapped awkwardly (P2). Kept the support label together, shortened the visible retake caption to Take photo N, and compacted the dock/header on short portrait screens. Post-fix evidence: phone-320-ready.jpg; all controls remain accessible.
3. Camera-switch failure during retake advertised import but only exposed Keep original (P2). Restored Import photos alongside Keep original. Subsequent camera-error/retry flow recovered without discarding accepted photos.
4. Final paired comparison retains the approved composition and the requested numeral-only override. Additional fullscreen, import, status and retake controls are necessary production behavior absent from the simplified mock.

### Functional and command validation

- Browser: automatic four-photo sequences on Panorama Four and Signature Strip; automatic transition to Arrange; select one photograph, capture replacement, accept it; retry/keep original; Stop during countdown; mirror toggle; five-second timer; unsupported rear-camera request/error and retry to previous source; Escape exits a retake while retaining originals; continue through filters to a prepared 1800 × 600 PNG link.
- Fullscreen control was exercised in the in-app browser; accessibility output switched to the workspace-only tree and back. This does not establish iOS/Android fullscreen compatibility.
- Browser warning/error log: [].
- `node --test scripts/online-photobooth.test.mjs scripts/online-photobooth-preview.test.mjs scripts/online-photobooth-gesture.test.mjs`: 39/39 passed, including camera release/stale permission, lens-selection recovery, enumeration failure, crop geometry, cancellation and transactional retake cases.
- Full `npm run lint`: passed. Focused ESLint passed. TypeScript `--noEmit`: passed. Final production `npm run build`: passed.
- No deploy or commit. Existing unrelated booth changes were preserved.

### Remaining limits

Actual mobile front/rear lenses, multiple physical webcams, bright/dark-face readability, browser permission prompts, physical touch input and mobile browser fullscreen behavior require device checks. Reduced-motion handling is implemented but was not browser-emulated. Partial-sequence preservation has reducer/timer tests; the attempted live partial-stop check finished its sequence before the interaction, so that particular browser assertion is not claimed. No independent reviewer was spawned.

final result: passed

### Follow-up: unavailable Flip camera control

Flip camera now requires at least two distinct, nonempty camera device IDs. Both its disabled state and click handler enforce availability. Device inventory is cleared when the stream is released or a device-change refresh begins, so unknown/unavailable cameras do not leave the action enabled. The existing disabled styling and a "No other camera available" tooltip explain the state.

Verified in a separate browser session: after the live camera was ready, Start, timer and Mirror were enabled while Flip camera remained disabled. The user's open session and photographs were left intact. TypeScript and all 39 focused tests passed for this follow-up.
