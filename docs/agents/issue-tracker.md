# GitHub issue tracker

GitHub Issues is the repository's issue and PRD system. Run `gh` from this clone so the remote selects the repository.

## Read

- Fetch a ticket and its discussion with `gh issue view <number> --comments --json number,title,body,state,labels,assignees,comments`.
- List a scoped queue with `gh issue list --state <state> --label <label> --json number,title,state,labels,assignees`.
- Resolve a bare `#<number>` as a PR first with `gh pr view <number>` and fall back to `gh issue view <number>` because GitHub shares the number space.

## Write

- Publish a requested PRD or ticket with `gh issue create`.
- Add progress or evidence with `gh issue comment`.
- Change labels or assignees with `gh issue edit`.
- Close completed work with `gh issue close --comment <summary>`.

Use a temporary body file for multiline content in PowerShell, then pass it with `--body-file`. Keep secrets, local paths, and private customer data out of issue bodies and comments.

## Branches

- Triage: read `triage-labels.md` before changing labels.
- Wayfinder maps, children, dependencies, or frontier selection: read `wayfinder.md`.
- Pull requests are review and delivery surfaces, not feature-request intake for this repository.

An issue operation is complete when the issue or PR number, resulting state, applied labels, and link are reported.
