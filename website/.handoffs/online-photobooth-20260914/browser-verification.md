# Browser verification record

Root verification, 2026-09-14. Final status: passed for the implemented local scope. Both repairs, fresh final conformance, desktop/tablet/mobile reviews, and fresh synthesis are complete. See `final-verification.md` for the consolidated result and explicit hardware/save limits. Earlier observations below are retained as dated baselines.

## Environment

- Actual app route `/fotohvn/online` inspected in the in-app browser, with the homepage Experience at `/fotohvn#experience`.
- The previous local preview stopped during testing. Root started a hidden Next dev process. `--hostname localhost` bound only to IPv6, which the in-app browser could not reach; root verified HTTP 200 on that binding and IPv4 refusal, stopped only the two processes root had just created, then restarted on `127.0.0.1:3000`. Browser navigation continues to use the canonical `http://localhost:3000` URL. Parent process at restart: 30212. Check current identity before managing it later.
- A browser error tab rejected subsequent navigation because its internal error document was a data URL. A fresh tab in the same in-app browser restored normal access. No security interstitial was bypassed.
- Functional testing used task-generated curtain, optical-glass, print still-life, and existing generated hero images as local file input. No camera permission was granted or live camera frames captured by root.

## Verified before pointer repair

1. The selected Experience section renders its new three images, copy, and online invitation. Initial observed 843px layout had document overflow 0. This was an integration observation, not the final responsive gate.
2. Selecting Archive and continuing enters the Photographs stage with the expected four slots and frame.
3. The browser's file chooser accepted four local task images. Four unique source blob URLs appeared in the rendered photo rail.
4. Retake position 2: all four rail source URLs remained unchanged while a replacement candidate was pending.
5. Keep Original: all four source URLs still matched the initial sources; focus returned to photo position 2.
6. Accept a replacement in position 2: only that position's source URL changed; the other three remained identical.
7. Keyboard Move from position 3 to 1: Tab reached the destination select, Home chose the first position, Tab/Return applied the move, and the resulting source order exactly matched the expected swap. Focus returned to position 1.
8. Pair layout retained two extra photographs in its visible tray. A tap Move → target swap worked in Pair. Returning to Long Strip restored four distinct sources, with no photograph lost.
9. The Look step displayed the full chosen Archive frame. Selecting Silver updated the visible composition and selection summary.
10. Final stage displayed a 900 × 2700 image; the image `src` and Download anchor `href` were the identical blob URL. The visible metadata said PNG and the correct frame/look.
11. Browser console error/warning read returned `[]` during these interaction checks.

Screenshots: `experience-initial-browser.png`, `online-layout-initial.png`, `online-capture-arranged.png`, `online-framed-look.png`. Early screenshots use the then-current browser size. The pointer reproduction was later stabilized at 1424 × 1104.

## Repair / verification gaps

- Pointer dragging started its UI state but did not commit a swap in two tested layouts. `repair-1-input.md` records the reproduction and fresh repair scope. Keyboard and tap alternatives passed, but this does not clear the pointer requirement.
- The in-app browser's native download-event waiter timed out. Its `downloadMedia` helper returned completion but did not expose a saved artifact. A file found under the old generic filename was visually found to be a different session and explicitly excluded from test evidence. Root removed only the mistaken copy in this handoff; the original user file was preserved. The next test must use the newly unique result filename and inspect the actual task-generated material images.
- Chrome fallback is unavailable to this tool session. Do not claim a Chrome or Safari hardware/browser pass.
- Physical camera behavior remains unverified by root; focused adapter tests cover explicit video-only request, late-grant cleanup, and mirrored capture, not a physical-device session.

## Code checks before pointer repair

- Full TypeScript check: passed.
- Production build: passed; homepage and online route generated successfully.
- Combined hero and online tests: 26/26 passed.
- Full lint: zero errors, one warning in the new test script. The bounded repair includes that warning.

Rerun affected checks after repair. Independent plan conformance and responsive evidence have not yet been declared passed.

## Pointer repair retest

The repaired mouse/pen pointer path passed a real browser drag from Long Strip position 1 to 4 at 1424 × 1104. The resulting rail image sources showed an exact 1/4 swap with positions 2 and 3 unchanged. The status announced the placement and focus returned to position 4. The fresh repair also added atomic source-identity/revision guards, cancellation coverage, and a stable timestamp/result identifier in the filename.

After pointer repair, full lint passed with no warnings or errors. Combined hero, online, and pointer suites passed 36/36. The production build passed against that source.

The unique test download name `fotohvn-long-strip-2026-09-14T09-11-08-425Z-7faba995.png` was read from the actual Download link. A real DOM click completed, but that exact file was not exposed in the local Downloads folder. This remains a browser automation artifact-access limitation; do not convert it into a confirmed on-disk download pass. The downloadable blob identity, dimensions, and compositor contract are verified independently.

## Final source checks after filename compatibility repair

The first independent conformance review found one additional P2: a missing `crypto.randomUUID` API could prevent the completed preview from being stored. The bounded repair adds a guarded local serial fallback. Its regression cases first reproduced both missing-API failures, then passed with 256 distinct same-timestamp results in each fallback environment. See `repair-2-result.md`.

Root inspected the final helper and its use in `useComposition`; a filename is still created once per accepted rendered blob. Root reran the combined suites against this final source: **39/39 passed, no skips**. Repository lint and TypeScript passed under the fresh repair agent. Root's final `npm run build` passed, including TypeScript and static generation for `/`, `/_not-found`, and `/online` (served under the configured `/fotohvn` base path).

The first conformance report intentionally records its earlier blocked baseline. The fresh recheck report is authoritative for the repaired source. Independent responsive reviews follow only after that recheck passes.

The fresh recheck passed. Root then reset its temporary viewport, opened Look and Download using the actual stage controls, and saved `root-final-result-default.png` as a screenshot of the completed task-generated material strip. This is a screenshot, not a saved PNG-download artifact. Root used Make Another → Start a New Strip to clear only its own generated test session. The route returned to the empty Long Strip/Ivory Layout stage with heading focus, ready for the user.

Final host-level route checks returned **HTTP 200** for both `http://localhost:3000/fotohvn` and `http://localhost:3000/fotohvn/online`, with the expected new Experience copy and online starting-screen copy respectively. The hidden development server remains running for the local preview.
