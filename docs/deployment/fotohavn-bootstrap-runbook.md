# FotoHAVN staging bootstrap runbook

This runbook provisions the one-time VPS seam required by the accepted deployment design in [ADR-0002](../adr/0002-deploy-website-as-an-immutable-container.md). It does not deploy an image. Review the script and every path below before execution.

## Scope

The bootstrap:

- creates the dedicated `fotohavn-deploy` account;
- installs a root-owned forced deployment command and a narrow sudo rule;
- installs `/opt/fotohavn/compose.yml` and the Caddy route fragment;
- adds the fragment as a read-only mount to the existing PhotoBIZ Caddy service;
- imports the fragment from the existing Caddyfile;
- recreates only the Caddy container when the import seam changes;
- validates both Compose projects and the resulting Caddy configuration;
- leaves FotoHAVN undeployed.

It does not update Ubuntu, reboot the VPS, change UFW, expose a new host port, alter application behavior, install a GitHub runner, or reuse the inspection key as an Actions credential.

## Preconditions

1. Review these repository files:
   - `deploy/fotohavn/bootstrap.sh`
   - `deploy/fotohavn/compose.yml`
   - `deploy/fotohavn/fotohavn.caddy`
   - `deploy/fotohavn/fotohavn-deploy`
   - `deploy/fotohavn/fotohavn-ssh-command`
2. Confirm the VPS findings still materially match [the inventory](digitalocean-vps-findings.md).
3. Confirm `/opt/photobiz/docker-compose.prod.yml`, `/opt/photobiz/infra/caddy/Caddyfile.prod`, and the `photobiz_default` Docker network still exist.
4. Take or verify the desired DigitalOcean snapshot/backup. Backup state was not visible from inside the VPS during discovery.
5. Plan for a short interruption while the existing Caddy container is recreated once.

## Create the deployment key

Generate a new key locally. Do not reuse `photobiz_pilot_ed25519`.

```powershell
$deployKey = Join-Path $env:USERPROFILE '.ssh\fotohavn_github_actions_ed25519'
ssh-keygen -t ed25519 -N '' -C 'github-actions-fotohavn' -f $deployKey
ssh-keygen -lf "$deployKey.pub"
```

The automation key is intentionally passphrase-free because GitHub stores it as an environment secret and the VPS restricts it with `authorized_keys`, a forced root-owned command, command validation, no PTY, and no forwarding. Rotate it if the secret or repository boundary changes.

## Copy and review the bootstrap bundle

From the repository root, copy only the bootstrap bundle and public key to a temporary VPS directory:

```powershell
$deployKey = Join-Path $env:USERPROFILE '.ssh\fotohavn_github_actions_ed25519'
ssh -i C:\Users\QUINJ3875\.ssh\photobiz_pilot_ed25519 root@159.223.47.227 'install -d -m 0700 /root/fotohavn-bootstrap'
scp -i C:\Users\QUINJ3875\.ssh\photobiz_pilot_ed25519 -r .\deploy\fotohavn\* root@159.223.47.227:/root/fotohavn-bootstrap/
scp -i C:\Users\QUINJ3875\.ssh\photobiz_pilot_ed25519 "$deployKey.pub" root@159.223.47.227:/root/fotohavn-bootstrap/fotohavn_deploy_ed25519.pub
```

Inspect the copied files before execution:

```powershell
ssh -i C:\Users\QUINJ3875\.ssh\photobiz_pilot_ed25519 root@159.223.47.227 'cd /root/fotohavn-bootstrap && sha256sum * && sed -n "1,260p" bootstrap.sh'
```

## Execute bootstrap

This is the mutation boundary. Run only after review:

```powershell
ssh -t -i C:\Users\QUINJ3875\.ssh\photobiz_pilot_ed25519 root@159.223.47.227 'cd /root/fotohavn-bootstrap && bash bootstrap.sh fotohavn_deploy_ed25519.pub'
```

The script prints the backup directory created under `/var/backups/fotohavn-bootstrap/`. Record that path in the change evidence.

## Verify the restricted identity

The key must reject arbitrary shell commands:

```powershell
$deployKey = Join-Path $env:USERPROFILE '.ssh\fotohavn_github_actions_ed25519'
ssh -i $deployKey fotohavn-deploy@159.223.47.227 'uname -a'
```

Expected: the forced command rejects the request and lists only `deploy <approved-image-digest>` or `rollback`. A shell prompt, `uname` output, forwarding, or PTY access is a failure.

## Configure GitHub

In `quinjan/FotoHAVN`:

1. Create an Environment named `production` without required reviewers.
2. Add environment secret `FOTOHAVN_SSH_PRIVATE_KEY` containing the complete new private key.
3. Add environment secret `FOTOHAVN_SSH_KNOWN_HOSTS` containing the verified `known_hosts` line for `159.223.47.227`.
4. Add environment variable `FOTOHAVN_VPS_HOST` with `159.223.47.227`.
5. Add environment variable `FOTOHAVN_DEPLOY_USER` with `fotohavn-deploy`.

The observed VPS ED25519 host-key fingerprint was `SHA256:qWJ3i6xyF04ofG+dMtmRCYAIpD9B4zGc4wi999i3AbbA`. Verify a freshly collected host key against that independently observed fingerprint before storing it; do not trust an `ssh-keyscan` result merely because the workflow collected it.

## Make the first GHCR package public

New GHCR packages are private initially. The workflow therefore includes a `publish-only` operation for the first run:

1. Manually run `Deploy FotoHAVN staging` from `main` with `operation=publish-only`.
2. Open the newly created `fotohavn-website` container package settings.
3. Change package visibility to **Public**.
4. Run the workflow again with `operation=deploy`.

Normal deployments require no registry credential on the VPS. Deployments use the digest emitted by the successful publish job, never a moving tag.

## Deployment verification

A successful workflow proves all of the following:

- repository gates passed against the current `main` revision;
- the built container became healthy at `/fotohvn`;
- the public route returned HTTP 200 and contained `FOTOHVN`;
- `/` on the raw IP still returned 404;
- the deployed state records the immutable GHCR digest.

Also verify the existing named PhotoBIZ routes after the first deployment. The FotoHAVN workflow deliberately does not restart those services.

## Rollback

Run the same workflow with `operation=rollback`. The forced command swaps the current and previous recorded digests and verifies the restored container. A failed normal deployment performs this rollback automatically.

The VPS keeps the current and previous image locally. Do not run broad Docker cleanup commands on this shared host. Retain the ten most recent successful package versions in GHCR until a package lifecycle policy is automated and reviewed.

## Bootstrap rollback

The bootstrap automatically restores the original PhotoBIZ Compose file and Caddyfile if validation or Caddy recreation fails. If a later manual rollback is required, use the recorded backup directory to restore:

- `docker-compose.prod.yml` to `/opt/photobiz/docker-compose.prod.yml`;
- `Caddyfile.prod` to `/opt/photobiz/infra/caddy/Caddyfile.prod`;
- the prior FotoHAVN fragment, or remove it if the backup contains none.

Then recreate only the PhotoBIZ `reverse-proxy` service and validate `/etc/caddy/Caddyfile` inside the resulting container.
