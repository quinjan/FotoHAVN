# Camera capture research and visual directions

Date: 2026-09-17. Status: research and concepts only; no production implementation selected.

Update 2026-09-18: the user selected option 1 for refinement and explicitly requested a temporary countdown overlay inside the viewfinder. See [option-1-refinement.md](option-1-refinement.md) and concept-1-refined-countdown.png. This supersedes the outside-photo countdown recommendation below; other findings remain research context.

## Recommendation

Use a dedicated full-viewport camera workspace on phones, with a compact header, the largest truthful crop preview, and controls in a separate solid dock. Carry the same separation to iPad and desktop. Prefer automatic capture with individual retakes afterward. Camera switching and mirror behavior are separate controls. During countdown remove secondary controls, keep Stop available, and place the countdown outside the photograph.

The first displayed concept is the recommended starting structure. The second provides a useful progress rail for larger screens. The third explores a taller contextual preview and deliberate one-at-a-time capture, at the cost of more taps and crop explanation.

## Existing implementation evidence

Inspected current source and the running local /online page in the in-app browser. Current site.config.ts has an empty base path. Reference screenshots are current-mobile.png and current-desktop.png in this directory. The stream displayed a black unavailable-camera symbol; actual face visibility and physical front/rear switching were not hardware-tested. The overlap conclusion is based on visible control placement plus source, consistent with the user's report.

- useCamera.ts requests facingMode: user; no camera-selection state or rear-camera action is exposed.
- OnlinePhotobooth.tsx mounts the camera below the stage progress, editorial heading, and Camera/Arrange tabs.
- OnlinePhotobooth.module.css positions the shutter inside the video at bottom center and Mirror inside the top-right corner. On small screens, the preview takes the selected template slot ratio, losing its earlier minimum height. The countdown is centered over the photograph with a full-image tint.
- The existing sequence counts down three seconds for each remaining slot, then gives capture feedback for 800ms. It already supports stopping and individual replacement; preserve that work.
- media.ts first normalizes all sources to 4:3; template composition may crop again. CameraFrameGuide.tsx accounts for the normalization on desktop, but the guide is hidden on mobile. A future redesign must unify preview/capture/output crop calculations, especially for portrait camera streams and Panorama Four. A direct object-fit cover preview can otherwise disagree with the saved double crop. This is source-derived risk, not a reproduced hardware result.

## Online references

These are first-party product descriptions or documentation accessed on 2026-09-17, not comparative usability-test results. Cutiora's template and capture shell were also opened in the browser; a completed capture sequence was not exercised. Other interaction findings below come from the linked first-party pages.

| Reference | Documented pattern | Suggested use in FotoHAVN |
|---|---|---|
| [Cutiora](https://cutiora.com/tools/photobooth) | Template, capture, then editing; 3/5/10-second timers; automatic multi-shot countdown; upload alternative | Make preparation distinct from posing; one Start action for the sequence; leave filters until review |
| [Pixlery](https://pixlery.com/tools/online-photo-booth/) | Countdown sequences, per-photo retake, crop refinement, optional guides, matching mirrored front-camera preview and saved photos | Preserve successful photographs; make crop and mirror outcomes predictable |
| [Pixect](https://www.pixect.com/help/how-to-record-video-with-webcam.php) | Camera/microphone device selection and aspect/resolution controls before recording; preview/save/cancel afterward | Explicit source selection before capture, then quieter capture controls; do not import its unrelated video/audio features |
| [Webcam Toy](https://webcamtoy.com/) | Camera entry, single/four-photo capture, Back and Save actions | A focused camera workspace and clear capture/review separation; its effects-heavy styling is not a brand reference |
| [Snapbar iPad case study](https://snapbar.com/blog/case-study-using-virtual-photo-booth-for-in-person-events) | Front/rear switch supports selfies or friend-taken photographs on iPad | Treat rear camera as a primary capture use case, with a reachable labeled Flip camera action |

## Fullscreen and camera contract

1. Enter a full-viewport workspace when the user enters camera mode. Use the available dynamic viewport and safe-area insets; keep exit and Stop reachable. Page scroll and marketing navigation should not compete with capture.
2. Request actual browser fullscreen only from an explicit user gesture when supported. Request it on the complete workspace container, so custom controls remain present. Handle refusal or exit by continuing in the viewport layout. Do not require installation, orientation locking, or fullscreen acceptance. [MDN requestFullscreen](https://developer.mozilla.org/en-US/docs/Web/API/Element/requestFullscreen) documents limited availability and transient activation.
3. Support user/environment facing modes, stopping the existing tracks before reacquiring when needed. After permission, offer device selection for desktops or ambiguous multiple-camera devices. Verify the resulting track rather than silently claiming a switch succeeded. Preserve photos if switching fails and provide return/retry. [MDN getUserMedia](https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getUserMedia) and [facingMode](https://developer.mozilla.org/en-US/docs/Web/API/MediaTrackSettings/facingMode).
4. Disable camera switching during a countdown. Rear camera should start unmirrored; front preview and saved-photo mirror choices should be clear and consistent. Show the active source; do not confuse Flip camera with Mirror.
5. Camera permission failure needs a clear Retry and import alternative before capture. Backgrounding or losing a stream stops the sequence without discarding accepted photographs; never resume taking photos unexpectedly.

## Geometry matters

A 4:3 landscape image at 390px wide is 292.5px tall. Fullscreen cannot make that image occupy a tall phone without cropping, distortion, or extra preview outside the saved area. Remove side padding and competing content, but retain honest geometry.

- Exact-crop view: show the selected template slot with the same crop as output. Panorama Four has portrait 3:4 slots; Signature Strip has landscape 4:3 slots.
- Contextual view: a taller sensor preview can be shown with an unmistakable output crop boundary and lightly shaded excluded regions. This is more immersive but requires users to understand the boundary.
- Short landscape screens: move the dock to a side rail. iPad portrait can use bottom controls; iPad landscape and desktop can use a side rail without shrinking the photograph into a small page card.

## Displayed concepts

Display order is authoritative:

1. Quiet Camera — concept-1-quiet-camera.png. Bottom dock, automatic sequence, minimal progress. Uses the existing Panorama Four portrait slots to show a truthful tall-photo direction.
2. The Living Strip — concept-2-living-strip.png. Sequence progress appears as thumbnails outside the image; right rail on landscape iPad and desktop. Uses Signature Strip landscape photographs.
3. Frame by Frame — concept-3-frame-by-frame.png. Taller contextual live view with marked print area; one exposure followed by Keep/Retake before the next.

Every board shows ready and capturing states on mobile, iPad, and desktop. The first/third show iPad portrait; the second shows iPad landscape. Images were produced independently with the built-in Image Gen tool, grounded in both current product screenshots and the verified DESIGN.md palette/type system.

### Concept illustration limits

Generated boards are directional, not pixel specifications. Some mock frame proportions drift from their labels (especially the second board's large camera panels); the exact ratios above govern a future build. Countdown headers must reserve height so switching states does not move or resize the preview. Generated small text, icon shapes, and extra slogans are not approved production copy. Secondary controls need actual 44–48px hit areas even when their icons are small. All options require real device checks before claiming front/rear or fullscreen support.

## Capture state recommendation

Ready (source, mirror, timer, Start) → countdown (photo index, count outside image, Stop) → captured feedback (brief subtle exposure cue, progress advances) → next countdown → review (individual Retake, Arrange, Continue).

Stop retains captured photographs and offers Resume remaining. Exit stops the stream and returns to the preceding workspace without clearing the session. Replacement retains the original until the user accepts the new shot. Reduced motion removes flash animation; optional audio must not be required to understand timing. Keyboard focus and announcements must work without announcing redundant status messages each tick.

## Next decision

Choose a visual direction or request a combined revision. Production files were not edited for this research. Existing unrelated working-tree edits were preserved.
