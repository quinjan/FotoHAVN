# Design QA — Operator Exit Safeguard

## Comparison target

- Source visual truth: `C:\Users\QUINJ3875\.codex\generated_images\019fdadd-b6b5-7412-9871-d593788bde6c\exec-e3767b5d-07e0-4c36-8edf-5b088478960d.png`
- Implementation screenshot: `implementation-holding-1280x720-iteration-2.png`
- Full-view comparison: `qa-comparison-final.png`
- Focused header comparison: `qa-comparison-header-final.png`
- State: operator access at 72% of the required 1.5-second hold
- CSS viewport: 1280 x 720
- Device pixel ratio: 1
- Source pixels: 1672 x 941
- Implementation pixels: 1280 x 720
- Density normalization: the 16:9 source was downsampled with high-quality bicubic interpolation to 1280 x 720 before being placed beside the 1280 x 720 browser capture.

## Findings

No actionable P0, P1, or P2 differences remain.

- Fonts and typography: bundled Inter weights reproduce the source hierarchy and optical density. Heading size, line length, weight, event label tracking, support copy, button type, and header lockup align after iteration two.
- Spacing and layout rhythm: header height, brand inset, centered guest group, action footprint, control placement, and footer baseline now align. The implementation has no viewport overflow at 1280 x 720, 1024 x 768, 853 x 480, or the 640 x 360 200%-equivalent stress case.
- Colors and visual tokens: the near-white guest surface, black primary action, neutral header, restrained gray copy, blue keyboard focus, and dark accessible danger action preserve the selected light monochrome direction.
- Image quality and asset fidelity: the visual target contains no photographic or decorative raster assets. UI icons use Fluent System Icons; the FotoHAVN lockup follows the existing WinUI component construction rather than substituting a generated asset.
- Copy and content: all source copy is preserved. Before confirmation, the operator control never exposes the words `Exit Event`; it changes from `Operator` to `Keep holding…` and reports determinate progress.
- Interaction and accessibility: early pointer click and quick Space release both cancel without opening a dialog. The confirmation gives initial focus to `Keep Event Active`, Escape dismisses it, focus returns to operator access, end-of-loop focus trapping is active, `Exiting Event…` stays button-local, and successful exit reaches Saved Events. Browser console errors and warnings: none.

The full-view comparison is sufficient for the hero hierarchy and overall composition. The focused header comparison was added because the brand scale, 48 px operator target, hold copy, icon treatment, and progress line are too small to judge reliably in the full frame.

## Comparison history

### Iteration 1 — blocked

- P1: the initial implementation rendered the brand, hero heading, and primary action materially smaller and lighter than the selected source.
- P2: the footer sat too close to the bottom edge, weakening the source's vertical rhythm.
- Evidence: `qa-comparison-iteration-1.png`.

### Fixes applied

- Bundled Inter at weights 400, 600, 700, and 800.
- Increased header, brand mark, brand text, hero heading, and primary action dimensions.
- Corrected hero spacing and raised the footer baseline.
- Added a short-height stress rule so the 640 x 360 reference does not crowd the action or footer.

### Iteration 2 — passed

- Post-fix evidence: `qa-comparison-final.png` and `qa-comparison-header-final.png`.
- The previous P1 and P2 differences are resolved.
- Responsive evidence: `implementation-holding-1024x768.png`, `implementation-holding-853x480.png`, and `implementation-holding-640x360-final.png`.

## Primary interactions tested

- Quick pointer click cancels before confirmation.
- Quick Space activation cancels before confirmation.
- Confirmation opens in its reference state with safe initial focus.
- Escape closes the dialog and restores operator-control focus.
- Exit changes only the initiating action to `Exiting Event…`.
- Completion reaches Saved Events and announces `Event exited.`
- Guest `Touch to start` exposes its button-local `Starting…` state.

## Follow-up polish

- P3: the coded control uses a Fluent rotating sync icon plus a determinate underline instead of the ImageGen mock's approximate circular ring. This is intentionally retained because the underline exposes real hold completion while the icon supplies motion without custom SVG or CSS-drawn artwork.

## Implementation checklist

- [x] Selected direction is interactive.
- [x] 1.5-second pointer, touch, Space, and Enter contract is implemented.
- [x] Early release cancels.
- [x] Confirmation safeguard and focus behavior are preserved.
- [x] Busy and completion states are present.
- [x] Required booth and scaling references fit without overflow.
- [x] Console is clean.

final result: passed
