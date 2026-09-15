# Silver clip asset

## Final deliverable

- Asset: `public/images/guest-board/silver-clip.webp`.
- Dimensions: 865 x 1105 pixels; aspect ratio 0.783.
- Size: 809,192 bytes (full resolution, lossless WebP).
- Genuine alpha: **no**. This is the explicitly authorized opaque gray fallback after two built-in alpha requests failed.
- Selected fallback source: `clip-gray-source.png`, preserved unchanged at 1254 x 1254 pixels.
- Preparation: crop rectangle `left: 194, top: 75, width: 865, height: 1105`; lossless WebP, effort 6. No resizing or color removal.
- Verification: decoded output pixels match the same source crop exactly (`Buffer.compare` returned 0).
- The painted checkerboard is gone. The requested background was exactly `#8f8f8b`, but the generator returned a visually flat gray with slight pixel variation, approximately RGB 138-141 / 134-140 / 133-135 in sampled empty areas. It is not a mathematically uniform exact-color fill.
- The consuming page should treat this as an opaque tile. Original alpha requirement remains unmet and must not be described as passed.

## Initial attempt

Built-in ImageGen was used on 2026-09-10 after inspecting `approved-design.png` with `view_image`. The single generated clip visually follows the selected mock's round top opening, narrow neck, and flat lower clamp.

## Verification

- Source: `clip-source.png`, preserved unchanged from the built-in output.
- Requested dimensions: 512 x 512 pixels.
- Actual dimensions: 1254 x 1254 pixels.
- Sharp metadata: RGB, 3 channels, `hasAlpha: false`.
- Sharp pixel statistics: `isOpaque: true`.
- The visible checkerboard is painted into the pixels. This is not a transparent asset.
- No website asset was created from this initial checkerboard result.

## Exact prompt

```text
Use case: background-extraction
Asset type: small website raster photo clip, consumed at about 26 x 38 CSS pixels.
Input images: Image 1 is the edit source and shape/style reference. Isolate only the silver bulldog spring clip at the top center of the central cream testimonial card. Remove every other part of the supplied image.
Primary request: produce ONE refined photoreal brushed silver bulldog photo clip as a clean cutout on a genuinely transparent background with real alpha. Render the clip clearly in frontal view: a circular opening at the top surrounded by a stamped silver rim, a narrow straight neck beneath it, and a short flat clamp jaw across the bottom. Preserve the recognizable tall, narrow silhouette of the referenced clip, width about 0.65 times height.
Composition/framing: requested 512 x 512 pixel image, the single clip centered upright and fully visible, approximately 440 pixels tall with modest transparent padding. Only one clip.
Lighting/material: softly brushed silver metal, restrained highlights, neutral warm soft illumination matching the mock, delicate realistic metal contours. Photoreal product cutout with enough crisp silhouette and tonal contrast to read at tiny UI size.
Constraints: real transparent pixels outside the clip AND through the top circular opening; no opaque or white background; no checkerboard painted into the image; no gray backplate inside the opening; no board, photographs, paper, card, text, logo, label, watermark, or additional object. No cast shadow on any background and no giant surrounding shadow. Keep the clip frontal, not three-quarter perspective.
```

Built-in generation source: `C:/Users/QUINJ3875/.codex/generated_images/01a08957-63fa-7710-8238-54fb5bb1ff17/exec-45c92f19-9fa4-492b-9db2-0c14ef05f5bd.png`.

## Targeted alpha retry

The parent requested one targeted retry. `clip-alpha-retry-source.png` preserves that result: 1254 x 1254 pixels, RGB, 3 channels, `hasAlpha: false`, `isOpaque: true`, another painted checkerboard. It is not used by the website.

```text
Use case: background-extraction
Input images: Image 1 is the edit target, a single silver bulldog clip with an incorrect painted checkerboard background.
Change only the background and opening to genuine transparency. Keep the exact silver clip geometry, dimensions relative to itself, frontal perspective, brushed silver material, surface light and edge details unchanged.
Output a real RGBA PNG with an actual alpha channel. All pixels outside the clip silhouette must have alpha 0, and the open circular hole near the top must also have alpha 0. Keep smooth partially transparent anti-aliased edges on the metal. This is an image asset, so do not draw a visual representation of transparency. The checkerboard in the source is a mistake that must be removed; absolutely no gray and white grid or painted checkerboard may remain. No white, gray, or other opaque background may remain.
Requested canvas size 512 x 512 with the single full clip upright centered and modest transparent padding. No extra objects, text, shadows, or other visual changes.
```

## Authorized opaque fallback prompt

After the retry failed, the parent's instruction authorized the same clip on flat gray matching the board and a tightly trimmed opaque tile. Built-in ImageGen was used; no CLI fallback was used.

```text
Use case: precise-object-edit
Input images: Image 1 is the edit target showing one brushed silver bulldog spring photo clip.
Change only the backdrop: remove the gray and white checkerboard everywhere and replace it with one perfectly flat opaque warm gray color, exactly hex #8f8f8b, RGB(143,143,139). The same flat warm gray must also show through the round opening at the top. This is deliberately an OPAQUE asset tile, so absolutely no checkerboard or visual depiction of transparency.
Keep the exact single clip unchanged: frontal upright view, round top hole, narrow neck, flat horizontal clamp bottom, realistic refined brushed silver material and soft neutral warm light. Preserve its full silhouette and edges. Entire clip centered with modest padding on the requested 512 x 512 canvas.
The background must be uniform single color edge to edge with no texture, grain, noise, vignette, gradient, shadow, floor, wall, paper, board or other object. No extra clips, text, logo, watermark or other content. No cast shadow around the clip.
```
