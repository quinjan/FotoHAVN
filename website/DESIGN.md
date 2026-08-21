# FOTOHVN — Design Language

> Editorial photography studio × vintage European photo booth × modern boutique brand

**Theme:** light-led, warm, tactile, and photography-first

FOTOHVN is a vintage enclosed photobooth experience in the Philippines. The website must position it as a considered photography experience—not a commodity booth rental. It should feel nostalgic without becoming costume-like, premium without becoming ornate, and intimate without becoming dark or cinematic.

The visual system adapts the reference design’s generous breathing room, poster-scale hierarchy, flat surfaces, minimal elevation, disciplined spacing, and subtle geometry. FOTOHVN replaces the reference’s pastel taxonomy, geometric-only typography, gradients, and illustration-led imagery with restrained earth tones, serif-led editorial type, tactile photography, and quiet antique-metal details.

## Authority

When sources disagree, apply this order:

1. The approved FOTOHVN brand and content brief.
2. This document.
3. `tokens.json`, `variables.css`, and `theme.css`.
4. The Refero/Amplemarket reference, used only for structural restraint.

Do not reintroduce the reference’s pastel colors, confetti language, product-marketing cards, geometric-only display type, illustrated hero, or vivid gradient accents.

## Brand Positioning

**Core idea:** photographs, developed differently.

**Brand promise:** a private, tactile photography experience that gives guests room to play and something physical to keep.

**Mood:**

- nostalgic
- refined
- intimate
- artistic
- premium
- understated
- timeless
- tactile
- warm

**Never feel like:**

- a generic party supplier
- a discount rental catalogue
- a wedding-template website
- a nightclub or cinematic luxury brand
- a social-media filter product

Avoid balloons, confetti, loud colors, cheesy party graphics, excessive icons, excessive gold, black-heavy layouts, dense card grids, and crowded decoration.

## Core Brand Copy

**Brand:** FOTOHVN

**Hero headline:**

> PHOTOGRAPHS,<br>
> DEVELOPED DIFFERENTLY.

**Hero supporting text:**

> An enclosed vintage photobooth experience for celebrations worth remembering.

**Brand line:**

> PHOTOGRAPHS, DEVELOPED DIFFERENTLY.

Copy should be concise, sensory, and human. Prefer “photographs,” “keepsakes,” “room,” “look,” and “experience” over “content,” “activation,” “solution,” “package options,” or “filters.”

## Foundations

### Color Palette

The palette should resemble warm photographic paper, dark wood, aged leather, and quiet antique hardware. Warm ivory and off-white occupy most of the page. Ebony and dark walnut provide typography and occasional contrast. Muted brass is a precision accent, never a large decorative fill.

| Name | Value | Token | Role |
|---|---:|---|---|
| Warm Ivory | `#F3EBDD` | `--color-warm-ivory` | Primary warm editorial surface |
| Cream Paper | `#E8DDCE` | `--color-cream-paper` | Secondary section and photo-mat surface |
| Off-white | `#FBF8F2` | `--color-off-white` | Main canvas and inverse text |
| Ebony | `#1E1A17` | `--color-ebony` | Primary text and highest-emphasis action |
| Dark Walnut | `#2D211B` | `--color-dark-walnut` | Controlled dark surface and hover state |
| Soft Brown | `#756457` | `--color-soft-brown` | Secondary text and quiet labels |
| Muted Brass | `#9A7A4F` | `--color-muted-brass` | Selected states, rules, and small brand moments |
| Antique Metal | `#B39A78` | `--color-antique-metal` | Muted detail on dark surfaces |
| Hairline | `#D1C4B4` | `--color-hairline` | Borders, dividers, and image frames |

#### Color Rules

- Keep 70–80% of visible surfaces in Off-white, Warm Ivory, or photography.
- Use Ebony for body text rather than pure black.
- Limit solid Dark Walnut or Ebony to one major content band plus the footer.
- Use Muted Brass for rules, active indicators, focus-adjacent detail, or large display text only; it is not a default body-copy color.
- Do not use metallic gradients, faux-gold effects, or glossy highlights.
- Photography supplies variation. Do not add colors merely to differentiate sections.

### Typography

The type system pairs an expressive editorial serif with a clean modern sans-serif.

**Display and headings:** `Cormorant Garamond`, with `Times New Roman` and `Georgia` fallbacks. Use weight 500 for most headings and 600 only for compact emphasis. Uppercase display copy may track slightly tighter; never use a heavy black weight.

**Body and interface:** `Manrope`, with `Inter`, `Segoe UI`, and system sans-serif fallbacks. Use weight 400 for reading text and 600 for buttons or concise labels.

| Role | Size | Line height | Tracking | Family | Weight |
|---|---:|---:|---:|---|---:|
| Eyebrow / label | 12px | 1.2 | `0.18em` | Sans | 600 |
| Body small | 14px | 1.55 | `0` | Sans | 400 |
| Body | 16px | 1.65 | `0` | Sans | 400 |
| Body large | 20px | 1.5 | `-0.01em` | Sans | 400 |
| Subheading | 28px | 1.2 | `-0.015em` | Serif | 500 |
| Heading small | 36px | 1.08 | `-0.02em` | Serif | 500 |
| Heading | 52px | 1.0 | `-0.025em` | Serif | 500 |
| Heading large | 72px | 0.94 | `-0.03em` | Serif | 500 |
| Display | 96px | 0.88 | `-0.035em` | Serif | 500 |
| Price | 80px | 0.9 | `-0.03em` | Serif | 500 |

Use fluid sizing in implementation. The display should resolve approximately as `clamp(3.5rem, 8vw, 6rem)` and the main section heading as `clamp(2.75rem, 5vw, 4.5rem)`.

#### Typography Rules

- Major headlines are editorial serif; supporting copy, navigation, buttons, prices metadata, and labels are sans-serif.
- Uppercase is reserved for the hero, short headings, labels, CTAs, photographic-look names, and event names.
- Keep prose in sentence case and a comfortable measure of 45–65 characters.
- Do not center every section. Alternate restrained left alignment, split layouts, and rare centered statements.
- Do not use playful script, condensed display, outlined, distressed, or typewriter fonts.

### Spacing and Layout

**Base unit:** 4px<br>
**Page max width:** 1280px<br>
**Reading measure:** 65ch<br>
**Desktop section rhythm:** 120–144px<br>
**Mobile section rhythm:** 72–96px

The spacing scale is `4, 8, 12, 16, 24, 32, 40, 48, 64, 80, 96, 120, 144`.

Use a 12-column desktop grid, a 6-column tablet grid, and a 4-column mobile grid. Full-bleed photography may leave the container; text remains aligned to the grid. Asymmetry is intentional but must still feel calm and balanced.

### Shape

| Element | Radius |
|---|---:|
| Hairline/photo edge | 2px |
| Buttons and inputs | 4px |
| Cards and panels | 8px |
| Circular controls only | 9999px |

Rounded geometry should feel subtle and crafted. Do not use oversized pill cards, bubbly containers, or nested rounded panels.

### Surfaces and Elevation

| Level | Surface | Purpose |
|---|---|---|
| 0 | Off-white | Primary page canvas |
| 1 | Warm Ivory | Soft editorial section |
| 2 | Cream Paper | Photo mat, quiet package detail, selected neutral state |
| 3 | Dark Walnut | Signature package or one intentional contrast band |
| 4 | Ebony | Footer and highest-emphasis detail |

Prefer whitespace, cropping, hairline borders, and tonal surface changes over shadows. Standard cards have no drop shadow. A photographic print may use one soft shadow: `0 24px 60px rgba(45, 33, 27, 0.14)`. Do not stack multiple shadows.

## Photography Direction

Photography is the primary visual language. It should communicate the physical booth, the privacy of the enclosure, the act of making photographs, and the printed result.

### Subject Priority

1. The full enclosed FOTOHVN booth in a real event environment.
2. Guests inside or entering the booth, shown candidly and respectfully.
3. Printed strips held, exchanged, pinned, or resting on tactile surfaces.
4. Close details: curtain, flash, camera, metal fittings, wood, paper, and hands.
5. Comparative examples of the four photographic looks.

### Treatment

- Favor warm available light, soft flash, visible texture, and natural skin tone.
- Use editorial crops with deliberate negative space for type.
- Preserve subtle grain and material detail; do not apply fake heavy film damage.
- Use a mix of landscape, portrait, and narrow photo-strip proportions.
- Borders may resemble a restrained photographic print mat, not a scrapbook.
- Never use party stock photography, fake bokeh overlays, confetti, balloons, neon, or generic camera icons.
- Do not represent photographic looks as one-click novelty filters. Show credible full-frame examples with consistent subjects where possible.

When approved FOTOHVN imagery is unavailable, implementation should use purpose-made editorial photo assets or omit optional images. Do not ship blank placeholder boxes or unrelated stock-party imagery.

## Components

### Navigation

A quiet sticky bar approximately 72px tall. It begins transparent over the hero when contrast is sufficient and becomes Off-white with a hairline lower border on scroll. FOTOHVN sits left; minimal text links sit right with one compact `BOOK FOTOHVN` action. Use no icon row.

### Primary Button

Ebony background, Off-white text, 1px Ebony border, 4px radius, minimum height 48px, and 16px × 22px padding. Label uses 12–13px sans-serif, weight 600, uppercase, and `0.12em` tracking. Hover shifts to Dark Walnut; focus uses a 2px high-contrast ring with a 3px offset.

### Secondary Button

Transparent background with a current-text hairline border. On photography, use Off-white text and border; on light surfaces, use Ebony. Hover introduces a restrained translucent surface. It must remain visibly secondary to booking.

### Editorial Text Link

Text-only action with a 1px underline offset by 5–6px. Use for `EXPLORE THE EXPERIENCE` when a bordered second button feels too heavy.

### Section Label

Short uppercase sans-serif label with `0.18em` tracking. Use Soft Brown on light surfaces and Antique Metal on dark surfaces. A short Muted Brass rule may precede it.

### Editorial Media Frame

Photography sits in a 2px radius frame with optional 1px Hairline border. Captions use small sans-serif text and align to the image edge. Never overlay badges, icon buttons, or decorative stickers.

### Experience Feature

One photograph, a short uppercase title, and one sentence. The three items—Enclosed, Printed, Distinctive—share a baseline but may use different photo proportions. No icons.

### Photographic Look Selector

The selector combines one dominant example image with four text-led tabs: Classic, Vintage, Monochrome, and FOTOHVN Signature. The active tab uses an Ebony label and Muted Brass rule; inactive tabs use Soft Brown. Selecting a tab crossfades the image and updates its descriptive caption without moving the surrounding layout.

Use accessible tab semantics, visible focus, arrow-key navigation, and a URL/hash or anchor fallback. If the target platform supports only links, each option should jump to its corresponding example figure.

### Signature Experience Block

This is an editorial pricing statement, not a pricing card. Use one full-width composition with a large `₱8,500`, large `3 HOURS`, a concise inclusion list, and one booking action. A Dark Walnut surface is allowed here when the rest of the page stays light. Do not add ribbons, “best value,” comparisons, crossed-out pricing, or multiple packages.

### Gallery Composition

Use an asymmetric 12-column composition with large and small photographs, controlled overlaps of no more than 24px, occasional photo-mat borders, and generous gaps. Avoid equal square tiles, carousel dots, social-media chrome, or an Instagram-style grid.

### Event Feature

Each event type uses a large photograph and minimal uppercase title. Alternate image alignment or use a measured editorial sequence. Do not turn the five event types into small icon cards.

## Page Architecture

The production site follows this order.

### 1. Hero

Use a sophisticated full-width image of the FOTOHVN booth at approximately 80–90svh. Place the text in genuine negative space, preferably lower-left or left-center. A restrained directional image overlay may be used only to preserve text contrast; keep the photograph luminous rather than cinematic.

**Headline:** `PHOTOGRAPHS,` / `DEVELOPED DIFFERENTLY.`<br>
**Supporting text:** `An enclosed vintage photobooth experience for celebrations worth remembering.`<br>
**Primary CTA:** `BOOK FOTOHVN`<br>
**Secondary CTA:** `EXPLORE THE EXPERIENCE`

The first viewport must communicate a premium photography experience before it communicates rental logistics.

### 2. The FOTOHVN Experience

**Heading:** `THE FOTOHVN EXPERIENCE`<br>
**Intro:** `A little room for photographs, laughter, and moments you'll want to keep.`

Present three photography-led features:

- **ENCLOSED** — A private little space made for candid moments.
- **PRINTED** — Take home photographs, not just digital files.
- **DISTINCTIVE** — Choose from FOTOHVN's specialized photographic looks.

### 3. Photographic Looks

This is one of the two most visually important sections after the hero.

**Eyebrow:** `FOTOHVN PHOTOGRAPHIC LOOKS`<br>
**Heading:** `CHOOSE YOUR LOOK`<br>
**Subheading:** `One booth. Four ways to remember it.`

- **CLASSIC** — Clean, timeless tones with subtle analog character.
- **VINTAGE** — Warm, faded tones inspired by old photographs.
- **MONOCHROME** — Rich black-and-white with a classic studio feel.
- **FOTOHVN SIGNATURE** — A distinctive FOTOHVN house look developed specifically for the brand.

On desktop, use a 7/5 split with the dominant image on one side and the selector on the other. On mobile, place the image first and use a horizontally scrollable tab list or compact stacked list with clear selection.

### 4. Signature Package

**Eyebrow:** `SIGNATURE EXPERIENCE`<br>
**Heading:** `THE FOTOHVN EXPERIENCE`<br>
**Price:** `₱8,500`<br>
**Duration:** `3 HOURS`

Include:

- 3 hours of unlimited booth sessions
- Printed photo strips
- Digital copies
- Event attendant
- Custom event photo template
- Specialized FOTOHVN photographic looks
- Setup & teardown

**CTA:** `BOOK FOTOHVN`<br>
**Supporting line:** `ONE BOOTH. ONE EXPERIENCE. YOUR LOOK.`

Use a strong grid, generous space, and one fine brass rule. The price and duration may be visually paired but should not sit inside a conventional rounded pricing card.

### 5. Gallery

**Heading:** `SEE IT IN ACTION`

Build an editorial sequence rather than a uniform grid: one landscape anchor, one tall portrait, one narrow strip detail, and supporting candid images. Vary scale and vertical position while keeping calm margins. Use approved FOTOHVN photographs whenever available.

### 6. Events

**Heading:** `MADE FOR MOMENTS LIKE THESE`

Feature:

- `WEDDINGS`
- `DEBUTS`
- `BIRTHDAYS`
- `CORPORATE EVENTS`
- `PRIVATE CELEBRATIONS`

Use large visual moments with minimal text. The photography should carry the difference between event types; the interface should not introduce color coding or themed graphics.

### 7. Brand Story

**Heading:** `MORE THAN A PHOTOBOOTH.`

**Approved editorial copy:**

> FOTOHVN was created for people who want more than a quick snapshot. Step inside, draw the curtain, and take a little time to laugh, experiment, and make something together. Our vintage-inspired booth pairs an intimate experience with distinctive photographic looks and physical prints—keepsakes made to be held, shared, and kept long after the celebration.

Pair the story with one quiet booth-detail or print-handling photograph. Avoid founder-corporate language or a dense timeline.

### 8. Final CTA

Use one visually striking booth or print photograph with ample negative space, or a restrained Warm Ivory statement panel beside photography.

**Headline:** `LET'S MAKE SOMETHING WORTH KEEPING.`<br>
**Supporting text:** `Bring FOTOHVN to your next celebration.`<br>
**CTA:** `INQUIRE NOW`

The CTA links to the inquiry/contact section or the approved external inquiry form.

### Footer

Keep the footer minimal on an Ebony surface.

**FOTOHVN**<br>
**PHOTOGRAPHS, DEVELOPED DIFFERENTLY.**

Links: Instagram · Facebook · Email<br>
Legal: `© 2026 FOTOHVN`

## Interaction and Motion

- Standard interaction duration: 240ms.
- Fast hover/focus feedback: 150ms.
- Editorial reveal: up to 600ms using opacity and a maximum 16px vertical translation.
- Image hover scale: maximum `1.015`; never create dramatic zoom.
- Photographic-look transition: 240ms crossfade with stable dimensions.
- Sticky navigation may transition its background, border, and text color.
- Do not use autoplay carousels, bouncing elements, parallax that fights scrolling, cursor effects, or ornamental looping animation.
- Respect `prefers-reduced-motion` by removing transforms and using immediate or near-immediate state changes.

## Responsive Behavior

- Preserve image priority on every breakpoint; do not collapse the site into text followed by tiny thumbnails.
- Hero text must remain legible without covering the booth’s key details.
- Shift split layouts to a single column below approximately 768px.
- Keep at least 24px mobile page gutters and 48px tablet gutters.
- Allow photographic-look tabs to scroll horizontally only when labels remain fully readable.
- Convert gallery overlaps to a clean stacked rhythm on small screens.
- Keep CTAs at least 44px tall and full-width only when the narrow viewport requires it.

## Accessibility

- Maintain at least WCAG AA contrast: 4.5:1 for normal text and 3:1 for large text and controls.
- Do not use Muted Brass for small body text on light surfaces.
- Provide a visible 2px focus ring with a 3px offset on every interactive element.
- Use semantic headings, landmarks, buttons, links, lists, and tabs.
- Give photography concise, purposeful alt text; decorative texture assets use empty alt text.
- Keep body copy at 16px minimum and avoid long centered paragraphs.
- Never communicate the selected photographic look by color alone; pair the brass rule with text state and ARIA selection.
- Keep inquiry links descriptive and ensure external forms expose an accessible name.

## Do and Don’t

### Do

- Let one excellent photograph dominate each major section.
- Use generous negative space and strong alignment.
- Use serif scale, cropping, and tonal contrast to create drama.
- Use physical details—paper, curtain, wood, metal, flash, and hands—to make the experience tactile.
- Keep brass accents thin, quiet, and intentional.
- Present one signature package with confidence.
- Treat photographic looks as authored image treatments with credible examples.

### Don’t

- Don’t use party motifs, confetti, balloons, neon, or celebratory clip art.
- Don’t use pastel category cards from the reference.
- Don’t use a generic wedding-supplier template or dense package comparison.
- Don’t over-darken photographs or fill the page with black sections.
- Don’t add icons where photography or type can communicate the idea.
- Don’t use gold gradients, metallic bevels, or ornate vintage decoration.
- Don’t turn the gallery into a social feed.
- Don’t call photographic looks “filters.”

## Implementation Contract

- `variables.css` is the canonical CSS custom-property source.
- `tokens.json` is the canonical machine-readable token source and should mirror the same primitive values.
- `theme.css` exposes the same approved primitives to Tailwind v4.
- Load the approved serif and sans font files before relying on the named families; fallbacks must remain functional.
- Use real or purpose-made photography assets. Do not approximate imagery with CSS drawings, emoji, icons, or empty blocks.
- Preserve the page order, core copy, one-package model, and booking/inquiry hierarchy defined here.

## Reference Inheritance Summary

**Retain from the reference:** generous whitespace, poster-scale hierarchy, flat surfaces, minimal shadow, controlled radii, tight grid discipline, and a confident light-to-dark rhythm.

**Replace for FOTOHVN:** pastel taxonomy → restrained material palette; geometric-only typography → editorial serif plus modern sans; gradient/illustration hero → immersive booth photography; product card grids → image-led editorial compositions; software conversion language → intimate photography-experience language.

**Reject entirely:** confetti, loud multi-hue accents, generic pricing cards, novelty-filter framing, excessive icons, and decorative party styling.
