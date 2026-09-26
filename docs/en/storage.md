[简体中文](../storage.md) | English

# Local library, search and backup

The library is a user folder separate from the installation directory. It contains `library.db`, originals in `images/` and thumbnails in `thumbnails/`. Images use unique IDs as filenames. The database record is added only after the image write succeeds. Originals and thumbnails are not rewritten after import. Moving an item to the recycle bin changes its status without deleting its files.

## Search

The first release uses parameterized SQLite literal-substring queries across titles, notes, tags and OCR text. One- and two-character Chinese queries work. Quotes, percent signs and underscores are ordinary characters. English letters are matched without case sensitivity.

This approach scans records. It does not provide a full-text index, fuzzy matching, pinyin or semantic search. Results default to newest first; oldest-first sorting and pagination are also available. Sorting applies to the full matching library before retrieving a page, rather than reversing only the most recent page. The tag list comes from the entire undeleted library, independently of the current result page.

Tag categories use separate, exact membership filtering: split on Chinese or English commas, trim surrounding spaces and compare without English case sensitivity. Tag navigation uses those same rules. Percent signs, underscores and quotes remain literal. A matching word in a title or OCR text does not count as a tag. Category, keyword, starred and other conditions are intersected in the database before sorting and pagination. The original tag-editing text is preserved rather than automatically rewritten.

Recently saved first filters undeleted records to the preceding seven days by UTC creation time, then sorts and paginates. It does not retrieve an arbitrary library page and discard older images afterward, which could omit recent items.

## OCR tasks and restart

Images are saved before background recognition begins. Each task receives a generation number. Only a valid record with the matching generation and processing state can accept its result. Deletion, restoration or a new recognition task invalidates old results. On restart, interrupted tasks return to pending status and can be queued again.

## Backup and restore

SQLite's backup API creates a complete snapshot, including the originals, thumbnails and recycle-bin records referenced by it. Copying an active database file alone is insufficient. The archive does not include changes made after the snapshot.

Missing originals cause backup to fail. It neither leaves an apparently successful but incomplete ZIP nor overwrites an existing backup. Archives contain the user's screenshots and are not sent to a server.

Restore first validates the format version, database integrity, file inventory and relative paths, then writes to a new empty directory. It rejects path traversal, duplicate entries, missing images and unsupported backup versions. It does not merge with or overwrite an existing library. Initializing the restored library recovers pending recognition tasks.

## Verification scope

Automated tests use real SQLite, image bytes, ZIP archives and filesystem operations. Coverage includes short Chinese words and special characters, editing and saving, restart, OCR races, backups made during concurrent changes, byte-for-byte restoration, missing files and unsafe archive paths. Tag tests cover exact membership, symbols, case, mixed comma separators, restart and pagination after intersection with keyword and starred filters.

Capture-geometry tests cover reverse selection, negative coordinates, clamped movement, resizing past an anchor, rectangle intersection and all eight resize handles.

Mixed-DPI monitors, clipboard interaction with other Windows applications, recognition accuracy and first launch on a clean computer still need hardware testing. Passing automated tests does not mean those scenarios have been verified.
