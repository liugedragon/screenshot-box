#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C
repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
root="$(mktemp -d "$repo/artifacts/linux-installer-validation-XXXXXXXX")"
export XDG_DATA_HOME="$root/xdg-data" XDG_CONFIG_HOME="$root/xdg-config"
fixture="$root/release"
mkdir -p "$fixture/installer/linux" "$fixture/assets" "$fixture/docs" "$XDG_DATA_HOME/ScreenshotBox/library" "$XDG_CONFIG_HOME/autostart"
cp -- "$repo/installer/linux/"*.sh "$fixture/installer/linux/"
printf '#!/usr/bin/env bash\nexit 0\n' > "$fixture/ScreenshotBox.Linux"
printf '<svg xmlns="http://www.w3.org/2000/svg"/>\n' > "$fixture/assets/screenshotbox.svg"
printf 'Release document\n' > "$fixture/docs/page.txt"
printf 'An older release file\n' > "$fixture/obsolete.txt"
printf 'Synthetic library image\n' > "$XDG_DATA_HOME/ScreenshotBox/library/image.png"
printf 'Unrelated startup entry\n' > "$XDG_CONFIG_HOME/autostart/unrelated.desktop"
checks="$root/checks.tsv"
touch "$checks"
trap 'python3 - "$checks" "$root/result.json" <<"PY"
import json,pathlib,sys
checks=[{"name":line.split("\t")[0],"passed":line.split("\t")[1]=="true"} for line in pathlib.Path(sys.argv[1]).read_text().splitlines()]
pathlib.Path(sys.argv[2]).write_text(json.dumps({"test":"Isolated per-user Linux installer","checks":checks,"scope":"Synthetic release, custom Unicode path and isolated XDG directories; no real menu launch or application process test."},indent=2)+"\n")
PY' EXIT
pass() { printf '%s\ttrue\n' "$1" >> "$checks"; printf '%s: true\n' "$1"; }
fail() { printf '%s\tfalse\n' "$1" >> "$checks"; echo "$1: false" >&2; exit 1; }
manifest() { (cd -- "$fixture" && find . -type f ! -name FILE-SHA256SUMS.txt -print0 | LC_ALL=C sort -z | xargs -0 sha256sum | sed 's/  \.\//  /' > FILE-SHA256SUMS.txt); }
install_to() { bash "$fixture/installer/linux/install.sh" --prefix "$1" > "$root/$2.out.txt" 2> "$root/$2.err.txt"; }
uninstall_from() { bash "$fixture/installer/linux/uninstall.sh" --prefix "$1" > "$root/$2.out.txt" 2> "$root/$2.err.txt"; }
manifest
prefix="$root/安装位置 空格/ScreenshotBox"
install_to "$prefix" install
[[ -x "$prefix/ScreenshotBox.Linux" && -f "$prefix/.screenshotbox-installed-files" ]] && pass 'Custom Unicode and space path installs' || fail 'Custom Unicode and space path installs'
desktop="$XDG_DATA_HOME/applications/ScreenshotBox-linux.desktop"
grep -Fxq "Exec=\"$prefix/ScreenshotBox.Linux\"" "$desktop" && pass 'Desktop executable path is quoted' || fail 'Desktop executable path is quoted'
[[ -f "$prefix/assets/screenshotbox.svg" ]] && grep -Fxq "Icon=$prefix/assets/screenshotbox.svg" "$desktop" && pass 'Desktop launcher uses installed SVG' || fail 'Desktop launcher uses installed SVG'
[[ "$(cat "$XDG_CONFIG_HOME/autostart/unrelated.desktop")" == 'Unrelated startup entry' && "$(find "$XDG_CONFIG_HOME/autostart" -type f | wc -l)" == 1 ]] && pass 'Installer leaves login startup unchanged' || fail 'Installer leaves login startup unchanged'
userfile="$prefix/user-created.txt"
printf 'Preserve me\n' > "$userfile"
# Refuse a corrupted prior ownership manifest before touching the installation.
cp -- "$prefix/.screenshotbox-installed-files" "$root/marker.original"
original_app_hash="$(sha256sum "$prefix/ScreenshotBox.Linux" | cut -d ' ' -f1)"
original_desktop_hash="$(sha256sum "$desktop" | cut -d ' ' -f1)"
printf '../outside/victim.txt\n' >> "$prefix/.screenshotbox-installed-files"
if install_to "$prefix" previous-marker-rejected; then fail 'Update rejects a corrupt previous manifest before changes'; fi
[[ "$(sha256sum "$prefix/ScreenshotBox.Linux" | cut -d ' ' -f1)" == "$original_app_hash" && "$(sha256sum "$desktop" | cut -d ' ' -f1)" == "$original_desktop_hash" ]] && pass 'Update rejects a corrupt previous manifest before changes' || fail 'Update rejects a corrupt previous manifest before changes'
cp -- "$root/marker.original" "$prefix/.screenshotbox-installed-files"
# Inject a copy failure only into this invocation's staging copies.
mkdir -p "$root/failure-bin"
cat > "$root/failure-bin/cp" <<'WRAPPER'
#!/usr/bin/env bash
for argument in "$@"; do [[ "$argument" != --remove-destination ]] || exit 91; done
exec /bin/cp "$@"
WRAPPER
chmod +x "$root/failure-bin/cp"
if PATH="$root/failure-bin:$PATH" install_to "$prefix" staged-copy-failure; then fail 'Staging failure leaves previous app and launcher unchanged'; fi
[[ "$(sha256sum "$prefix/ScreenshotBox.Linux" | cut -d ' ' -f1)" == "$original_app_hash" && "$(sha256sum "$desktop" | cut -d ' ' -f1)" == "$original_desktop_hash" ]] && cmp -s "$root/marker.original" "$prefix/.screenshotbox-installed-files" && [[ -f "$userfile" ]] && pass 'Staging failure leaves previous app and launcher unchanged' || fail 'Staging failure leaves previous app and launcher unchanged'
rm -- "$root/failure-bin/cp"
# Fail once at launcher publication, after the application-directory swap.
cat > "$root/failure-bin/mv" <<'WRAPPER'
#!/usr/bin/env bash
source_argument="$2"; destination_argument="$3"
if [[ "$destination_argument" == "$SCRIPT_TEST_DESKTOP" && "$source_argument" == "$SCRIPT_TEST_DESKTOP."* && ! -f "$SCRIPT_TEST_FAILURE_MARKER" ]]; then
  touch "$SCRIPT_TEST_FAILURE_MARKER"; exit 92
fi
exec /bin/mv "$@"
WRAPPER
chmod +x "$root/failure-bin/mv"
if PATH="$root/failure-bin:$PATH" SCRIPT_TEST_DESKTOP="$desktop" SCRIPT_TEST_FAILURE_MARKER="$root/once-failed" install_to "$prefix" swap-rollback; then fail 'Failed publication rolls back application and launcher'; fi
[[ -f "$root/once-failed" && "$(sha256sum "$prefix/ScreenshotBox.Linux" | cut -d ' ' -f1)" == "$original_app_hash" && "$(sha256sum "$desktop" | cut -d ' ' -f1)" == "$original_desktop_hash" ]] && cmp -s "$root/marker.original" "$prefix/.screenshotbox-installed-files" && [[ -f "$userfile" ]] && pass 'Failed publication rolls back application and launcher' || fail 'Failed publication rolls back application and launcher'
rm -- "$root/failure-bin/mv"
printf 'User-owned content\n' > "$prefix/new-file.txt"
printf 'New package content\n' > "$fixture/new-file.txt"
manifest
if install_to "$prefix" unowned-collision; then fail 'New package files cannot overwrite unowned user files'; fi
[[ "$(cat "$prefix/new-file.txt")" == 'User-owned content' ]] && pass 'New package files cannot overwrite unowned user files' || fail 'New package files cannot overwrite unowned user files'
rm -- "$fixture/new-file.txt"
manifest
oldhash="$(sha256sum "$prefix/ScreenshotBox.Linux" | cut -d ' ' -f1)"
printf 'Corrupted\n' > "$fixture/ScreenshotBox.Linux"
if install_to "$prefix" checksum-rejected; then fail 'Corrupt release is rejected before overwriting files'; fi
[[ "$(sha256sum "$prefix/ScreenshotBox.Linux" | cut -d ' ' -f1)" == "$oldhash" ]] && grep -q 'did NOT match' "$root/checksum-rejected.err.txt" && pass 'Corrupt release is rejected before overwriting files' || fail 'Corrupt release is rejected before overwriting files'
printf '#!/usr/bin/env bash\nexit 0\n' > "$fixture/ScreenshotBox.Linux"
rm -- "$fixture/obsolete.txt"
printf 'New release document\n' > "$fixture/docs/new.txt"
manifest
install_to "$prefix" upgrade
[[ -f "$prefix/docs/new.txt" && -f "$prefix/obsolete.txt" ]] && grep -Fxq obsolete.txt "$prefix/.screenshotbox-installed-files" && pass 'Upgrade retains ownership of older installed files' || fail 'Upgrade retains ownership of older installed files'
[[ "$(cat "$userfile")" == 'Preserve me' ]] && pass 'Upgrade preserves user-added files' || fail 'Upgrade preserves user-added files'
unknown="$root/unregistered"
mkdir -p "$unknown"; printf 'Unknown content\n' > "$unknown/data.txt"
if install_to "$unknown" nonempty-rejected; then fail 'Unregistered nonempty directory is rejected'; fi
[[ "$(cat "$unknown/data.txt")" == 'Unknown content' ]] && pass 'Unregistered nonempty directory is rejected' || fail 'Unregistered nonempty directory is rejected'
otherprefix="$root/another installation"
if install_to "$otherprefix" launcher-owner-rejected; then fail 'Launcher owned by another installation blocks replacement'; fi
[[ ! -e "$otherprefix" ]] && pass 'Launcher owned by another installation blocks replacement' || fail 'Launcher owned by another installation blocks replacement'
cp -- "$desktop" "$root/desktop.original"
printf '[Desktop Entry]\nX-ScreenshotBox-InstallPath=/another/installation\n' > "$desktop"
uninstall_from "$prefix" uninstall-other-launcher
[[ -f "$desktop" ]] && grep -Fxq X-ScreenshotBox-InstallPath=/another/installation "$desktop" && pass 'Uninstall preserves another installation launcher' || fail 'Uninstall preserves another installation launcher'
[[ ! -f "$prefix/ScreenshotBox.Linux" && ! -f "$prefix/obsolete.txt" ]] && pass 'Uninstall removes current and older managed files' || fail 'Uninstall removes current and older managed files'
[[ -f "$userfile" && -f "$XDG_DATA_HOME/ScreenshotBox/library/image.png" ]] && pass 'Uninstall preserves user files and library data' || fail 'Uninstall preserves user files and library data'
# All subsequent launcher changes remain inside the randomized XDG directory.
rm -- "$desktop"
install_to "$otherprefix" install-normal-uninstall
uninstall_from "$otherprefix" uninstall-normal
[[ ! -e "$desktop" && ! -e "$otherprefix" ]] && pass 'Normal uninstall removes its launcher and empty application directory' || fail 'Normal uninstall removes its launcher and empty application directory'
# A tampered manifest must not delete a file outside the registered fixture prefix.
escape="$root/tampered"
mkdir -p "$escape" "$root/outside"
printf 'ScreenshotBox installation v1\n../outside/victim.txt\n' > "$escape/.screenshotbox-installed-files"
printf 'Do not remove\n' > "$root/outside/victim.txt"
if uninstall_from "$escape" traversal-rejected; then fail 'Uninstall rejects traversal before deleting files'; fi
[[ "$(cat "$root/outside/victim.txt")" == 'Do not remove' ]] && pass 'Uninstall rejects traversal before deleting files' || fail 'Uninstall rejects traversal before deleting files'
# Existing installation paths through a directory symlink cannot escape the prefix.
symlinkprefix="$root/symlink-target"
mkdir -p "$symlinkprefix"
printf 'ScreenshotBox installation v1\n' > "$symlinkprefix/.screenshotbox-installed-files"
ln -s "$root/outside" "$symlinkprefix/docs"
if install_to "$symlinkprefix" symlink-rejected; then fail 'Install rejects a directory symlink escaping the prefix'; fi
[[ ! -e "$root/outside/page.txt" ]] && pass 'Install rejects a directory symlink escaping the prefix' || fail 'Install rejects a directory symlink escaping the prefix'
printf 'Result: %s\n' "$root/result.json"
