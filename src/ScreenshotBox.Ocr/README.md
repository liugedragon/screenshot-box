# ScreenshotBox.Ocr

English · [简体中文](README.zh-CN.md)

Shared CPU OCR queue using RapidOcrNet 4.2.0 and PP-OCRv5 Chinese mobile models. Windows and Linux use the same dictionary, model version and database job format.

`Enqueue` deduplicates pending item IDs. A single reader reuses one engine with two inference threads and releases it after 90 seconds of inactivity. Results include recognized text, whole-line image boxes, confidence and model version. The database accepts results only if the task generation and active item state still match.

Call `DisposeAsync` before shutting down or restarting. Interrupted processing jobs become pending when the library reopens. `RecognizeAsync` is a lower-level entry point used by isolated probes; do not call it concurrently with an active worker.

The desktop projects retain a direct RapidOcrNet reference because its model-copy targets are not transitive. The Chinese recognition model and dictionary are listed in [sources.json](../../models/chinese/sources.json); download them with `scripts/fetch-models.py` or the Windows PowerShell equivalent. Licenses and upstream responsibilities are documented in [third-party software](../../docs/third-party.md).
