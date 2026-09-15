# Hero-only visual proposals

Current brief: 2026-09-10. Proposal set initiated across the September 9–10 local-date boundary. Branch: `codex/website-real-booth-redesign`.

## User request

Replace the hero's real event photograph and remove its small guest-picture card. Use a generated asset grounded in the actual booth, fitting the hero intentionally. Make the hero more dramatic, with potential animation. Generate three independent proposals through Product Design ideate before any implementation.

## Boundaries

- Proposal-only work. The live hero and application source remain unchanged.
- Preserve the warm ivory, paper, ebony and walnut palette and the editorial/intimate character.
- Preserve the physical booth's recognizable silhouette, PHOTOBOOTH lightbox, curtain, front display/hatch and right glazed pane.
- No mall setting, pets, people or floating guest-picture cards in the hero asset. The small physical photo display on the booth may remain.
- Retain FOTOHVN branding, Cormorant Garamond/Manrope typography, concise copy and the rental action. Do not reintroduce obsolete pricing or unconfirmed claims from older design documents.
- The rendered proposals are still images. Motion descriptions are implementation intent, not a claim that an animation has been built.

## Grounding

The current hero was inspected in the in-app browser at a verified 1440 × 900 CSS viewport. Its section measured 1425 × 790, below the 73px header. `current-hero.png` captures the existing composition. Both original booth references were opened visually and attached, together with the current screenshot, to each independent generation:

- `marketing/reels/fotohvn-evia-event/public/assets/Pictures/3f970799-a9ca-4a6e-966a-3a804a2ebc8a.jpg`
- `marketing/reels/fotohvn-evia-event/public/assets/Pictures/1000018870.jpg`

Brand authority: `website/DESIGN.md`, `website/tokens.json`, and current hero source/copy. Product Design's saved-context preflight found no separate saved context. The new user brief supersedes the older real-photo hero requirement and quiet-motion ceiling for this scoped exploration.

## Independent directions

The following names identify generation prompts only. They are NOT option numbers; selection must follow the order generated images actually appear in the conversation.

- **The Light Room:** a full booth in an integrated luminous studio on the right, with directional daylight and an editorial left text column. Intended motion: a gentle lighting/typography entrance and minimal depth movement.
- **Center Stage:** a central booth with large headline fragments on either side and deliberately offset copy/actions. Intended motion: a slight turn into the frontal pose while the type enters from opposing sides.
- **The Threshold:** a close, immersive facade with typography over the pale central curtain, an invitation of warm light, and a curtain-opening secondary interaction. Intended motion: a user-initiated fabric reveal, with a static reduced-motion alternative.

Each proposal targets a single 1440 × 900 desktop hero. Generated assets and UI mockups use the built-in image generator, not hand-drawn stand-ins. Preview-only images may remain in the generator's default output directory until a direction is selected.

## Selection gate

Wait for all three independently generated images to be visible exactly once. Number them by displayed order only, then ask the user to choose or refine. Do not build from prompt submission order. If the user combines directions or requests changes to a selected direction, show a revised visual before implementation.

## Completed display-order mapping

All three independent results were displayed once in the main conversation, in the order below. No application code was changed in this proposal turn. These are complete hero UI mockups, not final text-free production background assets.

| Displayed option | Prompt direction | Generated file |
| --- | --- | --- |
| 1 | The Light Room | `C:\Users\QUINJ3875\.codex\generated_images\01a0864b-7ffa-7920-a2c4-6bb8edae4292\exec-3a9a31cf-6df9-4859-926b-ef5b3acdff32.png` |
| 2 | Center Stage | `C:\Users\QUINJ3875\.codex\generated_images\01a0864b-7ffa-7920-a2c4-6bb8edae4292\exec-8b927270-dfab-4909-b7c8-76004fb34f59.png` |
| 3 | The Threshold | `C:\Users\QUINJ3875\.codex\generated_images\01a0864b-7ffa-7920-a2c4-6bb8edae4292\exec-0512440e-4754-4cd3-9340-f28ca55348d8.png` |

Each visible result retains the warm material palette, references the real cabinet, removes the separate guest card, and offers a distinct hierarchy/composition. The physical booth display still contains small photographic prints. When implementing a chosen direction, keep web text and controls live; derive suitable art-only assets instead of shipping a flattened UI screenshot.

## Selected direction and sign correction

The user selected displayed option 3 (The Threshold), with a physical-accuracy correction: remove the wood immediately to the left and right of the PHOTOBOOTH LED sign. The sign should stand above the cabinet's horizontal timber rail, with open background flanking the lightbox. Retain the rail below the sign, thin sign casing, lower cabinet walls, curtain, photo board/hatch and glazed pane.

This is a single revised visual, not another three-direction exploration. The original option-3 mockup is the edit target; `1000018870.jpg` is the physical sign-construction reference. Preserve all existing typography, crop, navigation, hero copy, colors and actions. Application code remains unchanged during this revision.

Revised output: `C:\Users\QUINJ3875\.codex\generated_images\01a0864b-7ffa-7920-a2c4-6bb8edae4292\exec-fc4495b9-3a2d-432d-801d-22fa2f2521b7.png`.

Generated with the built-in image editor and displayed once. Visual inspection confirms that the two solid timber wings beside the lightbox are gone; the sign casing and horizontal rail below remain. The rest of the curtain-led UI composition is retained. This revised visual is awaiting the user's accuracy check and has not been implemented in the website.

## Subsequent approval and implementation

The user approved this revised visual and asked to include the proposed animation: “yes lets do that and also include the animation you've mentioned for this design”. The proposal-only boundaries above describe the historical ideation turn, not the current authorization. Implementation and verification now live in `../hero-curtain-implementation/`, with the exact approved mock preserved as `approved-hero.png`. The existing Next.js hero is replaced by the selected generated facade and a reversible, user-initiated curtain reveal. The rest of the earlier redesign remains in place. See `../../design-qa.md` for the current release gate.
