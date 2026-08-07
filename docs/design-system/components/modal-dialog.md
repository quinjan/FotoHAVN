# Modal Dialog

Semantic ID: `component.modal-dialog`

## Contract

- One Calm footer anatomy serves Start, Exit, Delete, Discard, retained-context Retry, and equivalent confirmations.
- Anatomy: optional icon, optional eyebrow, level-1 title, consequence copy, optional neutral Event Identity panel, optional inline status, and horizontal action footer.
- Properties: title, body, identity, primary action, secondary action, semantic intent, action state.
- Confirmation and destructive are configurations, not separate shells. Use Primary for non-destructive confirmation and Destructive for Exit, Delete, and Discard.
- Place the safe action first and confirming action second. On recoverable failure retain context, announce the failure, and replace confirmation with Retry.

## Accessibility and behavior

Use WinUI `ContentDialog` or an equivalent truly modal surface. The visible title is the level-1 heading and dialog name. Initial focus enters on the safest action. Tab and Shift+Tab remain contained; background content is inert; close and Escape work only while idle; focus returns to the invoker. During busy work only the initiating label changes, both actions and close are disabled, and context remains visible.

## Responsive

Standard: centered, maximum 500 px width. Compact: at least 24 px viewport margins. Stress: 16 px margins and maximum available height; actions stack full width with safe action first. Identity, progress/recovery, and actions remain fixed; only supporting operator explanation may scroll. Exit Event confirmation is always zero-scroll.
