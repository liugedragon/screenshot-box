# Known limitations · 0.1.2

[简体中文](../limitations.md) | English

- The available test machine has one 2560 × 1440 display at 125% scaling. Real dual-monitor, mixed-DPI, display-change, and cross-screen mouse flows remain unverified.
- The Windows interaction tool could not initialize in the WSL workspace. A full manual mouse/keyboard pass and real pasting into common external applications have not been completed. Native integration probes and Windows automated tests do not replace those checks.
- The complete flow on a fresh Windows machine without a development SDK, with the network physically disconnected, remains unverified. The published executable was confirmed to load its bundled runtime, and the application source makes no network calls.
- Search uses literal substring matching, not semantic retrieval. Performance on large libraries has not been measured.
- OCR uses a Chinese/English mobile model. Low-resolution text, handwriting, complex backgrounds, and tilted documents may fail. Highlights cover whole text lines, not individual characters.
- Annotation tools include adjustable pen, arrow, outline rectangle, eraser, and mosaic, plus RGB/Hex colors and undo/redo. Text labels, solid redaction, and editing annotations after saving are not implemented. Mosaic does not guarantee secure redaction.
- The recycle bin supports soft deletion and restore; there is no permanent-empty action yet. Backups include recycle-bin entries, so keep an eye on disk usage.
- The runtime and native OCR libraries account for most of the download size. WPF avoids a browser runtime, but this self-contained release is not a few-megabyte utility.
- OCR models initialize on demand, so the first recognition has a loading delay. Closing the main window leaves the app in the tray; use the tray menu to quit completely.
- Data encryption, application code signing, automatic updates, cloud sync, and accounts are not provided.

Program-driven tests for 0.1.2 checked real OCR, image clipboard readback, exact tag filtering, edit drafts, tall-image fitting, and annotation pixels. The RGB/Hex palette, tool menu, and different window widths were exercised programmatically and reviewed in actual window screenshots. These are not recorded as human mouse/keyboard, multi-monitor, or physically offline acceptance tests.

See the [validation record](validation.md) for results and the [UI review](ui-review.md) for screenshots and changes.
