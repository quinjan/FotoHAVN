# Design QA — canonical operator modal prototype

Status: **Ready for operator review. No variant is approved yet.**

## Question under test

What canonical modal structure and behavior should standardize action hierarchy, destructive styling, focus entry and trapping, Escape behavior, background isolation, busy states, and focus restoration?

The approved issue #56 Event Setup direction is treated as fixed context, not a competing variant.

## Variant comparison

| Variant | Structural bet | Strength | Tradeoff |
|---|---|---|---|
| A — Calm footer | Familiar single-column content with horizontal footer actions | Fastest to scan and closest to common Windows dialog expectations | Consequence and decision compete within one visual column |
| B — Decision split | Explanatory content and a dedicated decision rail | Strongest separation of context from action; safe choice remains prominent | Widest footprint and most expensive at narrow widths |
| C — Guided stack | Numbered review steps with full-width actions | Most deliberate for destructive or identity-sensitive decisions | Heaviest treatment for routine Start and Exit confirmations |

## Inherited standards verified

- Neutral Event identity panel shows the Event name and complete lowercase UUID.
- Operator actions and icon actions retain a minimum 48 px target.
- Destructive emphasis uses one dark-red token; Event identity remains neutral.
- Event Setup preserves the approved form/preview columns, silent valid fields, optional Printer treatment, storage path, and persistent footer.
- Busy state changes only the initiating action label and disables conflicting dismissal.
- Error state retains context, explains recovery, and changes the primary action to Retry.
- The prototype switcher is URL-stable and wraps with pointer controls or Left/Right keys.

## Browser verification

Playwright CLI, Chromium, 2026-08-07:

| Check | Result |
|---|---|
| Production bundle | Pass (`npm run build`) |
| Console after reload | Pass — 0 errors, 0 warnings |
| Safe initial focus | Pass in A, B, and C |
| Tab / Shift+Tab containment | Pass; focus wraps within the modal |
| Escape while idle | Pass; modal closes |
| Focus restoration after invoked Delete | Pass; focus returns to the Delete icon action |
| Escape while busy | Pass; modal and `Deleting Event…` remain present |
| Recoverable error | Pass; context remains, alert is announced, Retry is available |
| Retry success | Pass; dialog closes and Start recovery reaches `Ready when you are.` |
| Delete success | Pass; dialog closes and `Event deleted.` is announced |
| 1280 × 720 | Pass for all variants |
| 640 × 360 stress | Pass with vertical scrolling and no horizontal overflow (`scrollWidth === clientWidth`) |

At 640 × 360, focus scrolls the safe action into view. The full dialog remains keyboard reachable through vertical scrolling; this prototype does not attempt to settle the screen-wide responsive rules owned by issue #59.

## Evidence

- [Variant A — Delete, 1280 × 720](qa/a-delete-1280x720.png)
- [Variant B — Exit, 1280 × 720](qa/b-exit-1280x720.png)
- [Variant C — Discard from approved Event Setup, 1280 × 720](qa/c-discard-1280x720.png)
- [Variant C — Delete, 640 × 360 stress](qa/c-delete-640x360.png)

## Review prompt

Choose one canonical anatomy, or identify a precise combination such as “A for routine confirmations, C only for irreversible actions.” That decision should be recorded on issue #57 before any variant is promoted into the Figma source of truth.
