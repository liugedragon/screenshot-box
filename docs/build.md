# Build from source

English · [简体中文](zh-CN/build.md)

## Requirements

- Windows x64; Windows 11 is the primary development and test environment.
- .NET SDK **10.0.401**.
- Windows PowerShell 5.1 or PowerShell 7.

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/fetch-models.ps1
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 0.1.4
```

Scripts prefer `.tools/dotnet/dotnet.exe`, otherwise the installed SDK. [`global.json`](../global.json) pins the SDK patch range. Each project's `packages.lock.json` pins direct and transitive dependencies; restore uses `--locked-mode`.

## Models and publishing

[`fetch-models.ps1`](../scripts/fetch-models.ps1) downloads the Chinese recognition model and dictionary during development. It checks byte counts and SHA-256 before replacing files. Sources and hashes are in [`models/chinese/sources.json`](../models/chinese/sources.json).

The released app reads its adjacent `models` directory; OCR needs no connection. Publishing targets **win-x64 CPU**, with the .NET runtime and detection, orientation, and Chinese recognition models. WPF resources, ONNX Runtime, and SkiaSharp require the complete directory, so single-file publishing and trimming are disabled. See [packaging and installation](distribution.md) for installer builds.

## Tests

[`build.ps1`](../scripts/build.ps1) restores locked dependencies, builds the app, and runs Core tests. Capture annotations use a separate [synthetic pixel test project](../tests/ScreenshotBox.Capture.Probe/README.md). [Windows CI](../.github/workflows/windows.yml) runs both.

The release executable provides an integration self-test using synthetic data and a separate directory:

```powershell
.\artifacts\ScreenshotBox-0.1.4-win-x64\ScreenshotBox.exe --self-test --data-dir E:\temp\ScreenshotBox-test
```

Results are written to `self-test.json` in that directory. Checks include Chinese OCR, source-pixel line boxes, literal Chinese searches, clipboard readback, and backup restore. External-app pasting, multi-monitor dragging, and physically offline operation need separate checks; see the [validation record](validation.md).

### Background startup

After building, run the isolated startup checks:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/test-startup.ps1
```

Use `-ExePath` for an extracted release exe and `-OutputDirectory` for a new result directory. The script checks English and Simplified Chinese separately, with 13 checks per language. Each run uses a unique instance identifier and library path; user settings are not saved, and their file hash is checked before and after.

Checks cover native window visibility, tray availability, live hotkey registration, duplicate background and normal launches, and returning to the tray after closing the window. Results are written to each language directory's `startup-test.json`. Actual sign-out/sign-in and Task Manager enable/disable require separate manual checks.

## Regenerate icons (optional)

Normal builds use the committed `assets/screenshotbox.ico`. To edit and regenerate the icon, use [build-icons.py](../scripts/build-icons.py) with Python 3, librsvg, PyGObject, Pycairo, and Pillow on Linux. The script header lists installation commands and dependency sources. These tools are used only during asset development; the Windows application does not require them.

Models, build tools, and generated files are excluded by `.gitignore`.
