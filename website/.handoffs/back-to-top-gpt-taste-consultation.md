# FOTOHVN Back-to-Top — gpt-taste Consultation

## Taste position

The control should read as quiet editorial page furniture, not as an app-style floating action button. The current page already gets its character from large Cormorant Garamond typography, warm paper fields, dark walnut photography, disciplined rectangular actions, and extremely long mobile storytelling. A back-to-top treatment should borrow the interface grammar—Manrope labels, ebony/off-white contrast, hairlines, 2–4px corners, no shadow—without introducing a new decorative motif.

The useful part of the gpt-taste lens here is insistence on deliberate placement, legible contrast, precise hover physics, and motion that responds to user intent. Its generic prescriptions for GSAP-heavy motion, looping animation, round floating controls, gradients, and ornamental effects are rejected because the local FOTOHVN authority explicitly calls for calm, tactile restraint. This component needs CSS-scale feedback, not a spectacle.

## 1. Editorial Corner Tab — recommended

**Placement.** On desktop, fix the control at the lower right but align it to the editorial grid rather than the raw viewport edge: `right: max(24px, calc((100vw - 1280px) / 2))`, with 24–32px bottom clearance. This puts it in the calm outside margin at the 1440px reference width and keeps it visually tied to the content edge. It must sit below the sticky 72px header and above browser safe-area insets. Do not place it over hero copy or photography before the user has scrolled.

**Shape.** Use a narrow vertical rectangle, approximately 52×64px on desktop, with a 4px control radius. Stack a thin, purpose-drawn upward arrow over the visible label `TOP`. This is intentionally not circular and not icon-only: the compact typeset label makes it read like an editorial tab rather than chat/social chrome.

**Tokens and visual treatment.** Use Ebony (`--color-ebony`) as the surface, Off-white (`--color-off-white`) for the arrow and label, a 1px Hairline-strength edge where needed, Manrope at 11–12px/600 with restrained uppercase tracking, and no shadow. Hover uses Dark Walnut (`--color-dark-walnut`). Muted Brass should be limited to an optional 1px divider between arrow and label; it should never become the fill.

**Visibility behavior.** Keep the control absent for the hero and first meaningful narrative beat. Reveal it only after the visitor has moved roughly one viewport or after the first post-hero section sentinel passes the sticky header—whichever is more robust in implementation. Render it only while actionable; when hidden, it must not remain in the tab order. Do not flash it on small scroll reversals.

**Interaction and motion.** Use the local 150ms feedback for hover/focus color changes and a 240ms opacity plus maximum 8px vertical entrance. On hover, the arrow may travel upward by only 2px; the tab itself stays still. Activation should use native smooth scrolling when allowed, then move focus to a real page-top target so keyboard and screen-reader users receive the same return. With `prefers-reduced-motion: reduce`, remove translation and use immediate scrolling/state change.

**Mobile adaptation.** At 390px and the required 320px minimum, reduce to approximately 48×56px, preserve both arrow and `TOP`, and use 16px right/bottom clearance plus `env(safe-area-inset-bottom)`. Do not stretch into a bottom pill or use a circular mobile-only variant. Because the mobile page is exceptionally long and content fills nearly the full measure, keep the component visually compact and disable it before form content begins.

**Form/footer collision.** Establish the inquiry section as a hard boundary. As its top approaches the control’s lower clearance, clamp the tab into a reserved utility position 24px above the inquiry section so it scrolls away with the page rather than overlapping radio options, inputs, textarea, or `START THE CONVERSATION`. On narrow screens, fading it out just before the inquiry enters the viewport is preferable to a complex clamp. It remains absent over the Ebony footer and its social/legal links. Recalculate the boundary with `ResizeObserver` rather than a one-time document-height estimate.

**Why it fits.** This direction gives the long page a genuinely useful persistent return without competing with `FIND A BOOTH`, `RENT FOTOHVN`, or inquiry actions. Its labelled rectangular silhouette echoes existing controls while its smaller vertical proportion clearly marks it as a utility.

## 2. Header-Integrated Return

**Placement.** Put a text-led `TOP` utility inside the existing sticky 72px navigation, in the quiet space before the right-side booking group. It should align to the navigation baseline and remain subordinate to `RENT FOTOHVN`; it must not replace or visually bracket the primary action.

**Shape.** Use a transparent 64×44px rectangular hit area with no enclosing pill. Pair the word `TOP` with a small upward arrow and a short underline/rule treatment. The hit area can be rectangular while the visible treatment remains a text link, matching the site’s editorial-link grammar.

**Tokens and visual treatment.** Use Manrope 11–12px/600 uppercase, Ebony on the scrolled Off-white header, Soft Brown for its resting secondary emphasis, and a 1px Muted Brass rule only for hover/current feedback. No filled background, shadow, badge, or additional icon container.

**Visibility behavior.** Reserve its width in the navigation layout to prevent link reflow, but reveal the label only after the first viewport or first content-section sentinel. A 150ms opacity change is sufficient because the sticky header already provides the containing transition. If the header is over photography before becoming opaque, keep the utility hidden rather than inventing another inverse state.

**Interaction and motion.** On hover/focus, move the arrow upward by 2px and extend the underline over 150–240ms. Use the same page-top focus transfer and reduced-motion behavior as Direction 1. The focus indicator is a 2px high-contrast ring with the system’s 3px offset; the subtle resting state cannot be allowed to become a subtle focus state.

**Mobile adaptation.** Preserve a 48×44px `TOP` target at the trailing edge of the mobile header only if the existing navigation can retain its logo and primary/menu affordance without compression. Never collapse it to an unexplained arrow. At 320px, hide this treatment if the header cannot keep at least 8–12px between independent controls; the footer/end return then remains the fallback.

**Form/footer collision.** Because the control lives inside the header, it never covers form fields, the submission action, or footer links. Keep the header’s stacking context unchanged and verify that opening any mobile navigation state either hides or safely includes the return action.

**Why it fits.** It is the quietest persistent solution and feels structurally native. Its weakness is hierarchy: the current desktop header already balances center navigation and two right-side actions, while mobile width is scarce. Adding another label may make the navigation feel more transactional than the photography-led page should.

## 3. Closing Colophon Link

**Placement.** Add an in-flow utility band at the end of the inquiry experience, immediately after the form action and before the Ebony footer. Align `RETURN TO TOP` to the form/footer grid edge, ideally right-aligned on desktop and left-aligned with the mobile content gutter.

**Shape.** Use a minimum 44px-high text-link target inside a 56–64px band, separated from surrounding content by a single hairline. There is no floating enclosure. The visible treatment is `RETURN TO TOP` plus a thin upward arrow, with the label carrying the meaning.

**Tokens and visual treatment.** Keep the band Warm Ivory or Off-white, use Ebony text, Soft Brown for quiet secondary copy if any, and a 1px Hairline border. Manrope 12px/600 uppercase with the approved tracking is sufficient. A short Muted Brass rule may precede the label; no filled button, shadow, or circular device is needed.

**Visibility behavior.** It is always present in document flow and becomes visible naturally when the visitor completes or reaches the inquiry section. No scroll listener, threshold, or entrance animation is required. This is the most stable behavior and creates no unexpected UI chrome.

**Interaction and motion.** Underline or extend the adjacent rule over 150ms and shift the arrow by at most 2px on hover. Preserve the 2px focus ring/3px offset and native/reduced-motion scroll rules. After returning, focus the top landmark rather than leaving focus near the bottom of the document.

**Mobile adaptation.** Keep the link full-measure within the 24px page gutters, with at least a 44px target and 16px separation from the form’s submit control and the footer’s first link. Do not make the band into a full-width dark CTA, which would compete with submission and resemble a second footer.

**Form/footer collision.** It cannot collide because it owns a reserved strip between the form and footer. The spacing must be explicit so validation messages or an expanding textarea cannot push the link into the footer without the band growing naturally.

**Why it fits.** It behaves like a restrained book colophon and is the most editorially pure option. Its tradeoff is functional: it helps only visitors who reach the end, so it does not solve quick return from the long middle of the mobile page.

## Recommendation

Choose **Editorial Corner Tab**. It best answers the long-page usability need while retaining FOTOHVN’s quiet rectangular interface language. Its success depends on three non-negotiables: retain the visible `TOP` label, delay appearance until a meaningful scroll threshold, and stop/fade it before the inquiry so it never overlays form or footer actions.

The Header-Integrated Return is the strongest fallback if the team wants zero content overlay, but it should proceed only after proving that the 320px header remains calm and uncompressed. The Closing Colophon Link is an excellent supplemental or low-JavaScript fallback, not the primary answer for mid-page return.

Explicitly reject a circular arrow-only control, pill-shaped floating button, brass medallion, drop-shadowed square, looping/bouncing arrow, scroll-progress ring, or any version that resembles a chat widget or decorative badge. Those treatments import a generic product-UI motif and conflict with the existing photography-first editorial system.

## Selection criteria for the next step

- Primary task access: available after meaningful scroll, not just at the page end.
- Booking hierarchy: visibly quieter than `FIND A BOOTH`, `RENT FOTOHVN`, and form submission.
- Collision proof: no overlap with inputs, validation messages, submit action, social links, or legal copy at 1440, 390, and 320px.
- Accessible operation: explicit visible label and accessible name such as `Back to top`; 44×44px minimum; strong focus; reliable focus transfer; reduced-motion support.
- Motion discipline: 150–240ms state feedback, at most 8px entrance travel and 2px arrow travel, with no bounce, loop, parallax, or GSAP dependency.
