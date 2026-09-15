# Final responsive QA input

Run this gate only after the fresh `gpt-taste-implementation-verification-recheck.md` reports passed. This is a read-only browser review of the final repaired source, using separate fresh desktop, tablet, and mobile agents followed by a fresh synthesis agent.

## Authority and evidence

Read `selected-design.md`, `visual-2.png`, the final conformance recheck, and the local `DESIGN.md`, `tokens.json`, `variables.css`, and `theme.css`. The implementation has no active production editor. All 39 focused tests, lint, TypeScript, and production build passed after repair 2. The earlier blocked conformance report is intentionally preserved as a pre-repair baseline.

Use CUA to render `http://localhost:3000/fotohvn#experience` and `http://localhost:3000/fotohvn/online`. Do not start or stop the server. Each agent has an isolated IAB session; use `createBrowserTab('iab', URL, {visible:false})` as its browser entry. Read the returned documentation and use only exposed APIs. Save screenshot bytes directly into this handoff with a viewport-specific prefix. Do not use external Playwright or hidden browser state injection.

## Assignments

| Reviewer | Viewports | Focus |
| --- | --- | --- |
| Desktop | 1440×1100 and 1024×900 | Selected Experience composition, all three new entry links, tight desktop navigation, online four-stage hierarchy, pointer/keyboard placement and framed result at desktop width. |
| Tablet | 820×1180 and 1180×820 | Portrait/landscape transitions, menu behavior, correct three-story reflow, frame/layout selection, capture-review arrangement, look/final actions. |
| Mobile | 320×720 first, then 390×844 | No horizontal overflow or cropped text; all three Experience stories and invitation; menu entry; four-stage device-photo flow; per-position retake and tap Move; readable frame/looks and reachable final actions. |

Each reviewer should compare the chosen image and a current rendered Experience screenshot in the same tool output, allowing the specified intentional reflow at narrow widths. Inspect the actual page through natural scrolling. Record DOM-backed containment and image loading checks alongside screenshots rather than treating a build or screenshot alone as interaction evidence.

## Safe, relevant interaction fixtures

Use only these small task-generated local images through the real file chooser:

- `website/public/images/experience-online/curtain-960w.webp`
- `website/public/images/experience-online/lens-960w.webp`
- `website/public/images/experience-online/keepsake-960w.webp`
- `website/public/images/hero/exterior-960w.webp`

The user requested single-slot retakes and reordering. Check a selected photo's replacement/cancel/accept behavior or a placement action appropriate to the viewport, and confirm source identities from rendered image attributes when needed. Check that Look displays the selected frame and final image/download share the same blob. The full transaction/drag matrix already passed root and the first independent reviewer; responsive reviewers need not duplicate every permutation.

Do not activate a physical camera, inspect user Downloads files, or claim on-disk download success. Both root and the first independent reviewer encountered the same native download-artifact limitation in IAB. The displayed PNG dimensions and download-link identity are testable; saved-file access and physical hardware remain explicit manual test limits. Browser network/storage instrumentation and actual touch devices are unavailable; distinguish source review and simulated viewport evidence from runtime/hardware claims. Do not change browser settings through unsupported APIs.

Check warning/error console output and keyboard focus visibility. Reduced-motion source is present; emulate only if the exposed browser API supports it and otherwise state the limit. Reset your viewport override after evidence capture. Stop after the agreed matrix unless a new actionable concern justifies another check.

Write one report per reviewer: `responsive-desktop.md`, `responsive-tablet.md`, or `responsive-mobile.md`, with `final result: passed` or `final result: blocked`, actual viewports and screenshot names, interaction results, and prioritized actionable findings. A finding must identify a reproducible user impact. Do not edit production files or the shared `website/design-qa.md`; synthesis owns the final scoped QA summary.
