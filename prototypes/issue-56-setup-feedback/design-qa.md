# Design QA — setup readiness and asynchronous feedback prototype

## Comparison target

- Source visual truth: `reference/field-anchored-direction.png`
- Final implementation evidence: `audit/after/06-setup-final.png`
- Full-view combined evidence: `audit/after/qa-comparison-final.png`
- Focused field/readiness evidence: `audit/after/qa-comparison-focused.png`
- State: New Event setup, Event name ready, Camera missing, Printer set to Not printing, storage ready, save actions disabled with an inline reason
- Viewport: 1280 × 720 CSS px at device pixel ratio 1
- Source pixels: 1672 × 945, normalized to 1280 × 720 for comparison
- Implementation pixels: 1280 × 720

## Findings

No actionable P0, P1, or P2 differences remain.

- Fonts and typography: Segoe UI Variable/Segoe UI provides the intended Inter-like Windows product character. Weight, hierarchy, line height, muted copy, and compact operator labels match the reference closely.
- Spacing and layout rhythm: The seamless left readiness/form section and right live-preview section are preserved. Field rows, dividers, footer separation, modal proportions, and touch-target spacing are consistent and unclipped.
- Colors and visual tokens: The light neutral canvas, white surface, black actions, neutral borders, blue focus, restrained green readiness, amber action-needed state, and accessible dark red error treatment follow the reference and FotoHAVN audit.
- Image quality and asset fidelity: The selected mock contains no photographic or decorative raster assets. The live preview is an intentional empty camera surface, and UI icons use Microsoft Fluent icons rather than code-drawn substitutes.
- Copy and content: Readiness stays adjacent to its field; Printer is explicitly optional and neutral; disabled actions explain the missing Camera. The global `Opening Event setup…` banner from the generated source was intentionally removed per the user's selection feedback.

## Focused comparison

The field/readiness crop confirms matching row anatomy, label-to-control rhythm, Camera focus treatment, optional Printer treatment, storage status, preview framing, and capture-area label. The implementation adds only product-relevant empty-preview guidance and the separate prototype scenario control.

## Comparison history

1. Initial pass — blocked.
   - P2: the live-preview height collided visually with the footer feedback line.
   - P2: the bottom-centered prototype scenario control obscured footer actions.
   - Fixes: reduced the preview height and moved the prototype control to the unused left gutter; hide it on narrower viewports.
   - Post-fix evidence: `implementation-save-incomplete-v2.png` and `qa-comparison-save-incomplete.png`.
2. Focused pass — blocked.
   - P2: the selected Camera field did not carry the source's visible focus treatment.
   - P3: storage copy drifted from the selected reference.
   - Fixes: autofocus the blocking Camera field; align storage copy to `Local storage (C:)`, `120 GB free`, and `Plenty of space.`
   - Post-fix evidence: `implementation-save-incomplete-final.png` and `qa-comparison-final.png`.
3. Audit pass — blocked.
   - P1: failed-Start feedback collided with the content/footer boundary and described an unavailable Camera as selected/usable.
   - P1: Exit/Delete confirmations lacked initial focus, focus containment, Escape, background isolation, and working safe dismissal.
   - P1: the saved Event card omitted the persistent Start task and readiness summary.
   - P2: Delete success remained trapped in the confirmation.
   - Fixes: responsive recovery sizing and truthful Camera state; accessible dialog behavior; persistent Start/Edit/readiness; Save/Delete destination confirmations.
   - Post-fix evidence: `audit/after/01-saved-events.png` through `audit/after/06-setup-final.png`.
4. Final pass — passed.
   - No actionable P0/P1/P2 differences remain. The omitted top loading banner is an explicit user-directed change, not unresolved drift.

## Interaction and responsive verification

- Readiness: selecting a Camera enables both save actions.
- Busy/success: Save & Start immediately exposes `Checking Camera and storage…`, keeps the button width stable, then announces success.
- Error/recovery: selecting the unavailable Camera retains entered values, explains the failure, offers Try Again, and returns focus to Camera from Choose another Camera.
- Shared actions: Open, Save, Start, Exit, and Delete scenarios expose local idle/busy/success/error feedback without a global banner.
- Modal behavior: safe initial focus, cyclic Tab/Shift+Tab, Escape, inert/hidden background, dismissal, and focus restoration verified for Exit and Delete.
- Destinations: Save returns to Saved Events with `Event saved`; Delete removes the card and focuses `Event deleted`.
- Browser console: no errors.
- Responsive references: verified at 1024 × 768; 853 × 640 scaling stress; and 640 × 360 200%-zoom-equivalent stress. Controls remain reachable with vertical scrolling and no horizontal document overflow.

## Follow-up polish

- P3: replace the empty-preview Camera glyph with a real camera feed only when this pattern is promoted into production WinUI.

final result: passed
