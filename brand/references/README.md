# References

Images that show the brand. References are **evidence, not authority**. The prose documents in [`brand/`](../README.md) are normative, and the live website is the reference implementation.

| Folder | Contains | Origin |
| --- | --- | --- |
| [photography/](photography/README.md) | Approved photographs by subject | Copies of images the live website uses |
| [looks/](looks/README.md) | Printed strips in different looks | Copies of images the live website uses |
| [applications/](applications/README.md) | Finished FOTOHAVN work | Screenshots of the live website and online booth |
| [mood/](mood/README.md) | Material world, palette, type, and brand overview | **Generated boards** composed from live imagery, tokens, and fonts |
| [do-dont/](do-dont/README.md) | Paired correct and incorrect examples | **Generated boards**. The "don't" sides are deliberately off-brand. |
| [logo/](logo/README.md) | Wordmark usage boards and archived mark concepts | Generated boards from `brand/assets/` plus archived concepts |

## Rules

- Photographs here are reduced copies, at most 1600px on the long edge. Originals stay where the website or the reel project keeps them.
- Generated boards are rendered from [`_sources/boards.html`](_sources/boards.html). To change a board, edit the source and re-render it (see below). Do not edit the PNGs by hand.
- Add a reference only if it is approved FOTOHAVN material or a clearly labeled generated board. Record its source in the folder README.
- Follow [photography.md](../photography.md#people-and-consent) for any image that shows guests.

## Re-rendering the generated boards

1. Serve the repository root over HTTP, for example `python -m http.server 8765 --directory .` from the repository root.
2. Open `http://127.0.0.1:8765/brand/references/_sources/boards.html` in a Playwright-controlled browser at least 1680px wide, with the repository root as the working directory. Add a query string such as `?v=2` to avoid stale caches.
3. Run [`_sources/render-boards.playwright.js`](_sources/render-boards.playwright.js) with that page. It waits for the brand fonts, fails if they are missing, and writes every board to its folder.
4. Review every changed PNG before committing it.

The boards load Cormorant Garamond and Manrope from Google Fonts, so rendering needs network access.
