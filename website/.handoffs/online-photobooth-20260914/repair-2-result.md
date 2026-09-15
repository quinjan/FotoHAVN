# Filename compatibility repair result

Completed 2026-09-14. Scope: the automatic result identity in `src/components/onlinePhotobooth/download.ts` and filename regressions in `scripts/online-photobooth-gesture.test.mjs`.

## Finding and change

The default `crypto.randomUUID()` argument threw before a preview could be stored when the browser exposed `crypto` without `randomUUID`, or exposed no `crypto` global. Existing filename tests always supplied a result ID and therefore bypassed this path.

The helper now detects `globalThis.crypto` and a callable `randomUUID` safely. It preserves that API's receiver when available. Otherwise it uses an increasing local serial for filename bookkeeping, encoded at the start of the existing eight-character hexadecimal suffix. Multiple results in the same page session at the same timestamp therefore have distinct names. The fallback is not a security token and requires no network access or photograph uploads.

The filename pattern, layout suffix removal, timestamp formatting, and explicitly supplied result IDs remain unchanged. `useComposition.ts` still generates the name once when it stores the rendered blob; repeated downloads of that blob retain the same name. No changes were made to composition, media, UI, styles, assets, manifests, or runtime management.

## Verification

- Before the production change, the focused script ran 13 tests: 11 passed and the two new missing-API regressions failed with `TypeError: crypto.randomUUID is not a function` and `ReferenceError: crypto is not defined` respectively.
- After the change, `node scripts/online-photobooth-gesture.test.mjs` passed all 13 tests. Each missing-API case generates 256 filenames at one fixed timestamp, checks the existing filename format, and verifies every name is distinct. A further regression checks the available UUID receiver and preservation of an explicit stable result identity.
- `node node_modules/eslint/bin/eslint.js` passed repository lint.
- `node node_modules/typescript/bin/tsc --noEmit --pretty false --incremental false` passed the project typecheck.

All commands ran from `website/`. The initial `node --test` invocation could not spawn a test subprocess in this sandbox (`EPERM`); invoking the same `node:test` script directly ran the actual tests and returned the expected failing and passing exit codes. No test was skipped.

Browser review and the build remain owned by the coordinating task; neither was run for this bounded non-UI repair. The independent conformance report was not modified.
