# Website hosting and deployment

Use this guide for production hosting, domains, environment variables, releases, or rollback of the FotoHAVN website.

## Load

- Read `docs/adr/0003-host-website-on-vercel.md` for the accepted Vercel topology.
- Read `../../website/docs/microsoft-clarity-production-runbook.md` when a release changes analytics, consent, masking, or Clarity configuration.
- `docs/adr/0002-deploy-website-as-an-immutable-container.md`, `docs/fotohavn-bootstrap-runbook.md`, `docs/digitalocean-vps-findings.md`, `docs/research/`, the shell scripts, `compose.yml`, `fotohavn.caddy`, and `.github/workflows/deploy-fotohavn-staging.yml` describe the retired DigitalOcean path. Use them only for DNS rollback or decommissioning.

## Topology

- Vercel project `fotohavn` (account `quinjanrobertocampo-9778`) is linked to `quinjan/FotoHAVN` with root directory `website/` and functions in `sin1`.
- Pushes to `main` deploy to production; other branches create preview deployments.
- `NEXT_PUBLIC_CLARITY_PROJECT_ID` is a production environment variable on the Vercel project.
- Spaceship is the authoritative DNS provider: apex A records `216.198.79.1` and `64.29.17.1`, `www` CNAME `cname.vercel-dns.com`. Vercel redirects `www` to the apex; `website/next.config.ts` redirects legacy `/fotohavn` paths.

## Boundaries

- Verify current Vercel project, deployment, and Spaceship DNS state before acting.
- The DigitalOcean Droplet also hosts PhotoBIZ, PostgreSQL, Redis, Jellyfin, File Browser, and Caddy. Do not touch them from website work.
- Keep secrets and tokens out of command output, logs, commits, and evidence.

## Verification

- Run `npm run check` from `website/` before releasing.
- Confirm the production deployment is `Ready` and built from the intended `main` commit.
- Verify public HTTPS for `/` and `/online`, 308 redirects for `/fotohavn`, `/fotohavn/*`, `http://`, and `www`, and the canonical `https://fotohavn.com`.

## Rollback

- Promote the previous production deployment in Vercel (instant rollback).
- For a host-level rollback, point the Spaceship apex A record back to `159.223.47.227` while the DigitalOcean container still runs.

Completion requires the deployment ID and commit, public checks, rollback disposition, and every unverified live assumption.
