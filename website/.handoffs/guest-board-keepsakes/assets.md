# Board keepsake assets

Built-in ImageGen created four decorative raster assets for the existing guest board. Original PNGs are preserved alongside this file. `scripts/prepare-board-keepsakes.mjs` creates 480px-wide alpha-preserving WebPs in `public/images/guest-board/`. No guest photographs or testimonials were generated.

## Delivered files

| Runtime file | Size | Bytes |
| --- | --- | --- |
| `C:/Quinjan/Repos/FotoHAVN/website/public/images/guest-board/teddy-charm.webp` | 480x720 | 74878 |
| `C:/Quinjan/Repos/FotoHAVN/website/public/images/guest-board/bunny-charm.webp` | 480x720 | 73850 |
| `C:/Quinjan/Repos/FotoHAVN/website/public/images/guest-board/ribbon-keepsake.webp` | 480x525 | 54618 |
| `C:/Quinjan/Repos/FotoHAVN/website/public/images/guest-board/ticket-keepsake.webp` | 480x285 | 28678 |

All source PNGs have an alpha channel and are non-opaque, checked with Sharp metadata/stats. Transparency is preserved during WebP resizing; no background replacement or manual cutout was performed. Source dimensions: teddy/bunny 1024x1536, ribbon 1199x1312, ticket 1626x967. Runtime total: 232024 bytes. Original files remain both in the generated-images directory and as named source PNGs beside this report.

## teddy-charm

Runtime: `public/images/guest-board/teddy-charm.webp`.

```text
Use case: product-mockup
Asset type: one small photoreal decorative website object, displayed at approximately 70–160 CSS pixels on a grey metal photo board.
Primary request: One small vintage caramel-brown teddy bear plush keychain, full body hanging from a short brass ball chain and one small brass ring. Rounded ears, tiny dark stitched eyes, subtly worn curly mohair, soft stuffed paws, a narrow ivory cotton ribbon tied at its neck. A sincere keepsake with physical fabric detail, not a cartoon character.
Composition: isolated single object centered, full silhouette visible with small even margins, straight-on frontal view as though pinned to a vertical board. Soft warm light from upper left. Muted brown, cream and antique brass palette. Crisp realistic material detail.
Background: genuinely transparent alpha outside the object, including through any chain/ring openings. No painted checkerboard. If transparency is unavailable, use perfectly uniform pure white #FFFFFF behind the object; absolutely no grid, texture, grey backdrop, horizon, floor or vignette.
Constraints: no people, photographs, photo board, furniture, extra objects, logos or watermark. Avoid cartoon proportions, neon colors and plastic toy gloss.
```

## bunny-charm

Runtime: `public/images/guest-board/bunny-charm.webp`.

```text
Use case: product-mockup
Asset type: one small photoreal decorative website object, displayed at approximately 70–160 CSS pixels on a grey metal photo board.
Primary request: One small warm taupe bunny rabbit stuffed plush keychain, full body hanging from a short brass ball chain and small brass ring. Two long floppy ears, stitched dark eyes and a little stitched nose, soft slightly worn linen plush texture, a tiny muted cocoa satin bow at the neck. A sincere handmade-looking keepsake, not a cartoon character.
Composition: isolated single object centered, full silhouette visible with small even margins, straight-on frontal view as though pinned to a vertical board. Soft warm light from upper left. Muted brown, cream and antique brass palette. Crisp realistic material detail.
Background: genuinely transparent alpha outside the object, including through any chain/ring openings. No painted checkerboard. If transparency is unavailable, use perfectly uniform pure white #FFFFFF behind the object; absolutely no grid, texture, grey backdrop, horizon, floor or vignette.
Constraints: no people, photographs, photo board, furniture, extra objects, logos or watermark. Avoid cartoon proportions, neon colors and plastic toy gloss.
```

## ribbon-keepsake

Runtime: `public/images/guest-board/ribbon-keepsake.webp`.

```text
Use case: product-mockup
Asset type: one small photoreal decorative website object, displayed at approximately 70–160 CSS pixels on a grey metal photo board.
Primary request: One loosely hand-tied cocoa-brown cotton ribbon bow, soft wrinkled fabric, two uneven long trailing ends and a tiny dull brass safety pin through its knot. Natural asymmetry and fine woven textile detail. One object only.
Composition: isolated single object centered, full silhouette visible with small even margins, straight-on frontal view as though pinned to a vertical board. Soft warm light from upper left. Muted brown, cream and antique brass palette. Crisp realistic material detail.
Background: genuinely transparent alpha outside the object, including through any chain/ring openings. No painted checkerboard. If transparency is unavailable, use perfectly uniform pure white #FFFFFF behind the object; absolutely no grid, texture, grey backdrop, horizon, floor or vignette.
Constraints: no people, photographs, photo board, furniture, extra objects, logos or watermark. Avoid cartoon proportions, neon colors and plastic toy gloss.
```

## ticket-keepsake

Runtime: `public/images/guest-board/ticket-keepsake.webp`.

```text
Use case: product-mockup
Asset type: one small photoreal decorative website object, displayed at approximately 70–160 CSS pixels on a grey metal photo board.
Primary request: One small aged cream admission ticket stub, horizontal rectangle about 2.3 times as wide as tall, scalloped short edges and tiny perforations, subtle folded corner and paper wear. The ONLY printed text is 'a little memory' in small dark-brown serif type. A single tiny pressed tan daisy lies diagonally over one end of the ticket, secured by a short translucent washi tape strip. A quiet intimate paper keepsake.
Composition: isolated single object centered, full silhouette visible with small even margins, straight-on frontal view as though pinned to a vertical board. Soft warm light from upper left. Muted brown, cream and antique brass palette. Crisp realistic material detail.
Background: genuinely transparent alpha outside the object, including through any chain/ring openings. No painted checkerboard. If transparency is unavailable, use perfectly uniform pure white #FFFFFF behind the object; absolutely no grid, texture, grey backdrop, horizon, floor or vignette.
Constraints: no people, photographs, photo board, furniture, extra objects, logos or watermark. Avoid cartoon proportions, neon colors and plastic toy gloss.
```
