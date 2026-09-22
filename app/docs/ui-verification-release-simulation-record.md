# v1.0.1 simulated manual-gate record

Date: 2026-08-11

Issue: [#78](https://github.com/quinjan/FotoHAVN/issues/78)

Application commit: `df1265fe8f42dd33091f6ec3295f4ad2f366b73d`

This record documents engineering simulations of the remaining manual release
gates. It is deliberately not a manual pass record. Synthetic input, UI
Automation, static captures, and process observation do not replace a person
listening to Narrator or operating physical touch hardware.

The simulations ran on Windows build 26200. High Contrast used the built-in
Aquatic theme and was restored to None. Reduced motion used the Windows
client-area-animation policy with animations disabled and was restored to On.

## Shared semantic and target proxy

The approved 103-fixture run exposed all eight production surfaces and reported:

- 541 actionable element instances, all with accessible names;
- 533 keyboard-focusable element instances;
- 628 live-region events;
- zero failed UI Automation checks; and
- zero target-size failures.

This is supporting evidence for Narrator semantics, keyboard reachability, and
touch geometry. It does not prove spoken output, physical touch behavior, or a
complete keyboard traversal.

## Narrator proxy

Result: **partial simulation**.

Windows Narrator was started and one representative fixture was attempted for
each production surface. Seven of eight fixtures completed with 34 observed
live-region events, zero failed UI Automation checks, and zero semantic
violations. `capture.capture-1.standard` repeatedly lost its transient
verification window before sizing. Narrator was stopped afterward.

No audio oracle or human listener verified the actual spoken text, cadence,
interruptions, or pronunciation. `MANUAL-NARRATOR-JOURNEY` remains open.

Evidence: `artifacts/manual-simulation/issue-78/narrator-proxy`.

## Synthetic touch proxy

Result: **not executed at the application input boundary**.

The UI Automation evidence verified names, Invoke-compatible patterns, and
approved target geometry. Two `InjectTouchInput` attempts targeted the real
production **Touch to start** button, but Windows rejected both synthetic
contact packets before FotoHAVN received them. No physical touch digitizer was
available. `MANUAL-TOUCH-TARGETS` remains open.

Evidence request: `artifacts/manual-simulation/issue-78/touch-request.json`.

## High Contrast simulation

Result: **partial simulation with one focus finding**.

Windows High Contrast was genuinely enabled with the Aquatic theme. Seven of
eight 640 x 360 representative surface captures completed. Visual inspection
found their headings, actions, errors, busy states, hold feedback, and overlays
distinguishable without clipping or overlap. Six fixtures had zero semantic
findings. `guest-start.long-event-name-and-exit-hold.scale-200-stress-equivalent`
reported that focus did not match `PrimaryGuestActionWhenPresent`.
`capture.capture-4-countdown.scale-200-stress-equivalent` repeatedly lost its
verification window before sizing. The original None theme was restored.

`MANUAL-HIGH-CONTRAST` remains open until the focus finding is resolved and the
missing Capture state is executed and reviewed.

Evidence: `artifacts/manual-simulation/issue-78/high-contrast`.

## Reduced-motion simulation

Result: **partial simulation with one focus finding**.

Windows client-area animations were genuinely disabled. Four of five
motion-sensitive fixtures completed. Event Setup saving, Photo Strip returning,
and delete-busy Confirmation had zero semantic findings.
`guest-start.exit-holding.standard` reported that focus did not match
`PrimaryGuestActionWhenPresent`. `capture.countdown-1.standard` repeatedly lost
its verification window before sizing. Windows Animation effects was restored
to On.

Static settled captures cannot independently prove animation timing or motion
cadence. `MANUAL-REDUCED-MOTION` remains open.

Evidence: `artifacts/manual-simulation/issue-78/reduced-motion`.

## Keyboard-stress proxy

Result: **partial simulation**.

Seven of the eight 640 x 360 stress fixtures reran with zero focus, target-size,
or responsive-geometry findings. The Capture countdown stress fixture repeatedly
lost its transient verification window before sizing. Its prior approved
103-fixture result has zero focus and geometry findings, but this run did not
re-execute it. The host verifies focus policy and reachability; it does not
substitute for an observed Tab, Shift+Tab, Enter, Space, arrow-key, and Escape
journey. `MANUAL-KEYBOARD-JOURNEY` remains open.

Evidence: `artifacts/manual-simulation/issue-78/keyboard-stress`.

## Disposition

These simulations add useful regression evidence but do not complete the five
manual acceptance records. They also expose two items to resolve before manual
sign-off: the Guest Start focus mismatch under High Contrast/reduced motion and
the Capture verification-window lifecycle race.
