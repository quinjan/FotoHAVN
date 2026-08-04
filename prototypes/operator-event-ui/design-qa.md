# Design QA — Operator Event UI

**Comparison target**

- Source visual truth: `reference-attio-event-setup.png`, superseded where noted by the user's browser annotations
- Implementation: `implementation-variant-a-annotated.png`
- Combined comparison evidence: `design-qa-comparison.png`
- Viewport and CSS size: 1280 × 720 at device scale factor 1
- Source pixels: 1672 × 941 (same 16:9 composition, normalized to 1280 × 720 for comparison)
- Implementation pixels: 1280 × 720
- State: existing Event setup, Camera Tuning expanded, Logitech BRIO ready, fixed DNP DS-RX1HS ready

**Findings**

- No actionable P0, P1, or P2 differences remain. Variant A preserves the reference hierarchy: restrained monochrome shell, compact Inter typography, centered flat modal, two-column Event/printer identity row, progressive Camera section, mirrored preview, tight tuning rows, and separated footer. The annotated truth promotes black `Save & Start Event`, makes `Save & Close` white and outlined, reduces the Camera Tuning trigger to an accessible gear, and replaces the fixed printer status block with an eligible-printer selector.
- [P3] The generated live-preview subject is not pixel-identical to the mock, but it matches the required subject, crop, warm wedding setting, mirrored camera treatment, image quality, and art direction. This is acceptable prototype media.
- [P3] Native range-input thumb geometry varies slightly by browser. The blue accent, track density, values, and alignment match the source closely enough for the layout decision.

**Required fidelity surfaces**

- Fonts and typography: passed. Inter 400/500/600/700 is bundled locally; hierarchy, small labels, letter spacing, line height, and wrapping match the Attio-inspired reference.
- Spacing and layout rhythm: passed. Modal position, proportions, grid tracks, padding, borders, 6–10px radii, nearly shadowless internal surfaces, and footer separation match after iteration.
- Colors and visual tokens: passed. Cool-white surfaces, gray hairlines, exact black/white primary action colors, blue tuning controls, green Camera readiness state, and muted overlay align with the reference and annotations.
- Image quality and asset fidelity: passed. A dedicated generated 16:10 wedding camera asset is used with a mirrored crop; no placeholder or CSS-drawn imagery is present.
- Copy and content: passed. Visible setup copy follows the reference and issue 18 terminology. Lifecycle confirmations use the exact issue 18 decision copy where specified.
- Icons: passed. Controls use one consistent Phosphor icon family; no handcrafted SVG or text-glyph substitutes are present.
- Accessibility and interaction states: passed for prototype scope. Labels are associated with controls, focus rings are visible, modal actions are explicit, tuning is expandable, validation is visible, and the primary path is keyboard reachable.

**Comparison history**

1. First browser capture used an 860 × 608 modal. The same-state combined comparison showed a P2 major-region proportion mismatch: the modal occupied too much of the 1280px frame compared with the selected source.
2. Fix: Variant A was narrowed to 660px and its identity gap, preview/tuning tracks, and control grid were rebalanced without changing behavior.
3. Post-fix evidence: the current `implementation-variant-a.png` and `design-qa-comparison.png` show matching modal proportions, content hierarchy, section boundaries, and footer placement. No P0/P1/P2 findings remain.
4. Annotation pass: the primary-action hierarchy was reversed, Camera Tuning became icon-only while retaining its accessible name, and the printer became a required eligible-only dropdown with no setup readiness badge. The selected-Camera readiness line was also removed and Camera Tuning sliders changed from blue to black. `implementation-variant-a-annotated.png` confirms the revised state at 1280 × 720 with no layout regression.
5. Deletion pass: confirmed deletion now shows a blocking progress dialog while its Event card remains visible, then removes the card and shows `Event deleted` with `Done` as its only action. Evidence is captured in `implementation-deleting.png` and `implementation-deleted.png`.
6. Incomplete-deletion pass: deleting the seeded `Year-End Party` fixture fails after progress, opens the issue 18 recovery dialog, and leaves a quarantined Event card whose only action is `Retry Deletion`. Repeated retries return to the same state without suggesting restoration, and minimal recovery metadata preserves quarantine across reload. Evidence is captured in `implementation-deletion-failed.png` and `implementation-deletion-quarantined.png`.

**Primary interactions tested**

- Closed Event setup to Saved Events using an explicit control.
- Opened New Event and observed required Event name and Camera validation.
- Selected an eligible Camera and saved the draft.
- Required an eligible Printer and validated the empty-printer state.
- Opened the saved Event Start confirmation.
- Completed Camera, printer, and storage preflight and reached guest Start.
- Opened Exit Event only from guest Start, between Guest Cycles.
- Browser console checked: no application errors.

Focused-region comparison was not required after the final full-view combined image because the typography, icons, field labels, tuning values, and footer controls are legible at original resolution in both halves.

**Follow-up polish**

- Evaluate physical touch-target comfort on the real 15.6-inch booth display before promoting the winning layout to production code.

final result: passed
