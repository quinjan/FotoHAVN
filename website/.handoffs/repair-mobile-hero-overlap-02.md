# Mobile hero overlap repair 02

result: passed

Date: 2026-08-22  
Scope: gpt-taste conformance pass 2 P1 only

## Change

The mobile `/images/printed-strips.png` hero figure remains a 120 by 180px bottom-right artistic overlap with a 24px right gutter. Its mobile bottom offset changed from `-48px` to `-96px`. This retains 84px of the framed print inside the hero and 96px beyond the hero edge while creating a deterministic 12px clearance below the CTA group.

No hero copy, action, asset, hierarchy, sizing, or component source changed.

## Browser-rendered evidence

Fresh same-origin renders were inspected with the Codex in-app Browser against `http://localhost:3000/`.

### 320 by 720 viewport

- Browser viewport: `innerWidth=320`, `innerHeight=720`; layout viewport: `305 by 720` because of the visible vertical scrollbar.
- Hero: `x=0`, `y=0`, `304.8 by 680px`.
- Mobile content gutters: left `24px`; right `24.2px`.
- `FIND A BOOTH`: `x=24`, `y=476`, `256.8 by 48px`.
- `RENT FOTOHVN`: `x=24`, `y=536`, `256.8 by 48px`; label `x=91.2375`, `y=552.2`, `122.325 by 15.2px`.
- Print figure: `x=160.8`, `y=596`, `120 by 180px`; computed `right=24px`, `bottom=-96px`.
- Print-to-secondary-CTA vertical clearance: exactly `12px` (`596 - 584`).
- Figure/button intersection: `0px` high and `0px2` area for both CTAs.
- Figure/label intersection: `0px` high and `0px2` area for both labels.
- Hit testing at the center and bottom-right interior of each CTA resolved to the correct anchor, including the area that had previously been covered.
- H1 rendered as exactly three lines: one line for `PHOTOGRAPHS,` and two for `DEVELOPED DIFFERENTLY.`; punctuation was visible.
- Exactly two hero actions remained present.
- `/images/printed-strips.png` remained present in the framed figure.
- `document.documentElement.scrollWidth === clientWidth === 305`; no horizontal overflow was introduced.

### 390 by 844 viewport

- Browser viewport: `innerWidth=390`, `innerHeight=844`; layout viewport: `375 by 844`.
- Hero: `375.2 by 742.7125px`.
- Mobile content gutters: left `24px`; right `23.8px`.
- `FIND A BOOTH`: `x=24`, `y=538.7125`, `327.2 by 48px`.
- `RENT FOTOHVN`: `x=24`, `y=598.7125`, `327.2 by 48px`; label `x=126.4375`, `y=614.9125`, `122.325 by 15.2px`.
- Print figure: `x=231.2`, `y=658.7125`, `120 by 180px`.
- Print-to-secondary-CTA vertical clearance: exactly `12px`.
- Figure intersections with both CTA boxes and both CTA labels: `0px2` area.
- H1 remained exactly three lines; exactly two actions and the printed-strip asset remained present.
- `document.documentElement.scrollWidth === clientWidth === 375`; no horizontal overflow was introduced.

The in-app visual captures showed the print frame below and to the right of the action group with both labels fully readable at both widths. The temporary Browser viewport override was reset and the temporary local tab was closed after inspection.

## Checks

- `npm run lint`: exit 0.
- `npx tsc --noEmit`: exit 0.
- `npm run build`: exit 0; Next.js 16.3.2 compiled and generated the static `/` route.
- `git diff --check -- website/src/components/UpperExperience.module.css`: no whitespace errors.

The development render still recorded the separately assigned hero LCP warning before the loading-strategy repair was integrated. This bounded CSS result does not claim to resolve or independently verify that P2 finding.

## Files changed

- Production: `website/src/components/UpperExperience.module.css` only, mobile `.heroPrint` offset at line 549.
- Handoff: `website/.handoffs/repair-mobile-hero-overlap-02.md`.

`website/src/components/UpperExperience.tsx` and all other production files were not edited by this repair agent. Existing unrelated worktree changes were preserved.

## Fresh verifier instructions

1. Start the current integrated website on `http://localhost:3000/` and use a fresh in-app Browser tab.
2. At `320 by 720`, measure both hero CTA boxes and their text ranges, then the hero print figure. Require a positive gap and zero-area intersection for every CTA and label.
3. Hit-test or activate both CTAs, including near each button's right edge, and confirm the print frame does not intercept either target.
4. Confirm the H1 is no more than three rendered lines, the hero exposes exactly two CTAs, the print figure still uses `/images/printed-strips.png`, both content gutters are 24px, and `scrollWidth === clientWidth`.
5. Repeat the geometry, hierarchy, and overflow checks at `390 by 844`.
6. Run the complete fresh gpt-taste conformance gate after all bounded P1/P2 repairs are integrated. Do not treat this handoff as an independent gate pass.
