# Asynchronous feedback

Delayed Open, Save, Start, Exit, Delete, Retry, and equivalent work use one initiating-action pattern.

| State | Contract |
|---|---|
| Idle | Normal label and enabled action. |
| Busy | Immediately change the initiating label to the active task, preserve width, show progress when useful, disable conflicts, and announce politely once. |
| Success | Navigate to the approved destination and announce the result. A non-critical Toast is optional only when the destination does not make success clear. |
| Error | Retain context, identify the problem, and offer Retry when the same action can recover. |

- Progress stays at the initiating button. Global loading banners and duplicate status rows are prohibited.
- Only the action doing work changes. Exit uses `Exiting event…`; Delete uses `Deleting event…`.
- Busy dismissal is suppressed on modal work. Recoverable failure replaces the busy state in place.
- Completion announcements use sentence form without an ellipsis, for example `Event started.`
- The state change is immediate; motion or a delayed spinner may not postpone feedback.
