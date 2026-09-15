# Editorial Lift — design QA

Date: 2026-09-10. Scope: selected hero-to-Experience transition, replacing the rejected curtain reveal. Soft Exposure was superseded during implementation. Prior curtain report: `.handoffs/soft-exposure/previous-design-qa.md`. Earlier full-site evidence remains historical, not a new whole-site certification.

final result: passed

## Source and combined comparison

Authority: `.handoffs/editorial-lift/plan.md` and `approved-storyboard.png` (displayed option 1). Evidence below is in that folder.

- One three-frame motion storyboard, not three designs. Existing approved hero and Experience endpoints remain authoritative: `.handoffs/hero-scroll-ideation/current-hero.png` and `current-experience.png`.
- Desktop: 1440 × 900 CSS pixels, DPR 1, loaded fonts/images, light palette. Capture: 1425 × 891 pixels due to scrollbar/capture resampling.
- `comparison-rest.png`, `comparison-mid.png`, `comparison-arrived.png`: source LEFT / implementation RIGHT, combined and opened for assessment. Source mini-frames differ in aspect from the viewport; both use contain/letterboxing, without stretching or a pixel-identical claim. Script: `scripts/compare-editorial-lift.mjs`.
- Final production smoke: `production-desktop-rest.png`, `production-desktop-mid.png`, `production-desktop-arrived.png`. Temporary production port 3002; user preview remains port 3000.
- Midpoint native scrollY 270, progress 0.545: opaque paper covers the lower hero; first destination headline appears. Arrival scrollY 495, progress 1; Experience top 72.83px beneath header.

## Required fidelity surfaces

| Surface | Assessment |
| --- | --- |
| Typography | Existing Cormorant Garamond / Manrope retained. Hero hierarchy unchanged; “The world can wait.” resolves before the remaining existing three-line headline. |
| Spacing/layout | Closed hero subtly recedes while one opaque Experience section rises. Existing endpoint composition, guest-image placement and downstream order retained. Portrait layouts use natural flow. |
| Colors/tokens | Existing warm ivory, cream paper, ebony and walnut. No exposure flash or translucent dissolve. Existing accessible secondary ink over cloth retained. |
| Images/assets | Same generated exterior with corrected raised sign and no wood wings. Curtain remains closed; no hero interior request. Original guest photos remain in Experience. Separate 3D explorer unchanged. |
| Copy/content | Existing headline, description and rental action preserved. Secondary real anchor: EXPLORE THE EXPERIENCE. Destination is live semantic content, not a flattened storyboard. |

No outstanding actionable visual P0/P1/P2 difference in parent-reviewed paired evidence. Generated miniature lettering/photo drift is not copied into the real interface.

## Iterations and independent gates

1. User switched to Editorial Lift during incomplete Soft Exposure work. Removed dissolve code; retained its folder as superseded history.
2. Initial sticky sizing released the stage too early. Explicit root height replaced padding-based travel; full reload and actual midpoint/arrival captures verified the repair.
3. Refined title timing so the first Experience line precedes the later lines/body. Repeated combined midpoint comparison.
4. Initial Standards P2: responsive mode changes while reading later sections could jump back to Experience. Guarded correction by pre-layout sequence visibility; added regression.
5. Initial Spec P2: late font completion could resume interrupted navigation. Font readiness now measures only, without hash navigation; added deferred-font regression.
6. Fresh Standards and Spec reviewers each reported zero actionable findings after repairs. Standards was source-based; Spec used source plus supplied combined images, not a separate live browser session. Parent then completed responsive/production browser checks. See `review.md`.

## Browser checks

- Desktop native wheel rest → midpoint → arrival, plus reverse scroll. No wheel interception.
- Native Tab/Enter reaches Explore, arrival focuses `experience-heading`. Header Experience uses same lift. Browser Back returns to #top, scrollY 0.
- Prints navigation reaches #prints. Resizing there to phone width does not teleport back.
- Production 390 × 844: complete facade and readable actions. Explore focuses heading at scrollY 602; section top 84.92px. Scroll/client widths both 375px.
- Production 320 × 720: complete facade/sign, Explore fits. Native Explore focuses heading at scrollY 587. Scroll/client widths both 305px.
- Production 820 × 1180: natural flow, full sign/editorial composition. Scroll/client widths both 805px. `tablet-820-rest.png`.
- Production desktop repeats progress 0 / 0.545 / 1 and focused arrival. Direct Experience entry checked separately.
- Production warning/error console query: empty list.

## Automated checks and limits

- Final production build, ESLint, TypeScript and git whitespace checks passed.
- `npm run test:hero`: 10/10. Motion bounds, responsive/reduced flow, reverse/inert layers, focus/history, interruption/modified clicks, deep links, cleanup, navigation/resize cancellation, offscreen changes and late fonts.
- `npm run test:booth`: existing 4/4 regressions passed. No new full-feature 3D visual review claimed.
- Reduced-motion/no-JavaScript paths checked through fixtures and CSS/semantic source. Native OS reduced-motion and JavaScript-disabled browser runs were unavailable in exposed controls and are NOT runtime passes.
- Cancellation edge cases are fixture-tested; native activation, Back, reverse scroll and downstream resize separately browser-tested. No new network-failure proxy run for simplified exterior fallback.
- No form submission, external message, dependency upgrade, commit, push or deployment. Unrelated work preserved. npm email-config and Git line-ending notices are environment notices, not application failures. Prior dependency advisories remain a separate pre-deployment concern.
