# Design QA: Healthy Guest Cycle

## Comparison target

- Visual truth: issue #20, approved Variant A (`design-qa/issue-20-variant-a-start-reference.png`, `design-qa/issue-20-variant-a-countdown-reference.png`, and `design-qa/issue-20-photo-strip-reference.png`).
- Implementation evidence: `design-qa/issue-28-start-implementation.jpg`, `design-qa/issue-28-countdown-implementation.jpg`, and `design-qa/issue-28-photo-strip-implementation.jpg`.
- Source and implementation viewport: 1280 × 720 px at 1× density. This is a native WinUI app, so CSS viewport normalization does not apply.
- States compared: Start, countdown with live mirrored Camera preview, and the ten-second Photo Strip preview.

## Findings

- No actionable P0/P1/P2 visual mismatches remain.
- Typography and hierarchy: pass — the uppercase Event label, oversized guest-facing headings, supporting copy, tracked status labels, and high-contrast countdown match Variant A.
- Spacing and layout: pass — the fixed header, centered Start composition, top-right Capture progress, primary content alignment, and Photo Strip preview follow the reference rhythm at the target viewport.
- Color and controls: pass — neutral canvas, white surfaces, black primary action, subtle borders, and visible 3 px keyboard focus styling preserve the reference palette and accessibility requirements.
- Camera state: pass — the guest preview is mirrored and labeled, the active Capture is explicit in text and four progress circles, and the countdown remains centered over the live image.
- Photo Strip state: pass — the 600 × 1800 lossless strip is shown only after decode, the full Event name is visible, the progress line is present, and the booth returns automatically after ten seconds plus the completion transition.
- Acceptance-driven differences: the live preview uses the required exact 3:2 guest frame rather than the wider issue #20 illustration; the final strip uses the required exact 2:6 output ratio rather than the wider illustrative mock. These are intentional and not defects.

## Evidence

- Full-view comparisons: each source/implementation pair was opened together at 1:1 density. Header, copy hierarchy, primary actions, Capture progress, and final-strip composition were compared across the entire 1280 × 720 surface.
- Focused regions: Start CTA foreground and pointer states; countdown overlay and progress circles; Photo Strip proportions, gutters, label, timer copy, and progress line.
- Primary interaction run: created and started a local Event, completed four real Camera Captures, observed canonical strip composition, watched the ten-second preview and 450 ms transition, and confirmed automatic return to Start.
- Persistence evidence: the exercised Guest Cycle produced exactly four canonical JPEGs, `photo-strip.png`, and a versioned completed `guest-cycle.json` with all four ordered Capture names and the Photo Strip reference.
- No unhandled native app errors were observed. Browser console checks are not applicable to this WinUI app.

## Comparison history

1. The first rendered Start comparison exposed a P1 contrast defect: the nested CTA content rendered black, and the WinUI pointer-over state replaced the black fill.
2. The CTA now pins its icon and text to white and explicitly preserves the black fill for pointer-over and pressed states.
3. Post-fix Start evidence shows the expected black/white action at rest and under pointer interaction.
4. Countdown and Photo Strip comparisons found no remaining actionable P0/P1/P2 differences.

## Follow-up polish

- P3/test gap: Operator Assistance recovery is covered by presentation and orchestration tests, but was not forced during the final physical-Camera visual run.

Previous report result: passed

---

# Design QA: Saved Events card redesign

## Comparison target

- Source visual truth: `docs/design-system/reference-states/review/saved-events-card-selected-reference.png`, the approved refinement of Product Design option 3.
- Implementation evidence: `docs/design-system/reference-states/targets/saved-events/busy--1280x720.png`.
- Full-view comparison: `docs/design-system/reference-states/review/saved-events-card-full-comparison.png`.
- Focused card comparison: `docs/design-system/reference-states/review/saved-events-card-focused-comparison.png`.
- Source dimensions: 1774 × 887 px at 1× density. Implementation dimensions and CSS viewport: 1280 × 720 px at 1× density.
- Normalization: the full source was resized proportionally to 1280 × 640 and stacked with the 1280 × 720 implementation. The focused source crop was resized to 1184 px wide and stacked with the 1184 × 257 implementation card row.
- State: first Event busy (`Opening Event…`); two Events idle.

## Findings

- No actionable P0/P1/P2 visual mismatches remain.
- Fonts and typography: pass — the cards load the repository’s Inter variable font; Event names, tracked Event ID labels, ID values, and muted saved-recency copy preserve the selected hierarchy without wrapping at Standard.
- Spacing and layout rhythm: pass — the cards retain the selected vertical structure, generous empty body, and bottom-right action placement. The implementation uses a 256 px card height instead of the taller ImageGen canvas proportion so six Events remain practical in the canonical 1280 × 720 operator layout; overflow is contained in the main operator content.
- Colors and visual tokens: pass — off-white canvas, white panels, near-black typography, subtle neutral borders, gray busy surface, and restrained dark-red Delete glyph follow the source and FotoHAVN tokens.
- Image quality and asset fidelity: pass — no raster artwork is required. Edit and Delete use the installed Segoe MDL2/Fluent icon font rather than custom SVG, CSS drawing, emoji, or placeholder art.
- Copy and content: pass — idle `Ready to start` copy and all visible Start buttons are absent. The busy card retains one loader with `Opening Event…`; Event names, IDs, and saved-recency values remain intact.
- Interaction and accessibility: pass — the full neutral card body is an independent Start button with an Event-specific accessible name. Edit and Delete remain separate 48 × 48 buttons with accessible names and tooltips. Busy Start, Edit, and Delete controls are disabled together.
- Responsive behavior: pass — the 12 Saved Events references cover Standard, Compact, scale-equivalent, and 640 × 360 Stress layouts without horizontal scrolling or clipped actions.

## Primary interaction checks

- Clicking the first idle card navigated to Start Event confirmation.
- Edit navigated to Edit Event without starting the Event.
- Delete navigated to Delete Event confirmation without starting the Event.
- New Event navigated to New Event setup.
- The idle screen contains zero `Ready to start` labels.
- The busy card exposes one `Opening Event…` message and disables Start, Edit, and Delete.
- Browser console warning/error check: none observed.

## Comparison history

1. First coded comparison found a P2 icon mismatch: the initial MDL2 code point rendered a settings gear instead of the selected pencil. It was replaced with the MDL2 Edit glyph (`E70F`).
2. First coded comparison also found a P2 density mismatch: 208 px cards compressed the selected whitespace and used the browser fallback font. Cards were increased to 256 px and scoped to the repository’s Inter variable font.
3. Busy icon buttons initially lost their boundaries against the disabled card surface. Their disabled border and surface were restored.
4. Post-fix full and focused comparisons show the approved hierarchy, bottom-right icon actions, empty idle lower-left area, and busy treatment with no remaining actionable P0/P1/P2 differences.

## Follow-up polish

- P3 intentional difference: the ImageGen source uses taller illustrative cards than the canonical six-Event desktop matrix. The implementation keeps the same design direction at a denser operator-friendly height.

final result: passed

---

# Design QA: Shared confirmation dialog standard

## Comparison target

- Source visual truth: `docs/design-system/reference-states/review/confirmation-dialog-selected-reference.png`, selected Product Design option 3.
- Browser-rendered implementation: `docs/design-system/reference-states/review/confirmation-dialog-implementation.png`.
- First-pass implementation evidence: `docs/design-system/reference-states/review/confirmation-dialog-first-pass.png`.
- Full-view comparison: `docs/design-system/reference-states/review/confirmation-dialog-full-comparison.png`.
- Focused modal comparison: `docs/design-system/reference-states/review/confirmation-dialog-focused-comparison.png`.
- Exit/Discard exception comparison: `docs/design-system/reference-states/review/confirmation-dialog-exit-discard-comparison.png`, stacking the selected standard with the two identity-free variants at a common 720 px modal width.
- Source dimensions: 1453 × 1082 px at 1× density. Implementation dimensions and CSS viewport: 1280 × 720 px at 1× density.
- Normalization: full views were proportionally resized to a common 960 px display width. Modal crops were proportionally resized to a common 720 px display width; neither artifact was stretched.
- Primary state: `confirmation.start-idle.standard`. Supporting states include Save, Discard, Exit, Delete, busy, failed/Retry, and the four registered responsive-risk viewports.

## Findings

- No actionable P0/P1/P2 visual mismatches remain.
- Fonts and typography: pass — FotoHAVN’s Inter stack reproduces the selected centered title, tracked Event labels, bold Event name, muted monospace ID, and balanced action labels without clipping.
- Spacing and layout rhythm: pass — the renderer follows the selected icon/title/divider/identity/divider/action-rail anatomy. The 500 px contract width keeps the dialog compact in the 1280 × 720 product surface while preserving the reference hierarchy.
- Colors and visual tokens: pass — neutral decisions use near-black iconography and Primary action styling. Exit, Delete, and Discard use the existing dark-red destructive token for both icon and confirming action. The white panel, gray scrim, borders, and focus ring remain token-aligned.
- Image quality and asset fidelity: pass — no raster artwork is required. The icons use layered Segoe MDL2/Fluent system glyphs, including a ring plus action-specific glyph, rather than custom SVG, CSS illustration, emoji, or placeholder art.
- Copy and content: pass — Start, Save, Delete, and retained-context Retry preserve Event name and full Event ID. Exit and Discard intentionally omit Event identity because the current-flow consequence already provides sufficient context. Start and Save avoid generic filler copy; destructive decisions add one specific consequence sentence. Confirmation eyebrows and bordered identity cards are absent.
- Interaction and accessibility: pass — each dialog exposes one level-1 heading, the consequence through `aria-describedby` when present, a hidden redundant icon, initial focus on the safe action, and 48 px controls. Busy states retain the leading loader and disable conflicting actions.
- Responsive behavior: pass — all 16 Confirmation references were recaptured. At 640 × 360 the long-name destructive busy state keeps 16 px margins, two 48 px actions, no page overflow, and no internal clipping.

## Primary interaction checks

- Start Event navigated to Guest Start.
- Save changes navigated to the approved success acknowledgement.
- Delete Retry navigated to the Delete busy state.
- Exit and Discard each rendered zero Event identity sections while retaining their specific consequence copy and both actions.
- Keep event active returned to Guest Start, and Discard changes returned to Saved Events.
- Browser console warning/error check: none observed.
- The evidence builder reported 103 frames, `complete: true`, and zero missing targets after recapturing the Confirmation family and embedded Exit confirmation.

## Comparison history

1. The first implementation pass had a P1 icon mismatch: the selected outline circle rendered as a solid black disk because the wrong system status-circle layer was used. Evidence: `docs/design-system/reference-states/review/confirmation-dialog-first-pass.png`.
2. The icon layer was replaced with the system ring glyph while retaining the action-specific center glyph.
3. Post-fix full and focused comparisons show the selected outline icon, centered heading, divider-based identity, and paired full-width action rail with no remaining actionable P0/P1/P2 differences.
4. Product refinement removed Event identity from Exit and Discard only. The combined comparison confirms that both variants preserve the selected icon, centered hierarchy, consequence copy, and paired action rail without leaving an empty divider or awkward vertical gap.

## Follow-up polish

- P3 intentional difference: the generated reference presents a taller, larger modal relative to its canvas. The renderer uses FotoHAVN’s 500 px maximum and tighter product spacing so confirmations remain practical at all required desktop sizes.
- P3 intentional difference: the renderer shows the required initial keyboard focus ring on the safe action; the generated reference depicts an unfocused pointer state.

final result: passed

---

# Design QA: Success and information acknowledgement dialog

## Comparison target

- Source visual truth: `docs/design-system/reference-states/review/success-dialog-selected-reference.png`, the selected Product Design option 3.
- Browser-rendered implementation: `docs/design-system/reference-states/review/success-dialog-implementation.png`.
- Full-view comparison: `docs/design-system/reference-states/review/success-dialog-full-comparison.png`.
- Focused modal comparison: `docs/design-system/reference-states/review/success-dialog-focused-comparison.png`.
- Source dimensions: 1535 × 1024 px at 1× density. Implementation dimensions and CSS viewport: 1280 × 720 px at 1× density.
- Normalization: full views were proportionally resized to a common 960 px display width. Modal crops were proportionally resized to a common 720 px display width; neither artifact was stretched.
- State: `confirmation.success-destination.standard`.

## Findings

- No actionable P0/P1/P2 visual mismatches remain.
- Fonts and typography: pass — the renderer uses FotoHAVN’s Inter stack, preserves the selected centered hierarchy, and keeps the title, one-sentence message, and action readable without wrapping.
- Spacing and layout rhythm: pass — the implementation follows the selected icon/title/message/full-width-action anatomy with balanced vertical spacing. The 440 px modal maximum follows the repository dialog contract; the larger generated canvas presentation was normalized for the focused comparison.
- Colors and visual tokens: pass — the white panel, near-black copy/action, gray scrim, and restrained semantic green use existing FotoHAVN tokens. The dialog adds no gradient or decorative color.
- Image quality and asset fidelity: pass — no raster artwork is required. The circular check is the installed Segoe MDL2 `Completed` icon, not custom SVG, CSS art, emoji, or a placeholder.
- Copy and content: pass — the dialog contains only `Event saved`, `Your changes have been saved.`, and `Continue`. Event name, Event ID, confirmation eyebrow, and Cancel are absent.
- Interaction and accessibility: pass — the dialog has one programmatic level-1 heading, an accessible description, one 48 px Primary action with initial focus, and a redundant status icon hidden from accessibility. Continue returns to Saved Events.
- Responsive behavior: pass — at 640 × 360 the dialog keeps 16 px margins, a 48 px full-width action, zero horizontal overflow, and no clipped content. Evidence: `docs/design-system/reference-states/review/success-dialog-stress-640x360.png`.

## Primary interaction checks

- The 1280 × 720 browser render contained zero Event identity panels and zero Cancel buttons.
- Initial focus entered on Continue.
- Clicking Continue navigated to `saved-events.card-idle`.
- Browser console warning/error check: none observed.
- The evidence builder reported 103 frames, `complete: true`, and zero missing targets.

## Comparison history

1. The selected visual established the centered completion icon, concise acknowledgement copy, and single full-width action.
2. The implementation translated that structure into FotoHAVN’s 440 px standard acknowledgement dialog and existing type, spacing, color, and icon tokens.
3. The first browser-rendered full and focused comparisons found no actionable P0/P1/P2 mismatch; no visual correction loop was required.

## Follow-up polish

- P3 intentional difference: the ImageGen source presents the modal at a larger relative canvas scale. The renderer uses the approved 440 px acknowledgement maximum so it remains consistent with FotoHAVN’s desktop dialog system.

final result: passed
