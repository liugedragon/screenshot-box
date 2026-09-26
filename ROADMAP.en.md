# Roadmap

[简体中文](ROADMAP.md) · English

These are maintenance priorities, not release-date promises. Delivered features are in [0.1.2](https://github.com/liugedragon/screenshot-box/releases/tag/v0.1.2); current gaps are in [limitations](docs/en/limitations.md).

## Testing and stability first

- Manual checks with mixed-DPI monitors, negative coordinates, selections across displays, and monitor connection changes.
- Image pasting into common apps and a complete physically offline run on a clean PC without a development SDK.
- Fix shortcut, focus, recognition failure, and recovery problems reported in actual use.
- Measure cold starts, memory after OCR initialization, and larger-library query times before choosing optimizations.

## Candidates based on feedback

- An English UI. Documentation is bilingual; the UI is still primarily Chinese.
- Text annotations, solid masking, and editing a copy after saving.
- Clearer recognition errors and diagnostic exports that exclude user content by default.
- A measured way to accelerate Chinese queries while retaining original text and bounding-box mappings.

Recording, cloud sync, translation, and semantic search are outside the near-term scope. Feature suggestions should include a use case and current workaround, not just a name.
