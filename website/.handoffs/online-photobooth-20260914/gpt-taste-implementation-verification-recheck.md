# FOTOHVN online booth: fresh conformance recheck

final result: passed

Reviewer: fresh agent `online_conformance_recheck`, 2026-09-14. This gate reviews final source after pointer repair 1 and filename compatibility repair 2. It supersedes the older blocked conformance gate for the current implementation; the earlier report remains intact as historical evidence.

No actionable P0, P1, or P2 conformance findings remain. Fresh responsive QA may begin. This is a design/plan conformance pass, not a claim that all hardware and native-save checks have been completed.

## Authority and method

Used `selected-design.md` and Option 2 (`visual-2.png`) as the current authority, together with the interaction contract and integration seams in `interaction-plan.md`, the implementation reports, both repair reports, the root browser record, and the first review's complete finding inventory. Read the local design authority and FotoHAVN design QA workflow. The user-selected warm editorial direction takes precedence over incompatible generic style defaults.

Independently inspected Experience source and styles, its scoped reveal and lift hooks, navigation/Prints entry seams, the online route, session reducer, camera/media adapters, preset registry, compositor, composition hook, pointer handlers, rendered stage controls, motion rules, and filename regressions. No production source, assets, runtime process, dependency, or existing review report was changed.

Opened a fresh isolated CUA in-app browser session at `http://localhost:3000/fotohvn#experience`. Used 1424 × 1104 for comparison to the chosen visual, then reset the viewport override at completion. The selected image and freshly captured Experience screenshot were emitted in the **same tool invocation** for direct visual comparison. The local online session used only task-generated material/hero image fixtures through the actual file chooser; no physical camera was activated and no user Downloads file was read.

## Repair 2 is verified

`download.ts` now reads `globalThis.crypto` safely, checks that `randomUUID` is callable, and invokes it with its receiver intact. Where that capability is absent, a local increasing serial supplies the eight-character hexadecimal suffix. Its serial appears inside the retained suffix, so successive results at one timestamp do not collapse to the same truncated prefix.

The helper is called once when `useComposition` accepts a completed blob. That stored result supplies both the filename and URL used by the final controls. An explicit result identity remains stable. Nothing in this change alters photograph pixels, frame geometry, filtering, or preview/download URL identity.

Independently reran `node scripts/online-photobooth-gesture.test.mjs` from `website/`: **13 passed, 0 failed, 0 skipped**. The actual source is exercised in environments with `crypto: {}` and without a crypto global; each case produces 256 unique names at a fixed timestamp. The available-UUID receiver and explicit stable identity regression also passes. The same script passes the pointer lifecycle and stale-placement regressions.

This independently closes the prior P2. It is a source-level missing-capability verification, not a claim of an insecure-origin browser test.

## Current conformance evidence

| Area | Evidence in this fresh review |
| --- | --- |
| Experience fidelity | Current composition closely matches Option 2's wide serif headline, italic emphasis, 5/2/5 material-image band, three aligned offers, hairline separation, and online invitation. The three images loaded from prepared local width candidates. No customer or generated-person picture is used in this section. Computed heading family resolves to Cormorant Garamond. |
| Website integration | The Experience invitation opened `/fotohvn/online`; the final page's Back to FOTOHVN link returned to `/fotohvn#experience`. The navigation and Prints contextual entry expose the same correct online destination. Existing physical-print/guest-album links and hero/lift hooks remain present. |
| Editorial online workspace | The live page uses the same paper palette, serif/sans contrast, restrained rules, and ebony controls. The frame/composition is dominant, with concise controls beside it. Four progress stages remain Layout, Photographs, Look, Download, with the appropriate current step and prerequisite disabling. Stage headings received focus on progression. |
| Starter collection | Source matches all four agreed layouts and their exact output/slot rectangles, four original frame treatments, and four deterministic photographic looks. Browser began with Long Strip/Ivory; independently selected Contact Sheet/Gallery and completed that combination. The Gallery mount and shallow footer remained complete and legible as artwork. The UI identifies these as original online starter designs. |
| Device-photo completion | Actual file chooser imported four distinct task fixtures. Four unique source URLs populated the photo rail and frame; the complete review enabled Look. No camera request was made. |
| Arrangement | Used Move from position 1 then selected occupied position 3 in the actual frame. Source order changed exactly from `[A,B,C,D]` to `[C,B,A,D]`; positions 2 and 4 remained identical. The change was announced and focus moved to position 3. Pointer and keyboard paths were independently browser-tested in the prior review; this review inspected their current source and reran the real-handler pointer regression suite. |
| Per-slot replacement | Selected position 2 and imported a replacement candidate. All committed source URLs remained unchanged while the candidate awaited acceptance. Accepting changed exactly source index 1, preserved the other three, announced the replacement, and focused position 2. Cancellation/preservation guards remain in the reducer and adapter source; prior full review covered cancellation in the browser. |
| Framed looks | Rapidly selected Warm Paper, Soft Fade, then Silver. The prior valid composition stayed visible while the new one prepared, and final-stage progression was disabled until ready. The completed Silver image showed monochrome photographs inside the unchanged warm Gallery frame. All four look choices remained available. |
| Completed PNG | Final stage displayed a complete **1800 × 1500** Contact Sheet/Gallery/Silver image without photo-position controls baked into the result. The image source and Download PNG href were exactly the same blob URL. Open Image to Save pointed to that same blob. The filename included layout, timestamp, and result suffix. |
| Motion and cleanup | Current source scopes Experience reveal to its own elements and preserves lift ownership. Online state arrival, image fade, countdown feedback, and paper reveal have reduced-motion overrides. Operation/revision guards reject obsolete work; camera tracks and obsolete source/output resources have cleanup paths. These are source conformance observations, not newly emulated motion-preference or hardware tests. |
| Containment and console | At this desktop conformance viewport there was no positive document overflow. The Experience and final online stage were visibly contained. Warning/error console reads returned `[]` during online completion and after returning to Experience. |

Final runtime identity observed in this recheck:

- Image and download: `blob:http://localhost:3000/191ed76a-637b-4ff3-8f17-ec61a1f0355b`.
- Natural dimensions: `1800 × 1500`.
- Filename: `fotohvn-contact-sheet-2026-09-14T09-30-15-231Z-6e64e264.png`.

These are inspected DOM result identifiers, not evidence of a file saved to disk.

## Screenshots

All files are in this handoff directory, serialized directly from CUA screenshot bytes:

- `recheck-experience-1424.png`: current Experience, compared beside `visual-2.png` in the same tool output.
- `recheck-layout-contact-gallery.png`: selected Contact Sheet/Gallery at Layout.
- `recheck-photographs.png`: completed arrangement after accepting a single-slot replacement.
- `recheck-look-gallery-silver.png`: ready, complete Silver/Gallery preview; replaces the earlier transient preparing-state capture under the same name.
- `recheck-download.png`: final Contact Sheet and output controls.

Fixtures: `public/images/experience-online/{curtain,lens,keepsake}-960w.webp` plus `public/images/hero/exterior-960w.webp`. The accepted replacement was the generated curtain fixture.

## Remaining evidence boundaries

- Root reports final integrated **39 passing tests**, full lint/typecheck, and production build after repair 2. Those remain parent-owned checks; this reviewer independently executed the 13-test gesture/filename suite only.
- Real camera permission, capture, interruption, mirror behavior, and physical touch-device interaction remain unverified. Source and adapter tests are useful but do not establish hardware success.
- Native on-disk download saving remains unverified because both earlier independent browser sessions could not expose a completed artifact. This review did not repeat those ineffective attempts or read unrelated files. The final composed image, dimensions, filename, and identical download URL are verified.
- No upload/storage service or photograph transmission/persistence call is present in the inspected booth implementation. Runtime network/storage inspection remains unavailable in the supported browser capabilities and is not claimed as passed.
- This gate does not replace responsive QA. Fresh desktop, tablet, and mobile reviewers must now inspect their viewport groups, including 320px, and report current findings before final synthesis. Any production repair after this report requires another fresh conformance gate.
