# Build on Windows

[简体中文](../build.md) | English

Use Windows x64 and .NET SDK **10.0.401**. Windows 11 is the primary development and test target. Run commands from the repository root.

```powershell
pwsh -File scripts/fetch-models.ps1
pwsh -File scripts/build.ps1
pwsh -File scripts/package.ps1 -Version 0.1.2
```

The scripts also work with Windows PowerShell 5.1. They use `.tools/dotnet/dotnet.exe` if present, otherwise the installed SDK. [`global.json`](../../global.json) pins the SDK patch range. Each project's `packages.lock.json` locks direct and transitive dependencies; restore uses `--locked-mode`. Do not delete lock files or upgrade dependencies blindly to work around a build failure.

## Models and publishing

[`fetch-models.ps1`](../../scripts/fetch-models.ps1) downloads the Chinese recognition model and dictionary during development. It verifies each file's size and SHA-256 before replacing it. The pinned URLs and hashes are in [`models/chinese/sources.json`](../../models/chinese/sources.json).

The released app reads the adjacent `models` directory. It does not download models or need a network connection for OCR.

Publishing is self-contained for **win-x64 CPU**. Single-file publishing and trimming are disabled: WPF resources, ONNX Runtime, and SkiaSharp need their complete dependencies. The ZIP includes the .NET runtime, Chinese mobile recognition model, detection model, and text-line orientation model. Keep the full directory together; copying only the executable will not work.

See [distribution and installation](distribution.md) for building an installer, changing installation paths, and package contents.

## Tests

[`build.ps1`](../../scripts/build.ps1) restores locked dependencies, builds the app, and runs the Core tests, including storage, search, and rectangle geometry checks. The separate synthetic capture/annotation probe is also run by [Windows CI](../../.github/workflows/windows.yml); its standalone project and instructions are in [`tests/ScreenshotBox.Capture.Probe`](../../tests/ScreenshotBox.Capture.Probe).

The release executable has an integration self-test. It uses synthetic content rather than importing real user screenshots:

```powershell
.\artifacts\ScreenshotBox-0.1.2-win-x64\ScreenshotBox.exe --self-test --data-dir E:\temp\ScreenshotBox验收
```

Read `self-test.json` in that data directory after it finishes. It covers real Chinese OCR, line boxes in original-image pixels, two-character Chinese words and symbol queries, image clipboard readback, and reopening data after restoring a backup into a new directory.

This self-test does **not** replace pasting into common external applications, dragging across real monitors, or testing with the network physically disconnected. See the [validation record](validation.md).

The installer and portable ZIP preserve user data. Uninstall does not delete the library. Build tools and generated outputs are excluded from Git.
