# Measured contrast pairs

Ratios use WCAG relative luminance calculations for the approved sRGB values in `tokens.json`.

| Foreground | Background | Ratio | Contract use |
|---|---|---:|---|
| `color.text.primary` | `color.surface.panel` | 17.77:1 | Normal text — pass |
| `color.text.muted` | `color.surface.panel` | 5.35:1 | Normal text — pass |
| `color.text.inverse` | `color.action.primary.idle` | 17.77:1 | Action label — pass |
| `color.text.inverse` | `color.action.destructive.idle` | 4.77:1 | Destructive action label — pass |
| `color.status.info.foreground` | `color.status.info.surface` | 5.64:1 | Status text — pass |
| `color.status.success.foreground` | `color.status.success.surface` | 4.76:1 | Status text — pass |
| `color.status.warning.foreground` | `color.status.warning.surface` | 6.09:1 | Status text — pass |
| `color.status.danger.foreground` | `color.status.danger.surface` | 8.57:1 | Status text — pass |
| `color.focus.ring` | `color.surface.panel` | 6.16:1 | Focus indicator — pass |
| `color.focus.ring` | `color.surface.canvas` | 5.69:1 | Focus indicator — pass |
| `color.border.strong` | `color.surface.panel` | 5.35:1 | Meaningful boundary — pass |
| `color.border.default` | `color.surface.panel` | 1.43:1 | Decorative separation only |

`color.border.default` and the subtle status-border tokens may not carry meaning or define a control boundary by themselves. Use `color.border.strong`, text, iconography, shape, or another tested non-color cue when the boundary communicates state or operability. Disabled components remain identifiable by content and shape even when inactive colors are exempt from WCAG contrast requirements.
