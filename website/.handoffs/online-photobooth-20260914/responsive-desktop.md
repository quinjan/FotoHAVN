# FOTOHVN online booth: desktop responsive QA

final result: passed

Fresh reviewer: `online_desktop_qa`, 2026-09-14. No actionable P0/P1/P2 desktop findings. This read-only review followed the passed fresh conformance recheck and the desktop assignment in `responsive-qa-input.md`. No production source, assets, server process, or shared QA report was changed.

## Scope and evidence

Read `selected-design.md`, `visual-2.png`, the passed conformance recheck, and the local `DESIGN.md`, `tokens.json`, `variables.css`, and `theme.css`. Used an isolated CUA in-app browser at the canonical localhost routes. Tested explicit 1440 x 1100 and 1024 x 900 viewports, natural scrolling, pointer actions, real keyboard input, and the actual device-file chooser. Reset the viewport override at completion.

Emitted the chosen Option 2 image and a new 1440-pixel Experience screenshot together in the same tool call. The implementation follows the selected wide serif statement, italic possibility, unequal three-image band, aligned offers, and final online invitation. All three prepared material images loaded; no customer image appears in Experience. At 1024 the heading becomes two intentional lines and the invitation action follows its supporting copy. All three offers remain visible and readable.

| Check | 1440 x 1100 | 1024 x 900 |
| --- | --- | --- |
| Document containment | `scrollWidth = clientWidth = 1425` | `scrollWidth = clientWidth = 1009` |
| Experience | Full composition matches Option 2 and its story order | All text contained; each measured text block has equal client/scroll width, readable wrapping, intact image band |
| Navigation | ONLINE BOOTH fits in shared desktop navigation | No overlap; navigation hit areas are 44px tall, rental CTA 48px; rightmost edge 958.56 within 1009px content width |
| Layout | Dominant Archive preview; four layouts in one row and four frame choices | Layout choices reflow to two columns while four frame choices remain readable; continuation reachable by natural scroll |
| Photographs | Viewfinder, rail and composed frame clearly separated; pointer and keyboard moves pass | Four rail images, selected-position actions, composed-frame targets and Look action remain contained and reachable |
| Look | Complete Archive frame remains visible around Silver photographs | Preview and text-led look choices remain side by side, all choices and editing actions readable |
| Download | Complete PNG and clear download/edit/another actions | Complete framed result and final actions contained; main download target 52px tall, edit/another 48px |

The 15px difference between configured viewport and client width is the browser scrollbar. It is not horizontal overflow.

## Interaction results

- Clicked each of the three entry points: Experience's EXPERIENCE FOTOHVN ONLINE, desktop ONLINE BOOTH, and Prints' MAKE A DIGITAL STRIP. Each opened `http://localhost:3000/fotohvn/online`. Back to FOTOHVN returned to the homepage Experience anchor. The existing SEE THE GOOD COMPANY link remains alongside the Prints invitation.
- Selected Archive and continued to Photographs. Imported the four permitted generated fixtures through the actual multiple-file chooser: curtain, lens, keepsake, and booth exterior, all `-960w.webp` candidates. No camera request was made.
- Pointer-dragged photograph 1 to occupied position 3 in the actual composed frame. The order changed exactly from `[A,B,C,D]` to `[C,B,A,D]`. Positions 2 and 4 preserved their original blob identities. Status announced the swap and focus moved to `photograph-position-2`.
- Opened Move, used Tab to reach the native destination selector, then Home, Tab, Return to apply position 1. The complete original source order was restored exactly. Focus moved to position 1. The destination selector showed a visible ebony 2px outline, captured in `desktop-keyboard-placement-1440.png`.
- Look displayed the selected Archive frame with Silver photographic treatment. Selected/current states are conveyed by native radio checked state and visible marks/rules. Stage navigation uses `aria-current="step"` and prerequisite disabling. Stage transitions focus their headings.
- Final image natural dimensions were **900 x 2700**. Its `src` exactly equaled Download PNG's `href`: `blob:http://localhost:3000/2457f76d-1406-44f1-9a46-ff36dfb4b16c`. The download filename was `fotohvn-long-strip-2026-09-14T09-34-55-637Z-3e39e1ea.png`. This verifies the displayed-image/link contract, not an on-disk save.
- Computed heading font resolved to Cormorant Garamond and body font to Manrope. Warning/error console reads after the full flow and all entry-link checks returned `[]`.

## Screenshot inventory

All files are in this handoff directory and were written directly from CUA screenshot bytes.

- `desktop-experience-1440.png`: selected-composition comparison.
- `desktop-layout-1440.png`: settled Archive empty-layout preview; replaced the initial transient preparing screenshot.
- `desktop-photographs-1440.png`: four imported photographs in rail and frame.
- `desktop-keyboard-placement-1440.png`: actual keyboard focus ring and placement controls.
- `desktop-look-1440.png`: Silver within the Archive frame.
- `desktop-download-1440.png`: settled final PNG and controls.
- `desktop-experience-1024.png`: narrow desktop navigation, heading, image band and offers.
- `desktop-invitation-1024.png`: complete invitation and subsequent section boundary.
- `desktop-layout-1024.png`: two-column layout choices and complete framed preview.
- `desktop-photographs-1024.png`: naturally scrolled lower arrangement workspace and editing controls.
- `desktop-look-1024.png`: framed preview and all four looks.
- `desktop-download-1024.png`: full result and final actions.
- `desktop-prints-entry-1024.png`: contextual digital-strip entry alongside the existing album action.

## Limits

This is desktop viewport and local device-photo evidence. Physical camera permissions/capture, physical touch devices, and native on-disk saving were not tested. No user Downloads file was inspected. No unsupported media-preference emulation or browser instrumentation was used: reduced-motion rules were source-inspected, but runtime reduced-motion behavior is not claimed. The full retake/permutation matrix and automated build/test gates remain covered by the separate root and conformance evidence; this reviewer did not repeat them.
