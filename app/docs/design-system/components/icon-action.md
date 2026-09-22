# Icon Action

Semantic ID: `component.icon-action`

## Contract

- Anatomy: 16, 20, or 24 px Windows-native icon inside a minimum 48 × 48 target; optional tooltip.
- Properties: semantic action, icon size, emphasis, enabled state.
- States: idle, hover, pressed, focus, disabled.
- It is never the only representation of a surface's primary action. Destructive actions remain spatially separated from routine actions.

## Accessibility and behavior

Use a native WinUI `Button` with a FontIcon, SymbolIcon, or approved vector asset. A concise verb-based accessible name and matching tooltip are mandatory, such as `Edit event` or `Delete event`. Do not announce the glyph. Activation occurs on release.

## Responsive

Keep the target at 48 × 48 in every mode. The visual glyph may not grow to fill its target.
