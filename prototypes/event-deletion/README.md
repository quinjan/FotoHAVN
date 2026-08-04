# FotoHAVN Event Deletion Prototype

> THROWAWAY PROTOTYPE — Issue #18 deletion affordance exploration. Do not ship this code.

Deletion-entry and incomplete-deletion patterns on the approved Luma deck,
switchable with `?variant=` on one prototype route.

- A — Direct trash icon on each Event card
- B — Delete Event inside a card overflow menu
- C — Delete Event inside existing Event setup
- D — Direct trash icon revealed only on card hover or keyboard focus
- E — Failed deletion dialog over a quarantined Event card
- F — Failed Event card becomes an inline recovery surface
- G — Page-level recovery notice plus a quarantined Event card

Variant A is the selected direction; Variant D's hover-only icon was explored
and rejected in favor of A's touchscreen discoverability. After the operator confirms deletion, the
prototype keeps the Event visible beneath a blocking busy dialog, waits for the
simulated directory deletion to finish, and then shows a dedicated success
dialog. The card disappears only after deletion succeeds.

Variants E–G focus on the one-way `Deletion incomplete` state. The Event cannot
be started, edited, or sent through a fresh deletion flow. `Retry Deletion`
simulates the underlying problem persisting, then returns to the same state so
the repeated-failure behavior can be judged. Reloading a failure-variant URL
restores that state, standing in for persistence without writing real data.

Run from the repository root:

```powershell
npm run prototype:event-deletion
```

Then open <http://localhost:4179/prototypes/event-deletion/?variant=A>.

Direct failure comparisons:

- <http://localhost:4179/prototypes/event-deletion/?variant=E>
- <http://localhost:4179/prototypes/event-deletion/?variant=F>
- <http://localhost:4179/prototypes/event-deletion/?variant=G>

The prototype uses only in-memory sample data. Reloading restores deleted Events.
