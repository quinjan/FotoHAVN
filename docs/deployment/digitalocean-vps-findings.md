# DigitalOcean VPS deployment findings

## Snapshot

- **Observed:** 2026-08-22 04:03-04:09 UTC (2026-08-22 12:03-12:09 Asia/Manila)
- **Target:** DigitalOcean Droplet at `159.223.47.227`, hostname `photobiz-pilot-sgp1`
- **Purpose:** establish the current host constraints before designing a GitHub Actions deployment for the FotoHAVN website
- **Method:** authenticated, strictly read-only SSH inspection plus a read-only inspection of the local `website/` project
- **Mutations:** none on the VPS; no package installation, configuration change, service restart, file creation, deployment, or cleanup was performed

The private-key passphrase, key contents, environment values, and full sensitive configuration were not captured. The existing `/opt/photobiz/.env` was observed only as a mode-`0600` file and was not read.

## Executive findings

The Droplet can host the website, but it is already a small, shared production-like host. It has one vCPU, 1.9 GiB RAM, no swap, and seven running containers. Caddy already owns ports 80 and 443 for the existing PhotoBIZ stack. A FotoHAVN deployment must therefore coexist behind that proxy; it must not bind another process or container directly to host ports 80/443.

Docker and Compose are current and healthy, while Node.js is not installed on the host. The safest fit to investigate is to build and test off-host in GitHub Actions, publish an immutable image, then have a non-root deployment identity pull and restart only the FotoHAVN service. Building Next.js on this Droplet would add avoidable CPU, memory, and disk pressure.

The host needs maintenance before it becomes a deployment target: 34 packages were reported upgradable, a reboot is required, and the running kernel is older than the installed replacement. SSH is key-only and Fail2Ban is active, but direct root login and SSH forwarding are enabled. The public firewall also exposes Jellyfin and File Browser directly on ports 8096 and 8097.

## Host inventory

| Area | Observed state | Deployment implication |
| --- | --- | --- |
| Provider / region signal | DigitalOcean Droplet; hostname suffix `sgp1`; KVM | Treat this as a shared Singapore-region VPS, not a blank target. |
| OS | Ubuntu 24.04.4 LTS (Noble) | Supported base for Docker and systemd operations. |
| Kernel / architecture | Linux `6.8.0-124-generic`, `x86_64` / `amd64` | Standard Linux container images are suitable. A newer kernel is installed but not yet running. |
| CPU | 1 vCPU (`DO-Regular`) | Avoid building production images on the VPS; keep deploy-time work small. |
| Memory | 1.9 GiB total; about 748 MiB available at observation time | Add health checks and consider a container memory limit. Existing workloads, especially Jellyfin, can burst. |
| Swap | None | A build or concurrent workload spike can trigger OOM termination rather than spill to swap. |
| Root disk | 48 GiB; 28 GiB used; 20 GiB available; 58% used; inode use 3% | Capacity is adequate for a small image and limited rollback history, but image retention needs a policy. |
| Other storage | `/srv` uses about 16 GiB, mainly media; `/opt` uses about 34 MiB | Do not place the website under the media tree. A dedicated `/opt/fotohavn` path is the natural convention, but it does not exist yet. |
| Time | UTC; NTP active; clock synchronized | GitHub Actions and server logs can use UTC consistently. |
| Uptime | Booted 2026-06-22; about 60 days at inspection | The required reboot has not yet been taken. |

### Current container load

Docker reported eight containers total, seven running, eight images, about 3.24 GB of image data, and 4.25 GB of build cache (3.87 GB reported reclaimable). No cleanup was performed.

Approximate one-shot memory usage during inspection:

| Container | Purpose inferred from image/name | Memory |
| --- | --- | ---: |
| `jellyfin` | media server | 496 MiB |
| `photobiz-api-1` | PhotoBIZ API | 90 MiB |
| `photobiz-worker-1` | PhotoBIZ worker | 71 MiB |
| `photobiz-reverse-proxy-1` | Caddy | 50 MiB |
| `photobiz-postgres-1` | PostgreSQL | 30 MiB |
| `filebrowser` | file browser | 17 MiB |
| `photobiz-redis-1` | Redis | 4 MiB |

These are observations, not safe capacity guarantees. All inspected long-running containers use `restart: unless-stopped`, Docker's `json-file` logging driver, and no explicit memory or CPU limit.

## Docker and hosting footprint

### Runtime

- Docker Engine `29.6.0`, active and enabled under systemd.
- Docker Compose `v5.1.4`.
- `containerd` active.
- Storage driver `overlayfs`; cgroup v2 with the systemd cgroup driver.

### Existing Compose projects

| Project | Compose file | Running services |
| --- | --- | --- |
| `photobiz` | `/opt/photobiz/docker-compose.prod.yml` | Caddy reverse proxy, API, worker, PostgreSQL, Redis |
| `jellyfin-stack` | `/opt/jellyfin-stack/docker-compose.yml` | Jellyfin, File Browser |

There is also one stopped `hello-world` container.

The PhotoBIZ deployment directory is owned by `photobiz:photobiz`. It is an artifact/source layout rather than a Git worktree. Built Admin and Booth static assets are mounted read-only into Caddy from `/opt/photobiz/deploy`. This existing pattern suggests artifact-oriented delivery rather than running `git pull` on the server.

### Reverse proxy and TLS

- No host-level Caddy, Nginx, Apache, HAProxy, Traefik, or Certbot executable was found.
- Caddy `v2.11.3` runs in `photobiz-reverse-proxy-1` and exclusively publishes host ports 80 and 443.
- Its configuration is bind-mounted read-only from `/opt/photobiz/infra/caddy/Caddyfile.prod`.
- Current routes are:
  - `api.159.223.47.227.sslip.io` -> `api:8080`
  - `admin.159.223.47.227.sslip.io` -> static files in `/srv/admin`
  - `booth.159.223.47.227.sslip.io` -> static files in `/srv/booth`
- HTTPS verification succeeded for all three names. Admin and Booth returned HTTP 200; the API root returned HTTP 404, which only proves proxy/TLS reachability, not API health.
- Caddy has current ZeroSSL certificates for the three names through 2026-10-21 and retains an older Let's Encrypt Admin certificate in its data volume. Certificate renewal is Caddy-managed; Certbot is not part of the host.

**Constraint:** another proxy cannot be added on the same IP and ports without displacing the existing Caddy service. The GitHub Actions design must choose how FotoHAVN is registered with this Caddy instance and how to avoid restarting unrelated PhotoBIZ services.

## Network, SSH, and firewall posture

### Listening ports

| Port | Listener | Exposure observed |
| ---: | --- | --- |
| 22/tcp | OpenSSH via systemd socket activation | Public IPv4 and IPv6; allowed by UFW |
| 80/tcp | Docker proxy -> Caddy | Public IPv4 and IPv6; allowed by UFW |
| 443/tcp | Docker proxy -> Caddy | Public IPv4 and IPv6; allowed by UFW |
| 8096/tcp | Docker proxy -> Jellyfin | Public IPv4 and IPv6; allowed by UFW |
| 8097/tcp | Docker proxy -> File Browser | Public IPv4 and IPv6; allowed by UFW |

UFW is active with logging at `low`, default-deny incoming, allow outgoing, and deny routed traffic. Docker installs its own forwarding/NAT rules. DigitalOcean cloud-firewall state was not available from inside the Droplet and remains unverified.

### SSH

- OpenSSH `9.6p1`.
- Public-key authentication enabled.
- Password and keyboard-interactive authentication disabled.
- Direct root login allowed.
- Empty passwords disabled.
- X11, TCP, and agent forwarding enabled.
- `ssh.socket` is active/enabled; `ssh.service` itself is disabled because socket activation is in use.
- Fail2Ban is active/enabled with the `sshd` jail. It reported substantial historical hostile traffic (518 total bans), reinforcing the need to minimize SSH exposure and key scope.
- AppArmor is active; unattended upgrades are active/enabled.

The supplied identity is authorized for both `root` and the non-root `photobiz` account. The `photobiz` account:

- has UID/GID 1000;
- is a member of the `docker` group;
- has no sudo permission;
- owns `/opt/photobiz`;
- already has a second authorized key whose comment indicates a GitHub Actions deployment identity.

The existence, owner, repository scope, rotation policy, and current use of that second key were not established. Docker-group access is effectively root-equivalent, so `photobiz` is operationally better than direct SSH as `root` but is not a strong privilege boundary. Do not put the supplied personal private key into GitHub Secrets. Decide whether FotoHAVN gets a dedicated deploy key/account and a narrowly scoped deployment command.

No PTR record was returned for `159.223.47.227` by the inspecting resolver. The Droplet itself maps the address to its local hostname.

## Host tools and deployment paths

| Tool | State |
| --- | --- |
| Git | `2.43.0` installed |
| rsync | `3.2.7` installed |
| curl / wget / tar | installed |
| Node.js / npm / npx | not installed |
| pnpm / yarn / Bun / Corepack | not installed |

`/var/www` does not exist. Existing application conventions use `/opt/<application>` for code/deployment configuration and `/srv` for large persistent media/config data. A new `/opt/fotohavn` directory would need to be explicitly provisioned and owned before a non-root deploy can use it. No directory was created during this inspection.

## Maintenance state

- `apt list --upgradable` reported 34 packages, including Docker, Compose, containerd, `wget`, and security updates.
- `/var/run/reboot-required` exists.
- The running kernel is `6.8.0-124-generic`; `6.8.0-138-generic` is installed.
- The login message independently reported pending standard security updates.

Plan a deliberate patch-and-reboot maintenance window before relying on the server as a production deployment target. Reconfirm Docker/Caddy startup and all existing PhotoBIZ/Jellyfin routes afterward. This inspection did not apply updates or reboot. The subsequent design interview accepted this maintenance state as a non-blocking risk for temporary staging; the risk remains documented rather than verified away.

## Local FotoHAVN website deployment shape

The local project at `website/` currently has this shape:

| Area | Observed state | Implication |
| --- | --- | --- |
| Framework | Next.js `16.3.2`, React `19.2.8`, TypeScript | The build environment must satisfy Next's Node engine requirement (`>=20.9.0`). |
| Scripts | `npm run lint`, `npm run build`, `npm run start` | CI can lint/build; production currently expects a Next server. |
| Config | `next.config.ts` only disables dev indicators | No `output: "standalone"` and no static-export mode is configured. |
| Route shape | One app route (`/`), currently listed in `.next/prerender-manifest.json` as prerendered | The page is static in content shape, but the packaging/runtime decision is not yet made. |
| Images | Multiple `next/image` usages; about 25.5 MB across 28 public files | Default production behavior uses the Next image optimizer, favoring a Next runtime unless the project deliberately switches to an unoptimized static export. |
| Fonts | `next/font/google` for Cormorant Garamond and Manrope; runtime Fontshare CSS for Cabinet Grotesk | Builds need outbound font access unless fonts are self-hosted. Browser rendering also currently depends on Fontshare availability. |
| Secrets/backend | No server route, server action, cookies/headers call, or runtime environment reference was found in `website/src` | The current landing page does not establish a server-side secret requirement, but inquiry-form delivery behavior still needs a product decision before deployment. |

The local worktree already contains extensive uncommitted website changes and QA artifacts. They were preserved. Only this findings document was added.

## Agreed deployment direction

The design interview resolved the deployment shape on 2026-08-22. The durable rationale is recorded in [ADR-0002](../adr/0002-deploy-website-as-an-immutable-container.md).

1. A manually dispatched workflow deploys only the current `main` revision; other refs are rejected.
2. GitHub-hosted runners execute `npm ci`, lint, TypeScript checking, the production build, and a local container smoke test before publishing.
3. The workflow builds a standalone Next.js image with the `/fotohvn` base path, without changing the inquiry flow or other product behavior.
4. The image is public in GHCR and is deployed by immutable commit SHA and digest rather than by `latest`.
5. A dedicated `fotohavn-deploy` SSH key and account invoke only a root-owned forced deployment command; the key has no general shell, forwarding, PTY, or unrestricted Docker access.
6. FotoHAVN runs as an isolated Compose project with no public host port, explicit memory/CPU/PID limits, restart policy, and rotated logs.
7. Temporary staging is exposed only at `http://159.223.47.227/fotohvn`; the IP root returns `404`, staging metadata is `noindex`, and trusted HTTPS waits for a hostname.
8. A one-time bootstrap gives the existing Caddy project an imported FotoHAVN route and shared external proxy network. Routine releases do not restart Caddy or unrelated services.
9. Deployments are serialized. Internal and public HTTP checks must pass; otherwise the previous digest is restored automatically.
10. Deployment secrets live in a reviewer-free GitHub `production` environment. Manual dispatch is the sole approval step.
11. Bootstrap is delivered as a human-reviewed runbook plus an idempotent script and is not part of the recurring deployment workflow.
12. No uptime target is imposed for staging, and pending OS updates/reboot do not gate this deployment work.

The current site metadata names `https://fotohvn.com`, but `fotohvn.com` and `www.fotohvn.com` returned no DNS records during this inspection. Moving from raw-IP staging to a public hostname remains a separate future decision.

### Repository implementation status

The repository implementation was completed and locally verified on 2026-08-22:

- [the manual staging workflow](../../.github/workflows/deploy-fotohavn-staging.yml) builds, publishes, deploys, or rolls back;
- [the standalone website image](../../website/Dockerfile) serves the base-path-aware Next.js app;
- [the deployment bundle](../../deploy/fotohavn/) contains the isolated Compose service, Caddy route, restricted deploy command, executable state-machine test, and idempotent bootstrap;
- [the bootstrap runbook](fotohavn-bootstrap-runbook.md) separates repository preparation from reviewed VPS and GitHub configuration.

This status does not mean the workflow has run. No GHCR image was published, GitHub Environment or secret was created, bootstrap was executed, or VPS configuration was changed during implementation.

## Risks and gaps

### Blocking or high-priority

1. **Proxy ownership:** Caddy and ports 80/443 belong to the existing `photobiz` Compose project. A route/config reload must not become a full unrelated-stack restart.
2. **Resource headroom:** 1.9 GiB RAM, no swap, one vCPU, and an existing media workload make on-host builds and uncontrolled image pulls risky.
3. **Maintenance debt:** pending updates and a required reboot should be resolved and verified first.
4. **DNS absent:** the intended public names do not currently resolve.
5. **Deployment identity:** a dedicated Actions-looking key exists, but its provenance and scope are unknown. The attached personal key must not be reused as a repository secret.

### Security hardening to decide

- Whether to disable direct root SSH and unneeded X11/TCP/agent forwarding.
- Whether SSH should be IP-restricted at UFW and/or DigitalOcean cloud-firewall level, recognizing that GitHub-hosted runner IP ranges are broad and change.
- Whether to use a self-hosted runner, direct SSH from GitHub-hosted runners, or a pull-based deployment agent. A self-hosted runner on this shared 2 GB Droplet would add privilege and resource risk.
- Whether direct public access to ports 8096/8097 is intentional.
- Whether Docker log rotation and container resource limits should be added.

### Unverified external/control-plane state

- DigitalOcean backups/snapshots, monitoring, reserved IP, and cloud-firewall rules.
- Cloudflare or other DNS account ownership and records.
- GitHub repository Environments, secrets, branch protections, packages/registry permissions, and current Actions workflows.
- Off-server backups and recovery testing for the existing PhotoBIZ data services.
- Whether `fotohvn.com` is the final canonical domain or a temporary value.

## Evidence summary

Read-only evidence came from the following command families; outputs were summarized above rather than copied wholesale:

- Host: `hostnamectl`, `timedatectl`, `uname`, `/etc/os-release`, `lscpu`, `free`, `df`, `lsblk`, `swapon`, `uptime`.
- Network/security: `ss -lntup`, `sshd -T` filtered to authentication/forwarding directives, `ufw status verbose`, filtered `nft` rules, `fail2ban-client status`, `aa-status`, and systemd service/socket states.
- Containers: Docker/Compose versions, `docker info`, `docker compose ls`, formatted `docker ps`, formatted image/volume/network lists, selected non-secret inspection fields, mounts, restart/log/resource settings, `docker stats --no-stream`, and `docker system df`.
- Proxy/TLS: selected Caddy route directives, certificate subjects/issuers/dates, and HTTPS status/TLS verification checks.
- Deployment shape: command availability, account/group membership, authorized-key fingerprints only, directory ownership/size, Compose/config paths, and environment-file presence/permissions without reading values.
- Maintenance: reboot-required markers, installed/running kernel versions, and cached upgradable-package metadata.
- Local website: `package.json`, `next.config.ts`, framework engine metadata, source searches for runtime APIs/environment dependencies, public-asset sizing, and the existing prerender manifest.

Because this is a point-in-time snapshot, rerun the material checks immediately before implementation and again after the maintenance reboot.
