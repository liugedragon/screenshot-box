# Synthetic capture pixel probe

Windows-only executable that links the application's native capture sources into a separate project. It exercises the real capture session and its `Accept` export path with an in-memory, patterned `Bgr32` image. It does not start ScreenshotBox, display overlay windows, capture the desktop, write image files, alter the clipboard, register hotkeys, or trigger display changes.

Run from the repository root using .NET 10 on Windows:

```powershell
dotnet run --project tests/ScreenshotBox.Capture.Probe/ScreenshotBox.Capture.Probe.csproj
```

If using this repository's local SDK:

```powershell
.\.tools\dotnet\dotnet.exe run --project tests/ScreenshotBox.Capture.Probe/ScreenshotBox.Capture.Probe.csproj
```

Exit code `0` means every case passed; `1` means at least one failed. Each case prints its result, followed by a summary. Build outputs stay in this test project's own `bin` and `obj` directories.

Successful run summary for the current implementation:

```text
RESULT: 35 passed, 0 failed. Synthetic pixels only; no desktop capture, files, clipboard, hotkeys, or overlay windows.
```

This records the observed local Windows run, not a guarantee that later changes will pass. Re-run the command against the exact source revision being reviewed.

Coverage includes positive and negative desktop origins, cropped source alignment, six preset pen colors, fractional and maximum pen widths, arrowheads, reverse-direction rectangles, mosaic averages for 6 and 32 pixel blocks (including partial edge blocks), restoring the source through each annotation type with the eraser, undo/redo, undoable clear, branching history, and runtime tool preference persistence and input bounds. RGB comparisons normalize both source `Bgr32` and exported `Pbgra32` images; the source's unused fourth byte deliberately varies to catch accidental alpha assumptions.

The test uses reflection only to construct the private capture session, select its private mode enum, and read its completion task. Drawing, tool changes, history, and export go through the actual session methods. Every intermediate export replays the recorded operations on a fresh session. Expected original pixels and mosaic block averages come directly from the synthetic pattern, independently of the application's renderer.

This verifies the annotation pixel model and export. It does not verify toolbar placement, mouse input routing, hotkey registration, live monitor disconnects, mixed display scaling, real desktop capture, OCR, or disk/clipboard persistence.
