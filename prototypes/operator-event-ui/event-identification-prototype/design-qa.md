# Design QA — Variant A grouped Event ID

## Comparison target

- Source visual truth: `qa/source-option-2-grouped-event-id.png` (selected Product Design option 2, 1383 × 1137 pixels)
- Normalized source: `qa/source-option-2-normalized.png` (1256 × 1032 pixels)
- Card implementation: `qa/implementation-option-2-cards-final.png` (1256 × 1032 pixels)
- Side-by-side evidence: `qa/comparison-option-2.png`
- Full-ID evidence:
  - `qa/implementation-option-2-edit-full-id.png`
  - `qa/implementation-option-2-start-full-id.png`
  - `qa/implementation-option-2-delete-full-id.png`
  - `qa/implementation-option-2-delete-full-id-narrow.png`
  - `qa/implementation-option-2-deleted-full-id-narrow.png`
- Viewport: 1256 × 1032 CSS pixels at DPR 1; the in-prototype canonical frame is 1280 × 720.
- State: Variant A, light theme, duplicate `Summer Social` Events.

The generated source was normalized to the browser capture size with high-quality bicubic scaling. The comparison judges the selected card hierarchy; the implementation intentionally preserves FotoHAVN's existing page width, card density, button sizing, and prototype controls instead of adopting incidental ImageGen drift.

## Findings

No actionable P0, P1, or P2 findings remain.

- Fonts and typography: Inter remains the UI font. The Event name stays primary; `EVENT ID` uses a quiet uppercase label; the grouped fingerprint uses a monospace face and the selected `XXXX · XXXX` rhythm. Full UUIDs use 11 px monospace text in the larger surfaces and can wrap at safe boundaries.
- Spacing and layout rhythm: the selected hierarchy is preserved as Event name → Event ID label → grouped fingerprint → Saved recency → actions. Existing FotoHAVN card geometry and whitespace remain stable.
- Colors and visual tokens: the existing light monochrome palette, neutral metadata colors, borders, and dark primary buttons are preserved. No decorative badge or new semantic color was introduced.
- Image and asset fidelity: the target contains no product imagery or custom visual assets; no placeholders, generated assets, or substitute icons were introduced into the implementation.
- Copy and content: creation dates are absent from Variant A. Cards show a grouped fingerprint derived from the same UUID displayed in full on edit, start, delete, deletion-progress, and deletion-result surfaces. `Saved …` recency is restored to its original position and wording.
- Responsive behavior: the narrow / 150% stress state preserves the grouped card ID and wraps the full UUID without clipping the delete dialog.
- Interaction checks: Edit opened from the duplicate Event card. Start and delete confirmations rendered the selected Event's complete UUID. Delete advanced through progress and result while retaining that UUID. Browser console reported no errors.

The full-view side-by-side evidence is sufficient for the card hierarchy because all identifier and recency text is readable at native resolution. Separate full-ID screenshots provide focused evidence for consequential surfaces and narrow wrapping.

## Comparison history

1. First implementation finding — P2: the full UUID used 10 px type, which underused the additional space available in edit and confirmation surfaces.
   - Fix: increased full UUID text to 11 px while preserving safe wrapping.
   - Post-fix evidence: the edit, start, delete, and narrow-delete screenshots listed above.

## Follow-up polish

- P3: production design must define how the eight-character fingerprint is derived and collision-checked; the prototype uses the final eight hexadecimal UUID characters grouped `XXXX · XXXX`.
- P3: verify contrast, Narrator pronunciation of the grouped and full IDs, selection/copy behavior, and real Windows 125%/150% scaling in the later WinUI implementation.

final result: passed
