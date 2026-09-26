# Design references and style guide

English · [简体中文](zh-CN/design.md)

## References

Official screenshots and documentation from these projects informed the layout and interactions. Dependency sources are in [third-party notices](third-party.md).

| Reference | Design used | How it is used |
| --- | --- | --- |
| [Eagle](https://eagle.cool/) Browse and library interface | Left categories, center thumbnails, right properties; preserved image proportions; Space preview. | Commercial product; layout reference. |
| [ShareX region capture](https://getsharex.com/docs/region-capture) | Frozen desktop, eight handles, selection movement, nearby toolbar, Enter/Esc. | GPL-3.0 project; interaction reference. ScreenshotBox implements its capture module. |
| [PowerToys](https://github.com/microsoft/PowerToys) | Task-based settings, inline hotkey conflict and operation feedback. | MIT project; settings interaction reference. |
| [WPF UI 4.3.0](https://github.com/lepoco/wpfui) | Fluent controls, themes, and states. | MIT dependency through NuGet, with its license retained. |

The reference projects' branding, screenshots, and prose are not used as ScreenshotBox assets.

## Style

| Element | Specification |
| --- | --- |
| Fonts | Segoe UI / Microsoft YaHei UI; 14 DIP body, 12 DIP secondary, 20 DIP headings. |
| Spacing | 4 / 8 / 12 / 16 / 24 DIP. |
| Controls | Inputs and buttons approximately 32 DIP high, 4 DIP corners; focus, hover, selected, and disabled states. |
| Colors | Neutral backgrounds, borders, and text; primary accent `#1769C2`. |
| Themes | System, light, and dark library themes; fixed light capture toolbar. |
| Window | Initial 1200×800 DIP, 176 DIP navigation, 300 DIP details; details collapse below 1040 DIP or manually. |
| Images | Preserve proportions, adjustable thumbnail width; full truncated titles remain available in details. |
| Language | System by default: Chinese Windows display languages use Simplified Chinese, others English; optional explicit language, applied on restart. |

Empty-library messages explain capture and import; empty searches report no matches. OCR states describe recognition, search availability, or a failure with retry. Titles and notes remain searchable before recognition completes.

## Capture tools

Save and copy adds the image to both the library and clipboard. Copy only and Save PNG as are separate actions. Stars are independent of saving.

The toolbar's main row keeps modes, undo, redo, save, and cancel. Only the current tool's options appear. Pen, arrow, and rectangle share six quick colors and 1–32 px width; eraser size is 4–80 px and mosaic blocks are 6–32 px. Narrow layouts wrap; below 420 DIP, all six modes move to a Tools menu.

The palette offers RGB sliders, a preview, and six-digit Hex input. Color and size changes affect later annotations; existing strokes retain their parameters. Selecting a color does not enter undo history.

Each stroke, shape, mosaic, eraser action, and clear is one history operation. New edits clear the redo branch; unavailable history buttons are disabled. Erasing recomposites original pixels. Save and copy use the same composed image, which is also the OCR input.

Global hotkeys combine Ctrl/Alt/Shift with one non-modifier key. Common reserved system combinations are rejected; failed registration keeps the previous combination.

## Editing and preview

Main details and the separate editor share drafts, with only one writable entry point per item. Exit, backup, migration, and restore resolve unsaved edits first. Settings groups shortcuts and appearance, local data, backup and restore, and About, with a fixed operation-status area.

Preview supports keyboard commands, tall-image fitting, actual pixel size, and image dragging while preserving scrollbar behavior. OCR highlights share the image's scale and pan.

## Resource use

WPF provides the desktop interface. OCR uses a serial CPU queue, limited threads, and a reused model; the model is released after 90 idle seconds. Thumbnails decode at display size, and results are paginated. Runtime and native libraries are bundled. Package and memory measurements are in [validation](validation.md); layout records and screenshots are in [UI inspection](ui-review.md).
