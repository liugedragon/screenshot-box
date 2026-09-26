<div align="center">

<img src="assets/screenshotbox.svg" width="64" height="64" alt="ScreenshotBox icon">

# ScreenshotBox

**Save screenshots locally. Find them by their text.**

[English](README.md) · [简体中文](README.zh-CN.md)

[![Windows CI](https://github.com/liugedragon/screenshot-box/actions/workflows/windows.yml/badge.svg?branch=main)](https://github.com/liugedragon/screenshot-box/actions/workflows/windows.yml)
[![Release](https://img.shields.io/github/v/release/liugedragon/screenshot-box?include_prereleases&label=release)](https://github.com/liugedragon/screenshot-box/releases)
[![MIT](https://img.shields.io/badge/license-MIT-1769C2)](LICENSE)

[**Download installer**](https://github.com/liugedragon/screenshot-box/releases/download/v0.1.3/ScreenshotBox-0.1.3-win-x64-setup.exe) · [**Download ZIP**](https://github.com/liugedragon/screenshot-box/releases/download/v0.1.3/ScreenshotBox-0.1.3-win-x64.zip) · [User guide](docs/usage.md) · [Report a problem](https://github.com/liugedragon/screenshot-box/issues/new/choose)

</div>

ScreenshotBox is a Windows screenshot tool and image library. Select a screen region with a hotkey and save it; text recognition runs in the background. Later, search by title, notes, tags, or text in the image. Class schedules, order numbers, and warranty dates stay on your computer.

For **Windows 11 x64**. No account, API key, Python, or dedicated GPU required. OCR runs locally on the CPU with bundled models. **0.1.3 is a prerelease**; see [known limitations](docs/limitations.md) for current issues and untested configurations.

![Screenshot library with text search and image details](docs/images/en/library-light.png)

*Version 0.1.3, with English sample documents and metadata. All sample content is synthetic. The interface follows the Windows display language: Chinese for Chinese display languages, English for all others. Choose System, 简体中文, or English in Settings; restart to apply.*

## Get started

1. Press **Ctrl+Alt+S** and drag a selection. Move it or resize it with eight handles, then add a pen stroke, arrow, rectangle, or mosaic if needed.
2. Press **Enter** to save and copy, then return to your previous app. The image is saved immediately; OCR runs in the background.
3. Open the library from the tray and search for a word in the screenshot. Select an image and press **Space** to preview it with matching text lines highlighted.

You can change the capture hotkey to `Alt+A`, `Ctrl+Shift+Q`, or another combination. The app checks common reserved system shortcuts and global hotkey availability; failed registration leaves the old hotkey active. **Esc** cancels without creating an image or entry.

## Features

| Feature | Details |
| --- | --- |
| Capture and copy | Region selection, movement, eight resize handles; save and copy, copy only, or save as PNG. |
| Annotation | Pen, arrow, outline rectangle, eraser, mosaic; adjustable sizes, RGB/Hex palette, undo and redo. |
| Text search | Titles, notes, tags, and OCR text; short Chinese phrases, mixed text, dates, and IDs. |
| Organization | Import PNG/JPEG, add tags, notes, and stars; restore deleted items from the recycle bin. |
| Image preview | Zoom, pan, fit to window, or actual pixel size; whole-line OCR match highlights. |
| Local library | Custom library location, ZIP backup and restore. |
| Appearance and language | System, light, and dark themes; System, Simplified Chinese, or English. |

<details>
<summary>Capture tools, text search, and dark theme</summary>

![English capture toolbar with pen and arrow annotations](docs/images/en/capture.png)

![RGB sliders, Hex input, and color preview](docs/images/en/palette.png)

![OCR search highlights in the original image](docs/images/en/preview.png)

![English dark library](docs/images/en/library-dark.png)

More screenshots are in the [UI inspection record](docs/ui-review.md).

</details>

## Installation and data

- **Installer**: choose an installation folder; Start menu and uninstall entries are created.
- **ZIP**: extract the complete directory and run `ScreenshotBox.exe`.

Both include the .NET runtime, native libraries, and OCR models. Keep the complete directory together. Checksums are on the [release page](https://github.com/liugedragon/screenshot-box/releases/tag/v0.1.3). Exit the old version from the tray before upgrading.

The library defaults to `%LOCALAPPDATA%\ScreenshotBox\library`, separate from the installation. Settings can move it to another folder while retaining the original. The app does not upload images or text, or download models during recognition. Uninstalling preserves your library. Closing the main window leaves the app in the tray; use its menu to quit completely.

OCR may misread low-resolution, handwritten, or busy images. Mosaic is visual pixelation, not secure redaction; erasing it restores the original pixels. Scrolling capture, recording, cloud sync, automatic updates, and editing annotations after saving are not supported. The app and installer are unsigned. Test environments and results are in the [validation record](docs/validation.md).

## Documentation and development

| Guide | Contents |
| --- | --- |
| [User guide](docs/usage.md) | Capture, annotation, search, organization, backups, and settings. |
| [Build](docs/build.md) · [Packaging and installation](docs/distribution.md) | Run from source, create a ZIP or installer. |
| [Architecture](docs/architecture.md) · [Storage and search](docs/storage.md) | Coordinates, queries, background jobs, and recovery. |
| [Design](docs/design.md) · [UI inspection](docs/ui-review.md) | References, styles, and layout records. |
| [Tests](docs/validation.md) · [Limitations](docs/limitations.md) | Test results and supported conditions. |
| [Third-party components](docs/third-party.md) | Components, models, versions, licenses, and hashes. |

Requires Windows x64 and **.NET SDK 10.0.401**. Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/fetch-models.ps1
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 0.1.3
```

The first model download requires a connection. Dependencies are pinned in `packages.lock.json`; building an installer requires Inno Setup.

See [CONTRIBUTING](CONTRIBUTING.md) for bug reports, documentation fixes, and code contributions, and [ROADMAP](ROADMAP.md) for planned features. If you find the app useful, a Star is welcome.

## License

Application code and the original icon use [MIT](LICENSE). WPF UI, RapidOcrNet, PaddleOCR models, ONNX Runtime, SQLite, and other components retain their own licenses; see [third-party notices](docs/third-party.md). Layout references include Eagle, ShareX, and PowerToys; see the [design notes](docs/design.md).
