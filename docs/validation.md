# Test record

English · [简体中文](zh-CN/validation.md)

## 0.1.4 · 2026-09-26–27

Environment: Windows 11 x64, one 2560 × 1440 display at 125% scaling; .NET SDK 10.0.401, runtime 10.0.12, CPU OCR. The Release build completed with 0 warnings and 0 errors, and all 42 Core tests passed.

### Application integration

The built executable ran with isolated synthetic libraries and exited with code 0 in both languages:

| Interface | Result | First OCR |
| --- | --- | ---: |
| English | 43 checks passed | 735 ms |
| Simplified Chinese | 43 checks passed | 605 ms |

Checks cover OCR and original-image line boxes, literal Chinese and symbol searches, metadata, clipboard readback, retry, deletion races, backup restore, atomic overwrite, edit drafts, preview sizing, and language settings. Public reports: [English](ui-review-images/0.1.4/en-self-test.json) and [Simplified Chinese](ui-review-images/0.1.4/zh-self-test.json). They omit local paths and temporary item identifiers; OCR text is synthetic test content.

### Background startup

The startup script ran 13 checks in English and 13 in Simplified Chinese, all passing. User settings remained unchanged. The tests use separate instance identifiers and library directories, with a live native window handle, tray icon, and registered hotkey.

- Background launch never loaded or displayed the library window.
- The hidden window owned the capture hotkey; a competing registration was rejected.
- A second background process exited without opening the library.
- A normal second launch opened the existing library while retaining the tray and hotkey.
- Closing the window returned it to the tray; a later background launch kept it hidden.

Reports: [English](ui-review-images/0.1.4/en-startup-test.json) and [Simplified Chinese](ui-review-images/0.1.4/zh-startup-test.json). Reproduce from the repository root after building:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/test-startup.ps1
```

Use `-ExePath` for an extracted release and `-OutputDirectory` for a new result directory. Actual sign-out/sign-in and Task Manager enable/disable were not performed.

### Installer startup registration

Isolated registration fixtures passed 25 checks for the Inno Setup installer and 20 for the PowerShell installer. They use separate installation identifiers and Run value names; preexisting Run and StartupApproved values remained unchanged.

Coverage includes default startup selection, quoting a custom Unicode path with `--background`, retained upgrade choices, opting out and opting back in, rejecting commands over 260 characters before installation, and unregistering on uninstall. Script checks also confirm that opting out or uninstalling preserves a Run entry owned by another installation and files outside the installation manifest.

StartupApproved values were treated as opaque test data, without decoding the Windows format or writing it from production installers. These checks did not operate Task Manager or perform a real sign-in. Reports: [Inno Setup, 25 checks](ui-review-images/0.1.4/installer-startup-check.json), [PowerShell, 20 checks](ui-review-images/0.1.4/script-startup-check.json).

### Installed candidate and uninstall

On 2026-09-27 local time, the candidate installed to a selected directory containing Chinese characters and spaces. Installation and uninstall exited with code 0. All 497 installed exe/dll/onnx hashes matched the distribution, and the Start menu shortcut was present.

The Run entry quoted the installed executable and included `--background`; StartupApproved remained unchanged. The installed executable passed 13 startup checks in English and 13 in Simplified Chinese. It also passed 43 application checks under each of System, English, and Simplified Chinese. System selected Chinese on the test computer; its first OCR took 990 ms.

A second independent process recovered one interrupted OCR task, returned four course-query results and two note-query results, and found no missing originals. The test process had a Windows-only PATH, a nonexistent DOTNET_ROOT, and an unavailable proxy. Its runtime loaded from the installation directory; the system network was not physically isolated.

Uninstall removed the exe, uninstall registration, and Run entry. The database hash, five originals, five thumbnails, and an extra user file remained unchanged. User settings were unchanged.

The [installation report](ui-review-images/0.1.4/installer-check.json) identifies the earlier tested candidate by SHA-256 and omits local paths and temporary identifiers. The final application was rebuilt from [source revision c6739d3](https://github.com/liugedragon/screenshot-box/commit/c6739d39c843bd360b885a3becdfeeb50a9ddf90) to align its SourceLink metadata with the committed source. The application code and dependencies were unchanged; executable, assembly, and debug-symbol metadata changed.

The rebuilt application passed 43 application checks. Startup checks passed 13 checks in English and 13 in Simplified Chinese. Chinese OCR took 1865 ms. The [rebuild report](ui-review-images/0.1.4/committed-binary-check.json) records these results. The full installation and uninstall sequence above was not repeated with the metadata rebuild. Final documentation packaging uses this rebuilt binary baseline; download hashes are provided with the release assets. Actual sign-in and Task Manager controls remain unverified.

### Windows icon

Windows `LoadImage` loaded the original ICO at 16, 20, 24, 32, 40, 48, 64, 128, and 256 px. All nine checks returned the requested dimensions, with transparent and opaque pixels. The [icon report](ui-review-images/0.1.4/icon-load-test.json) identifies the asset by SHA-256. Source and regeneration instructions are in [design](design.md#application-icon).

## 0.1.3 · 2026-09-26

Environment: Windows 11 x64, one 2560 × 1440 display at 125% scaling; SDK 10.0.401, bundled runtime 10.0.12, CPU OCR. The App Release build completed with 0 warnings and 0 errors.

### Application integration

The built `ScreenshotBox.exe` ran directly with separate synthetic libraries under three language policies:

| Policy | Effective UI | Result | First OCR |
| --- | --- | --- | ---: |
| System | Simplified Chinese on the test computer | 43 checks passed | 595 ms |
| English | English | 43 checks passed | 566 ms |
| 简体中文 | Simplified Chinese | 43 checks passed | 589 ms |

The checks cover language-setting persistence, localized resources and application errors, legacy settings without a language, unknown-setting fallback, manual overrides, System-policy persistence, and the three-choice language control. Existing checks for Chinese OCR, search, metadata, clipboard readback, retry, backup/restore, atomic overwrite, edit drafts, and preview sizing passed in each run. User-supplied titles, notes, tags, and recognized text remain in their original language.

System-language mapping is parameterized: `zh-CN`, `zh-TW`, `zh-HK`, and `zh-Hant` select Simplified Chinese; `en-US`, `fr-FR`, `ja-JP`, `de-DE`, and `ko-KR` select English. Windows on the test machine has a Chinese display language; other operating-system languages were not installed or switched during testing.

Public reports: [System](ui-review-images/0.1.3/system-self-test.json), [English](ui-review-images/0.1.3/en-self-test.json), [Simplified Chinese](ui-review-images/0.1.3/zh-self-test.json). To run the same integration checks from a built or extracted application directory, create a fresh test-library path for each language:

```powershell
$testData = Join-Path $env:TEMP ("ScreenshotBox-test-" + [guid]::NewGuid().ToString("N"))
.\ScreenshotBox.exe --self-test --language en-US --data-dir $testData
```

Use `System` or `zh-CN` instead of `en-US` for the other policies. The command-line language override is available only in test mode; normal use selects the language in Settings.

### English capture layout

Six tools at 320/560/720/960 DIP produced 24 English toolbar samples. Every toolbar stayed within the window, all labels fitted, and no resize-handle centers were obscured. See the [layout log](ui-review-images/0.1.3/layout-log.txt) and [UI check record](ui-review.md).

### Candidate installation and uninstall

The 0.1.3 release candidate installed to a chosen path containing Chinese characters and spaces, with exit code 0. Directory registration and the Start menu shortcut were correct; all 497 installed exe/dll/onnx hashes matched the distribution.

The installed executable passed 43 checks under each of System, English, and Simplified Chinese. In the System run, the effective language was Chinese, OCR took 556 ms, and working set after OCR was 363.0 MiB. A second independent process recovered one interrupted task, returned four “课程” results and two note-query results, and found no missing originals.

The process used Windows-only PATH, a nonexistent DOTNET_ROOT, and an unavailable proxy; the runtime loaded from the installation directory. System networking was not physically isolated.

Uninstall exited with code 0. The executable and uninstall registration were removed. The database hash, five originals, five thumbnails, and an extra user file remained unchanged.

The [installation summary](ui-review-images/0.1.3/installer-check.json) identifies the tested candidate by its SHA-256. It excludes local absolute paths and task identifiers. This record precedes documentation synchronization into the final package; final download checksums are provided with the release assets. Hardware and manual-test limitations remain listed below.

## 0.1.2 · 2026-09-26

Environment: Windows 11 x64 (kernel 10.0.26340), i7-14650HX, one 2560 × 1440 display at 125% scaling; .NET SDK 10.0.401, bundled runtime 10.0.12, CPU OCR.

This record separates core tests, synthetic-pixel tests, programmatic Windows integration checks, and installation checks. UI checks create WPF windows, invoke controls, and inspect client-area images. A complete manual input pass and other outstanding scenarios are listed at the end. Test images are synthetic.

### Automated tests

| Suite | Result | Coverage |
| --- | --- | --- |
| Core / SQLite | 42 passed, 0 failed, 0 skipped | Chinese short terms and literal symbols, exact tags, search updates, persistence, pagination after filtering, delete/restore, OCR races, backup/restore, selection geometry |
| Capture pixel probe | 35 passed, 0 failed | Positive/negative desktop origins, cropping, six colors, 7.5/32 px widths, arrows, reversed rectangles, 6/32 px mosaic, erasing, undo/redo, clear, and history branching |
| App Release build | 0 warnings, 0 errors | Locked restore and Windows application build |

Exact-tag tests cover exclusion of matching title/OCR words, `%`, `_`, quotes, Chinese/English commas, whitespace, case, edits followed by restart, and combinations with keywords, stars, sorting, and pagination. OCR task tests check that stale results and results for deleted images cannot be written back.

The [Windows CI run](https://github.com/liugedragon/screenshot-box/actions/runs/36243627343) passed 42 Core tests and 35 pixel checks at commit `9d01e0a`. The current workflow is [windows.yml](../.github/workflows/windows.yml).

To reproduce from the repository root on Windows:

```powershell
.\scripts\build.ps1 -Configuration Release
dotnet restore tests/ScreenshotBox.Capture.Probe/ScreenshotBox.Capture.Probe.csproj --locked-mode
dotnet run --project tests/ScreenshotBox.Capture.Probe/ScreenshotBox.Capture.Probe.csproj --configuration Release --no-restore
```

The [pixel probe documentation](../tests/ScreenshotBox.Capture.Probe/README.md) describes its method. It uses in-memory synthetic patterns only. Desktop capture, hotkeys, toolbar layout, and the clipboard are covered by the integration checks below.

### Windows integration checks

- Twenty consecutive 2560 × 1440 desktop captures kept the GDI handle count at 1→1. DPI was 120, with thread DPI awareness 2 (PerMonitorV2). Desktop images remained in memory.
- Hotkey checks covered Alt+A, multiple modifiers, number keys, common reserved system combinations, occupied global hotkeys, preserving the old registration on update failure, and registering again after release.
- A 200 × 160 physical-pixel crop, compositing annotations, undo restoring original pixels, mosaic, and cancellation without output passed.
- Six tools at four widths (320/560/720/960 DIP) produced 24 samples. Every toolbar stayed within the window; each sample obscured 0/8 resize-handle centers.
- Control checks applied a 13.25 px width, RGB `#7F3FBF`, and Hex `#8247CB`; checked invalid-Hex feedback and the six-tool menu; and simulated a display-settings callback that canceled capture and removed its subscription.

Public logs: [native runtime](ui-review-images/0.1.2/native-runtime-log.txt), [layout and controls](ui-review-images/0.1.2/validation-log.txt). Images and layout corrections are in the [UI check record](ui-review.md).

### Distribution and installation

The final installer installed to a chosen path containing Chinese characters and spaces. It exited with code 0; directory registration and the Start menu shortcut were correct. All 497 installed exe/dll/onnx files matched the distribution.

The installed `ScreenshotBox.exe` ran directly with an isolated synthetic library. The test process had a Windows-only PATH, a nonexistent DOTNET_ROOT, and an unavailable proxy. Its runtime loaded from the installation directory. System networking was not isolated.

All 29 boolean checks passed, with process exit code 0:

- A 1000 × 600 Chinese image produced four text lines, source-image line boxes, and confidence values. First OCR took 684 ms.
- Seven queries passed: “课程”, “订单”, “保修”, `AB-20260926`, `English`, `100%`, and `A_B`. Edited notes and tags became searchable.
- Clipboard image readback measured 1000 × 600.
- Recognition failed with a missing original and became searchable after replacement and retry. Deleting, restoring, and requeuing during OCR passed.
- Backup/restore retained images, categories, and text. A second independent process recovered one interrupted task, returned four “课程” results and one original-note result, and found no missing originals.
- PNG and image-export overwrite passed. A locked target retained the old file byte-for-byte; the temporary file was cleaned up.
- Separate-editor drafts, saved state, exclusive editing, and exact tag filtering passed.
- At 125% scaling, actual-size mode displayed a 1000 px image at 1000 physical pixels. A 20000 px tall image fitted the window and recalculated on resize.
- Invoking the WPF window's key handler with the Save button as its event source confirmed that preview shortcuts did not intercept Space or Enter.

Uninstall exited with code 0. The executable and uninstall registration were removed. The database SHA-256, five original images, five thumbnails, and an extra user file in the installation directory remained unchanged.

Package checks covered 617 files, 33 native x64 libraries, and eight Microsoft CRT signatures. Chinese model and dictionary hashes matched their sources. Published 0.1.2 files:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| ZIP | 111,799,384 | `43a934744867e7857dcd0fc432c058d03b376be96b08579d3552d14148b84a57` |
| Installer | 85,945,228 | `78affe92730737e5908effc010b4fad1318dad434b6c8ec37cc3976f6c4a747e` |

Downloads and checksum files are on the [0.1.2 release page](https://github.com/liugedragon/screenshot-box/releases/tag/v0.1.2). Complete installation logs and structured results are local build records at `artifacts/installer-shipping-validation-0.1.2/result.json`; these files are not committed to the source repository.

The executable's self-test can run with a fresh test-library path and writes `self-test.json`. A subsequent independent launch resumes the interrupted task and writes `restart-test.json`:

```powershell
$testData = Join-Path $env:TEMP ("ScreenshotBox-test-" + [guid]::NewGuid().ToString("N"))
.\ScreenshotBox.exe --self-test --data-dir $testData
.\ScreenshotBox.exe --resume-ocr-test --data-dir $testData
```

### Memory observations

The 0.1.2 multi-window integration process used 361.3 MiB of working set after OCR. A 0.1.1 empty library in normal mode, with tray and hotkey enabled but no loaded model, used 148.3 MiB of working set and 94.4 MiB of private memory six seconds after startup. The 0.1.1 multi-window test used 360.3 MiB after OCR and 281.1 MiB after releasing the model following 90 idle seconds. These are measurements on one machine with different window counts and model states.

## History

### 0.1.1

- All 33 Core tests passed; added regression coverage for filtering recent items before sorting and pagination.
- Distribution checks covered Chinese OCR, seven queries, clipboard readback, delete/restore races, backup/restore, and actual pixel sizing.
- A second concurrent launch exited after 440 ms while the first process continued. An independent launch recovered one interrupted task with no missing originals.
- Fixed SQLiteProvider cold start by explicitly initializing the SQLitePCL bundle in Core.

### 0.1.0

- All 32 Core tests passed, with no failures or skips.
- Current-user silent installation, a custom Chinese path, installed-program integration checks, and uninstall passed. Uninstall preserved the database, three originals, three thumbnails, and an extra user file.
- ZIP: 108,968,831 bytes; SHA-256 `601c35fb9d44859f51ad27c937a403313357437e41eb1993d66ac530d9cf789e`.
- Installer: 83,286,837 bytes; SHA-256 `3ad3b6858d9b83e1462c07edb0e5acaabb83e6a7d4fe40eea3b0d9144d5692d6`.

## Outstanding checks

- Actual mixed-DPI dual displays, screens left of or above the primary display, cross-screen mouse selection, and physical display connection/disconnection.
- Pasting into common external applications, a complete manual mouse/keyboard pass, and prolonged use.
- A fresh Windows machine with no development SDK and a physically disconnected network.
- More scaling factors and monitor layouts, and large-library performance.
- The complete manual flow for folder selection, migration, confirmation dialogs, and restart in Settings.

Clipboard readback, geometry tests, simulated display callbacks, and bundled-runtime checks cover their respective components; they do not include the hardware and user workflows listed above.
