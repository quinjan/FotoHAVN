# Hero-to-page motion proposals

## Subsequent selection — 2026-09-10

The user initially selected Soft Exposure, then changed to **Editorial Lift (displayed option 1)** during implementation. Editorial Lift is implemented and verified; current authority and evidence are in `../editorial-lift/`. Soft Exposure is superseded. Proposal-only statements below describe the historical ideation phase, not current application state.

Date: 2026-09-10. Proposal-only work; application code is unchanged.

The user rejected opening/closing the hero curtain and showing the interior. They want an animation that moves visitors into the following webpage sections. Preserve the selected closed facade, accurate raised sign without timber wings, typography, palette, and intimate editorial mood. Do not interpret this as rejection of the separate 3D explorer further down the page.

## Interaction contract for all proposals

- Keep the booth curtain closed throughout; never reveal an interior or move the camera into the booth.
- Replace the hero's secondary action with EXPLORE THE EXPERIENCE, linking to #experience. Keep RENT FOTOHVN unchanged.
- Connect the existing hero to the existing “The world can wait. This little moment is yours.” section, without skipping it or rearranging the rest of the page.
- Ordinary scroll is primary; the secondary action moves to that same destination. No wheel interception, long scroll trap, forced autoplay, all-page cinematic intro, bright flash or repeated looping.
- On completion, continue normal document scrolling through the booth, prints, album and inquiry. Later sections may share restrained entrance timing, not repeat the large hero transition.
- Reduced motion uses a direct section handoff; mobile avoids long pinning and large perspective movement.
- Generated images are explanatory still storyboards, not implemented or recorded animations.

## Independent motion directions

Names are prompt identities, not option numbers. Final numbering follows actual displayed image order only.

- **The Editorial Lift** — preferred. A warm-paper Experience section rises over the resting hero as its artwork subtly recedes and copy fades. The destination headline resolves first, then the two existing photographs settle into place. Scroll-linked overlap over roughly half a viewport; button-driven handoff approximately 900ms, interruptible by input. No booth deformation.
- **From Booth to Paper** — a more theatrical product metaphor. An ivory printed sheet emerges from the existing lower-left print hatch, expands toward the viewer, and becomes the Experience section. No curtain change and no extra people card added to the resting hero. More asset/masking work and a simpler mobile version would be needed.
- **The Soft Exposure** — the quietest direction. A warm exposure-like veil dissolves the hero into the paper section while its serif headline replaces the hero copy. No bright flash, whitening spike, simulated shutter or tunnel. Short crossfade tied to the section handoff, then ordinary scrolling.

## Visual grounding

Fresh hero and Experience screenshots from the local page are attached to each independent ImageGen request. Preserve existing Cormorant Garamond and Manrope. Palette: Off-white #FBF8F2, Warm Ivory #F3EBDD, Cream Paper #E8DDCE, Ebony #1E1A17, Dark Walnut #2D211B, Soft Brown #756457.

Output format: each concept is one 2048×1152 landscape motion storyboard, with three consecutive desktop keyframes at 1440×900 design proportions. Each board explains one animation, not three alternate layouts. Labels show resting state, transition and arrived section; motion annotations are presentation-only, not proposed on-page controls.

Stop after showing the proposals for user selection. Do not implement or deploy until a direction is selected.

## Completed displayed-order mapping

All three independent built-in ImageGen results were displayed exactly once in the main conversation. They are static storyboards, not working animation previews.

| Displayed option | Direction | Preview output |
| --- | --- | --- |
| 1 | The Editorial Lift | `C:/Users/QUINJ3875/.codex/generated_images/01a0864b-7ffa-7920-a2c4-6bb8edae4292/exec-e45effb0-3dd1-44e2-9902-30c205491912.png` |
| 2 | From Booth to Paper | `C:/Users/QUINJ3875/.codex/generated_images/01a0864b-7ffa-7920-a2c4-6bb8edae4292/exec-63bac32b-34a4-4f3c-832e-fcb51bd10f24.png` |
| 3 | The Soft Exposure | `C:/Users/QUINJ3875/.codex/generated_images/01a0864b-7ffa-7920-a2c4-6bb8edae4292/exec-df476dab-fd71-499f-8312-dd4676614f10.png` |

Each board shows the closed hero, a distinct transition state and the actual next section's composition. No interior is shown. The first direction is the recommended approach. The second has a more pronounced paper/perspective treatment; the third is a restrained dissolve. Application source remains unchanged. On selection, preserve the actual existing guest-photo files and responsive layout; do not substitute the storyboard's regenerated miniature photos or flatten its UI into the live page. Any camera/crop drift in a storyboard is illustrative, not authorization to redesign the selected booth facade.
