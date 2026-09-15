# Seven Output-based filters

Source: https://sites.google.com/view/fotohavnph/home — Looks > The Output carousel. All seven original reference files are alongside this document as output-1.jpg through output-7.jpg.

These are visual approximations requested by the user, not recovered original booth LUTs. Lighting, background, exposure and source processing also affect each published sample. The four named looks section does not establish seven separate official presets. User explicitly requested one selectable treatment based on each of the seven Output samples.

| Output | Filter | Description |
| --- | --- | --- |
| 1 | Monochrome | Black and white with deep blacks and strong contrast. |
| 2 | Naturale | Original colors. No filter. |
| 3 | Soft Monochrome | Black and white with softer contrast. |
| 4 | Soft Color | Slightly muted colors and softer highlights. |
| 5 | Sepia | Soft brown tones with faded contrast. |
| 6 | Warm Monochrome | Black and white with a light warm tint. |
| 7 | Classic | Warm colors with a golden tint. |

Implementation uses sourceOutput metadata in presets.ts, in carousel order. No source background pattern or template color is used as a filter. All processing is confined to photo slots; blank windows, frame artwork and lettering remain unchanged. Naturale is the unchanged default. Original photo blobs remain intact and each filter is applied from source, never cumulatively.

Verification: 32 focused tests passed, including seven distinct outputs, exact Naturale passthrough, alpha preservation, monochrome neutrality, reduced contrast for Soft Monochrome, subtle warm tint compared with Sepia, crop/slot confinement, and preserved session sources. Browser confirmed seven options, selection/preview updates, 320px containment, and a Warm Monochrome downloadable PNG. Browser errors/warnings: none. Evidence: filters-seven-final.png.
