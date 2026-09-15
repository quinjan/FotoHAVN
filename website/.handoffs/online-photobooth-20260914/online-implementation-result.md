# Online photobooth implementation result

Implemented the user-selected Option 2 design language in the new online route. This is a working local photo session, with the requested four stages, rather than a visual scaffold. User selection in `selected-design.md` was the implementation authority. Parent owns browser observation, integration checks, independent conformance, and responsive review; none of those are claimed here.

## Files and boundaries

New route: `src/app/online/page.tsx`, with server-owned route metadata and a client entry. The configured base path produces `/fotohvn/online`.

New scoped implementation under `src/components/onlinePhotobooth/`:

- `presets.ts`: four exact export geometries, four authored frame recipes, four versioned photographic looks, footer bounds, and center-cover crop.
- `session.ts`: immutable assignments and retained-photo tray; transactional replacement; operation-ID guards; layout, look, frame, stage, and move actions.
- `media.ts`: oriented local-image decoding, bounded 4:3 normalization, mirrored ready-video capture, PNG encoding, cancellation, and source cleanup.
- `useCamera.ts`: explicit video-only permission; track readiness; denial/unavailable handling; request invalidation; late-grant, disconnect, and unmount cleanup.
- `compositor.ts`: a single full-resolution opaque PNG renderer for previews and download, using local brand fonts. Looks affect only photo windows. Footer marks and geometric frame art are actually exported.
- `useComposition.ts`: aborts obsolete revisions, keeps the prior valid preview while preparing its successor, decodes the successor before display, and releases obsolete blob URLs.
- `PrintPreview.tsx`: exact normalized interactive photo rectangles, actual local PNG preview, functional frame thumbnails, selection and pointer-drag targets.
- `OnlinePhotobooth.tsx` and `.module.css`: four complete stages, native radio groups, countdown/stop, local-file alternative, retake candidate review, drag/tap/keyboard arrangement, retained tray, download/open-to-save/edit/reset, editorial typography, and reduced-motion-aware feedback.

New focused test file: `scripts/online-photobooth.test.mjs`.

No package manifests, global styles, existing components, Experience source, shared navigation, or runtime processes were changed by this agent. No shared browser was operated.

## Functional decisions

- Initial route does not request the camera. Camera permission follows the explicit camera button and always requests `audio: false`.
- A normal shutter action counts down three seconds per empty position. Stop keeps already committed photographs and invalidates further work.
- Selecting an occupied position then Retake preserves its original. A camera or file replacement remains a candidate until Use This Photograph; Try Again and Keep Original release the candidate and preserve all other source identities.
- Pointer drag, selecting a source then tapping a destination, and the native destination select/Apply control share the same move reducer. Occupied destinations swap; retained tray placement returns displaced photographs to the tray.
- Changing layout consumes the existing assignment order followed by the retained tray. Decreasing photo count never deletes the extras.
- Every source is normalized locally to an opaque 4:3 PNG of at most 1640 × 1230. Input size is limited to 25 MB and decoded images above 64 megapixels are rejected. The browser handles EXIF orientation before normalizing. No free-form crop tool was added.
- Original preserves source pixels after the common crop/orientation step. Other looks use one documented encoded-sRGB kernel shared with final rendering; no approximate CSS filter is used.
- The final image and Download PNG use the same completed blob URL. Open Image to Save is available as a browser-dependent alternate save path. Make Another requires an inline choice before clearing this in-memory session.
- Source/candidate/render URLs are released when obsolete or on unmount. No images are uploaded, persisted, logged, or placed in URLs by application code. Parent still needs network/storage browser verification before treating those runtime behaviors as observed.
- Background camera activity is paused, with pending capture cancelled and existing photographs retained. A quiet upload-only Layout/Download session does not receive a false camera-pause notice.
- Focus after accepted replacement and rearrangement waits for the current preview revision, then goes to its enabled photo-position control; the compact position rail is the visible mobile fallback.

## Focused verification

Command run from `website/`:

```text
node --test --test-isolation=none scripts/online-photobooth.test.mjs
```

Result: **14 passed, 0 failed**. The test runner uses the repository's installed TypeScript transpiler and Node test framework without adding a dependency.

Meaningful coverage: retake cancel/retry/accept; single-slot preservation; late/stale photo rejection; operation locking; layout/tray conservation; incomplete-stage guards; swap semantics; original-source identity under looks; exact geometry and crop; deterministic look behavior; all 16 layout/frame renderer contracts including the shallow Contact Sheet footer; look confinement; abortable timers; explicit video-only permission and late/unmounted grant cleanup; consistent mirrored capture.

The renderer and camera adapter tests use deterministic canvas/media doubles. They establish control-flow and drawing contracts, not real-device or browser pixel-output verification. Parent already reported an integrated TypeScript pass and was separately checking lint; later refinements require the parent's final checks against final source.

## Remaining parent-owned evidence

The rendered four-stage file-input path, actual camera hardware, pointer/touch/keyboard behavior, downloaded PNG inspection, no-upload/no-storage observation, all viewport checks including 320px, fresh plan conformance, final lint/type/build/hero checks, and any repair cycle remain the parent's work. Public camera support needs a secure origin; no deployment was requested or performed.

Design authority: selected Option 2 image and handoff, current local `DESIGN.md`/tokens, existing Cormorant and Manrope font setup. Installed Next 16.3.2 local page, layout, client-component, and metadata docs were read before source changes. `design-taste-frontend` was used for scoped composition and accessibility discipline, with the selected FOTOHVN brief taking precedence over generic style defaults.
