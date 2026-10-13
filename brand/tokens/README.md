# Brand tokens

[`tokens.json`](tokens.json) is the machine-readable form of the FOTOHAVN brand values. It uses the [Design Tokens Community Group](https://www.designtokens.org/) format so any tool or person can read it.

## What the file is

- **Reference values, not an implementation.** Nothing builds from this file. Each interpretation (the website, a reel, a deck, a poster, and later the desktop app) reads it and writes its own implementation in its own tooling.
- **Medium-agnostic.** Pixel values describe the screen reference at 1×. Print and video convert by role (for example, "body text" or "display") rather than copying pixels. See [mediums.md](../mediums.md).
- **Always current.** The file on the default branch is the current brand. It has no version numbers.

## Groups

| Group | Contents |
| --- | --- |
| `color` | Nine primitive colors. Each has its HEX `$value` and, under `$extensions["com.fotohavn.brand"]`, its 0–255 `rgb` and naive `cmyk` percentages. |
| `semantic` | Role aliases: `surface` levels 0–4, `text`, `action`, `accent`, and `border`. Interpretations should use roles before primitives. |
| `font` | Family stacks with fallbacks, and the three permitted weights. |
| `typography` | Composite type roles from `eyebrow` to `display`, plus `emphasis` (the italic turn). Casing and font style are in `$extensions`. |
| `space` | The 4px base unit and its scale. |
| `radius`, `stroke`, `shadow` | Shape, line weights, focus geometry, and the single print shadow. |
| `motion` | Durations, the editorial ease, maximum reveal distance, and maximum image scale. |

## Color values per medium

| Medium | Use |
| --- | --- |
| Screen (web, app, video, slides) | HEX or `rgb` in sRGB. |
| Print | `cmyk` only as a starting point. Convert from sRGB with the printer's ICC profile and approve a physical proof. See [color.md](../color.md#print). |

## Interpreting the tokens

1. Map every semantic role you use to a primitive through this file. Do not invent new hues.
2. Keep names recognisable, for example `--color-ebony`, `Ebony`, or `brand.ebony`.
3. You may add medium-specific values such as breakpoints, frame sizes, safe areas, or component sizes. Those belong to the interpretation, not to this file.
4. Record every deliberate departure in the interpretation's own "Brand interpretation" section. See [README.md](../README.md#interpretation-contract).

## Changing a token

To change a brand value, edit this file and the matching prose document in the same change. Then update each interpretation that the change affects, or record the drift there. The token file and its prose documents must not disagree.
