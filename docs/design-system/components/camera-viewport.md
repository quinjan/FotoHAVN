# Camera Viewport

Semantic ID: `component.camera-viewport`

## Contract

- Modes: Setup Preview and Guest Capture.
- Properties: mirroring, status label, Capture-area guide, overlay visibility.
- States: connecting, live, countdown, flash, Capture saved, unavailable.
- Setup Preview shows the live feed, 3:2 Capture-area guide, Camera status, and mirrored indicator. Guest Capture adds Capture Progress, countdown, flash, and saved overlays.
- The feed retains its intended aspect/crop and is never stretched or responsively recropped. Camera/storage failures route to Operator Assistance.

## Accessibility and behavior

Expose `Camera preview` with `Live` or `Unavailable` as state. Expose `Photo capture area` only when framing guidance is useful. Mirrored indicators, flash, decorative borders, and guest imagery are otherwise hidden; FotoHAVN never attempts to describe guests or surroundings.

## Responsive

Capture is the responsive anchor. Standard centers 16:9 below the header; Compact expands within available space; Stress uses protected overlays nearly edge-to-edge. Countdown clamps so numerals never clip.
