# Modal safety

Use the single Calm footer Modal Dialog for Start, Exit, Delete, Discard, and retained-context Retry.

1. Open with the visible title as the dialog name and level-1 heading.
2. Make background content inert and contain keyboard focus.
3. Put the safe action first and confirming action second; initial focus enters on the safe action.
4. Idle dialogs support Escape and close. Dismissal restores the invoking control.
5. Busy work changes only the initiating label, disables both actions and close, suppresses Escape, and retains context.
6. Recoverable failure keeps the dialog and Event Identity panel present, announces once, and replaces confirmation with Retry.
7. Success closes the dialog, navigates to the approved destination, restores or establishes meaningful destination focus, and announces the result.

Destructive meaning belongs to the action and surrounding status, never to the neutral Event Identity panel. Stress mode stacks full-width actions with the safe action first. Exit Event confirmation remains zero-scroll.
