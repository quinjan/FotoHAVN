# Analytics Edge Notch Design QA

## Evidence

- Source visual truth: `C:\Users\QUINJ3875\.codex\generated_images\01a0b28d-500d-7322-88be-65d74d13fa4c\exec-0ea5b8de-d8bd-4b53-8d62-57d062232249.png`
- Desktop implementation: `C:\Quinjan\Repos\FotoHAVN\website\.playwright-cli\analytics-notch-c-desktop.png`
- Tablet implementation: `C:\Quinjan\Repos\FotoHAVN\website\.playwright-cli\analytics-notch-c-tablet-834.png`
- Mobile implementation: `C:\Quinjan\Repos\FotoHAVN\website\.playwright-cli\analytics-notch-c-mobile-390.png`
- Implementation board: `C:\Quinjan\Repos\FotoHAVN\website\.playwright-cli\analytics-notch-implementation-board.png`
- Full-view comparison: `C:\Quinjan\Repos\FotoHAVN\website\.playwright-cli\analytics-notch-qa-full.png`
- Focused edge comparison: `C:\Quinjan\Repos\FotoHAVN\website\.playwright-cli\analytics-notch-qa-focus.png`
- Route: `http://localhost:3000/?analytics-prototype=1&variant=C`
- State: cookieless choice made, settings closed, edge control in its collapsed resting state.

## Viewport And Normalization

- Source responsive board: 1536 x 1024 pixels.
- Desktop browser override: 1440 x 1024 CSS pixels at device pixel ratio 1; captured content area 1425 x 1013 pixels.
- Tablet browser override: 834 x 1194 CSS pixels at device pixel ratio 1; captured content area 819 x 1173 pixels.
- Mobile browser override: 390 x 844 CSS pixels at device pixel ratio 1; captured content area 375 x 812 pixels.
- The implementation board contains each unwarped browser capture fitted within the equivalent source frame. The full comparison places the source and implementation boards at equal 1536 x 1024 board size.

## Findings

- No actionable P0, P1, or P2 mismatch remains.
- Fonts and typography: the implementation preserves the real FotoHAVN display and utility fonts. The analytics label remains hidden at rest and uses the existing uppercase utility treatment when revealed.
- Spacing and layout rhythm: the resting notch remains flush with the right viewport edge and is visually secondary at all three breakpoints. The transparent control retains a 44-pixel interaction width while only the 16-pixel desktop or 14-pixel tablet/mobile notch is painted.
- Colors and visual tokens: ivory surface, ebony label, and muted-brass outline match the source direction and existing site tokens. The exposed corners use a restrained five-pixel radius.
- Image quality and asset fidelity: the live implementation keeps the real FotoHAVN photography and does not replace any visual asset.
- Copy and content: no analytics label appears in the resting state. On reveal, the control reads `Analytics settings`.
- Interaction and accessibility: desktop reveals the label on hover or keyboard focus and opens Settings on activation. Tablet/mobile use first tap to reveal and second tap to open. Keyboard activation opens directly. Settings focus moves to its heading.
- Responsive behavior: desktop, tablet, and mobile had `scrollWidth` equal to `clientWidth`; the control introduced no horizontal overflow.
- Console: no warnings or errors were reported in the final in-app browser pass.

## Comparison History

1. The selected responsive source established a tiny ivory-and-brass resting notch at all three breakpoints, expanding only on interaction.
2. The first implementation matched the responsive placement and behavior but used sharper exposed corners and sat too quietly against the viewport edge for the user's preference.
3. The notch was widened from 12 to 16 pixels on desktop and from 10 to 14 pixels on tablet/mobile, with a five-pixel radius on the exposed corners. Updated captures confirm the control protrudes slightly farther while remaining unobtrusive.

## Focused Comparison

The focused comparison was required because the resting control is intentionally too small to judge accurately in the full responsive board. It confirms the same right-edge placement, ivory fill, muted-brass outline, compact proportions, and softened exposed corners across desktop, tablet, and mobile.

## Primary Interactions Tested

- Collapsed resting notch at 1440, 834, and 390 widths.
- Keyboard focus reveals the label; Enter opens Settings directly.
- First tablet/mobile tap reveals the label without opening Settings.
- Second tablet/mobile tap opens Settings.
- Leaving the control collapses it again.
- Settings retains the inline privacy-information link and right-aligned desktop actions.

## Follow-up Polish

- None required for the selected prototype direction.

final result: passed

---

# Production Analytics Consent QA Audit

## Audit scope

- Fresh implementation run on 2026-09-18 in the Codex in-app Chromium browser against `http://localhost:3000/` and `http://localhost:3000/online`.
- Viewports: 1440 x 1024 desktop, 834 x 1194 tablet, 390 x 844 mobile, and 320 x 720 narrow mobile.
- States captured and inspected: initial consent rail, privacy explanation, cookieless Edge Notch rest, keyboard-focus reveal, first-tap mobile reveal, second-tap Settings open, Settings selected states, withdrawal confirmation, Escape close, and the masked Online Booth template state.
- This audit evaluates UX, visual design, responsive reflow, focus behavior, target size, and observable semantics. It does not claim full assistive-technology or legal compliance.

## Findings

- No actionable P0, P1, or P2 findings remain after the repair pass.
- The dark consent rail preserves approved direction C, states that cookieless measurement continues, keeps the photograph promise visible, and gives both choices full 44-pixel actions. Desktop/tablet actions align right; 390 and 320 layouts stack without clipping.
- The warm-ivory Settings dialog preserves the approved hierarchy, explicit radio choices, inline privacy action, right-aligned desktop actions, stacked narrow-mobile actions, and a clear two-step withdrawal confirmation.
- The privacy explanation reflows from a two-column desktop editorial layout to one column on mobile. Its fixed header, Back action, headings, and copy remain readable without horizontal overflow.
- The Edge Notch retains a 44-pixel interaction target with a 16-pixel desktop or 14-pixel tablet/mobile painted rest state. Desktop keyboard focus reveals the label. Mobile first tap reveals it and second tap opens Settings.
- At 320 pixels, `scrollWidth` equals `clientWidth` and both consent buttons measure 235 x 44 pixels. The Settings panel scrolls internally and exposes both stacked actions at its lower edge.
- The `/online` root has `data-clarity-mask="true"` before generated preview blobs render. At 390 pixels, the route reports equal document `scrollWidth` and `clientWidth`.
- Browser console inspection found no application errors. Development-only Fast Refresh warnings were excluded from production findings.

## Repair and fresh verification

1. First-pass keyboard testing found a P2 focus-restoration defect: Escape from Settings returned focus to the page root because the fixed Edge Notch unmounted while the dialog was open.
2. Settings close and save now schedule focus onto the remounted Edge Notch without scrolling.
3. Fresh 320-pixel interaction evidence confirmed the complete sequence: resting notch, first-tap expanded notch, second-tap Settings, heading focus entry, Escape close, and focus restored to the revealed Edge Notch.

## Remaining release gates

- A real Clarity project ID is not present in the checkout. Production Clarity loading, cookie writes, `/collect` payload inspection, recording masking, synthetic report validation, and HTTPS behavior remain release-blocking external checks.
- Camera permission and customer-created media were not used in this visual audit. Focused deterministic tests cover the Online Booth Cycle analytics boundary; the existing booth tests cover capture/import behavior independently.

final result: passed locally; production provider verification pending
