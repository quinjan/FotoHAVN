# Selected design and implementation authority

The user selected **Option 2** on 2026-09-14 through the visual-choice question. This is the second displayed generated image, copied to `visual-2.png`, the wide photographic sequence headed “A little privacy. A little possibility.” Implementation of the original request is now authorized. No further visual-choice approval is needed.

Read this together with `brief.md`, `interaction-plan.md`, and `references-and-visuals.md`. This file resolves planning proposals and supersedes earlier awaiting-selection statements. The selected image is the Experience layout target; its typography, palette, spacing, photographic character, and restrained rule treatment extend into the new online workflow.

## Experience implementation

Preserve the current global hero and `EditorialLift` wrapper/anchors. Replace the Experience interior with the selected wide headline, unequal-width photographic band, aligned supporting offers, and final online invitation. Use the existing global navigation; do not render another navigation bar inside the section. Respect intrinsic flow so the lift's measured section height includes its entire invitation.

Exact copy:

- Eyebrow: `THE FOTOHVN EXPERIENCE`
- Heading: `A little privacy.` / `A little possibility.` with italic emphasis on `possibility.`
- Offers: `Room to be yourself.` / `A private space, away from the crowd.`; `A look that feels like you.` / `Choose the frame and photographic finish.`; `Something real to keep.` / `A printed keepsake from a fleeting moment.`
- Invitation: `Your next photograph starts here.`
- Supporting copy: `Step into our online booth. Make a digital strip of your own.`
- Button: `EXPERIENCE FOTOHVN ONLINE`, linking to `/fotohvn/online`.

Three clean individual generated image assets will replace the mockup imagery: sunlit curtain folds and walnut; optical-glass light study on walnut; a printed strip with abstract material-study photographs on warm paper. No customers, faces, or people. Their source references are the chosen image and current site capture. Asset preparation must provide all widths required by EventPhoto or use an intentional equivalent static delivery method. Root owns asset production and processing.

## Experience refinement, 2026-09-14

The user later refined the three story beats without changing the selected Option 2 layout. This newer direction supersedes the two relevant image descriptions and the third offer above:

- `Room to be yourself.` now uses a generated candid of two women from the user-supplied reference, seated in the 3D-model-accurate FOTOHVN orientation. The camera-facing close crop puts the fixed cream pleated backdrop directly behind them; the opposite camera/screen wall and entry are outside the frame.
- `A look that feels like you.` now uses the user-approved generated composition of four physical FOTOHAVN strips in hand. One fictional Filipino guest appears consistently across all panels; the supplied real strips inform only the full-bleed templates, wordmark placement, rotated orientation, backdrop, and photographic filters.
- The final offer is `A memory made to last.` / `A FOTOHVN moment, printed to last.` It uses the approved café reunion: three fictional Filipino friends laughing over old FOTOHAVN strips, with their expressions and hands dominant and the prints kept secondary.

Generated sources are `asset-candid.png`, its later reference-led replacement `asset-candid-v2.png`, the superseded `asset-look-choice.png`, the approved `asset-look-prints.png`, and the approved `asset-keepsake-reunion.png`; active responsive production families are `candid-v2*.webp`, `look-prints*.webp`, and `keepsake-reunion*.webp`. Earlier candidates remain recoverable but are no longer used.

On narrow screens, retain all three story beats with readable images and copy in a vertically reflowed editorial sequence. Avoid cropping the headline or hiding intrinsic overflow. Aim for desktop fidelity to the selected composition and intentional mobile adaptation. The local design system's focus rings, typography, and motion accessibility apply.

## Online workflow decisions

Accept the detailed interaction plan's four layouts and exact geometry: Long Strip (4), Story Strip (3), Pair (2), Contact Sheet (4). Accept Ivory, Archive, Walnut, and Gallery frame designs with actual exported type/rules. Accept Original, Warm Paper, Silver, and Soft Fade as original online starter photographic looks; implement and tune their shared deterministic color transform. Do not describe these as the physical booth's final presets.

Use the four stages `Layout`, `Photographs`, `Look`, `Download` on a single client-owned session under `/fotohvn/online`. Page header: FOTOHVN left, `THE ONLINE BOOTH` as a quiet label, a clear return to the website. An open paper workspace, never nested card dashboards. Layout stage pairs a dominant composed-frame preview with concise layout and frame selection. Capture uses a generous 4:3 viewfinder and numbered photo positions in the real selected frame. Look uses a dominant full framed preview and four named photographic options. Download reveals the actual completed PNG, with Download PNG, Edit, and Make Another.

Preserve per-slot retake/cancel, single-slot acceptance, drag/tap/keyboard rearrangement, original pixels under look changes, all photos on layout changes with a retained tray, accurate framed previews, and a download matching the final displayed blob. A three-second countdown begins only on explicit capture, fills empty slots, and exposes Stop. Mirror control affects capture and stored results consistently. Camera access is requested explicitly, video only; camera tracks and late operations are cleaned up. Device-photo import is a complete local fallback.

The site's existing fonts, colors, radii, and Phosphor icons carry through. State transitions, photo insertion/reorder, look changes, and a short paper reveal supply purposeful motion; reduced motion removes travel and flashes. Do not add sign-in, storage, sharing services, novelty effects, editing tools beyond the requested scope, or artificial processing delays.

## Extra entry points

Add `ONLINE BOOTH` to the shared desktop/mobile navigation if it fits at the existing breakpoint, adapting spacing as needed. Add a quiet `MAKE A DIGITAL STRIP` contextual link after the physical-print copy without removing the guest-album link. Online page return links use the homepage plus correct anchors, not hashes on the online route.

## Work ownership and verification

- Fresh online implementation agent: only new `src/app/online/` and `src/components/onlinePhotobooth/` files plus new focused test script(s) in `scripts/`. Do not modify global navigation, Experience components, package manifests, or existing motion.
- Fresh Experience implementation agent: `UpperExperience.tsx`, `UpperExperience.module.css`, a scoped new Experience motion module if necessary, `SiteChrome.tsx`/CSS, and `MiddleExperience.tsx`/CSS only. Use asset paths agreed with root. Do not edit online route files.
- Root: generated assets, asset preparation, integration checks, startup, and orchestration. Shared build commands run under root after agents finish.

Read the local Next.js docs before code changes as required by `website/AGENTS.md`. Preserve the extensive unrelated existing worktree changes. Add meaningful logic tests for destructive-state risks and compositing geometry; do not add tests that only mirror visual copy. Check all four stage paths in a real browser, including downloaded output, camera recovery, individual replacement, and keyboard/touch rearrangement. Fresh independent conformance compares this plan and `visual-2.png` before fresh responsive reviews, including 320px. Record real-camera limitations honestly if camera hardware cannot be exercised.

Use the local app preview, not a deployment. Public camera support requires HTTPS; the current HTTP staging URL is not a verified production-camera origin. No deployment is requested.

Historical runtime observation at implementation start: a host-level read confirmed port 3000 was owned by this workspace's Next.js process (PID 28780 at that inspection). The sandbox-level port query could not see the listener, and the attempted duplicate startup failed with EPERM. That process later stopped; the replacement server and final HTTP 200 checks are recorded in `browser-verification.md`. Use that newer runtime record, verify current process identity before managing it, and do not stop unrelated processes or infer app availability from a sandbox-only port query.
