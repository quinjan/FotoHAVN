# Design QA — field-only setup feedback

## Comparison target

- Source visual truth: `reference/annotation-field-warning.png`, `reference/annotation-camera-error.png`, and `reference/annotation-remove-footer-notice.png`
- Browser-rendered error state: `audit/after/11-field-only-error.png`
- Browser-rendered valid state: `audit/after/12-field-only-ready.png`
- Browser-rendered insufficient-storage state: `audit/after/14-storage-path-1gb.png`
- Compact-footer evidence: `audit/after/15-tight-footer.png`
- Expanded-form evidence: `audit/after/16-expanded-form-scroll.png` and `audit/after/17-expanded-form-scrolled.png`
- Viewport: 1256 × 1032 CSS px at device scale factor 1
- Source pixels: 515 × 35, 510 × 42, and 597 × 98 focused annotation crops
- Implementation pixels: 1256 × 1032 for both states
- Density normalization: all artifacts were inspected at native pixel density. The focused annotation crops were compared against their corresponding visible regions in the full implementation captures without stretching.

## Findings

No actionable P0, P1, or P2 differences remain.

- Fonts and typography: field messages retain the compact 11 px FotoHAVN treatment, with amber for required input and red for Camera failure. The silent valid state contains no redundant success typography.
- Spacing and layout rhythm: messages appear immediately beneath the affected control within its row. Their appearance does not break the left/right split or collide with the footer; the footer now contains actions only.
- Colors and visual tokens: warning, error, focus, disabled, and neutral tokens remain consistent with the existing prototype and the supplied crops.
- Image quality and asset fidelity: the references contain only native UI. Warning, error, spinner, and Camera icons come from the existing Microsoft Fluent icon library; no substitute drawings or raster placeholders were introduced.
- Copy and content: `Enter an Event name to continue.` and `Camera unavailable. Choose another Camera or try again.` match the approved field-level language. The former setup-wide failure/retry notice is completely removed.

## Full-view comparison evidence

The error capture shows both approved field messages in context, disabled save actions, an unavailable preview, and no setup-wide footer notice. The valid capture shows no passive checks, status labels, helper copy, or readiness banner anywhere beside the form or above its actions.

The insufficient-storage capture isolates Storage as the only failing requirement: Event name and Camera are valid, the Event destination is identified as `C:\Program Files\FotoHAVN\Events`, capacity reads `480 MB free`, recovery guidance specifies that at least 1 GB must be freed, and both save actions remain disabled.

## Focused comparison evidence

The supplied crops are already focused references. Their icon scale, single-line copy, color, and placement beneath the input/select align with the corresponding regions in `audit/after/11-field-only-error.png`.

## Comparison history

1. Previous compact-status pass — blocked by new product direction.
   - P2: passive checks remained inside and beside valid controls.
   - P2: setup-wide readiness and failure feedback still appeared above footer actions.
   - Fixes: removed all passive status checks and footer feedback; retained only field-level checking, warning, and error messages.
2. Camera-evaluation pass — passed.
   - Camera selection now enters a brief `Checking Camera…` state, then clears silently when valid or leaves the approved error beneath Camera when unavailable.
   - Post-fix evidence: `audit/after/11-field-only-error.png` and `audit/after/12-field-only-ready.png`.

## Interaction and browser verification

- Empty Event name immediately shows its field warning.
- Selecting a Camera immediately disables save actions and shows `Checking Camera…` beneath Camera.
- FJ Camera 01 clears the checking message after validation and enables both save actions.
- FJ Camera 02 leaves the Camera error beneath the select and keeps both save actions disabled.
- Start progress appears only inside the initiating button as `Starting Event…`; no footer notice is created.
- The Storage scenario shows field-level recovery guidance, keeps the Storage/footer overlap at 0 px, and disables both save actions while all other required fields remain valid.
- The final Storage state exposes `C:\Program Files\FotoHAVN\Events`, enforces the approved 1 GB minimum, and preserves a 0 px Storage/footer overlap after the additional path line.
- The footer is 80 px high and remains fully inside the modal. With Event-name, Camera, and Storage guidance visible together, the content region becomes independently scrollable (`418 px` client height, `436 px` scroll height) while the footer stays fixed and unobscured.
- Browser console errors: none.
- Build and Sites worker tests: passed.

## Follow-up polish

No P3 follow-up is required for this scoped annotation.

final result: passed
