# FOTOHAVN website design

## Brand interpretation

This document is the **website's interpretation** of the shared FOTOHAVN brand system in [`../brand/`](../brand/README.md). The brand system owns the foundation, voice, palette, type, photography, logo, motion, and accessibility rules. This document owns only what is specific to the website and online booth: page architecture, web components, responsive behavior, implementation files, and verification.

- **Interprets:** [`brand/`](../brand/README.md) on the default branch. The brand system has no version numbers.
- **Reference implementation:** the live website is the brand's reference implementation. When this document drifts from what the website renders, **the website wins**. Update this document (and `brand/` if the change is brand-level) to match, or fix the website deliberately.
- **Deliberate departures from `brand/`:** none. Known drift is listed in [Recorded drift](#recorded-drift).

Read [`CONTEXT.md`](CONTEXT.md) for website language such as the Website Intro Experience, Online Booth Cycle, and Inquiry Intent.

## What the website inherits

| Topic | Source of truth |
| --- | --- |
| Positioning, mood, principles, the three moments | [brand/foundation.md](../brand/foundation.md) |
| Voice, approved lines, actions, offers | [brand/voice.md](../brand/voice.md) |
| Palette, surfaces, contrast, derived inks | [brand/color.md](../brand/color.md) |
| Type pairing, roles, the italic turn | [brand/typography.md](../brand/typography.md) |
| Space, composition, shape, elevation | [brand/layout.md](../brand/layout.md) |
| Photography, looks, consent, alt text | [brand/photography.md](../brand/photography.md) |
| Wordmark and booth-portal mark | [brand/logo.md](../brand/logo.md) |
| Icons (Phosphor Light) | [brand/iconography.md](../brand/iconography.md) |
| Motion and reduced motion | [brand/motion.md](../brand/motion.md) |
| Accessibility baseline | [brand/accessibility.md](../brand/accessibility.md) |
| Shared components: actions, eyebrow, statement heading, path, print object, option list, step progress, navigation, footer | [brand/components.md](../brand/components.md) |

## Implementation files

| File | Role |
| --- | --- |
| [`variables.css`](variables.css) | CSS custom properties for the brand primitives, semantic roles, and web-only values. Imported by `src/app/globals.css`. |
| [`theme.css`](theme.css) | The same values exposed to Tailwind v4 through `@theme`. |
| [`tokens.json`](tokens.json) | Machine-readable web token mirror. It must match `variables.css`. |
| `src/app/globals.css` | Global type, `.button`, `.textLink`, focus, skip link, and reduced motion. |
| `src/app/layout.tsx` | Loads Cormorant Garamond (400, 500, 600; normal and italic) and Manrope through `next/font/google`. |
| `src/components/*.module.css` | Section and component styles. |

Brand primitives in these files must match [`brand/tokens/tokens.json`](../brand/tokens/tokens.json) by name and value. Web-only additions such as breakpoints, navigation height, gallery overlap, and layer order live only here.

## Page architecture

### Home (`/`)

The live order, from `src/app/page.tsx`:

| # | Section | Anchor | Content |
| --- | --- | --- | --- |
| — | Navigation | — | Wordmark; `EXPERIENCE`, `THE BOOTH`, `PRINTS`, `ONLINE BOOTH`; `FIND THE BOOTH`; primary `RENT FOTOHAVN`. Mobile shows `MENU`. |
| 1 | Hero | `#top` | Booth exterior photograph, with the statement heading centered on the curtain: `A LITTLE ROOM. A LASTING FEELING.` / Photographs, *developed differently.* Primary `RENT FOTOHAVN ↓` and text link `EXPLORE THE EXPERIENCE ↓`. |
| 2 | Experience | `#experience` | `THE FOTOHAVN EXPERIENCE` / A little privacy. A little *possibility.* The three moments with photographs, then `EXPERIENCE FOTOHAVN ONLINE`. |
| 3 | The booth | `#the-booth` | `THE BOOTH, UP CLOSE` / Small room. *Whole other world.* An interactive 3D booth with viewpoints, curtain toggle, and fullscreen. It is labeled as an illustrated approximation. |
| 4 | Prints | `#prints` | Keep the moment *close.* The print object, with `SEE THE GOOD COMPANY ↘` and `MAKE A DIGITAL STRIP →`. Sign-off: `MADE TO BE HELD, SHARED, AND KEPT.` |
| 5 | Guest album | `#guest-album` | `ON THE OTHER SIDE` / Look at *you.* Prints on a metal board with clips and keepsake charms. Prints lift and turn over to a note. It has a fullscreen board and a mobile carousel. |
| 6 | Closing | `#make-something-worth-keeping` | Let's make something *worth keeping.* Two paths: `#find-the-booth` (pay-per-use visit) and `#rent-fotohavn` (event rental), each with an Instagram inquiry link ↗. |
| — | Footer | — | Ebony surface, the brand line, Instagram / Facebook / TikTok ↗, the oversized wordmark, `© 2026 FOTOHAVN`, `BACK TO TOP ↑`. |

### Online booth (`/online`)

The Online Booth Cycle runs in four steps: `01 Template`, `02 Photographs`, `03 Filters`, `04 Download`. Each step has an italic-turn heading: Make a little *Moment*, Find your *light.*, Find the *feeling.*, A little moment. *Yours.* The header shows the wordmark, `THE ONLINE BOOTH`, and `← BACK TO FOTOHAVN`.

- **Templates:** Signature Strip, Panorama Four, Butter Gingham, Polka Keepsake, defined in `src/components/onlinePhotobooth/presets.ts`. Template artwork may carry its own colors inside the keepsake.
- **Looks (filters):** the seven brand looks, in the order of [brand/photography.md](../brand/photography.md#looks). They are defined in `presets.ts` as visual approximations of the booth's published outputs, not the booth's actual color profiles.
- Camera, preview, and download stay on the visitor's device. Customer-created media stays fully masked in analytics.

### Inquiry and analytics

Inquiry actions open the FOTOHAVN Instagram direct-message route from `site.config.ts` (`instagramMessageUrl`). Each activation is an Inquiry Intent. Consent, masking, and measurement are specified in [`docs/website-analytics.md`](docs/website-analytics.md).

## Web components

These are web implementations of the [brand components](../brand/components.md):

| Brand component | Web implementation |
| --- | --- |
| Primary action | `.button` in `globals.css`: 48 high, 14 × 22, 12px / 0.1em, with an arrow span that nudges on hover |
| Text link | `.textLink` in `globals.css`: 12px / 0.08em, 7px underline offset, Hairline underline that turns to the text color on hover |
| Statement heading | Section heading styles in each `*.module.css`. Headings use `<em>` for the italic turn. |
| Navigation | `SiteChrome`: sticky, wordmark at 30px (28px mobile), Meta links, primary action, mobile menu |
| Print object, guest album | `GuestAlbum`, `GuestAlbumCarousel`, `EditorialLift` |
| Option list, step progress | `onlinePhotobooth/*` |
| 3D booth | `BoothExplorer`, `booth/*` |

## Responsive behavior

- The main breakpoint is **767 / 768px**: split layouts become a single column, and navigation becomes the `MENU` button. Secondary adjustments happen at 1100, 1023, 960, 720/700, and 389/360/350px.
- Page gutters are fluid from 24 to 80. The hero keeps the booth and curtain visible at every width, and on mobile it uses the portrait exterior photograph.
- Keep photography first at every breakpoint. Do not collapse sections into text followed by tiny thumbnails.
- Text links and controls keep a target of at least 44 × 44. The primary action is at least 48 high.
- Verify at desktop, tablet, 390px, and 320px widths (see [`AGENTS.md`](AGENTS.md)).

## Recorded drift

These are known differences between the live website and the brand system or the website's own files. They are recorded, not hidden. Fix them deliberately.

| Drift | Where | Brand position |
| --- | --- | --- |
| Footer brand line is Muted Brass small text on Ebony (4.34:1) | Footer | Use Antique Metal for small text on Ebony (6.43:1) |
| `variables.css` defines `--focus-offset: 3px`; `globals.css` renders a 5px offset | `variables.css`, `globals.css` | 5px, as rendered |
| `variables.css` and `tokens.json` type scale (500 weight, 52 / 72 / 96 sizes, price role) does not match the rendered 400-weight fluid headings | `variables.css`, `theme.css`, `tokens.json` | The rendered values in [brand/typography.md](../brand/typography.md#type-roles) |
| Online booth wordmark tracking is −0.065em | `OnlinePhotobooth.module.css` | −0.05em, matching the master |
| Online booth uses one-off colors `#655648` and `#C5C0BA` | `OnlinePhotobooth.module.css` | Allowed as functional one-offs; prefer derived inks |
| Some consent and prototype styles use weight 700 | `AnalyticsConsent*.module.css` | Maximum weight is 600 |

### Superseded by the live site

Earlier versions of this document specified a four-look selector (Classic, Vintage, Monochrome, FOTOHAVN Signature), a `₱8,500 / 3 HOURS` signature package block, a gallery section, an events section, a brand-story section, an uppercase hero, and `BOOK FOTOHAVN` / `INQUIRE NOW` actions. None of these are on the live website, and they are no longer part of this interpretation. The history is in git.
