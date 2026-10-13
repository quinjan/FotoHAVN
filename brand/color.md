# Color

The palette is warm photographic paper, dark wood, aged leather, and quiet antique hardware. Light surfaces carry most of the space, dark wood tones supply type and contrast, and brass is a precision accent.

Exact values are in [tokens/tokens.json](tokens/tokens.json).

## Palette

| Name | HEX | RGB | CMYK (naive) | Role |
| --- | --- | --- | --- | --- |
| Off-white | `#FBF8F2` | 251 248 242 | 0 1 4 2 | Main canvas; inverse text |
| Warm Ivory | `#F3EBDD` | 243 235 221 | 0 3 9 5 | Primary warm editorial surface |
| Cream Paper | `#E8DDCE` | 232 221 206 | 0 5 11 9 | Photo mat; quiet inset; neutral selection |
| Hairline | `#D1C4B4` | 209 196 180 | 0 6 14 18 | Borders, dividers, image frames |
| Antique Metal | `#B39A78` | 179 154 120 | 0 14 33 30 | Muted detail and supporting text on dark |
| Muted Brass | `#9A7A4F` | 154 122 79 | 0 21 49 40 | Precision accent |
| Soft Brown | `#756457` | 117 100 87 | 0 15 26 54 | Secondary text on light |
| Dark Walnut | `#2D211B` | 45 33 27 | 0 27 40 82 | Controlled dark surface; emphasis |
| Ebony | `#1E1A17` | 30 26 23 | 0 13 23 88 | Primary text; highest-emphasis action |

## Surface levels

| Level | Surface | Use |
| --- | --- | --- |
| 0 | Off-white | Primary canvas |
| 1 | Warm Ivory | Soft editorial section |
| 2 | Cream Paper | Photo mat, quiet detail, neutral selected state |
| 3 | Dark Walnut | One intentional contrast band |
| 4 | Ebony | Closing band, footer, highest-emphasis detail |

## Proportion

- Keep 70–80% of any composition in Off-white, Warm Ivory, or photography.
- Use at most one major dark band (Dark Walnut or Ebony) plus a closing band per page, deck section, poster, or reel.
- Brass should cover less than about 2% of a composition: thin rules, one active mark, or a small brand moment.
- Photography supplies variety. Don't add colors to tell sections apart.

## Derived inks and tints

The live website derives a few values from the palette. These are allowed. New hues are not.

| Value | Derivation | Use |
| --- | --- | --- |
| Secondary ink on photography `#52463D` | 60% Soft Brown mixed with Ebony | Eyebrows and supporting copy over the light curtain (6.8–8.6:1 on light surfaces) |
| Ebony and Dark Walnut tints | Ebony or Dark Walnut at 4–32% alpha | Hairline borders, print shadows, scrims, and dimming layers |
| Off-white tints | Off-white at 11–60% alpha | Borders and veils on dark surfaces or photographs |
| Selection tint | Muted Brass at 24% alpha | Text selection; selected option backgrounds may use Warm Ivory at 50% |

Other one-off values may appear in an interpretation for a functional reason, such as a disabled control color. They must be recorded in that interpretation and must not become brand colors.

## Rules

- Use Ebony for text instead of pure black. Use Off-white instead of pure white, except where a medium's technical constraints require white, such as some print stocks.
- Use Muted Brass for selection marks, active rules, filled radios, and small brand moments. Never use it for body copy.
- Use no metallic gradients, faux-gold effects, glossy highlights, or bevels.
- Use no new hues. Event-specific colors belong in photography, templates, and props, not in the brand interface. Online-booth template artwork, such as Butter Gingham, may carry its own colors inside the keepsake.
- Prefer type in a photograph's natural light area over darkening overlays. The website hero sets Ebony type directly on the cream curtain. If an overlay is unavoidable, use the lowest-opacity Ebony or Dark Walnut gradient that passes contrast.

## Contrast pairs

WCAG 2.x contrast ratios. Body text needs at least 4.5:1. Large text (24px or larger, or 18.66px bold) and non-text elements need at least 3:1.

| Foreground ↓ / Background → | Off-white | Warm Ivory | Cream Paper | Dark Walnut | Ebony |
| --- | ---: | ---: | ---: | ---: | ---: |
| Ebony | 16.30 ✅ | 14.60 ✅ | 12.89 ✅ | — | — |
| Dark Walnut | 14.73 ✅ | 13.19 ✅ | 11.65 ✅ | — | — |
| Soft Brown | 5.33 ✅ | 4.77 ✅ | **4.21 ⚠ large only** | 2.76 ❌ | 3.06 ⚠ large only |
| Muted Brass | 3.76 ⚠ large only | 3.37 ⚠ large only | 2.97 ❌ | 3.92 ⚠ large only | 4.34 ⚠ large only |
| Antique Metal | 2.54 ❌ | 2.27 ❌ | 2.01 ❌ | 5.81 ✅ | 6.43 ✅ |
| Off-white | — | — | — | 14.73 ✅ | 16.30 ✅ |

Consequences:

- Set secondary text on Cream Paper in Ebony, Dark Walnut, or the derived secondary ink, not Soft Brown.
- Antique Metal is for dark surfaces only.
- Muted Brass text must be large, or must be paired with a non-color cue. The live footer sets the small uppercase brand line in brass on Ebony (4.34:1). That falls just below the 4.5:1 body-text bar, so it is recorded as website drift to correct. New interpretations should use Antique Metal for small text on Ebony.

## Screen

Treat HEX and RGB as sRGB. Color-managed tools such as browsers, Remotion, slide software, and WinUI use them directly.

## Print

The CMYK values above are naive mathematical conversions **for orientation only**. They are not press-ready.

1. Build print files in sRGB from the HEX values, or convert with the print vendor's ICC profile (commonly FOGRA39, FOGRA51, or GRACoL 2013, depending on the press and stock).
2. Request a physical proof on the final stock before any run. Warm neutrals shift noticeably on uncoated and toned stocks.
3. For large Ebony areas on press, ask the vendor for their rich-black recipe instead of using the naive CMYK value.
4. Muted Brass is printed as a flat CMYK color, never as metallic foil, unless an approved brief specifically calls for a single restrained foil detail.
5. After a proof is approved, record the vendor-approved CMYK or spot values in the print interpretation, not here.
