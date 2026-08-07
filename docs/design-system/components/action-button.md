# Action Button

Semantic ID: `component.action-button`

## Contract

- Anatomy: label; optional leading icon; optional trailing directional/disclosure icon; optional busy indicator.
- Properties: emphasis (`primary`, `secondary`, `tertiary`, `destructive`), audience (`operator`, `guest-prominent`), label, optional icons, async capability.
- States: idle, hover, pressed, focus, disabled; async actions also expose busy.
- A decision surface has at most one Primary action. Destructive is separate from Primary styling.
- Labels are concise verb phrases and remain stable when disabled. Busy preserves width, disables conflicts, and names the active task, for example `Starting event…`.

## Accessibility and behavior

Use a native WinUI `Button`. Minimum height is 48 px for operators and 64 px for guest-prominent actions. The accessible name contains the visible label. Enter, Space, pointer, and touch activate on release. Busy is exposed programmatically and announced once; focus remains visible when the button can receive it.

## Responsive

Do not shrink targets. Labels wrap only as a last resort and never truncate. Dialog actions stack full width in Stress mode with the safest action first.
