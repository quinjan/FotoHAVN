# Repair 1 result: pointer placement and export naming

Implementation is complete. Production files are frozen for the fresh conformance review. The root agent reported a successful browser retest of the repaired pointer placement. On-disk saving remains unverified because the available browser tooling did not expose a completed download artifact.

## Diagnosis and repair

The original native handlers accepted dragover/drop only when the `PrintPreview` render already contained `state.move`. A focused component-event harness reproduced unchanged `[A,B,C,D]` after starting and dropping a drag without an intervening React render; Pair likewise remained `[A,B]`. This demonstrates the state-render dependency. The root had independently reproduced actual browser failures; the repair does not claim that every detail of the browser's native drop failure was instrumented.

`PrintPreview.tsx` now uses a local primary mouse/pen pointer session. It captures the pointer, preserves the original photograph ID and composition revision, and starts a drag after eight CSS pixels of movement. Release hit-tests a photo-position button inside the same frame. A completed gesture sends a single `move-position` action, independent of a prior move-state render. The reducer rejects stale revisions, changed source identities, pending operations, another pending Move, and departure from the capture stage. The existing tap/keyboard Move path and pointer placement share the same identity-preserving placement function.

Ordinary clicks retain selection. Touch never acquires pointer capture or prevents its native scrolling, and continues to use the selected plan's Move then tap-destination controls. Native dragging is disabled so it cannot compete with pointer placement. Outside releases, Escape, pointer cancellation, lost capture, blur, and workspace cleanup cannot commit a placement. The gesture is consumed before pointer release; the resulting click is suppressed, and repeated delivery cannot swap the photographs back. Scoped cursor and destination-outline feedback follow the existing typography, palette, and motion rules.

Each composed blob now receives a filename containing its layout, UTC creation timestamp, and a short random result identifier. That filename is stored with the rendered blob/URL, so repeated clicks on the same displayed result retain the same name. A new composed result receives a distinct name. The final preview and download anchor still use the identical blob URL. No saving-success notice was added.

## Files changed by this repair

- `src/components/onlinePhotobooth/PrintPreview.tsx`: pointer lifecycle, contained destination detection, cancellation, and duplicate-click suppression.
- `src/components/onlinePhotobooth/session.ts`: atomic validated pointer placement; shared placement behavior for existing Move controls.
- `src/components/onlinePhotobooth/OnlinePhotobooth.tsx`: validated placement callback, announcement/focus integration, and stored download filename.
- `src/components/onlinePhotobooth/OnlinePhotobooth.module.css`: selection-safe pointer cursor and destination feedback only.
- `src/components/onlinePhotobooth/download.ts`: result filename construction.
- `src/components/onlinePhotobooth/useComposition.ts`: bind the filename to the completed blob.
- `scripts/online-photobooth-gesture.test.mjs`: focused component-event and reducer regression coverage.
- `scripts/online-photobooth.test.mjs`: remove the unused destructured `type` while preserving the compositor rectangle assertion.

No homepage, global stylesheet, asset, manifest, runtime, or route edits were made. No browser was operated by this repair agent, and no commit was created. The local `website/AGENTS.md` and bundled Next.js `use-client.md` guide were read before production edits.

## Verification

- `node --test --test-isolation=none scripts/online-photobooth.test.mjs scripts/online-photobooth-gesture.test.mjs`: **24 passed, 0 failed**. Coverage includes same-render and mid-render pointer gestures, occupied and empty destinations, cancellation/blur/cleanup, primary-pointer and frame-containment guards, click suppression, stale/duplicate actions, Pair retained-tray restoration, existing transactional retake/camera/compositor behavior, and timestamp/result filename distinctness.
- `node node_modules/eslint/bin/eslint.js src/components/onlinePhotobooth scripts/online-photobooth.test.mjs scripts/online-photobooth-gesture.test.mjs`: **passed without warnings**. After the final test additions, both changed test scripts were linted again with no output and exit 0.
- `node node_modules/typescript/bin/tsc --noEmit --pretty false`: **passed**.
- Trailing-whitespace scan over all owned code/test files: **no matches**.
- The default Node test runner initially failed to spawn its subprocess with `EPERM`; running the same tests in-process with `--test-isolation=none` succeeded. This was a runner restriction, not an ignored test failure.

The event tests execute the real component handlers with a lightweight React/DOM harness; they are not a substitute for browser verification. The root agent subsequently reported its actual 1424 × 1104 browser retest **passed**: dragging frame position 1 to 4 changed exactly those source URLs, preserved positions 2 and 3, announced the swap, and focused position 4. The root also observed the unique filename and identical final-image/download blob URL.

The root reported that a real download-link click returned but the in-app browser exposed neither a completed download event nor the exact uniquely named saved file. Chrome was unavailable. No unrelated download was used as evidence or altered by this repair agent. The browser saving limitation remains explicit; fresh conformance and responsive review are the next gates.
