# Inline Status

Semantic ID: `component.inline-status`

## Contract

- Anatomy: optional status icon and concise adjacent text.
- Variants: neutral, info, success, warning, danger.
- Use next to the affected control for checking, warning, validation, or recovery information.
- Successful setup fields remain silent; do not add passive `Ready` checks.

## Accessibility and behavior

Use text plus a non-color cue. Polite announcements cover progress and non-blocking updates; blocking failures are assertive. Announce each semantic transition once and never move focus. Icon and text are one message, not duplicated nodes.

## Responsive

Wrap beneath the affected control and remain visible when that control is focused. Repeated failure replaces the existing message rather than stacking.
