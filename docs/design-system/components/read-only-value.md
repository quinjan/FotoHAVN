# Read-only Value

Semantic ID: `component.read-only-value`

## Contract

- Anatomy: visible label, non-editable value, optional helper/status slot.
- Properties: label, value, semantic condition.
- States: default, focus when actionable/copyable, disabled, unavailable.
- Use for fixed choices such as `No printer`; do not style it as an editable field.

## Accessibility and behavior

Use a `TextBlock` within a labeled bordered container, or a read-only `TextBox` only when selection/copy is intentionally supported. Expose the label/value relationship and do not imply editability.

## Responsive

Values wrap without clipping. Full consequential identifiers may wrap only at safe character boundaries.
