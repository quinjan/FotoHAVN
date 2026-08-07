# Status Callout

Semantic ID: `component.status-callout`

## Contract

- Anatomy: optional icon, optional title, required detail, optional action.
- Variants: neutral, info, success, warning, danger.
- Use for larger, blocking, multi-condition, or recoverable status. Prefer Inline Status for a single field condition.
- Errors name what happened and the next available action; raw exception strings are prohibited.

## Accessibility and behavior

Use a bordered container with `TextBlock` content and an optional Action Button. Associate it with the affected region. Announce blocking activation assertively; updates within an existing recoverable state do not duplicate the whole message.

## Responsive

Content wraps. The action remains at least 48 px and follows the message in reading order.
