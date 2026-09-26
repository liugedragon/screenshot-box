# Packaging and installation

English · [简体中文](zh-CN/distribution.md)

## Build release packages

The installation wizard requires **Windows 11 x64** (build 22000 or later). Windows 11 is the primary development and test environment. The portable executable targets Windows 10 build 19041 or later, but Windows 10 compatibility has not been tested. See the [build guide](build.md) for requirements.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 0.1.4
powershell -ExecutionPolicy Bypass -File scripts/installer.ps1 -Version 0.1.4
```

`package.ps1` builds and tests, then creates a complete release directory and `artifacts/ScreenshotBox-0.1.4-win-x64.zip`. Packages include the .NET runtime, Windows x64 native components, OCR models, licenses, dependency lock files, and the per-file checksum inventory `FILE-SHA256SUMS.txt`. The ZIP's SHA-256 is written to an adjacent `.sha256` file.

`installer.ps1` uses Inno Setup to package the existing release directory as `ScreenshotBox-0.1.4-win-x64-setup.exe`, with a separate checksum. The default compiler is `.tools/inno/ISCC.exe`; specify another with `-CompilerPath`.

## Installer and ZIP

The wizard supports Simplified Chinese and English. It defaults to the current user's `%LOCALAPPDATA%\Programs\ScreenshotBox`; the folder page accepts another writable location. It creates Start menu and Windows uninstall entries without requiring administrator rights.

For ZIP use, extract the complete directory and run `ScreenshotBox.exe`. No SDK, Python, or separate .NET runtime installation is needed. Keep the native libraries and models together with the executable.

The ZIP also includes [`installer/install.ps1`](../installer/install.ps1) and [`installer/uninstall.ps1`](../installer/uninstall.ps1). Use `-Destination` to select a folder. From 0.1.4, the script installer registers sign-in startup by default; pass `-NoAutoStart` to opt out. Its uninstaller removes the Run entry only when it points to that installation. It removes only listed application files and preserves extra files. Exit the app from the tray before upgrading or uninstalling.

The application language is independent of the wizard. It defaults to the Windows display language: Chinese display languages use Simplified Chinese, others use English. Settings offers System, Simplified Chinese, or English, applied after restart.

## Sign-in startup (0.1.4)

The installation wizard defaults to starting ScreenshotBox when the current user signs in. Its startup task can be deselected. The final launch option also starts the app in the tray. Setup writes a string value named `ScreenshotBox` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, containing the quoted installed exe path followed by `--background`.

Windows manages the entry through Task Manager's Startup apps. Both installers keep the value name stable and do not write `StartupApproved`, so an existing disabled state is retained. Deselecting the task on upgrade removes the Run value; uninstall removes it as well.

A Run command longer than 260 characters is rejected before installation. Choose a shorter installation directory or deselect sign-in startup. Portable execution does not add this registry value.

## User data

Uninstalling preserves settings and the library under `%LOCALAPPDATA%\ScreenshotBox`, and any other library directory chosen by the user. Move data through the app's migration, backup, or restore functions; do not copy only `library.db` while the app is running.

## Release checks and signing

Release checks cover installation, offline startup, capture, clipboard, OCR, search, backup restore, and uninstall. Version-specific environments and results are in [validation](validation.md); untested conditions are in [known limitations](limitations.md).

The app and installer currently have no project Authenticode signature. Windows may show an unknown publisher.
