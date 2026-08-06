# Design QA — Event identification in consequential flows

## Comparison target

- Source visual truth: `qa/implementation-option-2-start-full-id.png` (approved Event-ID prototype from **Choose the Event identification model**), 1256 × 1032 px.
- Canonical implementation: `qa/issue-55-a-start-1280x720.png`, 1280 × 720 px at a 1280 × 720 CSS viewport and device scale factor 1.
- Combined evidence: `qa/issue-55-source-vs-a-comparison.png`, source and implementation rendered together. The source was top-cropped to its 16:9 content region because its original browser capture is taller; geometry judgments therefore use the canonical implementation capture and the approved source only for hierarchy, typography, surfaces, and token fidelity.
- Responsive evidence: `qa/issue-55-a-edit-actual-1024x768-passed.png`, `qa/issue-55-c-delete-failed-150-final.png`, and `qa/issue-55-b-delete-200-final.png`.
- States checked: saved Event cards, Edit, Start confirmation, Starting, Could not start/Retry, permanent-delete confirmation, Deleting, Deletion incomplete/Retry, and Deletion complete.

## Findings

No actionable P0, P1, or P2 differences remain.

- Fonts and typography: Inter loads successfully before final capture; the approved eyebrow, heading, body, and monospace Event-ID hierarchy is preserved. Full UUID wrapping remains readable at 150% and 200%.
- Spacing and layout rhythm: the approved neutral dialog, 48 px actions, card grid, borders, radii, and low-elevation shell are preserved. The three variants intentionally differ only in consequential-flow identity hierarchy.
- Colors and visual tokens: the calm monochrome palette, neutral scrim, dark Primary action, red destructive action, and danger callout retain the approved visual semantics and usable contrast.
- Image quality and asset fidelity: this flow contains no photography or illustrative assets. The existing FotoHAVN mark and text-only Camera preview treatment are unchanged from the source prototype.
- Copy and content: all consequential states show the Event name and complete lowercase hyphenated UUID; cards retain the approved uppercase grouped suffix. Failure states explicitly preserve context and offer Retry.
- Interactions and accessibility: Start → Starting → Could not start → Retry was exercised in-browser. Arrow-key variant switching works, accessible dialog/status semantics are present, controls meet the 48 px minimum, and the browser console reported no warnings or errors.
- Responsive behavior: 1024 × 768 has no horizontal overflow (`scrollWidth = clientWidth = 1009` after scrollbar allocation). The 150% and 200% stress captures keep dialogs, actions, and the full UUID visible; intentional vertical scrolling remains available for the tall Edit surface.

## Comparison history

1. **P1 — scale simulation did not reach the overlay.** Initial 150%/200% captures scaled the background frame while leaving dialogs at canonical type size. Fixed by applying the selected viewport class to each scrim and constraining the scrim to the simulated booth width. Post-fix evidence: `qa/issue-55-c-delete-failed-150-final.png` and `qa/issue-55-b-delete-200-final.png`.
2. **P2 — 1024 × 768 produced a 15 px horizontal overflow when the vertical scrollbar appeared.** Fixed simulated frame and scrim widths to resolve against the containing block rather than `100vw`. Post-fix evidence: `qa/issue-55-a-edit-actual-1024x768-passed.png`; measured `scrollWidth = clientWidth = 1009`.

## Focused-region comparison

The complete lowercase UUID and its label were inspected in the dialog, Edit surface, failure callout context, and 200% delete confirmation. Separate crops were unnecessary because the relevant identity regions remain legible in the full-size saved captures and their bounding rectangles stayed inside their containers.

## Follow-up polish

- P3: after a direction is chosen, align its exact Figma spacing and semantic token names during the design-library ticket; this prototype deliberately stops short of Figma-ready pixel specification.

## Implementation checklist

- Choose A, B, C, or a specific combination.
- Record the chosen hierarchy and scaling behavior on the decision ticket.
- Carry only the decision into Figma; keep this code on the throwaway branch.

final result: passed
