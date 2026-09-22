# Text Field

Semantic ID: `component.text-field`

## Contract

- Anatomy: required visible label, input, optional helper/status slot.
- Properties: label, value, placeholder, required, helper text, semantic condition.
- States: default, focus, populated, disabled, invalid, unavailable.
- Validation states the problem and remedy and never relies on color alone. Labels do not disappear when a value is present.

## Accessibility and behavior

Use WinUI `TextBox`. Associate the visible label and status programmatically. Invalid submission moves focus to the first invalid field or its summary; asynchronous failure announces without stealing focus. Do not repeat role or state in the accessible name.

## Responsive

The field fills its available column. Labels and messages wrap; the input and status are revealed when focus or validation changes in a scrollable operator surface.
