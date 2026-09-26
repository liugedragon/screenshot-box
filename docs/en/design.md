[简体中文](../design.md) | English

# Design references and style guide

Recorded on 2026-09-26. The initial workspace inspection found no existing application or repository instruction files. The first release is a native Windows desktop library.

| Official reference | What informed the design | Reuse boundary |
|---|---|---|
| [Eagle](https://eagle.cool/), official `home-feature-eagle.png` and Browse description | Categories on the left, thumbnails in the center, properties on the right; preserved image proportions; Space to preview. | Commercial product. Layout reference only; no code, branding or screenshots used as ScreenshotBox assets. |
| [ShareX Region Capture](https://getsharex.com/docs/region-capture), official screenshots and documentation | Freeze the desktop before selection; eight resize handles; selection movement; nearby toolbar; Enter/Esc. | ShareX is GPL-3.0. The capture implementation is our own; its source code was not copied. |
| [PowerToys](https://github.com/microsoft/PowerToys), official tool descriptions | Settings organized by task, inline hotkey-conflict feedback and plain-language status. | MIT. Interaction reference rather than reuse of application code or assets. |
| [WPF UI 4.3.0](https://github.com/lepoco/wpfui), official themes and controls | Fluent themes, consistent control states, system fonts and compact utility layouts. | MIT. Used directly through NuGet, with its license retained. |

## Style

- System fonts: Segoe UI, with Microsoft YaHei UI fallback. Body text is 14 DIP, secondary text 12 DIP and headings 20 DIP.
- Spacing follows 4/8/12/16/24 DIP. Inputs and buttons are about 32 DIP high; corners use a 4 DIP radius. No decorative cards or gradients.
- Neutral backgrounds, borders and text, with one primary accent: Windows blue `#1769C2`.
- System, light and dark themes use WPF UI resources and native hover, focus and disabled states.
- The initial window is 1200×800 DIP. The navigation column is 176 DIP; details are 300 DIP. Details collapse automatically below a 1040 DIP window width and can also be collapsed manually.
- Images retain their proportions. Thumbnail width is adjustable. List titles truncate; detail titles wrap. Smaller windows do not force smaller text.
- Empty library: “按快捷键框选截图，或拖入图片。” (“Use the hotkey to capture an area, or drop in an image.”) Empty search: “没有匹配的截图。” (“No matching screenshots.”)
- OCR status: “正在识别” (“Recognizing”), “可搜索” (“Searchable”) and “识别失败，可重试” (“Recognition failed; retry available”). Titles and notes remain searchable before OCR finishes.
- Annotations are optional. After selection, users can switch to pen, arrow, rectangle, eraser or mosaic, with undo/redo. The output PNG includes the annotations.
- Hotkeys combine Ctrl/Alt/Shift with one non-modifier key, including Alt+A. Reserved system combinations are rejected in advance. A registration conflict preserves the existing hotkey.

## Size and resource use

The application uses native WPF, without a browser runtime, Python or a GPU dependency. OCR runs serially, limits CPU threads, reuses its model and releases it after 90 idle seconds. Thumbnails decode at the target size and the library is paginated. Bundled .NET and ONNX native libraries mean the distribution is larger than a few megabytes. See the measured size and memory use in the [validation record](validation.md).

## Annotation interactions in 0.1.2

“保存并复制” (“Save and copy”) adds the image to the library and clipboard. Copy only and Save PNG remain separate actions. Navigation uses “最近保存” (“Recently saved”). Stars remain an independent marker.

The toolbar keeps tool selection, undo/redo, save and cancel in its main row. Only the current tool's options are shown. Pen, arrow and rectangle share six quick colors and a continuous 1–32px line-width control. The eraser has a 4–80px size control; rectangular mosaic has a 6–32px block-size control. Controls show their numeric values, with clear selected states for color and mode. Narrow layouts wrap; below 420 DIP, the six tools move into a Tools menu without shrinking text or cutting off actions.

The eraser removes annotations along its path and recomposites the original screenshot pixels. It does not paint white, and using it on an unannotated area changes nothing. Each stroke, mosaic rectangle, eraser action and Clear annotations action is one undoable operation. Ctrl+Z undoes; Ctrl+Y redoes. Buttons are disabled when their histories are empty. A new edit clears redo history. Export and library save use the same composed image; OCR reads that output.

The 35 synthetic-pixel checks cover six colors, widths, arrows, reversed rectangles, both mosaic-size limits, restoration of original pixels, undo/redo and undoing a clear. Program-driven layout checks separately cover six modes at four window widths. These checks do not replace manual interaction or multi-monitor hardware tests.

## Shapes and custom palette

Arrow and rectangle tools share the pen's colors and 1–32px width range. Simple icons, Chinese labels and explicit selection states identify the tools. Save, copy, export and cancel remain visible; undo and redo stay in fixed positions.

Six common colors provide quick choices. A separate palette offers continuous RGB sliders, a current-color preview, Hex input and Apply. Users can select non-preset colors and inspect exact values. A custom color affects subsequent pen strokes, arrows and rectangles; existing annotations keep their original colors. Selecting a color neither creates an annotation nor enters undo history.

Program-driven checks verify non-preset RGB/Hex colors, invalid-value feedback, all six tools in the narrow menu and continuing capture after the palette closes. Preferences are stored locally and restored on startup. See the evidence and its limits in [Validation](validation.md).

The main details and separate editor share drafts, while only one editing entry point is writable for an item. Unsaved status stays in a fixed location. Exit and library changes resolve drafts first; backups include changes the user chooses to save. Settings are grouped into hotkeys and appearance, local data, backup and restore, and About, with persistent operation feedback. Preview supports keyboard shortcuts and fit-to-window for long images without changing scrollbar behavior.
