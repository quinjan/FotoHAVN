# Guest Photo Booth UI prototype

Three structurally different guest-facing Photo Booth flows for FotoHAVN issue 20, switchable with `?variant=A`, `?variant=B`, or `?variant=C`.

This is throwaway design-validation code. It uses in-memory state, a simulated Camera, and no Printer behavior.

Run it with one command:

```powershell
npm run dev
```

- Variant A — **Quiet stage**: the Capture preview is the unquestioned center of the experience.
- Variant B — **Guided split**: a persistent instruction rail makes progress and the next action explicit.
- Variant C — **Full-bleed booth**: the Camera fills the display and controls float over it.

The bottom prototype controls switch variants and jump among review states. `Camera issue` and `Storage issue` expose Operator Assistance. During the live flow, timing is accelerated for review: each on-screen second takes 650 ms. The product timing remains the map's fixed five-second countdown before each of four Captures, 600 ms white-flash feedback, 900 ms confirmation, and the hardware-state decision's ten-second final Photo Strip preview that fades back to Start over 450 ms.

`Exit Event` follows the operator UI decision from issue 19: it is a quiet utility action in the upper-right of Start, is unavailable during a Guest Cycle, and requires confirmation before handing back to Saved Events. Use the `Exit dialog` review control to open that confirmation directly.

Healthy Start does not display a redundant readiness badge. Use `Start camera off` or `Start storage off` to review a pre-Guest-Cycle hardware failure; Retry returns to the ready Start state. The existing `Camera issue` and `Storage issue` controls remain the mid-Guest-Cycle Operator Assistance cases with retained progress.

The first-field-test Photo Strip uses one plain white design. Its footer contains only the full Event name; the final preview has no completion checkmark or date.
