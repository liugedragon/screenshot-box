#!/usr/bin/env bash
set -euo pipefail
source_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
prefix="$HOME/.local/opt/ScreenshotBox"
while (($#)); do
  case "$1" in
    --prefix) [[ $# -ge 2 ]] || { echo '--prefix requires a directory.' >&2; exit 2; }; prefix="$2"; shift 2 ;;
    --help|-h) echo 'Usage: bash installer/linux/install.sh [--prefix DIRECTORY]'; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done
[[ $EUID -ne 0 ]] || { echo 'Run this installer as your normal user, without sudo.' >&2; exit 1; }
prefix="$(realpath -m -- "$prefix")"
case "$prefix" in *$'\n'*|*$'\r'*|*$'\t'*|*'"'*|*'\'*|*'`'*|*'$'*|*'%'*) echo 'Choose a path without control characters, quotes, backslashes, $, ` or %.' >&2; exit 1 ;; esac
[[ "$prefix" != / && "$prefix" != "$HOME" && ! -L "$prefix" ]] || { echo 'Choose a separate application directory.' >&2; exit 1; }
case "$prefix/" in "$source_dir/"*) echo 'The installation directory cannot be inside the extracted release.' >&2; exit 1 ;; esac
[[ -f "$source_dir/ScreenshotBox.Linux" && -f "$source_dir/FILE-SHA256SUMS.txt" ]] || { echo 'Extract the complete Linux release archive before installing.' >&2; exit 1; }
marker="$prefix/.screenshotbox-installed-files"
if [[ -d "$prefix" ]] && [[ -n "$(find "$prefix" -mindepth 1 -maxdepth 1 -print -quit)" ]]; then
  [[ -f "$marker" && "$(head -n 1 -- "$marker")" == 'ScreenshotBox installation v1' ]] || { echo 'Choose an empty directory or a directory installed by this script.' >&2; exit 1; }
fi
xdg_data="${XDG_DATA_HOME:-$HOME/.local/share}"
[[ "$xdg_data" == /* ]] || xdg_data="$HOME/.local/share"
desktop="$xdg_data/applications/ScreenshotBox-linux.desktop"
if [[ -e "$desktop" ]] && ! grep -Fxq -- "X-ScreenshotBox-InstallPath=$prefix" "$desktop"; then
  echo 'The existing ScreenshotBox desktop launcher belongs to another installation.' >&2; exit 1
fi
if command -v fuser >/dev/null && [[ -f "$prefix/ScreenshotBox.Linux" ]] && fuser "$prefix/ScreenshotBox.Linux" >/dev/null 2>&1; then
  echo 'Quit ScreenshotBox before updating this installation.' >&2; exit 1
fi
valid_path() {
  local relative="$1" resolved current component
  local -a components
  case "/$relative/" in *'/../'*|*'/./'*|*'//'*) return 1 ;; esac
  [[ -n "$relative" && "$relative" != /* && "$relative" != '.screenshotbox-installed-files' ]] || return 1
  resolved="$(realpath -m -- "$prefix/$relative")"
  [[ "$resolved" == "$prefix/"* ]] || return 1
  current="$prefix"
  IFS='/' read -r -a components <<< "$relative"
  for component in "${components[@]}"; do
    current="$current/$component"
    [[ ! -L "$current" ]] || return 1
  done
}
declare -A owned=()
previous=()
if [[ -f "$marker" ]]; then
  mapfile -t previous < <(tail -n +2 -- "$marker")
  for relative in "${previous[@]}"; do
    valid_path "$relative" || { echo 'Invalid previous installation manifest path.' >&2; exit 1; }
    owned["$relative"]=1
  done
fi
files=()
while IFS= read -r line || [[ -n "$line" ]]; do
  [[ "$line" =~ ^[[:xdigit:]]{64}'  '(.+)$ ]] || { echo 'Invalid release checksum manifest.' >&2; exit 1; }
  relative="${BASH_REMATCH[1]}"
  valid_path "$relative" && [[ ! -L "$source_dir/$relative" && -f "$source_dir/$relative" ]] || { echo 'Invalid release or installation path.' >&2; exit 1; }
  if [[ -e "$prefix/$relative" && -z "${owned[$relative]:-}" ]]; then
    echo "An unowned file would be overwritten: $relative" >&2; exit 1
  fi
  files+=("$relative")
done < "$source_dir/FILE-SHA256SUMS.txt"
if [[ -e "$prefix/FILE-SHA256SUMS.txt" && -z "${owned[FILE-SHA256SUMS.txt]:-}" ]]; then echo 'An unowned checksum manifest would be overwritten.' >&2; exit 1; fi
(cd -- "$source_dir" && sha256sum --check --strict FILE-SHA256SUMS.txt >/dev/null)
files+=(FILE-SHA256SUMS.txt)
parent="$(dirname -- "$prefix")"
mkdir -p -- "$parent" "$(dirname -- "$desktop")"
stage="$(mktemp -d "$parent/.screenshotbox-stage-XXXXXXXX")"
backup=""
desktop_temp=""
desktop_backup=""
old_moved=false
new_moved=false
desktop_moved=false
committed=false
cleanup() {
  local status=$?
  trap - EXIT
  set +e
  if [[ "$committed" != true ]]; then
    if [[ "$desktop_moved" == true && -f "$desktop" ]]; then rm -f -- "$desktop"; fi
    if [[ -n "$desktop_backup" && -f "$desktop_backup/launcher.desktop" ]]; then mv -- "$desktop_backup/launcher.desktop" "$desktop"; fi
    if [[ "$new_moved" == true && -d "$prefix" ]]; then rm -rf -- "$prefix"; fi
    if [[ "$old_moved" == true && -d "$backup" ]]; then mv -- "$backup" "$prefix"; fi
  fi
  [[ ! -d "$stage" ]] || rm -rf -- "$stage"
  [[ -z "$desktop_temp" || ! -e "$desktop_temp" ]] || rm -f -- "$desktop_temp"
  if [[ -n "$desktop_backup" && -d "$desktop_backup" ]]; then
    if [[ -e "$desktop_backup/launcher.desktop" ]]; then echo "Launcher rollback copy retained: $desktop_backup" >&2
    else rmdir -- "$desktop_backup"; fi
  fi
  if [[ -n "$backup" && -d "$backup" ]]; then
    if [[ "$committed" == true ]]; then rm -rf -- "$backup"
    else echo "Installation rollback copy retained: $backup" >&2; fi
  fi
  exit "$status"
}
trap cleanup EXIT
if [[ -d "$prefix" ]]; then cp -a -- "$prefix/." "$stage/"; fi
for relative in "${files[@]}"; do
  mkdir -p -- "$(dirname -- "$stage/$relative")"
  # Replace copied owned files without following a file symlink.
  cp -p --remove-destination -- "$source_dir/$relative" "$stage/$relative"
done
chmod u+x -- "$stage/ScreenshotBox.Linux" "$stage/installer/linux/install.sh" "$stage/installer/linux/uninstall.sh"
{ printf '%s\n' 'ScreenshotBox installation v1'; printf '%s\n' "${files[@]}" "${previous[@]}" | sort -u; } > "$stage/.screenshotbox-installed-files"
desktop_temp="$(mktemp "$desktop.XXXXXXXX")"
cat > "$desktop_temp" <<DESKTOP
[Desktop Entry]
Type=Application
Name=ScreenshotBox
Name[zh_CN]=截图资料盒
Comment=Capture screenshots and find them by their text
Comment[zh_CN]=保存截图，按图片中的文字查找
Exec="$prefix/ScreenshotBox.Linux"
Icon=$prefix/assets/screenshotbox.svg
Terminal=false
Categories=Utility;Graphics;
StartupNotify=true
X-ScreenshotBox-InstallPath=$prefix
DESKTOP
chmod 644 -- "$desktop_temp"
if [[ -e "$desktop" ]]; then
  desktop_backup="$(mktemp -d "$(dirname -- "$desktop")/.screenshotbox-launcher-XXXXXXXX")"
  mv -- "$desktop" "$desktop_backup/launcher.desktop"
fi
if [[ -d "$prefix" ]]; then
  backup="$(mktemp -d "$parent/.screenshotbox-backup-XXXXXXXX")"
  rmdir -- "$backup"
  mv -- "$prefix" "$backup"
  old_moved=true
fi
mv -- "$stage" "$prefix"
new_moved=true
mv -- "$desktop_temp" "$desktop"
desktop_moved=true
committed=true
if [[ -n "$desktop_backup" ]]; then rm -f -- "$desktop_backup/launcher.desktop"; fi
if command -v update-desktop-database >/dev/null; then update-desktop-database "$(dirname -- "$desktop")" >/dev/null 2>&1 || true; fi
printf 'Installed: %s\nDesktop launcher: %s\n' "$prefix" "$desktop"
printf '%s\n' 'Library data and settings are stored separately. Login autostart is not configured.'
