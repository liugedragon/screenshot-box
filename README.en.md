<div align="center">

<img src="assets/screenshotbox.svg" width="64" height="64" alt="ScreenshotBox icon">

# ScreenshotBox

**Save a screenshot. Find it later by its text.**

[简体中文](README.md) · English

[![Windows CI](https://github.com/liugedragon/screenshot-box/actions/workflows/windows.yml/badge.svg?branch=main)](https://github.com/liugedragon/screenshot-box/actions/workflows/windows.yml)
[![Release](https://img.shields.io/github/v/release/liugedragon/screenshot-box?include_prereleases&label=release)](https://github.com/liugedragon/screenshot-box/releases)
[![MIT](https://img.shields.io/badge/license-MIT-1769C2)](LICENSE)

[**Download installer**](https://github.com/liugedragon/screenshot-box/releases/download/v0.1.2/ScreenshotBox-0.1.2-win-x64-setup.exe) · [**Download ZIP**](https://github.com/liugedragon/screenshot-box/releases/download/v0.1.2/ScreenshotBox-0.1.2-win-x64.zip) · [User guide](docs/en/usage.md) · [Report a problem](https://github.com/liugedragon/screenshot-box/issues/new/choose)

</div>

Class schedules, order numbers, warranty dates: taking screenshots is easy; finding them a week later is harder. ScreenshotBox keeps the images and their recognized text together on your computer. Select a region, annotate it, save and copy, then search for a word or number to find the image again. Matching text lines are highlighted on the original.

For **Windows 11 x64**. No account, API key, Python, or dedicated GPU required. Chinese and English OCR runs locally on the CPU. **0.1.2 is a prerelease**; check the [known limitations](docs/en/limitations.md) before using it.

![Screenshot library with text search and item details](docs/ui-review-images/0.1.2/ui-light.png)

*Actual application, shown with synthetic test images. The application UI is currently primarily Chinese. English documentation does not imply an English UI.*

## Get started

1. After installing, press **Ctrl+Alt+S** and drag a rectangle. Move the selection or resize it with eight handles; add a pen stroke, arrow, or mosaic if needed.
2. Press **Enter** to save and copy, then return to your previous application. The image is saved first; OCR runs in the background.
3. Open the library from the tray and search for text in your screenshots. Press **Space** to view the original with matching text lines highlighted.

Change the shortcut in settings to a combination such as `Alt+A` or `Ctrl+Shift+Q`. Common system combinations are blocked, and Windows checks global hotkey availability. If registration fails, your previous shortcut stays active. **Esc** cancels without creating an image or library entry.

## What you can do

| Task | How it works |
| --- | --- |
| Keep a screenshot and share it right away | Select, adjust, save and copy; or copy only / save as PNG. |
| Mark something important | Pen, arrow, outlined rectangle; six quick colors, an RGB/Hex palette, adjustable stroke width. |
| Change an annotation | Erase back to the original pixels, undo, redo; clearing annotations is undoable. Mosaic block size is adjustable. |
| Find an older screenshot | Search titles, notes, tags, and OCR text, including short Chinese phrases, mixed text, dates, and IDs. |
| Organize existing images | Drag in or batch-import PNG/JPEG, add notes, tags, and stars; deleted items can be restored from the recycle bin. |
| Read small text or a tall image | Zoom, pan, fit to window, or use actual pixel size. Search highlights move with the image. |
| Move the library | Change its location in settings or restore a ZIP backup into a new directory. |

<details>
<summary>Capture tools, color palette, and dark theme</summary>

![Capture pen and adjustable size](docs/ui-review-images/0.1.2/capture-large-pen.png)

![Custom color with RGB sliders, Hex input, and preview](docs/ui-review-images/0.1.2/palette-large.png)

![Dark library theme](docs/ui-review-images/0.1.2/ui-dark.png)

See the [UI review](docs/en/ui-review.md) for more images and the fixes made after inspecting them.

</details>

## Downloads and your data

| Package | Use it when | Size |
| --- | --- | --- |
| [Installer](https://github.com/liugedragon/screenshot-box/releases/download/v0.1.2/ScreenshotBox-0.1.2-win-x64-setup.exe) | You want a normal installation, a chosen install directory, and an uninstall entry. | About 82 MiB |
| [ZIP](https://github.com/liugedragon/screenshot-box/releases/download/v0.1.2/ScreenshotBox-0.1.2-win-x64.zip) | You prefer to extract a directory and run `ScreenshotBox.exe`. | About 107 MiB |

Both include the runtime and Chinese OCR models. **Keep the complete directory, not just the exe.** Packages and checksum files are on the [release page](https://github.com/liugedragon/screenshot-box/releases/tag/v0.1.2). Exit the old version from the tray before upgrading.

Images, text, and categories default to `%LOCALAPPDATA%\ScreenshotBox\library`, separate from the installation. Settings can migrate the library to a directory you choose. The app does not upload images or text or download models during recognition. Uninstalling preserves your library. Closing the main window leaves the app in the tray; use its menu to exit completely.

## A few limits

- Including the runtime and OCR models makes this larger than a few-megabyte utility. First recognition includes model loading time.
- OCR can get low-resolution, handwritten, or busy images wrong. Highlights cover whole lines, not individual characters.
- Mosaic is visual pixelation, not a guarantee of secure redaction. Erasing mosaic restores the original pixels.
- No scrolling capture, recording, cloud sync, automatic updates, or annotation editing after saving. The app and installer are unsigned.
- Desktop checks used one Windows monitor at 125% scaling. Mixed-DPI multiple monitors, pasting into external apps, and a physically offline clean PC still need testing.

There are **42 core tests and 35 annotation pixel checks**. Windows CI checks builds, storage, and synthetic image behavior. Separate release checks cover real Chinese OCR, search, backups, installation, restart recovery, and preserving data on uninstall. Their scope differs; see the [validation record](docs/en/validation.md).

## Documentation

| Guide | Contents |
| --- | --- |
| [User guide](docs/en/usage.md) | Shortcuts, annotation, search, imports, and backups. |
| [Build](docs/en/build.md) · [Packaging and installation](docs/en/distribution.md) | Run from source or create a release package. |
| [Architecture](docs/en/architecture.md) · [Storage and search](docs/en/storage.md) | Physical pixel coordinates, Chinese queries, background tasks, and recovery. |
| [Design notes](docs/en/design.md) · [UI review](docs/en/ui-review.md) | References, styles, and actual window checks. |
| [Validation](docs/en/validation.md) · [Limitations](docs/en/limitations.md) | What was tested and what was not. |
| [Third-party components and models](docs/en/third-party.md) | Sources, licenses, versions, and hashes. |

Chinese is the default documentation language; each guide links to its Chinese counterpart.

## Build from source

Requires Windows x64 and **.NET SDK 10.0.401**. Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/fetch-models.ps1
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 0.1.2
```

Fetching models the first time requires a connection. Dependencies are pinned in `packages.lock.json`; running a release needs no SDK. Building an installer also requires Inno Setup; see [packaging and installation](docs/en/distribution.md).

## Help improve it

Multiple-monitor testing, OCR failure samples, documentation corrections, and small bug fixes are useful contributions. Use synthetic or redacted images and include the app version, display scaling, and steps to reproduce. The [contribution guide](CONTRIBUTING.en.md) explains the checks and submission process.

Next work starts with problems found in actual use. Larger-library search performance, an English UI, and text annotations are candidates, listed in the [roadmap](ROADMAP.en.md).

If this is useful to you, a **Star** is welcome. Bug reports are welcome too.

## License and acknowledgments

New application code and the original icon use [MIT](LICENSE). WPF UI, RapidOcrNet, PaddleOCR models, ONNX Runtime, SQLite, and other components retain their own licenses. This project does not train its own OCR models. See [third-party notices](docs/en/third-party.md).

Layout and documentation organization draw on Eagle, ShareX, PowerToys, Flameshot, and Flow Launcher. Their brand assets, screenshots, and prose are not used as our own. [Design references](docs/en/design.md) · [README references](docs/en/readme-references.md)
