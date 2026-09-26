[简体中文](../ui-review.md) | English

# UI review

On 2026-09-26, actual WPF windows were launched on Windows, their client areas exported and the images inspected individually. The display was 2560×1440 at 125% scaling. Editors and capture toolbars used a temporary library and synthetic course material. Capture backgrounds were not the user's desktop or a new product feature. Errors were recorded only in validation logs.

## Capture toolbar and editor: review and revisions

The first review found specific problems beyond whether the application could launch:

| Finding | Change | Observed result and evidence |
|---|---|---|
| Dark theme turned ordinary capture actions black while mode buttons stayed light. | The capture toolbar now has a fixed light palette, a blue Save and copy action, a pale-blue selected tool and an explicit Cancel button. | [Before](../ui-review-images/before/capture-large-select.png) and [after](../ui-review-images/capture-large-select.png). Theme changes no longer mix black buttons into the toolbar. |
| The pen originally had only a fixed red color, with no width control. | That iteration added red/blue and thin/thick choices, per-stroke color/width and Ctrl+Z undo. | [Red thin and blue thick strokes](../ui-review-images/capture-large-pen.png). Selected tool/color/width are distinguishable; Undo is disabled when history is empty. |
| After expanding pen options, toolbar placement used the old height. Its bottom reached 741 DIP in a 720 DIP window. | Remeasure after expansion or mode changes, reposition when space below is insufficient and wrap long buttons to the available width. | All 12 final overlay samples passed bounds assertions. The [320 DIP layout](../ui-review-images/capture-narrow-pen.png) retained color and width controls without clipping. |
| In very narrow layouts, wrapped controls could cover the selection's middle handles. | Below 420 DIP, reduce vertical button padding while retaining full Chinese labels and all actions. | Across 320/560/720/960 DIP and three modes, 0/8 handle centers were covered. Dimensions and Enter/Esc/Ctrl+Z hints remained visible. [Layout log](../ui-review-images/validation-log.txt). |
| The editor preview was too large, exposing only the title in a short window, with the scrollbar against the text. | Reduce preview height to 140 DIP normally or 90 DIP in a short window; add 12 DIP right padding and a fixed Ctrl+S Save footer. | [Before](../ui-review-images/before/editor-light-small.png) and [revised dark small window](../ui-review-images/editor-dark-small.png). Title and notes remain editable; other fields scroll, while Save stays visible. |

The final review inspected 18 images: light/dark editors in normal, OCR and minimum-size views, plus selection, pen and mosaic states at four widths.

- [Light details](../ui-review-images/editor-light.png) and [dark details](../ui-review-images/editor-dark.png): long Chinese titles wrap, fields have a clear order, and text does not disappear against a matching background.
- [Light OCR view](../ui-review-images/editor-light-ocr.png) and [dark OCR view](../ui-review-images/editor-dark-ocr.png): copy text, retry recognition, preview and export remain readable; OCR text scrolls while the save footer stays visible.
- [Mosaic state](../ui-review-images/capture-small-mosaic.png): content becomes real pixel blocks, existing red/blue strokes remain, and the active mode is clear.
- [Capture toolbar under the light app theme](../ui-review-images/capture-light-mosaic.png): long Chinese buttons are complete and the app theme does not change toolbar styling.

## Main window and settings

The main window and Settings were also launched, exported and inspected. The reviewer inspected six latest images and found these issues:

1. An empty library still displayed a large disabled detail form. It now prompts users to select an image and points to capture/import.
2. Navigation did not clearly mark the current category. It now uses a consistent blue selected state.
3. Summary line breaks and height squeezed content. Summaries now wrap to two lines and truncate.
4. A dark-settings ComboBox displayed white text on white. Its actual template and themed foreground/background/borders were corrected.
5. The details scrollbar sat too close to text. Content now has 12 DIP of spacing.
6. Blue navigation text had low contrast in dark mode. A brighter blue improves the selected-item contrast within the same accent family.
7. Actual size originally used DIP and enlarged images at 125% scaling. Conversion now uses the window's DPI; a 1000px image measured 1000 physical pixels in actual-size mode.

Evidence: [empty library](../ui-review-images/ui-empty.png), [light main window](../ui-review-images/ui-light.png), [dark main window](../ui-review-images/ui-dark.png), [narrow main window](../ui-review-images/ui-narrow.png), [settings](../ui-review-images/ui-settings.png), and [original preview with text highlights](../ui-review-images/ui-preview.png).

The empty library offers capture and selection guidance, navigation selection is consistent, light/dark inputs are readable, detail titles wrap, card summaries stay within two lines, narrow windows retain a Details entry, and highlights do not obscure the text. Card text is relatively small: thumbnails are for recognition, and reading the source requires opening preview.

The settings images also check agreement between the selected theme and displayed colors. The final probe synchronized its synthetic preferences with the dark theme. Code inspection or editor-only checks are not substituted for main-window review here.

## Historical 0.1.0 / 0.1.1 checks and limits

Beyond appearance, isolated editor checks passed for unsaved drafts, coalescing 100 recognition events into one refresh, per-item draft retention, narrow-window edits reaching the database, hiding every application window during capture and restoring their visibility on cancel.

Native checks covered real desktop capture, retaining the old hotkey on conflict, cancellation without output, pen/mosaic output pixels and undo restoring originals. The added red/blue and width choices were also verified in exported pixels.

Limits recorded for those versions:

- The editor image is a thumbnail; small text should be read through Preview original. Short editor windows require scrolling to all fields.
- A complete toolbar may sit inside the selection when a very narrow display leaves insufficient space. Export does not contain the toolbar. All eight handle centers stayed visible in the checked samples, but no claim covers every selection position.
- The toolbar was deliberately simple and did not implement all QQ capture features. In those versions the pen had only two colors and two widths, and mosaic used rectangular regions.
- Available hardware had one display. Geometry tests covered negative coordinates, reverse selection and crossing resize anchors; actual mixed-DPI multi-monitor checks remained outstanding.

Assessment at that stage: the ordinary path, theme readability and small-window editing were ready to deliver. Inspection found mixed colors, expanded-toolbar clipping and obscured resize handles; subsequent exports were inspected again and checked for bounds. Multi-monitor and very narrow-selection limits remained documented for future user feedback.

## 0.1.2 annotation tools and palette

The two-color/two-width descriptions above are historical 0.1.1 results. Version 0.1.2 adds six common colors, arbitrary custom colors, arrows, rectangles, erasing, redo and undoable clear. Native action labels consistently use Save.

| Tool or action | Current behavior | Evidence |
|---|---|---|
| Pen, arrow, rectangle | Red `#E63737`, blue `#1769C2`, green `#18A75B`, yellow `#F4C430`, white and black; shared continuous 1–32 physical-pixel width. Each annotation keeps its own color and width. | [Pen and controls](../ui-review-images/0.1.2/capture-large-pen.png), [arrow](../ui-review-images/0.1.2/capture-large-arrow.png), [reverse rectangle](../ui-review-images/0.1.2/capture-large-rectangle.png). Pixel checks cover six colors, 7.5/32px widths, arrowheads, reverse borders and unfilled interiors. |
| More colors | A rounded palette in the capture session has functional RGB sliders, Hex input, preview and Apply. | [Actual palette](../ui-review-images/0.1.2/palette-large.png). Sliders applied `#7F3FBF`; Hex applied `#8247CB`. Invalid input kept the palette open. Applying closed it without ending capture. |
| Eraser | Continuous 4–80 physical-pixel size. It reveals the frozen original along a path, removing pen, arrow, rectangle or mosaic annotations rather than painting white. | [Erased state](../ui-review-images/0.1.2/capture-large-eraser.png). Pixel checks compare center RGB, surrounding pixels, undo/redo and an origin of `(-320,-140)`. Edges retain normal antialiasing. |
| Mosaic | A 6–32 physical-pixel block-size slider; each completed rectangle keeps its chosen size. | [24px mosaic](../ui-review-images/0.1.2/capture-large-mosaic.png). Tests verify averages for full and partial edge blocks at 6/32px. |
| Undo, redo, clear | Ctrl+Z/Ctrl+Y; erasing and clearing are undoable/redoable; a new annotation after undo invalidates the old redo branch. | The [reproducible pixel probe](https://github.com/liugedragon/screenshot-box/blob/main/tests/ScreenshotBox.Capture.Probe/README.md) ran on Windows with **35 passed, 0 failed**. Clickable UI alone is not treated as pixel verification. |
| Narrow tools area | Only the active tool's parameters expand. At 320 DIP, the current tool, a functional six-tool menu, history and save actions remain. | [Narrow pen](../ui-review-images/0.1.2/capture-narrow-pen.png), [narrow eraser](../ui-review-images/0.1.2/capture-narrow-eraser.png), [tool menu](../ui-review-images/0.1.2/tools-menu-narrow.png). Programmatic menu selection changes the actual capture-session mode. |

The actual windows were rerun and 27 native-interface images retained: 24 states for six tools at four widths, two palette views and one tool menu. Key tool, palette and narrow-window states were inspected individually. At 320/560/720/960 DIP, all 24 samples stayed within toolbar bounds and obscured none of the eight handle centers. See the [layout and interaction log](../ui-review-images/0.1.2/validation-log.txt). Final editor styling was exported during the whole-app review rather than represented by old editor images.

The first attempt did not pass. Added tools covered two middle handles in the narrowest layout. Moving them into a current-tool/menu design revealed another issue: global text styles overrode compact button font sizes. Explicitly binding button text to its own font size let all actions fit above the selection's middle handles. The first circular slider also had no visible track; inspection caught this, a thin track was added, and palette/parameter/menu checks were rerun. Toolbar states now share rounding, hover, focus, selection and disabled styles. Blue marks the primary save action; other colors are annotation choices.

A real slider wrote 13.25px pen width into the capture session. The brush-size ring uses each monitor's physical-coordinate conversion and is excluded from export. Tools expose preference configuration/read APIs for loading and saving local JSON through the main window. A programmatic display-settings callback verified cancellation and unsubscribing; no physical dual-display unplug test was performed.

The current 0.1.2 source was rebuilt for the native runtime probe. Reserved-hotkey rejection, conflicts, preservation of the old registration after a failed update and release passed. **Twenty real 2560×1440 desktop captures kept GDI handles at 1→1.** Actual scaling was 125% and DPI awareness 2 (PerMonitorV2). Desktop images remained in memory and were never saved or published. Crop, color/width output, undo, mosaic pixels and cancel also passed. See the [runtime log](../ui-review-images/0.1.2/native-runtime-log.txt). The independent pixel probe uses only synthetic patterns, not desktop capture or clipboard writes, and can be reproduced by others.

Limits remain: only one display was available; not every selection size or position was exhausted. The toolbar may sit inside a selection when space is tight, but export contains only the original and committed annotations, without toolbar, palette or size ring. Erasing mosaic reveals the previously hidden original pixels. This is deliberate removal of an annotation, not permanent redaction.

## 0.1.2 whole-application review

The frozen version was run with isolated synthetic data. The review covered light/dark libraries, narrow layouts, normal/minimum editors, settings top/bottom/small light view, original-image highlights and diagnostics. Final evidence comes from `artifacts/ui-validation-0.1.2-205412` and was copied into the public image directory. These are actual WPF client-area renders, not design mockups.

The initial image export omitted the root layout margins, clipping the bottom-right edges of Settings and the editor. After correcting the export bounds, the application was rerun and the images inspected again, avoiding a false diagnosis based on cropped evidence.

| Problem | Final correction and evidence |
|---|---|
| A 90 DIP sort box truncated Chinese labels; long tags displaced right-hand tools. | Sort width is 112 DIP; the heading takes remaining width and truncates; tags have bounded width and complete tooltips. [Light library](../ui-review-images/0.1.2/ui-light.png), [narrow library](../ui-review-images/0.1.2/ui-narrow.png). |
| Save and collect wording was mixed. | New captures use “保存并复制” (Save and copy), the category uses “最近保存” (Recently saved), and metadata uses “保存修改” (Save changes). The displayed version comes from the actual assembly. |
| Main details and the separate editor could overwrite each other's edits; drafts had no status. | Main editing pauses while that item's editor is open. Unsaved status is visible; closing preserves the draft and saving clears dirty state. Programmatic title/note editing, closing and subsequent database save passed. [Editor](../ui-review-images/0.1.2/ui-editor-dark.png), [small editor](../ui-review-images/0.1.2/ui-editor-small.png). |
| Tag navigation only filled the search box, admitting untagged images with the word in OCR. | SQLite now filters exact tag membership before combining keyword search, sorting and pagination. The UI tag check passed. |
| Save-over deleted an old PNG before writing, risking loss on failure. | Write and flush a temporary file beside the target before replacing it. A real file lock caused replacement failure while retaining the old file byte-for-byte and cleaning the temporary file. |
| Preview captured every click, intercepting scrollbars; the 5% minimum could not fit long images. | Panning starts only on the image and resets on lost capture. A 20000px image fits and recalculates after both window enlargement and reduction. Actual size respects current DPI; copy/export and Ctrl+C/0/1 were added. [Original and line highlights](../ui-review-images/0.1.2/ui-preview.png). |
| Settings were one long panel with status scrolling out of view; restore required manual exit. | Group settings by purpose, keep bottom status and Done fixed, and restart after successful migration/restore. Busy-close guards and asynchronous failure handling are implemented. [Dark top](../ui-review-images/0.1.2/ui-settings.png), [bottom](../ui-review-images/0.1.2/ui-settings-bottom.png), [small light view](../ui-review-images/0.1.2/ui-settings-small-light.png). |
| Thumbnail size saved only on opening Settings; tool parameters reset each time. | Debounced thumbnail persistence and saving tool preferences after capture/on normal exit restore them at startup. Test mode does not write user preferences. |

[Diagnostics](../ui-review-images/0.1.2/ui-diagnostic.png) reports real checks for that synthetic library, without preset user data, fake counts or online content. Exit, backup and library changes resolve drafts first. Bulk saves freeze editing and recheck for remaining drafts afterward. Manual confirmation-dialog workflows remain on the untested list.

Assessment: the layout is now a consistent, restrained Windows library with readable light/dark views and images as the focus. Collapsed details and scrolling retain functionality in small windows. Navigation and some actions still use text. The palette is six quick colors plus RGB/Hex, not a complete drawing application. Small editors require scrolling, and very narrow selection areas may contain the toolbar. These are explicit tradeoffs, not decorative cover for missing features. Mixed-DPI hardware, external-app paste, a physically offline clean computer and a complete manual input workflow remain unverified.

Before release, keyboard handling was corrected: Space preview and Enter details trigger only in the image list, preserving buttons' own keys. Checks with the Save button as the source of an actual WPF window's key-handler event passed. These are programmatic component checks, not manual keyboard acceptance.
