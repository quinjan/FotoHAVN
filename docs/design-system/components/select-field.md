# Select Field

Semantic ID: `component.select-field`

## Contract

- Anatomy: required visible label, selection control, optional helper/status slot.
- Properties: label, placeholder, selected value, choices, required, helper text, semantic condition.
- States: default, focus, populated, disabled, invalid, unavailable.
- Availability of a listed Camera is not readiness. Camera checking and failure belong to Setup Field Group feedback.

## Accessibility and behavior

Use WinUI `ComboBox`. Expose the selected value natively, associate visible label/status, support expected arrow-key behavior, and keep the focused option visible. The accessible name does not duplicate the selected value.

## Responsive

The field fills its column. Drop-down bounds fit the workspace and never create horizontal page scrolling.
