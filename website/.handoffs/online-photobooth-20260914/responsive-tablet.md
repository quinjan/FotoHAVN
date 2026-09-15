# FOTOHVN online booth — tablet responsive QA

final result: passed

Reviewer: fresh agent `online_tablet_qa`, 2026-09-14. Read-only review after `gpt-taste-implementation-verification-recheck.md` passed. No actionable P0, P1, or P2 tablet findings were found in the assigned two-viewport matrix.

## Scope and method

Read `responsive-qa-input.md`, `selected-design.md`, the passed final conformance report, `DESIGN.md`, `tokens.json`, `variables.css`, and `theme.css`. Used an isolated CUA in-app browser at `http://localhost:3000/fotohvn#experience` and `/fotohvn/online`. Inspected the live page through ordinary navigation, clicking, keyboard Tab, and natural scrolling. The selected `visual-2.png` and a current Experience screenshot were displayed together in the same tool output for each viewport.

Explicit viewports were **820 × 1180** portrait and **1180 × 820** landscape. The viewport override was reset after completing the matrix. No production file, runtime process, dependency, or shared QA summary was changed.

## Viewport results

| Area | 820 × 1180 portrait | 1180 × 820 landscape |
| --- | --- | --- |
| Experience composition | The heading deliberately becomes two lines. The full unequal-width photographic band, all three offers, and the invitation are visible. The invitation button moves beneath its supporting copy. No cropped headline or offer text. | Wide heading, 5/2/5 photographic band, three aligned offers, and the invitation retain the selected composition. The lower invitation and next section are reachable through natural scrolling. |
| Imagery and type | All three generated material images loaded. The warm paper palette, italic serif emphasis, and clear body copy match the chosen direction. | All three images loaded. Computed heading font is Cormorant Garamond; supporting copy is Manrope. Each offer heading, paragraph, and invitation control has contained text. |
| Website navigation | Menu opens with a readable ONLINE BOOTH item and clear Close control. ONLINE BOOTH successfully opens the new route. | Full navigation fits without collisions; visible links are 44px high, with a 48px booking action. The Experience invitation opens the online route. |
| Layout | Preview and selection controls fit side by side. Four layout choices reflow into a readable 2 × 2 selector; all four frame choices remain visible. Selected Story Strip and Walnut. | Four layouts and four frame choices fit in their respective rows beside the dominant preview. All labels and the continue action are readable and reachable. |
| Photographs | The empty viewfinder, explicit camera/device choices, photo rail, real framed positions, and review actions fit. Three generated fixtures imported through the actual chooser. | The viewfinder and framed strip remain side by side. Natural scrolling reaches the rail, per-photo actions, retake choices, and stage progression. No overflowing control labels were detected. |
| Look | Soft Fade selection is marked with a check and rule. The whole Story Strip with Walnut frame remains visible beside all four look choices and final action. | Full framed preview, four look rows, and final/edit controls remain readable and reachable. Edit Your Strip correctly returns from Download to Look. |
| Download | The complete 900 × 2100 strip, Download PNG, Open Image to Save, Edit Your Strip, Make Another, and physical-booth link fit without horizontal overflow. | Orientation change preserves the exact completed blob. The strip and actions stay side by side; the full print bottom, metadata, final actions, and footer are visible after normal scrolling. |

## Interaction evidence

- Used only `curtain-960w.webp`, `lens-960w.webp`, and `keepsake-960w.webp` from the task-generated Experience asset directory. No physical camera was activated.
- Portrait device import populated three distinct photograph source URLs and enabled Look.
- With photograph 3 selected, clicked Move and then position 1 in the actual frame. Rendered rail sources changed exactly from `[A, B, C]` to `[C, B, A]`. Position 2 was preserved. The app announced the swap and focused `photograph-position-0`.
- Selected Soft Fade with the Walnut frame. The loaded framed image reported natural dimensions **900 × 2100**, and the final stage showed the same finished composition.
- The final `<img>` source and Download PNG link were exactly the same URL: `blob:http://localhost:3000/ea3dba10-8a7d-4575-ab26-c37a7d1c59ec`. Open Image to Save exposed that same URL. The filename was `fotohvn-story-strip-2026-09-14T09-35-39-810Z-0c951afe.png`. These are observed DOM values, not evidence of a file saved to disk.
- Changed from portrait to landscape while on Download. The image and download URLs remained the same, with no session reset or lost result.
- In landscape, Edit Your Strip returned to Look, then Edit Photographs returned to the populated arrangement. Selected photograph 2, entered Retake, and chose Keep Original. All three source identities stayed unchanged. The app announced the kept original and focused `photograph-position-1`.
- Change Layout or Frame returned to the populated Story Strip/Walnut layout state. Back to FOTOHVN returned to `/fotohvn#experience`, and the Experience invitation opened `/fotohvn/online` again.
- Keyboard Tab from the final heading focused Download PNG. Its computed focus outline was a visible **2px ebony solid ring with 5px offset**; the ring is visible in the landscape result screenshot.

## Containment and console

At portrait Experience, document `scrollWidth` and `clientWidth` were both **805px** inside the 820px viewport with the vertical scrollbar. Portrait online Layout was also **805 / 805**. Look and Download recorded **805 / 820**, with no positive horizontal overflow. At landscape Experience and online states, the dimensions were **1165 / 1165** inside the 1180px viewport. No positive horizontal overflow was observed.

DOM checks of landscape online buttons, labels, and headings returned no elements whose text width exceeded their client width. Experience offer headings, supporting paragraphs, and invitation controls also had matching contained widths. All relevant generated images were complete with nonzero natural dimensions.

Warning/error console reads returned `[]` after the online interaction matrix and after returning to Experience.

## Screenshots

All screenshots are serialized directly from CUA browser screenshot bytes in this handoff directory. Some landscape screenshots deliberately show the lower scrolled workspace because the full page is taller than 820px.

| Portrait | Landscape |
| --- | --- |
| `tablet-820-experience.png` | `tablet-1180-experience.png` |
| `tablet-820-menu.png` | `tablet-1180-experience-invitation.png` |
| `tablet-820-layout.png` | `tablet-1180-layout.png` |
| `tablet-820-photographs.png` | `tablet-1180-photographs.png` |
| `tablet-820-look.png` | `tablet-1180-look.png` |
| `tablet-820-download.png` | `tablet-1180-download.png` |
| | `tablet-1180-download-actions.png` |
| | `tablet-1180-retake.png` |

## Evidence limits

This is simulated tablet viewport evidence in the available browser, not a physical touch-device or Safari test. The Move action used the supported tap-style two-click UI; hardware touch gestures were not claimed. Physical camera permission/capture remains a manual check. Native file-save artifact access and network/storage instrumentation were not repeated because they are unavailable in this browser environment. No user Downloads files were inspected.

Reduced-motion source was inspected: Experience skips its reveal when the preference is set, and the booth CSS removes travel/print/countdown animations and relevant transitions. The exposed browser API did not provide motion-preference emulation, so a reduced-motion runtime pass is not claimed. Final tests/lint/typecheck/build remain root-owned evidence from the QA input; this read-only viewport review did not repeat them.

No tablet repair is requested. Final synthesis may use this passed report with the independent desktop and mobile reports.
