# Issue 98 approved desktop flow - gpt-taste conformance

Date: 2026-08-27  
Viewport: 1440 x 900 CSS px at DPR 1  
Scope: Variant A desktop intro only; Variant B and the live homepage are regression surfaces.

<design_plan>
Python RNG execution: intentionally not applied. This is a scoped conformance review of a human-selected storyboard, so randomized hero, font, component, and motion choices would violate the approved direction and repository authority.
AIDA check: not applicable to the intro overlay. The existing server-rendered homepage retains its navigation, hero, experience, desire, and action sequence unchanged beneath the prototype.
Hero math verification: the live homepage hero remains the approved two-line `PHOTOGRAPHS, / DEVELOPED DIFFERENTLY.` composition. The intro adds no stamps, badges, tags, or competing display copy.
Bento density verification: not applicable. This flow contains no grid or card system.
Label sweep and button check: passed. Visible intro copy remains `FOTOHVN`, `SKIP INTRO`, `PRESS TO ENTER FOTOHVN`, and the development-only A/B switcher; all controls retain high-contrast text and visible focus treatment.
</design_plan>

## Conformance findings

- Motion is purposeful and spatially legible: the existing exterior advances into a left-biased threshold, settles on the physical left-wall assembly, holds the homepage inside that touchscreen, then expands the same captured page into the live homepage.
- The user-selected physical model remains dominant. The right/rear guest background never appears, the curtain gathers at the right, and the camera/screen/paired-lights/control order preserves the real interior reference plus the user's paired-light correction.
- No booth, curtain, camera, light, screen, or control is reconstructed as CSS, SVG, canvas, or placeholder art. The two new photographic stages are raster assets and the screen content is a browser capture of the real homepage.
- The transition is full-bleed after repair. The earlier scaled-stage matte edges were removed before this review.
- The interface avoids cheap meta labels, decorative badges, invisible button text, and horizontal overflow.
- GSAP was not introduced. A new dependency and scroll-trigger paradigm would be unrelated to this approved fixed-viewport, time-based throwaway intro and would change Variant B and the live page outside scope.

## Evidence

- `approved-flow-storyboard-browser-comparison.png`
- `approved-flow-left-wall-detail-comparison.png`
- `approved-flow-repair1-threshold-1440x900.png`
- `approved-flow-repair1-interior-1440x900.png`
- `approved-flow-final-live-1440x900.png`

Result: passed for the approved issue-98 desktop-only prototype scope.
