# Look at you — interactive metal-board proposals

2026-09-10. Proposal-only work requested with Product Design ideate. Application source remains unchanged; wait for visual selection before building.

## Brief

Replace the existing six-photo horizontal album with a scalable collection inspired by the actual booth's matte-grey metal display board, thin silver frame and clipped photographs. Visitors browse all approved guest photographs, click/tap a print and discover its testimonial. Preserve the warm editorial typography/palette and intimate, sincere feel. Hero, 3D explorer and other sections are out of scope.

User reference is copied as `metal-board-reference.png`. `current-gallery.png` is a fresh browser capture. Additional actual imagery supplied to every generation: `public/images/evia/friends.webp` and `prints-detail.webp`.

## Shared workflow / content rules

- Photographs are native focusable controls, click/tap/Enter equivalent; no hover-only access. Preserve adequate hit areas and visible focus.
- Expand one guest story at a time; display related photos, permissioned attribution and approved quote. Escape/Close returns focus to the selected print and preserves board position.
- All approved photos remain accessible through more boards/photographs, not a fixed decorative subset. Lazy-load imagery, avoid rendering hundreds of overlapping full-resolution images at once.
- Phone/tablet versions retain the metal board and clipped-print identity. Recompose into reachable groups; do not shrink a whole desktop board into illegible thumbnails. Tap opens readable story content. No mandatory drag, zoom, pinning or scroll trap.
- Motions support selection: tiny paper lift, a controlled flip or a short inline reveal depending on the direction. Reduced motion shows the same states immediately.
- No customer testimony was supplied. Every illustrated quote is labeled SAMPLE TESTIMONIAL — NOT A REAL CUSTOMER QUOTE, with no invented name. Do not publish these as testimonials or attribute them to pictured people. Implementation requires approved guest quotes/attribution and permission; without a quote use an honest editorial caption, not fabricated speech.
- No star ratings, likes, stock endorsements, upload workflow or third-party publishing.

## Independent concepts, not display numbers

### The Keepsake Board

One large metal board beside an editorial story column. Browse clipped photos → select a print → read its enlarged photograph and quote without losing the overview. Mobile opens a bottom sheet; desktop keeps the board in view. More photographs loads the next composed board. Easiest overview-to-story orientation.

### On the Other Side

One generous, loosely arranged board. Select a print → it lifts into a focused paper viewer → turn it over to read the testimony on the back. Close returns it to its original clip. Explicit Read the note/See the photo controls; reduced motion does not flip. Mobile uses a full-height readable viewer. Most tactile and intimate, with a more deliberate photo/quote interaction.

### A Wall of Little Moments

A sequence of compact metal boards, each housing a manageable group of guest photographs. Scroll through the collection → tap any print → a photo-and-quote story opens inline below that board. Next group continues ordinary scrolling; no modal and no infinite draggable canvas. Mobile stacks boards vertically at a readable scale. Most naturally scalable to a large collection; slightly less theatrical.

Each output shows one direction's primary selected state and a short workflow annotation. Labels are descriptive identities only; option numbers must follow the actual displayed ImageGen order. Mocks are still images, not implemented interactions; actual photograph files and approved quotes must replace any generated miniature drift at implementation.

## Completed displayed-order mapping

All three independent ImageGen outputs were displayed exactly once in the main conversation. Small step labels within an image describe its workflow, not alternative options.

| Displayed option | Direction | Generated preview |
| --- | --- | --- |
| 1 | The Keepsake Board | C:/Users/QUINJ3875/.codex/generated_images/01a0864b-7ffa-7920-a2c4-6bb8edae4292/exec-81f50baa-bd2f-4717-a587-5f992d651217.png |
| 2 | On the Other Side | C:/Users/QUINJ3875/.codex/generated_images/01a0864b-7ffa-7920-a2c4-6bb8edae4292/exec-9db99e31-0055-4259-a2a6-e0e8fbf74574.png |
| 3 | A Wall of Little Moments | C:/Users/QUINJ3875/.codex/generated_images/01a0864b-7ffa-7920-a2c4-6bb8edae4292/exec-04577850-582c-4d31-81ae-60632aec7e1a.png |

All outputs visibly include the sample-testimonial disclaimer. The third uses a family picture from the supplied gallery screenshot rather than the extra supplied group photo; its mapping is internally consistent in board and story. Generated miniature photos, frames and incidental control styling are conceptual: production must use source guest photographs, accurate small clips and canonical button radii, with reviewed guest permission/quotes. No new interactions were implemented, no application code edited, no server started and no deployment performed. Awaiting the user's selected visual.

## Subsequent selection and implementation

The user selected displayed option 2, **On the Other Side**, then requested all posted photos be clickable, inclusion of the published 2x6 strips, animated lift and fullscreen board viewing. The user explicitly approved editorial captions for now; no sample testimonials were published. This historical proposal record is retained unchanged above. Completed implementation and verification: `../guest-board-implementation/README.md` and `../../design-qa.md`.
