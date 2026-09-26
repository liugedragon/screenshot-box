# Linux services probe

[简体中文](README.zh-CN.md)

Runs the shared OCR and library code on Linux without opening a desktop window. The probe creates its own synthetic Chinese and English image and uses a new output directory. It does not read the user's library.

Requirements: .NET SDK 10.0.401, GCC, binutils, curl, Python 3, and a font covering Simplified Chinese (for example `fonts-noto-cjk`). The bundled PP-OCRv5 recognition model and dictionary must have been fetched with the model download script documented in the main build guide.

From the repository root:

```bash
bash scripts/build-linux-sqlite.sh

dotnet restore tests/ScreenshotBox.Linux.Services.Probe/ScreenshotBox.Linux.Services.Probe.csproj --locked-mode
dotnet build tests/ScreenshotBox.Linux.Services.Probe/ScreenshotBox.Linux.Services.Probe.csproj --no-restore
cp artifacts/native/linux-x64/libe_sqlite3.so \
  tests/ScreenshotBox.Linux.Services.Probe/bin/Debug/net10.0/linux-x64/libe_sqlite3.so

dotnet tests/ScreenshotBox.Linux.Services.Probe/bin/Debug/net10.0/linux-x64/ScreenshotBox.Linux.Services.Probe.dll \
  artifacts/linux-services-probe
```

Choose a new output directory for each run. The program refuses to overwrite an existing nonempty directory.

The result includes `result.json`, `ocr-result.json`, a synthetic source image, an isolated library and a restored backup. Checks cover PNG imports, proportional thumbnails, atomic exports, actual CPU Chinese OCR and line boxes, two-character Chinese queries, literal symbols, metadata updates, deletion and OCR generation races, missing originals, backup contents and interrupted tasks. An assertion failure exits unsuccessfully.

Desktop capture, global shortcuts, clipboard interaction, tray behavior and Wayland are outside this probe's scope.
