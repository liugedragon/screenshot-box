# Linux installation checks

[简体中文](README.zh-CN.md)

```bash
bash tests/ScreenshotBox.Linux.Installer.Probe/verify.sh
python3 tests/ScreenshotBox.Linux.Installer.Probe/verify-archive.py
```

The probe creates a small synthetic release and isolated XDG data/configuration directories under `artifacts/`. It uses a custom installation path containing Chinese characters and spaces. No launcher or startup entry is written to the current user's desktop directories.

Checks cover corrupt previous manifests, forced staging and publication failures with rollback, rejection of unowned file collisions, checksum rejection before overwrite, update ownership, preservation of user files and library data, desktop launcher ownership, normal uninstall, and rejection of manifest traversal or directory symlinks escaping the installation prefix. Logs and `result.json` stay in the generated directory.

Bash, GNU coreutils and Python 3 are required for this development probe. The production installation scripts do not require Python. These checks do not launch the desktop menu entry or verify a real installed application process.

The archive check verifies 644/755 permissions, portable owner metadata, payload integrity, and rejection of symlinks. Archive permissions are normalized even when the build filesystem ignores `chmod`.
