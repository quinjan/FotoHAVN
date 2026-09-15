# Approved curtain-led hero implementation

Date: 2026-09-10. Branch: `codex/website-real-booth-redesign`.

## Authority and scope

The user approved the revised third hero proposal and its curtain-reveal animation. Exact visual truth: `approved-hero.png`, copied from generated result `exec-fc4495b9-3a2d-432d-801d-22fa2f2521b7.png`. The raised PHOTOBOOTH lightbox has open background on both sides; retain the horizontal cabinet rail below it. Do not return to the earlier wood-wing version.

Implement in the existing Next.js website, not a new prototype scaffold. Scope is the hero and its generated assets, supporting interaction, tests, and verification. Keep the current branch and previous uncommitted redesign; preserve user changes outside this seam. Leave the experience, 3D explorer, prints, album, rental/inquiry, base path and metadata unchanged.

## Measured composition and assets

The selected full mock is 1585 × 992. Its top navigation occupies about 77px. Hero artwork below it has aspect approximately 1585 / 915. The final text-free exterior is 1652 × 952. Its registered cloth aperture is x23.5%–78.5%, y17%–100%, with a slightly tapered top matching the opening.

- Closed exterior: one purpose-generated hero background with the exact cabinet, sign, cream cloth, window, print display/hatch and lighting, but no web UI baked in. Keep physical sign/plaque lettering in the image.
- Interior: one generated walnut/cream interior impression for the central aperture only. No people, extra props, facade, web UI or curtain. Exact unseen interior measurements are not available; this is illustrative, consistent with the site's existing model limitation.
- Cloth foreground: use the same exterior raster through a registered crop, rather than generating a mismatching second fabric texture or drawing cloth in CSS. The fabric image compresses and gathers to the right over the revealed interior.

Asset production is delegated in parallel through the Product Design image-to-code skill. Asset agents do not edit application code or operate the browser.

## Layout and motion

Desktop matches the selected close frontal facade, centered Cormorant headline over the cream curtain, restrained Manrope copy and the two native actions. Keep the physical artwork at its natural aspect ratio so the sign is never stretched. The existing quiet header remains live HTML.

On narrow phones, keep the complete sign and facade in a full-width image stage, then reflow the text/actions onto the existing warm paper surface. Do not crop the sign into illegibility or squeeze readable text into an impossibly narrow curtain opening. The same curtain interaction operates in the stage.

The reveal is opt-in. DRAW THE CURTAIN opens it from left to right and changes to CLOSE THE CURTAIN; the native button remains reachable and reversible. Desktop headline/copy gently fades away while the material reveals; booking stays available. Closing restores the headline. Phones retain the readable text below the image. No forced intro, scrolling lock, automatic navigation, continuous loop or change to the separate 3D booth.

Use composited raster-image layers and transform/opacity transitions; no artificial booth or material stand-ins. Reduced motion makes changes immediate. Loading/error states must preserve usable copy, rental navigation and the closed illustration. Enter/Space, Escape, repeated toggles and failure paths must work. Keep the same focused toggle element through state changes.

## Implementation and verification

- Isolate the new client interaction in `HeroCurtain`, leaving the remaining upper experience server-rendered.
- Prepare responsive static WebPs so the image pipeline does not introduce cold optimizer requests.
- Use small [Phosphor React](https://github.com/phosphor-icons/react) directional icons, selected for the mock's thin editorial arrows, with direct imports. Do not replace image artwork with handcrafted SVG.
- Read installed Next.js server/client, Image and CSS documentation before implementation.
- Run lint, typecheck, booth regressions, focused hero checks and production build.
- Compare the rendered closed desktop hero and approved image together at matched viewport/state and normalized pixel density. Include focused typography/sign/curtain-edge comparisons.
- Check actual pointer and keyboard reveal/reversal, image loading/failure, reduced-motion handling, menu/rental navigation and no overflow at 1440, 820, 390 and 320 CSS px.
- Complete the Product Design blocking QA report and preserve historical QA separately. Fresh independent review follows implementation and any substantive repairs.

No push or deployment is authorized. Existing baseline dependency advisories remain separate from this scoped hero change.

## Verification-driven refinements

The over-image secondary text uses a blend of existing Soft Brown (60%) and Ebony (40%) to clear contrast on the darker cloth folds; mobile paper copy keeps Soft Brown. Transient loading/error status uses canonical Ebony to remain legible across the deeper lower folds. The same native toggle survives exterior failure as RETRY ILLUSTRATION, remounting only the raster images. These are bounded accessibility repairs, not changes to the selected direction. See `README.md`, `review.md` and `../../design-qa.md` for delivery evidence and explicit runtime-test limitations.
