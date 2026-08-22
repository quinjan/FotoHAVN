---
status: accepted
---

# Deploy the website as an immutable container

FotoHAVN staging deployments use a manually dispatched GitHub Actions workflow that always builds the current `main` revision on a GitHub-hosted runner, publishes a public GHCR image identified by commit SHA and digest, and deploys that digest through a dedicated restricted `fotohavn-deploy` SSH identity. The VPS runs the standalone Next.js image as an isolated, resource-limited Compose service behind the existing Caddy container at `http://159.223.47.227/fotohvn`; a separately reviewed bootstrap creates the forced deployment command, shared proxy network, imported Caddy route, and server directories.

Deployments are serialized, keep SSH secrets in a reviewer-free `production` environment, require build and container gates, and restore the previous digest if internal or public health checks fail. The raw-IP route is temporary, HTTP-only, and `noindex`; it preserves the `/fotohvn` base path, returns `404` at the IP root, does not change the inquiry flow, has no uptime objective, and does not make pending OS maintenance a deployment gate.

## Considered options

- Building source on the VPS was rejected because the shared host has one vCPU, 1.9 GiB RAM, no swap, and no Node.js installation.
- A static export or host-level standalone bundle was rejected in favor of the VPS's existing Docker operating model and Next.js image optimization.
- A self-hosted Actions runner was rejected because it would add persistent GitHub execution privileges to a shared public-repository host.
- A second public proxy or direct host port was rejected because Caddy already owns ports 80 and 443.

## Consequences

- `website/` must enable standalone output and the `/fotohvn` base path, and its public image paths and staging metadata must be base-path-aware.
- Routine releases replace only FotoHAVN; the PhotoBIZ, PostgreSQL, Redis, Jellyfin, File Browser, and Caddy services are not restarted.
- Moving from raw-IP staging to a production hostname requires a deliberate rebuild and replacement of the temporary routing and metadata configuration.
