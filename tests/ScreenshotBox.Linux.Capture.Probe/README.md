# Linux capture checks

[简体中文](README.zh-CN.md)

Run on a Linux x64 X11 display with the .NET 10 SDK:

```bash
DISPLAY=:0 dotnet run --project tests/ScreenshotBox.Linux.Capture.Probe
```

The probe displays its own synthetic marker and overlay windows briefly. It acquires actual X11 root pixels, queries the marker's actual window position, and verifies that the application window is hidden before acquisition. Run without another UI test modifying the same display.

Checks cover reverse selection, eight resize points, movement bounds, exact crop pixels, adjustable pen width, arrows, rectangles, mosaic, whole-annotation erasing and undo. Two independent X11 connections check hotkey ownership and retention after a failed change. Real Avalonia controls check Enter, Escape, completion actions and toolbar placement.

Results and a synthetic English UI image are written to `artifacts/linux-capture-validation/`. Set `SCREENSHOTBOX_CAPTURE_PROBE_OUTPUT` to choose another directory. The probe creates no library record and does not save a real desktop image.

This is a component and native-interface check. It does not send actual keyboard shortcuts, verify mixed monitor scaling, or cover Wayland. Under WSL with an external X11 server, capture and hotkeys are limited to that server's X11 clients.
