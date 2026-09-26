# Capture annotation pixel tests

[English](README.md) · [简体中文](README.zh-CN.md)

Windows-only executable that links the application's native capture sources into a separate project. It checks the capture session and its `Accept` export path with an in-memory, patterned `Bgr32` image.

Run from the repository root using .NET 10 on Windows:

```powershell
dotnet run --project tests/ScreenshotBox.Capture.Probe/ScreenshotBox.Capture.Probe.csproj
```

If using this repository's local SDK:

```powershell
.\.tools\dotnet\dotnet.exe run --project tests/ScreenshotBox.Capture.Probe/ScreenshotBox.Capture.Probe.csproj
```

Exit code `0` means every case passed; `1` means at least one failed. Each case prints its result, followed by a summary. Build outputs stay in this test project's own `bin` and `obj` directories.

Recorded 0.1.2 result:

```text
RESULT: 35 passed, 0 failed. Synthetic pixels only; no desktop capture, files, clipboard, hotkeys, or overlay windows.
```

Coverage includes positive and negative desktop origins, cropped source alignment, six preset pen colors, fractional and maximum pen widths, arrowheads, reverse-direction rectangles, mosaic averages for 6 and 32 pixel blocks (including partial edge blocks), restoring the source through each annotation type with the eraser, undo/redo, undoable clear, branching history, and runtime tool preference persistence and input bounds. RGB comparisons normalize both source `Bgr32` and exported `Pbgra32` images; the source's unused fourth byte deliberately varies to catch accidental alpha assumptions.

The test uses reflection only to construct the private capture session, select its private mode enum, and read its completion task. Drawing, tool changes, history, and export go through session methods. Every intermediate export replays the recorded operations on a fresh session. Expected original pixels and mosaic block averages come directly from the synthetic pattern, independently of the application's renderer.

The tests cover in-memory annotation and export pixels. They do not start application windows, capture the desktop, write files, or change the clipboard. Toolbar, input, hotkey, multi-monitor, and OCR behavior have separate Windows integration checks; see [validation](../../docs/validation.md).
