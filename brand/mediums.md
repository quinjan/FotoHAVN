# Mediums

This page explains how the brand adapts to each medium. These are principles only. Templates, project files, and medium-specific tokens belong to each interpretation.

## Type scaling

Scale by **role**, not by multiplying pixels. Choose the medium's minimum Body size, then size the other roles relative to it.

| Role | Web / app (1×) | Slides (1920 × 1080) | Vertical video (1080 × 1920) | Print (A4 / A3) | Poster (A2+ / 24 × 36 in) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Eyebrow / Meta | 10–12px | 18–20px | 26–28px | 7–8pt | 14–18pt |
| Body (minimum for reading) | 16px | 28px | 36px | 9–10pt | 18–24pt |
| Heading | 44–56px | 56–80px | 72–110px | 24–36pt | 72–120pt |
| Display | 52–118px | 96–160px | 120–200px | 48–72pt | 160–300pt |

Tracking and line height follow the role (see [typography.md](typography.md#type-roles)). Uppercase eyebrows keep their wide tracking at every size. The italic turn applies in every medium.

## Web

- **Interpretation:** [`website/DESIGN.md`](../website/DESIGN.md). The live website is the reference implementation of this brand.
- Uses the token reference values directly at 1×, with fluid heading sizes.
- Content width is 1280–1600. Gutters are fluid from 24 to 80.
- Verify at desktop, tablet, 390px, and 320px widths.

## Desktop booth app

- **Interpretation:** [`app/DESIGN.md`](../app/DESIGN.md). It generates its CSS from the token reference values and records each booth departure with its reason.
- Booth readability at kiosk distance takes priority over editorial density. Type roles scale above the web reference from 1280 × 720 to 2560 × 1440, and guest reading text is at least 20px.
- Touch targets are raised: at least 64 × 64 for guests (the camera's live preview is itself the tap-to-start control) and 48 × 48 for operators.
- Guest screens never scroll as a page, and the live preview is the one dark band on the Photographs step.
- Guest Cycle phases run under Phase Timers whose time left is always visible and can't be extended, so an abandoned Guest Cycle still reaches a saved Photo Strip. This is a recorded business departure from [accessibility.md](accessibility.md#focus-and-input); see the interpretation.
- The app must also follow its own operator and Guest Cycle safety rules. Verify at 1280 × 720, 1366 × 768, 1920 × 1080, and 2560 × 1440.

## Video: reels, stories, and shorts

- **Interpretations:** each project under [`marketing/reels/`](../marketing/reels/AGENTS.md).
- **Frames:** 1080 × 1920 (9:16) is primary. 1080 × 1350 (4:5) and 1920 × 1080 (16:9) are derivatives.
- **Safe area (9:16):** keep text, the wordmark, and captions out of the top 250px and bottom 420px, and at least 64px from each side. Platform UI changes, so check the current Instagram and TikTok overlays before final export, and use the platform's stricter spec for paid placements.
- **Grid:** 4 columns at 64px margins.
- **Pacing:** let at least one photograph hold for 2 seconds or more. Use crossfades and restrained fades instead of whip, glitch, or zoom transitions. See [motion.md](motion.md).
- **End card:** wordmark, one line from [voice.md](voice.md#approved-lines), and one action, on Off-white or Ebony.
- **Audio:** use only cleared music. Never embed uncleared guide tracks in a master.
- **Captions:** burn in captions for essential speech, inside the safe area.

## Presentations

- **Frame:** 16:9, 1920 × 1080 reference.
- **Margins:** 96px. **Grid:** 12 columns.
- Cover: one full-bleed photograph, or Off-white with the wordmark and a single heading.
- One idea per slide. Most slides use one dominant photograph, one heading, and at most three short lines.
- Dark slides follow the color proportion rule: at most one dark section divider, plus a closing slide.
- No clip art, icons as bullets, or decorative transitions. Use only a fade between slides.
- Embed the fonts, or export to PDF for distribution.

## Print: posters, flyers, cards, and signage

- **Bleed:** 3mm. **Safe zone:** at least 5mm inside the trim. **Outer margin:** at least 10mm (A4) and scaled up with format.
- **Grid:** 6 columns portrait, 12 columns landscape.
- **Images:** at least 300ppi at final size (150ppi acceptable for large-format posters viewed at a distance).
- **Color:** proof on the final stock. See [color.md](color.md#print).
- Prefer uncoated or lightly textured warm stock where the budget allows; it supports the paper-and-print brand world.
- Posters lead with one photograph and one serif statement. Event details go in the Label and Body roles, in a quiet band.

## Social posts and profiles

- **Feed:** 1080 × 1350 (4:5) primary and 1080 × 1080 secondary, using the 4-column video grid at 64px margins.
- **Carousels:** one photograph per frame. The first frame carries the serif statement and the last frame carries the action.
- **Profile avatar:** the booth-portal mark centered on an Off-white tile with its full clear space.
- Never use platform stickers, emoji-heavy captions, or trending templates that break the palette.

## Physical booth and event signage

- Signage uses the same type roles at poster scale, on Off-white, Warm Ivory, or Dark Walnut.
- Brass can appear as a real material detail on the booth. It still is not a printed metallic effect.
- Photo Strip layouts are product artifacts governed by the booth app and the Event. They follow this brand's palette and type, but each Event's custom design takes priority inside the strip.
