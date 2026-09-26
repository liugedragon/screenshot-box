# Local library, search, and backup

[English](../storage.md) · [简体中文](../zh-CN/storage.md)

## Files

The library is separate from the installation folder. It contains `library.db`, originals in `images/`, and thumbnails in `thumbnails/`. Images use unique IDs as filenames; a database record is added after the files are written. Originals and thumbnails remain immutable after insertion. The recycle bin uses soft deletion and retains image files.

## Search and categories

Parameterized SQLite substring queries search titles, notes, tags, and OCR text, including one- and two-character Chinese terms. Quotes, percent signs, and underscores are ordinary characters; ASCII letters are case-insensitive. Queries scan records. Fuzzy, pinyin, and semantic matching are not supported.

Results default to newest first, with oldest-first sorting and pagination. Sorting applies to the entire matching set before retrieving a page. Recently saved filters by UTC creation time for the preceding seven days, then sorts and paginates.

Tags come from all active items. Categories match whole tags by splitting Chinese or English commas, trimming, and comparing with `OrdinalIgnoreCase`; symbols remain literal. Categories, keywords, stars, and other filters intersect in the database before sorting and pagination. Original tag-editing text is preserved. Words in titles or OCR text do not assign tags.

## OCR jobs

Images are saved before background recognition. A task receives a generation, and its result can update only an active Processing record with the same generation. Deletion, restoration, or newer jobs invalidate stale results.

Startup resets interrupted Processing jobs to Pending for requeueing. Failed jobs retain the image and error for manual retry. Queue behavior is detailed in [architecture](../architecture.md).

## Backup

SQLite's backup API creates a consistent snapshot. The ZIP contains the originals, thumbnails, and recycle-bin records it references; changes after the snapshot are excluded. Backup resolves unsaved editing drafts first.

Missing referenced files cause failure without creating an incomplete ZIP or replacing an existing destination. Archives contain user data and are saved to the chosen location.

## Restore

Restore checks format version, SQLite integrity, inventory, and relative paths, then installs into a new empty directory. Validation rejects traversal, drive and alternate-stream paths, duplicate entries, unlisted files, missing images, and unsupported versions. Existing libraries are not merged or overwritten.

Initializing a restored library creates standard subdirectories and recovers interrupted OCR jobs. The app restarts on the new library and retains the original.

## Tests

Core tests cover SQLite queries and updates, short Chinese terms and symbols, sorting and pagination, tag categories, OCR state races, backup during concurrent edits, restored byte equality, missing files, and unsafe archive paths. Commands are in [build](../build.md); version results are in [validation](../validation.md).
