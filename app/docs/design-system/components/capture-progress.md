# Capture Progress

Semantic ID: `component.capture-progress`

## Contract

- V1 is fixed to four Captures.
- Properties: active Capture (1–4), phase (not started, active, complete).
- States: not started, Capture 1–4 active, complete; each step is upcoming, active, or completed.
- Active and completed meaning uses shape, stroke, and contrast rather than color alone. Countdown is a separate overlay.

## Accessibility and behavior

Expose one progress concept and announce `Photo 2 of 4` in guest language. Individual decorative steps are hidden when their semantics would duplicate the group value. Preserved progress remains visible in Operator Assistance.

## Responsive

All four positions remain visible. Supporting text may collapse to the numbered indicator in Stress mode.
