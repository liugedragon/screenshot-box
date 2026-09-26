# User guide

English · [简体中文](zh-CN/usage.md)

## Install and configure

Run the installer and choose a folder, or extract the complete ZIP and open `ScreenshotBox.exe`. The app creates a local library on first launch.

The installation and library folders are separate. In Settings, change the save location to move images, recognized text, and categories. A dedicated subfolder is created if the destination is not empty. The app restarts after migration and retains the original folder.

Settings offers system, light, and dark themes. Language defaults to System: Chinese Windows display languages use Simplified Chinese; other display languages use English. Choose **System**, **简体中文**, or **English** and restart to apply changes. Changing the language does not change the OCR models; the bundled models continue to recognize Chinese and English text.

## Capture

1. Press `Ctrl+Alt+S` and drag a selection.
2. Resize it with eight handles. In Selection mode, drag inside to move it.
3. Add pen strokes, arrows, rectangles, erasing, or mosaic if needed.
4. Press `Enter` or choose Save and copy. The image is saved to the library and clipboard, then you return to your previous app.

Copy only does not create a library entry. Save PNG as lets you choose a file path. `Esc` cancels without creating an image or entry. The output includes annotations; OCR runs in the background.

Settings can record a combination of `Ctrl`, `Alt`, or `Shift` and one non-modifier key, such as `Alt+A`. The app checks common reserved system combinations and Windows global hotkey registration. Failed registration keeps your previous hotkey active. Shortcuts handled internally by another app without global registration may still overlap; choose a different combination in that case.

## Annotation and colors

The toolbar shows options for the current tool. On narrow screens, the Tools menu contains all six modes.

| Tool | Options |
| --- | --- |
| Pen, arrow, outline rectangle | Red, blue, green, yellow, white, and black; 1–32 px line width. |
| Palette | RGB sliders, a six-digit Hex value, and preview; choose Apply color or press `Enter`. |
| Eraser | 4–80 px; removes annotations and restores the original pixels, including mosaic areas. |
| Mosaic | 6–32 px blocks; larger blocks produce more visible pixelation. |

Existing annotations retain their own colors and sizes. Invalid color values show an error. When the palette is open, `Esc` closes it first; press it again to cancel capture.

- `Ctrl+Z`: undo. `Ctrl+Y`: redo. A new annotation clears redo history.
- Clear annotations restores the original and can be undone.
- `B`: pen. `E`: eraser. `M`: mosaic. `V`: selection. In the color field these letters remain text input.

Annotation color, pen width, eraser size, mosaic size, and library thumbnail size persist after restart. Mosaic is visual pixelation and should not be used for information that must remain confidential.

## Import, organize, and search

Open the library from the tray. Drop PNG/JPEG files into it or choose Import for a batch. Failed imports show a reason for each file.

Select a thumbnail to edit its title, notes, tags, or star. Choose Save changes or press `Ctrl+S`. Unsaved edits are marked. In a narrow window, choose Details or press `Enter` to open a separate editor; that item's fields in the main window are disabled while it is open. Closing the editor keeps its draft for the session. Before quitting, backing up, migrating, or restoring, choose save, discard, or cancel for outstanding edits.

Press `Ctrl+F` to search titles, notes, tags, and OCR text. Images still being recognized can already be found by title and notes; image text becomes searchable when recognition completes. Search matches literal substrings, including short Chinese terms. Quotes, percent signs, and underscores are ordinary text.

Separate tags with English or Chinese commas. Sidebar categories match whole tags and can be combined with a keyword. A word in a title or OCR result does not assign that tag. Surrounding spaces and English letter case do not affect category matching.

Deleted images go to the recycle bin and can be restored.

## Preview, copy, and export

Select an image and press `Space`, or double-click it. Matching OCR lines have orange outlines.

| Action | Shortcut |
| --- | --- |
| Zoom | `Ctrl` + mouse wheel |
| Pan | Drag the image |
| Copy image | `Ctrl+C` |
| Fit to window | `Ctrl+0` |
| Actual pixel size | `Ctrl+1` |
| Close preview | `Esc` |

Preview supports tall images; fit mode follows window resizing. Details offers recognized-text copying and image export.

A failed OCR task keeps the image; choose Recognize again to retry. Settings can check originals and recognition errors. If an original is deleted outside the app, its thumbnail may remain visible, but preview and export report an error; restore from backup. A failed save-as or export preserves the existing destination file.

## Back up, restore, and quit

Settings can open the library folder, create a ZIP backup, or restore into a new library directory. A backup contains images, text, categories, and recycle-bin records; protect any private information it contains.

Restore retains the old library and restarts the app on success. Data operations pause during backup, migration, or restore; errors appear in Settings.

Closing the main window leaves the app in the tray. Use Quit from the tray menu to exit completely. Uninstalling preserves settings and the library.

The app and installer are unsigned; Windows may show an unknown publisher. See [validation](validation.md) and [known limitations](limitations.md).
