# FOTOHVN Design QA — Passes 1–6

## Pass 6 Findings

No actionable P0, P1, or P2 mismatch remains in the supplied pass-6 regression scope.

- **Accepted measurement note — story-title serif overhang is not actionable clipping.**
  Surface: fonts/typography, spacing/layout rhythm, responsiveness, and copy/content.
  Location: `website/src/components/ClosingExperience.module.css:53–69` and `:512–531`; selector `.storyTitle`.
  Evidence: the viewed normalized board `website/design-qa/comparison-pass-6-final-mobile-regression.png` places the structural reference, the explicit `DESIGN.md` mobile requirements, all three latest hero captures, and five downstream 320px surfaces in one comparison input. At 320px the browser reports inner/client/document-scroll widths of `320/305/305`; the page-wide scrollbar visible in pass 5 is gone. The hero content is `305/305`, its `h1` and both spans are `257/257`, and the secondary CTA remains `44px`. Every audited downstream section/event heading is `257/257` except `.storyTitle`, which reports `261px` scroll width against a `257px` client. The story capture visibly contains the complete `MORE THAN A PHOTOBOOTH.` including the final period, with the normal 24px section gutters and no document or ancestor widening. At 375px the document is `360/360` and every audited title is `312/312`; at 390px the document is `375/375` and every title is `327/327`. Event-heading maximum overflow is `0px` at both larger widths.
  Assessment: the isolated 4px local delta is consistent with serif glyph ink/measurement overhang rather than a broken text track. It does not clip copy, consume the visible gutter, create two-dimensional scrolling, or change wrapping. No implementation change is warranted.

## Pass 6 Regression Status

- Pass-5 grouped P2 — downstream display-heading min-content overflow: **resolved**. The document now fits its client at all three audited widths, and the 320px package, event, story, final-CTA, and inquiry captures show complete, naturally wrapped headings.
- Pass-4 P2 — root-width overflow: **remains resolved**. The 320px document is `305/305` after the persistent vertical scrollbar is accounted for.
- Pass-3 P2 — first-line hero track overflow/right-gutter loss: **remains resolved**. Hero `h1` and both spans are `257/257` at 320px, `312/312` at 375px, and `327/327` at 390px.
- Pass-2 P2 — second-line nowrap/min-content expansion: **remains resolved**. `DEVELOPED DIFFERENTLY.` remains complete at all three widths.
- Pass-1 P2 — secondary mobile CTA below 44px: **remains resolved**. The CTA remains exactly `44px` high.
- Console: no warnings or errors in the supplied post-fix browser run.

## Pass 5 Findings

- [P2] Small-phone display headings outside the hero still exceed their text tracks and widen the entire document.
  Surface: fonts/typography, spacing/layout rhythm, responsiveness, and accessibility.
  Location: `website/src/components/UpperExperience.module.css:181–193` and `:537–548`; `website/src/components/MiddleExperience.module.css:35–50`, `:231–237`, `:374–378`, and `:417–526`; `website/src/components/ClosingExperience.module.css:20–29`, `:53–69`, `:142–147`, `:203–209`, and `:428–489`.
  Evidence: `website/design-qa/comparison-pass-5-small-phone-overflow.png` places the source requirements, the latest hero, signature-package, events, story, final-CTA, and inquiry captures, and the supplied browser geometry in one viewed comparison input. The root minimum is gone and the hero now fits its available track: `.heroContent` is `305/305`, the hero `h1` and both spans are `257/257`, the primary CTA is `44px`, the approved headline is complete, and the 24px text gutters are intact. The document nevertheless remains `305px` client versus `322px` scroll, with a horizontal scrollbar visible in every captured surface. The element audit localizes the remaining min-content expansion to the downstream editorial headings: `.storyGrid`, `.storyCopy`, and `.storyTitle` are `257px` client versus `299px` scroll (`+42px`); event `h3` elements sit in `172px` maximum-width boxes but scroll as wide as `274px`; the signature title is `229/266`; the experience heading is `210/242`; the final CTA title is `231/245`; and the inquiry title is `257/260`. `#top`, `main`, and the story ancestor inherit approximately `18px` of page-level overflow. These are not intentional full-bleed images: the overflow follows large minimum display sizes combined with restrictive `ch` measures.
  Impact: every small-phone section can be panned sideways even though the hero itself is fixed. The persistent two-dimensional scrolling breaks the design language's calm 24px gutter system and creates a text-scaling/accessibility risk. It remains P2 because the content and core actions are still visible and usable.
  Fix: add one measured `@media (max-width: 420px)` typography pass in each owning module. Allow the heading boxes to use the full mobile track with `min-inline-size: 0`, `max-inline-size: 100%`, and `max-width: none`; then lower only the small-phone display minima enough for the longest unbreakable word to fit. Preserve Cormorant Garamond, uppercase treatment, line-height, and editorial hierarchy. Do not add `overflow-x: hidden`, `overflow: clip`, `word-break: break-all`, or forced mid-word wrapping.

  ```css
  /* UpperExperience.module.css */
  @media (max-width: 420px) {
    .experienceHeader,
    .experienceHeader .sectionHeading {
      min-inline-size: 0;
      max-inline-size: 100%;
    }

    .experienceHeader .sectionHeading {
      max-width: none;
      font-size: clamp(2.3rem, 12vw, 3rem);
    }
  }

  /* MiddleExperience.module.css */
  @media (max-width: 420px) {
    .packageHeader,
    .packageHeader h2,
    .galleryHeader h2,
    .eventsHeader h2,
    .eventTitleBlock,
    .eventTitleBlock h3 {
      min-inline-size: 0;
      max-inline-size: 100%;
    }

    .packageHeader h2,
    .galleryHeader h2,
    .eventsHeader h2 {
      max-width: none;
      font-size: clamp(2.75rem, 13vw, 3.5rem);
    }

    .eventTitleBlock h3 {
      max-width: none;
      font-size: clamp(2.25rem, 11.5vw, 2.75rem);
    }
  }

  /* ClosingExperience.module.css */
  @media (max-width: 420px) {
    .storyGrid,
    .storyCopy,
    .storyTitle,
    .calloutCopy,
    .calloutTitle,
    .inquiryIntro,
    .inquiryTitle {
      min-inline-size: 0;
      max-inline-size: 100%;
    }

    .storyTitle,
    .calloutTitle,
    .inquiryTitle {
      max-width: none;
      font-size: clamp(2.25rem, 12vw, 2.75rem);
    }
  }
  ```

  After the grouped fix, recapture the same six 320 × 720 states and verify document `scrollWidth === clientWidth`, each audited heading has `scrollWidth <= clientWidth`, no word is clipped or split unnaturally, and 24px section gutters remain intact. Also recheck 375px and 390px to confirm the narrower override does not flatten the intended hierarchy.

No P0 or P1 findings were observed in pass 5.

## Pass 5 Regression Status

- Pass-4 P2 — root `min-width: 320px`: **resolved as a cause**. Removing it lets the hero content follow the `305px` client track.
- Pass-3 P2 — first-line hero track overflow/right-gutter loss: **remains resolved**. Hero content, `h1`, and both spans fit their measured boxes; the complete headline keeps its 24px gutters.
- Pass-2 P2 — second-line nowrap/min-content expansion: **remains resolved**. `DEVELOPED DIFFERENTLY.` remains complete.
- Pass-1 P2 — secondary mobile CTA below 44px: **remains resolved**. The supplied hero metric is `44px`.
- Remaining grouped P2 — downstream display-heading min-content overflow: **actionable**. The document remains `305/322`, with a visible scrollbar across all six captures.
- Console and interaction metrics were not newly supplied for pass 5; pass 4's no-error result is preserved as prior evidence, not claimed as a fresh check.

## Pass 4 Findings

- [P2] The 320px state still has document-level horizontal overflow because the page root enforces a wider minimum than the available client width.
  Surface: spacing/layout rhythm, responsiveness, and accessibility.
  Location: `website/src/app/globals.css:16–20`; specifically `html { min-width: 320px; }` at line 17.
  Evidence: the viewed normalized board `website/design-qa/comparison-pass-4-mobile-hero-normalized.png` combines the latest 390 × 844, 375 × 812, and 320 × 720 mobile hero captures in one input. The 390px and 375px states have equal document client/scroll widths (`375/375` and `360/360`). The 320px state has a `305px` document client but a `322px` document scroll width, and its capture visibly shows a horizontal scrollbar. The root's hard `320px` minimum explains why `.heroContent` remains `320px` wide when the browser's persistent vertical scrollbar leaves only `305px` of client width. This is no longer a headline min-content failure: the first span now reports equal scroll/client widths at every state (`327/327`, `312/312`, and `272/272`), the full second line wraps, and the first-line gutter is visually comfortable.
  Impact: users can pan the whole page sideways at the narrow breakpoint. That violates the design language's responsive requirement, creates avoidable two-dimensional scrolling, and is a text-zoom/accessibility risk even though the hero remains readable. It is a P2 rather than a P1 because no core action or content is hidden.
  Fix: remove the hard root-width floor so the initial containing block can shrink to the actual client width. Delete `min-width: 320px` from `html` or replace it with logical zero sizing; keep the existing bounded hero/grid rules. Do not add `overflow-x: hidden` or `overflow: clip`, because that would mask rather than resolve the sizing error.

  ```css
  html {
    min-inline-size: 0;
    background: var(--surface-canvas);
    scroll-padding-top: calc(var(--navigation-height) + var(--spacing-16));
  }
  ```

  After the root can shrink, recapture 320 × 720 and confirm `.heroContent` follows the `305px` client track, document `scrollWidth === clientWidth`, the full first span still fits its recalculated content box, the second line remains complete, and the 24px gutters remain visible. If the narrower client track makes the first span overflow after this root fix, reduce only the small-phone fluid display scale in a measured follow-up; do not pre-emptively clip it.

No P0 or P1 findings were observed in pass 4.

## Pass 4 Regression Status

- Pass-3 P2 — first-line track overflow/right-gutter loss: **resolved**. The first span's scroll and client widths now match at 390px, 375px, and 320px, and the gutter is visually comfortable.
- Pass-2 P2 — second-line nowrap/min-content expansion: **remains resolved**. `DEVELOPED DIFFERENTLY.` wraps completely at all three widths.
- Pass-1 P2 — secondary mobile CTA below 44px: **remains resolved**. The CTA is exactly `44px` high at all three widths.
- Remaining P2 — 320px document overflow: **still actionable**. Document width is `322px` against a `305px` client, with a visible horizontal scrollbar caused by the root `min-width: 320px`.
- Console: no warnings or errors in the supplied pass-4 browser run.

## Pass 3 Findings

- [P2] The small-phone hero still breaches its text track, and the 320px state now exposes document-level horizontal overflow.
  Surface: fonts/typography, spacing/layout rhythm, responsiveness, and accessibility.
  Location: `website/src/components/UpperExperience.module.css:12–17`, `:19–27`, `:46–50`, `:77–93`, and the small-phone rules at `:612–640`; selectors `.container`, `.hero`, `.heroContent`, `.heroHeading`, and `.heroHeading span:first-child`.
  Evidence: the new normalized combined board `website/design-qa/comparison-pass-3-mobile-hero-normalized.png` shows the latest post-fix hero at 390 × 844, 375 × 812, and 320 × 720 CSS px. The approved second line now wraps completely at all three widths. The first line remains unbroken, but its rendered text is wider than its own content box: `335px` scroll width versus `327px` client width at 390px, `322px` versus `312px` at 375px, and `280px` versus `272px` at 320px. This is an 8–10px local overflow. At 390px and 375px the comma is visible, but the intended 24px right gutter contracts to roughly 16px and 14px. At 320px, `.heroContent` remains `320px` wide while the document client is only `305px`; document `scrollWidth` is `322px` versus `305px` client width, and the screenshot visibly contains a horizontal scrollbar.
  Impact: the above-the-fold wordmark still feels pressed against the right edge instead of maintaining the calm 24px mobile gutter. At 320px the page can pan sideways, which is a responsive and text-zoom accessibility regression.
  Fix: size the hero grid item against the actual client-width track, not the nominal viewport width, and reduce the `<=420px` display scale enough for `PHOTOGRAPHS,` to fit after the 24px gutters are subtracted. Override the shared container width with `inline-size: auto; justify-self: stretch; min-inline-size: 0; max-inline-size: 100%` on `.heroContent`, retain `grid-template-columns: minmax(0, 1fr)` and a bounded `.hero`, and use a measured scale near `clamp(2.2rem, 11vw, 2.8rem)` on `.heroHeading`. At the 320px browser geometry, that resolves near `35.2px` and is estimated to keep the first line within the corrected `257px` content box. Keep only the first span nowrap and keep the second span wrappable.

  ```css
  @media (max-width: 420px) {
    .hero {
      inline-size: 100%;
      min-inline-size: 0;
      max-inline-size: 100%;
      grid-template-columns: minmax(0, 1fr);
    }

    .heroContent {
      inline-size: auto;
      min-inline-size: 0;
      max-inline-size: 100%;
      justify-self: stretch;
    }

    .heroHeading {
      inline-size: 100%;
      min-inline-size: 0;
      max-inline-size: 100%;
      font-size: clamp(2.2rem, 11vw, 2.8rem);
    }

    .heroHeading span:first-child {
      white-space: nowrap;
    }

    .heroHeading span:last-child {
      overflow-wrap: normal;
      white-space: normal;
    }
  }
  ```

  Recapture all three widths and confirm: `firstSpan.scrollWidth <= firstSpan.clientWidth`; `.heroContent` does not exceed `document.documentElement.clientWidth`; document `scrollWidth === clientWidth`; the 24px gutters remain visually intact; the full second line remains visible; and both hero CTAs remain at least 44px high.

No P0 or P1 findings were observed in pass 3.

## Pass 3 Regression Status

- Pass-2 P2 — second-line nowrap/min-content expansion: **resolved**. `DEVELOPED DIFFERENTLY.` now wraps and is complete at 390px, 375px, and 320px.
- Pass-1 P2 — secondary mobile CTA below 44px: **remains resolved**. The supplied browser metrics report `44px` at all three widths.
- Remaining P2 — first-line track overflow and right-gutter loss: **still actionable**. The first span exceeds its content box by 8–10px at every tested width.
- Remaining P2 — 320px document overflow: **still actionable**. Document width exceeds its client width by 17px and the capture shows a horizontal scrollbar.
- Console: no warnings or errors in the supplied pass-3 browser run.

## Pass 2 Findings

- [P2] The mobile hero still clips the approved headline because the long second line controls the grid item’s min-content width.
  Surface: fonts/typography, spacing/layout rhythm, responsiveness, and copy/content.
  Location: `website/src/components/UpperExperience.module.css:19`, `:46`, `:77`, and the mobile rules near `:500`; selectors `.hero`, `.heroContent`, `.heroHeading`, and `.heroHeading span`.
  Evidence: the normalized combined board `website/design-qa/comparison-pass-2-mobile-hero-normalized.png` shows the same post-fix hero state at 390 × 844, 375 × 812, and 320 × 720 CSS px. `PHOTOGRAPHS,` remains readable, but `DEVELOPED DIFFERENTLY.` stays on one line and is visibly cropped at every width. Browser geometry reports right gaps of `-178.54px`, `-172.31px`, and `-152.23px`, respectively. At 320px, the computed font size is `39.2px`, the `h1` is `448.225px` wide, and its hero-content parent expands to `496.225px`. `.hero { overflow: hidden; }` masks the local crop from page-level overflow checks.
  Impact: the main above-the-fold brand promise is incomplete on every tested phone width, and the widened grid item makes the required 24px content gutter unreliable.
  Fix: constrain the grid track and item with `minmax(0, 1fr)` and `min-width: 0`; preserve only `PHOTOGRAPHS,` as nowrap; allow `DEVELOPED DIFFERENTLY.` to wrap at its space. Use the measured small-phone scale below, resolving to `39.2px` at 320px, `45px` at 375px, and `46.8px` at 390px.

  ```css
  @media (max-width: 420px) {
    .hero {
      grid-template-columns: minmax(0, 1fr);
    }

    .heroContent,
    .heroHeading {
      width: 100%;
      min-width: 0;
      max-width: 100%;
    }

    .heroHeading {
      font-size: clamp(2.45rem, 12vw, 2.925rem);
    }

    .heroHeading span {
      min-width: 0;
    }

    .heroHeading span:first-child {
      white-space: nowrap;
    }

    .heroHeading span:last-child {
      white-space: normal;
      overflow-wrap: normal;
    }
  }
  ```

  Recapture all three widths and confirm: the comma remains visible, the second line wraps without clipping, all content respects the 24px gutters, and `scrollWidth === clientWidth` at both document and hero-content levels.

No P0 or P1 findings were observed in pass 2.

## Pass 2 Regression Status

- Earlier P2 — secondary mobile CTA below 44px: **resolved**. `.editorialLink` is exactly `44px` high at 390px, 375px, and 320px. Its text-link hierarchy is preserved.
- Earlier P2 — mobile hero headline clipping: **still actionable**. The first fix changed the display sizing, but the shared `white-space: nowrap` rule and min-content sizing still crop the second approved line.
- Console: no warnings or errors in the supplied post-fix browser run.

## Pass 1 Findings — Preserved History

- [P2] The mobile hero clipped approved headline punctuation/content.
  Evidence: preserved in `website/design-qa/comparison-focus-mobile-states.png`. The 390 × 844 state was locally clipped by `.hero { overflow: hidden; }` even though no page-level overflow was reported.
  Impact: the most important brand copy was incomplete.
  Original fix direction: reduce the mobile fluid size and verify 320px, 375px, and 390px while keeping the approved first line intact.

- [P2] The mobile secondary hero CTA was shorter than the 44px design-system target.
  Evidence: preserved in `website/design-qa/comparison-focus-mobile-states.png`; prior padding and line-height produced an approximately 38px target.
  Impact: the secondary conversion action was less reliable to tap.
  Original fix direction: add `min-height: 44px; display: inline-flex; align-items: center;` without turning it into a filled button.

No P0 or P1 findings were observed in pass 1.

## Comparison Target and Normalization

### Design authority

- Primary truth: `website/DESIGN.md`, followed by `website/tokens.json`, `website/variables.css`, and `website/theme.css`.
- Structural source visual: `website/design-qa/reference-refero-viewport-1440x1000.png`.
- The reference supplies breathing room, poster hierarchy, flat surfaces, grid discipline, and low elevation only. FOTOHVN’s approved palette, serif typography, photography, and copy override the reference identity.
- The reference has no matching mobile state. Responsive fidelity is judged against `DESIGN.md`; no false pixel match to its desktop screenshot is claimed.

### Pass 1 evidence

- Source: 1440 × 1000 CSS target and 1440 × 1000 pixels, treated as 1 source pixel per CSS pixel.
- Desktop implementation: 1440 × 1000 CSS; primary images 1425 × 990 pixels. The hero was resampled to 1440 × 1000 for the equal-size board. The reported browser DPR was approximately 1.25, while saved pixels reflect the browser capture surface.
- Original pass-1 mobile state: preserved in `website/design-qa/comparison-focus-mobile-states.png`.
- States covered: desktop hero, Vintage look, package, gallery, story, inquiry; mobile hero, open menu, looks, package, inquiry.

### Pass 2 evidence

- Combined board: `website/design-qa/comparison-pass-2-mobile-hero-normalized.png`.
- Board dimensions: 1181 × 998 pixels.
- State: default mobile hero, top-aligned.
- 390 × 844 CSS: source 375 × 811 pixels, normalized to 390 × 844 board pixels.
- 375 × 812 CSS: source 360 × 779 pixels, normalized to 375 × 812 board pixels.
- 320 × 720 CSS: source 305 × 686 pixels, normalized to 320 × 720 board pixels.
- Density: each panel was resampled to its stated CSS viewport, so 1 CSS pixel equals 1 board pixel. A fresh device-scale factor was not supplied, so no DPR is inferred from bitmap dimensions.
- The board includes the relevant `DESIGN.md` mobile source requirements and all three implementations in one viewed comparison input. Findings were not made from separate image views.

### Pass 3 evidence

- Combined board: `website/design-qa/comparison-pass-3-mobile-hero-normalized.png`.
- Board dimensions: 1173 × 1034 pixels.
- State: latest post-fix default mobile hero, top-aligned.
- 390 × 844 CSS: source capture 375 × 811 pixels, normalized to 390 × 844 board pixels.
- 375 × 812 CSS: source capture 360 × 779 pixels, normalized to 375 × 812 board pixels.
- 320 × 720 CSS: source capture 305 × 686 pixels, normalized to 320 × 720 board pixels.
- Density: each supplied capture was resampled to its stated CSS viewport so the board presents one normalized board pixel per CSS pixel. No browser DPR is inferred from the capture dimensions.
- The new board combines all three latest implementation states with the explicit mobile checks in one viewed comparison input. The design source for this focused responsive pass is `website/DESIGN.md`: complete approved headline, at least 24px mobile gutters, image priority, and no broken wrapping or overflow.

### Pass 4 evidence

- Combined board: `website/design-qa/comparison-pass-4-mobile-hero-normalized.png`.
- Board dimensions: 1173 × 1050 pixels.
- State: latest post-fix default mobile hero, top-aligned.
- 390 × 844 CSS: source capture 375 × 811 pixels, normalized to 390 × 844 board pixels; document client/scroll `375/375`; first-span scroll/client `327/327`; computed heading size `42.944px`.
- 375 × 812 CSS: source capture 360 × 779 pixels, normalized to 375 × 812 board pixels; document client/scroll `360/360`; first-span scroll/client `312/312`; computed heading size `41.272px`.
- 320 × 720 CSS: source capture 305 × 686 pixels, normalized to 320 × 720 board pixels; document client/scroll `305/322`; first-span scroll/client `272/272`; computed heading size `35.2px`; visible horizontal scrollbar.
- CTA: exactly `44px` high at all three widths. The second headline line is complete at all three widths. No console warnings or errors were reported.
- Density: each supplied capture was resampled to its stated CSS viewport so the board presents one normalized board pixel per CSS pixel. No browser DPR is inferred from the bitmap dimensions.
- The board combines all three latest implementation states, exact browser geometry, and the explicit source checks in one viewed comparison input. The design source for this focused responsive pass remains `website/DESIGN.md`: complete approved copy, at least 24px mobile gutters, image priority, no broken wrapping or overflow, and CTAs at least 44px high.

### Pass 5 evidence

- Combined board: `website/design-qa/comparison-pass-5-small-phone-overflow.png`.
- Board dimensions: 1030 × 1770 pixels.
- Source visual truth: `website/DESIGN.md`, especially the editorial typography rules at lines 96–125 and responsive/accessibility requirements at lines 358–376. The relevant source requirements are printed in the board with all six implementations.
- Implementation screenshots: `website/design-qa/implementation-mobile-hero-320x720.png`, `website/design-qa/implementation-mobile-package-320x720.png`, `website/design-qa/implementation-mobile-events-corporate-320x720.png`, `website/design-qa/implementation-mobile-story-320x720.png`, `website/design-qa/implementation-mobile-final-cta-320x720.png`, and `website/design-qa/implementation-mobile-inquiry-320x720.png`.
- Viewport/state: 320 × 720 CSS target; default light theme; hero top state plus scrolled signature-package, events, story, final-CTA, and inquiry states.
- Source pixels and density normalization: each supplied browser bitmap is 305 × 686 pixels. Each is resampled to 320 × 720 on the board, so one board pixel represents one stated CSS pixel. The smaller source bitmap is consistent with the browser capture surface after persistent scrollbars; no device pixel ratio is inferred.
- Browser geometry: document client/scroll `305/322`; hero content `305/305`; hero `h1` and spans `257/257`; CTA `44px`; story grid/copy/title `257/299`; event `h3` client as narrow as `172px` and scroll as wide as `274px`; signature title `229/266`; experience heading `210/242`; final CTA title `231/245`; inquiry title `257/260`; `#top`, `main`, and story ancestor overflow approximately `18px`.
- The combined board is the full-view and focused comparison input for this pass: it shows the source constraints, exact geometry, six complete viewport captures, and the persistent horizontal scrollbar at readable scale. Separate crops were unnecessary because the scrollbar and affected display headings are directly visible, while the element metrics precisely identify overflow that a screenshot alone cannot localize.

### Pass 6 evidence

- Combined board: `website/design-qa/comparison-pass-6-final-mobile-regression.png`.
- Board dimensions: 1240 × 3020 pixels.
- Source visual truth: `website/DESIGN.md`, especially its typography, responsive, accessibility, and structural-reference rules; `website/design-qa/reference-refero-viewport-1440x1000.png` is embedded on the same board and explicitly labelled as structural reference only.
- Implementation screenshots: `website/design-qa/implementation-mobile-hero-320x720.png`, `website/design-qa/implementation-mobile-hero-375x812.png`, `website/design-qa/implementation-mobile-hero-390x844.png`, `website/design-qa/implementation-mobile-package-320x720.png`, `website/design-qa/implementation-mobile-events-corporate-320x720.png`, `website/design-qa/implementation-mobile-story-320x720.png`, `website/design-qa/implementation-mobile-final-cta-320x720.png`, and `website/design-qa/implementation-mobile-inquiry-320x720.png`.
- Viewport/state: default light theme; hero top states at 320 × 720, 375 × 812, and 390 × 844 CSS targets, plus scrolled signature-package, events, story, final-CTA, and inquiry states at 320 × 720.
- Source pixels and density normalization: the 320px browser bitmaps are 305 × 686 pixels and are resampled to 320 × 720; the 375px bitmap is 360 × 779 and is resampled to 375 × 812; the 390px bitmap is 375 × 811 and is resampled to 390 × 844. One board pixel represents one stated CSS pixel; no DPR is inferred.
- Browser geometry: 320 inner/client/document scroll `320/305/305`; hero content `305/305`; hero `h1` and spans `257/257`; CTA `44px`; every audited section/event heading `257/257` except story title `261/257`. The story's full period and normal gutters are visible and the document does not widen. At 375px the document is `360/360`, every audited title is `312/312`, and event maximum overflow is `0px`. At 390px the document is `375/375`, every audited title is `327/327`, and event maximum overflow is `0px`.
- Browser runtime: no console warnings or errors were reported. Existing primary-anchor, look-tab, and mobile-menu Escape interaction evidence remains applicable because those controls were not changed by the responsive typography fix.
- The board was opened and inspected as the normalized full-view and focused regression input. Separate crops were unnecessary because the complete hero, relevant downstream headings, visible gutters, and scrollbar state are directly readable at normalized size, while the exact element metrics distinguish harmless local serif ink overhang from document-level overflow.

## Evidence Opened and Compared

**Full-view evidence**

- Pass 1: `website/design-qa/comparison-full-reference-vs-hero-normalized.png`.
- Pass 2: `website/design-qa/comparison-pass-2-mobile-hero-normalized.png` contains the complete browser-rendered mobile viewport at all three widths.
- Pass 3: `website/design-qa/comparison-pass-3-mobile-hero-normalized.png` contains the latest complete browser-rendered mobile viewport at all three widths.
- Pass 4: `website/design-qa/comparison-pass-4-mobile-hero-normalized.png` contains the newest complete browser-rendered mobile viewport at all three widths with the post-fix metrics printed in the same board.
- Pass 5: `website/design-qa/comparison-pass-5-small-phone-overflow.png` contains the source truth, latest 320px hero, package, events, story, final-CTA, and inquiry captures, and exact downstream heading metrics in one viewed comparison input.
- Pass 6: `website/design-qa/comparison-pass-6-final-mobile-regression.png` contains the structural reference, source requirements, all three current hero widths, five current downstream 320px surfaces, and exact post-fix geometry in one viewed comparison input.
- Pass-5 result: desktop structural inheritance remained accepted, while the 320px document scrollbar traced to downstream editorial headings and kept the grouped P2 open.
- Pass-6 result: the horizontal scrollbar is gone, complete headings retain the intended editorial hierarchy and 24px gutters, and no actionable P0/P1/P2 mismatch remains.

**Focused evidence**

- Hero/typography: `website/design-qa/comparison-focus-hero-typography.png`.
- Looks/package: `website/design-qa/comparison-focus-looks-package.png`.
- Prior mobile states: `website/design-qa/comparison-focus-mobile-states.png`.
- Gallery/story/inquiry: `website/design-qa/comparison-focus-gallery-story-inquiry.png`.
- A separate pass-2 crop was unnecessary: the combined board displays the complete headline, support copy, and CTAs at 1 CSS px per board px, while browser metrics supply exact geometry.
- A separate pass-3 crop was unnecessary: the combined board makes the first-line right edge, full second line, CTA height, and 320px scrollbar directly readable; supplied element and document metrics quantify the remaining overflow.
- A separate pass-4 crop was unnecessary: the normalized board presents the complete hero at each target size, the 320px scrollbar is directly visible, and the supplied geometry distinguishes root-width overflow from headline overflow.
- A separate pass-5 crop was unnecessary: the new board contains all six relevant browser states at normalized target size, and its exact element geometry distinguishes local heading min-content overflow from hero or root overflow.
- A separate pass-6 crop was unnecessary: the final normalized board keeps the complete relevant headings, their visible right edges, all three target widths, and the exact element/document geometry together at readable scale.

`website/design-qa/implementation-desktop-full-1440x1000.png` remains supplementary because its long-page stitch duplicates part of the hero image; it is not used to file findings.

## Required Fidelity Surfaces — Pass 6

### Fonts and typography

- Cormorant Garamond remains the correct editorial display family and Manrope remains the clean supporting/interface family. The supplied captures preserve the intended weight, uppercase hierarchy, tracking, line height, antialiasing, and display-versus-body optical contrast.
- Hero, experience, package, event, story, final-CTA, and inquiry headings wrap naturally without word breaks, truncation, or lost punctuation at 320px. At 375px and 390px every audited title reports equal scroll and client widths.
- The story title's isolated `261/257` metric at 320px does not widen the document, consume the visible gutter, or hide its final period. It is accepted as harmless serif ink/measurement overhang, not an actionable P2.
- Status: **passed**.

### Spacing and layout rhythm

- The page preserves the 24px small-phone gutters, single-column section rhythm, calm vertical spacing, restrained radii, low elevation, and light-to-dark cadence required by `DESIGN.md`.
- The 320px document now reports `305/305` client/scroll width after the persistent vertical scrollbar; 375px reports `360/360`, and 390px reports `375/375`. No page-wide horizontal scrollbar is visible.
- Image priority, CTA hierarchy, and complete editorial headings remain balanced at every tested width.
- Status: **passed**.

### Colors and visual tokens

- Off-white, Warm Ivory, Cream Paper, Ebony/Dark Walnut, Soft Brown, muted brass, and antique-metal details remain mapped to their intended roles. The package remains the one controlled dark band, and the hero veil remains warm and luminous rather than overly cinematic.
- No pastel reference taxonomy, vivid gradient, excessive gold, generic party color, new shadow, or semantic-state drift is visible.
- Previously verified contrast remains applicable because the color tokens and component treatments are unchanged.
- Status: **passed**.

### Image quality and asset fidelity

- The booth, curtain/guest, and print-strip photographs remain real editorial assets with coherent subject matter, warm crops, sharp detail, stable aspect ratios, and no compression, masking, or transparency artifacts visible at the normalized targets.
- No image has been replaced by a placeholder, CSS drawing, handcrafted SVG, emoji, or unrelated party-stock substitute.
- Repeated event/gallery photography remains a non-blocking P3 library limitation, not a fidelity regression.
- Status: **passed**.

### Copy and content

- Approved hero, experience, photographic-look, package, event, story, final-CTA, inquiry, and booking copy remains complete, coherent, and aligned with the photography-experience positioning.
- The story's final period is visibly present; no prompt leakage, generic rental language, substitution, truncation, or accidental duplication is visible in the supplied regression surfaces.
- Status: **passed**.

## Required Fidelity Surfaces — Pass 5

### Fonts and typography

- Cormorant Garamond remains the correct display family, with Manrope retained for supporting copy, labels, navigation, inclusions, captions, and CTAs. Weight, case, tracking, and line-height preserve the approved editorial hierarchy.
- The hero display now fits and remains complete. Downstream headings retain desktop/tablet minimum sizes (`44–48px` for several section titles and `40px` for event titles) while some are constrained to `9–11ch`; at a `305px` client these combinations create scroll widths from `242px` to `299px`, with event titles up to `274px` inside `172px` boxes.
- Status: **blocked by the grouped P2 small-phone display-heading sizing/measure mismatch**.

### Spacing and layout rhythm

- The mobile sections remain visually airy, single-column, and aligned to the intended 24px inset. Photography, inclusion spacing, CTA hierarchy, and the warm light/dark section cadence remain calm.
- The page-wide scrollbar is visible in all six captures, and the document remains `17px` wider than its client. This is not a deliberate full-bleed treatment because the overflow follows text min-content widths and affects `#top`/`main`.
- Status: **blocked by page-wide horizontal overflow at 320px**.

### Colors and visual tokens

- Off-white, Warm Ivory, Ebony/Dark Walnut, Soft Brown, and restrained brass details remain consistent across light and dark sections. The package retains the single intentional dark band, and the hero veil remains warm rather than overly cinematic.
- No pastel reference colors, vivid gradients, excessive gold, new shadows, or semantic-token drift are visible.
- Status: **passed for the pass-5 regression scope**.

### Image quality and asset fidelity

- The booth, curtain portrait, print-strip detail, and gallery photographs remain real editorial assets with coherent warm crops and adequate sharpness after normalization. Image priority is preserved on mobile.
- No placeholder, CSS drawing, handcrafted SVG, emoji, compression halo, or unrelated party-stock replacement is visible.
- Status: **passed for the pass-5 regression scope**.

### Copy and content

- The approved hero copy, signature-package title/price/duration/inclusions, gallery title/captions, and booking labels visible in the supplied states remain coherent and complete. The exact story, event, final-CTA, and inquiry headings are represented by supplied browser element metrics even where a scrolled capture shows adjacent content rather than the title itself.
- No truncation, prompt leakage, generic supplier language, or content substitution is visible. The problem is geometric overflow, not missing copy.
- Status: **passed for content completeness; typography/layout remains blocked separately**.

## Required Fidelity Surfaces — Pass 4

### Fonts and typography

- Cormorant Garamond remains visually consistent for the hero display, while Manrope remains coherent for navigation, supporting copy, and CTAs. Weight, scale, line-height, tracking, antialiasing, and hierarchy remain aligned with the editorial system.
- `PHOTOGRAPHS,` now fits its own measured box at all three widths (`327/327`, `312/312`, and `272/272`), with a visually comfortable gutter. `DEVELOPED DIFFERENTLY.` wraps completely and without truncation.
- Status: **passed for the pass-4 mobile-hero regression scope**.

### Spacing and layout rhythm

- At 390px and 375px, headline, support copy, action stack, 24px gutters, button dimensions, and hero image crop retain the intended calm vertical rhythm.
- At 320px, the root remains `320px` wide against a `305px` client because of `html { min-width: 320px; }`, producing `322px` document scroll width and a visible horizontal scrollbar. This is a viewport/container sizing failure, not an intentional full-bleed treatment.
- Status: **blocked by the remaining P2 root-width overflow**.

### Colors and visual tokens

- The hero preserves the approved warm booth photograph, Off-white display/support copy, Ebony booking action, and restrained antique-metal eyebrow/rule. The veil remains warm and luminous rather than overly dark.
- No pastel taxonomy, vivid gradient, excessive gold, shadow drift, or semantic-token regression is visible.
- Status: **passed for the pass-4 mobile-hero regression scope**.

### Image quality and asset fidelity

- The real booth image remains sharp and coherent at every tested width, with a responsive crop that retains the enclosure, curtain, and tactile materials.
- No placeholder, CSS drawing, handcrafted SVG, compression artifact, masking halo, or party-stock replacement was introduced.
- Status: **passed for the pass-4 mobile-hero regression scope**.

### Copy and content

- The approved headline, supporting sentence, `BOOK FOTOHVN`, and `EXPLORE THE EXPERIENCE` labels are present and complete at all three widths.
- The second-line wrap preserves the approved meaning, and no truncation or prompt leakage is visible.
- Status: **passed for the pass-4 mobile-hero regression scope**.

## Required Fidelity Surfaces — Pass 3

### Fonts and typography

- Cormorant Garamond remains visually consistent for the display headline, with Manrope retained for navigation, support copy, and CTAs. Weight, line-height, letter spacing, and hierarchy remain coherent with the approved editorial system.
- `DEVELOPED DIFFERENTLY.` now wraps naturally and remains complete at every tested width; the pass-2 second-line blocker is closed.
- `PHOTOGRAPHS,` remains 8–10px wider than its content box. The text is visible at 390px and 375px but optically crowds the right gutter; at 320px it participates in the page-width failure.
- Status: **blocked by the remaining P2 first-line scale/track mismatch**.

### Spacing and layout rhythm

- The left content gutter remains approximately 24px, action spacing stays calm, and the supporting copy and CTA stack retain the intended editorial rhythm.
- The first line intrudes 8–10px into the intended right gutter at all three widths. At 320px the hero content remains 320px wide against a 305px document client, producing 17px of document-level overflow and a visible horizontal scrollbar.
- The primary CTA remains full-width within the content area; the editorial CTA remains visually secondary and 44px high.
- Status: **blocked by right-gutter loss and 320px document overflow**.

### Colors and visual tokens

- The latest captures preserve the approved warm booth photograph, Off-white headline/support copy, Ebony booking action, and quiet antique-metal eyebrow/rule. The image veil remains luminous rather than overly cinematic.
- No pastel taxonomy, vivid gradient, excessive gold, new shadow, or semantic-token drift is visible in the pass-3 hero scope. Previously verified contrast values remain applicable because the tokens and hero treatment are unchanged.
- Status: **passed for the pass-3 mobile-hero regression scope**.

### Image quality and asset fidelity

- The booth photograph remains the correct real image asset, with a coherent warm crop, visible curtain/booth material, and adequate sharpness across all three panels.
- No placeholder, CSS drawing, custom SVG substitute, compression artifact, masking halo, or unrelated party imagery was introduced.
- Status: **passed for the pass-3 mobile-hero regression scope**.

### Copy and content

- The approved headline, supporting sentence, `BOOK FOTOHVN`, and `EXPLORE THE EXPERIENCE` labels are present and complete at all three widths.
- The second line’s new wrap preserves meaning and does not introduce truncation. The remaining issue is geometric overflow of the first line, not missing copy.
- Status: **passed for copy completeness; typography/layout remains blocked separately**.

## Required Fidelity Surfaces — Pass 2 (Preserved)

### Fonts and typography

- Cormorant Garamond remains assigned to display copy and Manrope to supporting/interface copy. Optical weight, hierarchy, and line-height remain consistent.
- The 320px display size of 39.2px is credible; the blocker is nowrap/min-content behavior. The proposed scale preserves the first approved line while allowing the longer second line to wrap.
- Status: **blocked by the P2 headline crop**.

### Spacing and layout rhythm

- No regression is visible in accepted desktop compositions or the prior looks, package, gallery, story, inquiry, and menu states.
- The 44px editorial CTA target is fixed at all three widths.
- The hero grid item still expands beyond the viewport, making the 24px mobile gutter unreliable.
- Status: **blocked by min-content sizing**.

### Colors and visual tokens

- The hero retains the approved warm photograph, Off-white text, Ebony action, and restrained antique-metal detail. No pastel taxonomy, gradient, excessive gold, or contrast regression appears.
- Previously checked contrast remains: Ebony/Off-white `16.30:1`; Soft Brown/Off-white `5.33:1`; Off-white/Dark Walnut `14.73:1`; Antique Metal/Dark Walnut `5.81:1`.
- Status: **passed for pass-2 regression scope**.

### Image quality and asset fidelity

- The booth photograph remains sharp and coherent across all normalized panels. Responsive crops preserve the booth/curtain material story.
- No placeholder, CSS drawing, handcrafted SVG, compression artifact, masking halo, or party-stock imagery was introduced.
- Status: **passed for pass-2 regression scope**.

### Copy and content

- Required brand, supporting copy, and CTA labels remain present.
- `PHOTOGRAPHS,` is readable; `DEVELOPED DIFFERENTLY.` is incomplete at 390px, 375px, and 320px.
- Status: **blocked by visible truncation of approved copy**.

## Accessibility, Responsiveness, and Interactions

- Pass 1: primary anchors worked; look tabs changed state; Escape closed the mobile menu; no console warnings/errors.
- Pass 2: `.editorialLink` is exactly 44px high at all widths; no console warnings/errors.
- Pass 3: the secondary CTA remains 44px high and no console warnings/errors were reported. The complete second headline line is now readable, but the 320px horizontal scrollbar is a responsive/text-zoom risk and remains blocking.
- Pass 4: the secondary CTA remains exactly 44px high, both headline spans are complete, the first span now fits its measured box at all widths, and no console warnings/errors were reported. The 320px document scrollbar remains a two-dimensional scrolling and text-zoom risk, so responsiveness is still blocked.
- Pass 5: the root-width fix allows the hero to fit its `305px` client and preserves the `44px` CTA, complete headline, and 24px text gutters. Downstream display headings still widen the document to `322px`, so two-dimensional scrolling and text-zoom resilience remain blocked. No fresh console or interaction run was supplied in this review-only pass.
- Pass 6: document and audited heading widths now fit at 320px, 375px, and 390px; the 44px secondary CTA, complete hero copy, 24px gutters, and natural heading wraps remain intact. The supplied post-fix browser run reports no console warnings or errors. Existing interaction evidence remains valid: primary anchors work, look tabs change state, and Escape closes the mobile menu.
- Semantic landmarks/headings, skip link, labelled inquiry controls, purposeful alt text, tab roles/selection, arrow/Home/End navigation, focus rules, and reduced-motion handling remain accepted from pass 1.
- Residual non-blocking test gaps: no fresh 200% text-zoom, screen-reader announcement, full inquiry validation/submission, or cross-browser run was supplied for pass 6. These remain recommended verification beyond the current visual-regression acceptance scope.

## Open Questions

- None. Source authority and intended mobile behavior are explicit in `DESIGN.md`.

## Comparison History

| Iteration | Evidence | Findings | Fixes made | Post-fix evidence | Result |
|---|---|---|---|---|---|
| 1 | Pass-1 full/focused boards | P2 headline clipping; P2 secondary CTA below 44px | None in review-only pass | Pending | Blocked |
| 2 | `website/design-qa/comparison-pass-2-mobile-hero-normalized.png`; 390px/375px/320px browser geometry | P2 headline/min-content clipping remains; CTA finding closed | Heading changed to `width: 100%`, `max-width: none`, `clamp(2.45rem, 12.2vw, 4.25rem)`; CTA gained 44px target | Three normalized captures; CTA is 44px; heading still exceeds visible track | Blocked |
| 3 | `website/design-qa/comparison-pass-3-mobile-hero-normalized.png`; latest 390px/375px/320px element and document geometry | P2 second-line clipping closed; P2 first-line 8–10px track overflow remains; P2 320px document overflow visible | Small-phone grid changed to `minmax(0, 1fr)`; hero content/heading received `min-width: 0` and bounded width; only first span remains nowrap; second span may wrap; scale changed to `clamp(2.45rem, 12vw, 2.925rem)` | Complete second line and 44px CTA at all widths; first line still crowds right gutter; 320px capture shows horizontal scrollbar and `322px` document scroll versus `305px` client | Blocked |
| 4 | `website/design-qa/comparison-pass-4-mobile-hero-normalized.png`; newest 390px/375px/320px document, heading, font-size, CTA, and console metrics | P2 first-line track overflow closed; P2 320px document overflow remains and is traced to the root `min-width: 320px` | Small-phone hero/content sizing was bounded; display scale changed to `clamp(2.2rem, 11vw, 2.8rem)` | First span fits its box at all widths; second line is complete; CTA is 44px; 390/375 documents fit; 320 remains `305/322` with a visible scrollbar | Blocked |
| 5 | `website/design-qa/comparison-pass-5-small-phone-overflow.png`; six latest 320px browser states and downstream element geometry | Root cause closed; grouped P2 document overflow remains from restrictive heading measures plus large small-phone display minima | Root `min-width: 320px` removed before this review; no implementation edits in pass 5 | Hero is fully fixed at `305/305` and `257/257`; package, experience, events, story, final CTA, and inquiry headings still exceed their boxes; document remains `305/322` with a visible scrollbar | Blocked |
| 6 | `website/design-qa/comparison-pass-6-final-mobile-regression.png`; current hero at 320px/375px/390px, five downstream 320px surfaces, and final geometry | No actionable P0/P1/P2 remains; story title's isolated 4px local scroll-width delta is accepted as harmless serif overhang because copy/gutters remain complete and the document does not widen | Small-phone display headings were allowed to use the full mobile track and their minima were reduced without changing the approved family, hierarchy, or natural word wrapping | Document is `305/305`, `360/360`, and `375/375`; hero and all audited titles fit their tracks except the harmless story local metric; no horizontal scrollbar; CTA remains 44px; no console errors | Passed |

## Implementation Checklist

1. Preserve the current small-phone heading bounds and measured type minima; they close the page-wide overflow without flattening the editorial hierarchy.
2. Preserve the pass-4 hero sizing rules, 24px mobile gutters, natural word wrapping, and 44px secondary CTA target.
3. Keep the pass-6 normalized board and all prior comparison boards with the handoff evidence.
4. Treat future image-library expansion and deeper assistive-technology/form-state testing as follow-up work, not as reasons to reopen the resolved responsive P2 without new evidence.

## Follow-up Polish

- [P3] Replace repeated event/gallery photography with event-specific approved FOTOHVN images as the real library grows.

## Residual Test Gaps

- Fresh 200% text-zoom and browser zoom resilience.
- Screen-reader announcement and full keyboard regression across the look selector, mobile menu, and inquiry form.
- Inquiry validation, submission, success, and error states.
- Cross-browser visual regression outside the browser used for the supplied captures.

final result: passed

No actionable P0, P1, or P2 finding remains. The only visual follow-up is the non-blocking P3 asset-reuse limitation; the listed test gaps are recommended verification beyond the supplied evidence.
