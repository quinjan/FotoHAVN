---
status: accepted
supersedes: 0002-deploy-website-as-an-immutable-container.md
---

# Host the website on Vercel

The FotoHAVN website is hosted on the Vercel project `fotohavn` (Hobby account `quinjanrobertocampo-9778`), linked to `quinjan/FotoHAVN` with root directory `website/` and functions in `sin1`. Pushes to `main` deploy to production; other branches produce preview deployments. `NEXT_PUBLIC_CLARITY_PROJECT_ID` is a production project environment variable.

Spaceship remains the authoritative DNS provider. The apex points to Vercel's A record, `www` is a CNAME to Vercel and redirects to the apex in the Vercel project's domain settings, and legacy `/fotohavn` paths redirect permanently through `website/next.config.ts`.

## Considered options

- Keeping the immutable container on the shared Droplet (ADR 0002) was replaced to remove the website from the one-vCPU host it shares with PhotoBIZ and to drop the custom SSH, GHCR, and Caddy release path.

## Consequences

- The DigitalOcean FotoHAVN container, Caddy route, and `deploy-fotohavn-staging.yml` workflow remain only as a rollback path; rolling back means pointing the Spaceship apex A record back to `159.223.47.227`. Decommissioning them is separate work.
- The raw-IP `http://159.223.47.227/fotohavn` compatibility route is no longer part of the public topology.
- `output: "standalone"` is applied only outside Vercel (`process.env.VERCEL` unset) so the rollback image still builds; Vercel's build adapter fails with standalone output.
