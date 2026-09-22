# Components

V1 publishes exactly these 17 families. Full screens remain compositions. Components consume semantic tokens only; hover, pressed, focus, disabled, and busy map to WinUI VisualStates rather than developer-set styling options.

| Semantic ID | Family | Tier |
|---|---|---|
| `component.action-button` | [Action Button](action-button.md) | Reusable control |
| `component.icon-action` | [Icon Action](icon-action.md) | Reusable control |
| `component.text-field` | [Text Field](text-field.md) | Reusable control |
| `component.select-field` | [Select Field](select-field.md) | Reusable control |
| `component.read-only-value` | [Read-only Value](read-only-value.md) | Reusable control |
| `component.inline-status` | [Inline Status](inline-status.md) | Feedback control |
| `component.status-callout` | [Status Callout](status-callout.md) | Feedback control |
| `component.progress-indicator` | [Progress Indicator](progress-indicator.md) | Feedback control |
| `component.toast` | [Toast](toast.md) | Feedback control |
| `component.modal-dialog` | [Modal Dialog](modal-dialog.md) | Shared shell |
| `component.app-header` | [App Header](app-header.md) | FotoHAVN composite |
| `component.event-card` | [Event Card](event-card.md) | FotoHAVN composite |
| `component.setup-field-group` | [Setup Field Group](setup-field-group.md) | FotoHAVN composite |
| `component.camera-viewport` | [Camera Viewport](camera-viewport.md) | FotoHAVN composite |
| `component.capture-progress` | [Capture Progress](capture-progress.md) | FotoHAVN composite |
| `component.operator-assistance` | [Operator Assistance](operator-assistance.md) | FotoHAVN composite |
| `component.photo-strip-result` | [Photo Strip Result](photo-strip-result.md) | FotoHAVN composite |

## Shared state rules

Interactive controls expose only meaningful capabilities. Presentation precedence is unavailable, disabled, busy, pressed, hover, idle. Focus is an independent overlay, as are semantic conditions such as invalid or warning. Operator controls and dialog actions are at least 48 px high; guest-prominent actions are at least 64 px high. V1 has no compact interactive variant.
