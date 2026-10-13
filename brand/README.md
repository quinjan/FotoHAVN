# FOTOHAVN brand system

> Photographs, developed differently.

This directory is the shared brand system for everything FOTOHAVN makes: the website and online booth, the desktop booth app, social reels, presentations, posters, print collateral, and future work.

It describes the brand itself and does not depend on any tool. It contains no website code, no WinUI resources, and no Remotion components. Each product or medium writes an **interpretation** of it in its own tooling.

## Contents

| Resource | Answers |
| --- | --- |
| [foundation.md](foundation.md) | What FOTOHAVN is, what it promises, and how it should and should not feel |
| [voice.md](voice.md) | How FOTOHAVN writes: tone, vocabulary, casing, and approved lines |
| [color.md](color.md) | Palette meaning, proportions, contrast pairs, and screen and print color |
| [typography.md](typography.md) | Typeface pairing, type roles, casing, and fallbacks |
| [layout.md](layout.md) | Space, grids, alignment, shape, and surface levels |
| [photography.md](photography.md) | Subjects, treatment, cropping, and the seven photographic looks |
| [logo.md](logo.md) | Wordmark, booth-portal mark, clear space, and misuse |
| [iconography.md](iconography.md) | When icons are allowed, which is rarely |
| [motion.md](motion.md) | Timing, easing, permitted movement, and reduced motion |
| [accessibility.md](accessibility.md) | The minimum legibility and inclusion every medium must meet |
| [components.md](components.md) | Cross-medium building blocks such as actions, labels, rules, and frames |
| [mediums.md](mediums.md) | How the brand adapts to web, app, video, slides, print, and social |
| [tokens/](tokens/README.md) | Machine-readable values (DTCG) with HEX, RGB, and CMYK |
| [assets/](assets/README.md) | Master brand marks |
| [references/](references/README.md) | Approved imagery, look comparisons, do and don't boards, mood, and finished applications |

## Authority

This system is derived from the **live FOTOHAVN website**, which is the reference implementation of the brand. When sources disagree, apply this order:

1. An approved FOTOHAVN brand or content brief.
2. This directory. Prose documents and `tokens/tokens.json` must agree; if they don't, fix them together.
3. An interpretation, within the scope it declares.
4. References, research, generated concepts, and screenshots. These are evidence, not authority.

**Live website over website docs.** When the website's own design documents (for example, `website/DESIGN.md`) drift from what the live website actually renders, the live website is the design reference. Update this directory from the implementation, and record the stale document as drift. If this directory and the live website disagree, raise it: either the website has drifted from an intentional brand change, or this directory needs updating.

Product language comes from [`CONTEXT.md`](../CONTEXT.md) and [`CONTEXT-MAP.md`](../CONTEXT-MAP.md). This directory uses those terms and does not redefine them.

The brand system has no version numbers. The default branch is the current brand.

## Interpretation contract

An interpretation is a product's or medium's implementation of this brand. It **must inherit**:

- the foundation, the voice rules, and the approved brand line;
- the nine palette colors and their roles (no new hues for decoration);
- the type pairing, the role hierarchy, and the casing rules;
- the photography direction and the names of the seven photographic looks;
- the logo rules;
- the accessibility baseline;
- the motion prohibitions and reduced-motion behavior.

It **may adapt**:

- sizes, spacing, and grids for its medium, scaled by role;
- medium-specific tokens such as breakpoints, frame sizes, safe areas, bleed, and component dimensions;
- page or scene architecture, specific copy within the voice rules, and specific components;
- tool-specific file formats, such as CSS variables, XAML resources, slide masters, or print templates.

It **must declare**:

- a "Brand interpretation" section near the top of its design document that links here;
- every deliberate departure from this directory, with the reason, in that section.

### Current interpretations

| Interpretation | Document | Status |
| --- | --- | --- |
| Website and online booth | [`website/DESIGN.md`](../website/DESIGN.md) | Interprets this system |
| Marketing reels | [`marketing/reels/AGENTS.md`](../marketing/reels/AGENTS.md) | Each reel interprets this system directly |
| Desktop booth app | [`app/docs/design-system/`](../app/docs/design-system/README.md) | **Not yet aligned.** It is an independent monochrome Inter contract that predates this system. A future app refactor will reconcile it. Until then, the app contract remains authoritative for app UI. |

## Changing the brand

- Edit the prose document and `tokens/tokens.json` in the same change.
- Update affected interpretations in the same change, or record the drift in each one's "Brand interpretation" section.
- Replace a reference image only with approved FOTOHAVN material, or with a generated board that is labeled as generated.
