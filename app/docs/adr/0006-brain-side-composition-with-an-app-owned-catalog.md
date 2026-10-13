---
status: accepted
date: 2026-10-13
---

# Brain-side composition with an app-owned catalog

The brain composes every Photo Strip with SkiaSharp from a catalog that the desktop app owns: a .NET copy of the website's four Templates and seven Looks. There is no parity test with the website for now, and the website is not edited (D8).

Replaces: the corresponding part of the deleted WinUI 3 app, whose Win2D compositor drew every Photo Strip at one fixed 600×1800 size with four Captures, with no Templates or Looks.

## Context

- The guest workflow follows the website's online booth (D10), so guests choose a Template and a Look (shown to them as a "filter") from the same set the website offers ([`presets.ts`](../../../website/src/components/onlinePhotobooth/presets.ts)).
- Template and Look are defined in `app/CONTEXT.md` (D25). D25 governs the vocabulary; this ADR governs the copied catalog data.
- Composition must run headlessly in tests and produce byte-stable output for golden-image tests ([ADR 0007](0007-unit-first-testing-with-real-camera-end-to-end.md)).
- The UI never touches image files ([ADR 0003](0003-react-ui-in-wpf-webview2-over-a-dotnet-brain.md)).

## Decision

**Catalog.** `FotoHavn.Catalog` holds the Templates and Looks as .NET records.

| Template | ID | Size | Photograph windows | Artwork |
| --- | --- | --- | --- | --- |
| Signature Strip | `signature-strip` | 600×1800 | 4 edge-to-edge, 600×450 | None; `FOTOHAVN` signature across the middle |
| Panorama Four | `panorama-four` | 1800×600 | 4 side by side, 450×600 | None; corner signature |
| Butter Gingham | `butter-gingham` | 600×1800 | 3, 547×522 | `butter-gingham.png` |
| Polka Keepsake | `polka-keepsake` | 600×1800 | 4, 513×390 | `polka-keepsake.png` |

- Template and Look IDs match the website's, so the same Template means the same thing everywhere.
- Looks follow the order in [`brand/photography.md`](../../../brand/photography.md): Monochrome, Naturale, Soft Monochrome, Soft Color, Sepia, Warm Monochrome, Classic. Naturale never changes a pixel.
- The two artwork PNGs are copied from `website/public/images/online-templates/` into `FotoHavn.Catalog/Assets/Templates/` with a copy of their `SOURCES.md` provenance note, and loaded as embedded resources.
- Other entries in the website's `presets.ts` that its current Template flow doesn't use (the Ivory, Archive, Walnut, and Gallery frames, and Long Strip and other plain designs), are left out of v1.
- An Event offers a subset of the built-in catalog with a default Template and a default Look (D13). Custom Templates, uploaded artwork, and custom strip text are out of v1.

**Compositor.**

- Inputs: a Template, a Look, and the arranged Capture files. Output: a lossless, opaque PNG at the Template's native size.
- Steps: fill the paper with Warm Ivory `#F3EBDD`; draw the artwork; center-cover crop each Capture to its window with high-quality sampling; apply the Look to that window only; draw the signature on artwork-free Templates.
- Signature: `FOTOHAVN` in Cormorant Garamond 500, white, 58 px centered on Signature Strip and 48 px right-aligned on Panorama Four, as the website draws it. Inside a Photo Strip, the Event's design takes priority over the interface palette ([`brand/mediums.md`](../../../brand/mediums.md)).
- Fonts: Cormorant Garamond and Manrope ship as OFL font files inside the app and load from embedded resources, never from the system.
- **Look kernels** port the website's `applyLook` operation for operation. JavaScript's `Math.round` rounds halves up, so the C# port uses `MidpointRounding.AwayFromZero` on non-negative values, never the default banker's rounding.
- Panorama Four keeps its native landscape orientation in the saved PNG; only the UI rotates its preview a quarter turn counterclockwise, as the website does.

**Previews.**

- Blank Template previews use `#E5E0D7` for empty windows, with the website's hairline rules between touching empty windows. They are composed once per Event start and are never saved as Event artifacts.
- Previews are composed on demand, cached by (Template, Look, arrangement revision), and served through `preview.fotohavn`. Look thumbnails on the Filters step use the first arranged Capture's crop at thumbnail size.
- The Keepsake step composes the final Photo Strip, which the Event store commits ([ADR 0005](0005-sqlite-metadata-with-file-artifacts-for-event-storage.md)).
- Target: a full Photo Strip in under 150 ms on the booth laptop; spike S3 measures it.

**No parity test (D8).** The website remains the reference for v1, but no automated test compares the app's catalog or output with the website's, and this work does not edit `website/`. M7 includes a manual visual comparison of all 28 Template × Look strips with the website's output as a reference check, not a parity requirement.

## Considered options

- **Composing in the UI** in WebView2 was rejected (D3, D7). Composition feeds a durable artifact, so it belongs to the brain and must be testable without a browser.
- **Importing the website's `presets.ts` or sharing a catalog package** was rejected for v1 (D8). The website is not edited, and the app owns its copy.
- **A parity test against the website** was deferred (D8).
- **Keeping the deleted app's Win2D compositor** was rejected; it depends on the WinUI 3 stack being replaced ([ADR 0003](0003-react-ui-in-wpf-webview2-over-a-dotnet-brain.md)) and supports one fixed four-Capture design only.

## Consequences

- The app's catalog can drift from the website's. Drift is caught only by the M7 manual comparison or a later decision to add a parity test or share the catalog.
- If Template and Look become shared language, they are promoted to the root `CONTEXT.md` in a later change (D25).
- Golden-image tests pin a PNG hash for every Template × Look from fixture photographs, and kernel tests cover the 0, 127.5, and 255 edge values and alpha preservation.
- The catalog carries its own copy of the artwork and its provenance note, which must be updated by hand if the website's artwork changes.
