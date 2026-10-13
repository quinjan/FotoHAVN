# FotoHAVN agent guide

Route the task before exploring. Load only the branches that apply, then use the completion rules below.

## Task routes

| Trigger | Read before acting |
| --- | --- |
| Domain language or product behavior | `docs/agents/domain.md`, beginning with its `CONTEXT-MAP.md` route |
| Brand, visual identity, or any FOTOHAVN-branded material (decks, posters, print, social) | `brand/README.md` |
| Desktop app or anything under `app/` | `app/AGENTS.md` |
| Website or online booth under `website/` | `website/AGENTS.md` |
| Website analytics, consent, or privacy | `website/AGENTS.md` and `website/docs/website-analytics.md` |
| Production deployment, `deploy/`, or the deployment workflow | `deploy/fotohavn/AGENTS.md` |
| Marketing video or `marketing/reels/` | `marketing/reels/AGENTS.md` |
| GitHub issue, PRD, or issue update | `docs/agents/issue-tracker.md` |
| Issue triage or label changes | `docs/agents/triage-labels.md` in addition to the issue-tracker guide |

Read every accepted ADR that touches the affected behavior. Surface conflicts instead of silently overriding a decision.

## Authority and evidence

- Treat code, configuration, package scripts, and command help as the executable source of truth.
- Treat `CONTEXT*.md`, accepted ADRs, `brand/`, `website/DESIGN.md`, and `app/docs/design-system/` as normative within the scopes they declare. `brand/` is the shared brand system; `website/DESIGN.md` interprets it, and the live website wins when website design docs drift.
- Treat `artifacts/`, `output/`, `.handoffs/`, `design-qa.md`, screenshots, `brand/references/`, and review records as evidence or history, not as current authority.

## Completion

- Run the scoped verification entry point named by the applicable agent guide.
- Validate visual or interaction changes in a rendered application at the required viewport or window states.
- Report checks actually run, their results, and every remaining hardware, production, or environment limit.
