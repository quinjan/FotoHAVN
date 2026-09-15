

## Online booth Template step — 2026-09-14

Scope: approved refinement of option 2; combined template selection in the existing online booth.

Source visual truth: `.handoffs/template-step-ideation-20260914/selected-design.png` (1488 x 1058).
Implementation: `http://localhost:3000/fotohavn/online`.
Final screenshot: `.handoffs/template-step-ideation-20260914/implementation-desktop-final.png` (1473 x 1047 browser capture, 1488 x 1058 requested viewport; capture excludes scrollbar and is slightly reduced by browser tooling). Compared at equivalent full-frame scale, not as a pixel-difference claim.
Mobile evidence: `.handoffs/template-step-ideation-20260914/implementation-mobile-selection.png` (305 x 686 captured content at 320 x 720 requested viewport). Also checked 390 x 844.
State: Template step, Butter Gingham selected, empty session. All original source photographs are replaced with uniform #E5E0D7 photo windows in the selector.

### Comparison history

- Initial desktop comparison: P2 heading was too small, progress/header spacing placed content too low, and body descriptions were too small. Increased heading size and description size, reduced top spacing and heading line height. Re-captured and compared the source and implementation together in the same browser tool result.
- Initial 320px comparison: P2 excessive wrapping in template descriptions. Reduced thumbnail column and row gaps to allocate more width to copy. Re-captured mobile rows: complete names, readable descriptions, radio controls contained, no horizontal overflow.
- Final full-view comparison: selected source and final screenshot were emitted together. No remaining actionable P0/P1/P2 findings. Focused mobile row capture checked the most constrained text/control area; desktop source and final full views were readable at native viewing size.

### Fidelity surfaces

- Typography: existing Cormorant Garamond and Manrope; exact approved heading `Make a little Moment` and subtitle `choose your template`; italic Moment. Original app header retained. Readable 16px desktop template descriptions and 14px narrow-screen descriptions.
- Layout: two desktop columns, left preview below heading, four radio rows right, selected summary and action beneath. One-column mobile adaptation. Selected preview and thumbnails use the identical blob URL for each template, verified in browser DOM.
- Colors: existing warm ivory/off-white/ebony/brass tokens. All photo windows use shared #E5E0D7.
- Assets: actual published yellow gingham and polka artwork with original guest windows blanked. Signature and Panorama are edge-to-edge compositions with the existing canvas lettering pipeline. Native 1:3 / 3:1 output proportions intentionally preserved rather than copying ImageGen's slightly distorted thumbnail dimensions. This is an expected source-fidelity correction.
- Copy: four template names/descriptions, Template step label, combined selection, Continue to photographs, and Change template links. No separate frame selector.

### Verification

- 30 focused tests passed: session photo retention, 3/4 slot switching, native template geometry, empty/captured composition, existing pointer and retake behavior, look confinement and downloads.
- TypeScript passed. Scoped ESLint passed. Production build passed. A final CSS-only button typography polish followed the build and was browser-verified.
- Browser: radio selection, Butter Gingham / Panorama Four / Polka Keepsake states, identical thumbnail/large-preview URL, 320px and 390px containment, three local fixture photos imported, Look step, downloadable 600 x 1800 PNG and Download PNG action exercised, return to blank Template selection.
- Browser warning/error log: empty.
- Physical camera permission/hardware not exercised. Download link and finished PNG preview verified; no OS Downloads-file inspection performed.
- No deployment performed. Existing unrelated working-tree changes preserved.

final result: passed
