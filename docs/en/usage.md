# User guide

[简体中文](../usage.md) | English

## Install and open

Run the installer and choose an installation folder, or extract the **entire** release ZIP and open `ScreenshotBox.exe`. The app creates a local library on first launch. You do not need an account or a separate runtime installation.

The installation folder and library folder are separate. In Settings, **更改保存位置并迁移资料** (Change save location and move library) moves your images, recognized text, and categories to a folder you choose. If the destination is not empty, the app creates a dedicated subfolder. It restarts after the move and leaves the original library in place.

## Capture a screenshot

1. Press `Ctrl+Alt+S` and drag to select an area. After releasing the mouse, resize it with the eight handles, or switch to **选区** (Selection) and drag inside to move it.
2. Add annotations if needed: pen, arrow, outline rectangle, eraser, or mosaic.
3. Press `Enter` or choose **保存并复制** (Save and copy). The image is saved to your library and copied to the clipboard. **仅复制** (Copy only) does not save a library entry; **另存 PNG** (Save PNG as) lets you choose a file path. `Esc` cancels without creating an image or entry.

The output includes your annotations. OCR runs in the background on that output; capturing returns you to the previous app without waiting for recognition.

In Settings, record a different shortcut using Ctrl, Alt, or Shift, such as `Alt+A`. A reserved or already registered shortcut is rejected, and the old shortcut stays active.

## Annotation tools and colors

The toolbar shows the color and size controls for the current tool. Save, copy, export, and cancel remain visible. On a very narrow screen, the **工具** (Tools) menu still gives access to all six modes.

| Tool | Controls |
| --- | --- |
| Pen, arrow, outline rectangle | Six quick colors: red, blue, green, yellow, white, and black. Line width: 1–32 px. |
| Custom palette | RGB sliders, a six-digit Hex color, and a preview. Choose **应用颜色** (Apply color) or press `Enter`. Invalid values show an error. |
| Eraser | 4–80 px. Removes annotations and restores the original screenshot pixels rather than painting white. Works on all annotation types, including mosaic. |
| Mosaic | 6–32 px blocks. Larger blocks produce stronger visible pixelation. |

Existing strokes keep the color and width they were drawn with. When the palette is open, `Esc` closes it first; press `Esc` again to cancel the capture.

- `Ctrl+Z`: undo. `Ctrl+Y`: redo. Drawing a new annotation clears the redo history.
- **清除标注** (Clear annotations) restores the original image and can also be undone.
- `B`: pen. `E`: eraser. `M`: mosaic. `V`: selection. These letters do not switch tools while you are typing a color value.

The current annotation color, pen width, eraser size, mosaic size, and library thumbnail size are remembered after restarting.

Mosaic is visual pixelation. It is not a secure redaction tool for information that must remain confidential.

## Find and organize images

Open the library from the tray icon. Drag in PNG or JPEG files, or use batch import.

Select a thumbnail to edit its title, notes, tags, or star. Choose **保存修改** (Save changes) or press `Ctrl+S`. Unsaved changes are marked. In a narrow window, use **详情** (Details) or `Enter` to open a separate editor. While that editor is open, the same item's fields in the main window are disabled to prevent conflicting edits.

Closing the editor keeps its draft for the current session. Before quitting, backing up, or changing libraries, the app asks whether to save, discard, or cancel if there are unsaved edits.

Press `Ctrl+F` to search titles, notes, tags, and recognized text. An image marked **正在识别** (Recognizing) can already be found by its title and notes; its image text becomes searchable when OCR finishes.

The sidebar filters by whole tags and can be combined with a search term. Finding a word in a title or OCR text does not assign that tag. Both English and Chinese commas separate tags; surrounding spaces and English letter case are ignored when comparing tags.

## Preview and export

Press `Space` or double-click a thumbnail to preview the original. Matches in OCR text are highlighted with orange **whole-line** rectangles.

| Action | Shortcut |
| --- | --- |
| Zoom | `Ctrl` + mouse wheel |
| Pan | Drag the image |
| Copy image | `Ctrl+C` |
| Fit to window | `Ctrl+0` |
| Actual pixel size | `Ctrl+1` |
| Close preview | `Esc` |

Fit mode follows window resizing, including for tall imported images. Dragging the image does not intercept the scrollbars.

If recognition fails, the image stays in your library. Choose **重新识别** (Recognize again) to retry. Settings → **检查原图和识别错误** (Check originals and recognition errors) shows diagnostic details.

If an original file is deleted outside the app, its thumbnail may still be visible, but preview and export will report the missing file. Restore it from a backup. Save-as and export write a temporary file before replacing the destination; a failed write does not delete the existing target first.

## Back up, restore, and quit

Settings can open the library folder, create a new ZIP backup, or restore into a new library subfolder. Moving or restoring a library restarts the app on success. The settings window cannot close while it is processing the library, and failures show a reason. The original library is not overwritten.

Closing the main window leaves the app running in the tray so the capture shortcut remains available. To quit completely, right-click the tray icon and choose **退出** (Quit).

Uninstalling removes the app, not your library. The application and installer are currently unsigned, so Windows may show an unknown publisher. See the [validation record](validation.md) for tested behavior and remaining checks.

Shortcut checks cover common reserved system combinations and Windows global hotkey registration. Keys handled internally by another application without global registration cannot all be detected. Choose another combination if it overlaps with an app you use.
