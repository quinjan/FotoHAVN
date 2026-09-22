# design-v1.0.1 manual procedures

These named procedures are evidence endpoints for the traceability contract. Record operator, environment, timestamp, result, and attachments for every execution.

## MANUAL-SEMANTIC-RESOURCE-AUDIT

Compare each semantic ID, XAML key, WinUI type, and dictionary owner against mapping.json; record every mismatch.

## MANUAL-VISUAL-EQUIVALENCE

Capture the production composition in the pinned environment and review target, actual, and diff at 100%; no unexplained visible difference passes.

## MANUAL-KEYBOARD-JOURNEY

Traverse every reachable action using keyboard only; verify logical order, visible focus, containment, restoration, and Escape behavior.

## MANUAL-NARRATOR-JOURNEY

Run the named journey with Narrator and record spoken names, roles, states, headings, status announcements, and reading order.

## MANUAL-RESPONSIVE-REFLOW

Review all four responsive-risk viewports for clipping, overlap, scroll policy, focus reachability, and essential-content priority.

## MANUAL-TOUCH-TARGETS

Measure operator and guest targets and verify the approved 48px and 64px minimums plus separation.

## MANUAL-HIGH-CONTRAST

Verify critical state, focus, action, and overlay meaning in Windows High Contrast without relying on custom color alone.

## MANUAL-REDUCED-MOTION

Verify progress, dialogs, guarded holds, Capture feedback, and Photo Strip transitions with reduced motion enabled.

## MANUAL-UIA-CONTRACT-REVIEW

Until the shared Windows UI verification host lands, inspect the named UI Automation contract against production controls and attach the inspection record.

## Shared verification identifiers

The following design-system and UI Automation identifiers resolve to **MANUAL-UIA-CONTRACT-REVIEW** until their named automated suite is implemented by the rollout batch. The identifier must remain on the result record so automated evidence can replace the manual endpoint without changing scenario identity.

- `DS-ASSISTANCE-KEYBOARD`
- `DS-ASSISTANCE-NARRATOR`
- `DS-ASSISTANCE-REFLOW`
- `DS-BUTTON-BUSY`
- `DS-BUTTON-KEYBOARD`
- `DS-BUTTON-TOUCH`
- `DS-CALLOUT-CONTRAST`
- `DS-CALLOUT-NARRATOR`
- `DS-CAMERA-ASPECT`
- `DS-CAMERA-NARRATOR`
- `DS-CAMERA-REFLOW`
- `DS-CAPTURE-PROGRESS-CONTRAST`
- `DS-CAPTURE-PROGRESS-NARRATOR`
- `DS-EVENTCARD-KEYBOARD`
- `DS-EVENTCARD-NARRATOR`
- `DS-EVENTCARD-REFLOW`
- `DS-EVENTCARD-TOUCH`
- `DS-FIELD-ERROR`
- `DS-FIELD-KEYBOARD`
- `DS-FOCUS`
- `DS-HEADER-NARRATOR`
- `DS-HEADER-REFLOW`
- `DS-ICON-NAME`
- `DS-ICON-TOUCH`
- `DS-MODAL-BUSY`
- `DS-MODAL-ESCAPE`
- `DS-MODAL-FOCUS`
- `DS-MODAL-NARRATOR`
- `DS-NARRATOR`
- `DS-PROGRESS-NARRATOR`
- `DS-READONLY-NARRATOR`
- `DS-REDUCED-MOTION`
- `DS-REFLOW`
- `DS-SELECT-ERROR`
- `DS-SELECT-KEYBOARD`
- `DS-SETUP-NARRATOR`
- `DS-SETUP-REFLOW`
- `DS-SETUP-VALIDATION`
- `DS-STATUS-ANNOUNCE`
- `DS-STATUS-CONTRAST`
- `DS-STRIP-ASPECT`
- `DS-STRIP-NARRATOR`
- `DS-STRIP-REFLOW`
- `DS-TOAST-FOCUS`
- `DS-TOAST-NARRATOR`
- `MANUAL-UIA-CONTRACT-REVIEW`
- `UIA-CAPTURE-CONTRACT`
- `UIA-CONFIRMATION-CONTRACT`
- `UIA-EVENT-SETUP-CONTRACT`
- `UIA-GUEST-START-CONTRACT`
- `UIA-GUEST-START-UNAVAILABLE-CONTRACT`
- `UIA-KEYBOARD-COMPLETE`
- `UIA-LIVE-REGION-EVENTS`
- `UIA-OPERATOR-ASSISTANCE-CONTRACT`
- `UIA-PHOTO-STRIP-CONTRACT`
- `UIA-RESPONSIVE-GEOMETRY`
- `UIA-SAVED-EVENTS-CONTRACT`
- `UIA-SURFACE-STRUCTURE`
