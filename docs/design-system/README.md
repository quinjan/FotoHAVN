# FotoHAVN Design Contract

Version: `1.0.0`

Status: Approval candidate off the default branch; Approved only when this exact contract commit is merged to the default branch by `quinjan` and tagged `design-v1.0.0`

Canonical theme: Light

Canonical frame: 1280 × 720 effective pixels

This package is the normative visual, interaction, responsive, language, and accessibility contract for FotoHAVN. It refines the existing calm, monochrome, Inter-based identity; it is not a rebrand. WinUI remains the behavioral implementation, but a UI-affecting change must not knowingly merge while WinUI and this contract disagree.

## Contract contents

- [Foundations](foundations.md) — principles, visual foundations, responsive modes, and language/accessibility rules.
- [Machine-readable tokens](tokens.json) — primitive values and semantic aliases.
- [Components](components/README.md) — the exact 17-family v1 inventory and one normative specification per family.
- [Patterns](patterns/README.md) — cross-component interaction contracts.
- [Reference states](reference-states/README.md) — the risk-based operator and Guest Cycle review matrix.
- [WinUI mapping](winui-mapping.yaml) — semantic IDs, intended WinUI seams, accessibility obligations, and verification seams.
- [Decision provenance](provenance.md) — the GitHub decisions consolidated into this package.
- [Changelog](CHANGELOG.md) — design-contract SemVer history.

## Authority and lifecycle

1. Work begins as `Draft` or an approval candidate.
2. A contract version becomes `Approved` when `quinjan` merges it to the default branch and creates a matching `design-vMAJOR.MINOR.PATCH` tag.
3. A mapped item becomes `Verified in WinUI` only after its required visual, interaction, keyboard, Narrator, touch, High Contrast, reduced-motion, and responsive checks pass.

The files in this directory hold the current normative answer. GitHub issues hold rationale and history. Prototype branches and audit screenshots are evidence, not competing sources of truth. Figma may be used as a non-authoritative exploratory canvas, but approval and implementation may not depend on paid Figma capabilities.

## Change control

- **Major:** breaking semantic rename, removal, or behavior-contract change.
- **Minor:** new token, component, variant, or approved state.
- **Patch:** correction that preserves identity, mappings, and usage.
- Every token must have an approved consumer. One-off visual values require a documented functional or optical reason and a semantic consumer.
- An urgent code-first correction may merge only when delay would harm an Active Event or block operation. Record the drift in a GitHub issue and reconcile this contract before the next UI-affecting release.

## v1 acceptance gate

Approval is blocked unless:

- all tokens have names, values, and consumers;
- all 17 component families expose only meaningful properties and states;
- the reference matrix covers every required canonical state and responsive risk case;
- no state relies on hover, color, precision tapping, dragging, or animation alone;
- targets, focus, contrast, announcements, responsive behavior, and WinUI seams are explicitly annotated;
- there are no known clipping, overlap, horizontal-scroll, hidden-content, unreachable-focus, media-distortion, or guest-stage vertical-scroll gaps;
- Event identity, modal safety, button-local progress, the Exit Event hold, preserved-progress Operator Assistance, and Guest Cycle announcements match their resolved decisions.

Formal accessibility certification, dark mode, runtime theming, Event-specific branding, and production WinUI changes are outside v1 contract scope.
