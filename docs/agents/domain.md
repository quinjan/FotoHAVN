# Domain documentation

Use domain documentation to establish vocabulary and behavioral boundaries before exploring implementation details.

## Load

1. Read `CONTEXT-MAP.md` at the repository root.
2. Read `CONTEXT.md` and every scoped context selected by the map for the affected behavior.
3. Read each accepted ADR that touches the behavior or boundary being changed.

The context map is the routing source; context files are the vocabulary source; ADRs are the durable decision source.

## Apply

- Use defined terms in issues, hypotheses, code, tests, UI copy, and reports.
- Preserve distinctions listed in each context's `_Avoid_` line.
- Treat a missing concept as either vocabulary drift to correct or a domain gap to resolve explicitly.
- Surface an ADR conflict by naming the ADR and explaining the proposed departure.

## Update

- Put cross-product concepts in root `CONTEXT.md`.
- Put desktop concepts in `app/CONTEXT.md`, website concepts in `website/CONTEXT.md`, and new scope-specific concepts in the nearest scoped `CONTEXT.md`.
- Put cross-context distinctions and navigation in `CONTEXT-MAP.md`.
- Record durable implementation or operational choices as ADRs, not glossary entries.

Domain work is complete when every named concept follows the loaded contexts and every affected ADR is accounted for.

After changing an agent guide, context route, or context file, run `./scripts/verify-agent-docs.ps1`.
