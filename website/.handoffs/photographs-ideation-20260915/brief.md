# Photographs step ideation — 2026-09-15

Status: three visual concepts, awaiting user selection. No production changes authorized in this ideation turn.

## Brief
Simplify step 2 of the existing online booth without removing functionality. Keep the established FOTOHAVN warm ivory, Cormorant Garamond + Manrope language and four-stage navigation: Template, Photographs, Filters, Download. Camera requests access on entry; preview starts if permission is available. Capturing remains an explicit action. Handle denied/unavailable cameras with import and retry; respect manual pause until resumed.

Headline: Find your light.
Subheader: Let this moment become a memory.

## Preserve in all directions
- Existing selected template and actual photo crops, frame art, photo count, and photographs retained when navigating between steps.
- Automatic sequence for unfilled slots, three-second countdown per shot, stop mid-sequence without discarding accepted shots.
- Mirroring, pause/resume, camera retry, multi-file import and single-slot replacement.
- Retake candidate review: use photograph, try again, keep original.
- Reordering and swaps, drag-and-drop directly in template, plus tap/keyboard Move fallback.
- Retained photos from earlier templates available in a compact expandable drawer only when present.
- One combined selection/print-preview surface; no duplicated slot thumbnail rail.
- Continue to Filters only once complete and the composed preview is ready.
- Empty slots use the shared blank photo treatment. Existing photographs appear in template previews.

## Independent visual directions
Names describe generation intent, not displayed option numbering. The visible generated-result order determines option 1–3.

### The Live Studio
Large live camera left, single interactive actual Signature Strip right. Capture controls grouped under camera. Select or drag directly in print; contextual actions beside selected slot. Balanced capture-first workspace.

### Inside the Frame
One central large Signature Strip is the workspace. The next empty frame contains live camera; completed frames remain in place. Main shutter and camera settings beside this single canvas. Selected slots open an enlarged contextual view for retake review; no parallel duplicate selector.

### The Moment Table
Shared central stage alternates between live camera and full-size arrangement, using a quiet Camera / Arrange toggle. Show the Arrange state with Panorama Four, four side-by-side portraits; all manipulation occurs in this actual template. Compact live camera inset remains on until paused or leaving step. This provides room for detailed editing without simultaneously showing all controls.

## Reference evidence
Current implementation inspected live at http://localhost:3000/fotohavn/online, step Photographs. Screenshot: current-photographs.png. Source reviewed: OnlinePhotobooth.tsx, PrintPreview.tsx. Canonical DESIGN.md reread. Current page duplicates photo-position controls and shows camera instructions plus preview instructions plus separate actions at once.

## Interaction details for implementation after selection
Drag from desktop files into a slot to replace or onto template to fill open slots; preserve import validation and transactional replacement. Drag captured photo to occupied slot swaps. Retained photos remain recoverable. On touch offer tap-source/tap-destination Move and retain native scroll. Never silently replace the original during candidate review. Keep all native template aspect ratios, including horizontal Panorama Four.

## Displayed ImageGen result mapping
1. The Live Studio — C:/Users/QUINJ3875/.codex/generated_images/01a0a060-0f41-7260-b4a8-46a03937acc8/exec-059efd05-a89b-44a5-a3fa-8e6c75606fab.png
2. Inside the Frame — C:/Users/QUINJ3875/.codex/generated_images/01a0a060-0f41-7260-b4a8-46a03937acc8/exec-52bd61a7-b98a-4844-a891-69ac1e795b26.png
3. The Moment Table — C:/Users/QUINJ3875/.codex/generated_images/01a0a060-0f41-7260-b4a8-46a03937acc8/exec-99088270-056a-4d75-a25a-24609ae0c7b7.png

Generated once each and displayed in this order. These are conceptual mocks. Preserve real frame artwork, native geometries, shared blank treatment, and approved fonts in implementation; do not reproduce generated decorative photo gutters or serif drift in functional labels. Option 3 shows Panorama Four to demonstrate the horizontal arrangement surface, but all directions must support all existing templates.
