# Option 3 refinement — Camera and Arrange workflow

Date: 2026-09-15
Status: design refinement only; no production implementation yet.
Authority: latest user instruction requires camera always live while Camera tab is open. This supersedes prior pause/resume and frozen completed-camera proposals.

## Current visual targets
- refined-arrange.png: no camera inset or camera controls on Arrange.
- camera-capturing-v2.png: integrated viewfinder controls, countdown and capture progress.
- camera-complete-v2.png: live camera after completion, Mirror and Review photographs.
Earlier camera-capturing.png and camera-complete.png are superseded.

## Copy and controls
Headline: Find your light.
Subtitle: Let this moment become a memory.
Remove Your captured photographs will stay. Do not show Pause camera or Resume camera.
Stop capturing and Mirror belong inside the bottom of the viewfinder. During countdown, Stop capturing is the prominent central action, Mirror is visibly locked. When idle with open slots, central action becomes Take N photographs and Mirror is enabled. When complete, remove capture action and keep compact Mirror control. Use a proper mirroring icon in implementation; the generated mock's camera icon is not authoritative.

## Camera lifecycle
Camera requests access automatically on opening Camera, whether incomplete, completed, or retaking; never starts a capture automatically. Keep stream live for the whole Camera tab visit. Stop capturing cancels only the sequence, not the stream. Opening Arrange or leaving the Photographs step releases camera; no inset on Arrange. Permission denial or missing hardware still provides Retry camera and device import, without request loops. Do not present an idle camera as live if permission/hardware prevents access.

## Workflow stories
1. First entry with missing photographs opens Camera. Show live viewfinder, crop guide matching selected template slot, Take N photographs, Mirror and Import photos. No duplicate thumbnail selection rail.
2. Capture fills empty slots only with three seconds per shot. Show Photograph X of N, saved count, countdown and Stop capturing. Lock competing actions during sequence. Stop preserves accepted photos and keeps live view running; central action returns to Take remaining photographs. Once all slots are filled and composition is ready, automatically switch to Arrange, releasing camera because Camera tab has been left.
3. Arrange uses the actual template as preview and selector. Click/tap selects a photo; contextual Retake, Replace and Move appear near it. Drag onto occupied slot swaps. Provide equivalent tap/keyboard Move. Kept photos drawer only when nonempty, Import only when slots open. Continue to Filters requires filled slots and ready composition.
4. Retake: choose a target in Arrange, switch to Camera with Retaking photograph N, original stays accepted. Capture one candidate. To keep Camera tab consistently live, review candidate back in Arrange inside its template slot, with Use photograph / Try again / Keep original. Try again returns to live Camera with same target; accept replaces only target and remains Arrange; keep original cancels candidate and remains Arrange. This review placement supersedes the old frozen-candidate Camera state. Navigation or cancellation must not silently accept a candidate.
5. Replace: device picker opens from Arrange without switching tabs. Picker cancel changes nothing. Valid file appears as candidate within target slot; Use photograph / Choose another / Keep original. Commit only on acceptance. Dropping a file onto occupied slot follows same review; dropping onto empty slot fills it. Errors preserve original. Lock unrelated editing while replacement is pending.
6. Completed Camera visit: camera automatically starts live. Show All four, yours. or matching three-photo copy, completion count, enabled Mirror and Review photographs. No frozen last-image state, pause/resume controls, generic Take more or capture button. Retaking requires selection of a target in Arrange, preventing accidental overwrite of full template.
7. Returning from Template or Filters with complete set opens Arrange; incomplete set opens Camera for remaining photos. Preserve accepted photos, actual frame, native photo geometry, and extras in Kept photos. Handle camera interruptions without accepting corrupt shots.

## Implementation constraints
Same shared blank image treatment as Template; all existing three- and four-shot templates and landscape/portrait slots supported. Keep native geometry and real frame artwork. Control overlays and crop guides do not appear in captured/exported images. Mirroring applies consistently to view and captured photographs. Capture-control bar must not obscure faces/countdown; mobile placement needs verification. Change template enabled whenever no operation is active. No new zoom/crop-edit functionality implied by guide. Existing Filters and Download behavior preserved.
