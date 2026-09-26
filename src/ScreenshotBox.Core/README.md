# Local library implementation

`LibraryStore` owns metadata and OCR job states. Screenshot and thumbnail files
must already exist before `AddAsync`, use unique relative paths, and remain
immutable after insertion. Recycle-bin deletion does not delete image files.

## Search

Version 1 uses parameterized SQLite `instr(lower(column), lower(query))` over
title, notes, tags, and OCR text. This is literal substring search, including
one- and two-character Chinese. Quotes, `%`, and `_` do not become SQL syntax
or wildcards. ASCII letters are case-insensitive. It is intentionally a linear
scan and is not advertised as FTS, fuzzy, semantic, or pinyin search.

Supported filters: `all`, `favorites` (`favorite`), `trash` (`deleted`),
`pending` (Pending or Processing), and `failed`. Pagination order is UTC date
descending followed by ID ascending. Item JSON and queryable columns update in
the same transaction.

## OCR durability

`BeginOcrAsync` returns a generation. A completion is accepted only for the
same generation, an active item, and a Processing job. Deletion, recovery from
an interrupted Processing job, and a newer OCR job invalidate old completions.
On initialization, interrupted jobs become Pending and can be requeued.

## Backups

Backups use SQLite's database backup API, not a copy of the live database file.
The snapshot determines exactly which immutable originals and thumbnails to
include. Changes after the snapshot are excluded consistently. Recycled items
are included. A missing referenced image fails the backup without publishing
an incomplete archive. An existing destination ZIP is never overwritten.

Restoration stages a validated backup and installs it only into a new, empty
directory. Validation rejects traversal, drive/alternate-stream paths,
duplicate names, files absent from the database manifest, missing images,
unsupported schema versions, and SQLite integrity failures. Existing libraries
are never merged or overwritten. Run `InitializeAsync` on the restored library
before use to create standard subdirectories and recover interrupted OCR jobs.

The module tests exercise actual SQLite, filesystem, ZIP, restart, and OCR-race
behavior. UI screenshot correctness, OCR recognition quality, and multi-monitor
behavior require separate Windows integration testing.
