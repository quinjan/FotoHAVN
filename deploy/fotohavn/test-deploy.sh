#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly SCRIPT_DIR
readonly FAKE_BIN="/tmp/fotohavn-fake-bin"
readonly IMAGE_ONE="ghcr.io/quinjan/fotohavn-website@sha256:1111111111111111111111111111111111111111111111111111111111111111"
readonly IMAGE_TWO="ghcr.io/quinjan/fotohavn-website@sha256:2222222222222222222222222222222222222222222222222222222222222222"
readonly IMAGE_BAD="ghcr.io/quinjan/fotohavn-website@sha256:3333333333333333333333333333333333333333333333333333333333333333"

fail() {
  printf 'FAIL: %s\n' "$*" >&2
  exit 1
}

assert_file() {
  local expected="$1"
  local path="$2"
  local actual
  actual="$(tr -d '\r\n' < "$path")"
  [[ "$actual" == "$expected" ]] \
    || fail "Expected ${path} to contain ${expected}; found ${actual}."
}

install_fakes() {
  install -d -m 0755 "$FAKE_BIN"

  cat > "${FAKE_BIN}/docker" <<'FAKE_DOCKER'
#!/usr/bin/env bash
set -Eeuo pipefail

if [[ " $* " == *" compose "* && " $* " == *" up "* ]]; then
  sed -n 's/^FOTOHAVN_IMAGE=//p' /opt/fotohavn/.env > /tmp/fotohavn-active-image
  exit 0
fi

if [[ "${1:-}" == "inspect" ]]; then
  active_image="$(cat /tmp/fotohavn-active-image 2>/dev/null || true)"
  if [[ "$active_image" == *"sha256:3333333333333333333333333333333333333333333333333333333333333333" ]]; then
    printf 'unhealthy\n'
  else
    printf 'healthy\n'
  fi
  exit 0
fi

exit 0
FAKE_DOCKER

  cat > "${FAKE_BIN}/curl" <<'FAKE_CURL'
#!/usr/bin/env bash
set -Eeuo pipefail

output_file=''
while [[ "$#" -gt 0 ]]; do
  if [[ "$1" == "--output" ]]; then
    output_file="$2"
    shift 2
    continue
  fi
  shift
done

printf '<html><title>FOTOHVN</title></html>\n' > "$output_file"
FAKE_CURL

  cat > "${FAKE_BIN}/flock" <<'FAKE_FLOCK'
#!/usr/bin/env sh
exit 0
FAKE_FLOCK

  chmod 0755 "${FAKE_BIN}/docker" "${FAKE_BIN}/curl" "${FAKE_BIN}/flock"
}

main() {
  install -d -m 0755 /opt/fotohavn /usr/local/sbin /var/lib/fotohavn
  install -m 0644 "${SCRIPT_DIR}/compose.yml" /opt/fotohavn/compose.yml
  install -m 0755 "${SCRIPT_DIR}/fotohavn-deploy" /usr/local/sbin/fotohavn-deploy
  install_fakes
  export PATH="${FAKE_BIN}:${PATH}"

  if /usr/local/sbin/fotohavn-deploy "deploy ghcr.io/example/not-approved@sha256:1111"; then
    fail "An unapproved image was accepted."
  fi

  /usr/local/sbin/fotohavn-deploy "deploy ${IMAGE_ONE}"
  assert_file "$IMAGE_ONE" /var/lib/fotohavn/current-image
  [[ ! -e /var/lib/fotohavn/previous-image ]] \
    || fail "A previous image was recorded on the first deployment."

  /usr/local/sbin/fotohavn-deploy "deploy ${IMAGE_TWO}"
  assert_file "$IMAGE_TWO" /var/lib/fotohavn/current-image
  assert_file "$IMAGE_ONE" /var/lib/fotohavn/previous-image

  if /usr/local/sbin/fotohavn-deploy "deploy ${IMAGE_BAD}"; then
    fail "An unhealthy image was accepted."
  fi
  assert_file "$IMAGE_TWO" /var/lib/fotohavn/current-image
  assert_file "$IMAGE_ONE" /var/lib/fotohavn/previous-image
  assert_file "FOTOHAVN_IMAGE=${IMAGE_TWO}" /opt/fotohavn/.env

  /usr/local/sbin/fotohavn-deploy rollback
  assert_file "$IMAGE_ONE" /var/lib/fotohavn/current-image
  assert_file "$IMAGE_TWO" /var/lib/fotohavn/previous-image

  printf 'FotoHAVN deploy state-machine tests passed.\n'
}

main "$@"
