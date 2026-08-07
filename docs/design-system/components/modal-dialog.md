# Modal Dialog

Semantic ID: `component.modal-dialog`

## Contract

- One centered, icon-led decision anatomy serves Start, Exit, Delete, Save, Discard, retained-context Retry, and equivalent confirmations.
- Show Event name and full Event ID only when they help distinguish the target of the action. Start, Save, Delete, and retained-context Retry keep the identity section; Exit and Discard omit it because their consequence copy already identifies the scope.
- Decision anatomy: semantic icon, level-1 title, optional one-sentence consequence, divider-separated Event identity, optional inline status, and a full-width two-action rail. Confirmation eyebrows and bordered identity cards are not used.
- Properties: title, body, identity, primary action, secondary action, semantic intent, action state.
- Confirmation and destructive are configurations, not separate shells. Use Primary for non-destructive confirmation and Destructive for Exit, Delete, and Discard.
- Place the safe action first and confirming action second. On recoverable failure retain context, announce the failure, and replace confirmation with Retry.
- Start and Save use neutral icon color with Primary confirmation. Exit, Delete, and Discard use the destructive semantic icon and Destructive confirmation without changing the shared layout.
- Success and information acknowledgements use one shared, centered configuration: semantic status icon, level-1 title, one short sentence, and one full-width Primary action. They never repeat Event identity, show a Cancel action, or use a confirmation eyebrow.
- Use the success configuration only after work has completed. Use the information configuration for neutral facts that require acknowledgement but no decision. The action names the next destination, normally `Continue`.

## Accessibility and behavior

Use WinUI `ContentDialog` or an equivalent truly modal surface. The visible title is the level-1 heading and dialog name. Initial focus enters on the safest action; for a one-action acknowledgement, it enters on the Primary action. Tab and Shift+Tab remain contained; background content is inert; close and Escape work only while idle; focus returns to the invoker. During busy work only the initiating label changes, both actions and close are disabled, and context remains visible. Announce success or information once, politely, when the acknowledgement opens; the icon is redundant and hidden from accessibility.

## Responsive

Standard: centered, maximum 500 px width; acknowledgements use a narrower 440 px maximum. Compact: at least 24 px viewport margins. Stress: 16 px margins and maximum available height; actions stack full width with safe action first. Acknowledgements preserve the centered hierarchy and full-width action while reducing vertical spacing. Identity, progress/recovery, and actions remain fixed; only supporting operator explanation may scroll. Exit Event confirmation is always zero-scroll.
