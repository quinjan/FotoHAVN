#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly SCRIPT_DIR
readonly PHOTOBIZ_DIR="/opt/photobiz"
readonly PHOTOBIZ_COMPOSE="${PHOTOBIZ_DIR}/docker-compose.prod.yml"
readonly PHOTOBIZ_CADDYFILE="${PHOTOBIZ_DIR}/infra/caddy/Caddyfile.prod"
readonly APP_DIR="/opt/fotohavn"
readonly ROUTE_DIR="${APP_DIR}/caddy"
readonly ROUTE_FILE="${ROUTE_DIR}/fotohavn.caddy"
readonly DEPLOY_USER="fotohavn-deploy"
readonly DEPLOY_HOME="/var/lib/${DEPLOY_USER}"
readonly STATE_DIR="/var/lib/fotohavn"
readonly CADDY_IMPORT="import /etc/caddy/fotohavn.caddy"
readonly CADDY_MOUNT="      - /opt/fotohavn/caddy/fotohavn.caddy:/etc/caddy/fotohavn.caddy:ro"
readonly CADDY_MOUNT_ANCHOR="      - ./infra/caddy/Caddyfile.prod:/etc/caddy/Caddyfile:ro"

log() {
  printf '[fotohavn-bootstrap] %s\n' "$*"
}

fail() {
  log "ERROR: $*" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "Required command not found: $1"
}

main() {
  local public_key_file="${1:-}"
  local public_key backup_dir route_existed=0 caddy_changed=0 proxy_id account_entry account_home account_shell

  [[ "${EUID}" -eq 0 ]] || fail "Run this script as root."
  [[ -n "$public_key_file" && -f "$public_key_file" ]] \
    || fail "Usage: sudo bash bootstrap.sh /path/to/fotohavn_deploy_ed25519.pub"

  for command_name in cmp docker curl flock getent install sed ssh-keygen visudo; do
    require_command "$command_name"
  done

  [[ -f "$PHOTOBIZ_COMPOSE" ]] || fail "Missing ${PHOTOBIZ_COMPOSE}."
  [[ -f "$PHOTOBIZ_CADDYFILE" ]] || fail "Missing ${PHOTOBIZ_CADDYFILE}."
  [[ -f "${SCRIPT_DIR}/compose.yml" ]] || fail "Bootstrap bundle is incomplete."
  [[ -f "${SCRIPT_DIR}/fotohavn.caddy" ]] || fail "Bootstrap bundle is incomplete."
  [[ -f "${SCRIPT_DIR}/fotohavn-deploy" ]] || fail "Bootstrap bundle is incomplete."
  [[ -f "${SCRIPT_DIR}/fotohavn-ssh-command" ]] || fail "Bootstrap bundle is incomplete."
  docker network inspect photobiz_default >/dev/null \
    || fail "The existing photobiz_default network was not found."

  public_key="$(tr -d '\r\n' < "$public_key_file")"
  [[ "$public_key" =~ ^ssh-ed25519\ [A-Za-z0-9+/=]+(\ .*)?$ ]] \
    || fail "Expected one OpenSSH Ed25519 public key."
  ssh-keygen -lf "$public_key_file"

  if ! getent passwd "$DEPLOY_USER" >/dev/null; then
    useradd \
      --create-home \
      --home-dir "$DEPLOY_HOME" \
      --shell /bin/bash \
      --user-group \
      "$DEPLOY_USER"
    passwd --lock "$DEPLOY_USER" >/dev/null
  else
    account_entry="$(getent passwd "$DEPLOY_USER")"
    account_home="$(printf '%s' "$account_entry" | cut -d: -f6)"
    account_shell="$(printf '%s' "$account_entry" | cut -d: -f7)"
    [[ "$account_home" == "$DEPLOY_HOME" && "$account_shell" == "/bin/bash" ]] \
      || fail "Existing ${DEPLOY_USER} account has an unexpected home or shell."
  fi

  install -d -m 0750 -o root -g root "$APP_DIR" "$ROUTE_DIR" "$STATE_DIR"
  install -d -m 0700 -o "$DEPLOY_USER" -g "$DEPLOY_USER" "${DEPLOY_HOME}/.ssh"
  install -m 0644 -o root -g root "${SCRIPT_DIR}/compose.yml" "${APP_DIR}/compose.yml"
  install -m 0755 -o root -g root "${SCRIPT_DIR}/fotohavn-deploy" /usr/local/sbin/fotohavn-deploy
  install -m 0755 -o root -g root "${SCRIPT_DIR}/fotohavn-ssh-command" /usr/local/bin/fotohavn-ssh-command

  printf 'restrict,command="/usr/local/bin/fotohavn-ssh-command" %s\n' "$public_key" \
    > "${DEPLOY_HOME}/.ssh/authorized_keys"
  chown "$DEPLOY_USER:$DEPLOY_USER" "${DEPLOY_HOME}/.ssh/authorized_keys"
  chmod 0600 "${DEPLOY_HOME}/.ssh/authorized_keys"

  printf '%s ALL=(root) NOPASSWD: /usr/local/sbin/fotohavn-deploy *\n' "$DEPLOY_USER" \
    > /etc/sudoers.d/fotohavn-deploy
  chmod 0440 /etc/sudoers.d/fotohavn-deploy
  visudo -cf /etc/sudoers.d/fotohavn-deploy >/dev/null

  backup_dir="/var/backups/fotohavn-bootstrap/$(date -u +%Y%m%dT%H%M%SZ)"
  install -d -m 0700 "$backup_dir"
  cp -a "$PHOTOBIZ_COMPOSE" "${backup_dir}/docker-compose.prod.yml"
  cp -a "$PHOTOBIZ_CADDYFILE" "${backup_dir}/Caddyfile.prod"
  if [[ -f "$ROUTE_FILE" ]]; then
    route_existed=1
    cp -a "$ROUTE_FILE" "${backup_dir}/fotohavn.caddy"
  fi

  rollback_proxy_configuration() {
    local rollback_proxy_id
    trap - ERR
    log "Restoring the original Caddy and Compose configuration."
    cp -a "${backup_dir}/docker-compose.prod.yml" "$PHOTOBIZ_COMPOSE"
    cp -a "${backup_dir}/Caddyfile.prod" "$PHOTOBIZ_CADDYFILE"
    if [[ "$route_existed" -eq 1 ]]; then
      cp -a "${backup_dir}/fotohavn.caddy" "$ROUTE_FILE"
    else
      rm -f "$ROUTE_FILE"
    fi
    (
      cd "$PHOTOBIZ_DIR"
      docker compose --file docker-compose.prod.yml up --detach --no-deps --force-recreate reverse-proxy
    ) || true
    rollback_proxy_id="$(cd "$PHOTOBIZ_DIR" && docker compose --file docker-compose.prod.yml ps --quiet reverse-proxy)"
    if [[ -n "$rollback_proxy_id" ]]; then
      docker exec "$rollback_proxy_id" caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile || true
    fi
  }

  trap rollback_proxy_configuration ERR

  if ! cmp --silent "${SCRIPT_DIR}/fotohavn.caddy" "$ROUTE_FILE"; then
    install -m 0644 -o root -g root "${SCRIPT_DIR}/fotohavn.caddy" "$ROUTE_FILE"
    caddy_changed=1
  fi

  if ! grep --fixed-strings --quiet "$CADDY_MOUNT" "$PHOTOBIZ_COMPOSE"; then
    grep --fixed-strings --quiet "$CADDY_MOUNT_ANCHOR" "$PHOTOBIZ_COMPOSE" \
      || fail "Could not find the expected Caddyfile mount anchor."
    sed -i "\|${CADDY_MOUNT_ANCHOR}|a\\${CADDY_MOUNT}" "$PHOTOBIZ_COMPOSE"
    caddy_changed=1
  fi

  if ! grep --fixed-strings --line-regexp --quiet "$CADDY_IMPORT" "$PHOTOBIZ_CADDYFILE"; then
    printf '\n# FotoHAVN temporary raw-IP staging route.\n%s\n' "$CADDY_IMPORT" \
      >> "$PHOTOBIZ_CADDYFILE"
    caddy_changed=1
  fi

  (
    cd "$PHOTOBIZ_DIR"
    docker compose --file docker-compose.prod.yml config --quiet
  )

  if [[ "$caddy_changed" -eq 1 ]]; then
    log "Recreating only the Caddy service to apply the imported route mount."
    (
      cd "$PHOTOBIZ_DIR"
      docker compose --file docker-compose.prod.yml up --detach --no-deps --force-recreate reverse-proxy
    )
  fi

  proxy_id="$(cd "$PHOTOBIZ_DIR" && docker compose --file docker-compose.prod.yml ps --quiet reverse-proxy)"
  [[ -n "$proxy_id" ]] || fail "The PhotoBIZ reverse proxy is not running."
  docker exec "$proxy_id" caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile

  FOTOHAVN_IMAGE='ghcr.io/quinjan/fotohavn-website@sha256:0000000000000000000000000000000000000000000000000000000000000000' \
    docker compose --file "${APP_DIR}/compose.yml" config --quiet

  trap - ERR
  log "Bootstrap completed. Backup: ${backup_dir}"
  log "No FotoHAVN image was deployed."
}

main "$@"
