# FOTOHVN experience and online photobooth brief

Date: 2026-09-14. Status: design exploration; no visual direction selected yet.

## User request and authority

Redesign the editorial site's `#experience` section. Replace its existing customer photographs with generated imagery purpose-made for the message. Keep the experience dramatic and extend its explanation of what FOTOHVN offers. Add a compelling invitation such as “Experience FOTOHVN Online” that opens a dedicated working online photobooth page. Additional entry points may be placed where they suit the editorial site.

Required flow: choose layout including designed frame → take pictures → choose filter with the selected frame in the preview → reveal the final photostrip and download it. Capture review belongs within the capture stage: select an individual occupied slot for a retake, preserve other shots, and reorder captured images into the chosen layout. Make interaction and animation expressive while retaining the site's editorial typography and design language.

The actual booth's layout and filter assets have not been supplied. The user authorizes original temporary layouts, frame designs, and filters. Do not claim the digital starter looks reproduce the physical booth's production presets. Plan a replacement seam for the real assets later.

Local visual authority: `website/DESIGN.md`, `website/tokens.json`, `website/variables.css`, `website/theme.css`, and current rendered editorial site. Current user request wins when it extends that language (including interactive animations and digital filters). Use off-white/ivory, ebony/walnut, quiet brass; Cormorant Garamond and Manrope. Dramatic means light, photographic composition, large type, and purposeful transitions within that palette.

## Current verified source context

- Next.js application in `website/`, with base path `/fotohvn`; local preview `http://localhost:3000/fotohvn`.
- Actual `#experience` content is in `src/components/UpperExperience.tsx` and `.module.css`, wrapped in `EditorialLift` with the existing hero. Its two images are `sisters.webp` and `weekend.webp` from the Evia customer photographs.
- Preserve existing hero/lift integration and anchor behavior while redesigning this section. Investigate source selectors and sizing before future implementation.
- Proposed dedicated route: `/fotohvn/online`, implemented as an app route under the configured base path.
- `SiteChrome.tsx` owns primary navigation; `MiddleExperience.tsx` owns the printed keepsake narrative. These are likely additional entry seams.
- The worktree has extensive pre-existing modified and untracked work. Do not revert, overwrite, or treat unrelated work as part of this change.

## Working defaults

- English copy and displayed brand spelling FOTOHVN, matching the existing website.
- Camera consent on the capture step, no sign-in, local image processing in the browser, a camera-denied path, and a device-image fallback.
- Four simple layout choices, four frame treatments, and a small credible photographic filter set. Avoid novelty effects, stickers, a giant preset catalogue, or generic dashboard UI.
- Original shots remain available when filters or frame settings change; download should match the framed preview.
- Reordering supports touch and keyboard as well as pointer drag. Retakes are transactional: keep the old image until the replacement is accepted, with a cancel path.
- Motion includes intentional state changes, capture countdown/shutter, photo placement, filter crossfade, and a final print reveal; reduced motion removes travel and flashes.
- Keep physical-print messaging distinct from the digital PNG download.

## Exploration and selection

The Product Design ideation skill requires three independent generated visual directions, grounded in screenshots of the current website, followed by user selection before implementation. Prepare a concrete flow/design handoff and references now. Do not edit production UI or build a new route before the visual choice.

Root agent owns rendered inspection, web references, image generation, and synthesis. The independent planning agent owns `interaction-plan.md` only and should not modify production code or use the shared browser.

## References to inspect

- https://pixlery.com/tools/online-photo-booth/ — individual retakes, reorder, crop, template-driven shot counts.
- https://cutiora.com/tools/photobooth — layout-first sequence and non-destructive post-capture filters.
- https://photobooth-io.com/photobooth — simple countdown/strip flow; its no-retake rule is deliberately superseded by the user's per-slot-retake requirement.

These are functional references, not visual authority or permission to copy another product's assets or branding.
