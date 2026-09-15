# Experience story refinement

final result: passed

The selected Option 2 layout remains unchanged. This refinement replaces the first two material-only images and sharpens the final printed-memory message in response to the user's 2026-09-14 feedback.

## Delivered

- **Room to be yourself.** The active `candid-v2*.webp` uses the user's supplied two-person candid as its identity, clothing, expression, and mood reference. The 3D booth model fixes the spatial logic: the women sit on the right-wall bench facing the opposite camera/screen wall, with the fixed pleated cream backdrop directly behind them. A camera-facing 4:3 crop and a restrained 1.1× layout crop keep the women dominant; the camera, screen, doorway, and entry curtains remain outside the frame. The earlier `candid*.webp` remains recoverable but is unused.
- **A look that feels like you.** `look-prints*.webp` shows four physical FOTOHAVN strips featuring one fictional Filipino guest. Their full-bleed templates, wordmark placement, rotated monochrome treatment, sepia finish, and patterned high-contrast finish follow the four supplied real-print references without reusing customer identities.
- **A memory made to last.** `keepsake-reunion*.webp` shows three fictional Filipino friends laughing together in a warm café while revisiting old FOTOHAVN strips. Their expressions and gestures carry the story; the prints remain a secondary reminder of a moment that lasted. Supporting line: `A FOTOHAVN moment, printed to last.`

The active first photograph is a generated reference-led reconstruction of the people in the user-supplied image. Its structure references were `.handoffs/booth-model-corrections/desktop-bench.png` and `desktop-capture-wall.png`, supported by the model's documented bench/backdrop/camera orientation. The built-in image generation tool produced the PNG master; the project preparation script produced static responsive WebP families at 128, 256, 320, 384, 640, 960, and 1400 pixels.

## Final prompts

1. **Candid v2:** Preserve the two women, recognizable faces, hair, clothing, jewelry, seated closeness, and laughter from the user's reference. Recompose from the booth camera's viewpoint as a tight horizontal 4:3, chest-up two-shot. Place the model-accurate fixed cream pleated backdrop directly behind them; keep the opposite camera/screen wall, entry, doorway, print slot, mirror, signs, text, and logos outside the image. Retain warm documentary light, realistic anatomy, and natural skin texture.
2. **Look prints:** Preserve the hands-and-four-prints composition and use one fictional Filipino guest consistently across all 16 panels. Use the four supplied real prints only for their full-bleed layout, wordmark placement, orientation, backdrop, and photographic filter. The final approved revision removes inset borders, overlays FOTOHAVN across the photo-two/photo-three junction on the first two strips, and corrects the rightmost bottom portrait to the same generated identity.
3. **Café reunion:** Preserve three fictional Filipino friends leaning together around a dark walnut table, laughing as one points to an old FOTOHAVN strip. Move the scene into a warm café with window daylight, a brass pendant, dark timber, and a softly blurred counter. Keep expressions and hands dominant, with the prints limited to the lower foreground as a secondary reminder.

## Verification

- Desktop browser at 1280 CSS pixels: the new two-person frame keeps both faces complete, the cream backdrop reads clearly, and the opposite control wall is absent. The 5/2/5 image band remains intact.
- Mobile browser at 320×720: both faces and shoulders remain complete after the tighter crop, with the backdrop visible across the frame. The active image decoded at 272×203 CSS pixels; document `scrollWidth` equaled `clientWidth` (305 pixels after the scrollbar gutter).
- A fresh browser tab reported no warning/error console entries after marking the new lead image eager for direct Experience visits.
- The approved `look-prints` master was rendered in the Experience section at 1440×1000 and 320×720. Desktop preserves the four treatments within the narrow editorial column; mobile uses a source-matched 2:3 portrait frame and shows the complete hands-and-strips composition. The browser selected the 256px and 320px responsive WebP derivatives, respectively, with no warning/error console entries.
- The approved `keepsake-reunion` master was rendered at the same desktop and mobile viewports. Desktop selected the 640px derivative and preserved all three friends, hands, café context, and secondary prints; mobile selected the 320px derivative and showed the complete 4:3 composition. Neither viewport overflowed, and warning/error console reads were empty.
- `npm run lint` passed. TypeScript passed through the production build. The final `npm run build` passed and generated `/` and `/online`. The 12 editorial-lift regressions passed.

Latest persisted candid evidence remains `desktop-candid-v2.png` and `mobile-320-candid-v2.png`; the newer browser captures are attached to their implementation tasks. Image-generation masters are retained in the parent online-photobooth handoff as `asset-candid.png`, `asset-candid-v2.png`, `asset-look-choice.png`, `asset-look-prints.png`, and `asset-keepsake-reunion.png`. No deployment or commit was requested.
