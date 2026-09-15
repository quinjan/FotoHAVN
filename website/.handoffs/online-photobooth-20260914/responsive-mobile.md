# FOTOHVN online booth: fresh mobile QA

final result: passed

Reviewer: fresh agent `online_mobile_qa`, 2026-09-14. Reviewed the final source after the passed conformance recheck. No actionable P0, P1, or P2 findings were found in the assigned mobile matrix. No production files, assets, runtime process, dependencies, or other reviewers' reports were changed.

## Scope and method

Read `responsive-qa-input.md`, `selected-design.md`, the final conformance recheck, and local `DESIGN.md`, `tokens.json`, `variables.css`, and `theme.css`. Applied the repository's fresh mobile QA workflow. Used a new isolated CUA in-app browser at the canonical `/fotohvn#experience` and `/fotohvn/online` URLs.

Tested **320 × 720 first**, then **390 × 844**. Added the parent's requested **375 × 812** containment/readability spot check without repeating the complete transaction matrix. Screenshots were captured through natural scrolling, saved directly from CUA bytes, and inspected. Option 2 (`visual-2.png`) and the current 320px Experience screenshot were emitted together in one tool invocation for comparison. The approved narrow-screen reflow retains each story as a readable image/copy sequence.

The only imported fixtures were the task-generated curtain, lens, keepsake, and exterior `960w.webp` files identified in the QA input. No camera was activated and no user Downloads file was accessed. The viewport override was reset at completion.

## Results by viewport

| Viewport | Observed result |
| --- | --- |
| 320 × 720 | Experience headline wraps naturally, with italic emphasis intact; all three image/story pairs and the invitation remain visible through scrolling. The online CTA is 257 × 58 CSS px within 24px gutters. The mobile menu's Online Booth link wraps cleanly and opens the correct route. The booth reflows into one column, stage navigation into two columns, and layout/frame choices into readable two-column groups. Completed all four stages with Long Strip / Archive / Silver. |
| 390 × 844 | Experience headline resolves into two lines; all three material images and captions remain clear, followed by the complete invitation. Online stage navigation fits in four columns. Revisited Layout, selected Pair / Walnut, continued through Photographs and Look, and completed a Warm Paper result. The two extra photographs were retained visibly in the tray. Final controls remain reachable without horizontal scrolling. |
| 375 × 812 | Spot-checked the populated Pair result and its final actions, then the Experience heading and invitation. Text wraps naturally, image proportions remain deliberate, and all visible actions remain contained. Experience headings, paragraphs, and CTA had no positive internal text overflow. |

Document `scrollWidth` equaled `clientWidth` throughout the checked stages: **305 / 305** at the 320px viewport, **375 / 375** at 390px, and **360 / 360** at 375px. The 15px difference from the configured viewport is the in-app browser's vertical scrollbar, not horizontal overflow. Experience text and images remain inside 24px side gutters.

All three current Experience images were independently verified loaded through image-element properties (`complete: true`, positive natural dimensions) at 390px, in addition to visible screenshot evidence at 320px. The image subjects are curtain/light/material studies, with no people. Computed online heading and interface fonts resolve to Cormorant Garamond and Manrope.

## Interaction evidence

| Interaction | Actual browser result |
| --- | --- |
| Entry and return | Opened the mobile menu at 320px and followed Online Booth to `/fotohvn/online`. The final page's Back to FOTOHVN link returned to `/fotohvn#experience`. The Experience invitation also exposed the correct online destination. |
| Device-photo import | The real multiple-file chooser imported four unique task fixture sources. Four filled positions appeared and Look became available. |
| Single-position retake cancellation | Selected photograph 2, chose Retake, and imported a candidate. All four committed source URLs stayed identical while the replacement awaited approval. Keep Original preserved all four URLs and returned focus to `photograph-position-1`. |
| Single-position acceptance | Chose a new device replacement for photograph 2 and accepted it. The source-change vector was `[false, true, false, false]`; the other photographs stayed unchanged. Focus returned to position 2. |
| Tap placement | Selected Move for photograph 2, then tapped rail position 3. Sources changed exactly from `[A,B,C,D]` to `[A,C,B,D]`. Positions 1 and 4 were preserved; the new position was announced and focus moved to `photograph-position-2`. |
| Keyboard focus | Tab from framed position 3 focused position 4 with a visible ebony **3px solid outline / 4px offset**. Stage progression focused the appropriate new heading. |
| Framed photographic looks | At 320px, selected Silver and inspected the actual monochrome photographs inside the unchanged warm Archive frame. At 390px, selected Warm Paper and inspected both photos inside the complete Walnut frame. Full previews and all four look choices are available through scrolling. |
| Layout preservation | At 390px, changing from Long Strip to Pair preserved the first two active sources and exposed the remaining two as retained photos, with readable placement controls. |
| Final result identity | At 320px the loaded PNG's natural dimensions were **900 × 2700**. At 390px they were **900 × 1500**. In each case, the final image `src` exactly equaled the Download PNG `href`; Open Image to Save exposed the same blob. |
| Final editing/safeguard | Make Another exposed its fresh-strip confirmation. Keep This One preserved the existing result URL. Edit Your Strip returned to Look with the same frame/look and photographs. Download, Open Image to Save, Edit, and Make Another remain readable and reachable at all checked phone widths. |
| Console | Warning/error console reads returned `[]` after completing the online session and after the final Experience checks. |

Result identifiers inspected in this session:

- Long Strip / Archive / Silver: `blob:http://localhost:3000/9772c335-7c7d-46a0-bd62-2b85e3282c9b`, natural dimensions 900 × 2700, download filename `fotohvn-long-strip-2026-09-14T09-37-01-505Z-898fa996.png`.
- Pair / Walnut / Warm Paper: `blob:http://localhost:3000/c943f17e-3a28-4de9-be46-68e1d10f3930`, natural dimensions 900 × 1500.

These identifiers are DOM evidence of the rendered result and link target, not evidence of a file saved on disk.

## Screenshot index

All screenshots are in this handoff directory:

- 320px Experience: `mobile-320-experience-heading.png`, `mobile-320-experience-stories.png`, `mobile-320-experience-invitation.png`, `mobile-320-menu.png`.
- 320px booth selection: `mobile-320-layout-top.png`, `mobile-320-layout-choices.png`, `mobile-320-frame-controls.png`.
- 320px photographs: `mobile-320-photographs-empty.png`, `mobile-320-photographs-filled.png`, `mobile-320-retake-candidate.png`, `mobile-320-rearranged-frame.png`.
- 320px looks/result: `mobile-320-look-controls.png`, `mobile-320-framed-look.png`, `mobile-320-final-print.png`, `mobile-320-final-actions.png`. The final-print screenshot captures the complete long print; neighboring stage screenshots intentionally capture scroll positions rather than forcing the whole page into one image.
- 390px booth: `mobile-390-layout-choices.png`, `mobile-390-photographs-top.png`, `mobile-390-retained-tray.png`, `mobile-390-framed-look.png`, `mobile-390-final-actions.png`.
- 390px Experience: `mobile-390-experience-heading.png`, `mobile-390-experience-stories.png`, `mobile-390-experience-invitation.png`.
- 375px spot check: `mobile-375-final-actions.png`, `mobile-375-experience-heading.png`, `mobile-375-experience-invitation.png`.

## Evidence limits

- These are responsive viewport and click/tap-style checks in IAB, not tests on a physical touch device or a separate mobile browser engine.
- Physical camera permission/capture, interruption, and mirroring were not exercised. This review used the complete device-photo fallback.
- Native on-disk saving was intentionally not repeated because earlier independent reviews already established the IAB artifact-access limitation. Image loading, natural dimensions, link identity, and reachable save controls were verified.
- Runtime network/storage instrumentation and reduced-motion emulation were unavailable through the exposed browser capabilities. This review does not claim those runtime checks; the existing conformance report owns the corresponding source inspection.
- Root owns lint, typecheck, build, and the 39-test integrated suite. This bounded visual/interaction review did not rerun those checks.

The approved mobile matrix is complete with no required repair. Final synthesis should preserve these evidence limits alongside the passing result.
