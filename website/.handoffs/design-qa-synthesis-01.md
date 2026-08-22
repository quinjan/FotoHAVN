# Responsive design-QA synthesis handoff

Date: 2026-08-22  
Production files modified: none  
result: blocked

The desktop QA passed. Tablet and mobile independently found the same two actionable P2 root causes: the hero becomes three visual lines before the plan's 320px exception, and the persistent header brand/footer Email links are smaller than 44 x 44 CSS px. Browser synthetic Enter/Space behavior, unavailable reduced-motion emulation, and full-page stitch distortion remain tool limitations rather than product defects.

## Repair partition

1. Hero wrapping — `website/src/components/UpperExperience.module.css` only. Preserve two visible lines at 375/390/768/820/1024/1280/1440, no more than three at 320, approved gutters, punctuation, and zero overflow/overlap.
2. Header brand target — `website/src/components/SiteChrome.module.css` only. Make the header brand at least 44 x 44 CSS px without changing content or architecture.
3. Footer Email target — `website/src/components/ClosingExperience.module.css` only. Make the footer Email target at least 44 x 44 CSS px without changing content or architecture.

The three seams do not overlap and may be repaired in parallel. After repair, run a fresh gpt-taste gate and fresh desktop/tablet/mobile Browser QA before a new synthesis.

Authoritative report: `website/design-qa.md`
