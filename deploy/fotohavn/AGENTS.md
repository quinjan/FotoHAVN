# Website deployment work

Use this guide for the deployment bundle, production workflow, container packaging, Caddy integration, or a production release.

## Load

- Read `docs/adr/0002-deploy-website-as-an-immutable-container.md` for the accepted immutable-container topology and shared-host boundaries.
- Read `docs/fotohavn-bootstrap-runbook.md` for bootstrap or host-integration work.
- Read `../../.github/workflows/deploy-fotohavn-staging.yml` and the executable files in this directory for the release path.
- Read `../../website/docs/microsoft-clarity-production-runbook.md` when a release changes analytics, consent, masking, or Clarity configuration.

## Boundaries

- Verify current host, DNS, proxy, network, and container state before acting; discovery records are historical inputs.
- Keep routine releases scoped to FotoHAVN. Shared PhotoBIZ, database, media, and proxy services remain outside routine deployment ownership.
- Build and identify production images immutably, retain the previous digest, and keep rollback available until public verification passes.
- Keep secrets and private keys out of command output, logs, commits, and evidence.

## Verification

- Run `npm run check` from `website/` before changing release state.
- Run `bash deploy/fotohavn/test-deploy.sh` from the repository root in a compatible shell.
- Use the manually dispatched production workflow for a release; do not substitute an ad hoc host build.
- Verify internal health, public HTTPS, canonical and compatibility routes, the deployed digest, and rollback state.

Treat the shell scripts, Compose file, Caddy fragment, and forced command as one reviewed interface. Completion requires the release or dry-run result, immutable digest when deployed, public checks, rollback disposition, and every unverified live assumption.
