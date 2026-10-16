# FOTOHAVN desktop booth design

## Brand interpretation

This document is the **desktop booth's interpretation** of the shared FOTOHAVN brand system in [`../brand/`](../brand/README.md). The brand system owns the foundation, voice, palette, type, photography, logo, motion, and accessibility rules. This document owns only what is specific to the booth: the token pipeline, app-only tokens, scaling, screen compositions on the Operator Screen and Guest Screen, components, and verification.

- **Interprets:** [`brand/`](../brand/README.md) on the default branch. The brand system has no version numbers.
- **Authority:** this document is normative for the booth UI in `ui/`. The code is the executable source of truth; when the UI and this document disagree, fix the UI or change this document deliberately in the same change. Brand-level changes go to `brand/` first.
- **Language:** read [`CONTEXT.md`](CONTEXT.md) for Event Setup, Active Event, Guest Cycle, Phase Timer, Keepsake Preview, Event Gallery, and the other booth terms. Every label and line below uses those terms.
- **Decisions:** the UI renders presentations from the .NET brain and sends commands; it decides no rules ([ADR 0003](docs/adr/0003-react-ui-in-wpf-webview2-over-a-dotnet-brain.md)). Screens and displays follow [ADR 0008](docs/adr/0008-operator-and-guest-screens-on-bound-displays.md).

### Deliberate departures from `brand/`

| Departure | Brand position | Reason |
| --- | --- | --- |
| Guest touch targets are at least 64 × 64, the shutter at least 112 × 112, and operator targets at least 48 × 48. | Targets are at least 44 × 44 ([accessibility.md](../brand/accessibility.md#focus-and-input)). | Guests stand back from the screen and use touch. The brand allows raising the minimum. |
| Guest reading text is at least 20 px at 1280 × 720 and grows fluidly to 2560 × 1440. Type roles scale above the web reference, for example Display to 132 px. | Body is at least 16 px; web Display is 52–118 px ([mediums.md](../brand/mediums.md#type-scaling)). | Booth readability at standing distance takes priority over editorial density ([mediums.md](../brand/mediums.md#desktop-booth-app)). Sizes scale by role, which the interpretation contract allows. |
| Guest Cycle phases run under Phase Timers that don't depend on input. | Never require a time limit to complete a task ([accessibility.md](../brand/accessibility.md#focus-and-input)). | An abandoned Guest Cycle must still reach a saved Photo Strip. Within the last 20 seconds of the Template, Photographs capture, Photographs review, Filters, and guest gallery phases, a visible countdown offers `MORE TIME`, which restarts the phase's full time, so a guest who is present can always extend. An absent guest can't, so the Guest Cycle still completes. The Keepsake step has no warning and no `MORE TIME`: the Photo Strip is already saved, so returning to Start loses nothing. The operator may turn a phase's timer off. Running out never discards work. |
| Guest screens never scroll as a page. The only scrolling region on any guest screen is the guest gallery's row of Photo Strips, which scrolls horizontally by touch. Every guest view fits the viewport at every verified resolution and scaling. | No equivalent rule. | A booth screen has no scroll affordance a guest expects, and a hidden action is a lost guest. The guest gallery's strip row lets guests browse at their own pace; the page around it stays fixed. |
| The live preview is the only dark surface on the Photographs step. | At most one major dark band per composition ([color.md](../brand/color.md#proportion)). | The camera area reads best on Dark Walnut. It is the step's one dark band, so the proportion rule still holds. |
| The Guest Screen carries a discreet 3-second press-and-hold control that leads to the Operator PIN. From the keyboard, pressing and holding Enter or Space on the focused control for 3 seconds does the same. | Never require precision or a time limit to complete a task. | It's an operator-only control, not a guest task; guests never need it. It guards the operator's exit. With two screens, `END EVENT` on the Operator Screen is the main way out and needs no hold. The hold shows its progress as text as well as a ring. |

### Brand rules this interpretation applies

These are not departures. They record how the booth meets a brand rule where the booth adds a new situation.

| Brand rule | Booth interpretation |
| --- | --- |
| Use guest images publicly only with permission from the Event's Customer or the guests shown ([photography.md](../brand/photography.md#people-and-consent)). | The guest view of the Event Gallery is off by default. Its Event Setup control says "Turn this on only with the Customer's permission." It shows only the Active Event's completed Photo Strips. Deleting a Guest Cycle removes it from both views immediately. |
| Treat original media as read-only and store derivatives separately with their source ([photography.md](../brand/photography.md#people-and-consent)). | Event Gallery thumbnails are derived, regenerable files named by source and hash, never edited in place and never stored beside the originals. |
| No autoplay carousels; browsing a guest album at the user's pace is permitted ([motion.md](../brand/motion.md)). | The guest gallery never moves on its own. |
| Everything a user sees says FOTOHAVN ([voice.md](../brand/voice.md#the-name)). | The booth shows `FOTOHAVN` in every window title, heading, message, and Photo Strip signature. The old desktop exception no longer applies. |
| Avoid "kiosk" ([voice.md](../brand/voice.md#vocabulary)). | "Kiosk" appears only in code and internal documents, never in user-facing copy. |
| Event designs take priority inside a Photo Strip ([mediums.md](../brand/mediums.md#physical-booth-and-event-signage)). | Template artwork may carry its own colors inside the Photo Strip. The interface around it uses the palette only. |
| Camera-flow icons include switch camera and mirror toggle ([iconography.md](../brand/iconography.md)). | Guests get no camera switch, mirror, countdown, or import controls. The operator sets mirror and countdown per Event. |
| Essential non-text elements, such as a control's boundary, reach at least 3:1 ([accessibility.md](../brand/accessibility.md#contrast)). | A control whose border is its only boundary (text inputs, text area, date, select, PIN keys, segments, checkbox, radio, switch) draws it in Soft Brown instead of Hairline: 5.33:1 on Off-white and 4.77:1 on Warm Ivory (Hairline is 1.62:1 and 1.45:1). On Cream Paper the secondary ink is the derived `#52463D`; on dark it is Antique Metal. Hairline stays for decoration: dividers, row separators, and panel edges. |

## What the booth inherits

| Topic | Source of truth |
| --- | --- |
| Positioning, mood, principles | [brand/foundation.md](../brand/foundation.md) |
| Voice, approved lines, actions, casing | [brand/voice.md](../brand/voice.md) |
| Palette, semantic roles, surface levels, contrast pairs, derived inks | [brand/color.md](../brand/color.md) |
| Type pairing, roles, the italic turn | [brand/typography.md](../brand/typography.md) |
| Space, grid, composition, shape, elevation | [brand/layout.md](../brand/layout.md) |
| Photography, the seven Look names and their order | [brand/photography.md](../brand/photography.md) |
| Wordmark rules | [brand/logo.md](../brand/logo.md) |
| Icons (Phosphor Light) | [brand/iconography.md](../brand/iconography.md) |
| Motion tokens, prohibitions, reduced motion | [brand/motion.md](../brand/motion.md) |
| Accessibility baseline | [brand/accessibility.md](../brand/accessibility.md) |
| Actions, eyebrow, statement heading, print object, option list, step progress, media frame | [brand/components.md](../brand/components.md) |

## Tokens and implementation files

| File | Role |
| --- | --- |
| `scripts/generate-tokens.mjs` | Reads [`brand/tokens/tokens.json`](../brand/tokens/tokens.json) and writes `ui/src/styles/brand-tokens.css` (`npm run generate:tokens` in `ui/`). An unknown token `$type` fails the generator. |
| `ui/src/styles/brand-tokens.css` | Generated. CSS custom properties for colors, semantic roles, type roles, space, radius, stroke, shadow, and motion, named after their `tokens.json` paths (`semantic.text.primary` → `--semantic-text-primary`; a typography role expands to `--typography-<role>-font-family`, `-font-size`, `-font-weight`, `-letter-spacing`, `-line-height`, `-font-style`, `-text-transform`, and a `-font` shorthand). Never edit it by hand; `ui/scripts/brand-tokens.test.ts` fails when it's stale. |
| `ui/src/styles/app-tokens.css` | App-only values declared in [App tokens](#app-tokens). Nothing here may redefine a brand value. |
| `ui/src/styles/fonts.css` | Self-hosted fonts: Cormorant Garamond 400, 400 italic, and 500 (wordmark), and Manrope 400 and 600, latin and latin-ext, from the exact-pinned `@fontsource/cormorant-garamond` and `@fontsource/manrope` 5.3.0 packages, with `font-display: block`. `vite.config.ts` preloads the five latin files from `index.html`; nothing loads from the network. The SIL Open Font License texts ship as `/licenses/OFL.txt` (`ui/public/licenses/OFL.txt`). |
| `ui/src/styles/global.css` | Imports the three files above, then the reset (including `font-synthesis: none`), the focus ring, tones, the Guest Screen rules (`[data-fotohavn-role="guest"]`: no page scrolling, no text selection, callouts, or image drags, `touch-action: pan-x pan-y`), `touch-action: manipulation` and a transparent tap highlight everywhere, reduced motion (every transition and animation at `--app-duration-instant`), and `forced-colors`. |
| Lint rule | `npm run lint` runs ESLint, then stylelint (`ui/stylelint.config.js`), which rejects hex and named colors, `rgb()`/`hsl()`, raw `cubic-bezier()`, raw `ms`/`s` durations, and raw `z-index` outside the two token files. ESLint rejects hex colors, and raw easings or durations in inline styles, in TSX. `ui/scripts/lint-rules.test.ts` proves both fire. |
| `ui/src/components/` | The components, one folder each, with the conventions in [`ui/src/components/README.md`](ui/src/components/README.md). |
| Fixture gallery | `index.html?gallery` (every component fixture), `?gallery=<fixture id>` (one, full viewport), and `?gallery=manifest`, in development and E2E builds only; a field build that bundles it fails. |

Use semantic roles before primitives, as [`brand/tokens/README.md`](../brand/tokens/README.md) asks. The Photo Strip itself is composed by the brain, not the UI; its paper, signature, and fonts follow [ADR 0006](docs/adr/0006-brain-side-composition-with-an-app-owned-catalog.md).

## App tokens

These live only in `ui/src/styles/app-tokens.css`.

### Scale

| Token | Value | Use |
| --- | --- | --- |
| `--app-reference-width` | 1280px | Reference frame width (1280 × 720 physical pixels on the Guest Screen) |
| `--app-max-width` | 2560px | Width at which fluid sizes stop growing |
| `--app-type-eyebrow` | 15px → 20px | Guest eyebrows (a label, not reading text) |
| `--app-type-meta` | 14px → 18px | Guest step numbers and meta labels (a label, not reading text) |
| `--app-type-action` | 16px → 22px | Guest action labels |
| `--app-type-supporting` | 20px → 26px | Guest supporting lines; held at Body because a guest reads them to finish a step |
| `--app-type-body` | 20px → 26px | Guest reading text |
| `--app-type-body-lg` | 24px → 32px | Guest lead lines |
| `--app-type-subheading` | 32px → 44px | Guest option titles and the wordmark set as type |
| `--app-type-heading-sm` | 44px → 64px | Guest small headings |
| `--app-type-heading` | 56px → 84px | Guest step headings |
| `--app-type-heading-lg` | 72px → 108px | Guest single moments: Keepsake, Operator Assistance, fatal |
| `--app-type-display` | 88px → 132px | Start statement |
| `--app-type-countdown` | 160px → 320px | The countdown numeral over the live preview, sized to read from 1.5 m; `clamp(160px, calc(160px + (100vw - 1280px) * 160 / 1280), 320px)` |
| `--app-icon-action-guest` / `--app-icon-control-guest` | 24px → 32px / 36px → 48px | Guest icons beside action labels / on primary camera controls (operator: 20px / 28px) |
| `--app-space-fluid-16` … `-96` | 16 → 24, 24 → 36, 32 → 48, 48 → 72, 64 → 96, 96 → 144 | Brand space steps that grow 1.5× by 2560, for guest compositions |
| `--app-action-padding-block` / `-inline` | 14px / 22px (operator); 18px → 24px / 32px → 48px (guest) | Action padding |

Fluid values interpolate linearly by viewport width between the two widths, for example `clamp(20px, calc(20px + (100vw - 1280px) * 6 / 1280), 26px)`. Each guest role grows from 1280 to 2560 by about the factor of its neighbours in the brand scale, keeping the brand's order of roles: labels grow about 1.3×, reading text 1.3×, and headings 1.5×. Supporting is held at Body so no guest reading text falls under 20px.

**Role-resolved tokens.** Components size through `--app-text-<role>` (one per brand type role), `--app-target`, `--app-icon-action`, `--app-icon-control`, `--app-action-padding-*`, and `--app-stack-gap`. They resolve to the brand reference sizes and `--app-target-operator` by default (the Operator Screen, and status screens before a role arrives), and to the guest values above under `[data-fotohavn-role="guest"]`, which the App sets on `<html>` from the brain's `welcome`. So one component is right on both screens.

**Tones.** A surface sets `data-tone="light"` (Off-white, Warm Ivory; the default), `"paper"` (Cream Paper), or `"dark"` (Dark Walnut, Ebony). Components read the inks from `--app-tone-ink`, `-ink-secondary`, `-rule`, `-accent`, `-focus`, `-action-fill`, `-action-fill-emphasis`, `-action-label`, `-wash`, and `-surface`, so they invert on dark without a prop and keep the contrast pairs: on paper the secondary ink is the derived `#52463D`, never Soft Brown; on dark it is Antique Metal, the rule is Off-white at 28%, and the primary action is Off-white with an Ebony label, Warm Ivory on hover.

**Other app values.** `--app-stroke-rule-accent` (2px: the Muted Brass left rule on selected rows and the running-Event panel, and the current-step underline), `--app-wordmark-tracking` (−0.05em, the wordmark set as type), `--app-motion-nudge` (2px), `--app-duration-instant` (1ms, reduced motion), `--app-scrim` (Ebony 32%), `--app-selection` (Muted Brass 24%), `--app-measure-prose` (65ch), and `--app-measure-supporting` (42ch).

### Targets

| Token | Minimum | Applies to |
| --- | --- | --- |
| `--app-target-guest` | 64 × 64 | Every touch target on the Guest Screen, including text actions and the exit hold |
| `--app-target-shutter` | 112 × 112 | The circular shutter |
| `--app-target-operator` | 48 × 48 | Every target on the Operator Screen |

Targets may grow with the scale; they never shrink below these values. The fixture lane fails any control in a fixture smaller than its role's minimum.

### Layout regions

| Token | Value | Region |
| --- | --- | --- |
| `--app-gutter` | `clamp(24px, 5vw, 80px)` | Page gutters, fluid from 24 to 80 ([layout.md](../brand/layout.md#width-and-gutters)) |
| `--app-grid-columns` | 12 | The landscape grid |
| `--app-region-top` | 96px → 128px | The guest top band: step progress, `START OVER` (Template, Photographs, and Filters only), and the exit hold in the top-right corner. It holds a 64 target with 16 to 32 above and below. |
| `--app-region-stage` | `100vh` minus the top band | The rest of the guest viewport, which holds the step's composition |
| `--app-split-print` / `--app-split-options` | 5 / 7 | The 5 / 7 column split of the 12-column grid used by Template and Filters (`grid-column: span var(--app-split-print)`) |

### Z-order

From bottom to top. Each layer is a named token; components never set raw `z-index` values, and the lint rule rejects one.

| Token | Value | Layer |
| --- | --- | --- |
| `--app-z-base` | 0 | Surfaces and the step composition |
| `--app-z-stage-overlay` | 10 | Crop guide and countdown numerals over the live preview |
| `--app-z-flash` | 20 | The capture flash |
| `--app-z-chrome` | 30 | Step progress, `START OVER`, the running-Event panel when pinned |
| `--app-z-timer-warning` | 40 | The Phase Timer warning |
| `--app-z-dialog` | 50 | Scrim and dialogs: Start Over confirmation, PIN, operator confirmations |
| `--app-z-exit-hold` | 60 | The exit hold, which stays reachable above every guest view and is hidden only while the PIN dialog is open |

## Scaling

- **Reference.** The reference frame is 1280 × 720 physical pixels on the Guest Screen. Role sizes grow with `clamp()` between 1280 and 2560 wide and stop growing beyond 2560.
- **Grid.** 12 columns with gutters from 24 to 80, as in [`brand/layout.md`](../brand/layout.md).
- **Guest Screen ignores display scaling.** The Guest Screen ignores Windows display scaling for layout. The host sets the guest WebView2 zoom factor to 1 / (display scale), so one CSS pixel is one physical pixel and the guest layout always works in the display's physical pixels. The four guest resolutions are physical resolutions ([ADR 0008](docs/adr/0008-operator-and-guest-screens-on-bound-displays.md)).
- **Fit.** Each guest view fits without page scrolling or overlap at 1280 × 720, 1366 × 768, 1920 × 1080, and 2560 × 1440 physical pixels, at Windows display scaling of 100%, 125%, and 150%. The guest gallery's strip row is the only region that scrolls, horizontally by touch. Both windows use WebView2's Fluent overlay scrollbars, so scrolling regions (the guest gallery strip row, operator pages) reserve no layout space and match the fixture lane.
- **Measure.** Maximum line lengths still hold at 2560 wide. Prose stays within 45–65 characters.
- **Separate screens.** The Operator Screen and Guest Screen scale independently on their own displays; both windows are per-monitor DPI aware. The guest rules (64 px targets, 20 px reading text, no page scrolling) apply only to the Guest Screen. The Operator Screen follows Windows display scaling, uses the brand roles at their reference sizes, follows the operator target size, and may scroll.
- **Orientation.** Landscape only.

## Screen compositions

### Guest Screen

| View | Composition (landscape) | Brand components |
| --- | --- | --- |
| Start | Off-white canvas, centered statement over generous space, one primary action, and the `FOTOHAVN` wordmark, with no separate eyebrow repeating the name. Typographic only, with no booth photograph. The `SEE THE GOOD COMPANY →` text link sits beneath when the guest gallery is available. | Statement heading (centered single moment), primary action, text link |
| Template | 5 / 7 split: the selected Template's blank preview as a print object on a Cream Paper mat on the left; the option list of offered Templates on the right; step progress on top | Step progress, option list, print object, primary action |
| Photographs: Camera | Live preview dominant on Dark Walnut with the crop guide; capture progress; shutter; countdown numerals in Cormorant Garamond | Step progress, circular shutter (full radius), eyebrow |
| Photographs: Arrange | The strip preview as a print object; selection actions beside it; Move destination choices | Print object, text links, secondary actions |
| Filters | The strip preview on the left; the option list of offered Looks with thumbnails on the right | Option list, print object, primary action |
| Keepsake | Centered single-subject moment: the Photo Strip lifted with the `print` shadow, revealed like paper developing (≤ 600 ms) | Statement heading (centered), print object |
| Operator Assistance | One Dark Walnut band with Off-white type and `RETRY` | Statement heading, primary action (inverted) |
| Guest gallery | Warm Ivory surface; centered "Look at *you.*" heading as a single-subject moment; Photo Strips as print objects at native proportion with the `print-contact` shadow, in a horizontally scrolling row sized for touch, the only scrolling region on any guest screen; `BACK TO START ←` | Statement heading (centered), print objects, text link |
| Guest strip detail | The Photo Strip lifted with the `print` shadow, its arranged Captures beside it in a media frame, and `CLOSE` | Print object, media frame, text link |
| Dialogs | Start Over confirmation and PIN: Off-white panel, 8 radius, Hairline border, Ebony scrim at about 32% | Primary and secondary actions |

Panorama Four's preview is rotated a quarter turn counterclockwise, as on the website; the saved Photo Strip keeps its native orientation. Panorama Four strips also turn a quarter counterclockwise in the guest gallery and the guest detail; operator views keep native orientation.

### Operator Screen

| View | Composition | Brand components |
| --- | --- | --- |
| Starting | Off-white canvas with the left-aligned Events heading the list keeps and one neutral status line; no action, spinner, or motion, so the list appears in place. | Statement heading, inline status |
| Storage error | Off-white canvas, left-aligned statement heading, the problem as an alert status line, the Data folder's path under a Meta label, and `TRY AGAIN` as the view's one primary action. | Statement heading, inline status, primary action |
| Event list | Off-white canvas, left-aligned heading, rows separated by Hairline rules, not cards. `NEW EVENT →` is the view's one primary action, whether or not an Event runs, except during Operator Assistance (see the running-Event panel); row actions are text actions. | Statement heading, option-list rows, primary action |
| Running-Event panel | A Warm Ivory band pinned above the Event list, and only there, with a 2 px Muted Brass left rule, the Event name in the Subheading role, facts as Meta labels with values, and `OPEN GALLERY` (the running Event's live operator gallery, with no hold or PIN), `RETRY`, and `END EVENT`. Its actions are secondary or text actions, and `NEW EVENT →` stays the one primary action. During Operator Assistance it adds an alert line in Ebony with the reason, never color alone; `RETRY` becomes the single primary action and `NEW EVENT` drops to a secondary action, so the Operator Screen still has one primary action. `RETRY` is the primary action only here, on the panel. | Eyebrow, inline status, primary, secondary, and text actions |
| Operator Assistance alert elsewhere | On every other Operator Screen view (Event setup, Event Galleries, Guest Cycle detail), an Operator Assistance alert appears as an inline status line in Ebony with the reason, never color alone, and a text link back to the running-Event panel. It doesn't change the view's primary action. | Inline status, text link |
| Event setup | One screen of left-aligned sections with eyebrows; inputs with a 4 radius and Soft Brown borders (3:1), labels above fields; Template and Look pickers reuse option-list rows with checkboxes and a default radio. `SAVE` is the view's one primary action; `SAVE AND START` is a secondary action, disabled with "End the running Event to start another." while another Event runs. | Eyebrow, inputs, option list, primary and secondary actions |
| Event Gallery | Off-white canvas; eyebrow with the Event name; Photo Strips at native proportion in a flowing row layout, like strips laid on a table, with captions in the Meta role, not uniform square cards | Eyebrow, statement heading, media frame, print objects |
| Guest Cycle detail | The Photo Strip as a print object on the left; its Captures in a Cream Paper media frame on the right; metadata and actions below | Print object, media frame, text links |
| Single-screen placeholder | Shown in the Operator window only if the Guest Screen ever sits behind it | Statement heading |
| Identify displays overlay | Off-white panel centered on each display with the display number in the Display role and its name in the Meta role, for 3 seconds, taking no focus | Statement heading |
| Dialogs | Confirmations: Off-white panel, 8 radius, Hairline border, Ebony scrim at about 32% | Primary and secondary actions |

## Copy

Static copy lives in the UI; strings with domain meaning, such as Template and Look names and notices, come from the brain. Both follow [`brand/voice.md`](../brand/voice.md). Action labels are uppercase with a directional arrow where they lead somewhere.

### Guest Screen

| Where | Copy |
| --- | --- |
| Start | `FOTOHAVN` wordmark (no eyebrow) · "Small room. *Whole other world.*" · "Come a little closer." · `STEP INSIDE →` · text link `SEE THE GOOD COMPANY →` |
| Template | "Make a little *Moment*" (the website's approved line exactly, including the capital "M") · "Choose your template." · `CONTINUE TO PHOTOGRAPHS →` · the blank preview's alt text is "Blank <Template name> Photo Strip" |
| Photographs: Camera | "Find your *light.*" · "Let this moment become a memory." · `TAKE PHOTOGRAPHS` · *Interim copy confirmed in M3:* the shutter shows `STOP` while recording, with the accessible names "Take photographs" and "Stop taking photographs"; the live preview says "Getting the camera ready." while it waits and is named "Live camera preview"; announcements: "Keep holding: N seconds", "Next photograph in N seconds.", "Photograph N of M taken." *Written in M8:* `SEE MY PHOTOGRAPHS →` once a photograph exists (to Arrange) · `KEEP ORIGINAL` while a Retake runs · while the shutter can't be pressed: "One moment. The camera is getting ready." · `CaptureProgress` counts the Template's filled windows ("2 of 4"), not the capture sequence, so a stopped sequence, a second sequence, or a Retake never makes the count fall or misannounce |
| Photographs: Arrange | "Arrange your *moment.*" · "Drag to rearrange · Tap a photograph to edit" · `RETAKE` · `MOVE` · `CANCEL MOVE` · `ACCEPT` · `TRY AGAIN` · `KEEP ORIGINAL` · `TAKE MORE PHOTOGRAPHS ←` (while a window is empty) · `CONTINUE TO FILTERS →` (`FINISH MY STRIP →` when Filters is skipped) · "Photograph N is selected." · "Choose where photograph N goes." · "Keep the new photograph for position N, or try again." · "Take a photograph for each empty position to continue." · "N more photographs are kept for this Event." ("1 more photograph is kept for this Event."; Q24) · "Preparing your strip." while `FINISH MY STRIP →` composes with Filters skipped · window names "Photograph N of M", "Position N is empty.", "Swap with position N", "Move to position N", "Photograph N, moving" · the strip's group name "Your Photo Strip" · figure captions `ORIGINAL` / `NEW` (Meta role) · announcements "Your new photograph is ready. Accept it, try again, or keep the original." and "Photograph placed in position N." |
| Filters | "Find the *feeling.*" · "Choose a filter for your photographs." · `FINISH MY STRIP →` · while composing (polite, once) "Preparing your strip." · the strip's alt text "Preview of your Photo Strip" |
| Keepsake | "A little moment. *Yours.*" · "Keep the moment close." · the strip's alt text "Your Photo Strip" · no action, warning, or `START OVER` |
| Step progress | `01 Template` · `02 Photographs` · `03 Filters` · `04 Keepsake`. A skipped step is omitted and the rest are renumbered consecutively (Q26): `01 Photographs` · `02 Filters` · `03 Keepsake` for a one-Template Event; `01 Photographs` · `02 Keepsake` for one Template and one Look |
| Phase Timer warning | A visible countdown with `MORE TIME` on Template, Photographs, Filters, and the guest gallery; none on Keepsake. *Interim copy confirmed in M3:* the visible line is `N SECONDS LEFT` (`1 SECOND LEFT`); it is announced as "N seconds left. Choose MORE TIME to stay a little longer."; `MORE TIME` is a secondary action. |
| Start Over | `START OVER` · "Start over? Your photographs stay with the Event, but no strip will be made." · `START OVER` · `KEEP GOING` |
| Operator Assistance | "A little *interruption.*" · "Please ask the FOTOHAVN team for help." · `RETRY` · while checking: "Trying again…" (`RETRY` unavailable) · when it didn't go through: "That didn't go through. Try again." |
| Composition failure | "Your strip couldn't be prepared. Try again." (an alert line with text and icon) · `TRY AGAIN` replaces `FINISH MY STRIP →` as the one primary action; the previous strip stays (Q25) |
| Guest top band | Step progress on the left, the Phase Timer warning in the middle of the band while it shows, `START OVER` on the right, and the exit hold in the top-right corner. The warning is a polite, non-modal status; the Start Over confirmation is modal, with the stage behind it inert |
| Guest gallery | "Look at *you.*" · `BACK TO START ←` · `CLOSE` · alt text "Photo Strip from this Event", with no names or faces described. *Interim copy confirmed in M3:* paging `SHOW MORE ↓` with "Loading more photographs."; row action `OPEN`; a guest Capture's alt text is "Photograph from this Event"; Panorama Four strips turn a quarter counterclockwise in the guest gallery and the guest detail (operator views keep native orientation). *M9:* the row has no `SHOW MORE ↓`: the next page loads by itself when the row's end is near, or when keyboard focus reaches it, with "Loading more photographs." at the row's end and "Loading photographs." alone for the first page · the strip detail has `CLOSE` and `BACK TO START ←` side by side, no heading, and no metadata · the guest views carry only images, alt text, and these controls. The Phase Timer warning (`N SECONDS LEFT`, `MORE TIME`) sits at the end of the footer line, right of `BACK TO START ←`, never as a band above the row, so the strip row keeps its height. |
| Exit hold | Accessible name "Operator exit", with progress as text; operable by pressing and holding Enter or Space for 3 seconds. **M6:** the page times the 3 seconds, and the brain accepts the hold from 2.85 seconds on its clock to allow for message delay. |
| PIN dialog | *Interim copy confirmed in M3:* title "Operator PIN" · "Enter the Event's Operator PIN to end the Event." · `END EVENT` · `KEEP RUNNING` · messages "4 to 6 digits.", "That PIN isn't right. Try again.", "Too many tries. The pad is locked for now.", "Try again in N seconds." · **M6:** after a wrong PIN, "N tries left." ("1 try left.") from the brain's attempts remaining, never while the pad is locked (five wrong attempts lock it for 30 seconds) · when a submit doesn't go through: "That didn't go through. Try again." |
| Start unavailable | **M6.** Neutral, never the brand's "A little *interruption.*" (that is Operator Assistance): camera "The booth is *getting ready.*" with "One moment." · storage "The booth is *almost ready.*" with "Thank you for waiting." · no action (it recovers on its own when the brain sends Start again), and the guest gallery link stays when the brain shows it |
| Start: a command that doesn't go through | **M6.** A quiet, dismissible notice on `STEP INSIDE →` or `SEE THE GOOD COMPANY →`: "One moment. The booth is getting ready." (the brain's own notice wins when it sends one) |

### Operator Screen

| Where | Copy |
| --- | --- |
| Event list | `NEW EVENT →` · heading "Events" · row actions `START`, `GALLERY`, `EDIT`, `OPEN FOLDER`, `DELETE` (each named for its Event, for example "Edit Harbour gala", for assistive technology) · `RUNNING` on the running Event's row · "End the running Event to start another." · empty state "No Events yet." |
| Event setup | **M6.** Heading "Set up the *Event.*" · `BACK TO EVENTS` · `SAVE` (primary) · `SAVE AND START` (secondary; while another Event runs, disabled with "End the running Event to start another.") · `Saving…` while a save runs · section eyebrows `EVENT`, `CAMERA`, `GUEST DISPLAY`, `PRINTER`, `TEMPLATES`, `FILTERS`, `TIMING`, `GUESTS`, `OPERATOR PIN` · labels Name, Date, Venue, "Customer or host", Notes, Camera (placeholder "Choose a camera"), Mirror ("Flips the preview and the saved photographs, like a mirror."), Countdown, Printer, "Offered Templates" ("Choose the Templates guests can pick from, and mark one as the default."), "Offered Looks" ("Choose the Looks guests can pick from, and mark one as the default."), "Guest gallery" · timer names Template, "Photographs: capture", "Photographs: review", Filters, Keepsake, "Guest gallery view", each with its range line ("N to M seconds. Enter 0 for Off." or "N to M seconds. This timer is always on.") and what happens when time runs out ("When time runs out, the booth continues with the selected Template." · "…takes the photographs that are still empty." · "…keeps the original photographs and moves on to Filters." · "…finishes the strip with the selected Look." · "…returns to Start." for Keepsake and the guest gallery view), and, for the timers that can be off, "If it is off, a guest stays on this step until someone continues or uses START OVER." and "Off now." · leaving with changes: "Save your changes?" · "If you leave now, what you changed is lost unless you save it." · `SAVE`, `DISCARD CHANGES`, `KEEP EDITING` |
| Event setup: Operator PIN | **M6.** "PIN set" · `CHANGE PIN` · `KEEP CURRENT PIN` · labels "PIN" and "Confirm PIN" ("New PIN" and "Confirm new PIN" while changing) · help "4 to 6 digits. Enter it twice. It is never shown again." |
| Event setup: field errors | **M6.** Shown under the field after a save attempt, led by the warning icon: "Name the Event." · "This is needed." · "Keep the name to 80 characters or fewer." · "Keep the venue to 120 characters or fewer." · "Keep this to 120 characters or fewer." · "Keep the notes to 1,000 characters or fewer." · "That date isn't valid. Choose another." · "Offer at least one Template." · "Choose the default from the Templates you offer." · "Offer at least one Look." · "Choose the default from the Looks you offer." · a timer: "Enter 0 to turn it off, or N to M seconds." or "Enter N to M seconds." · "Use 4 to 6 digits, 0 to 9 only." · "The two PINs don't match." · "Set an Operator PIN to start this Event." (shown beside the PIN, and it blocks only `SAVE AND START`) · "Choose a camera." · "Choose the guest display." · the live announcement "One thing needs attention before this Event can be saved." or "N things need attention before this Event can be saved." |
| Event setup: start failures | **M6.** One alert line at the top of the form, by the brain's code: `eventRunning` "End the running Event to start another." · `guestDisplayMissing` "The chosen guest display isn't connected." · `cameraNotEligible` "The camera isn't ready to start." followed by the camera reason's line · `storageNotWritable` "The Data folder can't be written to, so the Event can't start. Check the folder, then try again." · `pinMissing` "Set an Operator PIN to start this Event." · `invalidSetup` "Some details need attention before this Event can start. Check the fields below." · `guestScreenNotReady` "The guest display didn't respond. Check it, then try again." |
| Camera field | Checking · Ready · Not available · In use by another app · when Windows camera privacy blocks access, Not available with "Camera access is off in Windows privacy settings." (exact) · the other reasons use the camera diagnostics lines below, as "Not available. <reason>" · **M6:** "In use by the running Event. The preview waits until that Event ends." · "Not checked while an Event is running. You can still save it." · "This camera isn't connected. FOTOHAVN keeps it until you choose another." (the bound camera keeps its name, listed as "<name> (not connected)") · "No camera found." |
| Camera diagnostics (*E2E/diagnostics build only, until Event setup in M6; confirmed in M5*) | Eyebrow `CAMERA DIAGNOSTICS` · heading "Check the *camera.*" · list label "Camera" · state line "Choose a camera." (none chosen), "Checking", "Ready", "In use by another app", or "Not available. <reason>" with the reason in words: stale "The camera stopped sending pictures.", failed "The camera stopped working.", removed "The camera was disconnected.", exclusiveLost "Another app took over the camera.", privacyDenied "Camera access is off in Windows privacy settings." (exact), noSupportedFormat "This camera has no picture size FOTOHAVN can use.", notFound "FOTOHAVN can't find this camera.", unknown "Something went wrong with the camera." · no cameras "No camera found." with "Connect a camera and it will appear here." · preview waiting lines "Getting the camera ready.", "Choose a camera to see it here.", "No picture right now." · facts Format, Frame rate, Last picture ("Just now" or "N s ago"), Booth report (E2E only), Last capture ("None yet", or the file name with size, dimensions, and Mirrored or Not mirrored) · controls `Mirror` switch and `CAPTURE` (E2E only) |
| Guest display field | `IDENTIFY DISPLAYS` · "Operator Screen" marker · "The Guest Screen will cover this screen. Use the hold and PIN to return." · "The chosen guest display isn't connected." · **M6:** each display reads "Display N" with its position, for example "Display 2, right of the Operator Screen" (left of, above, or below as it sits), and a bound display that isn't connected stays chosen as a "Not connected" row |
| Printer field | "No printer" |
| Guest gallery field | "Turn this on only with the Customer's permission." |
| Running-Event panel | Guest display Connected or Disconnected · camera Ready, Checking, or Not available · current step (Start, Template, Photographs, Filters, Keepsake, guest gallery, Operator Assistance) · completed Guest Cycles · time since start (a polite line that updates every 30 seconds: "Just started", "12 min", "1 h 12 min", "2 d 3 h") · `OPEN GALLERY` · `RETRY` · `END EVENT`. *Interim copy confirmed in M3:* the labels are `RUNNING`, `GUEST DISPLAY`, `CAMERA`, `CURRENT STEP`, `COMPLETED GUEST CYCLES`, and `TIME SINCE START`. |
| Operator Assistance alert elsewhere | **M6.** The reason in one sentence: camera "The camera needs attention.", storage "The Data folder needs attention.", guest display "The guest display isn't connected." · a text link back to the running-Event panel, `GO TO THE RUNNING EVENT →` |
| End Event | "End this Event? Anyone using the booth will be stopped. Their photographs are kept." · `END EVENT` · `KEEP RUNNING` |
| Close FOTOHAVN | **M10.** Asked when the Operator window is closed while an Event runs: "End the running Event and close FOTOHAVN?" · "Anyone using the booth will be stopped. Their photographs are kept." · `END EVENT AND CLOSE` · `KEEP RUNNING` (focus starts on `KEEP RUNNING`) |
| Delete confirmation | **M6.** "Delete this Event?" · "“<name>” will be removed with N Guest Cycles, N Photo Strips, and N photographs. This can't be undone." (with no photographs: "“<name>” will be removed. It has no photographs yet. This can't be undone.") · `DELETE EVENT` · `KEEP EVENT` |
| Notices | **M6.** From the brain's code: `saveFailed` "The Event couldn't be saved. Check the Data folder, then try again." · `deleteFailed` "The Event couldn't be deleted. Try again in a moment." · `eventNotFound` "That Event isn't there any more." · the notice's action is `CLOSE` |
| Command outcomes | **M6.** A polite line when a command doesn't go through: "That isn't ready yet." (not available) · "End the running Event to start another." (`eventRunning`) · "Some details need attention." (`validationFailed`) · "The booth didn't answer. Try again." (no answer or a lost connection) · "That didn't work. Try again." (anything else) |
| Event Gallery | Completed · Started over · Ended by operator · App closed · Unrecoverable · "No Photo Strip" · `OPEN FOLDER` · `DELETE` · empty state "No photographs yet." · *interim copy confirmed in M3:* paging `SHOW MORE ↓` with "Loading more photographs."; row action `OPEN`; Guest Cycle detail fact labels Time, Outcome, Template, Look, and Captures, with "No Captures" when there are none. Operator views keep every Photo Strip in native orientation, Panorama Four included. *M9:* `BACK TO EVENTS` · eyebrow is the Event name · heading "Event *Gallery.*" · `RUNNING` with "New photographs appear here as guests finish." while the Event runs · row actions `OPEN` and `DELETE` · captions show the time (`13 Oct, 8:42 PM`, local zone), the outcome, and the Template and Look when the Guest Cycle has them · "Loading photographs." for the first page · "Couldn't load these photographs." with `TRY AGAIN` · an item whose files are gone shows an empty mat and "These photographs can't be found." · Guest Cycle detail: `BACK TO GALLERY` · heading "Guest *Cycle.*" · `OPEN FOLDER` and `DELETE`, each disabled when the brain says it can't be done. |
| Guest Cycle delete confirmation | **M9.** "Delete this Guest Cycle?" · "The Guest Cycle from 13 Oct, 8:42 PM and its 5 files will be removed from this computer. This can't be undone." (one file: "its file"; no files: the files clause is left out) · `DELETE GUEST CYCLE` · `KEEP GUEST CYCLE` (focus opens here; Escape keeps) · when the delete fails: "The Guest Cycle couldn't be deleted. Try again in a moment." |
| Single-screen placeholder | "The booth is running on this screen." |

#### Storage

The copy for opening, reading, and recovering the Data folder. The UI owns it (`StartingScreen`, `StorageErrorScreen`, `format.ts`); the brain sends only the error kind and the folder's path. The data-path classifier's verdict reasons in `DataPathClassifier` are for logs, and the lines below are what the operator sees.

| Where | Copy |
| --- | --- |
| Starting | Events heading · "Opening the Data folder…" in a polite status line, announced once; no action and no motion. It shows until the Event store opens, then the Event list or the storage error takes its place. |
| Storage error | "The Data folder needs *attention.*" · `DATA FOLDER` Meta label above the full path, as selectable text · `TRY AGAIN` (the view's one primary action) |
| Storage error, per kind | Disk full: "The disk is full. Free some space, then try again." · Access denied: "FOTOHAVN can't write to the Data folder. Check its permissions, then try again." · Locked: "Another program is using a FOTOHAVN file. Close it, then try again." · Network drive: "The Data folder is on a network drive. Move FOTOHAVN to this computer's own disk." · Cloud-synced: "The Data folder is inside a cloud-synced folder, which can change files while the booth runs. Move FOTOHAVN outside it." · Not writable: "The Data folder can't be written to. Choose a folder FOTOHAVN can write to." · Integrity check failed and corrupt database (one line): "The Event database didn't pass its check. Copy the Data folder somewhere safe and contact the FOTOHAVN team." · Unknown: "Something went wrong with the Data folder. Try again." |
| Storage error, outcomes | While trying: "Trying again…" · when the folder still can't be used: "The Data folder still needs attention." · when the booth doesn't answer: "The booth didn't answer. Try again." · when the retry isn't available: "That isn't ready yet." · any other failure: "That didn't work. Try again." |
| Event list facts | A date reads "Sat 14 Nov 2026" (fixed English weekday and month names, whatever the machine's language) · the last use reads "Last used Sat 3 Oct 2026", in the machine's local day, or "Not used yet" when the Event never ran · "1 Guest Cycle" or "N Guest Cycles" |

## Components

| Component | Notes |
| --- | --- |
| `PrimaryAction`, `TextLink`, `SecondaryAction` | From the brand actions; an optional Phosphor Light arrow after the label (`arrow` prop), nudging 2 px on hover except under reduced motion; inverted on dark through the tone; one primary action per view (`data-fotohavn-action`, checked by the fixture lane). Unavailable actions keep focus with `aria-disabled` and ignore activation. Their disabled look is the interpretation's one functional value: no fill, a border in the tone's rule color, and the label in the tone's secondary ink (Soft Brown on light, Antique Metal on dark), with text nearby giving the reason. `TextLink` takes the Action role, or Meta for row actions. |
| `Eyebrow`, `StatementHeading` | The italic turn through `<em>`, one per heading, at the end: `StatementHeading` takes `lead` and `turn`, so the turn can't land elsewhere, and composes eyebrow, heading, one or two supporting lines, and an optional action |
| `StepProgress` | Numbered `01 Template` to `04 Keepsake`; `aria-current="step"`; going back is allowed, as on the website |
| `OptionList` | Radio and checkbox variants with thumbnail, title, description, and meta. The selected row has Warm Ivory, a 2 px Muted Brass left rule, and a checked state, never color alone. Arrow keys move between rows. |
| `PrintObject` | A Photo Strip as a physical print: Off-white border, `print-contact` or `print` shadow, one shadow per object |
| `LivePreview` | Canvas fed by the frames channel, with the crop guide for the target window's ratio, mirrored by CSS when the Event says so |
| `CountdownNumeral`, `CaptureFlash`, `CaptureProgress` | Motion tokens; one flash per Capture |
| `Shutter` | Circular control with the Phosphor Light camera icon; Fill only while recording |
| `TextField`, `TextArea`, `DateField`, `Select`, `Checkbox`, `Radio`, `Switch`, `SegmentedChoice` | Operator inputs; labels above fields; errors in text, not color alone |
| `HoldControl` | Discreet 3-second press-and-hold with the accessible name "Operator exit" and text progress. Keyboard: pressing and holding Enter or Space on the focused control for 3 seconds; releasing early cancels. |
| `PinPad` | On-screen digits with no system keyboard, masked entry, and the lockout state with its visible countdown |
| `Dialog`, `ConfirmDialog` | Focus trap, Escape to cancel, focus returned on close |
| `InlineStatus`, `Notice`, `PhaseTimerWarning` | Polite live regions; nothing disappears before it can be read; the warning carries `MORE TIME` and never appears on Keepsake |
| `StartOverAction` | Quiet text action with the `START OVER` / `KEEP GOING` confirmation, in the guest top band on the Template, Photographs, and Filters steps only. Keepsake has none, because the Guest Cycle is already completed when its Photo Strip is committed. |
| `GuestCycleList`, `GuestCycleDetail` | One pair with `operator` and `guest` modes: strips at native proportion, paged loading, detail with Captures. The guest mode receives a reduced view model with no metadata and no actions. |
| `RunningEventPanel` | Read-only facts with text states, the alert line, and its actions; updates live. Shown above the Event list only; other views carry the Operator Assistance alert as an `InlineStatus` line with a text link back to it. |
| `DisplayPicker` | Option-list rows with display name, resolution, position, and the "Operator Screen" marker; `IDENTIFY DISPLAYS` |
| `IdentifyOverlay` | Per-display number and name for 3 seconds, taking no focus |

## Errors

The palette has no red, so an error never relies on color. Every field error is ink text under the field, led by a Phosphor Light warning icon, with the control's border made heavier (the ink border plus an inset hairline; two pixels under `forced-colors`), `aria-invalid="true"` on the control, and the message tied to it by `aria-describedby`, ahead of help and the counter. `InlineStatus` carries other alerts the same way: text and an icon, never color alone. Screens move focus to the first invalid field on save, and dialogs make the content behind them inert (Event setup and the Operator Screen from M6).

## Motion

All timing comes from the brand motion tokens in `brand-tokens.css`.

- **Step changes:** the new step fades in at `standard` (240 ms) with the editorial ease; the first step shown is at rest. No layout shift. A Look change crossfades the strip over the one shown at `standard` (240 ms): the old print stays until the new picture has loaded, the print's shape comes from the Template (`FiltersView` width, height, landscape) so it holds its place from the first frame, and composing dims it to 60 % (a state, not a movement).
- **Countdown:** one numeral per second in the `--app-type-countdown` size (160 px at 1280 wide, 320 px at 2560), set in the Display role's family, so it reads from 1.5 m over the live preview. It is announced once when it starts, not every second.
- **Capture flash:** one simulated exposure flash per Capture at `flash` (320–380 ms). Never more than three flashes per second.
- **Keepsake:** the Photo Strip develops in at `reveal` (600 ms): a fade with a rise of at most 16 px and a 1.015 to 1 settle of the picture.
- **Exit hold:** a progress ring over 3 seconds, with the same progress as text.
- **Guest gallery:** moves only under the guest's touch. No autoplay.
- **Reduced motion:** transforms and scale are removed, and state changes are immediate: no step fade, no Look crossfade, no Keepsake develop. The flash becomes a near-instant static change, and the hold shows progress as text only. Information never disappears with the movement.
- **Prohibited:** everything in [`brand/motion.md`](../brand/motion.md#prohibited), including bouncing, confetti, and repeated strobe flashes.

## Accessibility checks

These apply [`brand/accessibility.md`](../brand/accessibility.md) to the booth:

- **Contrast:** pairs from [`brand/color.md`](../brand/color.md#contrast-pairs) only. Soft Brown text never sits on Cream Paper, and Antique Metal only appears on dark surfaces. Control boundaries are at least 3:1 (WCAG 1.4.11): text inputs, text area, date, select, PIN keys, segments, checkboxes, radios, and switches use Soft Brown (5.33:1 on Off-white, 4.77:1 on Warm Ivory), Antique Metal on dark (5.81:1 on Dark Walnut). The Hairline's 1.62:1 is for decoration only.
- **Focus:** a 2 px outline at a 5 px offset, Ebony on light and Off-white on dark.
- **Order and structure:** keyboard order follows reading order, option lists use arrow keys, and landmarks and headings are in order.
- **No gesture requirements:** never require drag, hover, or precision to finish a task. Move plus a destination choice does everything drag does, with taps or keys. Phase Timers on guest tasks always offer `MORE TIME`; Keepsake has no task left and no warning (see [Deliberate departures](#deliberate-departures-from-brand)). The exit hold is an operator control with a keyboard path (Enter or Space held for 3 seconds).
- **Meaning:** state is never color alone. `RUNNING`, the selected option, Camera and display states, and Operator Assistance alerts all carry text.
- **Live regions:** capture progress, countdown start, notices, and Phase Timer warnings announce politely and once.
- **Motion:** reduced motion removes transforms and makes changes instant. High Contrast (`forced-colors`) keeps every control and state visible.
- **Text:** guest reading text is at least 20 px; maximum line lengths hold at 2560 wide.
- **Images:** guest gallery strips use the alt text "Photo Strip from this Event".

## Verification

Run the commands named in [`AGENTS.md`](AGENTS.md). For UI changes, the lanes are:

- **Fixture gallery:** every component fixture (`?gallery=<id>`) and the brain-generated presentation fixtures (`?fixture=<name>`) rendered in Chromium at 1280 × 720, 1366 × 768, 1920 × 1080, and 2560 × 1440, with screenshots, axe with no violations, no overflow or page scroll on guest views (only the guest gallery's strip row scrolls, horizontally), minimum targets, at most one primary action, reduced-motion and forced-colors emulation, and the token drift check.
- **Real host:** end-to-end suites that size the real WebView2 windows to the four resolutions and compare screenshots with the fixture baselines, and check fonts, computed token colors, tab order, focus, and touch on 64 × 64 targets.
- **Manual:** 125% and 150% Windows display scaling; a full Guest Cycle on the booth touch screen at standing distance; countdown legibility from 1.5 m; flash comfort; and one review of brand fidelity against `brand/` and the website.

Report which lanes ran, their results, and what stays unproven.

## Open items

These are gaps between the implementation plan, `brand/`, and this document. Resolve each deliberately and update this document. Items closed during M0 are listed in [section 18 of the implementation plan](docs/implementation-plan.md#18-open-questions) (Q9–Q17).

- **Missing copy and placement (M8).** Closed in M8: the copy for the Template, Photographs, Filters, Keepsake, Operator Assistance, and composition failure views is in the Guest Screen copy table above, and the guest top band places the step progress, the Phase Timer warning, and `START OVER` as that table says. The exit hold stays in the top-right corner. (The Start unavailable view was written in M6.)
- **OptionList dark-surface styling.** Style `OptionList` for a dark surface only if a dark-surface use appears.
- **Guest dialogs (M8).** Closed in M8: the Start Over confirmation is modal and makes the stage behind it inert, as the Operator Screen's dialogs and the PIN dialog do. The Phase Timer warning is deliberately not modal: a present guest must still be able to continue, so it is a polite status in the top band with nothing inert behind it.
