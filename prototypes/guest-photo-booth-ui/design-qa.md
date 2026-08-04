# Guest Photo Booth UI prototype — design QA

Validated on 2026-08-04 at the fixed 1280 × 720 target.

## Coverage

- Three URL-stable variants render at `?variant=A`, `?variant=B`, and `?variant=C`.
- The live flow completes four Captures with a five-second countdown, white-flash feedback, and explicit saved-Capture confirmation.
- The final plain Photo Strip remains visible for ten seconds, shows a textual countdown plus progress bar, fades for 450 ms, and returns automatically to Start.
- Camera and storage failures both enter Operator Assistance with `Please call the operator`, retain two completed Captures, and expose only `Retry`; retry resumes at Capture 3.
- The Guest Cycle contains no Print action, printing state, physical-completion state, or printer/media recovery.
- `Exit Event` occupies the upper-right utility position on every Start variant, is absent during countdown/Capture/Photo Strip/Operator Assistance, and uses the issue 19 confirmation copy before the Saved Events handoff.
- Healthy Start is silent about readiness. Camera-disconnected and storage-unavailable Start fixtures block Start, ask the guest to call the operator, and return to ready Start after Retry without claiming Guest Cycle progress.
- Variant A places the Event name immediately above its `Let’s take some photos` heading; the utility header contains only FotoHAVN and `Exit Event`.
- The single field-test Photo Strip design is white, labels itself with the full Event name only, omits the date, and has no completion checkmark in the final preview.

## Accessibility and input

- Guest Start target: 204 × 68 CSS px.
- Operator Retry target: 160 × 68 CSS px.
- Status and Capture progress pair color with icons, text, or numerals.
- Visible keyboard focus uses a 3 px blue outline.
- `prefers-reduced-motion` reduces animations and transitions to near-instant.
- Semantic landmarks, headings, button names, an assertive Operator Assistance alert, and textual Capture progress are exposed in the browser accessibility snapshot.

## Technical validation

- `npm run build`: passed.
- Chromium at 1280 × 720: passed for all variants and review states.
- Full accelerated Guest Cycle through automatic Start return: passed.
- Storage recovery through Retry and resumed progress: passed.
- Browser console: zero errors and zero warnings after final reload.
