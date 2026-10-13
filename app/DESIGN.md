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
| Template | "Make a little *Moment*" (the website's approved line exactly, including the capital "M") · "Choose your template." · `CONTINUE TO PHOTOGRAPHS →` |
| Photographs: Camera | "Find your *light.*" · "Let this moment become a memory." · `TAKE PHOTOGRAPHS` · *Interim copy confirmed in M3:* the shutter shows `STOP` while recording, with the accessible names "Take photographs" and "Stop taking photographs"; the live preview says "Getting the camera ready." while it waits and is named "Live camera preview"; announcements: "Keep holding: N seconds", "Next photograph in N seconds.", "Photograph N of M taken." |
| Photographs: Arrange | "Drag to rearrange · Tap a photograph to edit" · `RETAKE` · `MOVE` · `TRY AGAIN` · `KEEP ORIGINAL` |
| Filters | "Find the *feeling.*" · "Choose a filter for your photographs." · `FINISH MY STRIP →` |
| Keepsake | "A little moment. *Yours.*" · "Keep the moment close." |
| Step progress | `01 Template` · `02 Photographs` · `03 Filters` · `04 Keepsake` |
| Phase Timer warning | A visible countdown with `MORE TIME` on Template, Photographs, Filters, and the guest gallery; none on Keepsake. *Interim copy confirmed in M3:* the visible line is `N SECONDS LEFT` (`1 SECOND LEFT`); it is announced as "N seconds left. Choose MORE TIME to stay a little longer."; `MORE TIME` is a secondary action. |
| Start Over | `START OVER` · "Start over? Your photographs stay with the Event, but no strip will be made." · `START OVER` · `KEEP GOING` |
| Operator Assistance | "A little *interruption.*" · "Please ask the FOTOHAVN team for help." · `RETRY` |
| Composition failure | "Your strip couldn't be prepared. Try again." · `TRY AGAIN` |
| Guest gallery | "Look at *you.*" · `BACK TO START ←` · `CLOSE` · alt text "Photo Strip from this Event", with no names or faces described. *Interim copy confirmed in M3:* paging `SHOW MORE ↓` with "Loading more photographs."; row action `OPEN`; a guest Capture's alt text is "Photograph from this Event"; Panorama Four strips turn a quarter counterclockwise in the guest gallery and the guest detail (operator views keep native orientation). |
| Exit hold | Accessible name "Operator exit", with progress as text; operable by pressing and holding Enter or Space for 3 seconds |
| PIN dialog | *Interim copy confirmed in M3:* title "Operator PIN" · "Enter the Event's Operator PIN to end the Event." · `END EVENT` · `KEEP RUNNING` · messages "4 to 6 digits.", "That PIN isn't right. Try again.", "Too many tries. The pad is locked for now.", "Try again in N seconds." |

### Operator Screen

| Where | Copy |
| --- | --- |
| Event list | `NEW EVENT →` · row actions `START`, `GALLERY`, `EDIT`, `OPEN FOLDER`, `DELETE` · `RUNNING` on the running Event's row · "End the running Event to start another." · empty state "No Events yet." |
| Event setup | `SAVE` (primary) · `SAVE AND START` (secondary; while another Event runs, disabled with "End the running Event to start another.") · leaving with changes: `SAVE`, `DISCARD CHANGES`, `KEEP EDITING` (*interim copy confirmed in M3:* the prompt reads "Save your changes?") |
| Camera field | Checking · Ready · Not available · In use by another app · In use by the running Event · when Windows camera privacy blocks access, Not available with "Camera access is off in Windows privacy settings." |
| Guest display field | `IDENTIFY DISPLAYS` · "Operator Screen" marker · "The Guest Screen will cover this screen. Use the hold and PIN to return." · "The chosen guest display isn't connected." |
| Printer field | "No printer" |
| Guest gallery field | "Turn this on only with the Customer's permission." |
| Running-Event panel | Guest display Connected or Disconnected · camera Ready or Not available · current step (Start, Template, Photographs, Filters, Keepsake, guest gallery, Operator Assistance) · completed Guest Cycles · time since start · `OPEN GALLERY` · `RETRY` · `END EVENT`. *Interim copy confirmed in M3:* the labels are `RUNNING`, `GUEST DISPLAY`, `CAMERA`, `CURRENT STEP`, `COMPLETED GUEST CYCLES`, and `TIME SINCE START`. |
| Operator Assistance alert elsewhere | The reason in text · a text link back to the running-Event panel, its label written in M6 (see [Open items](#open-items)) |
| End Event | "End this Event? Anyone using the booth will be stopped. Their photographs are kept." · `END EVENT` · `KEEP RUNNING` |
| Event Gallery | Completed · Started over · Ended by operator · App closed · Unrecoverable · "No Photo Strip" · `OPEN FOLDER` · `DELETE` · empty state "No photographs yet." · *interim copy confirmed in M3:* paging `SHOW MORE ↓` with "Loading more photographs."; row action `OPEN`; Guest Cycle detail fact labels Time, Outcome, Template, Look, and Captures, with "No Captures" when there are none. Operator views keep every Photo Strip in native orientation, Panorama Four included. |
| Single-screen placeholder | "The booth is running on this screen." |

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

The palette has no red, so an error never relies on color. Every field error is ink text under the field, led by a Phosphor Light warning icon, with the control's border made heavier (the ink border plus an inset hairline; two pixels under `forced-colors`), `aria-invalid="true"` on the control, and the message tied to it by `aria-describedby`, ahead of help and the counter. `InlineStatus` carries other alerts the same way: text and an icon, never color alone. Screens move focus to the first invalid field on save (see [Open items](#open-items)).

## Motion

All timing comes from the brand motion tokens in `brand-tokens.css`.

- **Step changes:** crossfades at `standard` (240 ms) with the editorial ease. No layout shift; a Look change keeps the strip's dimensions.
- **Countdown:** one numeral per second in the `--app-type-countdown` size (160 px at 1280 wide, 320 px at 2560), set in the Display role's family, so it reads from 1.5 m over the live preview. It is announced once when it starts, not every second.
- **Capture flash:** one simulated exposure flash per Capture at `flash` (320–380 ms). Never more than three flashes per second.
- **Keepsake:** the Photo Strip develops in at `reveal` (≤ 600 ms).
- **Exit hold:** a progress ring over 3 seconds, with the same progress as text.
- **Guest gallery:** moves only under the guest's touch. No autoplay.
- **Reduced motion:** transforms and scale are removed, and state changes are immediate. The flash becomes a near-instant static change, and the hold shows progress as text only. Information never disappears with the movement.
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

- **Missing copy and placement (M8).** The Start unavailable view, the fatal and protocol-mismatch view, the labels for stopping a capture sequence, reviewing from the Camera tab, accepting a Retake, and cancelling a Move, and the placement of `START OVER` and the Phase Timer warning in the guest top band are written from [`brand/voice.md`](../brand/voice.md) during M8 and recorded here. The exit hold stays in the top-right corner.
- **Operator Assistance link label (M6).** The label of the text link from an Operator Assistance alert on Event setup or a gallery back to the running-Event panel is written from [`brand/voice.md`](../brand/voice.md) during M6 and recorded here.
- **OptionList dark-surface styling.** Style `OptionList` for a dark surface only if a dark-surface use appears.
- **Dialog inertness and invalid focus (M6, M8).** Screens make the content behind dialogs inert and move focus to the first invalid field on save.
