# Layout

FOTOHAVN layouts are calm, asymmetric, and photography-led. They borrow structural restraint from editorial print: generous margins, a strong grid, and few elements per view. The website is the reference implementation.

## Space

- **Base unit:** 4px (or 4 units of the medium's reference size).
- **Scale:** `4, 8, 12, 16, 24, 32, 40, 48, 64, 80, 96, 120, 144`. The website also uses fluid in-between values, for example a section top padding of 130 and gaps of 100. Treat the scale as the default, and keep fluid values proportional.
- Large gaps between major moments, about 100–144 at desktop reference, are part of the brand. Do not compress them to fit more content.

## Width and gutters

The website reference: content tops out at **1280–1600** wide, with the wide editorial grid at 1600 and the reading and inquiry areas at 1280. Page gutters are fluid from **24 to 80** (`clamp(24px, 5vw, 80px)`).

## Grid

Every medium uses a column grid with consistent gutters and margins. Text aligns to the grid. Photography may bleed past it.

| Context | Columns | Reference |
| --- | --- | --- |
| Wide screens, landscape slides, landscape print | 12 | Desktop web, 16:9 slides, A-series landscape |
| Medium screens, portrait print | 6 | Tablet, A-series portrait, posters |
| Narrow screens, vertical video | 4 | Phones, 9:16 reels and stories |

Specific margins, safe areas, and bleed are in [mediums.md](mediums.md).

## Composition

- Let **one photograph dominate** each major moment.
- Use **asymmetry deliberately**: 5/7 and 1.1/1 splits, offset images, and type placed in the photograph's real negative space.
- Mix landscape, portrait, and narrow strip proportions. Avoid equal square tiles and uniform card grids.
- Prints may scatter on a board as physical objects (see [components.md](components.md#print-object)). Editorial photographs may overlap by no more than 24 units, and they collapse to a clean stack in narrow formats.
- Keep prose to a 45–65 character measure.
- **Center** only the hero statement over photography and single-subject moments, such as the guest album heading. Section content is left-aligned.

## Shape

| Element | Radius |
| --- | ---: |
| Photographs and prints | 2 |
| Actions and inputs | 4 |
| Panels | 8 |
| Circular controls only | full |

Corners should feel subtle and crafted. Never use pill-shaped cards, bubbly containers, or rounded panels nested inside rounded panels.

## Surfaces and elevation

FOTOHAVN is flat. Create depth with whitespace, cropping, hairline borders, and tonal surface changes (see [color.md](color.md#surface-levels)). Physical prints are the exception.

- Standard panels have no shadow.
- A print resting or pinned on a surface may use the small `print-contact` shadow (`0 3 8` in Dark Walnut at 9%).
- A lifted print may use the `print` shadow (`0 24 60` in Dark Walnut at 14%).
- One object gets one shadow. Never stack shadows for decoration.
- Use a 1-unit Hairline border for frames and dividers.
