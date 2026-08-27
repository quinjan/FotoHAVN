# Issue 98 approved desktop intro direction handoff

Date: 2026-08-28
Status: ready for a fresh implementation agent  
Ticket: [Prototype the Website Intro Experience composition and motion](https://github.com/quinjan/FotoHAVN/issues/98)  
Branch: `codex/prototype-website-intro-98`  
Worktree: `C:\Quinjan\Repos\FotoHAVN-prototype-98`

## Fresh-agent mission

Replace only Variant A's **desktop** intro choreography with the approved outside-to-inside booth journey below. Preserve the realistic booth artifact, Variant B, accessibility behavior, live homepage, and development-only variant switcher.

This remains a throwaway Wayfinder prototype. Produce a new browser-review checkpoint, publish it on this branch, and leave the HITL ticket open for the user's reaction. Do not treat this handoff as production authorization.

## Read first

Read these sources before editing:

- `website/.handoffs/issue-98-approved-desktop-intro-direction.md` — this file; current decision authority.
- `website/DESIGN.md`
- `website/tokens.json`
- `website/variables.css`
- `website/theme.css`
- `website/src/components/WebsiteIntroPrototype.tsx`
- `website/src/components/WebsiteIntroPrototype.module.css`
- `website/src/app/page.tsx`
- `website/design-qa.md` — historical evidence only; update it after the new implementation.

Use the Product Design `get-context`, `image-to-code`, Browser, and blocking `design-qa` workflows. Use ImageGen for any new booth/interior raster assets; do not reconstruct the booth, curtain, camera, light, or screen as CSS/div/SVG art.

## Approved visual truth

These repository files are durable and must be opened visually, not inferred from filenames:

- Exterior booth authority: `website/prototype-qa/issue-98/references/real-photobooth-reference.png`
- Existing realistic closed artifact to retain: `website/public/prototype/issue-98/variant-a-real-booth-closed.png`
- Real open-doorway and interior orientation: `website/prototype-qa/issue-98/references/real-booth-open-doorway-reference.png`
- Real left-wall screen/camera/light assembly: `website/prototype-qa/issue-98/references/real-booth-left-wall-screen-reference.png`
- User-corrected paired-light end-frame master: `website/public/prototype/issue-98/variant-a-left-wall-screen-paired-lights-desktop.png`
- External animation-generator pack (three consistently framed `1920 x 1080` keyframes): `website/public/prototype/issue-98/video-generator-pack-16x9/`
- User-supplied selected desktop journey video (`1920 x 1080`, 5.125 seconds): `website/public/prototype/issue-98/variant-a-generated-journey-desktop.mp4`
- Human-selected post-video workflow storyboard: `website/prototype-qa/issue-98/selected-black-development-focus-storyboard.png`
- Human-approved desktop storyboard: `website/prototype-qa/issue-98/references/approved-left-wall-welcome-screen-storyboard.png`
- Live homepage end state: the actual server-rendered homepage under the intro. Existing browser evidence may help with matching, but do not replace the final page with a screenshot.

The approved storyboard is the selected visual target. The two real interior photos control physical anatomy and camera orientation wherever the generated storyboard is ambiguous.

The `video-generator-pack-16x9` files are dedicated inputs for an external image-to-video model. They do not replace the browser prototype's `16:10` raster masters, and they intentionally leave the final physical screen black so the browser can composite the real homepage itself.

The selected video and post-video storyboard supersede the earlier miniature-page-in-screen handoff. The video's own final frame controls the browser zoom origin; do not crossfade to a differently proportioned still before the camera push.

## Approved desktop sequence

1. **Idle exterior**
   - Start on the existing realistic, zoomed-out, fully closed booth artifact.
   - Keep `FOTOHVN`, `SKIP INTRO`, `PRESS TO ENTER FOTOHVN`, and the development-only A/B switcher.
   - Retain the corrected exterior anatomy: left utility/display column; upper framed mirror only on the right column; lower-right walnut cabinetry.

2. **Open and enter**
   - On press, the real entrance curtain opens from left to right and gathers at the right, matching the physical booth.
   - The camera moves through the doorway while immediately biasing/panning toward the **left-hand interior wall**.
   - Do not reveal the website behind the curtain or across the whole booth opening.
   - Do not feature the right/rear standing or sitting background. Keep it concealed by the gathered curtain, doorway crop, and leftward camera path.

3. **Inside, aimed at the left wall**
   - Land on a believable close interior view of the left-hand wall.
   - Physical order and ownership:
     - `LOOK HERE` camera lens above;
     - physical landscape welcome touchscreen below the lens;
     - matching tall vertical white lights immediately to the screen's left and right;
     - metal control below the screen;
     - dark wood wall and real curtain edges around the assembly.
   - The booth's right/rear photo background belongs off-screen and must remain unrevealed.
   - The physical welcome touchscreen remains pure black while the full wall composition is visible.
   - Hold this composition long enough for the physical-screen destination to register. Do not place a miniature homepage inside the screen.

4. **Enter the welcome screen**
   - Continue the camera dolly directly into the black physical touchscreen until the black glass fills the entire viewport and all bezel/equipment edges have moved off-screen.
   - Only after full black takes over, reveal the already-mounted live, server-rendered homepage from near-black at low exposure and heavy blur, then complete a gentle focus pull into its normal crisp state.
   - Never swap a raster screenshot into the live page and never show a floating, bordered, or miniature webpage overlay.
   - Complete by unmounting the intro, restoring body scroll, and focusing `#hero-heading` without scrolling.

Exact milliseconds are not locked. Tune a slow, continuous, cinematic desktop sequence. Spatial continuity matters more than a specific duration.

## Spatial rule that must not drift

From the entrance looking inward:

- **Left-hand interior** = screen, `LOOK HERE` camera, paired vertical lights, and control.
- **Right-hand/rear interior** = background where guests stand or sit.

The approved camera moves left and excludes the right/rear background. The user's last correction specifically rejected a second-frame angle that looked into the background side.

## Implementation guidance

- Limit implementation and review to desktop. The agreed decision viewport is `1440 x 900` CSS px at DPR 1.
- Mobile adaptation is explicitly paused until the user approves the desktop animation. Do not regenerate mobile assets, tune mobile motion, or report mobile as passed.
- Reuse the existing Variant A idle artifact rather than regenerating it.
- Generate purpose-made desktop intermediate raster assets if needed. Likely useful states are:
  - a left-biased open-doorway/threshold view;
  - a close left-wall screen assembly view with a clearly bounded physical display.
- Do not enlarge a low-resolution crop until it pixelates.
- Avoid a visibly swapped curtain texture, giant curtain close-up, disconnected rail layer, CSS fold simulation, or abrupt cut between unrelated perspectives.
- For the post-video handoff, reveal the actual server-rendered homepage already mounted beneath the intro. Animate only its exposure, opacity, and focus; never scale, reflow, or replace it with a viewport-specific raster capture.
- Reserve the scrollbar gutter while body scroll is locked so restoring scroll cannot change the live page's responsive width at completion.
- Preserve progressive-enhancement behavior.
- Keep Variant B (`?variant=B`) unchanged. It remains an alternate prototype, not the selected implementation direction.

## Existing code seams

- Main state machine and variant selection: `website/src/components/WebsiteIntroPrototype.tsx`
- Motion/layout: `website/src/components/WebsiteIntroPrototype.module.css`
- Query-selected initial variant: `website/src/app/page.tsx`
- Current review URLs:
  - `http://localhost:4173/fotohvn?variant=A`
  - `http://localhost:4173/fotohvn?variant=B`
- Start locally from `website/` with:

```powershell
npm run dev -- --hostname 0.0.0.0 --port 4173
```

The last published checkpoint before this handoff is commit `04a16b5` (`Refine realistic curtain reveal`). Its realistic assets are useful, but its Variant A motion is **rejected and superseded** by this handoff. Earlier issue comments also describe superseded direct-page and curtain-close-up directions.

## Interaction invariants to preserve

- Semantic button activation by pointer, Enter, and Space.
- Real-browser Tab order reaches Skip and Enter.
- Escape and `SKIP INTRO` dismiss from idle or motion.
- Underlying `#site-content` stays inert while the intro is active.
- Body scroll locks during the intro and restores on completion/skip.
- Final focus moves to `#hero-heading` with `preventScroll`.
- Reduced motion collapses the cinematic sequence to a near-instant safe handoff.
- Direct `?variant=A|B` selection works without a wrong-variant flash.
- Development-only PREV/NEXT and ArrowLeft/ArrowRight switching continue to work.
- No production persistence, session storage, audio, Photo Strip, Brand Strip, development hold, or welcome-copy hold is added in this ticket.

## Explicitly rejected directions

Do not revive these without new user approval:

- Showing the homepage directly behind the opening entrance curtain.
- Turning the whole doorway into a website portal.
- Extreme zoom into a curtain texture.
- Swapping to a separate curtain close-up; the swap was visually obvious.
- CSS clipping/compression that made the curtain look pixelated, cut off the top, or folded as a flat screen wipe.
- Showing the right/rear standing/sitting background as the camera enters.
- Placing the interior screen on the wrong wall or approaching it from the wrong angle.
- Showing the webpage as a miniature overlay inside the physical screen before the camera reaches full black.
- Building or tuning mobile before the desktop flow is approved.
- Selecting Variant B's drawing/canvas treatment as the primary direction.

## Verification and handoff gates

The next agent must not hand off from code/build confidence alone.

1. Run browser-rendered desktop QA at `1440 x 900` using the in-app Browser and `http://localhost:4173/`.
2. Capture and inspect at minimum:
   - idle exterior;
   - curtain opening with left-biased threshold entry;
   - inside left-wall black-screen composition;
   - continued push until the black screen fills the viewport;
   - low-exposure development and focus-pull states;
   - final live homepage.
3. Test pointer activation and actual browser CUA keyboard activation, not only source inspection or locator `.press()`.
4. Re-test Skip, Escape, focus handoff, scroll restoration, A/B switching, horizontal overflow, and browser console warnings/errors.
5. Put the approved storyboard/real interior references and implementation captures together in combined comparison boards before judging fidelity.
6. Update `website/design-qa.md`; it must end exactly `final result: passed` for the desktop-only scope with mobile explicitly deferred.
7. Run:

```powershell
npm run lint
npx tsc --noEmit --pretty false
npm run build
git diff --check
```

8. Run a fresh gpt-taste plan-conformance review after implementation and after every repair. Do not reuse an earlier verification agent/result. Then use a fresh desktop visual reviewer.
9. Commit and push the prototype branch, comment on the ticket with the new review checkpoint, and leave the ticket open until the user approves the working desktop animation.

## Definition of the next checkpoint

A fresh agent is done when the approved outside-to-left-wall-to-physical-screen journey is running at the Variant A desktop URL, the user can inspect it locally, all desktop gates above pass, the branch and ticket point to the evidence, Variant B is intact, mobile is explicitly untouched/deferred, and issue 98 remains open for HITL review.
