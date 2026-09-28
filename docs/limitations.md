# Known limitations

English · [简体中文](zh-CN/limitations.md)

ScreenshotBox is currently a preview release. Recorded checks are in the [test record](validation.md).

The sections below describe the Windows release. Linux differences and unverified desktop environments are listed in the [Linux guide](linux.md).

## Features

- The app provides English and Simplified Chinese interfaces. System mode follows the Windows display language at startup. Language changes require a restart; system errors and Windows file dialogs may retain the operating system's language. Other display-language packs have not been tested on hardware.

- Search uses literal substring matching. Semantic search is not available, and large-library performance has not been measured.
- OCR uses a Chinese/English mobile model. Low-resolution text, handwriting, complex backgrounds, and tilted text may be inaccurate. Highlights cover entire text lines.
- Annotations do not yet support text labels, solid redaction, or editing after saving. Mosaic does not guarantee secure redaction; erasing it during capture restores the original image.
- The recycle bin supports deletion and restore, but not permanent emptying. Backups include recycled items.
- Data encryption, automatic updates, cloud sync, and accounts are not available. The published Windows 0.1.4 installer and app are unsigned; later direct-download releases need a verified signing identity.

## Size and runtime

The 0.1.4 installer is approximately 84.0 MiB; the ZIP is approximately 108.5 MiB. They include .NET, Chinese OCR models, ONNX, and related native libraries.

OCR models load on demand, adding a delay to the first recognition. On the 0.1.1 test machine, an empty library used about 148.3 MiB of working set six seconds after startup. A multi-window recognition test used about 360.3 MiB, falling to 281.1 MiB after releasing the model following 90 idle seconds. Usage varies with images, open windows, and model state.

Closing the main window leaves the app in the tray. Use the tray menu to quit. Uninstall preserves the library; delete unwanted data separately.

## Unverified scenarios

Hardware checks currently use one 2560 × 1440 display at 125% scaling. The following scenarios have not completed hardware or manual testing:

- Actual Windows sign-out/sign-in and Task Manager startup enable/disable.
- Dual displays, mixed DPI, cross-screen mouse selection, and physical display connection/disconnection.
- Pasting into common external applications, a complete manual mouse/keyboard pass, and prolonged use.
- A fresh Windows computer with no development SDK and a physically disconnected network.
- More scaling factors and monitor layouts.
- The complete manual Settings flow for directory migration, backup/restore, and restart.

The distribution has been checked loading its bundled runtime. Programmatic tests cover Chinese OCR, restore to a new directory, and clipboard readback.
