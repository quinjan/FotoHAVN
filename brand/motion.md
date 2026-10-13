# Motion

FOTOHAVN moves like a print developing or a curtain drawing: quiet, unhurried, and purposeful. Motion confirms and reveals. It never decorates.

## Timing

| Token | Value | Use |
| --- | --- | --- |
| `fast` | 150ms | Hover and focus feedback |
| `standard` | 240ms | State changes, crossfades, and arrivals |
| `reveal` | ≤ 600ms | Editorial entrance; paper reveal |
| `physical` | ≤ 700ms | Physical gestures such as turning a print over |
| `flash` | 320–380ms | A single simulated exposure flash at capture |
| `editorial` ease | `cubic-bezier(0.22, 1, 0.36, 1)` | Default for all motion |
| `physical` ease | `cubic-bezier(0.2, 0.75, 0.2, 1)` | Weighted print gestures |

In video, convert these values to frames at the composition's frame rate. At 30fps, `standard` is about 7 frames and `reveal` is about 18 frames. Scene timing in reels may be longer, but every element's entrance still follows the same ease and distances.

## Permitted movement

- Opacity fades and crossfades.
- Vertical translation of no more than 16 units on reveal.
- An image scale of no more than 1.015 on hover, or a slow settle from a slight crop on reveal.
- An action arrow nudging 2 units in its direction on hover.
- Slow, single-direction camera moves in video (a gentle push or drift), held long enough to read the photograph.
- Physical gestures that echo the booth: the curtain opening, a print lifting from the board and turning over, paper developing in, and one exposure flash at capture.
- User-driven exploration, such as rotating the 3D booth or browsing the guest album, at the user's pace.
- Background, border, and text-color transitions on sticky or persistent chrome.

## Prohibited

- Autoplay carousels, bouncing, elastic overshoot, or spring wobble.
- Parallax that fights scrolling.
- Cursor-follow effects, ornamental loops, or particle effects.
- Confetti, sparkle, glitch, or repeated strobe flashes.
- Layout shift while a state changes. A look crossfade keeps its dimensions stable.
- Fast cuts used as the only rhythm in brand video. Let at least one photograph hold.

## Reduced motion

Every interactive interpretation must respect the platform's reduced-motion preference:

- Remove transforms and scale.
- Make state changes immediate or near-immediate (1ms in CSS).
- Keep the information: content must still appear, and only the movement is removed.

Video and other non-interactive media can't detect the preference. They must avoid strobing or flashing more than three times per second (see [accessibility.md](accessibility.md)).
