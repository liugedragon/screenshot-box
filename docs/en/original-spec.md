[简体中文](../original-spec.md) | English

# Original development specification

This is the historical prompt used to start development, followed by the recorded 0.1.2 scope updates. Its original introduction said that the work at that point delivered a plan and prompt only. It is retained here as a requirement record, not a claim that every requirement has been implemented or verified. For current results, see [Validation](validation.md) and [Known limitations](limitations.md).

---

Act as the engineer responsible for product design, Windows desktop development, UI design, testing and distribution. Build a usable, installable open-source Windows application, provisionally called “截图资料盒 / ScreenshotBox”.

It is intended for students, office workers and home users. Its main purpose is to capture a screen region, save it locally, recognize the image's text automatically, and later find the screenshot by keyword and locate the original text. It should work offline by default, without an account, API key, Python or discrete GPU.

My computer runs Windows with an i7-14650HX and RTX 4060, but the software must not depend on that GPU. Prioritize Windows 11 x64.

Complete a concrete design first, then continue through development, testing and packaging. Do not stop at suggestions, plans, static interfaces or code snippets. Do not replace production functionality with simulated capture, preset recognized text or fake search results.

## 1. First-release scope

The first release must implement:

- A configurable global hotkey, system tray and single-instance operation.
- Drag-to-select capture, eight resize handles after releasing the mouse, movement of the whole selection and actual pixel dimensions.
- Save to the library and copy, copy only, save PNG and cancel. The original Chinese wording used “收藏” for saving to the library.
- Import existing PNG/JPEG images by dropping them into the app, including batch import with per-item failures.
- A local screenshot library with thumbnails, creation times, titles, notes, tags and a favorite marker.
- Background Chinese/English OCR with retry; recognition failure must not lose the image.
- Search across titles, notes, tags and image text; open results to view the original image and matching text region.
- Image zoom, copying recognized text, image export, an in-app recycle bin and restore.
- Settings, recoverable backups, a Windows distribution and usage documentation.

Scrolling capture, recording, translation, semantic search, automatic privacy detection and cloud sync are outside the first release. The original plan deferred arrows, text annotations, solid redaction and editable copies to the next version, prioritizing capture, saving, retrieval and recovery.

## 2. Use real UI references

Review these projects' official screenshots and documentation before designing the interface, and record specific decisions:

- [Eagle](https://eagle.cool/): library browsing, categories, thumbnails and details.
- [ShareX region capture](https://getsharex.com/docs/region-capture): region selection, adjustment and nearby toolbar.
- [PowerToys](https://github.com/microsoft/PowerToys): Windows utility settings and clear status messages.
- [WPF UI](https://github.com/lepoco/wpfui): mature Fluent controls and themes.

These are functional and visual references, not permission to copy trademarks, proprietary icons, paid assets or complete source code. Record the references and our layout choices in `docs/design-reference.md` (the historical requested filename).

Use a restrained, compact, clear Windows desktop layout. User images should be the focus. Avoid blue-purple gradients, stacks of glass cards, large welcome banners, decorative statistics, capsule-shaped controls everywhere, unnecessary animation and marketing copy.

Use a consistent style guide:

- Mature Fluent controls with system/light/dark themes and readable text on ordinary displays.
- System fonts; body text about 14 DIP, secondary text 12 DIP and page titles 18–20 DIP.
- A 4/8/12/16/24 DIP spacing scale. Central definitions for fonts, colors, corner radii and icon sizes.
- Neutral colors with one primary accent. Dangerous and failed states also use text.
- One clearly licensed icon family; no emoji standing in for tool icons.
- Normal, hover, focus, selected, disabled, loading, failed and empty states.
- Tooltips and keyboard access for small icon actions. Long Chinese titles must not break the layout.
- No fake statistics or clickable entries for unimplemented features.

## 3. Main-window layout

Use a library layout, initially about 1200×800 DIP, with support for smaller windows and high scaling:

- A roughly 200–230 DIP left column: all screenshots, recently saved, starred, tags and recycle bin; settings at the bottom.
- A top toolbar with a prominent search box and a few actions such as New capture, Import and Sort.
- A central image grid with adjustable thumbnail sizes, preserved image proportions, and modest space for titles and times.
- A collapsible 280–320 DIP details column with title, notes, tags, OCR status, recognized text, copy and export.
- Space opens a large preview with zoom, fit-to-window and actual-size modes.
- On narrow windows, collapse details or use a drawer rather than shrinking text to force three columns into the available space.

The empty-library prompt should say “按快捷键框选截图，或拖入图片。” (“Use the hotkey to capture an area, or drop in an image.”) OCR statuses should say “正在识别” (“Recognizing”), “可搜索” (“Searchable”) and “识别失败，可重试” (“Recognition failed; retry available”). Keep implementation details such as library names and inference backends out of ordinary workflows.

## 4. Complete region-capture interaction

1. The initial hotkey can be Ctrl+Alt+S. If registration fails, prompt for a different shortcut. Do not take over the system screenshot shortcut.
2. Hide this application's windows before capturing the desktop, then show the overlay. The output must not contain its own overlay or toolbar.
3. Drag any rectangle with the left mouse button, including leftward or upward reverse selection.
4. Keep the selection after release. Provide four corner and four edge handles, movement from inside the selection, and actual pixel width and height.
5. Place the toolbar near the selection and inside the screen, without covering the intended content.
6. Enter saves to the library and copies. Esc cancels without creating an image or library record.
7. Provide Copy only, Save PNG and Reselect. Very small selections and screen-edge dragging need clear behavior.
8. After saving, return immediately to the previous application without requiring a title or waiting for OCR.
9. Report successful saving separately from failed copying, so users do not think the image was lost.

Use explicit physical-pixel coordinates, with WPF logical coordinates converted separately per monitor. Account for 100%/125%/150%/200% scaling, mixed scaling, displays to the left or above the primary monitor, selections across displays, and connection changes. Do not apply the primary monitor's scale to all monitors.

First validate separate per-monitor overlays with one unified desktop-pixel selection. Handle or document cross-screen composition, gaps between screens and HDR limits. Explain failures to capture protected content honestly.

## 5. Technology and early checks

Prefer C#, .NET 10 LTS, WPF, MVVM, WPF UI and Microsoft.Data.Sqlite. Choose stable versions supporting the target system and lock dependencies.

Use real Windows APIs or mature components for hotkeys, tray, clipboard and capture. Image conversion and OCR must be distributable and CPU-based. Do not introduce a web server or require users to install development tools.

Complete two small technical checks before continuing the main application:

- Real Windows region capture: verify that the final image's position and pixel dimensions match the selection; test mixed-DPI hardware where available.
- Real offline Chinese OCR: run the distribution without network access, return text and original-image boxes, and verify that local libraries and models are complete.

Evaluate [RapidOcrNet](https://github.com/BobLd/RapidOcrNet) first and compare [RapidOCRCSharp](https://github.com/RapidAI/RapidOCRCSharp) if necessary. Verify the selected version's API, dependencies, models and licenses rather than inventing calls.

The RapidOcrNet defaults considered for this specification favor Latin recognition. Chinese requires an explicitly configured, compatible recognition model and dictionary. Demonstrate actual mixed Chinese/English input; initializing a default model is not proof of Chinese support.

If changing the technology, explain the practical reason and retain equivalent functionality and delivery goals. Do not turn the application into a display webpage to avoid difficult requirements.

## 6. Local data and background tasks

Store images and SQLite in a user-writable data directory, not a temporary folder, installation directory or location dependent on the current working directory. Organize originals, thumbnails and the database separately.

Each screenshot should retain its unique ID, image path, original dimensions, creation time, title, notes, tags, starred status, OCR status, recognized text, text boxes, engine/model version and deletion state.

Show the image as soon as its original has been saved. Limit OCR concurrency, reuse models and keep the UI responsive. Restart must resume interrupted work. Retry, deletion and old task completion must not overwrite each other; an old OCR task must not write a deleted item back into the library.

Use a recoverable sequence for file writes and database commits. At startup, check missing originals and unfinished tasks and show clear errors. Indexes may be rebuilt; originals must never be silently lost. A backup must contain a consistent database, originals and required metadata. Copying an actively written database alone is not a complete backup.

Data stays local by default. Do not commit actual screenshots, OCR text, data files or secrets to Git. Use synthetic or licensed, de-identified material for examples and demonstrations.

## 7. Chinese search and location mapping

The first release provides keyword-contains search without requiring a large language model. It must find two-character words such as “课程”, “订单” and “保修”, plus mixed Chinese/English text, identifiers, dates and user notes.

Check the actual limits of SQLite FTS5 tokenizers and trigrams: trigram full-text queries shorter than three Unicode characters cannot directly match. Use parameterized substring queries or another tested fallback for short terms. Default English tokenization is not a solution to Chinese search.

Give quotes, percent signs and underscores explicit semantics without causing SQL or full-text syntax errors. Queries may be normalized, but retain original OCR text and the mapping back to its boxes.

Results show thumbnails, titles and match summaries. Highlights on the original image follow zoom and pan. Convert any OCR preprocessing scale or rotation back to original-image coordinates. If the engine provides only whole-line boxes, highlight whole lines rather than inventing character-level accuracy.

Explain that a newly added image is not yet searchable by its image text while OCR is pending, although its title is searchable. Recognition failures offer retry without fabricating successful text.

## 8. Engineering and quality

Separate capture, coordinate conversion, OCR, image persistence, search and UI state into understandable modules. Do not put all logic into window event handlers. Keep the project simple without unnecessary microservices, plugin frameworks or abstractions.

Use consistent cancellation, error feedback, logging and resource cleanup. Logs must not contain complete screenshot text. Manage hotkey release, model disposal, image memory, window lifetimes and shutdown.

Load thumbnails as needed and virtualize the grid; do not decode large numbers of originals at once. Debounce search sensibly and prevent old results from replacing new ones. Record performance targets and measure them; do not invent latency or accuracy figures.

## 9. Implementation order

Proceed in dependency order, leaving runnable, reviewable results at each stage:

1. Inspect the workspace and instructions; record dependencies, UI references and the actual environment.
2. Complete capture/OCR checks and choose versions and models.
3. Build the main window, image viewer and settings with mature controls. Identify any demonstration data clearly.
4. Connect real capture, PNG persistence, the native clipboard and the library list.
5. Connect real OCR, Chinese search, text-box highlights, titles, notes and tags.
6. Implement import, recycle bin, backups, restore, interrupted-task recovery and failures.
7. Run functional tests and review every UI page; fix observed problems.
8. Produce the Windows distribution, documentation and genuine demonstration material.

Continue through reasonable, authorized work without repeatedly asking about routine implementation choices. Explain specific blockers only when user information or external permission is necessary. A scenario that cannot be verified must remain unverified, while unaffected work continues.

## 10. Tests and acceptance

Write meaningful tests for forward/reverse selection and scaled coordinates, short Chinese queries, search/index updates, recycle-bin consistency, deletion/retry races, and backup/restore.

Windows checks should include:

- Hotkey conflicts, repeated startup, continuous capture, Esc cancellation, eight-handle resizing and moving the selection.
- Single displays and available dual-monitor, mixed-DPI, negative-coordinate and cross-screen scenarios. Mark unavailable scenarios unverified.
- Pasting copied images into Paint, Word or available common applications. Clipboard contention gets bounded retries.
- Chinese, English, mixed text, small fonts, rotated images and failed-recognition retry.
- “课程”, “订单”, “保修”, numbers, symbols and edited notes.
- Exit/restart during OCR, task completion after deletion, Chinese paths, missing originals and controlled write failures.
- Light/dark themes, 125%/150% scaling, small windows, long titles, empty lists and keyboard use.
- Restore into a new directory, then open originals, search text and inspect tags.

On a Windows environment without the development SDK, Python or CUDA, validate the distribution offline through the complete flow: start → real capture → save → Chinese OCR → search → highlight → exit/restart → find the image again. A successful build does not replace this workflow.

## 11. Delivery

Provide full source, build instructions, a self-contained Windows x64 distribution ZIP and an executable that can actually be tested. Prioritize a usable distribution directory before considering an installer. Do not omit models or native libraries to make the package look like a single file.

Include a README, usage guide, design references, architecture, third-party licenses and model sources, validation, known limitations and distribution SHA-256 checksums. State the size and source of bundled models. Any download step must be visible and initiated by the user.

Publish to GitHub within the authorization already given in the session. If publishing is not authorized, finish the local deliverable and prepare release material first. Never publish actual user data. Attach any created pull request to the current task.

Report completed features, passed checks and unverified scenarios, with launch locations and demonstration steps. Speed, recognition accuracy, user numbers and community contribution claims require real evidence.

Use mature open-source components while distinguishing their capabilities from our UI, task orchestration, Chinese-search strategy, coordinate mapping and recovery mechanisms. A student should be able to read, understand and explain the core implementation for a real demonstration and future maintenance.

## Scope update on 2026-09-26: 0.1.2

The original prompt and its historical wording are retained above. The user subsequently requested that Chinese UI wording for adding an image to the library change from “收藏” to “保存”: the main action becomes “保存并复制” (“Save and copy”) and the recent category becomes “最近保存” (“Recently saved”). Stars remain a separate marker.

Annotations gain six pen colors and 1–32px pen width; a 4–80px eraser that removes annotations and restores original screenshot pixels rather than painting white; 6–32px mosaic blocks; Ctrl+Z undo and Ctrl+Y redo. Clear annotations is also undoable. Show size and color options only for the current tool. Keep save/cancel clear and avoid clipping the toolbar on narrow layouts.

After implementation, use the real application to check colors, both size limits, eraser restoration, undo/redo, undoing a clear, identical PNG/library output, light/dark themes and narrow windows. Finish source and verification before packaging 0.1.2. Do not overwrite the published 0.1.0 or 0.1.1 packages.

### Additional update that day: the whole UI and palette

The user also requested a more polished interface, inspection and correction of every page, and a genuine custom palette. Add arrows and rectangles using the pen's adjustable colors and line width. Beyond six quick colors, allow arbitrary colors, show the current color and exact value. Check action access, save state, keyboard behavior, narrow layouts and light/dark themes in the library, editor, preview and settings, and fix observed problems before release.
