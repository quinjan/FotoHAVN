# FOTOHVN online booth: independent plan-conformance review

final result: blocked

Reviewer: fresh agent `online_design_conformance`, 2026-09-14.

This review covers the implementation after pointer repair 1 and before filename repair 2. Root reported repair 2 complete while this review was being finalized. This reviewer has not evaluated that later repair and does not change this gate to passed. A fresh conformance reviewer must review the repaired source before responsive QA begins.

## Authority and method

Read `selected-design.md`, the complete `interaction-plan.md`, `brief.md`, `references-and-visuals.md`, `browser-verification.md`, `repair-1-input.md`, and `repair-1-result.md`. The selected Option 2 image, `visual-2.png`, controls Experience composition. Read `website/DESIGN.md`, `tokens.json`, `variables.css`, `theme.css`, and the FotoHAVN design QA skill. Approved local typography and the selected plan override incompatible generic gpt-taste defaults.

Read the Experience, navigation, Prints-entry, lift integration, new online route, all online-photobooth modules, and the focused test coverage. After root handed over browser access, reread the repaired pointer handlers, session transition, composition hook, filename helper, and component integration. Production files were not edited by this reviewer.

Rendered both routes through CUA's in-app browser. The subagent received an isolated browser session, so its fresh tabs did not reuse root's in-memory photographs. Used a 1424 × 1104 viewport and reset that override at completion. The chosen image and the current Experience screenshot were emitted together in one CUA tool invocation for a direct comparison.

## Actionable finding

### [P2] Preserve the device-photo fallback when `crypto.randomUUID` is unavailable

Baseline location: `website/src/components/onlinePhotobooth/download.ts:3`, called from `useComposition.ts` when accepting each completed render.

The repair-1 helper declared `compositionFilename(layoutId, createdAt = new Date(), resultId = crypto.randomUUID())`. Unlike `media.ts`, it did not guard this API. `randomUUID()` is restricted to secure contexts; the site already documents an HTTP staging origin, and the camera error UI explicitly offers device photographs when camera security requirements are unmet. [MDN Crypto.randomUUID](https://developer.mozilla.org/en-US/docs/Web/API/Crypto/randomUUID).

When this API is missing, a successfully composed PNG throws during filename construction before `setRendered` can accept it. The page therefore never obtains a ready initial preview, and the advertised device-photo flow cannot reach Look or Download. This is separate from the accepted requirement that public camera capture needs HTTPS.

The actual baseline helper was invoked in an isolated Node process with a stand-in `crypto` object lacking `randomUUID`. It failed with `TypeError: crypto.randomUUID is not a function`. This is a source-level missing-capability reproduction, not an insecure-origin browser or hardware-camera test. The localhost browser has the API and completes the flow.

Required repair: provide a guarded, non-sensitive result-ID fallback, retain a stable filename with the completed blob, and cover missing `randomUUID` and missing `crypto` with focused tests. Root reports that a subsequent repair addresses this finding; that claim is intentionally reserved for the next fresh reviewer.

No additional actionable P0, P1, or P2 findings were identified in this review.

## Verified design and behavior

| Area | Independent evidence |
| --- | --- |
| Selected Experience composition | The live section matches Option 2's wide serif headline with italic emphasis, 5/2/5 photographic band, three aligned offers, hairline rule, and online invitation. All three generated material-study assets loaded. No customer or generated-person photograph appears in Experience. The current hero, lift wrapper, anchors, and other guest content remain in place. |
| Local design identity | Computed heading family was Cormorant Garamond; body family was Manrope. Paper surfaces, ebony actions, restrained rules, and open composition carry into the online route. There is no second navigation inside Experience and no nested dashboard-card shell online. |
| Route integration | The main Experience invitation opened `/fotohvn/online`; Back to FOTOHVN returned to `/fotohvn#experience`. Desktop navigation and the Prints contextual entry expose the correct online URL in source and rendered DOM. Existing physical-print and guest-album links remain. |
| Layout and frame choice | A fresh session starts at Layout with Long Strip, Ivory, and no camera stream. Independently selected Story Strip/Archive, Pair/Walnut, and Contact Sheet/Gallery; previews, photo counts, radio states, and captions agreed. The initial Long Strip preview measured 900 × 2700; Story Strip 900 × 2100; Pair 900 × 1500. Contact Sheet's complete Gallery frame and shallow footer were visibly rendered. |
| Authored artwork | Functional empty photo windows and frame miniatures are the requested layout/frame controls. They are not substitute editorial imagery. Exact geometry, frame paper, lines, and typography are authored in the registry/compositor and included in PNG output. The main preview uses the actual composed PNG. |
| Four-stage structure | Layout → Photographs → Look → Download appeared with the expected current-step state and prerequisite controls. Capture review stays within Photographs. Stage headings receive focus when changing stage. The viewfinder remains 4:3 and capture/review controls are clearly grouped. |
| Device-photo path | Imported four distinct task-generated local files through the actual file chooser. Four unique source blob URLs populated the frame and photo rail. No camera permission was requested or granted. |
| Pointer rearrangement | Dragged position 3 onto position 1. Browser source order changed exactly from `[A,B,C,D]` to `[C,B,A,D]`; positions 2 and 4 retained their source identities. The swap was announced and focus moved to position 1. This independently confirms the repaired pointer path. |
| Per-slot retake | Selected position 2 and imported a replacement candidate. All four committed source URLs remained unchanged during candidate review. Keep Original retained all four and restored focus to position 2. A second replacement was accepted; only source index 1 changed, and the other three URLs were identical. |
| Keyboard and tap placement | Used the native destination selector with Home, Tab, and Return to apply a keyboard swap; source order matched the expected result. Switched to Pair, observed two retained photographs, used Move plus a destination click to swap the pair, and restored Long Strip. All four source IDs returned in the documented order without duplication or loss. |
| Photographic looks | Selected the four starter looks, including rapid consecutive changes ending at Silver. The complete Archive frame remained visible, with photographic windows changing and paper/lettering retaining their authored appearance. Returning to edit photographs after these changes preserved the exact source IDs. Silver remained selected when editing the result. |
| Final composition | Download stage visibly displayed the complete Archive/Silver strip, without interactive position overlays. Its image had natural dimensions 900 × 2700, and the Download PNG anchor used the identical blob URL. The filename included layout, UTC timestamp, and result suffix on this secure origin. |
| Edit/reset control | Edit retained the sources and selected look. Make Another displayed a consequence confirmation. Cancelling that confirmation retained the same final blob and filename in a focused repeat check. No successful-save claim appears merely from clicking Download. |
| Motion | Source provides scoped state arrival, preview fades, countdown feedback, and a short paper reveal with reduced-motion overrides. Experience reveal avoids taking ownership of the existing lift transform. Actual rendered state changes and completed paper presentation were inspected. Reduced-motion preference changes were not emulated in this review. |
| Desktop containment and console | At the 1424 × 1104 conformance viewport, both routes reported document overflow 0. Console warning/error reads returned `[]` for both review tabs during the tested flow. This is not the separate responsive acceptance gate. |

The main downloaded-image identity observation was `900 × 2700`, with both image and link pointing to `blob:http://localhost:3000/98dc9986-ed06-454b-8798-e349f5806b26`, and filename `fotohvn-long-strip-2026-09-14T09-21-38-616Z-e321c42e.png`. These are runtime review identifiers, not a claim of a saved disk artifact.

## Evidence artifacts

All screenshots are in this handoff directory and were serialized directly from CUA screenshot bytes:

- `conformance-experience-desktop.png`: current Experience compared with `visual-2.png` in the same tool output.
- `conformance-online-layout.png`: Long Strip/Ivory layout stage.
- `conformance-contact-gallery.png`: Contact Sheet/Gallery preview and selection controls.
- `conformance-online-capture-idle.png`: initial Photographs stage, no camera grant.
- `conformance-online-photographs.png`: committed task photographs, review frame, and arrangement controls.
- `conformance-online-retake.png`: candidate image alongside unchanged committed frame.
- `conformance-online-look.png`: Silver with the complete Archive frame.
- `conformance-online-download.png`: final composed image and output actions.

Local test inputs were `public/images/experience-online/{curtain,lens,keepsake}-960w.webp` and `public/images/hero/exterior-960w.webp`. The replacement input was the same task-generated curtain file; no private customer photograph or user Downloads file was used.

## Limits and next gate

- Physical-camera grant, live capture, interruption, and real-device mirror behavior were not exercised. Source and existing focused adapter tests were reviewed; those do not establish hardware success.
- A real Download PNG link click completed, but the independent in-app browser's download-event wait timed out after 3 seconds. Root separately encountered the same limitation. No uniquely named saved disk artifact was observed, and no unrelated Downloads file was read or accepted as evidence. The composed-image/link identity is verified; native on-disk saving remains unverified.
- The scoped source search found no upload, analytics-image, or browser-storage calls in the new booth modules. Browser network/storage verification remains unverified: the read-only DOM evaluator does not expose `performance`, and the available browser/tab capabilities expose no network inspector. No runtime no-upload or no-storage pass is inferred from this restriction.
- Root reported clean full lint, a successful production build, and 36 passing combined tests after pointer repair 1. These are parent-owned checks against the pre-filename-fix source and do not negate the missing-capability reproduction above.
- Responsive QA, including 320px, has not begun. Keep it gated until a fresh reviewer passes conformance after the filename fallback repair. Do not reuse this reviewer to approve the repaired result.
