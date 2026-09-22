# Setup Field Group

Semantic ID: `component.setup-field-group`

## Contract

- Anatomy: required label, one field control, helper/status slot, optional dirty indicator.
- States: clean, dirty, checking, ready, invalid, unavailable.
- Successful fields are silent. Checking uses Inline Status. Invalid/unavailable states explain the problem and next action.
- Camera validation begins immediately after selection, briefly shows `Checking camera…`, clears silently when Eligible, and shows a field-level failure otherwise.
- Retry appears only when recovery can succeed without changing setup. Multiple blockers may use one setup-level Status Callout.
- Storage always identifies `C:\Program Files\FotoHAVN\Events` and requires at least 1 GB free.

## Accessibility and behavior

Associate label, field, helper, status, and invalid state. Validation reveals and focuses the affected field when triggered by submission; asynchronous checks announce without stealing focus.

## Responsive

Standard uses the adaptive split composition. Compact/Stress place groups in one scrollable column ordered Event name, Camera/status, 16:9 preview, Printer, Storage. Fixed header and 80 px footer remain visible.
