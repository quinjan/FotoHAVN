# Toast

Semantic ID: `component.toast`

## Contract

- Use only for non-critical confirmation such as `Event saved.`
- It never carries required instructions, blocking status, or recovery actions. Use Inline Status or Status Callout when the outcome affects the next action.
- Repeated equivalent confirmations deduplicate.

## Accessibility and behavior

Use a non-modal in-app notification surface with a polite announcement. It never steals focus. Keep it perceivable long enough and provide dismissal without making dismissal the only way to continue.

## Responsive

Fit within viewport margins, avoid covering sticky actions or guest progress, and wrap concise text without horizontal overflow.
