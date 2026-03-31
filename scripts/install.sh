#!/usr/bin/env sh

set -eu

OWNER="alirezanet"
REPO="Lorex"
VERSION="${1:-}"
INSTALL_DIR="${HOME}/.local/bin"
BINARY_NAME="lorex"

info() { printf '%s\n' "[INFO] $1"; }
ok() { printf '%s\n' "[ OK ] $1"; }
warn() { printf '%s\n' "[WARN] $1"; }
fail() { printf '%s\n' "[FAIL] $1" >&2; exit 1; }

need_cmd() {
  command -v "$1" >/dev/null 2>&1 || fail "Required command '$1' was not found."
}

get_tag() {
  if [ -n "$VERSION" ]; then
    case "$VERSION" in
      v*) printf '%s' "$VERSION" ;;
      *) printf 'v%s' "$VERSION" ;;
    esac
    return
  fi

  need_cmd curl
  latest_url="$(curl -fsSLI -o /dev/null -w '%{url_effective}' "https://github.com/${OWNER}/${REPO}/releases/latest")"
  tag="${latest_url##*/}"

  if [ -z "$tag" ] || [ "$tag" = "latest" ]; then
    fail "Could not determine the latest Lorex release tag automatically. Pass a version like ./install.sh 0.0.1 instead."
  fi

  printf '%s' "$tag"
}

get_asset_name() {
  os="$(uname -s)"
  arch="$(uname -m)"

  case "$os" in
    Linux)
      case "$arch" in
        x86_64|amd64) printf '%s' "lorex-linux-x64" ;;
        aarch64|arm64) printf '%s' "lorex-linux-arm64" ;;
        *) fail "Unsupported Linux architecture '$arch'. Lorex currently publishes linux-x64 and linux-arm64 assets." ;;
      esac
      ;;
    Darwin)
      case "$arch" in
        x86_64|amd64) printf '%s' "lorex-osx-x64" ;;
        arm64|aarch64) printf '%s' "lorex-osx-arm64" ;;
        *) fail "Unsupported macOS architecture '$arch'. Lorex currently publishes osx-x64 and osx-arm64 assets." ;;
      esac
      ;;
    *)
      fail "Unsupported operating system '$os'. Use install.ps1 on Windows."
      ;;
  esac
}

ensure_path_hint() {
  case ":$PATH:" in
    *":${INSTALL_DIR}:"*) info "${INSTALL_DIR} is already on PATH." ;;
    *)
      warn "${INSTALL_DIR} is not on PATH."
      printf '%s\n' "Add this line to your shell profile:"
      printf '  export PATH="%s:$PATH"\n' "${INSTALL_DIR}"
      ;;
  esac
}

need_cmd curl

TAG="$(get_tag)"
ASSET_NAME="$(get_asset_name)"
DOWNLOAD_URL="https://github.com/${OWNER}/${REPO}/releases/download/${TAG}/${ASSET_NAME}"

mkdir -p "${INSTALL_DIR}"
TMP_FILE="$(mktemp)"

cleanup() {
  rm -f "${TMP_FILE}"
}
trap cleanup EXIT INT TERM

info "Downloading ${ASSET_NAME} from ${TAG}..."
curl -fsSL -H "User-Agent: lorex-installer" "${DOWNLOAD_URL}" -o "${TMP_FILE}"

TARGET_PATH="${INSTALL_DIR}/${BINARY_NAME}"
mv "${TMP_FILE}" "${TARGET_PATH}"
chmod +x "${TARGET_PATH}"

ok "Installed Lorex to ${TARGET_PATH}"
ensure_path_hint

printf '\n'
printf '%s\n' "Next steps:"
printf '%s\n' "  lorex --version"
printf '%s\n' "  lorex init"
