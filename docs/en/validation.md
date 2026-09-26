[简体中文](../validation.md) | English

# Validation record

Recorded on 2026-09-26. Environment: Windows 11 x64, kernel 10.0.26340; i7-14650HX; one 2560×1440 display at 125% scaling. SDK 10.0.401 and bundled runtime 10.0.12. All OCR runs on the CPU.

Historical checks remain below with their version numbers. Program-driven interface checks are identified separately from manual acceptance; passing them does not make untested hardware scenarios pass.

## Automated tests

Version 0.1.0 ran 32 tests on Windows with .NET 10: 32 passed, none failed or skipped, in about one second. Local record: `artifacts/test-results/core-final.trx`. Source: `tests/ScreenshotBox.Core.Tests`.

Version 0.1.1 added a regression for filtering recent items before sorting and pagination. It uses 15 old and six recent records with a page size of two, checking two oldest-first pages, newest-first sorting, combined queries and exclusion of deleted items. All 33 Core tests passed, with no failures or skips, in about two seconds. This is evidence for the core module, not acceptance of the revised UI.

Coverage includes one- and two-character Chinese substring queries; mixed Chinese/English; literal quotes, `%` and `_`; metadata edits and search updates; reopening persisted data; pagination and whole-library oldest-first sorting; tags across pages; soft delete and restore; generations preventing stale OCR writes and writes after deletion; interrupted-state recovery; backups of originals, categories and text; restore to a new directory; malicious archive paths and invalid archives; reverse selection; negative coordinates; cross-screen geometry; movement limits; and handles crossing their anchors.

## Native Windows integration probe

The standalone executable has a PerMonitorV2 manifest. It is not run through a `dotnet` host as a substitute for DPI verification.

- Hotkeys: Alt+A-style combinations, multiple modifiers, D1/NumPad keys, reserved combinations, rejection of occupied keys, retaining the original registration after failure, and successful registration after release.
- Twenty real 2560×1440 desktop captures: GDI handle count stayed at 1→1.
- A physical 200×160 crop, composited pen output, byte-for-pixel restoration after undo, a real 12×12 mosaic, and cancellation without an image all passed.
- Current DPI was 120; thread awareness was 2 (PerMonitor). Only the available single display was tested on hardware.

These are programmatic checks of real Windows APIs and application logic, not manual mouse acceptance. The local log is `.tools/native-validation/probe.out.txt`.

## Full executable integration checks

`ScreenshotBox.exe` was launched directly with an isolated synthetic data directory and no user screenshots.

The synthetic original is 1000×600 and contains:

```text
课程资料 · ScreenshotBox
订单编号 AB-20260926-001
保修日期 2026-09-26
中文 English 100% A_B "quote"
```

The locked Chinese model produced four real text lines, original-image line boxes and mean character confidence information. Queries for “课程”, “订单”, “保修”, an identifier fragment, `English`, `100%` and `A_B` succeeded. Edited notes and tags became searchable immediately. A missing original produced `Failed`; replacing the file and retrying produced `Ready`.

A clipboard bitmap was written and read back at 1000×600. One immediate readback was inconsistent during an idle-memory check; observation with a delay subsequently passed. No actual paste into a common external application has been tested, so clipboard readback is not evidence of that compatibility.

A backup restored into a new directory retained images and searchable text. The first process deliberately left a `Processing` task; a second independent executable launch recovered and completed it. Existing notes were still searchable, with zero missing originals.

Real WPF windows were created and their client areas rendered for light/dark themes, an 800×580 window and long Chinese titles. Whole-line OCR highlights were visible in preview. Scaling and panning share the source-pixel container. Visual inspection found and corrected background contrast using the locked WPF UI default templates.

A separate isolated WPF probe checked coalescing 100 OCR events into one refresh, preserving unsaved input during refresh, retaining each item's draft when switching selection, editing and saving details at 900 DIP, hiding all application windows during capture, and restoring previously visible windows after cancellation. Local log: `.tools/editor-validation/probe.out.txt`.

The user-reported SQLiteProvider startup error was reproduced and fixed. Core explicitly initializes the SQLitePCL bundle rather than relying on reflection-based discovery. Cold start and the integration checks above passed with the corrected executable.

## Measured size and memory

OCR of the synthetic image on this computer took about 0.6 seconds, including initial model loading. This is not a general accuracy claim or a speed guarantee for all images. One integration process used about 301 MiB of working set after recognition and about 237 MiB after releasing the model following 90 idle seconds, including WPF and runtime memory.

The original 0.1.0 ZIP was 108,968,831 bytes; its installer was 83,286,837 bytes. The bundle includes .NET, the Chinese model, ONNX native libraries and verified Microsoft C++ runtime files. All 33 native PE files were x64, with no Linux/macOS native libraries. Distribution and model hashes passed; all eight CRT files passed Microsoft Corporation Authenticode signature checks.

## Original 0.1.0 installation and uninstall

HKCU, HKLM and their 32-bit views were checked for the project's AppId, with no existing installation found. No ScreenshotBox process was running. Original installer SHA-256: `3ad3b6858d9b83e1462c07edb0e5acaabb83e6a7d4fe40eea3b0d9144d5692d6`. Original ZIP SHA-256: `601c35fb9d44859f51ad27c937a403313357437e41eb1993d66ac530d9cf789e`. Those files were kept unchanged rather than replaced by later versions.

The real installer performed a current-user silent installation to `E:\github\screenshot-box\artifacts\installer-validation-0.1.0\安装位置 空格\截图资料盒`. It exited with code 0. The registered directory matched that Chinese path with spaces, and the Start menu shortcut existed. No elevation or restart was required. This verifies that the installation engine accepts a custom path, not that someone manually completed the directory-selection page.

The installed executable ran directly with `--self-test --data-dir` pointing to a separate Chinese-named test directory. It exited 0. OCR, all seven text queries, note/tag updates, clipboard readback, missing-image failure, retry after replacement, backup and restore into a new directory all passed. `System.Private.CoreLib` loaded from the chosen installation directory; the app did not run through a `dotnet` host. A second independent launch recovered the interrupted task without missing data.

The installation's actual `unins000.exe` then exited 0. The executable and uninstall registration were removed. All three originals and three thumbnails in the external synthetic library remained. `library.db` had the same SHA-256 before and after uninstall. A Chinese-named marker placed in the installation directory but not owned by the installer also remained. This verifies removal according to the installation inventory rather than recursive deletion of extra user data.

Local logs and structured results: `artifacts/installer-validation-0.1.0/install.log`, `uninstall.log` and `result.json`. Synthetic images, reports and restored data are retained there without real user screenshots. These checks do not replace clean-machine, mixed-DPI or external-paste testing.

## Final 0.1.1 distribution checks

`artifacts/ScreenshotBox-0.1.1-win-x64/ScreenshotBox.exe` ran directly with isolated synthetic data in a Chinese-named directory. Its test process had only Windows System32 in `PATH`, `DOTNET_ROOT` pointed to a nonexistent directory, and the proxy pointed to unavailable `127.0.0.1:9`. System networking was not isolated, so this is not a physical offline test. The runtime loaded from the distribution's `System.Private.CoreLib.dll`.

The process exited 0. All seven queries, 1000×600 clipboard readback, note/tag updates, missing-original retry, backup/restore and collapsed details in a small window passed. A real queue regression deleted and restored an item during `Processing`, then immediately requested enqueue. The item reached `Ready` without losing its task. At actual 125% DPI, a 1000px image in actual-size mode occupied 1000 physical pixels. A second concurrent launch exited 0 after 440 milliseconds while the first process continued; it did not create a second library.

Another independent executable launch recovered the one interrupted task. “课程” returned four items, the original note returned one, and missing originals numbered zero. Local evidence: `artifacts/release-verification-0.1.1.json` and `artifacts/发行目录验收-0.1.1/restart-test.json`.

The multi-window/synthetic-image process took 627 milliseconds for its first OCR. Working set was 360.3 MiB after recognition and 281.1 MiB after 90 idle seconds. Window counts differed from the preceding measurement; this is not an ordinary empty-library figure. A separate **normal-mode** empty library, with actual tray and hotkey enabled, used 148.3 MiB working set and 94.4 MiB private memory after six seconds, before loading OCR. Local record: `artifacts/startup-memory-0.1.1.json`. The application is native and has no browser, Python or GPU dependency, but it is not currently a tool using only a few tens of megabytes of RAM.

See [UI review](ui-review.md) for the visual checks and revisions. All public demonstration images are synthetic.

## 0.1.2 source and candidate checks

The same-day update standardizes Save wording and adds arrows, outline rectangles, an RGB/Hex palette, 1–32px line width, a 4–80px eraser, 6–32px mosaic blocks, redo and undoable clear. Metadata editing gains an unsaved indicator and one writable editor per item. Preview gains copy/export, long-image fitting and keyboard shortcuts.

The full App Release build completed with zero warnings and errors. All 42 real-SQLite Core tests passed, with no failures or skips, in about two seconds. Added exact-tag tests cover exclusion of matching title/OCR words, percent signs/underscores/quotes, English and Unicode case, Chinese/English commas, whitespace, deletion, editing/restart and pagination after intersection with search and starred filters. Local evidence: `artifacts/core-tests-0.1.2/core-tests-0.1.2.trx`.

The candidate App executable ran directly with synthetic data at `artifacts/ui-validation-0.1.2-205412`, loading `System.Private.CoreLib.dll` from the same directory. Every reported boolean check was true. CPU Chinese OCR took 624 milliseconds and returned four real lines. Seven queries, 1000×600 clipboard readback, edited metadata, missing-file retry, deletion/restoration during recognition, backup and new-directory restore passed. This is one run on a 1000×600 synthetic image, not a performance or accuracy claim for all images. Working set after the multi-window checks was 357.45 MiB, not an ordinary empty-library measurement.

Added programmatic regressions passed: replacing PNG/export targets; a real file lock causing replacement to fail while retaining the old image byte-for-byte and cleaning the temporary file; editor changes, draft retention after closing and clearing dirty state after save; only one writable metadata editor for an item; and exact tag filtering in the UI. At 125% scaling, a 1000px actual-size image occupied 1000 physical pixels. A 20000px-long image fitted the window and continued fitting after enlargement and reduction. The complete report is that directory's `self-test.json`.

All 35 independent synthetic-pixel checks passed. Coverage includes virtual origins `(0,0)` and negative coordinates, six pen colors, 7.5px/32px widths, arrows, reversed outline rectangles, 6px/32px mosaic averages, erasing four annotation types, undo/redo, undoing clear and invalidating redo after a new edit. This probe does not capture the desktop or use files, clipboard, hotkeys or overlays. It runs in Windows CI alongside the 42 Core tests.

The frozen source also passed 20 real 2560×1440 desktop captures with GDI handles at 1→1, awareness 2, actual 125% scaling, preservation of the old hotkey after a conflict, rejection of reserved combinations and re-registration after release. The latest public log is `docs/ui-review-images/0.1.2/native-runtime-log.txt`.

Program-driven native windows checked six tools at four widths: 24 toolbar samples passed bounds assertions, each with 0/8 handle centers obscured. These samples do not exhaust arbitrary selection positions or monitor layouts. Real controls set a 13.25px width, applied RGB `#7F3FBF` and Hex `#8247CB`, kept the palette open with feedback after invalid Hex input, continued capture after palette close, and selected all six tools through the narrow menu. A simulated `DisplaySettingsChanged` callback canceled capture and removed the subscription; no physical monitor unplugging was tested. Local evidence: `artifacts/ui-review-0.1.2` and `.tools/visual-review/probe.out.txt`; public images are in [UI review](ui-review.md).

These are candidate-source and build checks, not advance acceptance of the final installer. All images use isolated synthetic data and program-driven window events; no full manual mouse/keyboard workflow is claimed. The 0.1.0/0.1.1 history and original checksums remain intact.

## 0.1.2 distribution, installation and public CI

An independent GitHub Windows 2025 environment restored locked dependencies, built the App and passed 42 Core tests plus 35 annotation-pixel checks at commit `9d01e0a`: [successful CI run](https://github.com/liugedragon/screenshot-box/actions/runs/36243627343). The build script was corrected to wait only for the `dotnet` process itself, avoiding a timeout caused by a persistent compiler server after the build had passed.

The new setup installed to the custom Chinese path with spaces `artifacts/installer-validation-0.1.2/安装位置 空格/截图资料盒`. Installation exited 0; directory registration and shortcuts were correct. All 497 exe/dll/onnx files matched the distribution. The user's running 0.1.1 portable application remained open. Self-tests used a separate `.Tests` single-instance channel and synthetic library.

The installed executable ran directly with Windows-only `PATH`, a nonexistent `DOTNET_ROOT` and an unavailable proxy. Its runtime loaded from the installation directory. It exited 0 with all 27 boolean checks true; the final button Space/Enter checks raised this to 29. Checks included real Chinese OCR (864ms in this installation run), seven searches, text-box preview, clipboard readback, missing-image retry, delete/restore, backup/restore, protected atomic overwrite, editor and tag behavior, 20000px-image fitting and actual pixel sizing. A new independent process then recovered one interrupted task: four “课程” results, one warranty-note result and no missing images.

Actual uninstall exited 0. The executable and uninstall registration disappeared; the database SHA-256, all five original hashes, all five thumbnail hashes and the extra user marker remained unchanged. Evidence: `artifacts/installer-validation-0.1.2/result.json` and its install/uninstall logs. Installed-page checks were program-driven, and silent installation is not presented as manual installer-wizard acceptance.

The distribution inventory verified 617 file hashes, 33 native x64 libraries and eight Microsoft CRT signatures. The Chinese model and dictionary matched their source hashes. Unused Latin recognition models and other-platform native libraries were absent. At that packaging stage, only final verification documents were synchronized and the 497 binaries remained identical to the installed candidate. A later keyboard fix is recorded below. Final release SHA-256 values come from the adjacent checksum files and GitHub assets, avoiding a circular hash caused by embedding a package's own hash in its bundled documentation. The original 0.1.0/0.1.1 packages remain unchanged.

## Not yet verified

- Actual mixed-DPI dual displays, screens to the left/above the primary display, cross-screen mouse selection and physical display changes.
- Pasting into common applications, a complete manual keyboard/mouse workflow and prolonged repeated use.
- Physical disconnection from the network and a new Windows environment with no SDK. The self-contained executable demonstrably loads its own runtime, but the test computer still has development tools.
- Every scaling factor, every monitor layout and very large-library performance.
- The complete manual folder-selection/migration/restart workflow in Settings. Automated restore to a new directory has passed.

The final keyboard-focus correction limits Space preview and Enter details to the image list, preserving buttons' own key handling. A programmatic check invokes the WPF window handler with the actual Save button as the event source and confirms that it intercepts neither key. This is not real system keyboard input or a full routed-interaction test.
