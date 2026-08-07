# Operator exit safeguard

The Guest Start header keeps a visible key-icon `Exit Event` control because exit is its only function.

1. Require a continuous 1.5-second hold from pointer, touch, Enter, or Space.
2. During hold, place the control on a bordered white surface, change the label to `Keep holding…`, show the refresh indicator on the right, and expose black determinate progress along the bottom edge.
3. Releasing early, moving pointer away, or losing capture cancels cleanly without opening confirmation.
4. On completion open the existing Exit confirmation with initial focus on `Keep event active`.
5. Confirmation contains focus, supports idle Escape, isolates the background, and restores focus to Exit event after dismissal.
6. Confirmed exit uses the initiating button's `Exiting event…` state, returns to Saved Events, and announces completion.

The control keeps a minimum 48 × 48 target, visible focus, and accessible name `Hold to exit event`. A generic Operator label, hidden gesture, tray, and PIN are outside v1.
