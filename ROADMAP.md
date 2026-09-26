# Roadmap

English · [简体中文](ROADMAP.zh-CN.md)

Released features are listed in the [release notes](https://github.com/liugedragon/screenshot-box/releases); current issues are in [known limitations](docs/limitations.md). The following work is pending, with no fixed release dates.

## Testing and stability

- Verify installed-app startup after Windows sign-in, tray-only launch, Task Manager enable/disable, upgrades, and removal of the startup entry on uninstall.
- Mixed-DPI monitors, negative coordinates, cross-monitor selections, and display connection changes.
- Image pasting into common applications and offline checks on a clean PC without a development SDK.
- Shortcut, focus, recognition, and recovery fixes based on bug reports.
- Measurements of cold starts, OCR memory use, and larger-library search times.

## Feature candidates

- Text annotations, solid masking, and editing a copy after saving.
- Diagnostic exports that exclude user content.
- Faster Chinese queries while retaining original text and bounding-box mappings.

Recording, cloud sync, translation, and semantic search are outside the near-term plan. Submit feature suggestions through [Issues](https://github.com/liugedragon/screenshot-box/issues/new/choose).
