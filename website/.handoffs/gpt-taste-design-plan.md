# FOTOHVN gpt-taste implementation design plan

Date: 2026-08-22  
Plan version: 1.1  
Status: implementation authority for the current orchestration loop  
Scope: the public-facing single-page FOTOHVN website

<design_plan>

## 1. Deterministic Python RNG execution

The normalized design-planning directive is 1,394 characters. The following was executed with Python's `random.Random(1394)` against the ordered option lists in the complete `gpt-taste` skill:

```text
prompt_chars=1394; seed=1394
hero=Artistic Asymmetry; font=Cabinet Grotesk
components=[Feedback/Testimonial Carousel, Horizontal Accordions, Infinite Marquee]; gsap=[Scroll Pinning (GSAP Split), Card Stacking]
```

These selections are mandatory. The implementation must not substitute a different hero, font, component architecture, or GSAP paradigm without a new design-revision plan.

### Revision history

| Version | Date | Decision |
|---|---|---|
| 1.0 | 2026-08-22 | Initial gpt-taste implementation authority. |
| 1.1 | 2026-08-22 | Restores the `2.2rem` hero floor and permits the authored second span to wrap below 768px, so phone layouts may use two or three visual lines while tablet/desktop remain exactly two. This supersedes the earlier responsive-QA interpretation that required exactly two visual lines at 375px and 390px. |

## 2. Source authority and conflict resolution

For this loop, use this precedence:

1. This `gpt-taste-design-plan.md`, which records the user's requested synthesis and is the implementation authority for this loop.
2. Confirmed facts and explicit non-fabrication boundaries in `docs/research/photohavn-branding-website-benchmark.md` dated 2026-08-22.
3. FOTOHVN's brand identity, palette, core hero/story copy, accessibility rules, and token primitives in `website/DESIGN.md`, `website/tokens.json`, `website/variables.css`, and `website/theme.css`, except for the explicit overrides below.
4. `CONTEXT.md` for domain language: Photo Strip is the narrow keepsake; Print Sheet is the submitted printer composition; Guest Cycle is the guest-facing journey.
5. Current implementation and assets as reusable implementation evidence, not as authority that a business claim is confirmed.
6. The Refero/Amplemarket source only for structural restraint. Do not import its palette, gradients, illustrations, or product-marketing language.

The dated benchmark is strategy input, not an approved identity or package specification. Nevertheless, its explicit list of unconfirmed facts is the newest evidence about truth status and therefore overrides older production copy wherever the old site presents those details as confirmed. `website/DESIGN.md` remains the visual brand contract where it does not conflict with truth or the direct gpt-taste request.

Plan version 1.1 is the authority for hero wrapping. The earlier `website/design-qa.md`, `website/.handoffs/design-qa-synthesis-01.md`, and `website/.handoffs/repair-responsive-hero-wrap-03.md` remain historical evidence, but their requirement for exactly two visible hero lines at 375px/390px is superseded. A line-count pass must not be purchased by lowering the headline below this plan's type floor or collapsing its optical hierarchy.

### Explicit overrides of the older website contract/current site

- Remove the public `₱8,500`, `3 HOURS`, and the seven-item inclusion list. Price, duration, meaning of unlimited, digital copies, attendant, custom template, setup, and teardown are unconfirmed in the dated brief.
- Remove offered `CLASSIC`, `VINTAGE`, `MONOCHROME`, and `FOTOHVN SIGNATURE` look claims. Color/filter choices and photographic treatments are unconfirmed. Existing look images may be used only as unlabeled editorial portrait imagery, never as proof of a selectable service.
- Do not claim weddings, debuts, birthdays, corporate events, or private celebrations are currently accepted service categories. The confirmed statement is only mall placement plus event rental.
- Replace the older page order with the AIDA sequence below. Preserve the approved hero headline, concise brand tone, story idea, image-led character, one-off-white/warm-ivory system, and minimal footer.
- Use selected Cabinet Grotesk for body/interface type instead of Manrope. Retain Cormorant Garamond for the editorial hero and major headings; the selected gpt-taste font is the sans member of the final stack because the skill's options contain no serif. Load Cabinet Grotesk 400/500/700 through Fontshare's official API and retain Manrope/System UI as fallbacks. Do not copy proprietary font files into the repository.
- Permit one pausable Infinite Marquee and the two selected GSAP scroll paradigms despite the older ban on looping ornamental motion and strong scroll choreography. Keep them calm, content-relevant, desktop-bounded, and completely disabled under reduced motion.
- On clickable media only, use the gpt-taste hover treatment `scale(1.05)` over 700ms inside an `overflow: hidden` frame. This intentionally supersedes the older `1.015` cap. Passive media must not imply clickability.
- Remove numeric section markers such as `01`, `02`, and generic labels such as `OUR STORY`, `ABOUT US`, or `SECTION 01`. Meaningful labels such as `MALL BOOTH`, `EVENT RENTAL`, and `PHYSICAL PRINTS` are allowed.
- Do not use Picsum or unrelated stock imagery because approved local purpose-made assets exist. No image may be described as a real customer, real event, testimonial subject, or completed installation unless provenance is separately confirmed.

No edit to `website/DESIGN.md` is required in this planning stage; this file explicitly owns the loop-specific overrides.

## 3. Final visual direction and typography

The website remains light-led, warm, tactile, and photography-first. It should feel like an editorial photography studio with a vintage enclosed booth, never a nightclub, party supplier, wedding template, or faux-analog scrapbook.

- Display/major headings: Cormorant Garamond 500, fallback Times New Roman/Georgia.
- Body/interface/labels: Cabinet Grotesk 400; 500 or 700 for controls and concise emphasis; fallback Manrope, Inter, Segoe UI, sans-serif.
- Hero display: `clamp(2.2rem, 7.5vw, 6rem)`, line-height `0.88–0.94`, tracking about `-0.035em`. The `2.2rem` / 35.2px floor is mandatory at every viewport and must not be overridden by a smaller phone-only clamp.
- Section display: `clamp(2.75rem, 5vw, 4.5rem)` desktop, with a tested small-phone floor that fits 320px without clipping.
- Body: 16px minimum, line-height 1.6–1.65, 45–65 character measure.
- Labels/buttons: 12–13px, 700, uppercase, `0.12em–0.18em` tracking.
- Keep 70–80% of surfaces in Off-white `#FBF8F2`, Warm Ivory `#F3EBDD`, Cream Paper `#E8DDCE`, or photography. Use Dark Walnut `#2D211B` for at most one content band and Ebony `#1E1A17` for the footer/highest-emphasis actions. Brass `#9A7A4F` is a thin rule/selected-state accent, never small body copy.
- Ambient treatments may use a subtle warm radial wash, restrained paper grain, or dark directional photo veil. No multicolor gradient, fake gold, glow, neon, heavy film damage, or black-dominant cinematic treatment.

## 4. AIDA page architecture

### Navigation

A 72px sticky minimal split navigation starts transparent over the hero where contrast permits and becomes Off-white with a hairline rule after scroll. FOTOHVN is left. Desktop links are `EXPERIENCE`, `THE BOOTH`, and `PRINTS`; `FIND A BOOTH` remains a visible text action and `RENT FOTOHVN` is the compact Ebony primary action. Mobile uses a `MENU`/`CLOSE` button and exposes both visitor intentions with equal reach. Preserve skip navigation, semantic landmarks, Escape-to-close, outside-click close, focus return, and 44px minimum touch targets.

### Attention — Artistic Asymmetry hero

- Use the selected **Artistic Asymmetry** architecture.
- Base image: `/images/hero-booth.png`, full-width, 86–90svh, booth weighted to the right with its genuine left-side negative space preserved.
- Copy is left/lower-left on the image's quiet wall area.
- H1 copy is fixed: `PHOTOGRAPHS,` / `DEVELOPED DIFFERENTLY.`
- Supporting copy is fixed: `An enclosed vintage photobooth experience for celebrations worth remembering.`
- Primary CTA: `FIND A BOOTH` → `#find-a-booth`.
- Secondary CTA: `RENT FOTOHVN` → `#rent-fotohavn`.
- The artistic overlapping image is `/images/printed-strips.png` in a narrow print-like frame at the bottom-right edge of the copy/image composition. It is a real content image, not a stamp, icon, badge, pill tag, or statistic.
- Apply a directional warm-dark veil only behind copy. Keep the booth luminous.
- H1 uses an ultra-wide `max-inline-size: 72rem` (Tailwind equivalent `max-w-6xl`) and `width: 100%`. Its two authored block spans remain intact and each span may wrap naturally. Below 768px, the resulting H1 may occupy two or three visual lines; three lines at 320px, 375px, and 390px are expected and approved. From 768px upward it must render as exactly two visual lines. It must never exceed three lines or clip punctuation.
- Exactly two high-contrast CTAs. No hero stats, raw data, floating badges, tags, or extra icon row.

### Interest — Experience bento

Use three large, image-led cards with `grid-auto-flow: dense` / Tailwind `grid-flow-dense`:

1. `A LITTLE ROOM FOR REAL MOMENTS` — `/images/experience-enclosed.png`; explains only the confirmed/private enclosure value.
2. `FIND A BOOTH` — `/images/hero-booth.png`; states only that mall use is pay-per-use and routes to current-location contact.
3. `RENT FOTOHVN` — `/images/printed-strips.png`; states only that event rental is billed by the hour with unlimited prints and routes to date inquiry. Do not define “unlimited” until confirmed.

Do not add icons, badges, card shadows, or more cards.

### Interest — Horizontal Accordions

Implement the selected **Horizontal Accordions** architecture as three vertical media slices titled `ENCLOSED`, `TOGETHER`, and `PRINTED`, using `/images/experience-enclosed.png`, `/images/candid-guests.png`, and `/images/printed-strips.png`. One slice is expanded by default; hover may preview, but click/Enter/Space is the authoritative selection. Each trigger is a native button with `aria-expanded` and `aria-controls`; arrow keys move between triggers. Focus must expose the same information as hover. On mobile, render a stable vertical accordion with no horizontal compression.

Copy may describe privacy, sharing a moment, and receiving a physical keepsake. It must not state capture count, print count, timing, retakes, digital delivery, or format selection behavior.

### Interest-to-Desire bridge — Infinite Marquee

Implement the selected **Infinite Marquee** as two opposing typography rows using only truthful brand language: `ENCLOSED`, `PRINTED`, `PRIVATE MOMENT`, `PHYSICAL KEEPSAKE`, `MALL BOOTH`, `EVENT RENTAL`, `PHOTO STRIP`, and `FOTOHVN`. No partner logos or “trusted by” claim. Include a visible `PAUSE MOTION` / `PLAY MOTION` control; pause on hover and keyboard focus. The repeated copy is `aria-hidden`; provide one static screen-reader sentence. Under reduced motion, show one static wrapped row and hide the motion control.

### Desire — GSAP Scroll Pinning split gallery

Use the selected **Scroll Pinning (GSAP Split)** paradigm for `THE FOTOHVN EXPERIENCE`:

- Left 4 desktop columns: sticky/pinned Cormorant heading plus the concise line `Step inside together. Leave with something real.`
- Right 8 columns: four editorial media stories scrolling upward: booth (`hero-booth.png`), guests (`candid-guests.png`), print (`printed-strips.png`), detail (`booth-detail.png`).
- At desktop `min-width: 1024px` and no reduced motion, register ScrollTrigger and pin the left title with `start: "top top+=96"`, `end: "bottom bottom-=96"`, `pinSpacing: false`; keep the right column in natural document flow.
- Each right-side image uses a scrubbed entry/exit timeline: `scale: 0.8`, `opacity: 0.2` before entry → `scale: 1`, `opacity: 1` in the reading zone → `opacity: 0.2` and a restrained dark overlay after exit. Do not translate content off-screen horizontally.
- Tablet/mobile: no pin. Heading precedes the natural single-column gallery and all images remain visible.

### Desire — GSAP Card Stacking

Use the selected **Card Stacking** paradigm for three full-width editorial slabs, not rounded pricing cards:

1. `STEP INSIDE` — private enclosure.
2. `BE YOURSELVES` — room for a shared moment.
3. `KEEP THE PHOTOGRAPH` — physical Photo Strip or print.

Desktop/no-reduced-motion: cards start at `scale: 0.94`, `yPercent: 18`, and stack from the bottom with increasing z-index while the section scrubs. Each card retains at least 72px of the previous card header so the relationship is understandable. Do not rotate cards or add fake shadows. Mobile/tablet and reduced motion: render the same three cards in a normal gap-separated vertical list with no overlap.

### Desire — Feedback-carousel architecture without fabricated testimonials

Implement the selected **Feedback/Testimonial Carousel** visual architecture—overlapping portrait crops beside a minimalist quotation and subtle previous/next arrows—but title it `NOTES FROM INSIDE THE BOOTH`. Its three slides use approved editorial brand statements, clearly attributed to `FOTOHVN`, not to customers:

- `A little room for photographs, laughter, and moments you'll want to keep.`
- `Step inside, draw the curtain, and take a little time to laugh, experiment, and make something together.`
- `A private, tactile photography experience with something physical to keep.`

Use local portrait/detail assets. Do not invent customer names, ratings, reviews, client marks, or event outcomes. Manual controls are required; no autoplay. Announce the current slide with `aria-live="polite"`; arrow keys operate the carousel; controls are native buttons with visible labels.

### Action — two truthful contact paths

Use a high-contrast Warm Ivory/Ebony split headed `CHOOSE HOW YOU WANT TO BEGIN.`

- `FIND A BOOTH` (`id="find-a-booth"`): say that current mall location, hours, price, payment methods, and session details must be confirmed; link to `mailto:hello@fotohavn.ph?subject=Current%20FOTOHVN%20booth`. Do not publish a location or price placeholder as fact.
- `RENT FOTOHVN` (`id="rent-fotohavn"`): state only `Event rental is offered by the hour with unlimited prints.` Ask for event date and city/venue; do not promise availability, service area, response time, staffing, setup, customization, or included print count.
- Preserve the current accessible inquiry form/mailto behavior, but add a required intent field (`Mall booth` or `Event rental`). Name and email remain required. Event date, city/venue, and notes may be optional because not every inquiry is an event. Keep the truthful note that submission opens the visitor's email app.
- Final CTA label: `START THE CONVERSATION`.

Footer remains minimal on Ebony: `FOTOHVN`, `PHOTOGRAPHS, DEVELOPED DIFFERENTLY.`, the existing Instagram/Facebook/email links, and `© 2026 FOTOHVN`. Do not add unverified address, hours, phone, response time, or legal claims.

## 5. Grid and spacing calculations

### Global grids

- Desktop: 12 columns, 24px gaps, `max-width: 1280px`, 64px outer gutter until the max container is reached. At a 1280px content width: `(1280 - 11×24) / 12 = 84.6667px` per column.
- Tablet: 6 columns, 24px gaps, 48px outer gutters. At 1024px viewport the content width is `1024 - 96 = 928px`; `(928 - 5×24) / 6 = 134.6667px` per column.
- Mobile: 4 columns, 16px gaps, 24px outer gutters. At 390px viewport the content width is `342px`; `(342 - 3×16) / 4 = 73.5px`. At 320px the content width is `272px`; `(272 - 48) / 4 = 56px`.

### Responsive hero acceptance math and implementation instruction

The fixed copy, 24px phone gutters, 48px tablet gutters, 72rem H1 maximum, and typography clamp produce this contract:

| Viewport | Clamp result | Nominal H1 width | Required visual-line result |
|---:|---:|---:|---|
| 320px | `max(35.2px, 24px) = 35.2px` | `320 - 48 = 272px` | two or three; three expected |
| 375px | `max(35.2px, 28.125px) = 35.2px` | `375 - 48 = 327px` | two or three; three expected |
| 390px | `max(35.2px, 29.25px) = 35.2px` | `390 - 48 = 342px` | two or three; three expected |
| 768px | `57.6px` | `768 - 96 = 672px` | exactly two |
| 820px | `61.5px` | `820 - 96 = 724px` | exactly two |
| 1024px | `76.8px` | `1024 - 96 = 928px` | exactly two |
| 1280px | capped at `96px` | `1280 - 128 = 1152px` | exactly two |
| 1440px | capped at `96px` | H1 cap `1152px` | exactly two |

Browser scrollbars may reduce the measured content width by their reserved width; evaluate line count against rendered CSS geometry without shrinking type. At 768–819px only, `letter-spacing: -0.04em` is an approved optical adjustment because it keeps the 57.6px tablet display at two lines without materially changing the type character; all other widths retain approximately `-0.035em`.

The next implementation agent must modify only the responsive `.heroHeading` seam in `website/src/components/UpperExperience.module.css`: remove the phone `font-size: clamp(1.7rem, 7.2vw, 3.45rem)` override so the base `clamp(2.2rem, 7.5vw, 6rem)` applies; remove the now-redundant `max-width: 360px` `font-size: 2.2rem` override; preserve `max-inline-size: 100%` below 768px; and retain the 768–819px `-0.04em` tracking adjustment subject to fresh browser confirmation. Do not alter copy, gutters, image crop, hero height, CTA count or geometry, print geometry, or component structure. Do not use `white-space: nowrap`, transform scaling, clipping, narrower gutters, altered copy, or tighter phone tracking to force two lines.

### Bento density proof

- Desktop grid is 12 columns × 2 rows = 24 cells. Card A spans 7×2 = 14 cells. Card B spans 5×1 = 5 cells. Card C spans 5×1 = 5 cells. `14 + 5 + 5 = 24`; no empty cell or corner.
- Tablet grid is 6 columns × 3 rows = 18 cells. Card A spans 6×1 = 6 cells. Cards B and C each span 3×2 = 6 cells. `6 + 6 + 6 = 18`; no empty cell.
- Mobile grid is 4 columns × 3 rows = 12 cells. Each of the three cards spans 4×1 = 4 cells. `4 + 4 + 4 = 12`; no empty cell.
- Apply `grid-auto-flow: dense` at every breakpoint even though the arithmetic already closes the grid. Do not use absolute positioning to fake density.

### Section rhythm

- Hero: 86–90svh, minimum 680px mobile and 720px desktop where viewport height allows.
- Major section padding: `clamp(80px, 10vw, 144px)`; 80px at mobile, 120px around tablet, 144px wide desktop.
- Chapter gaps inside major sections: 48–80px desktop, 32–48px mobile.
- Card/media gaps: 24px desktop/tablet; 16px mobile.
- No major section may be cramped below 80px vertical padding. No arbitrary margin values outside the approved 4px spacing scale.

## 6. Assets and treatment

- `/images/hero-booth.png`: hero and mall-booth card.
- `/images/experience-enclosed.png`: horizontal accordion/privacy and bento.
- `/images/candid-guests.png`: together accordion, pinned gallery, carousel.
- `/images/printed-strips.png`: hero overlap, printed accordion, pinned gallery.
- `/images/booth-detail.png`: pinned gallery and story/carousel detail.
- `/images/experience-printed.png`: alternate print crop where repetition is visually obvious.
- `/images/experience-distinctive.png` and `/images/look-*.png`: optional unlabeled editorial imagery only. Do not label them as offered photographic looks or real-event proof.

Use `next/image`, correct intrinsic sizing/fill containers, responsive `sizes`, meaningful alt text, and stable aspect ratios. Keep warm natural color. A subtle `contrast(1.05)` or `opacity(.94)` is acceptable; do not force grayscale/mix-blend filters that obscure delivered output or skin tone. No new remote image dependency is required.

## 7. Interaction, GSAP, and implementation architecture

- Add `gsap` and `@gsap/react`; register `ScrollTrigger` once in client code. Use `useGSAP({ scope })`, `gsap.context`, and `gsap.matchMedia()` so every trigger is reverted on unmount/breakpoint change.
- Keep semantic content visible before JS initializes. GSAP enhances layout; it must not own visibility or reading order.
- Wrap the page in `<main className="overflow-x-hidden w-full max-w-full">` as required by gpt-taste. Independently verify `document.documentElement.scrollWidth === document.documentElement.clientWidth` at 320px; overflow hiding must not mask a sizing defect.
- Clickable media uses an overflow-hidden frame and `scale(1.05)` over 700ms/ease-out on hover and focus-within. Controls use 150–240ms feedback. Buttons remain legible: Off-white text on Ebony/Dark Walnut; Ebony text/border on light surfaces.
- Do not use autoplay carousel, cursor effects, bouncing, random parallax, horizontal off-screen animation, decorative stamps, or uncontrolled animation loops.
- Use native buttons/links/forms, semantic headings/landmarks/lists, 2px focus ring with 3px offset, 44px minimum targets, and WCAG AA contrast.
- The two GSAP paradigms are limited to their named sections. Do not sprinkle additional entrance animations across every element.

## 8. Responsive and reduced-motion commitments

- Break split/pinned layouts below 1024px for GSAP safety; break visual two-column layouts below 768px.
- Tablet keeps 48px gutters and large image priority. Mobile keeps 24px gutters, including 320px.
- Hero uses two authored spans. Below 768px, allow natural wrapping into two or three visual lines and preserve the 35.2px floor; 320px, 375px, and 390px are explicitly allowed to use three. At 768px and above, require exactly two visual lines. Do not use `white-space: nowrap` on the long second span below 768px.
- Horizontal accordion becomes a vertical disclosure list on mobile.
- Bento reflows according to the exact density math above.
- Pinned gallery and stack become natural document flow below their motion breakpoint.
- Carousel remains manual and single-slide; arrows remain reachable without horizontal document scrolling.
- At `prefers-reduced-motion: reduce`: kill/revert every ScrollTrigger, remove transforms, show final opacity/scale states immediately, make the marquee static, keep the carousel manual with near-immediate crossfade, and preserve all content/functionality.

## 9. Label, contrast, ban, and preflight sweep

- No `SECTION 01`, `QUESTION 05`, `ABOUT US`, generic `OUR STORY`, or decorative numeric markers.
- No hero stamp, badge, tag, stats, or more than two CTAs.
- No fabricated price, duration, package inclusion, testimonial, rating, client logo, service area, location, hours, response time, event type, customization, attendant, setup, download, capture count, copy count, print time, or filter/look claim.
- No emoji, confetti, balloons, neon, pastel taxonomy, fake gold, metallic gradient, icon card grid, social-feed gallery, dense pricing card, or novelty “filter” language.
- No empty bento cells; `grid-flow-dense` is mandatory.
- Every primary button has Off-white-on-Ebony/Dark Walnut contrast. Every secondary action on a light surface has Ebony text/border. Never use Muted Brass for small body text.
- Hero H1 uses `max-w-6xl`/72rem and `width: 100%`, preserving exactly two tablet/desktop lines and no more than three phone lines without reducing the display below 35.2px.
- The implementation agent must run a literal text sweep for banned labels/claims before handoff.

## 10. Acceptance commitments for the implementation agent

Implementation is complete only when:

- all deterministic selections and every recorded override above are present;
- the exact AIDA order is intact;
- no unconfirmed factual claim from the dated brief is published;
- all three selected component architectures and both selected GSAP paradigms work with keyboard, pointer, mobile, and reduced motion;
- the bento arithmetic renders without voids;
- hero typography is verified in a browser at 320, 375, 390, 768, 820, 1024, 1280, and 1440: font size follows the recorded clamp with a 35.2px floor; phone layouts use no more than three visual lines; 768px and above use exactly two; punctuation is intact; artistic asymmetry and hierarchy remain dominant; CTAs and the print do not intersect and retain at least 12px clearance on small phones; and `scrollWidth === clientWidth` at every viewport;
- primary anchors, menu, accordions, carousel, marquee pause/play, and inquiry behavior work;
- there are no browser-console errors or hydration warnings;
- lint and production build pass.

</design_plan>
