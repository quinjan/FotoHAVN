# Reference states

This directory is the canonical, risk-based review matrix for the complete operator and Guest Cycle experience. It avoids a wasteful Cartesian product: every meaningful state is specified at 1280 × 720, while smaller references use the densest or highest-risk state for each surface.

- [Machine-readable matrix](matrix.yaml)
- [Approved visual evidence](evidence.md)
- [Accessibility annotation template](annotation-template.md)
- [Measured contrast pairs](contrast.md)

## Base surfaces

1. Saved Events
2. Event setup
3. Guest Start
4. Guest Start unavailable
5. Capture
6. Operator Assistance
7. Photo Strip preview/return
8. Shared confirmation dialog

## Canonical 1280 × 720 coverage

| Surface | Required meaningful states |
|---|---|
| Saved Events | New Event, card idle, hover, focus, unavailable, busy, deletion-incomplete, maximum-card collection |
| Event setup | New/Edit, empty/populated, dirty, Camera checking/ready/unavailable/access-denied/in-use/disconnected, storage ready/insufficient/unavailable, disabled/enabled actions, saving success/error |
| Guest Start | Ready, guarded Exit Event idle/hold/cancel/completed |
| Guest Start unavailable | Camera problem, storage problem, recoverable Retry, setup-correction Exit |
| Capture | Captures 1–4, countdown 3/2/1, flash, Photo saved, Camera failure, storage failure |
| Operator Assistance | Camera/storage/Photo Strip causes, 0–4 preserved Captures, retrying, recovered, retry failed, exit-only |
| Photo Strip | Preparing, visible, five-second milestone, returning/fading, failed |
| Confirmation | Start, Save, Discard, Exit, Delete; idle, focus, busy, failure/Retry, success destination; neutral Identity panel where consequential |

## Responsive risk coverage

At 1024 × 768, 1024 × 576, 853 × 480, and 640 × 360, review one dense/risky state for every base surface: maximum cards, longest Event identity, Camera checking/error, insufficient storage, busy/destructive confirmation, fourth Capture/countdown, Operator Assistance with preserved Captures, and Photo Strip preparing/returning.

Acceptance requires no clipping, overlap, unintended horizontal scrolling, hidden essential content, unreachable focus, media cropping/stretching, undersized target, or guest-stage vertical scrolling. Operator scrolling and fixed regions follow [Foundations](../foundations.md).

## Asset rule

The matrix and annotations are normative. The evidence registry links versioned PNG comparison/reference assets that informed approval; they do not override tokens, component specs, or interaction rules. A later WinUI verification capture must identify contract version, surface/state ID, effective size, scaling, theme, source commit, and result.
