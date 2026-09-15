# Responsive Editorial Lift implementation

2026-09-10. User explicitly requested implementation of the portrait treatment described in `../responsive-hero-audit/README.md`, without another mockup approval step. This supersedes the earlier static-mobile/tablet assumption, not the selected desktop hero.

Keep the closed curtain, corrected raised lightbox without timber wings, editorial color/type system, semantic copy/actions, later sections and separate 3D explorer. Use a portrait camera composition of the existing generated facade on phones/portrait tablets, with live text over cream linen. Desktop/landscape tablets keep the original wide asset. Source selection uses a native picture element, without swapping images after hydration.

Enable short native-scroll paper overlap on phones and portrait tablets. Phone travel: 32% of viewport, bounded to 180–300px; portrait tablet: 35%, bounded to 280–420px. Desktop retains previous 320–540px timing. The sheet starts at the actual hero bottom, not the taller destination bottom. Explore follows the same motion and focuses the heading. Reverse scroll, interruption, reduced motion and natural continuation remain.

Keep copy in normal layout over an absolutely positioned scene so enlarged text can expand the hero. If a short landscape viewport or enlarged content would make pinning conceal controls, retain readable natural flow. Reduced motion always uses natural flow/direct arrival. This is a safety fallback, not a blanket mobile/tablet default.

ImageGen built-in edit generated `portrait-source.png` from the approved exterior. `scripts/prepare-portrait-hero.mjs` compresses it and prepares seven responsive variants. Desktop asset is untouched. Final prompt: `asset-prompt.md`.

Verify 320×720, 390×844, 820×1180, 1180×820 and 1440×900 in the browser, midpoint/arrival, Explore/focus, reverse scroll, short landscape fallback, static asset selection, no overflow and console. Run hero/booth tests, lint, typecheck and production build. Preserve unrelated uncommitted work; no commit/push/deployment.
