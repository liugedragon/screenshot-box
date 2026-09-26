# ScreenshotBox.Core

[English](README.md) · [简体中文](README.zh-CN.md)

`LibraryStore` owns metadata and OCR job states. Screenshot and thumbnail files
must already exist before `AddAsync`, use unique relative paths, and remain
immutable after insertion. Recycle-bin deletion does not delete image files.

## Search

Version 1 uses parameterized SQLite `instr(lower(column), lower(query))` over
title, notes, tags, and OCR text. This is literal substring search, including
one- and two-character Chinese. Quotes, `%`, and `_` do not become SQL syntax
or wildcards. ASCII letters are case-insensitive. Queries scan records. Fuzzy, semantic, and pinyin matching are not supported.

Supported filters: `all`, `favorites` (`favorite`), `trash` (`deleted`),
`recent` (the preceding seven days, UTC), `pending` (Pending or Processing), and `failed`.
Pagination sorts the entire filtered result by UTC date (newest by default, or
oldest with `oldestFirst`), then ID ascending, before applying `limit`/`offset`.
The maximum per-query limit is 1,000,000; the UI should use result pages. Item JSON
and queryable columns update in the same transaction.

The optional `requiredTag` is an independent exact-member classification, combined
with the text query and other filters before pagination. SQLite runs a deterministic
registered member function; no `LIKE` pattern is used. Both this function and
`GetTagsAsync` split English/Chinese commas, trim whitespace, and compare tags using
.NET ordinal case-insensitive rules. Percent, underscore, and quotes are literal.
The original tag metadata is preserved without changing editor text.
Title or OCR matches alone never qualify an item for a tag classification.

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

Tests use SQLite, filesystem operations, and ZIP archives to cover queries, restart, and OCR state. Run the [build script](../../scripts/build.ps1) to execute them. Windows integration checks are in [validation](../../docs/validation.md).
