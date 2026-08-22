# Desktop responsive design-QA handoff — pass 2

Date: 2026-08-22  
Scope: fresh desktop-only responsive QA; production files and `website/design-qa.md` were not modified  
Implementation: `http://localhost:3011/`  
Browser: fresh Codex in-app Browser session selected with `agent.browsers.get("iab")`  
Source authority: `website/.handoffs/gpt-taste-design-plan.md` version 1.1, with `website/DESIGN.md` where the plan does not override it  
result: blocked

## Gate conclusion

One actionable P2 remains: the transparent initial header renders Ebony brand/navigation text over a dark, varied hero photograph, so the brand and several navigation links fail visible contrast at both desktop widths. This contradicts the plan's conditional requirement that the header start transparent only where contrast permits and the WCAG-AA contract.

The plan-v1.1 hero repair itself does not regress desktop. The H1 remains 96px Cormorant Garamond 500, exactly two authored lines at both 1440x1000 and 1280x800, with intact punctuation, no CTA/print overlap, no clipping, and zero horizontal overflow. All other inspected P0/P1/P2 surfaces and primary interactions passed.

The tablet-reported third-stack-image concern does not reproduce on desktop: after the card lazy-loaded, `/images/experience-printed.png` was complete, visible, opacity `1`, and `835 x 1044` natural pixels.

## Comparison basis and normalization

The source truth is a written design and implementation contract, not a fixed-pixel mock. Source bitmap dimensions, source CSS size, and source density are therefore `n/a`; no pixel-perfect bitmap or same-frame source-image comparison is claimed. The rendered implementation was compared directly against the plan's explicit typography, grid, spacing, palette, imagery, copy, shapes, interaction, accessibility, and desktop-breakpoint decisions.

- Requested viewport `1440 x 1000`: the primary run reported `innerWidth=1440`, `innerHeight=1000`, `devicePixelRatio=1.25`, and layout client width `1425`; viewport captures are `1425 x 990` pixels. A later full-page capture after viewport reset/reapply is `1424 x 13638` pixels. The focused header clip is `1440 x 96` pixels.
- Requested viewport `1280 x 800`: reported `innerWidth=1280`, `innerHeight=800`, `devicePixelRatio~=1`, layout client width `1265`; the hero capture is `1265 x 791` pixels.
- Density was recorded, not normalized against a nonexistent source bitmap.
- The full-page Browser stitch proves complete page reach only. Sticky and GSAP-pinned regions can stretch or repeat in stitched captures, so all spatial judgments use focused viewport captures.

## Findings

- [P2] Transparent desktop header fails contrast over the hero photograph.
  - Fidelity surfaces: accessibility, colors/tokens, navigation affordance, above-the-fold resilience.
  - Location: initial `.header`, `.brand`, `.navigationLink`, and `.findAction` state in `website/src/components/SiteChrome.module.css`.
  - Evidence: `website/design-qa/responsive-qa-pass2-desktop-header-contrast-1440x1000.png`, `website/design-qa/responsive-qa-pass2-desktop-hero-1440x1000.png`, and `website/design-qa/responsive-qa-pass2-desktop-hero-1280x800.png`. The live initial header computes to a fully transparent background with `rgb(30, 26, 23)` text. A representative nearby wall sample in the 1280 capture is approximately `rgb(75, 59, 44)`, producing about `1.61:1` against Ebony. The 26px/600 brand requires at least 3:1 as large text, while the 12px navigation labels require 4.5:1. Visual inspection confirms the brand and some center/right links nearly disappear into the wall, rafters, and booth trim.
  - Impact: the brand and primary wayfinding are difficult to perceive before scroll, especially for low-vision users. The compact Ebony `RENT FOTOHVN` action remains visible, so this is a P2 rather than a core-use P1.
  - Fix: treat this hero crop as a case where transparency does not permit contrast. Give the initial header a uniform contrast-safe surface/scrim and foreground pairing across its full width—preferably the same restrained Off-white/hairline treatment used after scroll—or otherwise prove every brand/link segment reaches its applicable contrast threshold across both hero crops. Preserve the 72px height, 44px targets, destinations, and scrolled transition.

No additional actionable P0, P1, or P2 was found.

## Fresh browser evidence

Full-view/reach evidence:

- `website/design-qa/responsive-qa-pass2-desktop-full-1440x1000.png`
- `website/design-qa/responsive-qa-pass2-desktop-hero-1440x1000.png`
- `website/design-qa/responsive-qa-pass2-desktop-hero-1280x800.png`

Focused evidence:

- `website/design-qa/responsive-qa-pass2-desktop-header-contrast-1440x1000.png`
- `website/design-qa/responsive-qa-pass2-desktop-bento-hover-1440x1000.png`
- `website/design-qa/responsive-qa-pass2-desktop-accordion-keyboard-1440x1000.png`
- `website/design-qa/responsive-qa-pass2-desktop-marquee-paused-1440x1000.png`
- `website/design-qa/responsive-qa-pass2-desktop-gsap-gallery-1440x1000.png`
- `website/design-qa/responsive-qa-pass2-desktop-card-stack-1440x1000.png`
- `website/design-qa/responsive-qa-pass2-desktop-card-stack-third-1440x1000.png`
- `website/design-qa/responsive-qa-pass2-desktop-carousel-note2-1440x1000.png`
- `website/design-qa/responsive-qa-pass2-desktop-form-filled-1440x1000.png`
- `website/design-qa/responsive-qa-pass2-desktop-footer-1440x1000.png`

## Required fidelity surfaces

- Fonts and typography — passed. Cabinet Grotesk 400/700 and Cormorant Garamond 500 reported loaded. Body copy remains at least 16px. The hero is 96px/86.4px with `-3.36px` tracking and two one-line authored spans at both desktop widths. Section headings retain the serif/sans hierarchy without clipping or truncation.
- Spacing and layout rhythm — passed. Desktop gutters are 64px at 1280 and approximately 72px when the 1280px container caps at 1440. Major sections retain the planned generous rhythm. Bento gaps are 24px and the 7x2 + 5x1 + 5x1 layout closes with no void. No document-level collision or hidden persistent control was observed.
- Colors and visual tokens — blocked only by the initial-header finding. The remaining canvas uses Off-white/Warm Ivory/Cream Paper, restrained brass rules, one intentional Ebony action half, and an Ebony footer. No neon, fake gold, multicolor gradient, glow, or black-dominant page treatment appears.
- Image quality and asset fidelity — passed. Required local booth, guest, strip, detail, and printed-experience images render with stable frames, coherent warm crops, purposeful alt text, and no visible compression or masking defect. No placeholder, CSS/div art, emoji, handcrafted SVG substitute, unrelated stock, or fake testimonial avatar appears. The third stack image independently loaded at natural `835 x 1044`, complete/visible/opacity `1`.
- Copy and content — passed. Hero copy and truthful mall-booth/event-rental paths match plan v1.1. The only rendered occurrence of “vintage” is the approved descriptive hero phrase, not a selectable look claim. No unconfirmed public price, duration, package inclusion, offered look taxonomy, event-category claim, customer testimonial, rating, location, hours, response time, or service-area promise appears.
- Icons — passed. Icon use is intentionally minimal; carousel arrows remain secondary to explicit labels and the date input uses the browser-native glyph. There is no fake illustrated icon system.
- Shapes and surfaces — passed. Controls remain restrained rectangles with 4px radii, photography uses quiet frames, editorial slabs use no fake shadows or rotation, and there are no bubbly cards, pills, badges, decorative blobs, or generic component-factory artifacts.
- Accessibility — blocked only by header contrast. Skip navigation, semantic landmarks, labelled native controls, purposeful alt text, accordion ARIA relationships, carousel live status, form required/optional semantics, focus treatment, and repaired target sizes otherwise pass. The header brand is `121.26 x 44px`; footer Email is exactly `44 x 44px`; visible desktop nav/CTA/carousel/form targets are at least 44px in their interactive wrapper. Radio controls are 18px but their associated clickable labels are 52px high.
- Viewport resilience — passed apart from header contrast. `scrollWidth === clientWidth` at 1440 and 1280. The hero remains exactly two lines, hero actions stay 48px high, the print remains in-bounds, and CTA-to-print clearance is over 638px at 1280. No desktop overflow, text collision, or breakpoint regression was observed.
- AI-shortcut artifacts — passed. No prompt leakage, unsupported metric, generic rounded-card factory, decorative numeric marker, placeholder surface, custom drawn imagery, or fake social proof appears.

## Interaction and state verification

- Sticky navigation and anchors — internal `EXPERIENCE`, `THE BOOTH`, `PRINTS`, `FIND A BOOTH`, `RENT FOTOHVN`, and `ASK ABOUT YOUR DATE` targets worked. `FIND A BOOTH` settled 88.3px below the viewport top, clear of the 72px sticky header. After scroll, the header becomes `rgba(251, 248, 242, 0.97)` with a Hairline lower rule and readable Ebony text.
- Clickable bento media — pointer hover reached `matrix(1.05, 0, 0, 1.05, 0, 0)` after the specified `0.7s` transition inside `overflow:hidden`.
- Horizontal accordion — pointer activation expanded `TOGETHER`; ArrowRight moved focus to `PRINTED` while preserving the authoritative selected state. Native buttons expose `aria-expanded` and `aria-controls`; all triggers are full-height targets. The known in-app Browser synthetic Enter/Space limitation was not treated as a product defect.
- Infinite marquee — explicit pause changed the label to `PLAY MOTION`, set `aria-pressed=true`, and paused both 42s opposing tracks. The static reduced-motion row remains a source-verified fallback.
- GSAP pinned gallery — the heading entered a fixed pinned state; right stories remained in document flow. Fresh reading/exit geometry showed scale/opacity progression, exited overlay opacity, and pre-entry `scale(0.8)`/opacity `0.2` without horizontal translation.
- GSAP card stack — desktop cards remained sticky at 96/168/240px with z-index 1/2/3, no rotation, and no shadow; an entering card retained the planned scale `0.94` and 18%/129.6px vertical offset. The third card's local image loaded and rendered normally.
- Editorial-note carousel — manual Next changed Note 1 to Note 2, updated the polite status, and retained labelled 44px Previous/Next buttons. Source and observation show no autoplay.
- Form — empty submit invoked native required validation and focused the missing intent radio; invalid controls were intent, name, and email only. Selecting Event rental plus valid name/email removed all invalid states; date, city/venue, and notes remain optional. The `mailto:` action and email-app disclosure are truthful. QA did not submit a valid mailto form or open an external application.
- Reload/console — final 1440 reload completed with fonts loaded, H1 present, no hydration-failure text, no failed loaded image, zero horizontal overflow, and zero console warnings/errors.

## Comparison history

1. Responsive QA pass 1 reported desktop passed and later tablet/mobile blockers concerning the older hero-wrap interpretation and undersized persistent targets.
2. Plan v1.1 superseded the phone line-count interpretation and implementation 02 restored the 35.2px floor. Separate repairs brought the header brand and footer Email targets to at least 44px.
3. This fresh pass verifies those repairs did not regress desktop: two-line 96px hero, 44px persistent targets, working interactions, loaded third stack image, and clean console/reload all pass.
4. Fresh visual inspection newly identifies the initial transparent-header contrast failure at both desktop widths. No post-fix evidence exists yet, so this desktop gate remains blocked.

## Residual evidence limits

- The in-app Browser exposes no reduced-motion or zoom/text-scaling emulation control in this session. Reduced-motion behavior was source-inspected but not freshly rendered.
- Synthetic Enter/Space did not reliably dispatch native default activation in prior/current Browser checks; pointer activation, native semantics, directional keyboard handlers, focus movement, and visible focus were verified.
- Full-page stitching is not spatial evidence for sticky/pinned sections; focused captures are authoritative.

## Bounded repair task

Repair only the initial header contrast seam in `website/src/components/SiteChrome.module.css` (and `SiteChrome.tsx` only if a state hook is genuinely required). Preserve header height, target sizes, copy, destinations, layout, and scrolled behavior. Then rerun both independent gates and fresh desktop/tablet/mobile browser QA.

final result: blocked
