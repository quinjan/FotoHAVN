# FOTOHVN Back-to-Top Ideation Brief

## Scope

Design only. Do not edit production UI. Propose a back-to-top control for the existing long-form FOTOHVN marketing website. The user will select a visual direction before implementation.

## User outcome

After scrolling through the long photography-led page, a visitor can return to the page top quickly without the control competing with booking or inquiry actions.

## Local authority

- `website/DESIGN.md`
- `website/tokens.json`
- `website/variables.css`
- `website/theme.css`

Local FOTOHVN direction overrides generic gpt-taste page styling.

## Design language

- Warm ivory and off-white paper surfaces; ebony and dark walnut for high-emphasis controls; muted brass only as a restrained detail.
- Editorial Cormorant Garamond paired with Manrope interface type.
- Photography-first, calm, tactile, refined, nostalgic, and understated.
- Hairline borders, 2-4px radii for non-circular controls, minimal elevation, 150-240ms feedback.
- Avoid badges, novelty symbols, oversized pills, excessive gold, shadows, looping animation, bouncing, or anything resembling a floating social/chat widget.

## Existing layout constraints

- Sticky 72px header.
- Long page with light paper sections, photographic sections, a warm-ivory inquiry form, and an ebony footer.
- Existing primary actions are rectangular uppercase controls.
- Desktop reference viewport: 1440x1000.
- Mobile reference viewport: 390x844; minimum QA width will be 320px.
- The control should appear only after meaningful scroll distance, respect reduced motion, be at least 44x44px, expose an explicit accessible name, and avoid covering form actions or footer links.

## Visual references

- `website/design-qa/responsive-qa-pass4-desktop-1440x1000-top.png`
- `website/design-qa/responsive-qa-pass4-mobile-390x844-full.png`
- Live preview verified at desktop and mobile against the current source.

## Consultation request

Using gpt-taste as a critical taste lens, propose three genuinely distinct component directions. For each, specify placement, shape, visual treatment, visibility behavior, hover/focus/motion, mobile adaptation, footer collision strategy, and why it fits FOTOHVN. Call out the strongest recommendation and reject any direction that reads as a generic floating action button or decorative badge.
