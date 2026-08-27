# FOTOHVN intro video-generator pack — 16:9

All three supplied keyframes are exactly `1920 x 1080` (`16:9`). They are a separate animation-source pack; the browser prototype keeps its existing `16:10` viewport masters unchanged.

## Files and sequence

1. `01-exterior-closed-1920x1080.png` — full closed booth; start frame for clip 1.
2. `02-threshold-left-wall-1920x1080.png` — curtain-open, left-biased doorway threshold; end frame for clip 1 and start frame for clip 2.
3. `03-left-wall-paired-lights-1920x1080.png` — frontal left-wall assembly with a black physical screen and matched lights; end frame for clip 2.

Generate two image-to-video clips rather than one long morph:

- Clip 1: frame 01 → frame 02, approximately 3.0–3.5 seconds.
- Clip 2: frame 02 → frame 03, approximately 3.0–3.5 seconds.

The video model should animate only the physical movement and camera path. Do not ask it to invent or render the website. The live homepage is composited into the black physical screen and then expanded to the live page by the browser prototype.

## Continuity constraints

- One uninterrupted forward camera move; no cuts, dissolves, morphs, or teleporting.
- The entrance curtain opens left-to-right and gathers on the right.
- The camera immediately biases toward the booth's left-hand interior wall.
- Never reveal the right/rear guest background.
- Preserve walnut geometry, cream fabric, warm mall lighting, camera, screen, control, and the two matched vertical lights.
- Keep the final screen pure black and stable for browser compositing.
- No people, hands, flashes, added text, logos, props, or screen content.

## ImageGen preparation prompts

The pack was prepared with the built-in ImageGen image-edit mode. Each approved source was used as a reference and horizontally outpainted before a deterministic export to `1920 x 1080`.

### Frame 01

> IMAGE EDIT — HORIZONTAL OUTPAINT ONLY. Use the supplied approved closed-photo-booth frame as the immutable center source. Produce a clean cinematic 16:9 landscape frame intended for a 1920x1080 video keyframe. Preserve every original booth detail, object, letter, proportion, material, lighting direction, curtain shape, and camera perspective exactly; do not redraw, restyle, move, enlarge, shrink, or crop the booth. Keep the complete booth, illuminated PHOTOBOOTH sign, curtain, lower feet, and floor visible. Extend only the mall environment to the left and right with seamless matching warm architectural walls, ceiling coves, floor, railing, plants, and ambient light. Add no people, no new signs, no logos, no text, no props, and no motion blur. The result must look like the same still camera frame with a wider field of view, centered and symmetrical enough for first-frame/last-frame video interpolation.

### Frame 02

> IMAGE EDIT — HORIZONTAL OUTPAINT ONLY. Use the supplied approved photo-booth threshold frame as the immutable center source. Produce a clean cinematic 16:9 landscape frame intended for a 1920x1080 video keyframe. Preserve the original camera position and every visible source detail exactly: the warm mall sliver at far left, walnut booth doorway and paneling, dark interior, shallow wooden step, partial black screen and camera lens, the single illuminated vertical light just inside the opening, and the heavy cream curtain sweeping in from the right. Do not redraw, restyle, move, scale, brighten, crop, reveal, or add any booth equipment. Keep the mysterious right/rear interior concealed. Extend only the far left and far right edges seamlessly to make a wider field of view, continuing the matching mall architecture on the left and cream curtain folds/dark doorway context on the right. Add no people, no text, no signs, no new lights, no logos, no props, and no motion blur. This is the exact middle continuity keyframe between the closed exterior and the final interior wall.

### Frame 03

> IMAGE EDIT — HORIZONTAL OUTPAINT ONLY. Use the supplied approved paired-light interior wall frame as the immutable center source. Produce a clean cinematic 16:9 landscape frame intended for a 1920x1080 video keyframe. Preserve the original frontal camera position and every source detail exactly: dark walnut wall grain, black rectangular screen, camera lens and curved LOOK HERE lettering above it, round metal control below, two identical illuminated vertical light bars symmetrically flanking the screen, cream curtain slivers at the edges, and the warm wooden doorway trim. Do not redraw, restyle, move, scale, brighten, crop, duplicate, remove, or add any equipment. Extend only the far left and far right edges seamlessly, continuing the same curtain-edge and dark-walnut doorway environment while keeping the equipment cluster centered and the geometry perfectly frontal. Add no people, no text changes, no logos, no props, no extra lights, no screen content, and no motion blur. This is the exact final keyframe onto which the website UI will later be overlaid in the browser; the screen must remain pure black.
