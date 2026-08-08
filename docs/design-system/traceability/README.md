# FotoHAVN design-v1.0.1 traceability handoff

This nonvisual patch makes the approved `design-v1.0.0` contract auditable. It does not change visual or behavioral semantics, and it inherits the 103 approved target hashes anchored at tag `design-v1.0.0` and commit `1a20bc6f46f8d724683f8c4b47c359379fc7371b`.

## Contents

- `mapping.json`: mapping schema v2 at cardinalities 91 / 17 / 8 / 71 / 32.
- `scenarios/*.json`: one catalog per production surface, totaling 71 canonical Given/When/Then scenarios.
- `evidence-index.json`: resolution for acceptance, UI Automation, visual, injection, shared-pattern, and manual identifiers.
- `manual-procedures.md`: named procedures for checks that require human evidence.
- `waivers.json`: empty-by-default exception register.
- `manifest.json`: baseline anchor, artifact hashes, and inherited target hashes.

## Validate

From the repository root, run:

```powershell
node scripts/validate-design-traceability.mjs
node --test tests/design-traceability-validator.test.mjs
```

Validation rejects missing, duplicate, dangling, or extra mapping and evidence records, altered handoff artifacts, changed target hashes, and matrix/registry drift. Waivers must remain empty for this patch. Future waivers require a named scope, justification, approval, expiry, affected scenarios, and affected fixtures.
