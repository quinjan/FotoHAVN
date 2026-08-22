# Responsive design QA — desktop pass 4

Date: 2026-08-22  
QA scope: fresh desktop QA at 1440x1000 and 1280x800  
Implementation: `http://localhost:3011/`  
Production files modified: none  
`website/design-qa.md` modified: no  
result: passed

## Independence disclosure

Thread-cap recovery assigned this desktop viewport pass sequentially to an agent that previously authored implementation 02 and later performed mobile pass 3, gpt-taste pass 9, and tablet pass 4. It did not author repair 05 and had not performed desktop responsive QA on the repaired source. A new named in-app Browser session and a new tab produced only new `responsive-qa-pass4-desktop-*` evidence. Earlier desktop reports were used as history, not as acceptance evidence.

## Comparison truth and normalization

- Source design truth: `website/.handoffs/gpt-taste-design-plan.md` version 1.1, with `website/DESIGN.md` applying only where the plan does not override it.
- Passed conformance authority: `website/.handoffs/gpt-taste-implementation-verification.md`, independent verifier pass 9.
- Repair under regression review: `website/.handoffs/repair-responsive-stack-typography-05.md`.
- Source bitmap pixels/CSS dimensions/density: `n/a`. The authority is a written design contract rather than a fixed bitmap, so no same-frame bitmap or density-normalization claim is made.
- Implementation truth: fresh browser-rendered captures from the shared local implementation after fonts loaded.

| Requested viewport | Browser layout | Focused capture pixels | Full-page pixels | Primary states |
|---:|---:|---:|---:|---|
| 1440x1000 | `innerWidth=1440`, `clientWidth=1425`, `clientHeight=1000` | 1425x990 | 1424x13638 | initial/scrolled header, hero, bento, accordion, marquee, gallery pin, card stack/third image, carousel, form |
| 1280x800 | `innerWidth=1280`, `clientWidth=1265`, `clientHeight=800` | 1265x791 | 1264x12415 | initial/scrolled header, hero, bento hover, card stack, overflow and reload |

The requested/client/capture differences are reserved scrollbar and in-app Browser capture-surface effects. Full-page stitches establish section order and reach; focused captures remain authoritative for sticky, pinned, interactive, and typographic judgments.

## Findings

No actionable P0, P1, or P2 finding remains. Repair 05 is confined to the tablet/mobile typography seams and introduces no desktop regression at either required width.

## Repair-05 desktop non-regression

The 1024-and-up base stack typography and GSAP branch remain authoritative:

| Viewport | Stack H3 size | Media left | STEP INSIDE right | BE YOURSELVES right | KEEP THE PHOTOGRAPH right | Tightest clearance |
|---:|---:|---:|---:|---:|---:|---:|
| 1440x1000 | 72px | 621.526px | 358.091px | 542.378px | 602.702px | 18.824px |
| 1280x800 | 64px | 552.747px | 322.928px | 486.747px | 540.362px | 12.385px |

- Every heading range ends before the media column. No glyph, text box, or paint overlaps the image region.
- Cards remain sticky with the planned 96/168/240px top offsets, z-index 1/2/3, and initial scale `.94` plus the planned vertical entry offset.
- The third printed-experience image remains eager, complete, and decoded with a non-empty optimizer URL: natural 835x1044 at 1440 and 742x927 at 1280.
- The chapter H2, copy column, third image, and card surfaces remain unclipped and in-bounds.

## Required fidelity surfaces

- Fonts and typography: passed. Cabinet Grotesk and Cormorant Garamond are loaded. Hero type remains 96px with -3.36px tracking and exactly two authored lines at both widths. Stack typography retains the desktop 72px/64px sizes above, complete copy, and no clipping.
- Spacing and layout: passed. Content gutters are approximately 72.5px at 1440 and 64px at 1280. AIDA section order is intact. Desktop bento remains a dense 12-column, two-row grid with 24px gaps and the planned 7x2 + 5x1 + 5x1 spans, with no void.
- Colors and visual tokens: passed. Off-white/Ivory/Paper surfaces, Ebony/Dark Walnut text and actions, restrained brass rules, hero veil, and contrast-safe sticky header match the plan.
- Images and assets: passed. The required local booth, guest, detail, strip, and print imagery renders sharply in stable crops. No placeholder, failed image, CSS/div illustration, emoji, handcrafted substitute, or remote-stock shortcut appears.
- Copy and truthfulness: passed. Fixed hero, component, carousel, action, form, and footer copy remains complete. No price, duration, package, rating, testimonial, location, hours, response-time, service-area, or unapproved category claim appears.
- Components and visual bans: passed. The approved dense bento, horizontal accordion, opposing marquee, pinned split gallery, stacked story cards, manual note carousel, intent form, and footer are present. No generic card factory, gradient, glow, fake gold, decorative blob, pill/badge field, rotated slab, or fake shadow appears.
- Accessibility and controls: passed. Skip link, landmarks, native links/buttons/form controls, labels, alt text, accordion ARIA, carousel live status, focus rules, and native validation remain intact. All sampled visible application targets are at least 44x44px.
- Responsiveness and overflow: passed. At both widths `documentElement.scrollWidth === clientWidth`; no unintended descendant overflow, clipping, collision, hidden persistent control, or failed loaded image was observed.
- AI-shortcut artifacts: passed. No prompt leakage, fabricated attribution, placeholder content, or generated-asset mismatch appears.

## Header, interaction, GSAP, and form verification

- Header: initial and scrolled states retain a 72px sticky surface, `rgba(251,248,242,.97)` background, Ebony content, and hairline lower rule. Geometry does not jump or regress while scrolling. The 1440 top/hero and scrolled component captures plus the dedicated 1280 initial/scrolled captures provide the state evidence.
- Hero: both desktop widths retain exactly two lines, clear gutters, 48px actions, an unobscured print crop, and no CTA/print overlap.
- Bento: pointer hover produces the specified `scale(1.05)` image transform through the 0.7s ease-out while the card clips the media correctly.
- Horizontal accordion: pointer activation updates the expanded panel and ARIA state; directional keyboard focus remains on native panel buttons.
- Marquee: pause changes the truthful control state and both opposing tracks compute paused; motion can be resumed.
- GSAP split gallery: the editorial heading pins and media cards scrub through the planned scale/opacity progression without overflow, jump, or obscured copy.
- GSAP card stack: sticky offsets, z-order, scale, and stack progression remain functional. The focused third-card state renders the eager printed image and preserves the measured text/media separation.
- Carousel: manual progression and ArrowRight advance the note and update the polite live state without autoplay or fabricated attribution.
- Form: empty submission is blocked by native validation, focuses the required intent group, and does not open an external mail application. Required and optional fields remain truthful to the plan.
- Primary anchors: in-page navigation lands below the sticky header; footer destinations remain labelled and in-bounds.

## Console, hydration, reload, and command authority

- Fresh 1440 and 1280 top loads recorded zero warning/error entries.
- Full-page and focused interaction sweeps produced no runtime error or hydration failure.
- A normal top reload returned complete document/font state, `scrollY=0`, the H1 present, document-width equality, no application/server-error text, and no failed loaded image.
- Direct rendered reduced-motion, browser zoom/text scaling, external mail-app launch, and physical-device input remain P3 environment limits rather than observed product defects. The source and passed gpt-taste gate verify static/reverted reduced-motion behavior.
- Integrated `npm run lint`, `npx tsc --noEmit`, and `npm run build` are green for the settled repair-05 source in gpt-taste verifier pass 9. This read-only viewport agent did not rerun shared build commands.

## Evidence inventory

- `website/design-qa/responsive-qa-pass4-desktop-1440x1000-top.png`
- `website/design-qa/responsive-qa-pass4-desktop-1440x1000-hero.png`
- `website/design-qa/responsive-qa-pass4-desktop-1440x1000-full.png`
- `website/design-qa/responsive-qa-pass4-desktop-1440x1000-bento-hover.png`
- `website/design-qa/responsive-qa-pass4-desktop-1440x1000-accordion-together.png`
- `website/design-qa/responsive-qa-pass4-desktop-1440x1000-marquee-paused.png`
- `website/design-qa/responsive-qa-pass4-desktop-1440x1000-gsap-gallery.png`
- `website/design-qa/responsive-qa-pass4-desktop-1440x1000-gsap-stack.png`
- `website/design-qa/responsive-qa-pass4-desktop-1440x1000-carousel.png`
- `website/design-qa/responsive-qa-pass4-desktop-1440x1000-action-form.png`
- `website/design-qa/responsive-qa-pass4-desktop-1280x800-top.png`
- `website/design-qa/responsive-qa-pass4-desktop-1280x800-hero.png`
- `website/design-qa/responsive-qa-pass4-desktop-1280x800-full.png`
- `website/design-qa/responsive-qa-pass4-desktop-1280x800-header-initial.png`
- `website/design-qa/responsive-qa-pass4-desktop-1280x800-header-scrolled.png`
- `website/design-qa/responsive-qa-pass4-desktop-1280x800-bento-hover.png`
- `website/design-qa/responsive-qa-pass4-desktop-1280x800-gsap-stack.png`

All files above are new pass-4 evidence under `website/design-qa/`. Focused implementation pixels were read from the saved PNGs and are listed in the normalization table.

## Comparison history

| Iteration | Finding | Repair/post-fix evidence | Result |
|---|---|---|---|
| Responsive QA pass 2 | Initial desktop/tablet header contrast failed. | Repair 04 added the contrast-safe min-768 header surface; later desktop evidence closed it. | closed |
| Responsive QA pass 3 | Desktop passed; tablet stack H3 ranges later required repair. | Repair 05 changed only 768–1023 card H3 and max-767 chapter H2 rules. | desktop unchanged |
| GPT-taste pass 9 | Full design-plan and command gate rerun on repair 05. | Typography, header, image loading, GSAP, overflow, console, lint, typecheck, and build passed. | passed |
| Responsive QA desktop pass 4 | Fresh 1440/1280 regression and interaction review. | Measurements and focused captures above show no actionable P0/P1/P2. | passed |

## Implementation checklist

No desktop repair is warranted. Preserve the repair-05 tablet/mobile seam, the repair-04 header/image behavior, the current 1024 GSAP boundary, and the verified desktop stack typography.

final result: passed
