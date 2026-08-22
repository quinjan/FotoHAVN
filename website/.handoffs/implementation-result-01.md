# FOTOHVN implementation result 01

result: passed

## Scope completed

- Implemented the mandatory design plan in the production Next.js site without changing its deterministic selections or factual boundaries.
- Replaced the superseded production look selector, price/package claims, event-type claims, generic gallery/event sequence, and older inquiry path.
- Kept this phase implementation-only; the independent gpt-taste conformance and responsive design-QA gates are not self-certified here.

## Files changed

- website/package.json
- website/package-lock.json
- website/src/app/globals.css
- website/src/app/layout.tsx
- website/src/app/page.tsx
- website/src/components/SiteChrome.tsx
- website/src/components/SiteChrome.module.css
- website/src/components/UpperExperience.tsx
- website/src/components/UpperExperience.module.css
- website/src/components/MiddleExperience.tsx
- website/src/components/MiddleExperience.module.css
- website/src/components/ClosingExperience.tsx
- website/src/components/ClosingExperience.module.css

## Design-plan decisions implemented

- Cabinet Grotesk 400/500/700 loads from Fontshare's official API; Cormorant Garamond remains the display face and Manrope/system fonts remain fallbacks.
- Navigation is a 72px transparent-to-Off-white sticky split bar with the exact desktop links and both visitor-intent actions; mobile retains Escape close, outside-click close, and focus return.
- Artistic Asymmetry hero uses hero-booth.png, the fixed two-line headline/supporting copy, exactly two hero CTAs, directional copy veil, and the overlapping printed-strips.png print frame.
- Experience bento uses exactly three image-led cards with grid-auto-flow: dense and the recorded 12-column 7x2 + 5x1 + 5x1, 6-column 6x1 + 3x2 + 3x2, and 4-column 4x1 + 4x1 + 4x1 layouts.
- Horizontal Accordions use the exact ENCLOSED/TOGETHER/PRINTED selections, native buttons, aria-expanded, aria-controls, arrow/Home/End focus movement, and click/Enter/Space selection. Mobile becomes a vertical disclosure sequence.
- Infinite Marquee uses the approved truthful terms in opposing rows, visible pause/play control, hover/focus pause, one screen-reader sentence, and a static reduced-motion row.
- GSAP Scroll Pinning uses ScrollTrigger, useGSAP({ scope }), gsap.context, and gsap.matchMedia; the pin uses the exact start/end values and each media story uses the recorded scale/opacity entry and exit behavior.
- GSAP Card Stacking uses the exact three slabs, initial scale: 0.94 and yPercent: 18, increasing z-index, 72px header retention, and natural tablet/mobile/reduced-motion flow.
- The manual FOTOHVN editorial-note carousel uses overlapping portrait crops, exact approved statements, FOTOHVN attribution, visible previous/next controls, arrow-key operation, and aria-live="polite".
- Action area provides the exact truthful mall and event paths, the required mailto for current booth details, required Mall booth/Event rental intent plus required name/email, optional event date/city-or-venue/notes, the email-app disclosure, and START THE CONVERSATION.
- Footer remains the approved minimal Ebony footer with the existing social/email links and 2026 line.
- Primary/secondary contrast, 44px minimum controls, 2px/3px focus treatment, stable image aspect ratios, next/image responsive sizes, 24px mobile gutters including 320px, and reduced-motion fallbacks are present.

## Checks and exact outcomes

- npm install gsap @gsap/react — passed; installed gsap@3.15.0 and @gsap/react@2.1.2; audit reported 0 vulnerabilities.
- npx tsc --noEmit — passed with exit code 0 and no TypeScript diagnostics.
- npm run lint — passed with exit code 0 and no ESLint findings.
- npm run build — passed; Next.js 16.3.2 compiled successfully, completed TypeScript, generated 4/4 static pages, and emitted / as static content.
- Required-source assertion — required_plan_strings=19; missing=0.
- Banned-claim/label assertion — forbidden_strings=17; forbidden_hits=0 for the old price/duration, look names, event types, generic labels, and old inclusion/social-proof claims.
- git diff --check — passed; only the repository's line-ending warnings were printed.

## Deviations or conflicts

None.

## Instructions for the independent verification agents

1. From website, run npm run dev.
2. Verify all recorded plan decisions against production source and rendered output, including Cabinet Grotesk network loading, the exact AIDA section order, hero authored line behavior, bento density, both GSAP paradigms, accordion keyboard behavior, marquee pause/focus/reduced-motion behavior, carousel controls/live announcement, mobile menu behavior, anchors, and mailto form behavior.
3. At 320px independently check document.documentElement.scrollWidth === document.documentElement.clientWidth; the main wrapper intentionally follows the mandated overflow-x-hidden w-full max-w-full, so inspect element geometry rather than treating clipping as proof.
4. Run desktop/tablet/mobile screenshots, browser-console and hydration checks, reduced-motion checks, and the independent gpt-taste and Product Design gates before accepting the website.
