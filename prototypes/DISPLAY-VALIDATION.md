# Booth display validation prototype

This throwaway issue 10 artifact joins the approved issue 19 operator Variant A to the approved issue 20 guest Variant A without revisiting their visual direction.

## Question

Does the complete `No Printer` flow remain understandable, legible, touch-safe, and operational at 1280 × 720 without extra instructions?

## Run

From `prototypes/operator-event-ui`:

```powershell
npm run dev:validation
```

Open `http://127.0.0.1:43173/?variant=A` at a 1280 × 720 viewport.

## Validation route

1. Create a new Event. Confirm that Event name and Camera are required while `No Printer` is already valid.
2. Choose an eligible Camera, optionally inspect Camera tuning, then select `Save & Start Event`.
3. Confirm preflight checks Camera, storage, and the `No Printer` configuration without requiring printer hardware.
4. On guest Start, validate Camera- and storage-unavailable states and Retry using the prototype review controls.
5. Run all four Captures, including the countdown, flash, saved-Capture feedback, and a representative mid-cycle Camera or storage failure with retained progress and Retry.
6. Confirm the final Photo Strip remains visible for ten seconds, fades, and returns automatically to guest Start in the same Active Event.
7. Confirm `Exit Event` is only available on Start and returns to Saved Events after confirmation.

## Decision boundary

Only usability failures observed in this integrated route can revise the approved designs. Printer-required validation and the old operator placeholder Guest Start are superseded integration gaps, not design questions.

## Automated integration observations

Validated in a headed Chromium session at an exact 1280 × 720 viewport on August 4, 2026:

- Empty `Save & Start Event` reports only the missing Event name and Camera; `No Printer` remains selected and valid.
- A newly named Event with an eligible Camera crosses preflight into the approved guest Start, with its full Event name preserved.
- Camera-unavailable Start blocks the Guest Cycle, uses `Please call the operator`, and returns to healthy Start through Retry.
- A representative mid-cycle Camera failure reports two durable Captures and Retry resumes at the next Capture.
- The resumed four-Capture route reaches the Event-named Photo Strip, counts down the ten-second preview, fades, and returns to Start in the same Active Event.
- `Exit Event` appears on Start, requires confirmation, and returns to Saved Events through the integrated operator/guest boundary.
- The browser console is clean after reload. Both prototype production builds and the operator prototype's four packaging tests pass.

The first integrated run exposed two prototype-only gaps, both corrected before handoff: the superseded operator printer requirement/placeholder guest screen, and overlapping operator/guest review controls after the handoff.

## Human verdict needed

Complete one pass on the booth display without relying on the bottom prototype review controls for the happy path. Record any point where an operator or guest needs extra instructions, any text that is not comfortably legible at normal viewing distance, or any target that is not comfortably touch-safe. If none occur, the smallest revision set is `None` and the UI is build-ready.
