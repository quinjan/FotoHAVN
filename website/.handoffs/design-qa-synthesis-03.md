# Responsive design-QA synthesis handoff — pass 3

Date: 2026-08-22  
Production files modified: none  
result: blocked

Fresh gpt-taste pass 8 and desktop responsive pass 3 are passed. Tablet and mobile reveal one shared-file P2 responsive stack-typography task:

- At 768/820, `.stackCopy h3` extends into the image column and the loaded media hides final letters. The defect ends before the 1024 boundary and does not reproduce in mobile's vertical cards.
- At 320/375/390, `.stackHeader h2` breaks the 24px content gutter. The repaired mobile card H3 remains fully in-bounds.

## Single repair instruction

Assign one fresh repair agent exclusive ownership of `website/src/components/MiddleExperience.module.css`.

1. Add a 768–1023px `.stackCopy h3` size/measure rule so every heading ends before `stackMedia.left` at 768 and 820.
2. Add a max-767 `.stackHeader h2` size/measure rule so every line stays inside the 24px mobile content boundary at 320, 375, and 390.
3. Preserve all copy, gutters, grid/padding, images, motion, the passing mobile card-H3 repair, the 1024 boundary, and 1280/1440 desktop typography.
4. Verify text ranges and descendant overflow at all affected widths.

These edits share one CSS file and must not run in parallel. After repair, rerun a fresh gpt-taste verification, then fresh desktop/tablet/mobile Browser QA and synthesis. This result does not authorize final website handoff.

Authoritative report: `website/design-qa.md`

result: blocked
