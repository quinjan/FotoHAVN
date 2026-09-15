# Connect the Spaceship domain to the FotoHAVN DigitalOcean deployment

Research date: 2026-09-15

## Execution status

The user approved the clean-root option for a coordinated cutover on 2026-09-15. The implementation now targets `https://fotohavn.com/`, removes the Next.js `/fotohavn` base path, enables canonical production metadata and indexing, redirects `www` to the apex, and retains the raw-IP `/fotohavn` route only for rollback compatibility. The planning and alternative-rollout sections below are preserved as research history; where they conflict with this execution status, this section and the current deployment files are authoritative.

## Decision-ready answer

Keep DNS on Spaceship for this cutover. Point the apex domain to the DigitalOcean server with one `A` record, point `www` to the apex with one `CNAME`, and configure the **existing PhotoBIZ Caddy container** to recognize both hostnames. There is no requirement to move DNS hosting to DigitalOcean merely because the web server is a DigitalOcean Droplet.

The likely purchased domain is `fotohavn.com`, but the user must confirm that exact spelling before any change. A live lookup on 2026-09-15 found:

| Name | Current result | Meaning |
| --- | --- | --- |
| `fotohavn.com` NS | `launch1.spaceship.net`, `launch2.spaceship.net` | Spaceship is currently authoritative, so records should be edited in Spaceship Advanced DNS. |
| `fotohavn.com` A | `54.149.79.189`, `34.216.117.25`, TTL 300 | These appear to be parking/default targets, not the FotoHAVN VPS. Both must be replaced; leaving either one creates round-robin traffic to the wrong server. |
| `www.fotohavn.com` | no record returned | Add it explicitly if `www` should work. |

The repository's last inspected Droplet address is `159.223.47.227`, but this is time-sensitive. Confirm the current public IPv4 address in the DigitalOcean control panel before using it in DNS. An optional DigitalOcean Reserved IP would make later Droplet replacement easier because Reserved IPs are static and can be reassigned, but adopting one changes the cutover address and should be a separate reviewed infrastructure decision. The SSH `known_hosts` entry changes only if CI is also switched to SSH through the Reserved IP; CI can continue using the Droplet's original address. [DigitalOcean: Reserved IP details](https://docs.digitalocean.com/products/networking/reserved-ips/details/)

## Important repository-specific constraints

This is not a DNS-only change:

- The website currently has Next.js `basePath: "/fotohavn"` through [`website/site.config.ts`](../website/site.config.ts) and [`website/next.config.ts`](../website/next.config.ts). Its current public route is therefore `/fotohavn`, not `/`.
- The staging metadata URL is still the raw HTTP IP and the site is explicitly `noindex` in [`website/src/app/layout.tsx`](../website/src/app/layout.tsx). A production launch must change the canonical origin to `https://<domain>` and deliberately enable indexing when ready.
- Container health checks, the deployment script, and GitHub Actions all hard-code the current raw-IP/subpath boundary in [`website/Dockerfile`](../website/Dockerfile), [`deploy/fotohavn/fotohavn-deploy`](../deploy/fotohavn/fotohavn-deploy), and [`.github/workflows/deploy-fotohavn-staging.yml`](../.github/workflows/deploy-fotohavn-staging.yml).
- The current Caddy fragment declares `@fotohavn` but uses `handle @fotohvn`. That matcher-name mismatch in [`deploy/fotohavn/fotohavn.caddy`](../deploy/fotohavn/fotohavn.caddy) is a pre-cutover defect and must be fixed and validated before the fragment reaches the server.
- The deployment script and GitHub Actions smoke tests still search for the legacy `FOTOHVN` page marker even though the rendered brand is now `FOTOHAVN`. Update those markers before relying on the deployment/rollback checks for launch evidence.
- Caddy already owns host ports 80 and 443 for several PhotoBIZ services. FotoHAVN should continue to share its Docker network and reverse proxy to `fotohavn-web:3000`; do **not** publish container port 3000 or install a second proxy.

## Recommended public URL and rollout shape

Use the apex as canonical: `https://fotohavn.com`. Redirect `https://www.fotohavn.com/...` to the same path on the apex.

There are two viable path choices:

1. **Safest first cutover:** keep the app at `https://fotohavn.com/fotohavn` and redirect only `/` to `/fotohavn`. This can reuse the already-built app namespace and limits the first change to DNS, Caddy, metadata, and deployment verification.
2. **Cleaner final URL:** serve the app at `https://fotohavn.com/`. This requires a coordinated rebuild with an empty Next.js `basePath`, updates to all asset/health/smoke-test URLs, and a Caddy root proxy. Treat it as an application migration, not a DNS setting.

A two-stage rollout—first make the domain work with the existing `/fotohavn` path, then migrate the app to `/`—has the smallest rollback surface.

## Step-by-step cutover

### 1. Confirm the exact domain and the target IP

1. Confirm the purchased domain is exactly `fotohavn.com`.
2. In DigitalOcean, open the Droplet and record its current public IPv4 address.
3. Confirm the raw-IP deployment is healthy before changing DNS:

   ```powershell
   curl.exe -fsS http://159.223.47.227/fotohavn | Select-String 'FOTOHAVN'
   ```

4. If the DigitalOcean address differs, stop and update the repository deployment boundary and GitHub SSH configuration before DNS.

### 2. Preflight both firewalls and Caddy

On the Droplet, confirm Caddy is the listener and Ubuntu permits HTTP/HTTPS:

```bash
sudo ss -ltnp '( sport = :80 or sport = :443 )'
sudo ufw status verbose
docker ps --format 'table {{.Names}}\t{{.Ports}}' | grep -E 'reverse-proxy|caddy'
```

In DigitalOcean, open **Networking -> Firewalls**, inspect every Cloud Firewall attached to the Droplet, and ensure inbound `HTTP`/TCP 80 and `HTTPS`/TCP 443 are allowed from all intended public sources. DigitalOcean Cloud Firewalls block traffic that is not expressly permitted; the control panel's HTTP and HTTPS presets map to TCP 80 and 443. [DigitalOcean: configure firewall rules](https://docs.digitalocean.com/products/networking/firewalls/how-to/configure-rules/)

Do not add an inbound rule for port 3000. Caddy reaches the FotoHAVN container over the existing `photobiz_default` Docker network.

Also confirm the Caddy container has persistent, writable storage for `/data`; Caddy requires persistent writable storage to retain and renew its ACME account and certificates. The 2026-08-22 inventory found that the existing proxy met this condition, but it must be rechecked before launch. [Caddy: Automatic HTTPS requirements](https://caddyserver.com/docs/automatic-https#overview)

### 3. Prepare and review the repository changes before touching DNS

For the safest first cutover, prepare a PR that:

1. Fixes the `@fotohavn` / `@fotohvn` matcher mismatch.
2. Adds domain site blocks that preserve `/fotohavn`. Keep the raw-IP site block temporarily as a rollback boundary:

   ```caddyfile
   fotohavn.com {
       @root path /
       redir @root /fotohavn 308

       @fotohavn path /fotohavn /fotohavn/*
       handle @fotohavn {
           encode zstd gzip
           reverse_proxy fotohavn-web:3000
       }

       handle {
           respond 404
       }
   }

   www.fotohavn.com {
       redir https://fotohavn.com{uri} permanent
   }
   ```

3. Changes `stagingSiteUrl`/metadata to the chosen `https://` production URL and reviews the `robots` decision.
4. Updates the stale `FOTOHVN` page markers to `FOTOHAVN` in the workflow and deployment script. Keep the pre-cutover public smoke test on `http://159.223.47.227/fotohavn` so the first deployment can pass before DNS changes.
5. Keeps `FOTOHAVN_VPS_HOST=159.223.47.227` for restricted SSH. Do not repurpose it as the public URL; add a non-secret environment variable such as `FOTOHAVN_PUBLIC_URL=https://fotohavn.com/fotohavn` and reference it from the workflow. GitHub supports repository- or environment-scoped configuration variables through the `vars` context. [GitHub: store information in variables](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-variables)
6. Prepares a follow-up change that switches the public smoke boundary to `https://fotohavn.com/fotohavn` after DNS and HTTPS pass, and updates tests/runbooks consistently. Do not make the first pre-cutover deployment depend on a hostname that is not live yet.

Validate the PR locally and with CI before installing its Caddy fragment. In particular, run the website lint/type/build gates, deployment tests, ShellCheck/actionlint if available, and Caddy validation. The Caddy CLI documents `caddy validate` as a stronger check than adaptation alone. [Caddy: command-line validation](https://caddyserver.com/docs/command-line#caddy-validate)

Once that pre-cutover deployment passes, install the reviewed fragment using the repository's existing bootstrap/change-control pattern: make a backup, copy the fragment to `/opt/fotohavn/caddy/fotohavn.caddy`, validate the complete imported Caddyfile inside the running PhotoBIZ proxy, and gracefully reload (or recreate only the reverse-proxy service if following the existing bootstrap script). Never restart the entire shared PhotoBIZ stack. Because DNS does not point at the Droplet yet, temporary certificate-issuance warnings are expected; Caddy retries automatically.

If the clean root URL is required immediately, the same PR must instead set the Next.js base path to empty, update all internal/public paths and health checks to `/`, proxy the whole domain in Caddy, and change the workflow's old expectation that `/` returns 404. Do not change only the Caddy prefix; the app was compiled for `/fotohavn`.

### 4. Replace the Spaceship parking records

Use Spaceship's DNS rather than changing nameservers:

1. Sign in to Spaceship.
2. Open **Advanced DNS Manager**.
3. Select `fotohavn.com`.
4. Open **DNS records -> Custom records**.
5. Remove or edit the two existing apex `A` records that resolve to `54.149.79.189` and `34.216.117.25`.
6. Create these records, replacing the address only if DigitalOcean shows a different verified public/Reserved IPv4:

   | Host | Type | Value | TTL during cutover |
   | --- | --- | --- | ---: |
   | `@` | `A` | `159.223.47.227` | `300` seconds |
   | `www` | `CNAME` | `fotohavn.com` | `300` seconds |

7. Remove any conflicting `AAAA` record unless the Droplet's IPv6 route, firewall, and Caddy listener have all been verified. A broken AAAA record can send IPv6-capable visitors to the wrong destination.
8. Preserve unrelated MX/TXT/DKIM/DMARC records if email is or may be used. DNS changes for the website do not require deleting mail records.

Spaceship documents that `A` maps a name to IPv4, `CNAME` aliases one name to another, and custom records are added under Advanced DNS Manager. Its current record API accepts TTLs from 60 to 3600 seconds. [Spaceship: DNS record types and UI steps](https://www.spaceship.com/en-GB/knowledgebase/dns-records-types/), [Spaceship: DNS record shapes and TTL range](https://www.spaceship.com/knowledgebase/spaceship-mcp/)

Five minutes is an appropriate cutover TTL, not a guarantee that every resolver changes in exactly five minutes. Spaceship says ordinary record changes usually take effect within a few hours depending on TTL; nameserver changes can take up to 48 hours. [Spaceship: DNS timing](https://www.spaceship.com/knowledgebase/spacemail-dns-records-third-party-domain/)

### 5. Wait for authoritative DNS and Caddy HTTPS

From more than one network/resolver, verify the new records:

```powershell
Resolve-DnsName fotohavn.com -Type NS
Resolve-DnsName fotohavn.com -Type A
Resolve-DnsName www.fotohavn.com -Type CNAME
```

Expected apex result: only the verified DigitalOcean address. Do not proceed to the production smoke-test switch while either old parking IP is still being returned.

Caddy will obtain and renew publicly trusted certificates and redirect HTTP to HTTPS automatically when the hostname is present in the Caddyfile, its `A`/`AAAA` records point to the server, ports 80 and 443 are externally reachable, Caddy can bind those ports, and its data directory is writable and persistent. Prefixing a site address with `http://`, as the current raw-IP fragment does, disables automatic HTTPS for that site. [Caddy: Automatic HTTPS](https://caddyserver.com/docs/automatic-https), [Caddy: Caddyfile site addresses](https://caddyserver.com/docs/caddyfile/concepts#addresses)

The default ACME HTTP challenge requires public port 80 and the TLS-ALPN challenge requires public port 443. A Spaceship API token or Caddy DNS plugin is **not** needed for this normal non-wildcard setup. [Caddy: ACME challenge requirements](https://caddyserver.com/docs/automatic-https#http-challenge)

### 6. Verify HTTPS before declaring the cutover complete

Run:

```powershell
Resolve-DnsName fotohavn.com -Type A
curl.exe -I http://fotohavn.com/
curl.exe -I https://fotohavn.com/
curl.exe -fsS https://fotohavn.com/fotohavn | Select-String 'FOTOHAVN'
curl.exe -I https://www.fotohavn.com/fotohavn
```

For the first-cutover configuration, verify all of these outcomes:

- `http://fotohavn.com/...` redirects to HTTPS;
- `https://fotohavn.com/` redirects to `/fotohavn`;
- `https://fotohavn.com/fotohavn` returns 200 and contains `FOTOHAVN`;
- `https://www.fotohavn.com/...` redirects to the apex while preserving the path;
- the browser reports a valid certificate for both apex and `www`;
- existing PhotoBIZ API/Admin/Booth hostnames still behave as before;
- the raw-IP route remains available for rollback until the domain deployment has passed.

After these checks pass, apply the prepared follow-up that switches the deployment script and GitHub Actions public smoke test to `https://fotohavn.com/fotohavn`. Keep `FOTOHAVN_VPS_HOST` on the IP for SSH.

Check Caddy logs for certificate issuance or challenge failures:

```bash
proxy_id="$(docker compose -f /opt/photobiz/docker-compose.prod.yml ps -q reverse-proxy)"
docker logs --since 15m "$proxy_id" | grep -Ei 'tls|certificate|acme|error'
```

Then manually run **Deploy FotoHAVN staging** from `main` with `operation=deploy`. The workflow should verify the public domain over HTTPS. Test `operation=rollback` only if the current/previous digest contract and rollback impact are understood.

### 7. Stabilize and finish the production transition

After 24–48 hours of correct resolution and browser checks:

1. Raise the DNS TTL to 1800 or 3600 seconds to reduce routine DNS query churn.
2. Remove the temporary raw-IP Caddy site block only after rollback no longer depends on it.
3. Rename staging-oriented metadata/workflow text if this is now production.
4. Enable indexing only when content, canonical URLs, privacy/legal pages, and launch approval are ready.
5. If desired, schedule the separate `/fotohavn` -> `/` application migration and add an intentional redirect from the old path.

## Alternative: delegate DNS to DigitalOcean

This is valid but not recommended for the first cutover. It is useful only if the team wants DNS managed beside the Droplet.

1. In DigitalOcean, go to **Networking -> Domains**, add the apex domain, and recreate **all** existing DNS records before delegation.
2. Add `@ A <verified-IP>` and `www CNAME @`/the apex.
3. In Spaceship **Advanced DNS -> Nameservers -> Change**, choose **Custom nameservers** and enter:
   - `ns1.digitalocean.com`
   - `ns2.digitalocean.com`
   - `ns3.digitalocean.com`
4. Wait for NS propagation, then manage records only in DigitalOcean.

DigitalOcean explicitly supports either registrar-managed records or delegation to DigitalOcean DNS, and Spaceship warns that changing to custom nameservers makes Spaceship records inactive and can disconnect hosting/email unless their records are recreated. [DigitalOcean: registrar DNS versus DigitalOcean DNS](https://docs.digitalocean.com/products/networking/dns/getting-started/dns-registrars/), [Spaceship: custom nameservers](https://www.spaceship.com/knowledgebase/connect-domain-custom-nameservers/)

Nameserver delegation has a larger blast radius and can take up to 48 hours according to Spaceship. It provides no special advantage to Caddy certificate issuance for this single Droplet, so retaining Spaceship's `launch1`/`launch2` nameservers is simpler.

## Assumptions and unresolved checks

- `fotohavn.com` is inferred from current DNS and repository history; ownership and the user's intended canonical spelling are not independently confirmed.
- `159.223.47.227` is the last repository-observed Droplet IP, not a timeless value; verify it live in DigitalOcean.
- The host-level UFW snapshot allowed 80/443 in August 2026, but current UFW and DigitalOcean Cloud Firewall state were not modified or re-verified from the provider account in this research.
- The production Caddy parent file is outside this repository and shared with PhotoBIZ; revalidate its import, service name, network, `/data` persistence, and current named routes immediately before the change.
- No runtime/deployment code was changed by this research.

## Primary sources

- [Spaceship: DNS record types and Advanced DNS steps](https://www.spaceship.com/en-GB/knowledgebase/dns-records-types/)
- [Spaceship: custom nameservers and propagation warning](https://www.spaceship.com/knowledgebase/connect-domain-custom-nameservers/)
- [DigitalOcean: DNS quickstart](https://docs.digitalocean.com/products/networking/dns/getting-started/quickstart/)
- [DigitalOcean: registrar DNS versus DigitalOcean nameservers](https://docs.digitalocean.com/products/networking/dns/getting-started/dns-registrars/)
- [DigitalOcean: firewall rule configuration](https://docs.digitalocean.com/products/networking/firewalls/how-to/configure-rules/)
- [Caddy: Automatic HTTPS](https://caddyserver.com/docs/automatic-https)
- [Caddy: Caddyfile concepts](https://caddyserver.com/docs/caddyfile/concepts)
- [GitHub: configuration variables](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-variables)
