# Prototype instructions

This directory contains throwaway design-validation code for the guest-facing Photo Booth UI. It is not production code.

## Durable decisions

- Inherit issue 19's Attio-inspired language: Inter, cool-white and light-gray flat surfaces, hairline borders, compact radii, restrained elevation, and mostly neutral color.
- Target the fixed 1280 × 720 touchscreen at 100% scaling.
- The Event is configured with `No Printer`; never introduce Print actions, printing progress, physical-completion states, or printer/media recovery.
- Use `Capture`, `Guest Cycle`, `Photo Strip`, and `Operator Assistance` as defined in the root domain glossary.
- Timing: five-second countdown, 600 ms flash, 900 ms Capture confirmation, ten-second final Photo Strip preview, and a 450 ms fade back to Start.
- Every actionable guest target is at least 64 × 64 CSS pixels. Never rely on color alone for status or progress.
- Camera or storage failure pauses the Guest Cycle, retains progress, says `Please call the operator`, and exposes one `Retry` action.
- `Exit Event` is the issue 19 operator utility action: place it in the upper-right of Start only, hide it throughout an active Guest Cycle, and require the approved confirmation before returning to Saved Events.
- Healthy Start is silent about hardware readiness; do not show a `Camera ready` status. Surface Camera or storage state only when it blocks Start, with `Please call the operator` and `Retry` but no retained-progress claim because no Guest Cycle has begun.
- In Variant A, place the Event name in the centered content hierarchy immediately above `Let’s take some photos`, not in the utility header.
- The first field test has exactly one Photo Strip design: a plain white strip with the full Event name centered below the four Captures, no date, and no completion checkmark on the final preview.
