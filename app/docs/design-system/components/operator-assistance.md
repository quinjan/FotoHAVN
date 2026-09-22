# Operator Assistance

Semantic ID: `component.operator-assistance`

## Contract

- Properties: cause (Camera, storage, Photo Strip), stage (before admission, during Captures, preparing Photo Strip), durable progress (0–4), recovery (Retry, setup correction, exit-only), action state (idle, retrying, recovered, retry failed).
- Always explain what happened, what progress is safe, and what the operator should do next. Use bounded plain-language copy, never raw exceptions.
- Guest-facing controls are suppressed. Retry appears only when rechecking can recover the Guest Cycle; setup correction uses Exit Event.
- Before admission, the semantic state remains Guest Start unavailable, but it uses the same centered Operator Assistance panel without the four-position preserved-Capture progress.

## Accessibility and behavior

Activation is an assertive announcement. Preserve the Capture count visually and programmatically. Retry progress stays within its button; repeated failure updates the existing region. Keep `Please call the operator`, the affected condition, preserved count, and recovery action in every mode.

## Responsive

Zero-scroll with fixed brand header and guarded Exit Event. Guest Cycle headers omit the Event name. Compact reduces spacing/icon size; Stress removes decorative eyebrow and secondary guidance first.
