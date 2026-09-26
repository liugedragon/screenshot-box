# Architecture

English · [简体中文](../zh-CN/architecture.md)

## Modules

| Module | Responsibilities |
| --- | --- |
| `ScreenshotBox.Core` | SQLite library, search, OCR task generations, backup and restore; no WPF dependency. |
| `ScreenshotBox.App/Native` | Win32 hotkeys, GDI desktop capture, per-monitor overlays, selections, annotations, and PNG output. |
| `ScreenshotBox.App/Services.cs` | Windows originals, thumbnails and clipboard retries. |
| `ScreenshotBox.Ocr` | Shared CPU OCR queue and model lifecycle. |
| `ScreenshotBox.Linux` | Avalonia library interface, X11 capture and shortcuts, annotations and PNG imports. |
| `PreviewWindow` | Original image, text-line boxes, zoom, and pan. |

The Windows main window displays an observable collection. Views handle focus, selection, and window events; Core and Services handle data operations. `SelectionGeometry` provides independent rectangle calculations.

## Startup and tray (Windows)

Normal launch opens the library. `--background` initializes the window's native handle, capture hotkey, and tray icon without showing the main window. Pending OCR jobs resume in either mode. Double-clicking the tray icon or choosing Open library displays the window.

The installation wizard registers the quoted exe path and `--background` under the current user's Windows Run key. The runtime does not register startup. Setup does not rewrite Windows's `StartupApproved` state; the stable `ScreenshotBox` value is removed on uninstall.

A per-user mutex enforces one instance. A second normal launch signals the existing instance to open its library; a second background launch exits without showing it. Background startup failures write `%LOCALAPPDATA%\ScreenshotBox\diagnostics\startup-error.txt` and exit without a dialog.

The Linux preview uses a private Unix socket for repeated-launch activation and keeps its library window available. It does not register login startup or use a tray icon.

## Capture coordinates

The virtual desktop origin can be negative. The app enumerates monitors, computes their combined physical rectangle, and freezes the desktop through GDI. Mouse positions come from `GetCursorPos`; selections and annotations use physical pixels.

Each monitor has an overlay with its own DPI-to-DIP conversion. The crop origin is `global selection origin − virtual desktop origin`; width and height are physical selection pixels. Annotation rendering subtracts the selection origin. Geometry handles reverse selection, clamped movement, and resize boundaries.

The executable manifest declares PerMonitorV2. DPI checks must launch the exe; the `dotnet` host lacks this declaration. Multi-monitor test coverage is listed in [validation](validation.md).

## Annotation and preview

Annotations store their color, size, and source-pixel positions. Mosaic uses block-average colors; erasing restores frozen original pixels. History includes clearing annotations, with undo and redo; new edits invalidate the redo branch. The palette changes later strokes and shapes only.

Preview puts the image and OCR line boxes in one source-pixel Grid, scales both together, then pans through a ScrollViewer. Actual size uses `1 / DpiScale`, so a 1000 px image occupies 1000 physical pixels at 125% scaling. Fit mode recalculates for the available window area and permits scales below 5% for tall images. Dragging starts only on the image.

## Search

Titles, notes, tags, and OCR text use parameterized `instr(lower(column), lower($q))` queries. Quotes, percent signs, and underscores remain literal; ASCII letters are case-insensitive.

SQLite FTS5's default `unicode61` tokenizer does not segment Chinese words; trigram full-text queries have limits below three characters. Substring scanning therefore supports short Chinese queries. Time indexes and pagination reduce display work; larger-library query performance still needs measurement.

Exact tag categories use a deterministic SQLite membership function, intersected in `WHERE` with keywords, stars, and other conditions before sorting and pagination. Tags split on Chinese or English commas and compare using Trim and `OrdinalIgnoreCase`. Original tag-editing text is preserved.

Raw OCR text and line boxes are stored in original-image coordinates. Highlights use the engine's whole-line bounds. Search does not provide simplified/traditional conversion, pinyin, or semantic matching.

## OCR queue and state

An image is written before its library record is created and sent to the Channel queue. A single consumer reuses the model with limited CPU threads. Status and task generations are persisted.

Each task starts with a generation. Deletion, retry, and related state changes advance it. Committing a result checks generation, Processing status, and active-item state; stale results are discarded. Startup changes interrupted Processing jobs back to Pending. Failed jobs retain the image and error and allow manual retry.

When an image is deleted and immediately restored during OCR, its old task may still own the deduplication slot. After releasing the slot, the queue checks the record again and re-enqueues it only if active and Pending. Failed jobs are not retried indefinitely.

## Editing and data transitions

Opening the separate editor disables that item's main details fields. Saving updates the in-memory item and saved baseline; closing retains a session draft. Exit, backup, migration, and restore resolve drafts through save, discard, or cancel.

Backup uses SQLite `BackupDatabase` for a consistent snapshot and packages the immutable originals and thumbnails it references, including recycle-bin items. Restore checks paths, structure, version, integrity, and inventory before writing to a new empty directory. Migration and restore restart the app on success and retain the old library. Data transitions block new images and metadata writes.

Save-as and export write and flush a temporary file beside the destination, then replace it atomically. Failures remove temporary files and preserve the existing destination.

Thumbnail size, annotation settings, and the language preference are stored locally. The preference is System, Simplified Chinese, or English. System reads the first Windows display language from `GetUserPreferredUILanguages`, falling back to `GetUserDefaultUILanguage`: any `zh-*` locale uses Simplified Chinese, and other locales use English. A manual choice overrides that rule. Language is applied at startup and requires restart after switching; image text and user metadata are preserved. Global hotkeys register through Win32.

## Component responsibilities

WPF UI supplies Windows themes and controls; Avalonia supplies the Linux interface. Microsoft.Data.Sqlite/SQLite provide the transactional database. RapidOcrNet invokes detection, orientation, and recognition models; PaddleOCR supplies trained weights. ONNX Runtime executes CPU inference, and SkiaSharp processes OCR images.

ScreenshotBox implements selection interactions, hotkey updates, annotation history, palette, library views, queries, job state, backup restore, and integration tests. Versions and licenses are in [third-party notices](third-party.md).
