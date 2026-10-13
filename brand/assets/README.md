# Brand assets

Master brand marks for use in any medium. The rules are in [logo.md](../logo.md).

| File | Format | Notes |
| --- | --- | --- |
| `logo/fotohavn-wordmark-ebony.svg` | Outlined SVG | Wordmark in Ebony `#1E1A17`, for light backgrounds |
| `logo/fotohavn-wordmark-off-white.svg` | Outlined SVG | Wordmark in Off-white `#FBF8F2`, for dark backgrounds |
| `mark/fotohavn-booth-portal-master.png` | 1024 × 1024 PNG | Booth-portal mark, copied from `website/public/brand/favicon/` |

## How the wordmark was produced

The wordmark SVGs are outlines of `FOTOHAVN` set in Cormorant Garamond Medium (500), uppercase, at −0.05em tracking with font kerning. That is the same setting the website navigation uses. They were generated with [opentype.js](https://opentype.js.org/) from the `@fontsource/cormorant-garamond` Latin 500 file, so they render without the font installed. Cormorant Garamond is licensed under the SIL Open Font License 1.1, which permits outlining it into a logo.

To regenerate them, outline the same text and settings, and keep the viewBox tight to the glyph bounds (aspect ratio about 7.5:1).

## Gaps

- There is no vector master for the booth-portal mark yet (see [logo.md](../logo.md#open-items)).
- There are no print-ready (CMYK / PDF) exports yet. Produce them in the print interpretation after a proof (see [color.md](../color.md#print)).
