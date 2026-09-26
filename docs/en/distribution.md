# Distribution and installation

[简体中文](../distribution.md) | English

The application targets **Windows 10 version 2004 (build 19041) or later, x64**. Windows 11 is the primary test target; the target framework's minimum OS version is not a claim that every older Windows configuration has been tested.

## Build a release

Source builds use .NET SDK **10.0.401**. The scripts prefer `.tools/dotnet/dotnet.exe`, falling back to the installed SDK. Dependencies are pinned in lock files. [`scripts/build.ps1`](../../scripts/build.ps1) restores in locked mode, builds the app, and runs the Core tests.

```powershell
pwsh -File scripts/package.ps1 -Version 0.1.2
pwsh -File scripts/installer.ps1 -Version 0.1.2
```

[`package.ps1`](../../scripts/package.ps1) builds and tests before creating the self-contained `artifacts/ScreenshotBox-0.1.2-win-x64.zip`. Users do not need Python, a separate .NET installation, or an account. Chinese OCR models are bundled.

The package keeps only Windows x64 native components and includes dependency lock files, licenses, and `FILE-SHA256SUMS.txt`, a per-file checksum manifest. The ZIP's SHA-256 is written to `ScreenshotBox-0.1.2-win-x64.sha256` alongside it.

[`installer.ps1`](../../scripts/installer.ps1) uses Inno Setup to turn the existing release directory into `ScreenshotBox-0.1.2-win-x64-setup.exe`, with a separate checksum file. It looks for `.tools/inno/ISCC.exe` by default; use `-CompilerPath` to specify another compiler.

## Installer or portable ZIP

The installer offers Simplified Chinese and English. Its default destination is the current user's `%LOCALAPPDATA%\Programs\ScreenshotBox`; choose another writable location on the folder selection page. It creates Start menu entries and an uninstall entry in Windows Installed Apps. Administrator rights are not required.

For portable use, extract the complete ZIP and run `ScreenshotBox.exe`. Do not separate the executable from its runtime, native libraries, and models.

The ZIP also includes [`installer/install.ps1`](../../installer/install.ps1) and [`installer/uninstall.ps1`](../../installer/uninstall.ps1) for users who prefer scripts to the installation wizard. The install script supports `-Destination`. The uninstall script removes only files listed in its manifest and preserves additional files. Exit ScreenshotBox normally before either script replaces or removes application files.

## User data and removal

Uninstalling removes application files and shortcuts but keeps settings and the library under `%LOCALAPPDATA%\ScreenshotBox`, along with any library directory the user selected elsewhere.

Use the app's ZIP backup and restore functions to move data. Do not copy only `library.db` while the app is running.

## Release checks and signatures

Before publishing, check installation, first offline launch, region capture, clipboard behavior, OCR, search, backup/restore, and uninstall on a real Windows desktop. Automated storage and geometry tests are not evidence that mixed-DPI monitors, lock screens, Remote Desktop, or every clipboard target have been tested. The [validation record](validation.md) distinguishes completed checks from unverified scenarios.

The current build has no project-owned Authenticode signature. Windows may show an unrecognized publisher. The Inno Setup compiler's official signature has been verified; this does **not** sign the generated ScreenshotBox executable or installer.
