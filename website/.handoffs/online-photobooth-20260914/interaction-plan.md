# FOTOHVN online photobooth — interaction and implementation plan

Date: 2026-09-14. Status: independent proposal for root synthesis; visual direction is awaiting user selection. This file authorizes no production changes by itself.

## Read first

Implement this plan only after the root handoff records the selected visual concept and reconciles the decisions at the end of this file. Read `brief.md` and `references-and-visuals.md` alongside that selection; the latter owns the exact displayed option mapping and verified web references. The user's four-stage workflow and the preservation rules below are fixed requirements; the named starter collections and presentation details are concrete proposals.

Local authority is `website/DESIGN.md`, `tokens.json`, `variables.css`, `theme.css`, and the rendered editorial site. Cormorant Garamond, Manrope, paper surfaces, ebony, walnut, and restrained brass remain the visual language. The user's request extends that language with a working digital camera flow, photographic filters, and expressive interaction. Dramatic presentation should come from photographic light, scale, composition, and state transitions.

The physical booth's layout and filter assets are unavailable. Everything proposed here is an original **online starter collection**, with a replacement seam for the actual assets later. Product copy must not imply that these treatments reproduce the booth's production presets or the existing documented FOTOHVN Signature look.

Planning evidence: the designated design files, current `UpperExperience`, `EditorialLift`, its controller/math, `SiteChrome`, `MiddleExperience`, `ExperienceMotion`, `EventPhoto`, application root/configuration, and the root-captured `current-experience.png` were inspected. This agent did not operate the browser, generate imagery, implement the interface, or run product tests. The root owns web-reference verification and visual exploration.

## Outcome and route

The editorial Experience becomes an invitation to make something: the privacy of the enclosure, freedom to play, choice of photographic treatment, and a keepsake. Purpose-made material imagery replaces the two current customer photographs in this section. Its final invitation leads to a dedicated `/fotohvn/online` page, where the visitor can:

1. Choose a layout and designed frame.
2. Capture photographs, retake one selected position, and arrange the photographs.
3. Choose a look while seeing the complete selected frame.
4. See the finished composition and download its PNG.

Capture review is part of stage 2. It is not an extra step between the four stages. A small progress navigation reads `Layout`, `Photographs`, `Look`, `Download`; the active stage has text and a rule as well as its accessible current state. Completed stages remain editable. Later stages are available only when their prerequisites hold.

Keep the online session on one route and in one client-owned session. Stage changes must not unmount the photo store. Refreshing or leaving the route ends this in-memory session; state this near the local-processing note. No account, server image processing, or persistent photo storage is needed for this scope.

## Editorial message and entry points

The following copy is a proposal to reconcile with the selected concept, rather than a replacement for the root's three visual directions.

| Role | Proposed copy | Purpose |
|---|---|---|
| Experience heading | `The world can wait.` / `Make this moment yours.` | Retains the current emotional opening in a compact, wide serif composition. |
| Supporting paragraph | `A curtain drawn. A little room to play. Photographs with a look of their own — and a keepsake that brings you back.` | Explains privacy, participation, treatment, and the physical result. |
| Three supporting beats | `A room of your own.` / `Make it your way.` / `Keep the feeling.` | Each has one short sentence about enclosure, photographic choices, or physical prints. |
| Digital invitation heading | `A little moment.` / `Right where you are.` | Makes the online invitation feel part of the editorial narrative. |
| Digital invitation copy | `Choose a frame. Find your light. Make a little strip of now.` | Describes the digital experience without suggesting a physical print is sent. |
| Main online action | `EXPERIENCE FOTOHVN ONLINE` | Explicit destination and an accessible invitation. |
| Quiet supporting line | `A digital keepsake, made here. The enclosed booth awaits in person.` | Distinguishes online PNG creation from visiting or booking the physical booth. |

Generated Experience images should carry the messages directly: luminous curtain folds and an intimate empty enclosure; a camera or soft flash in warm material detail; photographic paper and an original frame composition. Use purpose-made still lifes or an empty booth scene rather than generated people impersonating customers. The root selects the actual imagery and composition. Replacing these two images does not imply removing the real guest album elsewhere.

Place the principal online invitation inside `#experience`, after the experience promise and with a genuine route link. Recommended secondary entries are a quiet `TRY IT ONLINE` text link near the physical keepsake narrative in `#prints`, and `ONLINE BOOTH` in the navigation's link list. Keep the existing `FIND A BOOTH` and `RENT FOTOHVN` actions prominent. Add the navigation entry to both desktop and mobile menus only if the selected composition passes the 1024–1199px width check; the Experience invitation plus Prints link already provides two useful entrances.

The online page gets a compact FOTOHVN header and a clear `BACK TO FOTOHVN` link. Give that link a full home destination, such as `/fotohvn#experience`, rather than a hash pointing to a nonexistent section on the online route. A successful final result may include a restrained physical-booth link after the download action. It should not interrupt capture or masquerade as the next required step.

## Four-stage interaction contract

### 1. Choose the layout and frame

Opening the route displays a serif invitation such as `Make a little moment.` and a sentence describing the four stages. It does not request the camera. Default to the four-picture Long Strip, Ivory frame, and Original look so the visitor can proceed immediately; the options remain explicit and editable.

Use two native radio groups: `Layout` and `Frame`. Each layout shows its exact photo count, arrangement, and aspect ratio. Each frame shows a meaningful miniature of its margin and typography treatment. Selecting either updates one dominant full composition preview, with stable dimensions within its viewing area. A concise summary reads, for example, `Long Strip · 4 photographs · Ivory`.

The empty positions in the layout preview are functional numbered photo windows, not substitute editorial artwork. Give them discernible bounds and reading order. Keep the selected state visible through a checked control or rule plus text; do not rely on brass color alone. All 16 layout/frame combinations are valid in the proposed starter collection, so avoid a compatibility matrix in the visitor's flow.

Primary action: `CONTINUE TO CAMERA`. It opens stage 2 with the selection preserved. Layout options can be shown in a calm two-by-two set on narrow screens, with the frame treatments as a short, labelled set. They must not become a 16-card catalogue.

When returning from a later stage, this screen shows the actual captured photographs in the proposed composition. A layout change preserves photographs according to the layout-change rules below. Frame changes alter the surround and type treatment only.

### 2. Capture and arrange the photographs

Initial state contains a useful camera explanation and two actions: `USE MY CAMERA` and `CHOOSE PHOTOS FROM THIS DEVICE`. Supporting copy can read `Your photographs are processed on this device and aren't uploaded.` Camera permission is requested only after the explicit camera action. Request video without audio.

Once the video is ready, show a live 4:3 viewfinder and the chosen composition or photo positions. The video element must be ready and have nonzero dimensions before capture is enabled. The primary action tells the visitor what it does: `TAKE 4 PHOTOGRAPHS`, adapting to the number of empty positions. On an incomplete session, use `TAKE THE REMAINING 2` or the corresponding singular label.

Proposed default: one explicit press starts the remaining positions in reading order, with a visible three-second countdown before each photograph. Show `Photograph 2 of 4`, an active-position outline, and a `STOP CAPTURING` action throughout. Capture from an actual ready video frame at the end of each countdown. Lock duplicate shutter presses and schedule one sequence at a time. Each successfully captured photograph is committed immediately; stopping the sequence retains those already captured and leaves the remaining positions empty. A brief placement transition gives feedback without delaying the next countdown unnecessarily.

After the last position fills, the same stage becomes a review workspace, with a heading such as `Make them yours.` and the completed frame preview. Move focus once to the review heading after the sequence finishes. Do not scroll or move focus after every photograph. The visitor can select any photo position and choose `RETAKE`, `MOVE`, or, for the fallback path, `REPLACE FROM DEVICE`. The primary continuation is `CHOOSE YOUR LOOK`.

During ordinary review, camera preview can remain available only while clearly visible and in this stage. Stop the camera on leaving stage 2. Returning to edit photographs restores the composition and provides `RESUME CAMERA`; it must not silently restart the camera while the visitor is editing layout or filters.

**Mirroring:** propose a mirror view by default for a front-facing camera, with an explicit `Mirror` control whose setting affects both the live preview and committed photographs. If the implementation instead stores an unmirrored original and an orientation flag, the compositor must apply the same flag everywhere. The visitor must never see a final strip unexpectedly flipped relative to capture review. Imported images keep their decoded orientation and are not silently mirrored.

**Individual retake:** selecting photo position 2 targets that position only. Show `Retaking photograph 2` while keeping its old image in the composition. Capture a replacement into a temporary candidate, then provide `USE THIS PHOTOGRAPH`, `TRY AGAIN`, and `KEEP ORIGINAL`. Accepting replaces exactly that position; cancelling a countdown, cancelling the file picker, a camera error, or choosing Keep Original leaves the existing image intact. Retake does not restart the full strip.

While a countdown or unaccepted retake is active, layout changes and reorder controls are temporarily unavailable so the target cannot move beneath the camera operation. The visible cancel/keep-original action always remains available. Leaving the capture stage cancels the pending operation while retaining committed photographs. A late async result from the cancelled operation is ignored and its resources released.

**Photo arrangement:** the selected frame itself exposes photo-position buttons. A visible selection outline and caption identify the selected photograph. Touch users select the photo, choose `MOVE`, and tap a destination. Keyboard users select a position, activate Move, and use labelled destination buttons or a native destination select plus an Apply button. Pointer drag can be added to the same model; the other methods are first-class features. Occupied destinations swap photographs, while empty destinations receive the moved photograph. Tell the visitor this before the action and announce the result. Escape cancels a pending move and restores focus to the source position.

The stage can continue only when every active position has one committed photograph, no retake/capture is pending, and the preview corresponds to the current session revision. The disabled action has a nearby useful count such as `2 photographs still to take`.

### 3. Choose the photographic look

Use a serif heading such as `Find the feeling.` and a text-led radio group for the starter looks. The dominant preview always includes all selected photographs, their current order, the selected layout, its frame artwork, and its footer typography. Filter option thumbnails may show a representative photograph, but they do not replace this complete composition preview.

Selecting a look changes photographs only. The paper, frame lines, and brand typography keep their authored colors. Applying a new look always starts from the unfiltered source photographs. Rapid selections must not allow an old asynchronous render to replace the latest preview. Keep the current valid preview until the latest replacement is ready, then crossfade without changing its layout bounds.

Keep `EDIT PHOTOGRAPHS` and `CHANGE FRAME` available. These return to the corresponding earlier stage with all session state intact. The selected look remains chosen while editing; it should still be applied when the visitor returns. Small frame changes may be made here in a collapsible or compact control if the selected visual concept makes this clear, but layout selection belongs to stage 1.

Primary action: `MAKE MY PHOTOSTRIP`, adapting to `MAKE MY PHOTO SHEET` for the Contact Sheet if that proposal is kept. Enable it only when the latest chosen composition has rendered successfully. Freeze the selection into a render snapshot when proceeding, produce the full-resolution output, then open stage 4. The actual render operation determines readiness; animation timers do not.

### 4. Reveal and download

Use a heading such as `A little moment, yours to keep.` Show the completed image as the dominant object on paper, with the frame fully visible. The final displayed image is derived from the same completed PNG blob offered for download. No separate CSS-only filter or DOM-only frame treatment may affect the displayed result.

Primary action: `DOWNLOAD PNG`. Also provide a clear way to `EDIT MY STRIP` and a less prominent `MAKE ANOTHER`. Editing returns to stage 3 with source photographs intact. Making another clears the session only after a short product confirmation when it would discard existing photographs. The confirmation names that consequence; cancellation returns to the finished strip.

Use a predictable filename such as `fotohvn-long-strip-20260914.png`, with no personal device filename copied into it. Surface rendering or browser handoff failures with a retry action while keeping the finished image. Where a browser cannot save through a download link, provide `OPEN IMAGE TO SAVE` using the already generated image. Do not state that a file has been saved merely because the browser received the download action.

## State and preservation rules

Implement the session logic independently of the camera UI and animation library. A reducer or equivalently explicit transition module should own mutations; components dispatch semantic actions. Resource creation/cleanup remains in adapters and effects.

| State element | Meaning |
|---|---|
| `stage` | One of layout, capture, look, download. |
| `layoutId`, `frameId`, `lookId` | Stable, versioned registry IDs for the selected starter design. |
| `photosById` | Immutable decoded source photographs indexed by photo ID; contains blob, dimensions, source kind, and any orientation/mirror metadata. |
| `assignments` | A photo ID or null for each stable slot ID in the active layout. |
| `unplacedPhotoIds` | Ordered IDs retained when a layout has fewer positions. |
| `selectedSlotId` | Current photo-position selection; independent of the photo ID occupying it. |
| `captureOperation` | Idle, requesting camera, countdown, acquiring, or pending replacement; includes operation ID and a stable target slot ID where needed. |
| `pendingReplacement` | Candidate photo ID plus target slot ID and old photo ID. It is not committed into assignments. |
| `moveOperation` | Source slot or tray photo ID plus a pending destination; cancellation does not mutate assignments. |
| `revision` | Increments after a committed render-affecting change. |
| `renderState` | Requested revision, completed revision, preview resource, output snapshot/blob, and error. |

Photo IDs identify source images, not positions. For example, `[A, B, C, D]` in positions 1–4 becomes `[C, B, A, D]` after swapping positions 3 and 1. It does not rename the source images or recapture any pixels. Keep position numbering fixed to the layout's documented reading order: top-to-bottom for strips and left-to-right, top-to-bottom for the Contact Sheet.

Invariants to enforce and test:

- Every non-null assignment resolves to one source photo. Each source appears in at most one active slot; duplicates are not a hidden side effect of moving.
- Every retained committed source belongs either to an active slot or the unplaced tray. A pending candidate is the sole temporary exception.
- Filters, frame changes, and arrangement do not mutate the stored source pixels.
- A pending retake leaves the old assignment unchanged until explicit acceptance.
- A cancelled operation cannot later commit through a delayed permission, decoding, capture, or render promise. Match operation IDs and revisions before accepting async results.
- Final output always comes from a complete, immutable snapshot. A later edit invalidates the previous final output for the current session revision.

### Layout changes without lost photographs

For a layout change, form an ordered pool from assigned photographs in the current layout's reading order, followed by the unplaced tray. Fill the new layout in reading order from that pool. Put the remainder back into the tray; add empty positions if the new layout has more slots than retained photographs. This rule is simple enough to communicate and stable across every input method.

Example: Long Strip `[A, B, C, D]` → Pair `[A, B]` with tray `[C, D]`. Swapping the pair gives `[B, A]`. Returning to Long Strip gives `[B, A, C, D]`. Reducing the layout never deletes C and D. Show `2 photographs are kept below` and expose the tray so the visitor can replace a chosen position with an unused photograph.

Moving a tray photograph onto an occupied position returns the displaced photograph to that tray location; moving it to an empty position removes it from the tray. Both actions preserve source count. For a retake accepted into an occupied slot, release the old source once it has no references; an unlimited history of discarded retakes is outside this scope. The source being replaced remains retained until the new photograph is accepted.

Frame changes use exactly the same photo rectangles in this starter collection, so they do not recrop photographs. All proposed layouts use 4:3 photo windows. Camera framing and file-import preview use the same cover/crop rule. If arbitrary uploaded proportions need adjusting, keep that adjustment bounded to fitting the target window; this scope does not require a free-form editor, partial-image retouching, or a new crop stage.

## Proposed starter collection

### Layout geometry

Use a data registry containing output dimensions and exact pixel rectangles. Dimensions below are original digital output proposals, not claims about physical booth paper sizes or printer DPI. Every photo window is 4:3. All measurements are at full export resolution; previews scale the whole composition uniformly.

| ID / label | Photographs | PNG dimensions | Photo rectangles in reading order |
|---|---:|---|---|
| `long-strip-v1` / Long Strip | 4 | 900 × 2700 | `(60, 60, 780, 585)`, `(60, 675, 780, 585)`, `(60, 1290, 780, 585)`, `(60, 1905, 780, 585)` |
| `story-strip-v1` / Story Strip | 3 | 900 × 2100 | `(60, 60, 780, 585)`, `(60, 675, 780, 585)`, `(60, 1290, 780, 585)` |
| `pair-v1` / Pair | 2 | 900 × 1500 | `(60, 60, 780, 585)`, `(60, 675, 780, 585)` |
| `contact-sheet-v1` / Contact Sheet | 4 | 1800 × 1500 | `(60, 60, 820, 615)`, `(920, 60, 820, 615)`, `(60, 715, 820, 615)`, `(920, 715, 820, 615)` |

Use a defined footer region below the last photo row: it starts at the photo row's lower edge plus 30px and ends 45px before the output edge. Center the authored wordmark and supporting line within that region. Every frame must keep its type and rules inside this reserved area. With the current geometry, the Contact Sheet has the shallowest footer, so validate its type first. Export safe-area checks must reject any photo rectangle or text bounds outside the image.

### Frame treatments

The frame is part of the exported artwork, not a border supplied by CSS around a downloaded photo. Reuse the same loaded brand typefaces for the authored wordmark and quiet footer line. Product controls may show `Online starter frames` in supporting copy; avoid physical-presets claims.

| ID / label | Authored design |
|---|---|
| `ivory-v1` / Ivory | Warm Ivory mat, clean photo edges, centered ebony Cormorant wordmark, and the small Manrope line `A LITTLE MOMENT, YOURS.` A calm default with generous paper. |
| `archive-v1` / Archive | Off-white mat with a single Hairline rule separating photographs and footer. Small left-aligned FOTOHVN wordmark balanced by `ONLINE` at the right; no decorative serial numbers. |
| `walnut-v1` / Walnut | Flat Dark Walnut surround, Off-white wordmark, and one fine Antique Metal rule. A framed-object option within the otherwise light interface. No metallic texture or glossy gradient. |
| `gallery-v1` / Gallery | Cream Paper outer mat, a thin Off-white inset mat outside each photo rectangle, and minimal centered ebony wordmark. Fine Hairline detail gives the impression of a mounted photographic edition. |

Frame embellishment lives outside the photo rectangles. Tiny raster decorative flourishes are unnecessary; these designs should remain crisp at every export size. If the root chooses a generated texture, prepare a licensed/purpose-made local asset and make it part of the compositor's frame recipe rather than a browser-only background.

### Photographic looks

Propose four authored starter looks with restrained differences. Display the stage as `Choose your look` and use a short explanation mentioning photographic filters if clarity requires it. These names deliberately do not claim to be the physical booth's Classic, Vintage, Monochrome, or Signature presets.

| ID / label | Direction | Initial deterministic recipe for visual tuning |
|---|---|---|
| `original-v1` / Original | Natural color, as captured. | Identity transform. |
| `warm-paper-v1` / Warm Paper | Gently warmer, restrained saturation, soft contrast. | RGB gains 1.035 / 1.000 / 0.950; saturation 0.92; contrast 0.96 around 0.5; black lift 0.02. |
| `silver-v1` / Silver | Rich neutral black-and-white with open highlights. | Luma from 0.2126 R + 0.7152 G + 0.0722 B; contrast 1.10 around 0.5; offset 0.01; identical output channels. |
| `soft-fade-v1` / Soft Fade | Muted color and a quiet paper-like softness. | Saturation 0.80; contrast 0.90 around 0.5; lift 0.035; red offset +0.015 and blue offset −0.010. |

These are starting recipes to tune on neutral, colorful, light, and dark reference photographs after visual selection. Define their operation order in code, work in one documented color space, and clamp channel values. The same kernel and version must render preview and export. Avoid approximate CSS equivalents, randomized grain, face alteration, or compositing the look over the frame. Keep Original as an exact unfiltered baseline after the normal orientation/crop process.

The eventual physical-booth handoff should supply layout dimensions and slots, frame overlays and typography, filter implementation or LUTs, intended color space, version names, and reference outputs. Replace registry entries and renderer adapters at that seam; the session state and four-stage workflow should not need rewriting. Keep online starter IDs stable while adding new entries so existing tests retain their meaning.

## Image pipeline and camera lifecycle

### One composition renderer

Create one composition contract accepting a frozen selection, ordered source references, slot geometry, frame recipe, and look recipe. Its operation order is: opaque paper/frame background → oriented and consistently cropped photographs → photographic look inside each photo window → authored frame lines/typography. Produce an opaque PNG with the selected layout's exact dimensions.

Use the same renderer for complete preview and export. Rendering a smaller preview is acceptable if it is a uniform scale of the same geometry and photographic pipeline; the final preview must use the actual completed download blob. Hit targets for positions sit in an interactive overlay whose normalized bounds derive from the layout rectangles. Selection rings, position numbers, countdowns, and focus controls must never be baked into the exported image.

Await the actual local web fonts before rendering footer typography. Resolve the loaded family name; a canvas font declaration cannot merely reuse a CSS `var(...)` string. Font loading, source decoding, and complete geometry are render prerequisites. A slow or failed prerequisite leaves a retryable state and retains the previous valid image. A look change can invalidate a render in progress; only the latest revision may update the preview or enable the final action.

Use Blob/Object URL resources rather than repeatedly encoding full-resolution data URLs into React state. Normalize file orientation when decoding. Keep source resolution sufficient for the largest corresponding export window, with an explicit bounded decode policy for unusually large uploads. Release image bitmaps and revoke obsolete object URLs when they are replaced or the session ends. Keep the current preview/download URL alive while it remains in use; do not revoke it immediately after initiating a browser download.

### Camera states and recovery

The camera adapter reports idle, requesting, ready, denied, unavailable, interrupted, or error. A request is tied to an operation ID. If permission resolves after the visitor changes stage, resets the session, or navigates away, stop every returned media track immediately and ignore the result. Stop prior tracks before switching a device or creating a new stream. A ready state must mean both a live video track and a usable decoded frame.

On denied permission, show a short explanation with `TRY CAMERA AGAIN` and `CHOOSE PHOTOS FROM THIS DEVICE`. If no camera exists, the device is busy, the browser cannot provide the API, or the page does not have the required secure context, offer the same device-photo path. Avoid an endless spinner or a repeated permission loop. Keep setup copy clear without exposing internal exception names.

An unanswered browser permission prompt can remain pending. Keep the device-photo alternative and a cancel action usable while awaiting permission; invalidate the request if the visitor takes either path. The root verified the platform requirement in MDN, recorded in `references-and-visuals.md`: localhost is suitable for development camera use, while a public camera launch needs HTTPS. `website/site.config.ts` currently declares an HTTP IP staging URL. That destination is not a verified camera-ready launch origin. Secure deployment remains a future launch prerequisite rather than a reason to add a backend to this local image workflow.

During a countdown, visibility loss cancels the pending countdown and preserves committed photographs. If the camera track ends or the device disconnects, cancel acquisition and any unaccepted candidate, keep the old target photograph, and offer Resume/Retry or device photos. Returning to the foreground must not unexpectedly take a picture.

Device-photo input is a complete alternative path, not a dead-end error helper. Allow a batch to fill empty positions in file-selection order up to the required count, or one file for a selected replacement. Decode and validate each image before committing it. A cancelled picker or invalid replacement leaves the old photograph intact. If a batch includes an undecodable image, identify it without losing the files already accepted. Use the photo tray for explicitly retained extras; do not silently keep an unbounded library of large files.

Keep all camera frames, imported images, candidate images, and render outputs in memory on the device. They must not enter request bodies, analytics events, logs, query strings, localStorage, IndexedDB, or a service-worker cache. An export is a user-triggered local file operation. Verify that claim with a browser network inspection, not only a source search. The route may fetch its ordinary scripts, fonts, and authored starter assets.

## Interaction, motion, and responsive composition

Functional placement should remain consistent across the selected visual concept: one dominant camera/composition, one clear set of choices, and a nearby next action. Large editorial type introduces the stage; it must not compete with the viewfinder or make controls fall below a long decorative opening.

On desktop, use the site's 1280px content discipline and a broad preview plus a quieter control column. Keep the four-stage progress and stage heading aligned to that grid. A preview may be sticky only when its full controls fit within the viewport; fall back to normal flow for short windows. This route needs no scroll hijacking or pinned narrative timeline.

At tablet widths, give both columns `minmax(0, ...)` and `min-inline-size: 0`, then stack before either becomes cramped. Below approximately 768px, put the live camera first with its shutter and stop controls immediately adjacent. During capture, use a compact row of four position buttons rather than forcing the full tall strip above the shutter. The complete strip remains available for review and is dominant at stages 3 and 4. Layout options use a two-by-two arrangement; controls and captions wrap naturally within 24px page gutters.

At 320px, the progress labels may wrap into a deliberate two-row presentation if needed. Body text remains at least 16px. Stage headings use fluid sizes and a wide text area, generally no more than three lines. Main actions are at least 48px high; position controls and secondary actions meet the existing 44px minimum. Do not shrink interactive thumbnails below that target size to force a row to fit. Keep primary controls in document flow or, if a sticky action strip is selected, reserve its height and safe-area space so it cannot cover photographs or focused controls.

Use native inputs, buttons, and links where possible. Radio groups provide named legends and native keyboard selection. Position buttons expose names such as `Photograph 2, captured, selected`; empty positions expose their status. Move destinations include their reading-order position and whether they will swap an occupied photo. Announce capture completion, move results, a committed retake, and errors through a concise live region. The visible countdown can announce whole seconds, without repeated animation-frame announcements.

Focus follows purposeful state transitions: stage change → stage heading; completed sequence → capture review heading; retake acceptance/cancellation → target position; move cancellation → source position. Avoid hiding or unmounting the currently focused element before its successor is ready. Native controls provide Enter/Space support. If a custom control is unavoidable, test Enter, ` `, and legacy `Spacebar` where applicable, in addition to its declared arrow-key behavior.

| Interaction | Standard motion | Reduced motion |
|---|---|---|
| Layout/frame selection | 150–240ms rule/surface response; composed preview replaces or crossfades in stable bounds. | Immediate selection and image replacement. |
| Stage transition | Up to 16px vertical movement with a 240ms fade; stable page shell. | Immediate content and focus change. |
| Countdown | Large clear numerals with a quiet opacity change. Optional restrained viewfinder shutter response only at capture. | Static changing numerals; no flash. |
| Photo committed | Up to 16px placement movement and a 240ms emphasis on the target position. | Immediate photograph and visible selected state. |
| Reorder | 240ms positional settlement for the two affected photographs. | Immediate swap with announcement. |
| Look selection | 240ms crossfade once the current rendered revision is ready. | Immediate valid image replacement. |
| Finished strip | Up to 600ms paper reveal, limited to a short translation/opacity or a restrained mask within the image bounds. | Fully visible output immediately. |

Use the existing GSAP dependency when the chosen concept needs coordinated movement; keep each animation scoped to its component and clean it up on unmount. Pure transition feedback can use CSS tokens. Do not import the editorial scroll-reveal wrapper into the camera workflow: state transitions, readiness, cancellation, and focus should remain under the photobooth's own control. No looping decorative motion or artificial developing delay is needed. A real rendering wait can have honest progress feedback. Changing the reduced-motion preference during a session immediately settles active movement without cancelling a photograph the visitor already committed.

## Current integration seams

The following file locations were verified in the planning snapshot; inspect their current contents before implementation because the worktree contains unrelated changes.

| Seam | Required integration behavior |
|---|---|
| `src/app/online/page.tsx` — new route | The configured base path makes this `/fotohvn/online`. Use a server route shell with a bounded client photobooth entry for browser APIs. The root layout already supplies fonts and global tokens. Add route metadata describing the digital booth. |
| `site.config.ts:1–5`, `next.config.ts:6–10` | Native anchor and public asset URLs use `withSiteBasePath`. Existing code uses native anchors. If using Next Link instead, read the installed Next guide and avoid applying the base path twice. Never create `src/app/fotohvn/online`. |
| `src/components/UpperExperience.tsx:11–75` and its CSS | Replace the Experience interior and images while preserving the `EditorialLift` wrapper, hero input, `#experience`, and the focusable `#experience-heading` with `tabIndex={-1}`. The current customer sources are `sisters.webp` and `weekend.webp`. |
| `EditorialLift.tsx:13–16` | Preserve the single real DOM section and wrapper hooks `[data-editorial-lift]`, `[data-lift-stage]`, `[data-lift-hero]`, `[data-lift-experience]`. The online route does not use this wrapper. |
| `editorialLiftController.ts:5–8, 21–36, 45–73` | Selectors are required, not incidental styling hooks. The controller makes the Experience layer inert below progress 0.45 and measures stage height as the maximum of hero and Experience heights. Keep new content intrinsically measurable. Avoid absolute-positioning the whole new invitation outside the section's measured flow. |
| `EditorialLift.module.css:19–21` and current `UpperExperience.tsx:37` | `[data-lift-follow]` fades through a CSS variable during the lift. Retain it on the intended follow-on heading phrase if the selected design keeps that rhythm. Keep the new route CTA fully readable whenever its layer is interactive; do not hide a focusable CTA with this opacity hook. |
| `editorialLiftController.ts:75–139` | Exact `href="#experience"` links receive animated anchor handling and focus the heading. Other route links remain native. Direct hashes, back navigation, resized content, and font completion must still work after redesign. ResizeObserver already observes the hero and Experience wrappers. |
| `ExperienceMotion.tsx:32–89` | The current UpperExperience is wrapped by EditorialLift, not ExperienceMotion. Its existing `data-reveal`/`data-float` attributes do not by themselves install these GSAP effects. If new Experience motion is added, assign one owner per transform and avoid applying another scroll transform to the lift layer. |
| `EventPhoto.tsx:6–16` | The image loader rewrites each `.webp` URL to a `-<width>w.webp` candidate. New generated Experience assets used through this component need all configured width candidates. Otherwise choose an appropriate independent image delivery path. The online camera/object URLs must not pass through this static-event-photo loader. |
| `SiteChrome.tsx:7–11, 88–95, 126–138` and CSS | The shared navigation array reaches both menus. Verify the extra link at the 1024–1199px breakpoint where the current bar already uses tighter spacing. Reusing SiteChrome on the online route without adapting its home hash links would create dead destinations. |
| `MiddleExperience.tsx:16–32` | Place an optional online invitation after the printed-keepsake copy, preserving the truth of its physical-print description and the guest-album destination. Avoid silently replacing `SEE THE GOOD COMPANY`. |
| `src/app/globals.css` | Existing font variables, control styles, focus treatment, and reduced-motion rules are available. Keep new styling inside a component CSS module. Preserve `.siteMain`'s sticky-compatible `overflow-x: clip; overflow-y: visible`; correct intrinsic overflow at its source. |

Suggested new internal module boundary under `src/components/onlinePhotobooth/`:

- `presets`: versioned layout geometry, frame recipes, and look definitions.
- `session`: state, semantic actions, complete-stage guards, retake/move/layout transitions.
- `camera`: stream acquisition, readiness, capture, cancellation, and track cleanup.
- `compositor`: source decoding/crop, look kernel, frame drawing, and PNG output.
- `OnlinePhotobooth` with focused stage components: accessible UI, progress, and scoped motion.

The important boundary is shared geometry and source identity, not the exact filenames. The preview component and download path must not each invent their own filter or frame implementation.

## Implementation sequence after visual selection

1. Root records the selected concept, final Experience copy, starter collection choices, and the decisions below. Complete when a fresh implementer can map the chosen imagery and stage composition to this interaction contract without choosing another design direction.
2. Inspect the current worktree and local Next guides required by `website/AGENTS.md`. Assign production-file ownership before parallel work. The root's runtime note records a `spawn EPERM` startup attempt despite a matching route rendering in the in-app browser; resolve startup and verify the newly changed route afresh, rather than treating that observation as a successful restart. Complete when unrelated modifications and shared seams are documented, including the homepage's lift dependencies.
3. Implement the preset registry, session transitions, and compositor boundary with focused logic/geometry tests. Complete when retake cancellation, reordering, layout preservation, and immutable source rendering hold independently of the browser UI.
4. Implement the online route, camera/file adapters, four stages, and chosen motion. Complete when the granted-camera and device-photo paths both reach a real downloadable PNG.
5. Implement the selected Experience design and route triggers with prepared image candidates. Complete when the hero/lift/Experience anchor still functions and every entry opens the verified online route.
6. Run focused logic tests, existing `npm run test:hero`, lint, typecheck, and build. Treat pre-existing failures separately with evidence; never turn a skipped gate into a pass.
7. Use a fresh independent conformance agent to compare the final selected design plan and source, render the routes, and write a result of exactly `passed` or `blocked`. Responsive QA starts only after conformance passes.
8. Use fresh desktop, tablet, and mobile reviewers, including 320px evidence; synthesize the findings. Each repair gets a new implementation agent, followed by new conformance and responsive reviewers. Completion requires the acceptance cases below, clean console evidence, and no actionable P0/P1/P2 findings. A physical-camera limitation must remain explicitly unverified even if synthetic camera tests pass.

## Acceptance cases

These are required observed outcomes, not a request to create redundant tests for every line of UI. Use focused reducer/compositor tests for state rules; real browser interaction for permission flow, touch/keyboard semantics, rendering, media lifecycle, and downloading. Synthetic test images should have distinct colors/text so identity and ordering are unambiguous.

| Case | Setup and action | Required outcome |
|---|---|---|
| E1 — Experience assets | Inspect `#experience` at desktop and 320px. | The two customer photographs are replaced by selected purpose-made imagery. Copy communicates enclosure, choice, keepsake, and a distinct digital invitation. Other guest-album content is outside this scoped replacement. |
| E2 — Lift integration | Use hero Explore, navigation Experience, direct `#experience`, Back, resize, and reduced motion. | Heading remains visible and receives intended focus; no clipped CTA, hidden focusable layer, double scroll controller, or jump back from later content. |
| E3 — Entry routes | Activate all online links with keyboard and pointer, including mobile menu. | Each opens `/fotohvn/online` successfully. Header/Back links from the online route return to the real editorial page. |
| L1 — Initial selection | Open a new session; select every layout and frame. | Preview shows correct count/geometry and authored frame; no camera request occurs. Selected state and summary agree. |
| L2 — Fewer positions | Start with `[A,B,C,D]`; choose Pair; swap its positions; return to Long Strip. | Pair has `[A,B]` plus tray `[C,D]`; the return gives `[B,A,C,D]`. No photograph is discarded or duplicated. |
| L3 — More positions | Capture two photographs; choose Long Strip. | First two remain, two positions are empty, and continuation to Look remains unavailable until filled. |
| C1 — Camera sequence | Grant camera; start the four-photo sequence. | Four actual frames fill distinct positions in order, each with a countdown. Viewfinder, review, and export have consistent mirror orientation. |
| C2 — Stop sequence | Stop during the countdown for photograph 2. | Photograph 1 remains committed; remaining positions stay empty; no delayed shot appears. Starting again fills only remaining positions. |
| C3 — Cancel retake | From `[A,B,C,D]`, retake position 2, produce E, then Keep Original. | Assignment remains `[A,B,C,D]` throughout the pending retake and after cancellation. Other photo resources are unchanged. |
| C4 — Accept retake | Repeat the previous case and accept E. | Assignment becomes exactly `[A,E,C,D]`. Position selection/focus returns to 2; only the unreferenced replaced source may be released. |
| C5 — Error in retake | Disconnect camera, deny a retry, or cancel the replacement file picker. | Original target remains; useful recovery actions appear; continuation can resume once the pending operation is cancelled. |
| C6 — Late media result | Request camera, then change stage/reset/navigate before permission resolves. | Late returned tracks are stopped, no camera view appears in the new stage, and no photograph or state is overwritten. |
| C7 — Background interruption | Hide the document during a countdown, then return. | Countdown is cancelled; committed photos remain; no surprise capture occurs on return. |
| C8 — Fallback completion | Deny camera or use a device with no available camera, then choose local images. | The same arrangement, framed filter preview, final result, and PNG download are reachable. Invalid files and picker cancellation retain prior state. |
| R1 — Equivalent arrangement | Move position 3 to occupied position 1 via drag, tap-destination, and keyboard in separate fresh sessions. | All methods yield `[C,B,A,D]`, with no duplicates; visible state, focus, and announcement agree. |
| R2 — Tray placement | Put a retained tray photograph into an occupied position, then an empty one. | Occupied placement returns the displaced photograph to the tray; empty placement consumes the tray entry. Source count remains correct. |
| R3 — Retake lock | Attempt layout/reorder changes during a countdown or candidate review. | The target remains stable; cancellation remains usable; no partial arrangement mutation occurs. |
| F1 — Non-destructive looks | Toggle every look, including Original, after a retake and reorder. | Every photograph uses the selected look; frame colors remain authored; Original returns to the unfiltered source appearance and sources retain their identity. |
| F2 — Framed preview | Choose each frame while retaining one look. | Complete preview displays the actual frame, type, layout, and photograph order with stable surrounding UI. |
| F3 — Render races | Select multiple looks quickly while an earlier render is delayed. | Only the latest revision can become visible or enable Make My Photostrip. |
| D1 — Output identity | Complete stage 4, download, and inspect the file. | PNG dimensions equal the selected registry dimensions; opaque background, order, crop, look, frame, and typography match the final shown blob. No UI controls or position labels are exported. |
| D2 — Geometry coverage | Render synthetic sources through every layout/frame pair; exercise each look kernel. | Every photo rectangle is inside the output, footer type is unclipped, and the selected look is confined to photo windows. The Contact Sheet's shallow footer also passes. |
| D3 — Download recovery | Fail a render/export or use the browser's alternate save path. | Existing sources/result remain available; retry or Open Image to Save works; UI does not claim a confirmed save it cannot observe. |
| P1 — Local image handling | Inspect network activity while capture, retake, reorder, filtering, and downloading occur. Inspect browser storage after use. | No photograph/candidate/blob content is transmitted or persisted by the app. Only ordinary app assets and the user-triggered local save are involved. |
| P2 — Cleanup | Leave capture for Look, leave the route, and reset a completed session. | Camera tracks stop as specified; stale timers/render work cannot update state; obsolete image resources are released. |
| A1 — Keyboard flow | Complete layout → device photos → per-slot retake → reorder → look → download without pointer dragging. | Every function is reachable with visible focus and understandable names; no focus trap or lost focus; active stage is announced. |
| A2 — Touch and narrow layout | Test 320×720, 375px/390px phone widths, tablet portrait/landscape, desktop, and a short landscape viewport. | Camera controls and frame choices remain readable and reachable. Document `scrollWidth` does not exceed `clientWidth`; no clipped controls or manufactured overflow hiding. |
| A3 — Motion/zoom | Use reduced motion from initial load, change preference mid-session, and inspect enlarged text. | Travel/flashes stop, readiness still completes, layout reflows, and primary actions remain visible and operable. |
| Q1 — Final gates | Run focused tests, existing hero tests, lint, typecheck, build, conformance, and responsive browser review. | Results are recorded against the final integrated source. Browser console is clean; skipped or blocked evidence is named precisely. |

## Decisions for root synthesis

These are internal consolidation items before a fresh implementer starts; they do not each require another user question.

1. Select the Experience composition and online-page visual direction from the three user-reviewed concepts, then bind generated assets and final copy to it. The interaction contract remains common to those concepts.
2. Confirm the four proposed layout dimensions, including whether Contact Sheet should remain as a wider alternative; adjust its product label and final action if retained.
3. Confirm frame names/footer copy and tune the four look recipes on actual reference images. Keep the distinction between online starter treatments and future physical-booth presets explicit.
4. Confirm the proposed automatic sequence with a three-second countdown and visible Stop action, plus the explicit mirror behavior. If the selected design uses one-at-a-time capture instead, preserve the same commit/cancel/retake rules and update C1/C2 accordingly.
5. Choose secondary trigger placement after inspecting the navigation width budget; Experience plus Prints is the least intrusive pair, with Online Booth in navigation added when it fits.
6. Reconcile web-reference findings into the selected master plan. The three URLs in `brief.md` are root-owned references and were not independently verified by this agent; copy only useful workflow principles, not artwork or unrelated product features.
7. Set browser/device verification coverage and identify whether a real camera can be exercised. Feature detection and synthetic input can prove broad state behavior, but physical-camera behavior must not be reported as tested without corresponding evidence.

This planning deliverable is complete when the root has this file and its source seams. Product implementation and conformance remain future work after visual selection.
