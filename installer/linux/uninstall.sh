#!/usr/bin/env bash
set -euo pipefail
prefix="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
while (($#)); do
  case "$1" in
    --prefix) [[ $# -ge 2 ]] || { echo '--prefix requires a directory.' >&2; exit 2; }; prefix="$2"; shift 2 ;;
    --help|-h) echo 'Usage: bash installer/linux/uninstall.sh [--prefix DIRECTORY]'; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done
[[ $EUID -ne 0 ]] || { echo 'Run this uninstaller as your normal user, without sudo.' >&2; exit 1; }
prefix="$(realpath -m -- "$prefix")"
[[ "$prefix" != / && "$prefix" != "$HOME" && ! -L "$prefix" ]] || { echo 'Invalid installation directory.' >&2; exit 1; }
marker="$prefix/.screenshotbox-installed-files"
[[ -f "$marker" && "$(head -n 1 -- "$marker")" == 'ScreenshotBox installation v1' ]] || { echo 'This directory was not installed by install.sh.' >&2; exit 1; }
if command -v fuser >/dev/null && [[ -f "$prefix/ScreenshotBox.Linux" ]] && fuser "$prefix/ScreenshotBox.Linux" >/dev/null 2>&1; then
  echo 'Quit ScreenshotBox before uninstalling.' >&2; exit 1
fi
mapfile -t files < <(tail -n +2 -- "$marker")
for relative in "${files[@]}"; do
  case "/$relative/" in *'/../'*|*'/./'*|*'//'*) echo 'Invalid installation manifest path.' >&2; exit 1 ;; esac
  resolved="$(realpath -m -- "$prefix/$relative")"
  [[ -n "$relative" && "$relative" != /* && "$resolved" == "$prefix/"* && ! -L "$prefix/$relative" ]] || { echo 'Invalid installation manifest path.' >&2; exit 1; }
done
xdg_data="${XDG_DATA_HOME:-$HOME/.local/share}"
[[ "$xdg_data" == /* ]] || xdg_data="$HOME/.local/share"
desktop="$xdg_data/applications/ScreenshotBox-linux.desktop"
if [[ -f "$desktop" ]] && grep -Fxq -- "X-ScreenshotBox-InstallPath=$prefix" "$desktop"; then rm -- "$desktop"; fi
for relative in "${files[@]}"; do
  if [[ -f "$prefix/$relative" ]]; then rm -- "$prefix/$relative"; fi
done
rm -- "$marker"
find "$prefix" -depth -type d -empty -delete
if command -v update-desktop-database >/dev/null && [[ -d "$(dirname -- "$desktop")" ]]; then update-desktop-database "$(dirname -- "$desktop")" >/dev/null 2>&1 || true; fi
printf '%s\n' 'ScreenshotBox removed. Library data, settings and files outside the installation manifest were preserved.'
