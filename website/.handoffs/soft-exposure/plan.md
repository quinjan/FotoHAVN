# Selected Soft Exposure implementation

**Superseded during implementation:** the user changed their selection to Editorial Lift. This draft and its preliminary evidence are history, not a completed or approved release. Continue from `../editorial-lift/plan.md`.

The user selected the named Soft Exposure storyboard (displayed option 3): `exec-df476dab-fd71-499f-8312-dd4676614f10.png`. The selected board is copied here as `approved-storyboard.png`.

## Scope and visual authority

Replace the hero's open/close-curtain feature with a transition into the existing Experience section. The curtain remains closed; no hero interior request, rendering or control. Preserve the separate 3D explorer, actual customer photographs, corrected sign geometry, palette, typography and subsequent sections. Reuse the already prepared exterior WebP and actual guest images; no missing raster assets, so no new generation or asset subagent is necessary. Keep website copy live, not a flattened storyboard.

## Motion contract

- On landscape-sized screens with sufficient room, overlap the existing hero and Experience content in a short sticky stage. Native scroll controls a cross-dissolve into the existing warm-paper surface. No scaling, paper lift, print expansion, curtain motion, bright flash or camera journey.
- After roughly half a viewport of scroll, Experience is fully visible and normal document scrolling resumes. Scrolling back naturally reverses the dissolve. No wheel interception, long pin or looping animation.
- EXPLORE THE EXPERIENCE is a real #experience anchor. Its enhanced action travels to the same destination; keyboard activation finishes at the Experience heading. Modified clicks retain normal link behavior.
- Narrow/portrait screens retain the existing readable stacked layout with a short native view-transition dissolve for the Explore action where supported; normal scrolling stays normal. Unsupported browsers use smooth section navigation. Reduced motion and no JavaScript use a direct/natural anchor handoff.
- Only one live copy of each section. Hide overlapped inactive controls from focus, but preserve keyboard access to Experience through the anchor and ordinary page navigation. Honor direct #experience loads, Back, reduced-motion changes and resize. Remove listeners/animations on unmount.

## Delivery

Existing Next.js application on codex/website-real-booth-redesign; no scaffold, dependency replacement or deployment. Read installed Next component/CSS guides. Use existing fonts and Phosphor arrows. Archive the prior QA before writing the new one. Browser-check closed/mid-dissolve/arrived states against the selected storyboard in combined comparisons; inspect desktop, 820/390/320, actual pointer/keyboard, deep links, reverse scroll, continuation and console. Run focused tests, lint, typecheck and build; independent review follows.

Technical reference for the optional narrow-screen dissolve: [MDN Document.startViewTransition](https://developer.mozilla.org/en-US/docs/Web/API/Document/startViewTransition). Feature-detect; it is progressive enhancement, not a prerequisite for navigation.
