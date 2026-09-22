---
status: accepted
---

# Deploy the website as an immutable container

FotoHAVN production deployments use a manually dispatched GitHub Actions workflow that always builds the current `main` revision on a GitHub-hosted runner, publishes a public GHCR image identified by commit SHA and digest, and deploys that digest through a dedicated restricted `fotohavn-deploy` SSH identity. The VPS runs the standalone Next.js image as an isolated, resource-limited Compose service behind the existing Caddy container at `https://fotohavn.com/`; a separately reviewed bootstrap creates the forced deployment command, shared proxy network, imported Caddy route, and server directories.

Deployments are serialized, keep SSH secrets in a reviewer-free `production` environment, require build and container gates, and restore the previous digest if internal or public health checks fail. The canonical apex is indexed and served over HTTPS, `www` redirects to the apex, and legacy public `/fotohavn` paths redirect to their root equivalents. The HTTP raw-IP `/fotohavn` route remains only as a compatibility boundary for restoring the last pre-domain image; the IP root continues to return `404`.

## Considered options

- Building source on the VPS was rejected because the shared host has one vCPU, 1.9 GiB RAM, no swap, and no Node.js installation.
- A static export or host-level standalone bundle was rejected in favor of the VPS's existing Docker operating model and Next.js image optimization.
- A self-hosted Actions runner was rejected because it would add persistent GitHub execution privileges to a shared public-repository host.
- A second public proxy or direct host port was rejected because Caddy already owns ports 80 and 443.

## Consequences

- `website/` must enable standalone output at the default empty base path, and its metadata must use `https://fotohavn.com` as the canonical origin.
- Routine releases replace only FotoHAVN; the PhotoBIZ, PostgreSQL, Redis, Jellyfin, File Browser, and Caddy services are not restarted.
- Spaceship remains the authoritative DNS provider with the apex pointed to the Droplet and `www` aliased to the apex.
- The raw-IP compatibility route must remain until the previous-image slot no longer contains a build compiled for `/fotohavn`.
