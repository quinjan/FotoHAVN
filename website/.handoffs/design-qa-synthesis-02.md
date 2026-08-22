# Responsive design-QA synthesis handoff — pass 2

Date: 2026-08-22  
Production files modified: none  
result: blocked

The fresh gpt-taste gate remains passed. Responsive QA is blocked by three reconciled roots:

1. P1 — the third `KEEP THE PHOTOGRAPH` image remains unrequested/blank in the reproducible 820px tablet state. Desktop, 1024, and 320/390 mobile load the same asset, so the scope is not broader than the 820 tablet loading path.
2. P2 — desktop and tablet report the same initial transparent-header contrast failure. Mobile remains readable and is excluded.
3. P2 — mobile stack headings spill beyond the 24px-inset measure and clip visible letters at 320/375/390 while document overflow equality is masked by the wrapper.

## Parallel-safe repair partition

- `website/src/components/SiteChrome.module.css` only: add a contrast-safe initial header surface/hairline from 768px upward; preserve mobile treatment, 72px height, targets, layout, destinations, focus, menu-open, and scrolled behavior.
- `website/src/components/MiddleExperience.tsx` only: make only the third stack image request/decode reliably at 768/820; preserve asset, alt text, CSS, GSAP, and unrelated image loading.
- `website/src/components/MiddleExperience.module.css` only: fit all three mobile stack headings inside their slab content at 320/375/390 without copy changes, overlap, transform scaling, or overflow clipping.

These files do not overlap, so repairs may run in parallel. Every repair needs its own result handoff. After integration, rerun a fresh gpt-taste verification followed by fresh desktop/tablet/mobile Browser QA and synthesis. This result does not authorize final website handoff.

Authoritative report: `website/design-qa.md`

result: blocked
