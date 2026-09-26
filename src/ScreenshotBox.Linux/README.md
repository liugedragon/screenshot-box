# ScreenshotBox.Linux

English · [简体中文](README.zh-CN.md)

Avalonia 12.1.3 frontend for the X11 preview. The application shares `LibraryStore` and `OcrQueue` with Windows; capture and hotkeys use X11 interfaces.

## Build

Install the .NET SDK version in `global.json`, GCC, Python 3 and normal Ubuntu desktop libraries. Python is used by build scripts; the application does not require it.

From the repository root:

```bash
python3 scripts/fetch-models.py
bash scripts/build-linux-sqlite.sh
dotnet restore src/ScreenshotBox.Linux/ScreenshotBox.Linux.csproj --locked-mode
dotnet build src/ScreenshotBox.Linux/ScreenshotBox.Linux.csproj -c Release --no-restore -m:1
bash scripts/package-linux.sh
```

The package script replaces NuGet's SQLite binary with the verified source build. This is required on Ubuntu 20.04, where the bundled SQLitePCLRaw binary requires a newer glibc. See [Linux usage and test coverage](../../docs/linux.md).

`MainWindow` manages the library and selection, `PreviewWindow` maps OCR line boxes to scaled image coordinates, and `SettingsWindow` persists language, theme, shortcut and library location. `Native` owns X11 acquisition and hotkey registration. `AnnotationModel` keeps annotation objects separate from the frozen original image. `LinuxImageLibrary` commits image files before inserting database metadata.

A private Unix socket activates the existing process on repeated launch. Language changes and library migration restart only after the OCR worker and socket have shut down. Linux and Windows builds use separate intermediate directories.
