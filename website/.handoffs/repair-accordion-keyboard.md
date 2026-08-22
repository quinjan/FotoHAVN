# Accordion keyboard activation repair

Date: 2026-08-22  
Scope: `website/src/components/UpperExperience.tsx`, `handleAccordionKeyDown` only

## Change

- Added explicit handling for `Enter`, modern Space (`" "`), and legacy Space (`"Spacebar"`).
- Each activation key now calls `preventDefault()`, selects `accordionItems[currentIndex].id`, and returns immediately. This prevents the native synthesized button click from causing duplicate activation.
- Left the existing pointer `onClick`, ArrowLeft/ArrowRight/ArrowUp/ArrowDown wrapping, Home/End focus movement, trigger focus retention, native button and ARIA markup, and responsive/mobile structure unchanged.

## Verification

- `npm run lint` from `website/`: passed (exit code 0). ESLint emitted no findings; npm emitted only its existing warning about the unsupported user `email` config.
- `git diff --check -- src/components/UpperExperience.tsx` from `website/`: passed (exit code 0). Git emitted only the working-tree LF-to-CRLF notice.

## Residual risks

- Browser runtime behavior was not verified by this repair agent. Fresh gate agents must confirm Enter and Space update `aria-expanded`, reveal the selected panel, hide the prior panel, and retain focus in the desktop horizontal accordion at the required viewports.
- The legacy `Spacebar` branch is retained for older key-value compatibility; current browsers normally report Space as `" "`.

result: passed
