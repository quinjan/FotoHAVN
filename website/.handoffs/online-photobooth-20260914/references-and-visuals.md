# Research and visual directions

Date: 2026-09-14. These are design concepts, not implemented screens. The user has not selected a direction.

## Existing site inspection

The rendered `http://localhost:3000/fotohvn#experience` was inspected in the Codex in-app browser. `current-experience.png` captures its typography, warm paper surface, centered manifesto, and two guest photographs. Source confirms those guest images are `sisters.webp` and `weekend.webp`. The design request applies to this experience section; the rest of the existing guest album is outside this change.

The existing hero-to-experience transition is already an important part of the page. Keep its integration constraints when building the chosen section. The actual route, source, and current design documents were checked; historical memory was used only to orient the work and choose the separate-planning workflow.

Runtime note: an attempted local `npm run dev -- --hostname localhost --port 3000` returned `spawn EPERM`. The in-app browser nevertheless displayed the current route and matching experience content, from which the screenshot was captured. This turn verifies the observed rendered design, not a successful server restart or a new build. Resolve the local startup permission issue when beginning implementation and verify the newly changed route afresh.

## Functional references

### Pixlery

[Online Photo Booth](https://pixlery.com/tools/online-photo-booth/) was read and its initial editor was inspected in a rendered browser. Its documentation describes template-driven photo counts, individual retakes, reordering, crop adjustment, and export. The rendered editor puts captured-photo selection and a composed preview alongside template controls. This supports making the frame/slot relationship explicit.

FOTOHVN should adopt the principle of selecting a specific slot for replacement and preserving the remaining photos. Its workspace should use fewer controls at once and the site's editorial visual language. We did not run camera capture, grant camera permission, upload files, or verify the reference's downloaded output. Screenshots: `reference-pixlery.png`, `reference-pixlery-editor.png`.

### Cutiora

[Photo Booth](https://cutiora.com/tools/photobooth) was read and its layout stage was inspected in the browser. It offers layout selection before timed capture and applies filters afterward without needing another session. The layout selection makes the required number of shots understandable before camera access.

FOTOHVN will retain the requested sequence and keep capture review inside its second stage. Cutiora documents a full-reset retake; FOTOHVN instead requires an individual-slot replacement with an explicit cancel path. We inspected the layout controls only, without starting the camera or uploading images. Screenshot: `reference-cutiora.png`.

### PhotoBooth-IO

[PhotoBooth-IO](https://photobooth-io.com/photobooth) was read as a supplementary reference. Its brief welcome sets expectations for a timed four-picture session, then a digital download. Its stated no-retake rule does not meet this request. Use a similarly clear explanation of the upcoming shot count, with FOTOHVN's required correction and arrangement controls.

## Browser platform constraints

[MDN getUserMedia](https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getUserMedia) documents permission requirements, secure contexts, and permission/device errors. Camera access should be requested only when a person starts the capture stage. Request video only, handle denial or an unavailable device, and keep a local device-image option available. A prompt may remain unanswered indefinitely, so the page needs a usable alternate path while waiting. Localhost can support development capture; public camera use needs HTTPS. The current `site.config.ts` declares an HTTP IP staging URL, so camera launch on that deployment cannot be treated as ready without checking and providing a secure public origin.

[MDN MediaStreamTrack.stop](https://developer.mozilla.org/en-US/docs/Web/API/MediaStreamTrack/stop) supports explicitly stopping video tracks when the camera is no longer needed. [MDN canvas.toBlob](https://developer.mozilla.org/en-US/docs/Web/API/HTMLCanvasElement/toBlob) documents turning the composed canvas into a downloadable image blob. These are implementation references, not proof that the future FOTOHVN capture or download has passed browser verification.

## Displayed visual mapping

The option numbers below are assigned from the order the generated-image results were actually displayed in this conversation. All three were generated independently and each was displayed once. All use the actual current-site screenshot as an attached visual reference. All exclude customers, faces, and people.

| Displayed option | Artifact | Direction | Main design idea |
| --- | --- | --- | --- |
| 1 | `visual-1.png` | Behind the Curtain | A tall luminous curtain study beside a private-room manifesto and online invitation. |
| 2 | `visual-2.png` | A Study in Light | A wide photographic sequence joins privacy, creative choice, and a physical keepsake before the online invitation. |
| 3 | `visual-3.png` | The Keepsake Table | An oversized framed print still life carries the editorial message into a digital-photo invitation. |

The images are mood/layout targets, not source UI or production assets. Generate clean, text-free image assets to fit the chosen responsive composition during implementation, and render actual interface text with the site's fonts. Verify exact `FOTOHVN` spelling in all UI and downloaded frame marks. Option 1's generated button contains a lettering artifact; use the correct copy below, not OCR from the mock. Generated image proportions and headline wrapping also need responsive design judgment rather than mechanical screenshot scaling.

## Shared copy and interaction direction

Primary invitation: `EXPERIENCE FOTOHVN ONLINE`.

The invitation should make the outcome clear: make and download a digital photo strip. A lyrical headline provides drama while the action label remains understandable. Candidate headlines are part of the displayed options; choose the final copy with the visual selection.

Additional proposed triggers: a quiet `ONLINE BOOTH` item in desktop/mobile navigation, and a contextual `MAKE A DIGITAL STRIP` text link in the printed-keepsake section. The new route should have a clear return to the editorial website. Keep booking actions visually coherent with the existing page.

Motion concept: a restrained section reveal, tactile selection feedback, a readable countdown, each accepted photo settling into its slot, smooth slot rearrangement, stable framed filter crossfades, and a short paper-reveal animation at the final step. Motion follows an action or a narrative transition; nothing loops only for decoration. Reduced motion removes movement and flash effects while keeping status feedback.

The online page should be an open warm-paper workspace using Cormorant Garamond and Manrope, with a quiet four-step indicator, generous live view, and a persistent composed-strip preview where it helps the current task. The original booth layouts/filters are still unavailable. Any starter collection is provisional original digital work, with no claim to match the physical booth's final presets.

## Current boundary and next handoff

Complete: source/context inspection, rendered experience inspection, web references, three independent visual directions, and a separate detailed interaction plan.

Pending: user visual choice, production asset creation, implementation, camera/download verification, plan conformance, responsive QA, lint/type/build checks, and any future deployment request. No production UI changes, launch claims, or test passes are part of this design exploration.

Read `brief.md` and `interaction-plan.md` together with the selected visual before implementation. A fresh implementer should apply the local Next.js version's documentation, preserve pre-existing changes, and follow independent design-conformance and responsive review gates.
