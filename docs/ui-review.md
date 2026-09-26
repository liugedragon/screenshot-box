# UI check record

English · [简体中文](zh-CN/ui-review.md)

## 0.1.3 · 2026-09-26

Environment: Windows 11 x64, one 2560 × 1440 display at 125% scaling. The checks use synthetic library data and programmatic WPF controls, followed by client-area image inspection.

| Area | Check | Images |
| --- | --- | --- |
| Language settings | System, 简体中文, and English are available. System uses the Windows display language: Chinese selects Simplified Chinese, and other languages select English. Applying a change restarts the app. | [English Settings](ui-review-images/0.1.3/en-settings.png), [System on Chinese Windows](ui-review-images/0.1.3/system-zh-settings.png). |
| Library | Navigation, action buttons, state labels, and details switch language. User titles, tags, notes, and OCR text retain their original contents. Narrow windows retain capture, import, and details actions. | [English library](ui-review-images/0.1.3/en-library.png), [narrow English window](ui-review-images/0.1.3/en-narrow.png), [Chinese library](ui-review-images/0.1.3/zh-library.png). |
| Details | English field labels, unsaved status, and the fixed save footer remain readable in the small editor. | [Small English editor](ui-review-images/0.1.3/en-editor-small.png). |
| Capture and palette | English tool names, hints, parameter labels, and save actions fit the compact toolbar. RGB/Hex controls and Apply use the selected UI language. | [320 DIP pen toolbar](ui-review-images/0.1.3/en-capture-narrow.png), [English palette](ui-review-images/0.1.3/en-palette.png). |

English layout checks covered six tools at four widths (320/560/720/960 DIP). All 24 samples had in-bounds toolbars, fitted labels, and no obscured resize-handle centers; see the [layout log](ui-review-images/0.1.3/layout-log.txt). The language selector's three choices and selected policy are also checked by the [application integration tests](validation.md#013--2026-09-26).

Images show built application windows. The checks do not include a full manual language-change/restart flow or installing other Windows display-language packs. System errors and Windows file dialogs may use the operating system's language. Other layout and hardware limits remain below.

## 0.1.2 · 2026-09-26

Environment: Windows 11 x64, one 2560 × 1440 display at 125% scaling. Checks launch WPF windows, invoke controls, export client areas, and inspect the images. An isolated library and synthetic course images are used. A complete manual input pass, mixed-DPI hardware, and physical display changes remain untested.

### Library, details, and Settings

| Issue | Change | Image or result |
| --- | --- | --- |
| A 90 DIP sort box truncated Chinese options; long tags displaced tools. | Increased the sort box to 112 DIP, allocated remaining space to a truncated heading, and bounded tags with complete tooltips. | [Light library](ui-review-images/0.1.2/ui-light.png), [narrow window](ui-review-images/0.1.2/ui-narrow.png). |
| Save and collect wording was mixed. | Capture uses “保存并复制” (Save and copy), the category uses “最近保存” (Recently saved), and metadata uses “保存修改” (Save changes). | [Empty library](ui-review-images/0.1.2/ui-empty.png), [dark library](ui-review-images/0.1.2/ui-dark.png). |
| Main details and the separate editor could overwrite each other's edits; drafts had no visible state. | One editing entry per image, an unsaved indicator, drafts retained on close, and updated state after saving. | [Editor](ui-review-images/0.1.2/ui-editor-dark.png), [small editor](ui-review-images/0.1.2/ui-editor-small.png); draft and database checks passed. |
| Tag navigation used keyword search, admitting untagged images with the word in OCR. | Exact tag-member filtering before keywords, sorting, and pagination. | UI tag check and Core regressions passed. |
| Preview intercepted scrollbar clicks; a 5% minimum prevented fitting tall images. | Pan only from the image, fit tall images to the current window, and convert actual size using current DPI. Added copy, export, and Ctrl+C/0/1. | [Original with line highlights](ui-review-images/0.1.2/ui-preview.png); a 20000 px image fitted on resize, and a 1000 px original occupied 1000 physical pixels. |
| Settings status scrolled out of view. | Grouped settings, with fixed bottom status and Done button. | [Top](ui-review-images/0.1.2/ui-settings.png), [bottom](ui-review-images/0.1.2/ui-settings-bottom.png), [small light window](ui-review-images/0.1.2/ui-settings-small-light.png). |
| Thumbnail size and tool parameters were not saved promptly. | Debounced thumbnail persistence, tool preferences saved after capture/on exit, and loaded at startup. | Preference persistence paths checked. |
| Preview shortcuts could intercept buttons' Space and Enter. | Limit shortcuts to the image list and exclude button event sources. | Window key-handler checks with the Save button as source passed. |

[Diagnostics](ui-review-images/0.1.2/ui-diagnostic.png) shows missing originals, recognition failures, and model information. Short editors scroll to all fields; narrow library windows collapse details and offer a separate editor.

Image-export checks also found omitted root-layout margins in client-area snapshots. Settings and editor images were regenerated after correcting the export bounds.

### Capture tools and palette

| Tool | Parameters and behavior | Images |
| --- | --- | --- |
| Pen, arrow, rectangle | Red `#E63737`, blue `#1769C2`, green `#18A75B`, yellow `#F4C430`, white, and black; 1–32 physical-pixel width. Each annotation keeps its own color and width. | [Pen](ui-review-images/0.1.2/capture-large-pen.png), [arrow](ui-review-images/0.1.2/capture-large-arrow.png), [rectangle](ui-review-images/0.1.2/capture-large-rectangle.png) |
| Palette | RGB sliders, Hex input, color preview, and Apply. Invalid input keeps it open with feedback. | [Palette](ui-review-images/0.1.2/palette-large.png), [light window](ui-review-images/0.1.2/palette-light.png) |
| Eraser | 4–80 physical-pixel size; restores source pixels along a path, removing pen, arrow, rectangle, or mosaic. | [Eraser](ui-review-images/0.1.2/capture-large-eraser.png) |
| Mosaic | 6–32 physical-pixel blocks; each region keeps its parameters. | [Mosaic](ui-review-images/0.1.2/capture-large-mosaic.png) |
| History | Ctrl+Z undo, Ctrl+Y redo, undoable clear; new annotations replace the previous redo branch. | [Selection toolbar](ui-review-images/0.1.2/capture-large-select.png) |
| Narrow windows | Below 420 DIP, show the current tool plus a menu and only the active parameters. | [Pen](ui-review-images/0.1.2/capture-narrow-pen.png), [eraser](ui-review-images/0.1.2/capture-narrow-eraser.png), [tool menu](ui-review-images/0.1.2/tools-menu-narrow.png) |

There are 27 retained tool images: six tools at four widths produce 24 states, plus two palette views and one menu. All 24 samples at 320/560/720/960 DIP stayed within toolbar bounds and obscured no resize-handle centers. See the [layout and control log](ui-review-images/0.1.2/validation-log.txt). Control checks applied a 13.25 px width, RGB `#7F3FBF`, and Hex `#8247CB`.

Inspection led to three corrections: a narrow toolbar covering two handles, global text styles overriding compact button sizes, and a slider without a visible track. The toolbar has independent light styling with consistent selected, hover, focus, and disabled states. Blue marks the primary save action.

The [synthetic pixel probe](../tests/ScreenshotBox.Capture.Probe/README.md) passed all 35 annotation-output checks. Twenty desktop captures kept GDI handles at 1→1. Hotkey conflicts, cropping, undo, and cancellation are recorded in the [native runtime log](ui-review-images/0.1.2/native-runtime-log.txt).

### Current layout limits

Thumbnails identify images; small text needs the original preview. Short editors require scrolling. When space is limited, the capture toolbar may sit inside the selection. Export excludes the toolbar, palette, and brush-size ring. The 24 layout samples do not exhaust every selection position and display arrangement. See [outstanding checks](validation.md#outstanding-checks).

## 0.1.0 / 0.1.1 history

| Issue | Change and historical images |
| --- | --- |
| The dark app theme mixed light and dark capture buttons. | Added independent light toolbar colors. [Before](ui-review-images/before/capture-large-select.png), [after](ui-review-images/capture-large-select.png). |
| Expanding pen parameters placed the toolbar outside the window or over handles. | Remeasured and repositioned it, with less spacing in narrow windows. All 12 four-width/three-mode samples passed. [Narrow pen](ui-review-images/capture-narrow-pen.png), [historical log](ui-review-images/validation-log.txt). |
| The preview occupied too much of short editors. | Preview height became 140 DIP normally and 90 DIP in short windows, with a fixed save footer. [Before](ui-review-images/before/editor-light-small.png), [after](ui-review-images/editor-dark-small.png). |
| Empty libraries showed a large disabled details form; navigation selection was unclear. | Added selection guidance and consistent selected states. [Empty](ui-review-images/ui-empty.png), [light](ui-review-images/ui-light.png), [dark](ui-review-images/ui-dark.png). |
| Dark Settings had white text on white; detail scrollbars touched content. | Corrected theme resources and added 12 DIP right spacing. [Settings](ui-review-images/ui-settings.png), [dark editor](ui-review-images/editor-dark.png). |
| Actual size used DIP, enlarging images at 125% scaling. | Converted using current-window DPI; a 1000 px image occupied 1000 physical pixels. [Preview](ui-review-images/ui-preview.png). |

Those versions had two pen colors and two widths. Current 0.1.2 parameters are listed above. Historical images retain their original version's interface.
