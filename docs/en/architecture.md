[简体中文](../architecture.md) | English

# Architecture

## Modules

`ScreenshotBox.Core` handles the SQLite library, search, OCR task generations, backup and restore. It has no WPF dependency and is tested against real SQLite databases.

`ScreenshotBox.App/Native` contains Win32 hotkeys, GDI desktop capture, a separate overlay window for each monitor, physical-pixel selection, annotations and PNG output. `SelectionGeometry` is independent of the Windows UI; tests cover negative coordinates, reverse dragging and boundaries.

`ScreenshotBox.App/Services.cs` manages originals and thumbnails, a single-consumer OCR queue, the CPU model lifecycle and bounded clipboard retries. The main window uses a small MVVM observable collection to display library records. Views handle selection, focus and window events; Core and Services handle data operations.

`PreviewWindow` places the original image and OCR line boxes in the same source-pixel Grid. Both scale together, then pan through a ScrollViewer, keeping highlights aligned with the image.

## Capture coordinates

The Windows virtual desktop does not necessarily start at `(0,0)`. The application enumerates monitors and computes their combined physical rectangle. Desktop capture uses physical pixels, not DIP. Mouse positions come from `GetCursorPos` in physical coordinates. Each monitor gets its own overlay, whose physical size is converted to DIP for that monitor. The primary monitor's scale must not be applied to other displays.

The final crop origin is `global selection pixels − virtual desktop top-left pixels`; width and height come directly from the physical-pixel selection. Annotations use the same coordinate system and subtract the selection origin when rendered. The pure geometry module normalizes reversed selections and enforces boundaries. The executable manifest declares PerMonitorV2. DPI checks must launch the executable rather than a `dotnet` host that lacks that manifest.

The pen, arrow, outline rectangle, eraser and mosaic are project code, composited into the final PNG. Each annotation stores its own color, size and source-pixel positions. Mosaic blocks use average pixel colors. The eraser reveals pixels from the frozen original image; it does not paint white. Annotation history supports undo and redo, including clearing all annotations. A new edit invalidates the redo branch. RGB/Hex palette changes affect subsequent strokes and shapes, leaving existing annotations unchanged.

## Chinese search

The first version uses parameterized SQLite `instr(lower(column),lower($q))` queries across titles, notes, tags and OCR text. Quotes, percent signs and underscores are literal characters, not query syntax or wildcards. Two-character Chinese queries work without English tokenization.

FTS5's default `unicode61` tokenizer does not segment Chinese words. Its trigram tokenizer also cannot directly match full-text queries shorter than three characters. This version deliberately scans literal substrings; it does not claim a high-performance full-text index. Time indexes and pagination reduce display work. Search performance on large libraries still needs measurement.

Exact tag filtering uses a deterministic SQLite membership function. Tag membership is intersected with search, starred status and other conditions in `WHERE`, before sorting and pagination. Tag navigation and filtering share the same rules: Chinese or English commas, trimming and `OrdinalIgnoreCase`. A word in a title or OCR text does not make that word a tag. The original tag-editing text is preserved rather than rewritten.

Raw OCR text and its line boxes are retained in original-image coordinates. Search normalization does not discard location information. Search does not convert between simplified and traditional Chinese or provide fuzzy semantic matching. Highlights use the engine's whole-line bounds; no character-level precision is invented.

## Background tasks and deletion races

The image is written successfully before its library record is created and added to the Channel queue. One consumer reuses the Chinese OCR model with a limited number of CPU threads. SQLite stores OCR status and the task generation.

Each OCR run receives a generation number. Deletion, retry and other relevant state changes advance that generation. A result can be committed only if the generation still matches and the record has not been deleted. An old task therefore cannot make a deleted image reappear. On startup, interrupted `Processing` records return to `Pending` and are queued again. Failed recognition keeps the image and error information and allows retry.

Deleting and immediately restoring an image during recognition has an additional queue edge case: the old task may still own the deduplication slot, so a new enqueue request can be coalesced. Once the old task releases that slot, the queue checks the current record again and re-enqueues it only if it is not deleted and remains `Pending`. `Failed` records are not retried indefinitely; deleted records are not resurrected. The executable self-test deletes, restores and immediately enqueues an item during a `Processing` event, then uses real OCR to verify that it reaches `Ready`.

## Backup and restore

SQLite `BackupDatabase` creates a consistent database snapshot. The snapshot's inventory determines which immutable originals and thumbnails enter the archive. OCR does not modify image files, and the recycle bin uses soft deletion, retaining those files.

Restore validates paths and archive structure and writes only to a new empty directory. It never overwrites the running database. The restored library does not depend on the original installation path. Backup, migration and restore first resolve unsaved drafts. During a data transition, new images and metadata writes are blocked. Successful migration or restore restarts the application with the new library; the old library remains intact.

## Upstream components and project code

WPF UI provides themes and controls. Microsoft.Data.Sqlite/SQLite provide the transactional database. RapidOcrNet performs text detection, orientation classification and recognition. PaddleOCR Chinese mobile models supply the trained weights. ONNX Runtime executes them on the CPU; SkiaSharp handles OCR image processing.

ScreenshotBox adds selection and multi-window capture interactions, transactional hotkey updates, annotation history and the RGB/Hex palette, image persistence, library views, the Chinese substring-search strategy, OCR task generations, the backup format, restore validation and integration tests. This project does not claim to train its own OCR model and does not copy Eagle or ShareX source code.

## Editing, preview and file writes

Opening an item in the separate editor disables that item's main-window editing fields, preventing two writable drafts from overwriting each other. A successful save updates both the in-memory item and its saved baseline. Closing the editor preserves an unsaved session draft. Exit, backup, migration and restore offer save, discard or cancel before proceeding; writes remain blocked while those operations are busy.

Save PNG and export write a temporary file beside the destination, then replace the destination after success. Failures remove the temporary file without deleting the existing image first. Actual-size preview uses `1/DpiScale`, so a 1000px-wide image still occupies 1000 physical pixels on a 125% display. Fit-to-window recalculates for the current window size and allows scales below 5% for long images. Mouse panning starts only on the image, leaving scrollbar behavior intact.

Thumbnail size, annotation color, pen width, eraser size and mosaic block size are stored in local settings and restored after restart. The global capture hotkey is registered through Win32 rather than simulated with application keyboard events.
