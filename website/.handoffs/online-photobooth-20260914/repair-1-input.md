# Repair 1: pointer placement and export verification

Selected design remains Option 2. The root has completed browser interaction checks through all four stages, with real local task-generated image files. Typecheck, production build, and 26 combined hero/online tests passed. Full lint has one warning in the new test script.

## Reproduced issue

At `/fotohvn/online`, import task-generated images, select a filled position, and drag one framed photo-position overlay to another. Root used the documented in-app browser CUA drag with fresh DOM-derived element centers. Long Strip slot 1 to 4 and Pair slot 1 to 2 both called the drag-start UI (notice becomes “Choose a position in the frame…”), but source-image assignments did not swap at drop. Pair was retested with a stable explicit 1424 × 1104 viewport; centers were roughly (1074,320) to (1074,439). CUA drag returned normally. The tap Move → destination path and keyboard Move → native select → Apply both correctly swap the same images. The issue is isolated to the pointer gesture path as observed in this browser; do not treat untested Chrome behavior as verified. Chrome is unavailable in this tool environment.

Current `PrintPreview.tsx` relies on `state.move` already having rerendered before dragover/drop accept the event. Diagnose the actual source and choose a robust gesture approach. Native drag payload validation or pointer events with a movement threshold and stable source identity can avoid transient React state dependence. Preserve accessible tap/keyboard movement, selection on an ordinary click, cancellation, disabled pending operations, source identity, touch scrolling, and reduced motion. Do not enable accidental drops from outside the booth or duplicate a swap on click/drop/dragend.

## Additional bounded cleanup

1. Final output currently uses a fixed filename such as `fotohvn-long-strip.png`. Give each saved result a stable per-result/session timestamp suffix so repeat downloads and parallel sessions are identifiable. Keep the same blob for the final image and anchor, and don't claim successful saving from a click alone. Changing the filename is also needed to disambiguate this test export from an unrelated user download already in Downloads. Never read, alter, or delete that unrelated original.
2. Resolve the new lint warning at `scripts/online-photobooth.test.mjs` near line 215: the destructured `type` value in a `.map(({ type, ...rect }) => rect)` callback is unused. Preserve test intent.

## Already verified / preserve

- Candidate retake leaves all four source image URLs intact until accepted.
- Keep Original cancels with no source changes and restores photo selection/focus.
- Accept changes only the selected source slot.
- Keyboard and tap swaps are correct.
- Pair retains two extra photographs; restoring Long Strip restores four distinct retained sources in the new order.
- Full framed Silver preview is visible; final image and download link use the identical blob URL and natural dimensions 900 × 2700.
- Browser console errors/warnings are empty during these flows.

The in-app browser's `waitForEvent('download')` timed out after clicking the link; its `downloadMedia` operation returned success without exposing a resulting artifact. A file found under the old fixed filename was an unrelated session and was explicitly excluded from evidence. Root removed only its mistakenly created handoff copy, leaving the user's original untouched. Verify the next uniquely named test result; do not claim a successful on-disk export from that excluded file.

## Ownership

Fresh repair agent owns only the necessary files under `src/components/onlinePhotobooth/`, the new online tests, and `repair-1-result.md`. No homepage, asset, global stylesheet, package, or runtime changes. Root owns all browser operations and will retest the gesture/output after this repair. Do not use or reset the shared browser. Run focused tests and scoped lint, then report exactly what changed and any remaining uncertainty.
