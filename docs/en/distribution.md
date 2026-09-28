# Packaging and installation

[English](../distribution.md) · [简体中文](../zh-CN/distribution.md)

## Build release packages

The installation wizard requires **Windows 11 x64** (build 22000 or later). Windows 11 is the primary development and test environment. The portable executable targets Windows 10 build 19041 or later, but Windows 10 compatibility has not been tested. See the [build guide](../build.md) for requirements.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 0.1.4
powershell -ExecutionPolicy Bypass -File scripts/installer.ps1 -Version 0.1.4
```

`package.ps1` builds and tests, then creates a complete release directory and `artifacts/ScreenshotBox-0.1.4-win-x64.zip`. Packages include the .NET runtime, Windows x64 native components, OCR models, licenses, dependency lock files, and the per-file checksum inventory `FILE-SHA256SUMS.txt`. The ZIP's SHA-256 is written to an adjacent `.sha256` file.

`installer.ps1` uses Inno Setup to package the existing release directory as `ScreenshotBox-0.1.4-win-x64-setup.exe`, with a separate checksum. The default compiler is `.tools/inno/ISCC.exe`; specify another with `-CompilerPath`.

## Installer and ZIP

The wizard supports Simplified Chinese and English. It defaults to the current user's `%LOCALAPPDATA%\Programs\ScreenshotBox`; the folder page accepts another writable location. It creates Start menu and Windows uninstall entries without requiring administrator rights.

For ZIP use, extract the complete directory and run `ScreenshotBox.exe`. No SDK, Python, or separate .NET runtime installation is needed. Keep the native libraries and models together with the executable.

The ZIP also includes [`installer/install.ps1`](../../installer/install.ps1) and [`installer/uninstall.ps1`](../../installer/uninstall.ps1). Use `-Destination` to select a folder. From 0.1.4, the script installer registers sign-in startup by default; pass `-NoAutoStart` to opt out. Its uninstaller removes the Run entry only when it points to that installation. It removes only listed application files and preserves extra files. Exit the app from the tray before upgrading or uninstalling.

The application language is independent of the wizard. It defaults to the Windows display language: Chinese display languages use Simplified Chinese, others use English. Settings offers System, Simplified Chinese, or English, applied after restart.

## Sign-in startup (0.1.4)

The installation wizard defaults to starting ScreenshotBox when the current user signs in. Its startup task can be deselected. The final launch option also starts the app in the tray. Setup writes a string value named `ScreenshotBox` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, containing the quoted installed exe path followed by `--background`.

Windows manages the entry through Task Manager's Startup apps. Both installers keep the value name stable and do not write `StartupApproved`, so an existing disabled state is retained. Deselecting the task on upgrade removes the Run value; uninstall removes it as well.

A Run command longer than 260 characters is rejected before installation. Choose a shorter installation directory or deselect sign-in startup. Portable execution does not add this registry value.

## User data

Uninstalling preserves settings and the library under `%LOCALAPPDATA%\ScreenshotBox`, and any other library directory chosen by the user. Move data through the app's migration, backup, or restore functions; do not copy only `library.db` while the app is running.

## Release checks and signing

Release checks cover installation, offline startup, capture, clipboard, OCR, search, backup restore, and uninstall. Version-specific environments and results are in [validation](../validation.md); untested conditions are in [known limitations](../limitations.md).

The published **0.1.4** installer and `ScreenshotBox.exe` have no Authenticode signature. Edge may say the installer is “not commonly downloaded.” Microsoft describes this as a [download-reputation warning](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation), not a malware finding. It can also appear for newly signed files. Do not disable browser or Windows protection to install the app.

Download from the [0.1.4 release](https://github.com/liugedragon/screenshot-box/releases/tag/v0.1.4) and compare the installer hash with the adjacent `.sha256` file. In PowerShell, run this in the download folder:

```powershell
(Get-FileHash .\ScreenshotBox-0.1.4-win-x64-setup.exe -Algorithm SHA256).Hash.ToLowerInvariant()
```

Expected SHA-256: `96b7ddfbad053630706e3c188ea0f22a31549f00df90a016744b100b2220a38e` (88,097,504 bytes). The ZIP hash is `5acb89e67c4032ff4187063ebc41748a6d7640040cba00f861944778f609e1b9`. A match confirms that the downloaded bytes equal the release asset; it does not prove publisher identity or that the code is harmless. If Windows reports a specific malware detection, stop and include its name and the file hash in an issue.

For a future direct-download release, use a code-signing certificate tied to a verified publisher and trusted on clean Windows installations. Both packaging scripts accept `-RequireSignature -CertificateThumbprint <40-hex-digit thumbprint> -TimestampUrl <CA RFC 3161 URL>`; `-SignToolPath` can point to the Windows SDK signer. `package.ps1` signs `ScreenshotBox.exe` before creating the checksums and ZIP. `installer.ps1` requires that signed executable, then asks Inno Setup to sign the generated uninstaller and setup. The scripts stop if required signing or verification fails. Keep private keys outside the repository.

No signed release has been built with this path yet because no release-signing certificate is available on the current build machine. Test the resulting signatures, installation, and download behavior on a clean Windows machine before publishing. A valid signature improves publisher identity and reputation but does not guarantee that a new file will immediately stop showing the warning; [Microsoft documents](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation) both file-hash and publisher reputation. Existing 0.1.4 assets and tag remain unchanged.
