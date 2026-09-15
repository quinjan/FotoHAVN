# FOTOHVN — real-booth redesign handoff

Completed locally on 2026-09-09, on `codex/website-real-booth-redesign`.

Baseline: `107e895a488747c56031136d77b9af31d48930dd`. Changes are uncommitted. No push, deployment, issue closure, or email was performed. The user's existing root `CONTEXT.md`, `marketing/`, and `research/` changes were preserved.

Local preview: http://localhost:3000/fotohvn

## What changed

The old page composition has been replaced by a photography-led editorial experience: a real-booth hero, an intimate statement surrounded by guest prints, a WebGL booth studio, actual photographed Photo Strips, a draggable guest album, clear visit/rental paths, and the existing inquiry contract in a new layout.

The canonical palette in `tokens.json`, `variables.css`, and `theme.css` is unchanged. Cormorant Garamond and Manrope are self-hosted through Next's font pipeline. This is a light-led brand under either system color preference.

Motion includes staged hero entry, scroll-linked photo positioning and rotation, editorial reveals, model camera transitions, and a folding curtain. It does not intercept page-wheel scrolling, force an intro, or run an endless decorative loop. Motion and WebGL load progressively. The design-taste skill was applied with the brief's real-photograph and preserved-brand requirements taking precedence over its generic image-generation and palette defaults.

The design is an award-oriented craft direction, not a claim of an award or guaranteed judging outcome. See `plan.md` for the implementation brief and deliberate design-system interpretations.

## Run and verify

From `C:\Quinjan\Repos\FotoHAVN\website`:

```powershell
npm ci
npm run dev -- --hostname localhost --port 3000
```

Open `/fotohvn`, not `/`. The existing base path and standalone container output remain in place.

Verification commands:

```powershell
npm run lint
npx tsc --noEmit
npm run test:booth
npm run build
git diff --check -- .
```

All passed on the final implementation. The four booth tests exercise real Three.js geometry/material construction with mocked DOM/GPU boundaries: normal exactly-once disposal, partial-construction cleanup, texture-failure fallback/late callback handling, and keyboard/inside-camera/reduced-motion behavior. They are not GPU screenshot tests.

## Implementation map

- `UpperExperience`: real booth hero and editorial experience; `MiddleExperience`: actual prints and the guest album; `ClosingExperience`: visit/rental paths, inquiry, footer.
- `ExperienceMotion`: lazy GSAP/ScrollTrigger enhancement with media-query and lifecycle cleanup. Already-visible text is not hidden when the motion chunk arrives.
- `BoothExplorer`: accessible controls, scene lifecycle, lazy loading, real-photo comparison, retry, status and approximation note.
- `booth/createBoothModel`: geometric cabinet, wood/photo textures, pleated curtain, illuminated sign, hatch and interpretive interior.
- `booth/createBoothScene`: render-on-demand camera/curtain transitions, pointer/keyboard input, viewport fitting, visibility handling and disposal.
- `GuestAlbum`: native horizontal scrolling with pointer drag, keyboard arrows, Previous/Next, and reduced-motion-aware navigation.
- `InquiryForm`: required Intent/Name/Email validation and encoded mailto handoff. No backend or false submission-success state was introduced.
- `EventPhoto` plus `event-photo-sizes.json`: static responsive WebP delivery through Next Image. The custom loader is deliberately limited to the prepared Evia `.webp` sources used by this page.

## Photography

Originals: `marketing/reels/fotohvn-evia-event/public/assets/Pictures/` at the repository root. The user's `assests/pictures` spelling referred to this existing folder. No original was changed.

`scripts/prepare-event-photos.mjs` produces the contact sheet and derivatives in `public/images/evia/`. Regeneration requires the local original-photo directory; it is not a build/deployment step. Shipping uses the prepared derivatives, so the original marketing workspace need not be present on the server.

| Web asset                      | Original                                               |
| ------------------------------ | ------------------------------------------------------ |
| booth-close                    | 3f970799-a9ca-4a6e-966a-3a804a2ebc8a.jpg               |
| booth-at-evia                  | 1000018870.jpg                                         |
| couple                         | 1000018878.jpg                                         |
| friends                        | 1000018879.jpg                                         |
| sisters                        | 1000018877.jpg                                         |
| weekend                        | ff29714a-97be-4401-aa2c-72c51677425c.jpg               |
| family                         | 1000018876.jpg                                         |
| keepsakes                      | d0602d5c-cf98-47a5-8c1c-08e852fb131e.jpg               |
| smiles                         | 5ea64c75-d9ff-4aab-ac2e-99ee31f944bc.jpg               |
| evia-event                     | 1000018867.jpg                                         |
| prints-detail                  | 1000018866.jpg, cropped to the actual prints and linen |
| booth-photo-board / booth-wood | Crops from booth-close's original                      |

There are 77 responsive variants for the 11 photographs, across the seven configured widths. Originals smaller than a requested width are not enlarged. Texture images retain their separate reference crops. No guest testimonial, event price, permanent Evia availability, or fabricated sample Photo Strip was added.

## 3D truth and interaction boundary

This is a genuine Three.js/WebGL model, not an image rotation or CSS perspective trick. Exterior features follow the supplied photographs: dark timber enclosure, pale curtain, PHOTOBOOTH lightbox, photo display, print hatch, glazed pane, and plinth. The window reflection in the reference was not turned into physical mullions.

There is no measured scan, CAD file, or confirmed dimension set. The unseen interior, rear and proportions are interpretive, explicitly disclosed beside the viewer. The Inside preset shows a plausible left-side camera/touchscreen console and warm enclosure; it should not be used for installation planning or an exact interior specification.

Drag to rotate; focus the model and use arrow keys to rotate/tilt and +/− to zoom. Overview/Front/Side/Inside presets, curtain, reset, and real-photo comparison also work through native buttons. Mobile reserves model space above its toolbar. Reduced motion makes scene transitions immediate. The renderer stops when settled or offscreen, caps pixel ratio, and handles failure with a real photograph and retry.

## Compatibility and release boundary

Preserved: `/fotohvn`; existing main/section anchors and navigation; Instagram/Facebook/email destinations; Intent, Name, Email, Event date, City or venue, Notes in the same inquiry order; metadata title; intentional staging `noindex`; standalone output. No shared VPS or PhotoBIZ service was touched.

Final production-build QA and fresh independent source/evidence reviews passed; see `qa.md` and `review.md`. This is local implementation approval evidence, not a deployment or a real-device certification.

Before public deployment, address the pre-existing dependency advisories: `npm audit` reports Next 16.3.2 as critical and transitive js-yaml 4.3.1 / Sharp 0.35.3 as high. All three versions were confirmed in the baseline lockfile. They were not upgraded as part of this design change. Audit reports Next 16.3.4 as a patch candidate; revalidate the complete dependency tree and deployment after a security patch pass.

The existing email-app dependency also remains: the inquiry prepares an email; it does not deliver one directly from the website.
